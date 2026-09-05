using System;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Removes the old camera "PONTA" switch UI so the board flow can be rebuilt cleanly.
/// This intentionally does not touch domino rules, player hand, turn logic, spacing, or tile placement.
/// </summary>
[DefaultExecutionOrder(32000)]
public sealed class DominoOpenEndSwitchRemover : MonoBehaviour
{
    private const BindingFlags FieldFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const string CameraControllerTypeName = "GBTemplates.Domino.Controller.CameraController";
    private static DominoOpenEndSwitchRemover instance;
    private float nextScanTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
        {
            return;
        }

        var runner = new GameObject(nameof(DominoOpenEndSwitchRemover));
        DontDestroyOnLoad(runner);
        instance = runner.AddComponent<DominoOpenEndSwitchRemover>();
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
        RemoveOpenEndSwitch();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void LateUpdate()
    {
        if (Time.unscaledTime < nextScanTime)
        {
            return;
        }

        nextScanTime = Time.unscaledTime + 0.2f;
        RemoveOpenEndSwitch();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        nextScanTime = 0f;
        RemoveOpenEndSwitch();
    }

    private static void RemoveOpenEndSwitch()
    {
        DisableCameraControllerSwitch();
        DisableObjectsNamedOpenEndSwitch();
        DisablePontaLabels();
    }

    private static void DisableCameraControllerSwitch()
    {
        foreach (var behaviour in Resources.FindObjectsOfTypeAll<MonoBehaviour>())
        {
            if (behaviour == null || !IsSceneObject(behaviour.gameObject))
            {
                continue;
            }

            var type = behaviour.GetType();
            if (type.FullName != CameraControllerTypeName)
            {
                continue;
            }

            SetField(type, behaviour, "_showOpenEndSwitchButton", false);
            SetField(type, behaviour, "_openEndSwitchShouldShow", false);

            DisableFieldObject(type, behaviour, "_openEndSwitchRoot");
            DisableFieldObject(type, behaviour, "_openEndSwitchCanvas");
            DisableFieldObject(type, behaviour, "_openEndSwitchLabel");

            var button = GetField<Button>(type, behaviour, "_openEndSwitchButton");
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.interactable = false;
                DisableObject(button.gameObject);
            }
        }
    }

    private static void DisableFieldObject(Type type, object owner, string fieldName)
    {
        var value = GetField<Object>(type, owner, fieldName);
        if (value == null)
        {
            return;
        }

        switch (value)
        {
            case GameObject gameObject:
                DisableObject(gameObject);
                break;
            case Component component:
                DisableObject(component.gameObject);
                break;
        }
    }

    private static T GetField<T>(Type type, object owner, string fieldName) where T : class
    {
        var field = type.GetField(fieldName, FieldFlags);
        return field?.GetValue(owner) as T;
    }

    private static void SetField(Type type, object owner, string fieldName, object value)
    {
        var field = type.GetField(fieldName, FieldFlags);
        if (field != null)
        {
            field.SetValue(owner, value);
        }
    }

    private static void DisableObjectsNamedOpenEndSwitch()
    {
        foreach (var gameObject in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (gameObject == null || !IsSceneObject(gameObject))
            {
                continue;
            }

            if (gameObject.name.IndexOf("Open End Switch", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                DisableObject(gameObject);
            }
        }
    }

    private static void DisablePontaLabels()
    {
        foreach (var text in Resources.FindObjectsOfTypeAll<TMP_Text>())
        {
            if (text == null || !IsSceneObject(text.gameObject))
            {
                continue;
            }

            var label = text.text;
            if (string.IsNullOrEmpty(label) || label.IndexOf("PONTA", StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            text.text = string.Empty;

            var button = text.GetComponentInParent<Button>(true);
            DisableObject(button != null ? button.gameObject : text.gameObject);
        }
    }

    private static void DisableObject(GameObject gameObject)
    {
        if (gameObject != null && gameObject.scene.IsValid())
        {
            gameObject.SetActive(false);
        }
    }

    private static bool IsSceneObject(GameObject gameObject)
    {
        return gameObject != null && gameObject.scene.IsValid() && gameObject.scene.isLoaded;
    }
}
