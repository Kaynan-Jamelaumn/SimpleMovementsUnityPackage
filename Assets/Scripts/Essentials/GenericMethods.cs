public static class GenericMethods
{
    public static float GetRandomValue(float baseValue, bool useRandom, float minValue, float maxValue)
    {
        return useRandom ? UnityEngine.Random.Range(minValue, maxValue) : baseValue;
    }


}

