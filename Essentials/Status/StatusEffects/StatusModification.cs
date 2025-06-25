using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


[System.Serializable]
public class StatusModification
{
    public UnifiedStatusType statusType;
    public float baseValue;
    public float currentValue;
    public ModificationType modificationType;
    public bool isTemporary;
    public float duration;
    public string source;

    public StatusModification(UnifiedStatusType type, float value, ModificationType modType, string source, bool temp = false, float dur = 0f)
    {
        statusType = type;
        baseValue = value;
        currentValue = value;
        modificationType = modType;
        isTemporary = temp;
        duration = dur;
        this.source = source;
    }
}

public enum ModificationType
{
    Flat,          // Add/subtract flat value
    Percentage,    // Multiply by percentage
    Override       // Override to specific value
}