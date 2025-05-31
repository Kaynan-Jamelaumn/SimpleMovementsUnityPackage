using UnityEngine;
public class WeaponStateCoordinator
{
    private WeaponController controller;
    private AttackExecutor attackExecutor;
    private ComboSystem comboSystem;
    private VariationSystem variationSystem;
    private InputBufferSystem inputBufferSystem;

    public WeaponStateCoordinator(WeaponController controller)
    {
        this.controller = controller;
    }

    public void SetDependencies(AttackExecutor attackExecutor, ComboSystem comboSystem,
        VariationSystem variationSystem, InputBufferSystem inputBufferSystem)
    {
        this.attackExecutor = attackExecutor;
        this.comboSystem = comboSystem;
        this.variationSystem = variationSystem;
        this.inputBufferSystem = inputBufferSystem;
    }

    public void ResetAllStates()
    {
        variationSystem?.Reset();
        comboSystem?.Reset();
        attackExecutor?.ResetAttackState();
        inputBufferSystem?.Reset();

        controller.LogDebug("All weapon states reset");
    }

    public void CleanupAll()
    {
        attackExecutor?.CleanupAllCoroutines();
        ResetAllStates();
    }
}