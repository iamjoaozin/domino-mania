using TMPro;
using GBTemplates.Domino.Controller;
using GBTemplates.Domino.Model;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(37000)]
public sealed class InfiniteMatchSessionCleaner : MonoBehaviour
{
    private static InfiniteMatchSessionCleaner instance;
    private static readonly BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private float nextSweepTime;
    private float lastAutoContinueTime = -999f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
        {
            return;
        }

        GameObject obj = new GameObject("Infinite Match Session Cleaner");
        DontDestroyOnLoad(obj);
        instance = obj.AddComponent<InfiniteMatchSessionCleaner>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void LateUpdate()
    {
        if (Time.unscaledTime < nextSweepTime)
        {
            return;
        }

        nextSweepTime = Time.unscaledTime + 0.25f;
        ResetNativeSeriesState();
        HideBestOfThreeUi();
    }

    private void ResetNativeSeriesState()
    {
        DominoController[] controllers = Resources.FindObjectsOfTypeAll<DominoController>();
        for (int i = 0; i < controllers.Length; i++)
        {
            DominoController controller = controllers[i];
            if (controller == null || !CanTouch(controller.gameObject))
            {
                continue;
            }

            SetNetworkValue(controller, "_player1RoundWins", 0);
            SetNetworkValue(controller, "_player2RoundWins", 0);
            SetNetworkValue(controller, "_matchWinner", Owner.None);
            SetFieldValue(controller, "_roundsCompleted", 0);

            GameObject overlay = GetFieldValue<GameObject>(controller, "_seriesResultOverlay");
            if (overlay != null && CanTouch(overlay))
            {
                bool wasVisible = overlay.activeInHierarchy;
                if (wasVisible && Time.unscaledTime - lastAutoContinueTime > 1.5f)
                {
                    lastAutoContinueTime = Time.unscaledTime;
                    Button playButton = GetFieldValue<Button>(controller, "_seriesResultPlayButton");
                    if (playButton != null)
                    {
                        playButton.onClick.Invoke();
                    }
                }

                overlay.SetActive(false);
                HideBlock(overlay);
            }
        }
    }

    private static void HideBestOfThreeUi()
    {
        TMP_Text[] tmpTexts = Resources.FindObjectsOfTypeAll<TMP_Text>();
        for (int i = 0; i < tmpTexts.Length; i++)
        {
            TMP_Text text = tmpTexts[i];
            if (text == null || !CanTouch(text.gameObject))
            {
                continue;
            }

            if (ShouldHideText(text.text) || ShouldHideObjectName(text.gameObject.name))
            {
                HideAncestorPanel(text.rectTransform);
            }
        }

        Text[] legacyTexts = Resources.FindObjectsOfTypeAll<Text>();
        for (int i = 0; i < legacyTexts.Length; i++)
        {
            Text text = legacyTexts[i];
            if (text == null || !CanTouch(text.gameObject))
            {
                continue;
            }

            if (ShouldHideText(text.text) || ShouldHideObjectName(text.gameObject.name))
            {
                HideAncestorPanel(text.rectTransform);
            }
        }

        GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < objects.Length; i++)
        {
            GameObject obj = objects[i];
            if (obj == null || !CanTouch(obj))
            {
                continue;
            }

            if (ShouldHideObjectName(obj.name))
            {
                HideBlock(obj);
            }
        }
    }

    private static bool ShouldHideText(string rawText)
    {
        string value = Normalize(rawText);
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        if (value.Contains("MELHOR DE") || value.Contains("BEST OF"))
        {
            return true;
        }

        if (value.Contains("SERIE VENCIDA") ||
            value.Contains("SERIE FINALIZADA") ||
            value.Contains("DOMINOU A MESA") ||
            value.Contains("JOGAR DE NOVO"))
        {
            return true;
        }

        if (value.Contains("VITORIAS") && (value.Contains(" X ") || value.Contains("X")))
        {
            return true;
        }

        if (value.Contains("PONTOS") && (value.Contains("P1") || value.Contains("P2")))
        {
            return true;
        }

        if ((value.Contains("PARTIDA") || value.Contains("RODADA") || value.Contains("ROUND") || value.Contains("MATCH")) && value.Contains("/3"))
        {
            return true;
        }

        if ((value.Contains("PARTIDA") || value.Contains("RODADA") || value.Contains("ROUND") || value.Contains("MATCH")) &&
            (value.Contains("1 DE 3") || value.Contains("2 DE 3") || value.Contains("3 DE 3") || value.Contains("1 OF 3") || value.Contains("2 OF 3") || value.Contains("3 OF 3")))
        {
            return true;
        }

        return false;
    }

    private static bool ShouldHideObjectName(string rawName)
    {
        string name = Normalize(rawName);
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        return name.Contains("BESTOF") ||
            name.Contains("BEST OF") ||
            name.Contains("MELHOR") ||
            name.Contains("ROUND COUNTER") ||
            name.Contains("ROUNDCOUNTER") ||
            name.Contains("MATCH COUNTER") ||
            name.Contains("MATCHCOUNTER") ||
            name.Contains("MATCHSCORE") ||
            name.Contains("ROUND SCORE") ||
            name.Contains("ROUNDSCORE") ||
            name.Contains("SERIES RESULT") ||
            name.Contains("SERIESRESULT") ||
            name.Contains("SERIE") ||
            name == "PLAYERSCORETXT" ||
            name == "IASCORE";
    }

    private static void HideAncestorPanel(RectTransform start)
    {
        if (start == null)
        {
            return;
        }

        RectTransform current = start;
        RectTransform candidate = start;
        for (int i = 0; i < 8 && current != null; i++)
        {
            float width = Mathf.Abs(current.rect.width);
            float height = Mathf.Abs(current.rect.height);
            if (width >= 24f && width <= 1100f && height >= 14f && height <= 980f)
            {
                candidate = current;
            }

            if (current.GetComponent<Canvas>() != null)
            {
                break;
            }

            current = current.parent as RectTransform;
        }

        HideBlock(candidate.gameObject);
    }

    private static void HideBlock(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        CanvasGroup group = target.GetComponent<CanvasGroup>();
        if (group == null)
        {
            group = target.AddComponent<CanvasGroup>();
        }

        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        Graphic[] graphics = target.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            if (graphics[i] != null)
            {
                graphics[i].raycastTarget = false;
            }
        }
    }

    private static bool CanTouch(GameObject obj)
    {
        return obj != null && obj.scene.IsValid() && obj.scene.isLoaded;
    }

    private static T GetFieldValue<T>(object target, string fieldName) where T : class
    {
        if (target == null)
        {
            return null;
        }

        FieldInfo field = target.GetType().GetField(fieldName, InstanceFlags);
        if (field == null)
        {
            return null;
        }

        return field.GetValue(target) as T;
    }

    private static void SetFieldValue(object target, string fieldName, object value)
    {
        if (target == null)
        {
            return;
        }

        FieldInfo field = target.GetType().GetField(fieldName, InstanceFlags);
        if (field != null)
        {
            try
            {
                field.SetValue(target, value);
            }
            catch
            {
            }
        }
    }

    private static void SetNetworkValue(object target, string fieldName, object value)
    {
        if (target == null)
        {
            return;
        }

        FieldInfo field = target.GetType().GetField(fieldName, InstanceFlags);
        if (field == null)
        {
            return;
        }

        object variable = field.GetValue(target);
        if (variable == null)
        {
            return;
        }

        PropertyInfo valueProperty = variable.GetType().GetProperty("Value", InstanceFlags);
        if (valueProperty != null && valueProperty.CanWrite)
        {
            try
            {
                valueProperty.SetValue(variable, value, null);
            }
            catch
            {
            }
        }
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        string text = value.ToUpperInvariant();
        text = text.Replace((char)193, 'A').Replace((char)192, 'A').Replace((char)195, 'A').Replace((char)194, 'A');
        text = text.Replace((char)201, 'E').Replace((char)202, 'E');
        text = text.Replace((char)205, 'I');
        text = text.Replace((char)211, 'O').Replace((char)212, 'O').Replace((char)213, 'O');
        text = text.Replace((char)218, 'U');
        text = text.Replace((char)199, 'C');
        return text.Trim();
    }
}
