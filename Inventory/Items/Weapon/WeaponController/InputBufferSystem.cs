using System.Collections.Generic;
using UnityEngine;

public class InputBufferSystem
{
    private WeaponController controller;
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

    public void BufferInput(AttackType attackType, GameObject player)
    {
        if (!enableInputBuffer) return;

        inputBuffer.Enqueue(new BufferedInput(attackType, Time.time, player));
        CleanOldInputs();
        controller.LogDebug($"Input buffered: {attackType}. Buffer size: {inputBuffer.Count}");
    }

    public void ProcessInputBuffer()
    {
        if (inputBuffer.Count == 0 || attackExecutor.IsAttacking) return;

        while (inputBuffer.Count > 0)
        {
            var input = inputBuffer.Dequeue();
            if (Time.time - input.Timestamp <= inputBufferTime)
            {
                controller.LogDebug($"Processing buffered input: {input.AttackType}");
                attackExecutor.ExecuteAttack(input.Player, input.AttackType);
                break;
            }
        }
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

    // Nested Classes
    private readonly struct BufferedInput
    {
        public readonly AttackType AttackType;
        public readonly float Timestamp;
        public readonly GameObject Player;

        public BufferedInput(AttackType type, float time, GameObject player)
        {
            AttackType = type;
            Timestamp = time;
            Player = player;
        }
    }
}