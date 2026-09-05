using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class SpriteBackgroundRemover
{
    public static void RemoveCheckerboardAndBackground(string spritePath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(spritePath) as TextureImporter;
        if (importer == null) return;

        bool isReadable = importer.isReadable;
        TextureImporterCompression comp = importer.textureCompression;
        
        // Prepare for reading
        importer.isReadable = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();

        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(spritePath);
        if (tex == null) return;

        Color[] pixels = tex.GetPixels();
        int width = tex.width;
        int height = tex.height;

        // Find edge colors
        List<Color> borderColors = new List<Color>();
        for (int x = 0; x < width; x++)
        {
            borderColors.Add(pixels[x]); // Bottom
            borderColors.Add(pixels[(height - 1) * width + x]); // Top
        }
        for (int y = 0; y < height; y++)
        {
            borderColors.Add(pixels[y * width]); // Left
            borderColors.Add(pixels[y * width + (width - 1)]); // Right
        }

        // Flood fill from edges
        bool[] visited = new bool[width * height];
        Queue<int> queue = new Queue<int>();

        // Push all borders to queue if they match a border color
        for (int x = 0; x < width; x++)
        {
            queue.Enqueue(x);
            queue.Enqueue((height - 1) * width + x);
            visited[x] = true;
            visited[(height - 1) * width + x] = true;
        }
        for (int y = 0; y < height; y++)
        {
            if (!visited[y * width]) { queue.Enqueue(y * width); visited[y * width] = true; }
            if (!visited[y * width + (width - 1)]) { queue.Enqueue(y * width + (width - 1)); visited[y * width + (width - 1)] = true; }
        }

        // Tolerance for matching border colors
        float tolerance = 0.15f;
        
        while (queue.Count > 0)
        {
            int idx = queue.Dequeue();
            int cx = idx % width;
            int cy = idx / width;

            Color c = pixels[idx];
            bool isBg = false;
            
            // Checar se a cor eh parecida com alguma cor da borda, ou se eh puro branco/cinza claro (checkerboard)
            if (c.r > 0.8f && c.g > 0.8f && c.b > 0.8f) isBg = true; // White/Grey checkerboard
            else
            {
                foreach (Color bc in borderColors)
                {
                    if (Mathf.Abs(c.r - bc.r) < tolerance && Mathf.Abs(c.g - bc.g) < tolerance && Mathf.Abs(c.b - bc.b) < tolerance)
                    {
                        isBg = true;
                        break;
                    }
                }
            }

            if (isBg)
            {
                pixels[idx] = new Color(0, 0, 0, 0); // Make transparent

                // Add neighbors
                if (cx > 0 && !visited[idx - 1]) { queue.Enqueue(idx - 1); visited[idx - 1] = true; }
                if (cx < width - 1 && !visited[idx + 1]) { queue.Enqueue(idx + 1); visited[idx + 1] = true; }
                if (cy > 0 && !visited[idx - width]) { queue.Enqueue(idx - width); visited[idx - width] = true; }
                if (cy < height - 1 && !visited[idx + width]) { queue.Enqueue(idx + width); visited[idx + width] = true; }
            }
        }

        // Apply changes
        tex.SetPixels(pixels);
        tex.Apply();
        
        byte[] bytes = tex.EncodeToPNG();
        System.IO.File.WriteAllBytes(spritePath, bytes);

        // Restore settings
        importer.isReadable = isReadable;
        importer.textureCompression = comp;
        importer.SaveAndReimport();
    }
}
