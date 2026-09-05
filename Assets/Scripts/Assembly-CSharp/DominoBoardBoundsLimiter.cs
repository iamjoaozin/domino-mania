using System.Collections.Generic;
using System.Reflection;
using GBTemplates.Domino.Controller;
using GBTemplates.Domino.Model;
using GBTemplates.Domino.View;
using UnityEngine;
using UnityEngine.SceneManagement;
using DG.Tweening;

/// <summary>
/// Runtime visual limiter for board dominoes.
/// Keeps placed world tiles inside an invisible portrait-safe rectangle.
/// Each open end grows vertically and snakes through short horizontal turns at the limits.
/// </summary>
[DefaultExecutionOrder(31000)]
public sealed class DominoBoardBoundsLimiter : MonoBehaviour
{
    [Header("Board Bounds")]
    [SerializeField] private float leftLimit = -3.75f;
    [SerializeField] private float rightLimit = 3.75f;
    [SerializeField] private float topLimit = 4.75f;
    [SerializeField] private float bottomLimit = -5.75f;
    [SerializeField] private Vector2 boardVisualOffset = new Vector2(0f, -0.45f);

    [Header("Flow")]
    [SerializeField] private float pieceGap = 0.025f;
    [SerializeField] private float visualFootprintPadding = 0.025f;
    [SerializeField] private int horizontalPiecesOnTurn = 2;
    [SerializeField] private float maximumTableScale = 1.12f;
    [SerializeField] private float minimumTableScale = 0.34f;
    [SerializeField] private int shrinkStartsAtTileCount = 10;
    [SerializeField] private int shrinkFullAtTileCount = 28;
    [SerializeField] private bool autoShrinkToBounds = true;

    [Header("Placement Feedback")]
    [SerializeField] private bool showPlacementHints = true;
    [SerializeField] private float placementDropHeight = 0.42f;
    [SerializeField] private float placementAnimationDuration = 0.32f;
    [SerializeField, Range(0.5f, 1f)] private float placementStartScale = 0.78f;
    [SerializeField] private float placementImpactScale = 1.08f;
    [SerializeField] private float placementSettleDepth = 0.045f;
    [SerializeField] private float hintOutlinePadding = 0.055f;
    [SerializeField] private Color hintColor = new Color(0.35f, 1f, 0.16f, 0.92f);

    [Header("Camera")]
    [SerializeField] private bool lockCameraBoardFollow = true;

    [Header("Visible Limit")]
    [SerializeField] private bool showVisibleBounds = false;
    [SerializeField] private float boundsLineWidth = 0.035f;
    [SerializeField] private Color boundsColor = new Color(1f, 0.74f, 0.02f, 0.95f);

    private const string BoardRootName = "TileRootWorld - Board";
    private static DominoBoardBoundsLimiter instance;

    private readonly Dictionary<DominoTileWorld, Vector3> baseScales = new Dictionary<DominoTileWorld, Vector3>();
    private readonly Dictionary<DominoTileWorld, TileFacing> baseFacings = new Dictionary<DominoTileWorld, TileFacing>();
    private readonly Dictionary<DominoTileWorld, List<DominoTileWorld>> adjacency = new Dictionary<DominoTileWorld, List<DominoTileWorld>>();
    private readonly Dictionary<DominoTileWorld, DominoTileWorld> parentByTile = new Dictionary<DominoTileWorld, DominoTileWorld>();
    private readonly Dictionary<DominoTileWorld, int> orderByTile = new Dictionary<DominoTileWorld, int>();
    private readonly List<DominoTileWorld> boardTiles = new List<DominoTileWorld>(28);
    private readonly List<Placement> placements = new List<Placement>(28);
    private readonly List<DominoTileWorld> rightChain = new List<DominoTileWorld>(14);
    private readonly List<DominoTileWorld> leftChain = new List<DominoTileWorld>(14);
    private readonly List<DominoTileWorld> scratchTiles = new List<DominoTileWorld>(28);
    private readonly HashSet<DominoTileWorld> animatedTiles = new HashSet<DominoTileWorld>();
    private readonly Dictionary<DominoTileWorld, float> placementAnimationEnds = new Dictionary<DominoTileWorld, float>();

    private Transform boardRoot;
    private Transform tilePreviewTransform;
    private LineRenderer boundsLine;
    private Material boundsMaterial;
    private readonly LineRenderer[] hintLines = new LineRenderer[2];
    private readonly LineRenderer[] hintGlowLines = new LineRenderer[2];
    private Material hintMaterial;
    private CameraController cameraController;
    private FieldInfo cameraFollowField;
    private float nextLayoutTime;
    private int rightChainCount;
    private Vector3 frozenCameraPosition;
    private Quaternion frozenCameraRotation;
    private float frozenOrthoSize;
    private float frozenFOV;
    private bool cameraFrozen;
    private ChainCursor upperCursor;
    private ChainCursor lowerCursor;
    private int lastHintBoardTileCount = -1;
    private bool suppressHintsUntilPreviewHidden;
    private bool userSelectionArmed;
    private bool wasPreviewSelected;
    private FieldInfo tileViewDragField;
    private FieldInfo dragTileViewField;

    private enum FlowDirection
    {
        Up,
        Down,
        Right,
        Left
    }

    private struct Placement
    {
        public Vector2 Position;
        public TileFacing Facing;

        public Placement(Vector2 position, TileFacing facing)
        {
            Position = position;
            Facing = facing;
        }
    }

    private struct ChainCursor
    {
        public Vector2 Position;
        public TileFacing Facing;
        public FlowDirection Direction;
        public int VerticalSign;
        public int HorizontalSign;
        public int HorizontalRemaining;
        public bool IsValid;

        public ChainCursor(Vector2 position, TileFacing facing, FlowDirection direction, int verticalSign, int horizontalSign, int horizontalRemaining)
        {
            Position = position;
            Facing = facing;
            Direction = direction;
            VerticalSign = verticalSign;
            HorizontalSign = horizontalSign;
            HorizontalRemaining = horizontalRemaining;
            IsValid = true;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
        {
            return;
        }

        var runner = new GameObject(nameof(DominoBoardBoundsLimiter));
        DontDestroyOnLoad(runner);
        instance = runner.AddComponent<DominoBoardBoundsLimiter>();
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
        RefreshSceneReferences();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SetPlacementHintsVisible(false);
    }

    private void LateUpdate()
    {
        LockCameraFollowIfNeeded();
        DrawBounds();

        if (Time.unscaledTime < nextLayoutTime)
        {
            return;
        }

        nextLayoutTime = Time.unscaledTime + 0.05f;
        if (DominoRoundTransitionGuard.ShouldSuspendCustomTileLayout())
        {
            SetPlacementHintsVisible(false);
            return;
        }

        ApplyBoundedFlow();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        animatedTiles.Clear();
        placementAnimationEnds.Clear();
        SetPlacementHintsVisible(false);
        RefreshSceneReferences();
        nextLayoutTime = 0f;
    }

    private void RefreshSceneReferences()
    {
        var boardObject = GameObject.Find(BoardRootName);
        boardRoot = boardObject != null ? boardObject.transform : null;
        var previewObject = GameObject.Find("Tile -Preview");
        tilePreviewTransform = previewObject != null ? previewObject.transform : null;
        HideLegacyTilePreviewVisuals();
        RefreshCameraReference();
        EnsureBoundsLine();
    }

    private void RefreshCameraReference()
    {
        cameraController = null;
        CameraController[] cameras = Resources.FindObjectsOfTypeAll<CameraController>();
        foreach (CameraController candidate in cameras)
        {
            if (candidate != null && candidate.gameObject.scene.IsValid() && candidate.gameObject.scene.isLoaded)
            {
                cameraController = candidate;
                break;
            }
        }

        cameraFollowField = typeof(CameraController).GetField("_followBoardDuringMatch", BindingFlags.Instance | BindingFlags.NonPublic);
    }

    private void LockCameraFollowIfNeeded()
    {
        if (!lockCameraBoardFollow)
        {
            return;
        }

        if (cameraController == null || !cameraController.gameObject.scene.IsValid())
        {
            RefreshCameraReference();
            cameraFrozen = false;
        }

        if (cameraController == null)
        {
            return;
        }

        var camGO = cameraController.gameObject;

        // Disable every MonoBehaviour on the camera GameObject so nothing can shake/move it.
        if (!cameraFrozen)
        {
            var behaviours = camGO.GetComponents<MonoBehaviour>();
            foreach (var b in behaviours)
            {
                if (b != null && b.enabled)
                {
                    b.enabled = false;
                }
            }

            // Capture the clean resting position once.
            var cam = camGO.GetComponent<Camera>();
            frozenCameraPosition = camGO.transform.position;
            frozenCameraRotation = camGO.transform.rotation;

            // Set a fixed zoom out level to make pieces smaller, overriding the too-zoomed-in 2.2 default
            frozenOrthoSize = 9.3f;
            cam.orthographicSize = 9.3f;
            frozenFOV = cam.fieldOfView;
            
            // Kill any active DOTween animations on the camera.
            camGO.transform.DOKill();
            DOTween.Kill(cam);
            
            cameraFrozen = true;
            UnityEngine.Debug.Log("[DominoBoardBoundsLimiter] Camera fully frozen – all MonoBehaviours disabled and tweens killed.");
        }

        // Hard-lock transform every frame in case anything managed to move it.
        var t = camGO.transform;
        if (t.position != frozenCameraPosition || t.rotation != frozenCameraRotation)
        {
            t.position = frozenCameraPosition;
            t.rotation = frozenCameraRotation;
        }

        var currentCam = camGO.GetComponent<Camera>();
        if (Mathf.Abs(currentCam.orthographicSize - frozenOrthoSize) > 0.0001f)
        {
            currentCam.orthographicSize = frozenOrthoSize;
        }
        if (Mathf.Abs(currentCam.fieldOfView - frozenFOV) > 0.0001f)
        {
            currentCam.fieldOfView = frozenFOV;
        }
    }

    private void ApplyBoundedFlow()
    {
        CollectBoardTiles();
        if (boardTiles.Count == 0)
        {
            // Board is empty — likely between rounds. Clear stale cached data.
            SetPlacementHintsVisible(false);
            ClearCachedState();
            return;
        }

        EnsureTileBaseScales();
        if (!TryGetTileFootprint(out float shortSize, out float longSize))
        {
            return;
        }

        float fitScale = FindBestFitScale(shortSize, longSize);
        float scaledShort = shortSize * fitScale;
        float scaledLong = longSize * fitScale;

        BuildPlacements(scaledShort, scaledLong, placements);
        ApplyPlacements(fitScale, scaledShort, scaledLong);
        UpdatePlacementHints(scaledShort, scaledLong);
    }

    private void CollectBoardTiles()
    {
        boardTiles.Clear();

        IDominoTileCollection collection = FindTileCollection();
        if (collection != null && collection.MovementsDone != null && collection.MovementsDone.Count > 0)
        {
            foreach (DominoTileWorld tile in collection.MovementsDone)
            {
                if (IsUsableBoardTile(tile) && !boardTiles.Contains(tile))
                {
                    boardTiles.Add(tile);
                }
            }
        }

        if (boardTiles.Count > 0)
        {
            OrderBoardTilesByConnections();
            return;
        }

        if (boardRoot == null)
        {
            RefreshSceneReferences();
        }

        if (boardRoot == null)
        {
            return;
        }

        DominoTileWorld[] children = boardRoot.GetComponentsInChildren<DominoTileWorld>(true);
        foreach (DominoTileWorld tile in children)
        {
            if (IsUsableBoardTile(tile) && !boardTiles.Contains(tile))
            {
                boardTiles.Add(tile);
            }
        }

        OrderBoardTilesByConnections();
    }

    private static IDominoTileCollection FindTileCollection()
    {
        MonoBehaviour[] behaviours = Resources.FindObjectsOfTypeAll<MonoBehaviour>();
        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour is IDominoTileCollection collection && behaviour.gameObject.scene.IsValid() && behaviour.gameObject.scene.isLoaded)
            {
                return collection;
            }
        }

        return null;
    }

    private static bool IsUsableBoardTile(DominoTileWorld tile)
    {
        return tile != null
            && tile.gameObject.activeInHierarchy
            && tile.gameObject.scene.IsValid()
            && tile.gameObject.scene.isLoaded
            && tile.Model != null
            && tile.Model.Place == TilePlace.Board;
    }

    private void OrderBoardTilesByConnections()
    {
        rightChainCount = 0;
        if (boardTiles.Count <= 2)
        {
            rightChainCount = Mathf.Max(0, boardTiles.Count - 1);
            return;
        }

        BuildConnectionGraph();
        DominoTileWorld firstTile = boardTiles[0];
        if (firstTile == null || !adjacency.TryGetValue(firstTile, out List<DominoTileWorld> firstNeighbors) || firstNeighbors.Count == 0)
        {
            rightChainCount = Mathf.Max(0, boardTiles.Count - 1);
            return;
        }

        rightChain.Clear();
        leftChain.Clear();

        DominoTileWorld preferredRight = null;
        DominoTileWorld preferredLeft = null;
        foreach (DominoTileWorld neighbor in firstNeighbors)
        {
            DirTileUsed side = GetBoardSideUsed(neighbor);
            if ((side == DirTileUsed.rightUsed || side == DirTileUsed.upUsed) && preferredRight == null)
            {
                preferredRight = neighbor;
            }
            else if ((side == DirTileUsed.leftUsed || side == DirTileUsed.downUsed) && preferredLeft == null)
            {
                preferredLeft = neighbor;
            }
        }

        firstNeighbors.Sort(CompareTileOrder);
        if (preferredRight == null)
        {
            preferredRight = firstNeighbors[0];
        }

        if (preferredLeft == null)
        {
            for (int i = 0; i < firstNeighbors.Count; i++)
            {
                if (firstNeighbors[i] != preferredRight)
                {
                    preferredLeft = firstNeighbors[i];
                    break;
                }
            }
        }

        var visited = new HashSet<DominoTileWorld> { firstTile };
        if (preferredRight != null)
        {
            TraceMainChain(firstTile, preferredRight, visited, rightChain);
        }

        if (preferredLeft != null)
        {
            TraceMainChain(firstTile, preferredLeft, visited, leftChain);
        }

        AddUnvisitedTilesToNearestChain(visited);

        scratchTiles.Clear();
        scratchTiles.Add(firstTile);
        scratchTiles.AddRange(rightChain);
        rightChainCount = rightChain.Count;
        scratchTiles.AddRange(leftChain);

        boardTiles.Clear();
        boardTiles.AddRange(scratchTiles);
    }

    private void BuildConnectionGraph()
    {
        adjacency.Clear();
        parentByTile.Clear();
        orderByTile.Clear();

        var byNetworkId = new Dictionary<ulong, DominoTileWorld>();
        for (int i = 0; i < boardTiles.Count; i++)
        {
            DominoTileWorld tile = boardTiles[i];
            if (tile == null)
            {
                continue;
            }

            adjacency[tile] = new List<DominoTileWorld>(4);
            orderByTile[tile] = i;
            byNetworkId[tile.NetworkObjectId] = tile;
        }

        for (int i = 1; i < boardTiles.Count; i++)
        {
            DominoTileWorld tile = boardTiles[i];
            TileMovementValidation validation = tile != null ? tile.MovementValidationValidation : null;
            if (tile == null || validation == null || !byNetworkId.TryGetValue(validation.TileOnBoard, out DominoTileWorld parent) || parent == tile)
            {
                continue;
            }

            parentByTile[tile] = parent;
            AddConnection(parent, tile);
        }
    }

    private void AddConnection(DominoTileWorld a, DominoTileWorld b)
    {
        if (!adjacency.TryGetValue(a, out List<DominoTileWorld> aList))
        {
            aList = new List<DominoTileWorld>(4);
            adjacency[a] = aList;
        }

        if (!aList.Contains(b))
        {
            aList.Add(b);
        }

        if (!adjacency.TryGetValue(b, out List<DominoTileWorld> bList))
        {
            bList = new List<DominoTileWorld>(4);
            adjacency[b] = bList;
        }

        if (!bList.Contains(a))
        {
            bList.Add(a);
        }
    }

    private void TraceMainChain(DominoTileWorld previous, DominoTileWorld current, HashSet<DominoTileWorld> visited, List<DominoTileWorld> chain)
    {
        while (current != null && visited.Add(current))
        {
            chain.Add(current);

            DominoTileWorld next = null;
            if (adjacency.TryGetValue(current, out List<DominoTileWorld> neighbors))
            {
                neighbors.Sort(CompareTileOrder);
                foreach (DominoTileWorld neighbor in neighbors)
                {
                    if (neighbor != previous && !visited.Contains(neighbor))
                    {
                        next = neighbor;
                        break;
                    }
                }
            }

            previous = current;
            current = next;
        }
    }

    private void AddUnvisitedTilesToNearestChain(HashSet<DominoTileWorld> visited)
    {
        for (int i = 0; i < boardTiles.Count; i++)
        {
            DominoTileWorld tile = boardTiles[i];
            if (tile == null || visited.Contains(tile))
            {
                continue;
            }

            if (BelongsToChain(tile, rightChain))
            {
                rightChain.Add(tile);
            }
            else
            {
                leftChain.Add(tile);
            }

            visited.Add(tile);
        }
    }

    private bool BelongsToChain(DominoTileWorld tile, List<DominoTileWorld> chain)
    {
        DominoTileWorld current = tile;
        for (int i = 0; i < 28 && current != null; i++)
        {
            if (chain.Contains(current))
            {
                return true;
            }

            if (!parentByTile.TryGetValue(current, out current))
            {
                return false;
            }
        }

        return false;
    }

    private int CompareTileOrder(DominoTileWorld a, DominoTileWorld b)
    {
        int aOrder = a != null && orderByTile.TryGetValue(a, out int ao) ? ao : int.MaxValue;
        int bOrder = b != null && orderByTile.TryGetValue(b, out int bo) ? bo : int.MaxValue;
        return aOrder.CompareTo(bOrder);
    }

    private static DirTileUsed GetBoardSideUsed(DominoTileWorld tile)
    {
        TileMovementValidation validation = tile != null ? tile.MovementValidationValidation : null;
        return validation != null ? validation.BoardTileUsed : DirTileUsed.none;
    }

    private void EnsureTileBaseScales()
    {
        foreach (DominoTileWorld tile in boardTiles)
        {
            if (tile != null && !baseScales.ContainsKey(tile))
            {
                baseScales.Add(tile, tile.transform.localScale);
            }

            if (tile != null && tile.RotationHandler != null && !baseFacings.ContainsKey(tile))
            {
                baseFacings.Add(tile, tile.RotationHandler.Rotation);
            }
        }
    }

    private bool TryGetTileFootprint(out float shortSize, out float longSize)
    {
        shortSize = 0.58f;
        longSize = 1.16f;

        foreach (DominoTileWorld tile in boardTiles)
        {
            BoxCollider2D collider = tile != null ? tile.GetComponent<BoxCollider2D>() : null;
            if (collider == null)
            {
                continue;
            }

            Vector3 baseScale = baseScales.TryGetValue(tile, out Vector3 storedScale) ? storedScale : tile.transform.localScale;
            float sizeX = Mathf.Abs(collider.size.x * baseScale.x);
            float sizeY = Mathf.Abs(collider.size.y * baseScale.y);
            shortSize = Mathf.Min(sizeX, sizeY) + Mathf.Max(0f, visualFootprintPadding);
            longSize = Mathf.Max(sizeX, sizeY) + Mathf.Max(0f, visualFootprintPadding);
            return shortSize > 0.01f && longSize > 0.01f;
        }

        return true;
    }

    private float FindBestFitScale(float shortSize, float longSize)
    {
        float countScale = GetScaleForTileCount(boardTiles.Count);
        if (!autoShrinkToBounds)
        {
            return countScale;
        }

        for (float scale = countScale; scale >= minimumTableScale; scale -= 0.02f)
        {
            float scaledShort = shortSize * scale;
            float scaledLong = longSize * scale;
            BuildPlacements(scaledShort, scaledLong, placements);
            if (AllPlacementsValid(placements, scaledShort, scaledLong))
            {
                return scale;
            }
        }

        return minimumTableScale;
    }

    private float GetScaleForTileCount(int tileCount)
    {
        int start = Mathf.Max(1, shrinkStartsAtTileCount);
        int end = Mathf.Max(start + 1, shrinkFullAtTileCount);
        float amount = Mathf.InverseLerp(start, end, tileCount);
        return Mathf.Lerp(Mathf.Max(1f, maximumTableScale), minimumTableScale, amount);
    }

    private void BuildPlacements(float shortSize, float longSize, List<Placement> result)
    {
        result.Clear();
        int count = boardTiles.Count;
        if (count <= 0)
        {
            return;
        }

        TileFacing firstFacing = GetInitialFacing(boardTiles[0]);
        result.Add(new Placement(Vector2.zero, firstFacing));

        upperCursor = BuildChainPlacements(startIndex: 1, tileCount: rightChainCount, initialVerticalSign: 1, initialHorizontalSign: 1, firstFacing, shortSize, longSize, result);

        int leftStart = 1 + rightChainCount;
        int leftCount = boardTiles.Count - leftStart;
        lowerCursor = BuildChainPlacements(leftStart, leftCount, initialVerticalSign: -1, initialHorizontalSign: -1, firstFacing, shortSize, longSize, result);
    }

    private ChainCursor BuildChainPlacements(int startIndex, int tileCount, int initialVerticalSign, int initialHorizontalSign, TileFacing firstFacing, float shortSize, float longSize, List<Placement> result)
    {
        Vector2 current = Vector2.zero;
        TileFacing currentFacing = firstFacing;
        int verticalSign = initialVerticalSign;
        int horizontalSign = initialHorizontalSign;
        FlowDirection direction = verticalSign > 0 ? FlowDirection.Up : FlowDirection.Down;
        int horizontalRemaining = 0;
        int requiredValue = GetFirstChainValue(initialVerticalSign);

        for (int i = 0; i < tileCount; i++)
        {
            int tileIndex = startIndex + i;
            if (tileIndex < 0 || tileIndex >= boardTiles.Count)
            {
                break;
            }

            DominoTileWorld tile = boardTiles[tileIndex];
            TileFacing facing = currentFacing;
            Vector2 next = current;
            bool foundPosition = false;
            bool triedOppositeHorizontal = false;

            for (int attempt = 0; attempt < 6; attempt++)
            {
                TileFacing pathFacing = GetFacing(direction);
                facing = GetDisplayFacing(tile, pathFacing, requiredValue);
                float step = GetCenterStep(currentFacing, facing, direction, shortSize, longSize);
                next = current + GetStep(direction, horizontalSign, verticalSign, step);

                if (CanPlace(next, facing, shortSize, longSize, result))
                {
                    foundPosition = true;
                    break;
                }

                if (IsVertical(direction))
                {
                    direction = horizontalSign > 0 ? FlowDirection.Right : FlowDirection.Left;
                    horizontalRemaining = Mathf.Max(1, horizontalPiecesOnTurn);
                    triedOppositeHorizontal = false;
                    continue;
                }

                if (!triedOppositeHorizontal)
                {
                    horizontalSign *= -1;
                    direction = horizontalSign > 0 ? FlowDirection.Right : FlowDirection.Left;
                    triedOppositeHorizontal = true;
                    continue;
                }

                verticalSign *= -1;
                direction = verticalSign > 0 ? FlowDirection.Up : FlowDirection.Down;
                horizontalRemaining = 0;
            }

            if (!foundPosition)
            {
                // FindBestFitScale will retry the whole layout at a smaller uniform scale.
                TileFacing pathFacing = GetFacing(direction);
                facing = GetDisplayFacing(tile, pathFacing, requiredValue);
                float step = GetCenterStep(currentFacing, facing, direction, shortSize, longSize);
                next = current + GetStep(direction, horizontalSign, verticalSign, step);
            }

            result.Add(new Placement(next, facing));
            current = next;
            currentFacing = facing;
            requiredValue = GetNextRequiredValue(tile, requiredValue);

            if (!IsVertical(direction))
            {
                horizontalRemaining--;
                if (horizontalRemaining <= 0)
                {
                    verticalSign *= -1;
                    direction = verticalSign > 0 ? FlowDirection.Up : FlowDirection.Down;
                }
            }
        }

        return new ChainCursor(current, currentFacing, direction, verticalSign, horizontalSign, horizontalRemaining);
    }

    private static bool IsVertical(FlowDirection direction)
    {
        return direction == FlowDirection.Up || direction == FlowDirection.Down;
    }

    private int GetFirstChainValue(int initialVerticalSign)
    {
        if (boardTiles.Count == 0 || boardTiles[0] == null || boardTiles[0].Model == null)
        {
            return -1;
        }

        DominoTileModel model = boardTiles[0].Model;
        if (model.IsEqualValue)
        {
            return model.EqualValue;
        }

        return initialVerticalSign > 0 ? model.UpValue : model.DownValue;
    }

    private static Vector2 GetStep(FlowDirection direction, int horizontalSign, int verticalSign, float step)
    {
        switch (direction)
        {
            case FlowDirection.Up:
            case FlowDirection.Down:
                return new Vector2(0f, verticalSign * step);
            case FlowDirection.Right:
            case FlowDirection.Left:
                return new Vector2(horizontalSign * step, 0f);
            default:
                return Vector2.zero;
        }
    }

    private static TileFacing GetFacing(FlowDirection direction)
    {
        switch (direction)
        {
            case FlowDirection.Up:
                return TileFacing.Up;
            case FlowDirection.Down:
                return TileFacing.Down;
            case FlowDirection.Right:
                return TileFacing.Right;
            case FlowDirection.Left:
                return TileFacing.Left;
            default:
                return TileFacing.Up;
        }
    }

    private TileFacing GetInitialFacing(DominoTileWorld tile)
    {
        if (tile != null && tile.Model != null && tile.Model.IsEqualValue)
        {
            return TileFacing.Right;
        }

        if (tile != null && baseFacings.TryGetValue(tile, out TileFacing baseFacing))
        {
            return IsHorizontal(baseFacing) ? baseFacing : TileFacing.Right;
        }

        return TileFacing.Right;
    }

    private TileFacing GetDisplayFacing(DominoTileWorld tile, TileFacing pathFacing, int requiredValue)
    {
        if (tile != null && tile.Model != null && tile.Model.IsEqualValue)
        {
            return IsHorizontal(pathFacing) ? TileFacing.Up : TileFacing.Right;
        }

        if (tile != null && tile.Model != null && requiredValue >= 0)
        {
            bool upMatches = tile.Model.UpValue == requiredValue;
            bool downMatches = tile.Model.DownValue == requiredValue;
            if (upMatches || downMatches)
            {
                switch (pathFacing)
                {
                    case TileFacing.Right:
                        return upMatches ? TileFacing.Left : TileFacing.Right;
                    case TileFacing.Left:
                        return upMatches ? TileFacing.Right : TileFacing.Left;
                    case TileFacing.Down:
                        return upMatches ? TileFacing.Up : TileFacing.Down;
                    case TileFacing.Up:
                        return upMatches ? TileFacing.Down : TileFacing.Up;
                }
            }
        }

        TileFacing currentFacing = tile != null && baseFacings.TryGetValue(tile, out TileFacing baseFacing)
            ? baseFacing
            : pathFacing;
        if (IsHorizontal(pathFacing) == IsHorizontal(currentFacing))
        {
            return currentFacing;
        }

        return pathFacing;
    }

    private static int GetNextRequiredValue(DominoTileWorld tile, int currentRequiredValue)
    {
        if (tile == null || tile.Model == null)
        {
            return currentRequiredValue;
        }

        if (tile.Model.IsEqualValue)
        {
            return tile.Model.EqualValue;
        }

        if (tile.Model.UpValue == currentRequiredValue)
        {
            return tile.Model.DownValue;
        }

        if (tile.Model.DownValue == currentRequiredValue)
        {
            return tile.Model.UpValue;
        }

        return tile.Model.LastFreeValue >= 0 ? tile.Model.LastFreeValue : currentRequiredValue;
    }

    private static bool IsDouble(DominoTileWorld tile)
    {
        return tile != null && tile.Model != null && tile.Model.IsEqualValue;
    }

    private float GetCenterStep(TileFacing previousFacing, TileFacing nextFacing, FlowDirection direction, float shortSize, float longSize)
    {
        bool horizontalMove = !IsVertical(direction);
        float previousHalf = GetHalfAlong(previousFacing, horizontalMove, shortSize, longSize);
        float nextHalf = GetHalfAlong(nextFacing, horizontalMove, shortSize, longSize);
        return previousHalf + nextHalf + Mathf.Max(0f, pieceGap);
    }

    private static float GetHalfAlong(TileFacing facing, bool horizontalMove, float shortSize, float longSize)
    {
        GetHalfExtents(facing, shortSize, longSize, out float halfWidth, out float halfHeight);
        return horizontalMove ? halfWidth : halfHeight;
    }

    private static bool IsHorizontal(TileFacing facing)
    {
        return facing == TileFacing.Right || facing == TileFacing.Left;
    }

    private bool AllPlacementsValid(List<Placement> testPlacements, float shortSize, float longSize)
    {
        for (int i = 0; i < testPlacements.Count; i++)
        {
            Placement placement = testPlacements[i];
            if (!FitsInside(placement.Position, placement.Facing, shortSize, longSize))
            {
                return false;
            }

            for (int j = 0; j < i; j++)
            {
                if (Overlaps(placement, testPlacements[j], shortSize, longSize))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private bool CanPlace(Vector2 position, TileFacing facing, float shortSize, float longSize, List<Placement> existing)
    {
        if (!FitsInside(position, facing, shortSize, longSize))
        {
            return false;
        }

        var candidate = new Placement(position, facing);
        for (int i = 0; i < existing.Count; i++)
        {
            if (Overlaps(candidate, existing[i], shortSize, longSize))
            {
                return false;
            }
        }

        return true;
    }

    private bool Overlaps(Placement a, Placement b, float shortSize, float longSize)
    {
        GetHalfExtents(a.Facing, shortSize, longSize, out float aHalfWidth, out float aHalfHeight);
        GetHalfExtents(b.Facing, shortSize, longSize, out float bHalfWidth, out float bHalfHeight);

        float clearX = aHalfWidth + bHalfWidth + Mathf.Max(0.015f, pieceGap * 0.2f);
        float clearY = aHalfHeight + bHalfHeight + Mathf.Max(0.015f, pieceGap * 0.2f);
        return Mathf.Abs(a.Position.x - b.Position.x) < clearX
            && Mathf.Abs(a.Position.y - b.Position.y) < clearY;
    }

    private bool FitsInside(Vector2 position, TileFacing facing, float shortSize, float longSize)
    {
        position += boardVisualOffset;
        GetHalfExtents(facing, shortSize, longSize, out float halfWidth, out float halfHeight);
        return position.x - halfWidth >= leftLimit
            && position.x + halfWidth <= rightLimit
            && position.y - halfHeight >= bottomLimit
            && position.y + halfHeight <= topLimit;
    }

    private Vector2 ClampToBounds(Vector2 position, TileFacing facing, float shortSize, float longSize)
    {
        GetHalfExtents(facing, shortSize, longSize, out float halfWidth, out float halfHeight);
        Vector2 visualPosition = position + boardVisualOffset;
        visualPosition.x = Mathf.Clamp(visualPosition.x, leftLimit + halfWidth, rightLimit - halfWidth);
        visualPosition.y = Mathf.Clamp(visualPosition.y, bottomLimit + halfHeight, topLimit - halfHeight);
        return visualPosition - boardVisualOffset;
    }

    private static void GetHalfExtents(TileFacing facing, float shortSize, float longSize, out float halfWidth, out float halfHeight)
    {
        bool horizontal = facing == TileFacing.Right || facing == TileFacing.Left;
        halfWidth = (horizontal ? longSize : shortSize) * 0.5f;
        halfHeight = (horizontal ? shortSize : longSize) * 0.5f;
    }

    private void ApplyPlacements(float fitScale, float shortSize, float longSize)
    {
        int count = Mathf.Min(boardTiles.Count, placements.Count);
        for (int i = 0; i < count; i++)
        {
            DominoTileWorld tile = boardTiles[i];
            if (tile == null)
            {
                continue;
            }

            Vector3 targetScale = tile.transform.localScale;
            if (baseScales.TryGetValue(tile, out Vector3 baseScale))
            {
                targetScale = baseScale * fitScale;
                if ((tile.transform.localScale - targetScale).sqrMagnitude > 0.0001f)
                {
                    tile.SetBaseVisualScale(targetScale);
                }
            }

            Placement placement = placements[i];
            Vector2 boundedPosition = ClampToBounds(placement.Position, placement.Facing, shortSize, longSize);
            Vector3 currentPosition = tile.transform.position;
            Vector2 visualPosition = boundedPosition + boardVisualOffset;
            Vector3 targetPosition = new Vector3(visualPosition.x, visualPosition.y, currentPosition.z);

            if (tile.RotationHandler != null && tile.RotationHandler.Rotation != placement.Facing)
            {
                tile.RotationHandler.SetRotation(placement.Facing);
            }

            if (animatedTiles.Add(tile))
            {
                StartPlacementAnimation(tile, targetPosition, targetScale);
                continue;
            }

            if (placementAnimationEnds.TryGetValue(tile, out float animationEnd))
            {
                if (Time.unscaledTime < animationEnd)
                {
                    continue;
                }

                placementAnimationEnds.Remove(tile);
            }

            if ((currentPosition - targetPosition).sqrMagnitude > 0.0001f)
            {
                tile.transform.position = targetPosition;
            }

            if ((tile.transform.localScale - targetScale).sqrMagnitude > 0.0001f)
            {
                tile.SetBaseVisualScale(targetScale);
            }
        }
    }

    private void StartPlacementAnimation(DominoTileWorld tile, Vector3 targetPosition, Vector3 targetScale)
    {
        Transform tileTransform = tile.transform;
        tileTransform.DOKill();

        float duration = Mathf.Max(0.08f, placementAnimationDuration);
        float impactDuration = duration * 0.7f;
        float settleDuration = duration - impactDuration;
        float startScale = Mathf.Clamp(placementStartScale, 0.5f, 1f);
        float impactScale = Mathf.Max(1f, placementImpactScale);
        float settleDepth = Mathf.Max(0f, placementSettleDepth);

        tileTransform.position = targetPosition + Vector3.up * Mathf.Max(0f, placementDropHeight);
        tileTransform.localScale = targetScale * startScale;

        Sequence sequence = DOTween.Sequence().SetUpdate(true);
        sequence.Append(DG.Tweening.ShortcutExtensions.DOMove(
                tileTransform, targetPosition - Vector3.up * settleDepth, impactDuration)
            .SetEase(Ease.OutCubic));
        sequence.Join(DG.Tweening.ShortcutExtensions.DOScale(
                tileTransform, targetScale * impactScale, impactDuration)
            .SetEase(Ease.OutBack));
        sequence.Append(DG.Tweening.ShortcutExtensions.DOMove(
                tileTransform, targetPosition, settleDuration)
            .SetEase(Ease.OutQuad));
        sequence.Join(DG.Tweening.ShortcutExtensions.DOScale(
                tileTransform, targetScale, settleDuration)
            .SetEase(Ease.InOutSine));

        placementAnimationEnds[tile] = Time.unscaledTime + duration + 0.06f;
    }

    private void UpdatePlacementHints(float shortSize, float longSize)
    {
        HideLegacyTilePreviewVisuals();

        if (boardTiles.Count != lastHintBoardTileCount)
        {
            lastHintBoardTileCount = boardTiles.Count;
            suppressHintsUntilPreviewHidden = true;
            userSelectionArmed = false;
            SetPlacementHintsVisible(false);
        }

        bool previewSelected = TryGetSelectedPreviewPosition(out Vector2 previewPosition);
        if (!previewSelected)
        {
            suppressHintsUntilPreviewHidden = false;
            userSelectionArmed = false;
            wasPreviewSelected = false;
        }
        else if (!wasPreviewSelected && !suppressHintsUntilPreviewHidden)
        {
            userSelectionArmed = true;
        }

        wasPreviewSelected = previewSelected;

        if (!showPlacementHints || boardTiles.Count == 0 || !previewSelected ||
            suppressHintsUntilPreviewHidden || !userSelectionArmed)
        {
            SetPlacementHintsVisible(false);
            return;
        }

        EnsurePlacementHintLines();
        DominoTileWorld selectedTile = GetSelectedHandTile();
        if (selectedTile == null || selectedTile.Model == null)
        {
            SetPlacementHintsVisible(false);
            return;
        }

        int upperOpenValue = GetOpenChainValue(1, 1, rightChainCount);
        int lowerStart = 1 + rightChainCount;
        int lowerOpenValue = GetOpenChainValue(-1, lowerStart, boardTiles.Count - lowerStart);
        bool upperMatches = CanTileMatchValue(selectedTile, upperOpenValue);
        bool lowerMatches = CanTileMatchValue(selectedTile, lowerOpenValue);

        Placement upperHint = default;
        Placement lowerHint = default;
        bool hasUpper = upperMatches && TryCalculateNextHint(
            upperCursor, selectedTile, upperOpenValue, shortSize, longSize, placements, out upperHint);
        bool hasLower = lowerMatches && TryCalculateNextHint(
            lowerCursor, selectedTile, lowerOpenValue, shortSize, longSize, placements, out lowerHint);

        DrawPlacementHint(0, upperHint, hasUpper, shortSize, longSize);
        DrawPlacementHint(1, lowerHint, hasLower, shortSize, longSize);
    }

    private void HideLegacyTilePreviewVisuals()
    {
        if (tilePreviewTransform == null)
        {
            return;
        }

        Renderer[] renderers = tilePreviewTransform.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && renderers[i].enabled)
            {
                renderers[i].enabled = false;
            }
        }
    }

    private bool TryGetSelectedPreviewPosition(out Vector2 previewPosition)
    {
        previewPosition = Vector2.zero;
        if (tilePreviewTransform == null || !tilePreviewTransform.gameObject.scene.IsValid())
        {
            var previewObject = GameObject.Find("Tile -Preview");
            tilePreviewTransform = previewObject != null ? previewObject.transform : null;
        }

        if (tilePreviewTransform == null || !tilePreviewTransform.gameObject.activeInHierarchy)
        {
            return false;
        }

        Vector3 worldPosition = tilePreviewTransform.position;
        const float hiddenPreviewThreshold = 50f;
        if (Mathf.Abs(worldPosition.x) >= hiddenPreviewThreshold ||
            Mathf.Abs(worldPosition.y) >= hiddenPreviewThreshold ||
            Mathf.Abs(worldPosition.z) >= hiddenPreviewThreshold)
        {
            return false;
        }

        previewPosition = worldPosition;
        return true;
    }

    private DominoTileWorld GetSelectedHandTile()
    {
        if (tileViewDragField == null)
        {
            tileViewDragField = typeof(DominoTileView).GetField("_drag", BindingFlags.Instance | BindingFlags.NonPublic);
            dragTileViewField = typeof(DominoTileUIDraggin).GetField("_tileView", BindingFlags.Instance | BindingFlags.NonPublic);
        }

        DominoTileView[] views = Resources.FindObjectsOfTypeAll<DominoTileView>();
        for (int i = 0; i < views.Length; i++)
        {
            DominoTileView view = views[i];
            if (view == null || !view.gameObject.scene.IsValid() || !view.gameObject.scene.isLoaded ||
                !view.gameObject.activeInHierarchy || tileViewDragField == null)
            {
                continue;
            }

            DominoTileUIDraggin drag = tileViewDragField.GetValue(view) as DominoTileUIDraggin;
            if (drag == null || (!drag.IsPointerDown && !drag.IsDragging))
            {
                continue;
            }

            DominoTileWorld selected = view.WorldTile;
            if (selected == null && dragTileViewField != null)
            {
                DominoTileView dragView = dragTileViewField.GetValue(drag) as DominoTileView;
                selected = dragView != null ? dragView.WorldTile : null;
            }

            if (selected != null && selected.Model != null)
            {
                return selected;
            }
        }

        return null;
    }

    private int GetOpenChainValue(int initialVerticalSign, int startIndex, int tileCount)
    {
        int value = GetFirstChainValue(initialVerticalSign);
        int end = Mathf.Min(boardTiles.Count, startIndex + Mathf.Max(0, tileCount));
        for (int i = Mathf.Max(1, startIndex); i < end; i++)
        {
            value = GetNextRequiredValue(boardTiles[i], value);
        }

        return value;
    }

    private static bool CanTileMatchValue(DominoTileWorld tile, int openValue)
    {
        return tile != null && tile.Model != null && openValue >= 0 &&
            (tile.Model.UpValue == openValue || tile.Model.DownValue == openValue);
    }

    private bool TryCalculateNextHint(ChainCursor cursor, DominoTileWorld selectedTile, int requiredValue,
        float shortSize, float longSize, List<Placement> existing, out Placement hint)
    {
        hint = default;
        if (!cursor.IsValid || selectedTile == null || selectedTile.Model == null)
        {
            return false;
        }

        bool triedOppositeHorizontal = false;
        for (int attempt = 0; attempt < 6; attempt++)
        {
            TileFacing pathFacing = GetFacing(cursor.Direction);
            TileFacing facing = GetDisplayFacing(selectedTile, pathFacing, requiredValue);
            float step = GetCenterStep(cursor.Facing, facing, cursor.Direction, shortSize, longSize);
            Vector2 next = cursor.Position + GetStep(cursor.Direction, cursor.HorizontalSign, cursor.VerticalSign, step);

            if (CanPlace(next, facing, shortSize, longSize, existing))
            {
                hint = new Placement(next, facing);
                return true;
            }

            if (IsVertical(cursor.Direction))
            {
                cursor.Direction = cursor.HorizontalSign > 0 ? FlowDirection.Right : FlowDirection.Left;
                cursor.HorizontalRemaining = Mathf.Max(1, horizontalPiecesOnTurn);
                triedOppositeHorizontal = false;
                continue;
            }

            if (!triedOppositeHorizontal)
            {
                cursor.HorizontalSign *= -1;
                cursor.Direction = cursor.HorizontalSign > 0 ? FlowDirection.Right : FlowDirection.Left;
                triedOppositeHorizontal = true;
                continue;
            }

            cursor.VerticalSign *= -1;
            cursor.Direction = cursor.VerticalSign > 0 ? FlowDirection.Up : FlowDirection.Down;
            cursor.HorizontalRemaining = 0;
        }

        return false;
    }

    private void EnsurePlacementHintLines()
    {
        if (hintMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            if (shader != null)
            {
                hintMaterial = new Material(shader)
                {
                    name = "Domino Placement Hint Material"
                };
            }
        }

        for (int i = 0; i < hintLines.Length; i++)
        {
            if (hintGlowLines[i] == null)
            {
                hintGlowLines[i] = CreateHintLine("Domino Placement Hint Glow " + (i + 1), 31989);
            }

            if (hintLines[i] == null)
            {
                hintLines[i] = CreateHintLine("Domino Placement Hint " + (i + 1), 31990);
            }
        }
    }

    private LineRenderer CreateHintLine(string objectName, int sortingOrder)
    {
        var lineObject = new GameObject(objectName);
        DontDestroyOnLoad(lineObject);

        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = false;
        line.positionCount = 5;
        line.numCornerVertices = 5;
        line.numCapVertices = 2;
        line.textureMode = LineTextureMode.Stretch;
        line.sortingOrder = sortingOrder;
        if (hintMaterial != null)
        {
            line.sharedMaterial = hintMaterial;
        }

        return line;
    }

    private void DrawPlacementHint(int index, Placement placement, bool visible, float shortSize, float longSize)
    {
        LineRenderer line = hintLines[index];
        LineRenderer glow = hintGlowLines[index];
        if (line == null || glow == null)
        {
            return;
        }

        line.gameObject.SetActive(visible);
        glow.gameObject.SetActive(visible);
        if (!visible)
        {
            return;
        }

        GetHalfExtents(placement.Facing, shortSize, longSize, out float halfWidth, out float halfHeight);
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4.5f + index * 1.7f);
        float pulseScale = 1f + pulse * 0.035f;
        halfWidth = (halfWidth + hintOutlinePadding) * pulseScale;
        halfHeight = (halfHeight + hintOutlinePadding) * pulseScale;

        Vector2 center = placement.Position + boardVisualOffset;
        float z = boardTiles.Count > 0 && boardTiles[0] != null
            ? boardTiles[0].transform.position.z - 0.04f
            : -0.04f;

        SetHintRectangle(line, center, halfWidth, halfHeight, z);
        SetHintRectangle(glow, center, halfWidth, halfHeight, z + 0.005f);

        Color bright = hintColor;
        bright.a *= Mathf.Lerp(0.68f, 1f, pulse);
        Color glowColor = hintColor;
        glowColor.a = Mathf.Lerp(0.1f, 0.24f, pulse);

        line.widthMultiplier = Mathf.Lerp(0.035f, 0.052f, pulse);
        line.startColor = bright;
        line.endColor = bright;
        glow.widthMultiplier = Mathf.Lerp(0.105f, 0.145f, pulse);
        glow.startColor = glowColor;
        glow.endColor = glowColor;
    }

    private static void SetHintRectangle(LineRenderer line, Vector2 center, float halfWidth, float halfHeight, float z)
    {
        line.SetPosition(0, new Vector3(center.x - halfWidth, center.y + halfHeight, z));
        line.SetPosition(1, new Vector3(center.x + halfWidth, center.y + halfHeight, z));
        line.SetPosition(2, new Vector3(center.x + halfWidth, center.y - halfHeight, z));
        line.SetPosition(3, new Vector3(center.x - halfWidth, center.y - halfHeight, z));
        line.SetPosition(4, new Vector3(center.x - halfWidth, center.y + halfHeight, z));
    }

    private void SetPlacementHintsVisible(bool visible)
    {
        for (int i = 0; i < hintLines.Length; i++)
        {
            if (hintLines[i] != null)
            {
                hintLines[i].gameObject.SetActive(visible);
            }

            if (hintGlowLines[i] != null)
            {
                hintGlowLines[i].gameObject.SetActive(visible);
            }
        }
    }

    private void EnsureBoundsLine()
    {
        if (!showVisibleBounds)
        {
            if (boundsLine != null)
            {
                boundsLine.gameObject.SetActive(false);
            }

            return;
        }

        if (boundsLine != null)
        {
            boundsLine.gameObject.SetActive(true);
            return;
        }

        var lineObject = new GameObject("Domino Board Visible Limit");
        DontDestroyOnLoad(lineObject);
        boundsLine = lineObject.AddComponent<LineRenderer>();
        boundsLine.useWorldSpace = true;
        boundsLine.loop = false;
        boundsLine.positionCount = 5;
        boundsLine.widthMultiplier = boundsLineWidth;
        boundsLine.numCornerVertices = 3;
        boundsLine.numCapVertices = 3;
        boundsLine.sortingOrder = 32000;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }

        if (shader != null)
        {
            boundsMaterial = new Material(shader);
            boundsLine.material = boundsMaterial;
        }

        boundsLine.startColor = boundsColor;
        boundsLine.endColor = boundsColor;
    }

    private void DrawBounds()
    {
        EnsureBoundsLine();
        if (!showVisibleBounds || boundsLine == null)
        {
            return;
        }

        boundsLine.widthMultiplier = boundsLineWidth;
        boundsLine.startColor = boundsColor;
        boundsLine.endColor = boundsColor;
        float z = -0.05f;
        boundsLine.SetPosition(0, new Vector3(leftLimit, topLimit, z));
        boundsLine.SetPosition(1, new Vector3(rightLimit, topLimit, z));
        boundsLine.SetPosition(2, new Vector3(rightLimit, bottomLimit, z));
        boundsLine.SetPosition(3, new Vector3(leftLimit, bottomLimit, z));
        boundsLine.SetPosition(4, new Vector3(leftLimit, topLimit, z));
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = boundsColor;
        Vector3 topLeft = new Vector3(leftLimit, topLimit, 0f);
        Vector3 topRight = new Vector3(rightLimit, topLimit, 0f);
        Vector3 bottomRight = new Vector3(rightLimit, bottomLimit, 0f);
        Vector3 bottomLeft = new Vector3(leftLimit, bottomLimit, 0f);
        Gizmos.DrawLine(topLeft, topRight);
        Gizmos.DrawLine(topRight, bottomRight);
        Gizmos.DrawLine(bottomRight, bottomLeft);
        Gizmos.DrawLine(bottomLeft, topLeft);
    }

    /// <summary>
    /// Clears all cached tile data (scales, facings, adjacency, etc.).
    /// Must be called between rounds so stale data from the previous match doesn't corrupt the new one.
    /// </summary>
    public static void ClearCachedState()
    {
        if (instance == null)
        {
            return;
        }

        instance.baseScales.Clear();
        instance.baseFacings.Clear();
        instance.adjacency.Clear();
        instance.parentByTile.Clear();
        instance.orderByTile.Clear();
        instance.boardTiles.Clear();
        instance.placements.Clear();
        instance.rightChain.Clear();
        instance.leftChain.Clear();
        instance.scratchTiles.Clear();
        instance.animatedTiles.Clear();
        instance.placementAnimationEnds.Clear();
        instance.SetPlacementHintsVisible(false);
        instance.upperCursor = default;
        instance.lowerCursor = default;
        instance.lastHintBoardTileCount = -1;
        instance.suppressHintsUntilPreviewHidden = false;
        instance.userSelectionArmed = false;
        instance.wasPreviewSelected = false;
        instance.rightChainCount = 0;
    }
}
