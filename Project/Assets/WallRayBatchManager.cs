using UnityEngine;
using Unity.MLAgents;
using Unity.Collections;
using Unity.Jobs;

/// <summary>
/// Batches all wall-ray physics queries for every CarAgent below this object
/// into one RaycastCommand.ScheduleBatch() call per decision step.
///
/// Put exactly one WallRayBatchManager on the common TrainingArea parent.
/// The CarAgents themselves do no raycast physics in CollectObservations().
/// </summary>
[DefaultExecutionOrder(-20)]
public class WallRayBatchManager : MonoBehaviour
{
    [Header("Agents")]
    [Tooltip("Normally leave empty. The manager automatically discovers every child CarAgent.")]
    public CarAgent[] carAgents;

    [Header("Batch Settings")]
    [Min(1)]
    [Tooltip(
        "Minimum commands placed into each Unity job. " +
        "For roughly 75-315 rays, 32 is a good starting point. " +
        "Benchmark 16, 32 and 64 on your server."
    )]
    public int minCommandsPerJob = 32;

    [Header("Diagnostics")]
    public bool logSetup = true;

    private NativeArray<RaycastCommand> commands;
    private NativeArray<RaycastHit> results;

    // Maps every command/result back to its owning car and ray index.
    // Managed arrays are allocated once and then reused.
    private CarAgent[] commandOwners;
    private int[] commandRayIndices;

    private int capacity;
    private bool initialized;

    private void Awake()
    {
        RefreshAgents();
    }

    private void OnEnable()
    {
        Academy.Instance.AgentPreStep += OnAgentPreStep;
    }

    private void Start()
    {
        // Refresh after all scene Awake/OnEnable work in case hierarchy changed.
        RefreshAgents();
    }

    private void OnDisable()
    {
        if (Academy.IsInitialized)
        {
            Academy.Instance.AgentPreStep -= OnAgentPreStep;
        }
    }

    private void OnDestroy()
    {
        DisposeBuffers();
    }

    /// <summary>
    /// Re-discovers all CarAgents below this TrainingArea and resizes
    /// the persistent batch buffers if necessary.
    /// </summary>
    public void RefreshAgents()
    {
        carAgents =
            GetComponentsInChildren<CarAgent>(true);

        int requiredCapacity =
            Mathf.Max(
                1,
                carAgents.Length *
                CarAgent.MaxWallRayCount
            );

        if (
            initialized &&
            requiredCapacity == capacity
        )
        {
            return;
        }

        DisposeBuffers();

        capacity = requiredCapacity;

        commands =
            new NativeArray<RaycastCommand>(
                capacity,
                Allocator.Persistent
            );

        results =
            new NativeArray<RaycastHit>(
                capacity,
                Allocator.Persistent
            );

        commandOwners =
            new CarAgent[capacity];

        commandRayIndices =
            new int[capacity];

        initialized = true;

        if (logSetup)
        {
            Debug.Log(
                $"WallRayBatchManager: " +
                $"Cars={carAgents.Length}, " +
                $"Capacity={capacity} rays, " +
                $"MinCommandsPerJob={minCommandsPerJob}"
            );
        }
    }

    private void DisposeBuffers()
    {
        if (commands.IsCreated)
            commands.Dispose();

        if (results.IsCreated)
            results.Dispose();

        initialized = false;
        capacity = 0;
    }

    /// <summary>
    /// Called by ML-Agents before AgentSendState / CollectObservations.
    /// Only agents that request a decision on this Academy step are included.
    /// </summary>
    private void OnAgentPreStep(int academyStepCount)
    {
        if (!initialized)
            RefreshAgents();

        int commandCount = 0;

        for (int carIndex = 0; carIndex < carAgents.Length; carIndex++)
        {
            CarAgent car = carAgents[carIndex];

            if (car == null)
                continue;

            if (!car.NeedsWallRayUpdate(academyStepCount))
                continue;

            int rayCount = car.ActiveWallRayCount;

            for (int rayIndex = 0; rayIndex < rayCount; rayIndex++)
            {
                // Defensive resize in case cars were added dynamically.
                if (commandCount >= capacity)
                {
                    Debug.LogError(
                        "WallRayBatchManager capacity exceeded. " +
                        "Call RefreshAgents() after adding cars at runtime."
                    );
                    return;
                }

                commands[commandCount] =
                    car.CreateWallRayCommand(rayIndex);

                commandOwners[commandCount] =
                    car;

                commandRayIndices[commandCount] =
                    rayIndex;

                commandCount++;
            }
        }

        if (commandCount == 0)
            return;

        NativeArray<RaycastCommand> commandSlice =
            commands.GetSubArray(
                0,
                commandCount
            );

        NativeArray<RaycastHit> resultSlice =
            results.GetSubArray(
                0,
                commandCount
            );

        int commandsPerJob =
            Mathf.Clamp(
                minCommandsPerJob,
                1,
                commandCount
            );

        JobHandle handle =
            RaycastCommand.ScheduleBatch(
                commandSlice,
                resultSlice,
                commandsPerJob,
                1,
                default
            );

        // Must be complete before AgentSendState calls CollectObservations().
        handle.Complete();

        for (int i = 0; i < commandCount; i++)
        {
            CarAgent owner =
                commandOwners[i];

            if (owner == null)
                continue;

            int rayIndex =
                commandRayIndices[i];

            RaycastHit hit =
                results[i];

            float normalizedDistance = 1f;

            if (hit.collider != null)
            {
                float maxDistance =
                    owner.GetWallRayDistance(rayIndex);

                if (maxDistance > 0.0001f)
                {
                    normalizedDistance =
                        hit.distance /
                        maxDistance;
                }
            }

            owner.SetWallRayObservation(
                rayIndex,
                normalizedDistance
            );
        }
    }
}
