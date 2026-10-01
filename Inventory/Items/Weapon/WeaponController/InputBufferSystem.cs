using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Remembers attack inputs pressed while an attack is still playing, for a short time, and starts the next attack
/// as soon as the current one can be cancelled (see <see cref="AttackComponent.cancelPoint"/>). This is what makes
/// chains and combos feel responsive. Releases are remembered too, so a quick tap on a chargeable attack is not
/// mistaken for a long hold.
/// </summary>
public class InputBufferSystem
{
    private readonly WeaponController controller;
    private readonly Queue<BufferedInput> inputBuffer = new Queue<BufferedInput>();
    private float inputBufferTime;
    private bool enableInputBuffer;

    // Dependencies
    private AttackExecutor attackExecutor;

    public InputBufferSystem(WeaponController controller, float inputBufferTime, bool enableInputBuffer)
    {
        this.controller = controller;
        this.inputBufferTime = inputBufferTime;
        this.enableInputBuffer = enableInputBuffer;
    }

    public void SetDependencies(AttackExecutor attackExecutor)
    {
        this.attackExecutor = attackExecutor;
    }

    public void Configure(float time, bool enabled)
    {
        inputBufferTime = time;
        enableInputBuffer = enabled;
    }

    public int Count => inputBuffer.Count;

    public void BufferInput(AttackType attackType, GameObject player)
    {
        if (!enableInputBuffer) return;
        CleanOldInputs();
        // One pending input is enough; a newer press replaces the older one.
        inputBuffer.Clear();
        inputBuffer.Enqueue(new BufferedInput(attackType, Time.time, player));
        controller.LogDebug($"Input buffered: {attackType}");
    }

    /// <summary>The buffered input of this type was released (a tap, not a hold).</summary>
    public void BufferRelease(AttackType attackType)
    {
        if (inputBuffer.Count == 0) return;
        BufferedInput b = inputBuffer.Peek();
        if (b.AttackType == attackType && !b.Released)
        {
            inputBuffer.Clear();
            inputBuffer.Enqueue(new BufferedInput(b.AttackType, b.Timestamp, b.Player) { Released = true });
        }
    }

    public void ProcessInputBuffer()
    {
        if (inputBuffer.Count == 0) return;
        CleanOldInputs();
        if (inputBuffer.Count == 0 || !attackExecutor.CanStartNext) return;

        BufferedInput input = inputBuffer.Dequeue();
        controller.LogDebug($"Processing buffered input: {input.AttackType}");
        attackExecutor.BeginInput(input.Player, input.AttackType, fromBuffer: true, alreadyReleased: input.Released || !controller.IsInputHeld(input.AttackType));
    }

    private void CleanOldInputs()
    {
        while (inputBuffer.Count > 0 && Time.time - inputBuffer.Peek().Timestamp > inputBufferTime)
            inputBuffer.Dequeue();
    }

    public void Reset()
    {
        inputBuffer.Clear();
    }

    private struct BufferedInput
    {
        public readonly AttackType AttackType;
        public readonly float Timestamp;
        public readonly GameObject Player;
        public bool Released;

        public BufferedInput(AttackType type, float time, GameObject player)
        {
            AttackType = type;
            Timestamp = time;
            Player = player;
            Released = false;
        }
    }
}
