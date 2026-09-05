using System.Collections.Generic;
using System.Reflection;
using GBTemplates.Domino.Controller;
using GBTemplates.Domino.Model;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Enforces Brazilian two-player domino rules on top of the original template runtime.
/// This script intentionally does not move board tiles or change HUD/animation layout.
/// </summary>
[DefaultExecutionOrder(32000)]
public sealed class DominoBoneyardHandLimitGuard : MonoBehaviour
{
    [SerializeField] private float checkInterval = 0.04f;

    private static DominoBoneyardHandLimitGuard instance;

    private float nextCheckTime;
    private int lastMatchNumber = -1;
    private int lastMovementCount = -1;
    private int lastP1Count = -1;
    private int lastP2Count = -1;
    private bool distributionRequested;
    private bool openingMoveRequested;
    private bool openingMovePlaced;
    private bool roundFinishRequested;
    private Owner passInProgressOwner = Owner.None;
    private int passInProgressMovementCount = -1;
    private float passGuardUntil;
    private FieldInfo isRoundFinishingField;
    private bool isRoundFinishingFieldSearched;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
        {
            return;
        }

        var runner = new GameObject(nameof(DominoBoneyardHandLimitGuard));
        DontDestroyOnLoad(runner);
        instance = runner.AddComponent<DominoBoneyardHandLimitGuard>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        DominoRoundTransitionGuard.NotifyRoundReset();
        ResetRoundState(true);
    }

    private void LateUpdate()
    {
        if (Time.unscaledTime < nextCheckTime)
        {
            return;
        }

        nextCheckTime = Time.unscaledTime + Mathf.Max(0.02f, checkInterval);
        Tick();
    }

    private void Tick()
    {
        DominoController controller = DependencyCache.DominoController as DominoController;
        IDominoTileCollection collection = DependencyCache.DominoTilesCollections;

        if (controller != null)
        {
            ConfigureTraditionalSettings(controller);
            PreventPrematureFinish(controller, collection);
        }



        if (controller == null || collection == null || controller.IsGamePaused || !controller.IsInMatch)
        {
            ResetRoundState(false);
            return;
        }

        SyncRound(controller, collection);

        if (roundFinishRequested)
        {
            return;
        }

        if (TryFinishEmptyHand(controller, collection))
        {
            return;
        }

        if (collection.MovementsDoneCount == 0)
        {
            if (EnsureAllTilesDistributed(controller, collection))
            {
                return;
            }

            TryPlaceOpeningDouble(controller, collection);
            return;
        }

        CloseBoneyardVisuals();
        ApplyAutomaticPassOrBlock(controller, collection);
    }

    private static void ConfigureTraditionalSettings(DominoController controller)
    {
        DominoSettings settings = controller.Settings;
        if (settings == null)
        {
            return;
        }

        SetSetting(settings, "<StartRule>k__BackingField", FirstMoveRule.HighestDouble);
        SetSetting(settings, "<RequireHighestDoubleFirstMove>k__BackingField", true);
        SetSetting(settings, "<AutoPlayFirstMove>k__BackingField", false);
        SetSetting(settings, "<UseCorrectDoubleScoring>k__BackingField", true);
        SetSetting(settings, "<IgnoreIntermediateDoubles>k__BackingField", false);
    }

    private static void SetSetting(DominoSettings settings, string fieldName, object value)
    {
        FieldInfo field = typeof(DominoSettings).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field != null)
        {
            field.SetValue(settings, value);
        }
    }

    private void SyncRound(DominoController controller, IDominoTileCollection collection)
    {
        int matchNumber = controller.CurrentMatchNumber;
        int movementCount = collection.MovementsDoneCount;

        if (matchNumber != lastMatchNumber || movementCount < lastMovementCount)
        {
            // Se o jogo resetou o tabuleiro MUITO rápido, o layout guard não teve chance de mostrar o popup.
            // Puxamos quem bateu baseado no último frame válido que capturamos!
            if (!DominoRoundTransitionGuard.roundFinishing && lastMovementCount > 0)
            {
                if (lastP1Count >= 0 && lastP1Count <= 1)
                {
                    DominoRoundTransitionGuard.NotifyInstantWin(Owner.Player1);
                }
                else if (lastP2Count >= 0 && lastP2Count <= 1)
                {
                    DominoRoundTransitionGuard.NotifyInstantWin(Owner.Player2);
                }
            }

            DominoRoundTransitionGuard.NotifyRoundReset();
            ResetRoundState(false);
        }

        if (movementCount != lastMovementCount)
        {
            passInProgressOwner = Owner.None;
            passInProgressMovementCount = -1;
            passGuardUntil = 0f;
        }

        lastMatchNumber = matchNumber;
        lastMovementCount = movementCount;
        lastP1Count = GetHandCount(collection, Owner.Player1);
        lastP2Count = GetHandCount(collection, Owner.Player2);
    }

    private bool EnsureAllTilesDistributed(DominoController controller, IDominoTileCollection collection)
    {
        if (collection.BoneyardCount <= 0)
        {
            return false;
        }

        if (distributionRequested)
        {
            return true;
        }

        NetworkManager network = NetworkManager.Singleton;
        if (network != null && network.IsListening && !network.IsServer && !network.IsHost)
        {
            return true;
        }

        DominoTilesColecctionsNetworking concreteCollection = collection as DominoTilesColecctionsNetworking;
        List<DominoTileWorld> boneyard = collection.Boneyard;
        if (boneyard == null || boneyard.Count == 0)
        {
            return false;
        }

        int player1Count = GetHandCount(collection, Owner.Player1);
        int player2Count = GetHandCount(collection, Owner.Player2);
        var tilesToDeal = new List<DominoTileWorld>(boneyard);

        for (int i = 0; i < tilesToDeal.Count; i++)
        {
            if (player1Count >= 7 && player2Count >= 7)
            {
                break;
            }

            DominoTileWorld tile = tilesToDeal[i];
            if (tile == null)
            {
                continue;
            }

            Owner targetOwner;
            if (player1Count < 7 && player2Count < 7)
            {
                targetOwner = player1Count <= player2Count ? Owner.Player1 : Owner.Player2;
            }
            else if (player1Count < 7)
            {
                targetOwner = Owner.Player1;
            }
            else
            {
                targetOwner = Owner.Player2;
            }

            DealBoneyardTile(controller, concreteCollection, targetOwner, tile);

            if (targetOwner == Owner.Player1)
            {
                player1Count++;
            }
            else
            {
                player2Count++;
            }
        }

        distributionRequested = true;
        return true;
    }

    private static void DealBoneyardTile(DominoController controller, DominoTilesColecctionsNetworking collection, Owner owner, DominoTileWorld tile)
    {
        if (collection != null)
        {
            ulong tileId = tile.NetworkObjectId;
            collection.RemoveTileFromBoneyardServerRpc(tileId);
            if (owner == Owner.Player1)
            {
                collection.AddTilePlayer1ServerRpc(tileId);
            }
            else
            {
                collection.AddTilePlayer2ServerRpc(tileId);
            }

            return;
        }

        controller.AddTileFromBoneyard(owner, tile);
    }

    private void TryPlaceOpeningDouble(DominoController controller, IDominoTileCollection collection)
    {
        if (openingMovePlaced || openingMoveRequested || collection.MovementsDoneCount > 0)
        {
            return;
        }

        DominoTileWorld openingTile = GetHighestDoubleTile(collection, out Owner openingOwner);
        if (openingTile == null || openingOwner == Owner.None)
        {
            return;
        }

        if (controller.CurrentTurn != openingOwner)
        {
            SetCurrentTurn(controller, openingOwner);
            return;
        }

        openingMoveRequested = true;
        PlaceFirstTile(controller, openingOwner, openingTile);
        openingMovePlaced = true;
    }

    private static void SetCurrentTurn(DominoController controller, Owner owner)
    {
        if (controller.TurnController == null)
        {
            return;
        }

        controller.TurnController.SetInitTurnServerRpc(owner);

        NetworkManager network = NetworkManager.Singleton;
        if (network == null || network.IsServer || network.IsHost)
        {
            controller.TurnController.CurrentTurn.Value = owner;
        }
    }

    private static void PlaceFirstTile(DominoController controller, Owner owner, DominoTileWorld tile)
    {
        var validation = new TileMovementValidation(
            true,
            TileFacing.Up,
            DirTileUsed.none,
            DirTileUsed.none,
            Vector3.zero,
            tile,
            tile);

        tile.SetTransformPositionClientRpc(Vector3.zero);
        tile.SetOnBoardServerRpc(validation, false, TilePlace.Board);
        tile.SetRotationServerRpc(TileFacing.Up);
        tile.Model.SetPlace(TilePlace.Board);
        tile.SetValueSprite();

        controller.RemoveTileClientRpc(owner, tile.NetworkObjectId);
        controller.AddMovement(tile, tile, owner);
    }

    private void ApplyAutomaticPassOrBlock(DominoController controller, IDominoTileCollection collection)
    {
        Owner currentTurn = controller.CurrentTurn;
        if (currentTurn == Owner.None)
        {
            return;
        }

        bool player1CanPlay = collection.HaveTileToMakePlay(Owner.Player1);
        bool player2CanPlay = collection.HaveTileToMakePlay(Owner.Player2);

        if (!player1CanPlay && !player2CanPlay)
        {
            FinishByLowestHand(controller, collection);
            return;
        }

        bool currentCanPlay = currentTurn == Owner.Player1 ? player1CanPlay : player2CanPlay;
        if (currentCanPlay)
        {
            passInProgressOwner = Owner.None;
            return;
        }

        bool isRetry = (passInProgressOwner == currentTurn && passInProgressMovementCount == collection.MovementsDoneCount);

        if (isRetry && Time.unscaledTime < passGuardUntil)
        {
            return;
        }

        if (!isRetry && currentTurn == Owner.Player1)
        {
            int currentCoins = UnityEngine.PlayerPrefs.GetInt("Coins", 0);
            int newCoins = currentCoins - 50;
            if (newCoins < 0) newCoins = 0;
            UnityEngine.PlayerPrefs.SetInt("Coins", newCoins);
            UnityEngine.PlayerPrefs.Save();
            CoinFlowFeedback.ShowPassCost(50);
        }

        passInProgressOwner = currentTurn;
        passInProgressMovementCount = collection.MovementsDoneCount;
        passGuardUntil = Time.unscaledTime + 0.75f;

        controller.ReportMovementDone(currentTurn);
    }

    private bool TryFinishEmptyHand(DominoController controller, IDominoTileCollection collection)
    {
        if (collection.MovementsDoneCount <= 0)
        {
            return false;
        }

        if (GetHandCount(collection, Owner.Player1) == 0)
        {
            return HoldNativeEmptyHandFinish();
        }

        if (GetHandCount(collection, Owner.Player2) == 0)
        {
            return HoldNativeEmptyHandFinish();
        }

        return false;
    }

    private bool HoldNativeEmptyHandFinish()
    {
        if (roundFinishRequested)
        {
            return true;
        }

        roundFinishRequested = true;
        CloseBoneyardVisuals();
        DominoRoundTransitionGuard.NotifyRoundFinishing();
        return true;
    }

    private void FinishByLowestHand(DominoController controller, IDominoTileCollection collection)
    {
        int player1Total = GetHandTotal(collection, Owner.Player1);
        int player2Total = GetHandTotal(collection, Owner.Player2);
        Owner winner;

        if (player1Total != player2Total)
        {
            winner = player1Total < player2Total ? Owner.Player1 : Owner.Player2;
        }
        else
        {
            int player1Count = GetHandCount(collection, Owner.Player1);
            int player2Count = GetHandCount(collection, Owner.Player2);
            if (player1Count != player2Count)
            {
                winner = player1Count < player2Count ? Owner.Player1 : Owner.Player2;
            }
            else
            {
                // No draw state: if totals and counts are identical, the last player
                // who made a valid move wins. CurrentTurn points to the blocked player.
                winner = controller.CurrentTurn == Owner.Player1 ? Owner.Player2 : Owner.Player1;
            }
        }

        FinishRound(controller, winner);
    }

    private void FinishRound(DominoController controller, Owner winner)
    {
        if (roundFinishRequested)
        {
            return;
        }

        roundFinishRequested = true;
        CloseBoneyardVisuals();
        DominoRoundTransitionGuard.NotifyRoundFinishing(winner);
        controller.FinishRound(winner);
    }

    private static DominoTileWorld GetHighestDoubleTile(IDominoTileCollection collection, out Owner owner)
    {
        owner = Owner.None;
        DominoTileWorld best = null;
        int bestValue = -1;

        CheckHighestDouble(collection.GetPlayerDeck(Owner.Player1), Owner.Player1, ref best, ref owner, ref bestValue);
        CheckHighestDouble(collection.GetPlayerDeck(Owner.Player2), Owner.Player2, ref best, ref owner, ref bestValue);

        return best;
    }

    private static void CheckHighestDouble(List<DominoTileWorld> tiles, Owner candidateOwner, ref DominoTileWorld best, ref Owner bestOwner, ref int bestValue)
    {
        if (tiles == null)
        {
            return;
        }

        for (int i = 0; i < tiles.Count; i++)
        {
            DominoTileWorld tile = tiles[i];
            if (tile == null || tile.Model == null || !tile.Model.IsEqualValue)
            {
                continue;
            }

            int value = tile.Model.EqualValue;
            if (value <= bestValue)
            {
                continue;
            }

            best = tile;
            bestOwner = candidateOwner;
            bestValue = value;
        }
    }

    private static int GetHandCount(IDominoTileCollection collection, Owner owner)
    {
        List<DominoTileWorld> deck = collection.GetPlayerDeck(owner);
        return deck != null ? deck.Count : 0;
    }

    private static int GetHandTotal(IDominoTileCollection collection, Owner owner)
    {
        List<DominoTileWorld> deck = collection.GetPlayerDeck(owner);
        if (deck == null)
        {
            return 0;
        }

        int total = 0;
        for (int i = 0; i < deck.Count; i++)
        {
            DominoTileWorld tile = deck[i];
            if (tile != null && tile.Model != null)
            {
                total += tile.Model.TotalValue;
            }
        }

        return total;
    }

    private static void CloseBoneyardVisuals()
    {
        IBoneyardView boneyard = DependencyCache.BoneyardView;
        if (boneyard != null)
        {
            boneyard.SetAllowTakeTiles(false);
            boneyard.Close();
        }

        ITilesUICollectionsView tilesView = DependencyCache.TilesUICollectionsView;
        if (tilesView != null)
        {
            tilesView.RevealBoneyardTiles(false);
        }

        SetBoneyardUiHidden(true);
    }

    private static void SetBoneyardUiHidden(bool hidden)
    {
        GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < objects.Length; i++)
        {
            GameObject go = objects[i];
            if (go == null || go.name != "Boneyard - UI" || !go.scene.IsValid())
            {
                continue;
            }

            CanvasGroup group = go.GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = go.AddComponent<CanvasGroup>();
            }

            group.alpha = hidden ? 0f : 1f;
            group.interactable = !hidden;
            group.blocksRaycasts = !hidden;
        }
    }

    private void ResetRoundState(bool resetMatchTracking)
    {
        nextCheckTime = 0f;
        distributionRequested = false;
        openingMoveRequested = false;
        openingMovePlaced = false;
        roundFinishRequested = false;
        passInProgressOwner = Owner.None;
        passInProgressMovementCount = -1;
        passGuardUntil = 0f;

        if (resetMatchTracking)
        {
            lastMatchNumber = -1;
            lastMovementCount = -1;
        }
    }

    /// <summary>
    /// If the DLL internally set _isRoundFinishing=true but at least one player still has valid moves,
    /// forcefully revert it. The round should only end by block when BOTH players are stuck.
    /// </summary>
    private void PreventPrematureFinish(DominoController controller, IDominoTileCollection collection)
    {
        if (controller == null || collection == null)
        {
            return;
        }

        if (!isRoundFinishingFieldSearched)
        {
            isRoundFinishingFieldSearched = true;
            isRoundFinishingField = typeof(DominoController).GetField("_isRoundFinishing", BindingFlags.Instance | BindingFlags.NonPublic);
        }

        if (isRoundFinishingField == null)
        {
            return;
        }

        object value = isRoundFinishingField.GetValue(controller);
        if (!(value is bool isFinishing) || !isFinishing)
        {
            return;
        }

        // If a player already emptied their hand, this is a legitimate finish ("bateu"). Don't interfere.
        int player1Count = GetHandCount(collection, Owner.Player1);
        int player2Count = GetHandCount(collection, Owner.Player2);
        if (player1Count == 0 || player2Count == 0)
        {
            return;
        }

        // Check if at least one player can still play.
        bool player1CanPlay = collection.HaveTileToMakePlay(Owner.Player1);
        bool player2CanPlay = collection.HaveTileToMakePlay(Owner.Player2);

        if (player1CanPlay || player2CanPlay)
        {
            // The DLL is trying to end the round prematurely! Block it.
            isRoundFinishingField.SetValue(controller, false);
            UnityEngine.Debug.Log("[DominoBoneyardHandLimitGuard] Prevented premature round finish — at least one player can still play.");
        }
    }
}
