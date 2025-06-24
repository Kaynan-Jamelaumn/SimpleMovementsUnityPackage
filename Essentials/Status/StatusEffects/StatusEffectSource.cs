using System;

[Serializable]
public class StatusModification
{
    public enum ModificationType
    {
        Flat,
        Percentage,
        Override
    }

    public string statName;
    public float value;
    public ModificationType type;
    public bool isPermanent;
    public float duration;
    public string sourceId;

    public StatusModification(string statName, float value, ModificationType type, bool isPermanent = true, float duration = 0f, string sourceId = "")
    {
        this.statName = statName;
        this.value = value;
        this.type = type;
        this.isPermanent = isPermanent;
        this.duration = duration;
        this.sourceId = sourceId;
    }
}