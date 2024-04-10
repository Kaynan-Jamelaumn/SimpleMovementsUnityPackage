using UnityEngine;

public static class TextureGenerator
{
    public static void AssignTexture(Texture2D splatMap, TerrainGenerator terrainGenerator)
    {
        Material mat = new Material(Shader.Find("Custom/TerrainSplatMapShader"));
        //mat.SetTexture("_MainTex", defaultTexture);
        mat.SetTexture("_TextureR", terrainGenerator.Biomes[0].texture);
        mat.SetTexture("_TextureG", terrainGenerator.Biomes[1].texture);
        mat.SetTexture("_TextureB", terrainGenerator.Biomes[2].texture);
        mat.SetTexture("_TextureA", terrainGenerator.Biomes[3].texture);
        mat.SetTexture("_SplatMap", splatMap);
        terrainGenerator.GetComponent<Renderer>().sharedMaterial = mat;
    }
    public static void AssignTexture(RenderTexture splatMap, TerrainGenerator terrainGenerator)
    {
        Material mat = new Material(Shader.Find("Custom/TerrainSplatMapShader"));
        //mat.SetTexture("_MainTex", defaultTexture);
        mat.SetTexture("_TextureR", terrainGenerator.Biomes[0].texture);
        mat.SetTexture("_TextureG", terrainGenerator.Biomes[1].texture);
        mat.SetTexture("_TextureB", terrainGenerator.Biomes[2].texture);
        mat.SetTexture("_TextureA", terrainGenerator.Biomes[3].texture);
        mat.SetTexture("_SplatMap", splatMap);
        terrainGenerator.GetComponent<Renderer>().sharedMaterial = mat;
    }
    public static void AssignTexture(Texture2D splatMap, TerrainGenerator terrainGenerator, MeshRenderer meshRenderer)
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



}