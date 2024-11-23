using UnityEngine;
public static class SingleHeightGenerator
{

    public static float[,] GenerateHeightMap(SingleTerrainGenerator terrainGenerator)
    {
        float[,] heightMap = new float[terrainGenerator.GridWidthSize + 1, terrainGenerator.GridDepthSize + 1];
        for (int y = 0; y <= terrainGenerator.GridDepthSize; y++)
        {
            for (int x = 0; x <= terrainGenerator.GridWidthSize; x++)
            {
                //terrainGenerator.CellMap[x, y] = CalculateHeight(x, y, terrainGenerator);
                heightMap[x, y] = CalculateHeight(x, y, terrainGenerator);
            }
        }
        return heightMap;

    }
    private static float CalculateHeight(int x, int y, SingleTerrainGenerator terrainGenerator)
    {
        float amplitude = terrainGenerator.Amplitude;
        float frequency = terrainGenerator.BaseFrequency;
        float height = 0;
        AnimationCurve heightCurve =  new AnimationCurve(terrainGenerator.heightCurve.keys);

        for (int o = 0; o < terrainGenerator.Octaves; o++)
        {
            float sampleX = x / (float)terrainGenerator.GridWidthSize * frequency;
            float sampleY = y / (float)terrainGenerator.GridDepthSize * frequency;

            float perlinValue = Mathf.PerlinNoise(sampleX + 0.5f, sampleY + 0.5f) * 2 - 1;
            height += perlinValue * amplitude;
            //height = terrainGenerator.heightCurve.Evaluate(height);

            amplitude *= terrainGenerator.Persistence;
            frequency *= terrainGenerator.Lacunarity;

            // Early exit optimization
            if (amplitude < 0.001f)
                break;
        }
        if (height > terrainGenerator.MaxHeight) terrainGenerator.MaxHeight = height;
        if (height < terrainGenerator.MinHeight) terrainGenerator.MinHeight = height;
        
        if (GenericMethods.IsCurveConstant(heightCurve)) return height;
        // Normalize height to a range between 0 and 1
        float normalizedHeight = Mathf.InverseLerp(terrainGenerator.MinHeight, terrainGenerator.MaxHeight, height);

        // Apply AnimationCurve to the normalized height
        float curvedHeight = heightCurve.Evaluate(normalizedHeight);

        // Desnormalize curvedHeight to original scale
        float originalHeight = Mathf.Lerp(terrainGenerator.MinHeight, terrainGenerator.MaxHeight, curvedHeight);

        return originalHeight;
    }

}
