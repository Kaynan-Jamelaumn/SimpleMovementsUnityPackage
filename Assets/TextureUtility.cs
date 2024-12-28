using UnityEditor;
using UnityEngine;

public class TextureUtility : MonoBehaviour
{
    [MenuItem("Tools/Enable Read/Write for All Textures")]
    public static void EnableReadWriteForAllTextures()
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;

            if (importer != null && !importer.isReadable)
            {
                importer.isReadable = true;
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                Debug.Log($"Enabled Read/Write for: {path}");
            }
        }
    }
}
