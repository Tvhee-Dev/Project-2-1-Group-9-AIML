using System;
using UnityEngine;

public class LapTracker : MonoBehaviour
{
    public int totalCheckpoints;

    public int LapsCompleted { get; private set; }
    public event Action<float> LapCompleted;

    public int NextCheckpoint { get; private set; }
    public event Action<int> CheckpointPassed;

    public event Action StartFinishCrossed;

    public float LastLapTime { get; private set; }
    public float FastestLapTime { get; private set; }

    public bool LapStarted { get; private set; }

    // During training, the car must first reach the start/finish line
    public bool WaitingForTrainingStart { get; private set; }

    private float lapStartTime;

    // Prevent duplicate finish-line hits from multiple colliders
    private float lastFinishCrossingTime = -Mathf.Infinity;
    private const float finishCooldown = 0.5f;

    private bool waitingForEpisodeReset = false;


    public void SetTotalCheckpoints(int amount)
    {
        totalCheckpoints = amount;
        NextCheckpoint = 0;
    }


    public void PassCheckpoint(int checkpointIndex)
    {
        if (waitingForEpisodeReset)
            return;

        // During training, checkpoints before the start line do not count
        if (WaitingForTrainingStart)
        {
            Debug.Log(
                $"Checkpoint {checkpointIndex} ignored: " +
                $"start/finish has not been crossed yet."
            );

            return;
        }

        if (!LapStarted)
        {
            Debug.Log(
                $"Checkpoint {checkpointIndex} ignored: lap has not started."
            );

            return;
        }

        if (checkpointIndex != NextCheckpoint)
        {
            Debug.Log(
                $"Checkpoint {checkpointIndex} ignored. " +
                $"Expected checkpoint {NextCheckpoint}."
            );

            return;
        }

        Debug.Log(
            $"Checkpoint {checkpointIndex} passed"
        );

        NextCheckpoint++;

        CheckpointPassed?.Invoke(checkpointIndex);

        if (NextCheckpoint >= totalCheckpoints)
        {
            Debug.Log(
                "All checkpoints passed. Finish line is now valid."
            );
        }
    }


    public void CrossStartFinish()
    {
        if (waitingForEpisodeReset)
            return;

        // Prevent duplicate trigger hits.
        if (Time.time - lastFinishCrossingTime < finishCooldown)
            return;

        lastFinishCrossingTime = Time.time;


        // First crossing of a training episode - starts the times lap
        if (WaitingForTrainingStart)
        {
            WaitingForTrainingStart = false;

            BeginNewLap();

            // Notify CarAgent after we know it is a ligit training-start crossing

            StartFinishCrossed?.Invoke();

            Debug.Log(
                "Training start line crossed. Timed lap begins."
            );

            return;
        }

        // No state where WaitingForTrainingStart is false but no lap is running
        if (!LapStarted)
        {
            return;
        }


        // Crossing the finish early does not count
        if (NextCheckpoint < totalCheckpoints)
        {
            Debug.Log(
                $"Finish ignored. Passed " +
                $"{NextCheckpoint}/{totalCheckpoints} checkpoints."
            );

            return;
        }


        // If all checkpoints passed, the finish is valid
        CompleteLap();
    }


    private void BeginNewLap()
    {
        LapStarted = true;

        NextCheckpoint = 0;

        lapStartTime = Time.time;

        Debug.Log(
            $"Lap {LapsCompleted + 1} started"
        );
    }


    private void CompleteLap()
    {
        float lapTime =
            Time.time - lapStartTime;

        LastLapTime = lapTime;

        LapsCompleted++;

        LapStarted = false;

        // Prevent any more checkpoint / finish events until ML-Agents starts the next episode
        waitingForEpisodeReset = true;

        Debug.Log(
            $"Lap {LapsCompleted} completed in {lapTime:F3}s"
        );


        if (
            FastestLapTime <= 0f ||
            lapTime < FastestLapTime
        )
        {
            FastestLapTime = lapTime;

            Debug.Log(
                $"NEW FASTEST LAP: {FastestLapTime:F3}s"
            );
        }


        // CarAgent receives, gives lap reward and calls EndEpisode()
        LapCompleted?.Invoke(lapTime);
    }


    public float GetCurrentLapTime()
    {
        if (!LapStarted)
            return 0f;

        return Time.time - lapStartTime;
    }


    public void ResetForTrainingEpisode()
    {
        LapsCompleted = 0;
        LastLapTime = 0f;

        NextCheckpoint = 0;

        LapStarted = false;
        WaitingForTrainingStart = true;

        waitingForEpisodeReset = false;

    }
}
