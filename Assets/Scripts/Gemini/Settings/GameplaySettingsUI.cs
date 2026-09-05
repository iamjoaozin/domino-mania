using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Botão de configurações que aparece APENAS na mesa de jogo (nunca no menu principal).
/// Auto-injeta via RuntimeInitializeOnLoadMethod — não precisa arrastar na cena.
/// </summary>
public class GameplaySettingsUI : MonoBehaviour
{
    // ─── Referencias UI ────────────────────────────────────────────────────────
    private Canvas     settingsCanvas;
    private GameObject gearBtn;

    // ─── Detecção de gameplay ──────────────────────────────────────────────────
    // QuickPlay - Btn: ativo no MENU, inativo na MESA DE JOGO — indicador confiável
    private GameObject quickPlayBtn;
    private bool       wasInGameplay;
    private float      scanTimer;
    private const float ScanInterval = 0.5f;

    // Debounce: só muda estado se ficou estável por 1.5s (evita flickering)
    private float debounceTimer;
    private bool  pendingGameplay;
    private bool  hasPending;
    private const float DebounceTime = 1.5f;

    // ──────────────────────────────────────────────────────────────────────────
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        var go = new GameObject("[GameplaySettings]");
        DontDestroyOnLoad(go);
        go.AddComponent<GameplaySettingsUI>();
        SceneManager.sceneLoaded += (_, __) =>
        {
            // Re-scaneamos a cena após cada carregamento
            if (instance != null) instance.ScanScene();
        };
    }

    private static GameplaySettingsUI instance;

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
    }

    private void Start()
    {
        BuildUI();
        ScanScene();
    }

    // ─── Scan: acha QuickPlay - Btn (mesmo inativo) ────────────────────────────────
    private void ScanScene()
    {
        quickPlayBtn = null;
        foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (t.name == "QuickPlay - Btn" && t.gameObject.scene.IsValid())
            {
                quickPlayBtn = t.gameObject;
                break;
            }
        }
    }

    private void Update()
    {
        InterceptOriginalPopup();

        // Re-scan periódico
        scanTimer -= Time.unscaledDeltaTime;
        if (scanTimer <= 0f)
        {
            scanTimer = ScanInterval;
            if (quickPlayBtn == null) ScanScene();
        }

        bool inGameplay = IsInGameplay();

        // Debounce: acumula tempo no novo estado antes de aplicar
        if (!hasPending || pendingGameplay != inGameplay)
        {
            hasPending     = true;
            pendingGameplay = inGameplay;
            debounceTimer  = 0f;
        }
        debounceTimer += Time.unscaledDeltaTime;

        if (debounceTimer < DebounceTime) return; // ainda aguardando confirmação
        if (inGameplay == wasInGameplay) return;   // não mudou nada

        wasInGameplay = inGameplay;
        hasPending    = false;

        gearBtn?.SetActive(inGameplay);
    }

    private bool IsInGameplay()
    {
        // QuickPlay - Btn ativo na hierarquia = estamos no MENU PRINCIPAL
        // QuickPlay - Btn inativo = estamos na MESA DE JOGO
        if (quickPlayBtn != null)
            return !quickPlayBtn.activeInHierarchy;

        // Fallback: se não achou ainda, assume que não está em gameplay (esconde o botão)
        return false;
    }

    // ─── Construção da UI ──────────────────────────────────────────────────────
    private void BuildUI()
    {
        // Canvas raiz
        var canvasGo = new GameObject("SettingsCanvas");
        canvasGo.transform.SetParent(transform, false);
        settingsCanvas = canvasGo.AddComponent<Canvas>();
        settingsCanvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        settingsCanvas.sortingOrder = 32000;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight  = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        BuildGearButton(canvasGo.transform);

        // Começa escondido — Update vai controlar
        gearBtn.SetActive(false);
    }

    // ── Botão engrenagem ────────────────────────────────────────────────────────
    private void BuildGearButton(Transform parent)
    {
        gearBtn = new GameObject("GearBtn");
        gearBtn.transform.SetParent(parent, false);

        var rect = gearBtn.AddComponent<RectTransform>();
        rect.anchorMin       = new Vector2(1f, 1f);
        rect.anchorMax       = new Vector2(1f, 1f);
        rect.pivot           = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-28f, -220f); // canto top-right, abaixo do perfil
        rect.sizeDelta       = new Vector2(110f, 110f);

        var img = gearBtn.AddComponent<Image>();
        img.sprite         = Resources.Load<Sprite>("art/Settings/gear_icon");
        img.preserveAspect = true;
        img.color          = Color.white;
        img.raycastTarget  = true;

        var btn = gearBtn.AddComponent<Button>();
        var colors = btn.colors;
        colors.normalColor      = Color.white;
        colors.highlightedColor = new Color(1f, 0.95f, 0.6f, 1f);
        colors.pressedColor     = new Color(0.75f, 0.6f, 0f, 1f);
        btn.colors = colors;
        btn.onClick.AddListener(OpenOriginalPopup);
    }

    private void OpenOriginalPopup()
    {
        try
        {
            var go = GameObject.Find("Canvas - MainMenu");
            if (go != null)
            {
                var mainMenuView = go.GetComponent("MainMenuView");
                if (mainMenuView != null)
                {
                    var method = mainMenuView.GetType().GetMethod("OpenSettingsInGame",
                        System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Instance);
                    method?.Invoke(mainMenuView, null);
                }
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("[Settings] Falhou ao abrir o popup original: " + e.Message);
        }
    }

    private void InterceptOriginalPopup()
    {
        if (!wasInGameplay) return; // Só intercepta na mesa de jogo

        var popupObj = GameObject.Find("UIPopup(Clone)");
        if (popupObj == null) popupObj = GameObject.Find("UIPopup");
        
        if (popupObj != null && popupObj.activeInHierarchy)
        {
            var buttonsTransform = popupObj.transform.Find("Root/Root/Buttons");
            if (buttonsTransform != null)
            {
                // Protege contra desativação pelo GameplayHudSimplifier
                buttonsTransform.gameObject.hideFlags = HideFlags.DontSave;
                
                var cg = buttonsTransform.GetComponent<UnityEngine.CanvasGroup>();
                if (cg != null)
                {
                    UnityEngine.Object.Destroy(cg);
                }

                // Configura o Botão 1 (Sair da Partida)
                var b1 = buttonsTransform.Find("Button - (1)");
                if (b1 != null)
                {
                    b1.gameObject.hideFlags = HideFlags.DontSave;
                    if (!b1.gameObject.activeSelf)
                    {
                        b1.gameObject.SetActive(true);
                    }
                    
                    var txtGo = b1.Find("Text (TMP)");
                    if (txtGo != null)
                    {
                        txtGo.gameObject.hideFlags = HideFlags.DontSave;
                        var txt = txtGo.GetComponent<TMPro.TextMeshProUGUI>();
                        if (txt != null && txt.text != "Sair da Partida")
                        {
                            txt.text = "Sair da Partida";
                        }
                    }
                    
                    var btn = b1.GetComponent<UnityEngine.UI.Button>();
                    if (btn != null)
                    {
                        btn.onClick.RemoveAllListeners();
                        btn.onClick.AddListener(ExitGameFromOriginalPopup);
                    }
                }

                // Configura o Botão 0 (Continuar Jogando)
                var b0 = buttonsTransform.Find("Button - (0)");
                if (b0 != null)
                {
                    b0.gameObject.hideFlags = HideFlags.DontSave;
                    if (!b0.gameObject.activeSelf)
                    {
                        b0.gameObject.SetActive(true);
                    }
                    
                    var txtGo = b0.Find("Text (TMP)");
                    if (txtGo != null)
                    {
                        txtGo.gameObject.hideFlags = HideFlags.DontSave;
                        var txt = txtGo.GetComponent<TMPro.TextMeshProUGUI>();
                        if (txt != null && txt.text != "Continuar Jogando")
                        {
                            txt.text = "Continuar Jogando";
                        }
                    }
                }
            }
        }
    }

    private void ExitGameFromOriginalPopup()
    {
        var popupObj = GameObject.Find("UIPopup(Clone)");
        if (popupObj == null) popupObj = GameObject.Find("UIPopup");
        if (popupObj != null)
        {
            popupObj.SetActive(false);
        }
        ExitGame();
    }

    // ─── Ação: sair da partida ────────────────────────────────────────────────────────
    private void ExitGame()
    {
        // Suprime popup de desconexão do servidor
        NetworkManagerController.SuppressDisconnectPopup(10f);

        // Chama StopDominoEarly via reflection (encerra a partida corretamente)
        try
        {
            var asm = System.Reflection.Assembly.Load("GBTemplates.Domino.Controller");
            var dcType = asm?.GetType("GBTemplates.Domino.Controller.DominoController");
            if (dcType != null)
            {
                var domCtrl = UnityEngine.Object.FindObjectOfType(dcType);
                if (domCtrl != null)
                {
                    var method = dcType.GetMethod("StopDominoEarly",
                        System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Instance);
                    method?.Invoke(domCtrl, null);
                }
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("[Settings] StopDominoEarly falhou: " + e.Message);
        }

        // Aguarda 1 frame antes de carregar a cena
        StartCoroutine(LoadMenuNextFrame());
    }

    private System.Collections.IEnumerator LoadMenuNextFrame()
    {
        yield return null;
        SceneManager.LoadScene("DominoTemplate");
    }
}
