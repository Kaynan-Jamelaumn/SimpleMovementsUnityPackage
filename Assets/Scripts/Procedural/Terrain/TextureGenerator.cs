using UnityEngine;

public  class TextureGenerator
{
    public static void AssignTexture4Textures(Texture2D splatMap, TerrainGenerator terrainGenerator, MeshRenderer meshRenderer)
    {
        Material mat = new Material(Shader.Find("Custom/TerrainSplatMapShader"));
        //mat.SetTexture("_MainTex", defaultTexture);
        mat.SetTexture("_TextureR", terrainGenerator.Biomes[0].texture);
        mat.SetTexture("_TextureG", terrainGenerator.Biomes[1].texture);
        mat.SetTexture("_TextureB", terrainGenerator.Biomes[2].texture);
        mat.SetTexture("_TextureA", terrainGenerator.Biomes[3].texture);
        mat.SetTexture("_SplatMap", splatMap);
        meshRenderer.sharedMaterial = mat;
    }
    public void AssignTexture(Texture2D splatMap, TerrainGenerator terrainGenerator, MeshRenderer meshRenderer)
    {
        Shader shader = Shader.Find("Custom/TerrainSplatMapShaderHDRP"); // Ensure you replace with your HDRP shader's name
        if (shader == null)
        {
            Debug.LogError("Failed to find shader: HDRP/TerrainSplatMapShader");
            return;
        }
        Material mat = new Material(shader);

        // Define a consistent texture format and size
        int textureWidth = 1024; // Set your desired width
        int textureHeight = 1024; // Set your desired height
        TextureFormat format = TextureFormat.RGBA32;

        // Create the Texture2DArray
        Texture2DArray textureArray = new Texture2DArray(textureWidth, textureHeight, terrainGenerator.Biomes.Length, format, true);

        for (int i = 0; i < terrainGenerator.Biomes.Length; i++)
        {
            Texture2D sourceTexture = terrainGenerator.Biomes[i].texture;

            // Standardize the texture
            Texture2D standardizedTexture = StandardizeTexture(sourceTexture, textureWidth, textureHeight, format);

            // Copy the standardized texture to the Texture2DArray
            Graphics.CopyTexture(standardizedTexture, 0, 0, textureArray, i, 0);
        }

        textureArray.Apply();

        // Assign the textures and splat map to the material
        mat.SetTexture("_TextureArray", textureArray);
        mat.SetTexture("_SplatMap", splatMap);

        if (mat == null)
        {
            Debug.LogError("Failed to create material: Shader not found!");
        }


        meshRenderer.sharedMaterial = mat;

        if (meshRenderer.sharedMaterial == null)
        {
            Debug.LogError("Failed to assign material to MeshRenderer!");
        }

        meshRenderer.sharedMaterial = mat;
    }
    Texture2D StandardizeTexture(Texture2D sourceTexture, int width, int height, TextureFormat format)
    {
        // Create a new texture with the desired format and dimensions
        Texture2D standardizedTexture = new Texture2D(width, height, format, true);
        standardizedTexture.SetPixels(sourceTexture.GetPixels());
        standardizedTexture.Apply();
        return standardizedTexture;
    }

}