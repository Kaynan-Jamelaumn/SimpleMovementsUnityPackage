using UnityEngine;

public static class GenericMethods
{
    public static float GetRandomValue(float baseValue, bool useRandom, float minValue, float maxValue)
    {
        return useRandom ? UnityEngine.Random.Range(minValue, maxValue) : baseValue;
    }

    public static bool IsCurveConstant(AnimationCurve curve)
    {
        float firstValue = curve.Evaluate(0f);
        for (float t = 0.01f; t <= 1f; t += 0.01f)
        {
            if (curve.Evaluate(t) != firstValue)
            {
                return false;
            }
        }
        return true;
    }

}

