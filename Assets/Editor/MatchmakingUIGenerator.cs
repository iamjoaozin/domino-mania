#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MatchmakingUIGenerator
{
    [MenuItem("Dominó Mania/Definir Fundo do Matchmaking")]
    public static void SetBackground()
    {
        // Abre o explorador de arquivos do Windows
        string path = EditorUtility.OpenFilePanel("Escolha a imagem de fundo", "", "png,jpg,jpeg");
        if (string.IsNullOrEmpty(path)) return;

        // Carrega os bytes do arquivo diretamente (sem precisar do Sprite Editor)
        byte[] bytes = System.IO.File.ReadAllBytes(path);
        Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.name = System.IO.Path.GetFileNameWithoutExtension(path);
        if (!tex.LoadImage(bytes))
        {
            Debug.LogError("Nao foi possivel carregar a imagem: " + path);
            return;
        }

        // Cria o Sprite a partir da textura carregada
        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);

        // Encontra o Background na cena e aplica
        GameObject bg = GameObject.Find("Background");
        if (bg == null) { Debug.LogError("Objeto 'Background' nao encontrado. Rode 'Gerar Matchmaking UI' primeiro."); return; }

        Image img = bg.GetComponent<Image>();
        if (img == null) { Debug.LogError("Componente Image nao encontrado no Background."); return; }

        Undo.RecordObject(img, "Set Matchmaking Background");
        img.sprite = sprite;
        img.color = Color.white;
        img.type = Image.Type.Simple;
        img.preserveAspect = false;

        // Garante que cobre o canvas todo (stretch)
        RectTransform rt = bg.GetComponent<RectTransform>();
        Undo.RecordObject(rt, "Set BG Rect");
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        EditorUtility.SetDirty(img);
        Debug.Log("<color=green><b>Fundo aplicado com sucesso!</b></color> " + System.IO.Path.GetFileName(path));
    }

    [MenuItem("Dominó Mania/Gerar Matchmaking UI")]
    public static void GenerateUI()
    {
        // Create Dedicated High-Priority Canvas
        GameObject canvasObj = GameObject.Find("MatchmakingCanvas");
        if (canvasObj != null) Object.DestroyImmediate(canvasObj);

        canvasObj = new GameObject("MatchmakingCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000; // Por cima de tudo

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 2400);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();
        Undo.RegisterCreatedObjectUndo(canvasObj, "Create Matchmaking Canvas");

        // Create Main Panel
        GameObject mainPanelObj = new GameObject("Matchmaking Panel");
        Undo.RegisterCreatedObjectUndo(mainPanelObj, "Create Matchmaking Panel");
        mainPanelObj.transform.SetParent(canvas.transform, false);
        RectTransform mainRect = mainPanelObj.AddComponent<RectTransform>();
        SetFullscreen(mainRect);

        // 1. Invisible Blocker (Escudo Invisivel transparente)
        GameObject blockerObj = new GameObject("Invisible Blocker", typeof(RectTransform));
        blockerObj.transform.SetParent(mainPanelObj.transform, false);
        RectTransform blockerRect = blockerObj.GetComponent<RectTransform>();
        SetFullscreen(blockerRect);
        Image blockerImg = blockerObj.AddComponent<Image>();
        blockerImg.color = new Color(0, 0, 0, 0); // Totalmente transparente de volta
        blockerImg.raycastTarget = true; // Bloqueia os cliques no menu abaixo!

        // 2. Top Banner (Agora com o fundo Roxo Clean)
        GameObject topBanner = CreateImageObject("Top Banner", mainPanelObj.transform, null, new Color(0.15f, 0.05f, 0.28f, 1f), new Vector2(0, -60), new Vector2(600, 80));
        RectTransform bannerRect = topBanner.GetComponent<RectTransform>();
        bannerRect.anchorMin = new Vector2(0.5f, 1f);
        bannerRect.anchorMax = new Vector2(0.5f, 1f);
        bannerRect.pivot = new Vector2(0.5f, 1f);

        // Flash Image
        GameObject flashObj = CreateImageObject("MatchFound Flash", mainPanelObj.transform, null, new Color(1, 1, 1, 0), Vector2.zero, new Vector2(1080, 1920));
        SetFullscreen(flashObj.GetComponent<RectTransform>());
        flashObj.SetActive(false);

        // Status Text (dentro do Top Banner) melhorado
        TextMeshProUGUI statusText = CreateTextObject("Status Text", topBanner.transform, "<i><b>PROCURANDO ADVERSÁRIO...</b></i>", 42, Vector2.zero, new Vector2(600, 80));
        statusText.color = new Color(1f, 0.85f, 0f); // Dourado premium
        statusText.alignment = TextAlignmentOptions.Center;
        
        RectTransform statusRect = statusText.GetComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0.5f, 0.5f);
        statusRect.anchorMax = new Vector2(0.5f, 0.5f);
        statusRect.pivot = new Vector2(0.5f, 0.5f);

        // Attach to Manager
        MatchmakingManager manager = Object.FindObjectOfType<MatchmakingManager>();
        if (manager == null)
        {
            GameObject managerObj = new GameObject("MatchmakingManager");
            manager = managerObj.AddComponent<MatchmakingManager>();
            Undo.RegisterCreatedObjectUndo(managerObj, "Create MatchmakingManager");
        }

        Undo.RecordObject(manager, "Update MatchmakingManager");
        manager.matchmakingPanel = mainPanelObj;
        manager.statusText = statusText;
        manager.matchFoundFlash = flashObj.GetComponent<Image>();
        manager.opponentPanelToShake = bannerRect; // Tremer a barrinha quando achar

        mainPanelObj.SetActive(false); // Default off
        Selection.activeGameObject = mainPanelObj;
        
        Debug.Log("<color=green><b>Matchmaking Minimalista UI Gerado com Sucesso!</b></color>");
    }

    private static void SetFullscreen(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static GameObject CreateRectObject(string name, Transform parent, Vector2 pos, Vector2 size)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        return obj;
    }

    private static GameObject CreateImageObject(string name, Transform parent, Sprite sprite, Color color, Vector2 pos, Vector2 size)
    {
        GameObject obj = CreateRectObject(name, parent, pos, size);
        Image img = obj.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return obj;
    }

    private static TextMeshProUGUI CreateTextObject(string name, Transform parent, string text, float fontSize, Vector2 pos, Vector2 size)
    {
        GameObject obj = CreateRectObject(name, parent, pos, size);
        TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 12;
        tmp.fontSizeMax = fontSize;
        tmp.raycastTarget = false;
        return tmp;
    }
}
#endif
