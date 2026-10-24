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

    public bool WaitingForTrainingStart { get; private set; }

    public bool verboseLogging;

    private float lapStartTime;

    private float lastFinishCrossingTime = -Mathf.Infinity;
    private const float finishCooldown = 0.5f;

    private bool waitingForEpisodeReset = false;


    public void SetTotalCheckpoints(int amount)
    {
        totalCheckpoints = Mathf.Max(0, amount);
        NextCheckpoint = 0;
    }


    public void PassCheckpoint(int checkpointIndex)
    {
        if (waitingForEpisodeReset)
            return;

        if (WaitingForTrainingStart)
        {
            if (verboseLogging)
            {
                Debug.Log(
                    $"Checkpoint {checkpointIndex} ignored: " +
                    "Start/Finish has not been crossed yet."
                );
            }

            return;
        }

        if (!LapStarted)
        {
            if (verboseLogging)
            {
                Debug.Log(
                    $"Checkpoint {checkpointIndex} ignored: " +
                    "lap has not started."
                );
            }

            return;
        }

        if (checkpointIndex != NextCheckpoint)
        {
            if (verboseLogging)
            {
                Debug.Log(
                    $"Checkpoint {checkpointIndex} ignored. " +
                    $"Expected checkpoint {NextCheckpoint}."
                );
            }

            return;
        }

        if (totalCheckpoints <= 0)
        {
            if (verboseLogging)
            {
                Debug.Log(
                    "Checkpoint ignored: totalCheckpoints has not been initialized."
                );
            }

            return;
        }

        if (verboseLogging)
        {
            Debug.Log(
                $"Checkpoint {checkpointIndex} passed."
            );
        }

        NextCheckpoint++;

        CheckpointPassed?.Invoke(checkpointIndex);

        if (
            NextCheckpoint >= totalCheckpoints &&
            verboseLogging
        )
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

        if (
            Time.time - lastFinishCrossingTime <
            finishCooldown
        )
        {
            return;
        }

        lastFinishCrossingTime = Time.time;

        if (WaitingForTrainingStart)
        {
            WaitingForTrainingStart = false;

            BeginNewLap();

            StartFinishCrossed?.Invoke();

            if (verboseLogging)
            {
                Debug.Log(
                    "Training Start/Finish crossed. Timed lap begins."
                );
            }

            return;
        }


        if (!LapStarted)
            return;

        if (totalCheckpoints <= 0)
        {
            if (verboseLogging)
            {
                Debug.Log(
                    "Finish ignored: totalCheckpoints has not been initialized."
                );
            }

            return;
        }

        if (NextCheckpoint < totalCheckpoints)
        {
            if (verboseLogging)
            {
                Debug.Log(
                    $"Finish ignored. Passed " +
                    $"{NextCheckpoint}/{totalCheckpoints} checkpoints."
                );
            }

            return;
        }

        CompleteLap();
    }


    private void BeginNewLap()
    {
        NextCheckpoint = 0;

        lapStartTime = Time.time;

        LapStarted = true;
    }


    private void CompleteLap()
    {
        if (!LapStarted)
            return;

        if (totalCheckpoints <= 0)
            return;

        if (NextCheckpoint < totalCheckpoints)
            return;


        float lapTime =
            Time.time - lapStartTime;

        LastLapTime = lapTime;

        LapsCompleted++;

        LapStarted = false;

        waitingForEpisodeReset = true;


        if (
            FastestLapTime <= 0f ||
            lapTime < FastestLapTime
        )
        {
            FastestLapTime = lapTime;

            if (verboseLogging)
            {
                Debug.Log(
                    $"NEW FASTEST LAP: " +
                    $"{FastestLapTime:F3}s"
                );
            }
        }

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
