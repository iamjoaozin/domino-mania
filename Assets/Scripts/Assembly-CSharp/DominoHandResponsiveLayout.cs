using System.Collections.Generic;
using GBTemplates.Domino.Controller;
using GBTemplates.Domino.Model;
using GBTemplates.Domino.View;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Keeps the player's hand inside the mobile screen without touching board flow or game rules.
/// </summary>
[DefaultExecutionOrder(33000)]
public sealed class DominoHandResponsiveLayout : MonoBehaviour
{
    private const string HandSlotPrefix = "Lara Hand Tile Slot";

    [SerializeField] private float horizontalFill = 0.94f;
    [SerializeField] private float horizontalPadding = 16f;
    [SerializeField] private float maxScale = 0.92f;
    [SerializeField] private float minScale = 0.20f;
    [SerializeField] private float desiredGap = 5f;

    private static DominoHandResponsiveLayout instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
        {
            return;
        }

        var runner = new GameObject(nameof(DominoHandResponsiveLayout));
        DontDestroyOnLoad(runner);
        instance = runner.AddComponent<DominoHandResponsiveLayout>();
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
        ResetHandScales();
    }

    public static void ResetScale()
    {
        if (instance != null)
        {
            instance.ResetHandScales();
        }
    }

    private void ResetHandScales()
    {
        ITilesUICollectionsView tilesView = DependencyCache.TilesUICollectionsView;
        if (tilesView != null)
        {
            if (tilesView.RootBottom != null)
            {
                tilesView.RootBottom.localScale = Vector3.one;
            }
            Transform upperRoot = ResolveCommonParent(tilesView.TilesUIUpper);
            if (upperRoot != null)
            {
                upperRoot.localScale = Vector3.one;
            }
        }
    }

    private void LateUpdate()
    {
        ApplyLayout();
    }

    private void ApplyLayout()
    {
        if (DominoRoundTransitionGuard.ShouldSuspendCustomTileLayout())
        {
            return;
        }

        ITilesUICollectionsView tilesView = DependencyCache.TilesUICollectionsView;
        if (tilesView == null)
        {
            return;
        }

        ScaleContainer(tilesView.TilesUIBottom, tilesView.RootBottom, true);
        ScaleContainer(tilesView.TilesUIUpper, ResolveCommonParent(tilesView.TilesUIUpper), false);
    }

    private void ScaleContainer(IReadOnlyList<Transform> source, Transform root, bool isBottomHand)
    {
        if (source == null || root == null)
        {
            return;
        }

        int activeCount = 0;
        for (int i = 0; i < source.Count; i++)
        {
            if (source[i] != null && source[i].gameObject.activeInHierarchy)
            {
                activeCount++;
            }
        }

        if (activeCount <= 1)
        {
            root.localScale = Vector3.one;
            return;
        }

        float availableWidth = ResolveAvailableWidth(root);
        availableWidth = Mathf.Max(80f, availableWidth * horizontalFill - horizontalPadding * 2f);

        // Native domino piece width is 86, desired gap is 5.
        float baseWidth = 86f;
        float nativeTotalWidth = baseWidth * activeCount + desiredGap * (activeCount - 1);

        float targetScale = 1f;
        if (nativeTotalWidth > availableWidth)
        {
            targetScale = availableWidth / nativeTotalWidth;
        }

        float limitScale = isBottomHand ? maxScale : Mathf.Min(maxScale, 0.78f);
        targetScale = Mathf.Clamp(targetScale, minScale, limitScale);

        root.localScale = new Vector3(targetScale, targetScale, 1f);
    }

    private static float ResolveAvailableWidth(Transform root)
    {
        Canvas canvas = root != null ? root.GetComponentInParent<Canvas>() : null;
        if (canvas != null && canvas.transform is RectTransform canvasRect && canvasRect.rect.width > 40f)
        {
            return canvasRect.rect.width;
        }

        return Screen.width;
    }

    private static Transform ResolveCommonParent(IReadOnlyList<Transform> tiles)
    {
        if (tiles == null) return null;
        for (int i = 0; i < tiles.Count; i++)
        {
            if (tiles[i] != null && tiles[i].parent != null)
            {
                return tiles[i].parent;
            }
        }
        return null;
    }
}
