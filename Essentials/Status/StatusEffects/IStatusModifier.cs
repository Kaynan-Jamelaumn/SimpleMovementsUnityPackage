using System.Collections.Generic;
using UnityEngine;

public interface IStatusModifier
{
    string ModifierId { get; }
    string ModifierName { get; }
    bool IsActive { get; }
    List<StatusModification> GetStatusModifications();
    void OnStatusModificationApplied(StatusModification modification);
    void OnStatusModificationRemoved(StatusModification modification);
}