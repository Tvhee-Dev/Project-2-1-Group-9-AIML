using UnityEngine;

public class LapTracker : MonoBehaviour
{
    [Header("Race State")]
    public int totalCheckpoints;

    public int LapsCompleted { get; private set; }
    public float FastestLapTime { get; private set; }
    public float LastLapTime { get; private set; }

    public bool LapStarted { get; private set; }
    public int NextCheckpoint { get; private set; }

    private float lapStartTime;

    // Prevents (potential) multiple car colliders from registering the start/finish line several times at once
    private float lastFinishCrossingTime = -Mathf.Infinity;
    private const float finishCooldown = 0.5f;

    public void SetTotalCheckpoints(int amount)
    {
        totalCheckpoints = amount;
        ResetCheckpointProgress();
    }

    public void PassCheckpoint(int checkpointIndex)
    {
        // Lap must first be started by crossing start/finish.
        if (!LapStarted)
            return;

        // Only accept checkpoints in the correct order.
        if (checkpointIndex != NextCheckpoint)
            return;

        Debug.Log($"Checkpoint {checkpointIndex} passed");

        NextCheckpoint++;

        if (NextCheckpoint >= totalCheckpoints)
        {
            Debug.Log("All checkpoints passed - finish line is now valid");
        }
    }

    public void CrossStartFinish()
    {
        // Protect against multiple colliders triggering at once.
        if (Time.time - lastFinishCrossingTime < finishCooldown)
            return;

        lastFinishCrossingTime = Time.time;

        // First crossing starts the first lap.
        if (!LapStarted)
        {
            StartLap();
            return;
        }

        // Don't allow a lap to finish if checkpoints were skipped.
        if (NextCheckpoint < totalCheckpoints)
        {
            Debug.Log(
                $"Finish crossed, but only " +
                $"{NextCheckpoint}/{totalCheckpoints} checkpoints were passed."
            );

            return;
        }

        FinishLap();
    }

    private void StartLap()
    {
        LapStarted = true;
        lapStartTime = Time.time;

        ResetCheckpointProgress();

        Debug.Log("Lap started");
    }

    private void FinishLap()
    {
        float lapTime = Time.time - lapStartTime;

        LastLapTime = lapTime;
        LapsCompleted++;

        if (FastestLapTime <= 0f || lapTime < FastestLapTime)
        {
            FastestLapTime = lapTime;

            Debug.Log(
                $"NEW FASTEST LAP: {FastestLapTime:F3}s"
            );
        }

        Debug.Log(
            $"Lap {LapsCompleted} completed in {lapTime:F3}s"
        );

        // Crossing the finish also starts the next lap.
        lapStartTime = Time.time;

        ResetCheckpointProgress();
    }

    private void ResetCheckpointProgress()
    {
        NextCheckpoint = 0;
    }

    public float GetCurrentLapTime()
    {
        if (!LapStarted)
            return 0f;

        return Time.time - lapStartTime;
    }
}
