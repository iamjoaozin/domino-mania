using System.Collections.Generic;
using System.Reflection;
using DG.Tweening;
using GBTemplates.Domino.Controller;
using GBTemplates.Domino.Model;
using GBTemplates.Domino.View;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Suspends custom tile layout while the original template runs the round-end scoring animation.
/// This prevents hand/board layout scripts from fighting the "collect tiles to center" transition.
/// </summary>
public static class DominoRoundTransitionGuard
{
    private const float FinishSuspendSeconds = 5f;
    private const float ShortAudioMaxLength = 2.5f;
    private const int MatchWinCoins = 1000;
    private const int MatchLossCoins = 1000;

    public static bool roundFinishing;
    public static bool roundEndCleanupDone;
    private static float suspendUntil;
    private static float lastCleanupTime = -999f;
    private static FieldInfo controllerRoundFinishingField;
    private static bool controllerRoundFinishingFieldSearched;

    // Cache para Reflection dos scores
    private static FieldInfo p1ScoreField;
    private static FieldInfo p2ScoreField;
    private static bool scoreFieldsSearched;

    private static readonly List<CanvasGroupState> hiddenCanvasGroups = new List<CanvasGroupState>(32);
    private static readonly List<RendererState> hiddenRenderers = new List<RendererState>(32);
    private static readonly List<Collider2DState> hiddenCollider2Ds = new List<Collider2DState>(32);
    private static readonly HashSet<int> hiddenCanvasGroupIds = new HashSet<int>();
    private static readonly HashSet<int> hiddenRendererIds = new HashSet<int>();
    private static readonly HashSet<int> hiddenCollider2DIds = new HashSet<int>();

    private struct CanvasGroupState
    {
        public CanvasGroup Group;
        public float Alpha;
        public bool Interactable;
        public bool BlocksRaycasts;
        public bool Created;

        public CanvasGroupState(CanvasGroup group, bool created)
        {
            Group = group;
            Alpha = group.alpha;
            Interactable = group.interactable;
            BlocksRaycasts = group.blocksRaycasts;
            Created = created;
        }
    }

    private struct RendererState
    {
        public Renderer Renderer;
        public bool Enabled;

        public RendererState(Renderer renderer)
        {
            Renderer = renderer;
            Enabled = renderer.enabled;
        }
    }

    private struct Collider2DState
    {
        public Collider2D Collider;
        public bool Enabled;

        public Collider2DState(Collider2D collider)
        {
            Collider = collider;
            Enabled = collider.enabled;
        }
    }

    public static void NotifyRoundFinishing(Owner resolvedWinner = Owner.None)
    {
        roundFinishing = true;
        suspendUntil = Mathf.Max(suspendUntil, Time.unscaledTime + FinishSuspendSeconds);

        // Run the heavy cleanup ONCE (not every frame) to avoid the visual/audio loop.
        if (!roundEndCleanupDone)
        {
            roundEndCleanupDone = true;
            KillTileTweens();
            StopAllTileAudio();
            ShowWinnerPopup(resolvedWinner);
        }

        HoldRoundEndVisuals();
    }

    public static void NotifyRoundReset()
    {
        bool wasSuspended = roundFinishing || Time.unscaledTime < suspendUntil;
        roundFinishing = false;
        roundEndCleanupDone = false;
        suspendUntil = 0f;

        // Force native _isRoundFinishing to false to prevent bug in 2nd match
        DominoController controller = DependencyCache.DominoController as DominoController;
        if (controller != null)
        {
            if (controllerRoundFinishingField == null)
            {
                controllerRoundFinishingField = controller.GetType().GetField("_isRoundFinishing", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            }
            if (controllerRoundFinishingField != null)
            {
                controllerRoundFinishingField.SetValue(controller, false);
            }
        }

        // Kill any remaining tweens BEFORE restoring visuals, to prevent flicker.
        if (wasSuspended)
        {
            KillTileTweens();
            StopAllTileAudio();
        }

        RestoreHiddenRoundEndVisuals();

        // Notify the board limiter to clear its cached state for the new round.
        DominoBoardBoundsLimiter.ClearCachedState();

        // Reset responsive hand layout scales for the new round.
        DominoHandResponsiveLayout.ResetScale();
    }

    public static bool ShouldSuspendCustomTileLayout()
    {
        if (roundFinishing || Time.unscaledTime < suspendUntil)
        {
            HoldRoundEndVisuals();
            return true;
        }

        DominoController controller = DependencyCache.DominoController as DominoController;
        IDominoTileCollection collection = DependencyCache.DominoTilesCollections;
        if (controller == null || collection == null || controller.IsGamePaused || !controller.IsInMatch)
        {
            return true;
        }

        // HACK: Zera os scores da partida constatemente para garantir que a partida NUNCA termine (Partida Infinita)
        if (!scoreFieldsSearched)
        {
            scoreFieldsSearched = true;
            p1ScoreField = controller.GetType().GetField("player1Score", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            p2ScoreField = controller.GetType().GetField("player2Score", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        }
        if (p1ScoreField != null) p1ScoreField.SetValue(controller, 0);
        if (p2ScoreField != null) p2ScoreField.SetValue(controller, 0);

        // Proteção essencial: se ainda não houve movimentos, a rodada acabou de ser limpa ou está distribuindo peças.
        if (collection.MovementsDoneCount <= 0)
        {
            return false;
        }

        if (IsControllerRoundFinishing(controller))
        {
            NotifyRoundFinishing();
            HoldRoundEndVisuals();
            return true;
        }

        int player1Count = GetHandCount(collection, Owner.Player1);
        int player2Count = GetHandCount(collection, Owner.Player2);
        if (player1Count == 0 || player2Count == 0)
        {
            NotifyRoundFinishing();
            HoldRoundEndVisuals();
            return true;
        }

        if (collection.BoneyardCount <= 0 &&
            !collection.HaveTileToMakePlay(Owner.Player1) &&
            !collection.HaveTileToMakePlay(Owner.Player2))
        {
            NotifyRoundFinishing();
            HoldRoundEndVisuals();
            return true;
        }

        return false;
    }

    private static bool IsControllerRoundFinishing(DominoController controller)
    {
        if (!controllerRoundFinishingFieldSearched)
        {
            controllerRoundFinishingFieldSearched = true;
            controllerRoundFinishingField = typeof(DominoController).GetField("_isRoundFinishing", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        }
        if (controllerRoundFinishingField != null)
        {
            return (bool)controllerRoundFinishingField.GetValue(controller);
        }
        return false;
    }

    private static void HoldRoundEndVisuals()
    {
        HideRemainingHandVisuals();
        StopShortTileAudio();
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

    private static void CleanupStaleTileMotionAndClicks()
    {
        if (Time.unscaledTime - lastCleanupTime < 0.35f)
        {
            return;
        }

        lastCleanupTime = Time.unscaledTime;
        KillTileTweens();
        StopShortTileAudio();
    }

    private static void HideRemainingHandVisuals()
    {
        IDominoTileCollection collection = DependencyCache.DominoTilesCollections;
        HideWorldHandDeck(collection, Owner.Player1);
        HideWorldHandDeck(collection, Owner.Player2);

        ITilesUICollectionsView tilesView = DependencyCache.TilesUICollectionsView;
        HideUiHandTiles(tilesView != null ? tilesView.TilesUIBottom : null);
        HideUiHandTiles(tilesView != null ? tilesView.TilesUIUpper : null);
    }

    private static void HideWorldHandDeck(IDominoTileCollection collection, Owner owner)
    {
        if (collection == null)
        {
            return;
        }

        List<DominoTileWorld> deck = collection.GetPlayerDeck(owner);
        if (deck == null)
        {
            return;
        }

        for (int i = 0; i < deck.Count; i++)
        {
            DominoTileWorld tile = deck[i];
            if (tile == null || tile.Model == null || tile.Model.Place == TilePlace.Board)
            {
                continue;
            }

            HideWorldTile(tile);
        }
    }

    private static void HideWorldTile(DominoTileWorld tile)
    {
        Renderer[] renderers = tile.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            int id = renderer.GetInstanceID();
            if (hiddenRendererIds.Add(id))
            {
                hiddenRenderers.Add(new RendererState(renderer));
            }

            renderer.enabled = false;
        }

        Collider2D[] colliders = tile.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D collider = colliders[i];
            if (collider == null)
            {
                continue;
            }

            int id = collider.GetInstanceID();
            if (hiddenCollider2DIds.Add(id))
            {
                hiddenCollider2Ds.Add(new Collider2DState(collider));
            }

            collider.enabled = false;
        }
    }

    private static void HideUiHandTiles(List<Transform> tiles)
    {
        if (tiles == null)
        {
            return;
        }

        for (int i = 0; i < tiles.Count; i++)
        {
            Transform tile = tiles[i];
            if (tile == null)
            {
                continue;
            }

            HideCanvasTransform(tile);
            HideHandSlot(tile.parent, tile.GetInstanceID());
        }
    }

    private static void HideHandSlot(Transform parent, int tileInstanceId)
    {
        if (parent == null)
        {
            return;
        }

        string exactSlotName = "Lara Hand Tile Slot " + tileInstanceId;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child == null)
            {
                continue;
            }

            string childName = child.name;
            if (childName == exactSlotName || childName.StartsWith("Lara Hand Tile Slot"))
            {
                HideCanvasTransform(child);
            }
        }
    }

    private static void HideCanvasTransform(Transform transform)
    {
        if (transform == null)
        {
            return;
        }

        CanvasGroup group = transform.GetComponent<CanvasGroup>();
        bool created = false;
        if (group == null)
        {
            group = transform.gameObject.AddComponent<CanvasGroup>();
            created = true;
        }

        int id = group.GetInstanceID();
        if (hiddenCanvasGroupIds.Add(id))
        {
            hiddenCanvasGroups.Add(new CanvasGroupState(group, created));
        }

        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
    }

    private static void RestoreHiddenRoundEndVisuals()
    {
        for (int i = 0; i < hiddenCanvasGroups.Count; i++)
        {
            CanvasGroupState state = hiddenCanvasGroups[i];
            if (state.Group == null)
            {
                continue;
            }

            if (state.Created)
            {
                UnityEngine.Object.Destroy(state.Group);
                continue;
            }

            state.Group.alpha = state.Alpha;
            state.Group.interactable = state.Interactable;
            state.Group.blocksRaycasts = state.BlocksRaycasts;
        }

        for (int i = 0; i < hiddenRenderers.Count; i++)
        {
            RendererState state = hiddenRenderers[i];
            if (state.Renderer != null)
            {
                state.Renderer.enabled = state.Enabled;
            }
        }

        for (int i = 0; i < hiddenCollider2Ds.Count; i++)
        {
            Collider2DState state = hiddenCollider2Ds[i];
            if (state.Collider != null)
            {
                state.Collider.enabled = state.Enabled;
            }
        }

        hiddenCanvasGroups.Clear();
        hiddenRenderers.Clear();
        hiddenCollider2Ds.Clear();
        hiddenCanvasGroupIds.Clear();
        hiddenRendererIds.Clear();
        hiddenCollider2DIds.Clear();
    }

    private static void KillTileTweens()
    {
        DominoTileWorld[] worldTiles = Resources.FindObjectsOfTypeAll<DominoTileWorld>();
        for (int i = 0; i < worldTiles.Length; i++)
        {
            DominoTileWorld tile = worldTiles[i];
            if (tile == null || !tile.gameObject.scene.IsValid() || !tile.gameObject.scene.isLoaded)
            {
                continue;
            }

            tile.transform.DOKill(false);
        }

        ITilesUICollectionsView tilesView = DependencyCache.TilesUICollectionsView;
        KillUiTileTweens(tilesView != null ? tilesView.TilesUIBottom : null);
        KillUiTileTweens(tilesView != null ? tilesView.TilesUIUpper : null);
    }

    private static void KillUiTileTweens(List<Transform> tiles)
    {
        if (tiles == null)
        {
            return;
        }

        for (int i = 0; i < tiles.Count; i++)
        {
            Transform tile = tiles[i];
            if (tile == null || !tile.gameObject.scene.IsValid() || !tile.gameObject.scene.isLoaded)
            {
                continue;
            }

            tile.DOKill(false);
        }
    }

    private static void StopShortTileAudio()
    {
        AudioSource[] sources = Resources.FindObjectsOfTypeAll<AudioSource>();
        for (int i = 0; i < sources.Length; i++)
        {
            AudioSource source = sources[i];
            if (source == null || !source.gameObject.scene.IsValid() || !source.gameObject.scene.isLoaded || !source.isPlaying)
            {
                continue;
            }

            AudioClip clip = source.clip;
            if (clip != null && clip.length <= ShortAudioMaxLength)
            {
                source.Stop();
            }
        }
    }

    /// <summary>
    /// Aggressively stops ALL audio sources on tile-related objects.
    /// Used at round end and round reset to prevent the clicking loop.
    /// </summary>
    private static void StopAllTileAudio()
    {
        AudioSource[] sources = Resources.FindObjectsOfTypeAll<AudioSource>();
        for (int i = 0; i < sources.Length; i++)
        {
            AudioSource source = sources[i];
            if (source == null || !source.gameObject.scene.IsValid() || !source.gameObject.scene.isLoaded)
            {
                continue;
            }

            if (!source.isPlaying)
            {
                continue;
            }

            // Stop any audio on objects that have a DominoTileWorld component or are child of one.
            DominoTileWorld tileWorld = source.GetComponentInParent<DominoTileWorld>();
            if (tileWorld != null)
            {
                source.Stop();
                continue;
            }

            // Also stop any short audio anywhere (tile click sounds, etc.)
            AudioClip clip = source.clip;
            if (clip != null && clip.length <= ShortAudioMaxLength)
            {
                source.Stop();
            }
        }
    }

    public static void NotifyInstantWin(Owner winner)
    {
        roundFinishing = true;
        suspendUntil = Mathf.Max(suspendUntil, Time.unscaledTime + FinishSuspendSeconds);

        if (!roundEndCleanupDone)
        {
            roundEndCleanupDone = true;
            KillTileTweens();
            StopAllTileAudio();
            ShowInstantWinnerPopup(winner);
        }
    }

    private static void ShowInstantWinnerPopup(Owner winner)
    {
        string resultText = "";
        int coinsReward = 0;
        int coinsPenalty = 0;
        string reasonText = "MOTIVO: BATEU!";

        if (winner == Owner.Player1)
        {
            resultText = "VOCE VENCEU!";
            coinsReward = MatchWinCoins;
        }
        else if (winner == Owner.Player2)
        {
            resultText = "OPONENTE VENCEU!";
            coinsPenalty = MatchLossCoins;
        }

        SpawnPopup(resultText, reasonText, coinsReward, coinsPenalty);
    }

    private static void ShowWinnerPopup(Owner resolvedWinner = Owner.None)
    {
        IDominoTileCollection collection = DependencyCache.DominoTilesCollections;
        if (collection == null) return;

        // Protecao contra disparos falsos de 'fim de rodada' quando a mao esta vazia ao iniciar o jogo
        if (collection.MovementsDoneCount == 0)
        {
            return;
        }

        int p1Count = GetHandCount(collection, Owner.Player1);
        int p2Count = GetHandCount(collection, Owner.Player2);
        int p1Total = GetHandTotal(collection, Owner.Player1);
        int p2Total = GetHandTotal(collection, Owner.Player2);

        Owner winner = resolvedWinner;
        if (winner == Owner.None)
        {
            if (p1Count == 0 && p2Count > 0)
            {
                winner = Owner.Player1;
            }
            else if (p2Count == 0 && p1Count > 0)
            {
                winner = Owner.Player2;
            }
            else if (p1Total != p2Total)
            {
                winner = p1Total < p2Total ? Owner.Player1 : Owner.Player2;
            }
            else if (p1Count != p2Count)
            {
                winner = p1Count < p2Count ? Owner.Player1 : Owner.Player2;
            }
            else
            {
                DominoController controller = DependencyCache.DominoController as DominoController;
                winner = controller != null && controller.CurrentTurn == Owner.Player1
                    ? Owner.Player2
                    : Owner.Player1;
            }
        }

        string resultText;
        int coinsReward = 0;
        int coinsPenalty = 0;
        string reasonText;

        if (p1Count == 0 || p2Count == 0)
        {
            reasonText = "MOTIVO: BATEU!";
        }
        else
        {
            reasonText = "MOTIVO: JOGO TRANCADO (MENOR SOMA)";
        }

        if (winner == Owner.Player1)
        {
            resultText = "VOCE VENCEU!";
            coinsReward = MatchWinCoins;
        }
        else
        {
            resultText = "OPONENTE VENCEU!";
            coinsPenalty = MatchLossCoins;
        }

        SpawnPopup(resultText, reasonText, coinsReward, coinsPenalty);
    }

    private static void SpawnPopup(string resultText, string reasonText, int coinsReward, int coinsPenalty)
    {
        RoundRestartNotice.RemoveLegacyResultPopups();

        if (coinsReward > 0)
        {
            int currentCoins = PlayerPrefs.GetInt("Coins", 0);
            PlayerPrefs.SetInt("Coins", currentCoins + coinsReward);
            PlayerPrefs.Save();
            CoinFlowFeedback.ShowMatchGain(coinsReward);
        }
        else if (coinsPenalty > 0)
        {
            int currentCoins = PlayerPrefs.GetInt("Coins", 0);
            int newCoins = currentCoins - coinsPenalty;
            if (newCoins < 0) newCoins = 0;
            PlayerPrefs.SetInt("Coins", newCoins);
            PlayerPrefs.Save();
            CoinFlowFeedback.ShowMatchLoss(coinsPenalty);
        }

    }
}
