using UnityEngine;

public static class SingleGenerateTexture
{
    public static void SingleAssignTexture(Texture2D splatMap, SingleTerrainGenerator terrainGenerator)
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
    public static void SingleAssignTexture(RenderTexture splatMap, SingleTerrainGenerator terrainGenerator)
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


}