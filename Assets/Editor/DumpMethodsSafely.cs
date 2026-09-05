using UnityEngine;
using UnityEditor;
using System.Reflection;
using GBTemplates.Domino.Controller;
using System.IO;

[InitializeOnLoad]
public class DumpMethodsSafely
{
    static DumpMethodsSafely()
    {
        Dump();
    }

    [MenuItem("Gemini/Dump Methods Safely")]
    public static void Dump()
    {
        using (StreamWriter writer = new StreamWriter("dump_methods.txt"))
        {
            writer.WriteLine("--- DominoController Methods ---");
            foreach(var method in typeof(DominoController).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
            {
                if(method.DeclaringType == typeof(DominoController))
                    writer.WriteLine(method.Name);
            }

            writer.WriteLine("--- DominoTileWorld Methods ---");
            foreach(var method in typeof(DominoTileWorld).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
            {
                if(method.DeclaringType == typeof(DominoTileWorld))
                    writer.WriteLine(method.Name);
            }
        }
    }
}
