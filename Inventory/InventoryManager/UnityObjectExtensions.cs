/// <summary>
/// Unity objects that were destroyed or never assigned compare equal to null with ==, but the C# ?. and ??
/// operators do not see that and call into them (UnassignedReferenceException / MissingReferenceException).
/// <c>obj.Live()?.Member</c> is the safe form.
/// </summary>
public static class UnityObjectExtensions
{
    public static T Live<T>(this T obj) where T : UnityEngine.Object => obj != null ? obj : null;
}
