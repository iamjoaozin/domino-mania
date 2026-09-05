using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CopiarBgMoeda
{
    [MenuItem("Gemini/Copiar BG Moeda para o Jogo")]
    public static void Executar()
    {
        Scene mainScene = EditorSceneManager.GetActiveScene();

        GameObject bgMoedaOriginal = null;
        GameObject[] roots = mainScene.GetRootGameObjects();
        foreach(var root in roots)
        {
            // Ignora copias antigas feitas pelo script
            if (root.name == "bg moeda (Gameplay)") continue;

            Transform t = FindRecursive(root.transform, "moeda");
            if (t != null) 
            {
                bgMoedaOriginal = t.gameObject;
                break;
            }
        }

        if (bgMoedaOriginal != null)
        {
            // Remove a copia antiga se existir
            GameObject oldCopia = GameObject.Find("bg moeda (Gameplay)");
            if (oldCopia != null) Object.DestroyImmediate(oldCopia);

            GameObject copia = Object.Instantiate(bgMoedaOriginal);
            copia.name = "bg moeda (Gameplay)";
            
            if (copia.GetComponent<CoinHudUpdater>() == null)
            {
                copia.AddComponent<CoinHudUpdater>();
            }

            GameObject gameplayCanvas = GameObject.Find("Canvas - Game"); 

            if (gameplayCanvas != null)
            {
                copia.transform.SetParent(gameplayCanvas.transform, false);
                
                // Joga pro final para renderizar por cima de tudo
                copia.transform.SetAsLastSibling();

                RectTransform rect = copia.GetComponent<RectTransform>();
                if (rect != null)
                {
                    // Ancora no topo e no meio
                    rect.anchorMin = new Vector2(0.5f, 1);
                    rect.anchorMax = new Vector2(0.5f, 1);
                    rect.pivot = new Vector2(0.5f, 1);
                    
                    // Diminui o tamanho pela metade
                    rect.localScale = new Vector3(0.6f, 0.6f, 0.6f);
                    
                    // Joga mais para cima (exatamente abaixo do HUD com uma respirada)
                    rect.anchoredPosition = new Vector2(0, -160);
                }
                EditorUtility.DisplayDialog("Sucesso!", "O 'bgmoeda' foi redimensionado, movido para o centro-abaixo do HUD e colocado na frente!", "Perfeito!");
            }
            else
            {
                EditorUtility.DisplayDialog("Quase lá...", "Achei o bgmoeda, mas não achei o 'Canvas - Game' principal do jogo para colar ele dentro.", "OK");
            }
        }
        else
        {
            EditorUtility.DisplayDialog("Ops", "Ainda não achei nenhum objeto original com 'moeda' no nome na cena aberta.", "OK");
        }
    }

    private static Transform FindRecursive(Transform parent, string keyword)
    {
        if (parent.name == "bg moeda (Gameplay)") return null; // Ignora o clone antigo

        if (parent.name.ToLower().Contains(keyword.ToLower())) return parent;
        foreach (Transform child in parent)
        {
            Transform found = FindRecursive(child, keyword);
            if (found != null) return found;
        }
        return null;
    }
}
