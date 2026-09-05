using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class UniversalClickJuice : MonoBehaviour
{
    private AudioClip clickSound;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        var go = new GameObject("UniversalClickJuice");
        DontDestroyOnLoad(go);
        go.AddComponent<UniversalClickJuice>();
    }

    private void Start()
    {
#if UNITY_EDITOR
        string path = "Assets/Resources/Audio/ui_click.wav";
        clickSound = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(path);
#endif
        if (clickSound == null) {
            clickSound = Resources.Load<AudioClip>("Audio/ui_click");
        }
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            bool clickedInteractable = false;

            // 1. Check UI Canvas
            if (EventSystem.current != null)
            {
                PointerEventData pointerData = new PointerEventData(EventSystem.current)
                {
                    position = Input.mousePosition
                };

                List<RaycastResult> results = new List<RaycastResult>();
                EventSystem.current.RaycastAll(pointerData, results);

                foreach (RaycastResult result in results)
                {
                    if (result.gameObject.GetComponentInParent<Button>() != null || 
                        result.gameObject.GetComponentInParent<Toggle>() != null ||
                        result.gameObject.name.ToLower().Contains("button") ||
                        result.gameObject.name.ToLower().Contains("btn"))
                    {
                        clickedInteractable = true;
                        break;
                    }
                }
            }

            // 2. Check Physics 2D (Dominoes on hand/board)
            if (!clickedInteractable && Camera.main != null)
            {
                Vector2 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                RaycastHit2D hit = Physics2D.Raycast(mousePosition, Vector2.zero);
                
                if (hit.collider != null)
                {
                    clickedInteractable = true;
                }
            }

            // Play the sound!
            if (clickedInteractable && clickSound != null)
            {
                AudioSource.PlayClipAtPoint(clickSound, Camera.main != null ? Camera.main.transform.position : Vector3.zero, 1f);
            }
        }
    }
}
