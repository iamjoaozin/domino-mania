using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

public class AvatarSlicer : EditorWindow
{
    [MenuItem("Dominó Mania/Recortar Avatares Automaticamente")]
    public static void SliceAvatars()
    {
        string path = EditorUtility.OpenFilePanel("Selecione a imagem dos avatares", "", "png,jpg,jpeg");
        if (string.IsNullOrEmpty(path)) return;

        byte[] fileData = File.ReadAllBytes(path);
        Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.LoadImage(fileData);

        // Encontrar os limites da imagem ignorando o fundo branco/transparente
        Color32[] pixels = tex.GetPixels32();
        int width = tex.width;
        int height = tex.height;

        bool IsBackground(Color32 c)
        {
            return c.a < 10 || (c.r > 240 && c.g > 240 && c.b > 240);
        }

        // Dividir a imagem em 4 colunas e 4 linhas
        // Como tem uma parte branca gigante embaixo, vamos focar só na parte que tem pixels escuros
        int minY = height, maxY = 0;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (!IsBackground(pixels[y * width + x]))
                {
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
        }

        // A altura util da imagem contem os 4 avatares
        int usefulHeight = maxY - minY;
        int avatarHeight = usefulHeight / 4;
        int avatarWidth = width / 4;

        // O tamanho real do avatar é o menor entre width e height (para ficar quadrado)
        int size = Mathf.Min(avatarWidth, avatarHeight);

        string saveDir = Application.dataPath + "/Sprites/Avatars";
        if (!Directory.Exists(saveDir)) Directory.CreateDirectory(saveDir);

        int count = 1;
        for (int row = 3; row >= 0; row--) // Linhas de cima pra baixo (Unity Y é invertido)
        {
            for (int col = 0; col < 4; col++)
            {
                int startX = col * avatarWidth + (avatarWidth - size) / 2;
                int startY = minY + row * avatarHeight + (avatarHeight - size) / 2;

                Texture2D avatarTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                Color[] avatarPixels = tex.GetPixels(startX, startY, size, size);
                
                // Aplica mascara circular pra garantir
                Vector2 center = new Vector2(size / 2f, size / 2f);
                float radius = size / 2f;
                for (int i = 0; i < avatarPixels.Length; i++)
                {
                    int px = i % size;
                    int py = i / size;
                    if (Vector2.Distance(new Vector2(px, py), center) > radius)
                    {
                        avatarPixels[i] = new Color(0, 0, 0, 0); // Transparente fora do circulo
                    }
                    else if (IsBackground(avatarPixels[i]))
                    {
                        avatarPixels[i] = new Color(0, 0, 0, 0); // Transparente no fundo branco
                    }
                }

                avatarTex.SetPixels(avatarPixels);
                avatarTex.Apply();

                byte[] pngData = avatarTex.EncodeToPNG();
                string savePath = saveDir + "/Avatar_" + count + ".png";
                File.WriteAllBytes(savePath, pngData);
                count++;
            }
        }

        AssetDatabase.Refresh();

        Debug.Log("<color=green><b>Avatares recortados com sucesso!</b></color> Salvos em Assets/Sprites/Avatars.");
    }
}
