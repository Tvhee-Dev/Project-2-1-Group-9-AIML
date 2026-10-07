using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

public class TrackManager : MonoBehaviour
{
    [Header("Track")]
    public SplineContainer trackSpline;

    [Header("Start / Finish")]
    public Transform startFinishLine;
    public float startFinishHeightOffset = 0f;

    [Header("Checkpoint generation")]
    public GameObject checkpointPrefab;
    public Transform checkpointParent;

    [Tooltip("Vertical offset from the spline")]
    public float checkpointHeightOffset = 0f;

    [Header("Track direction")]
    [Range(0f, 1f)]
    public float startT = 0f;

    [Tooltip("Enable if cars drive opposite to the spline direction")]
    public bool reverseDirection = false;

    private readonly List<GameObject> generatedCheckpoints =
        new List<GameObject>();

    public void GenerateCheckpoints(int checkpointCount)
    {
        ClearCheckpoints();

        if (trackSpline == null)
        {
            Debug.LogError("TrackManager: No track spline assigned.");
            return;
        }

        if (checkpointPrefab == null)
        {
            Debug.LogError("TrackManager: No checkpoint prefab assigned.");
            return;
        }

        PositionStartFinish();

        for (int i = 0; i < checkpointCount; i++)
        {
            // Position around the lap after the start line.
            float progress =
                (i + 1f) / (checkpointCount + 1f);

            float t;

            if (reverseDirection)
            {
                t = startT - progress;
            }
            else
            {
                t = startT + progress;
            }

            // Wrap around the closed spline.
            t = Mathf.Repeat(t, 1f);

            Vector3 position =
                trackSpline.EvaluatePosition(t);

            Vector3 direction =
                trackSpline.EvaluateTangent(t);

            if (reverseDirection)
            {
                direction = -direction;
            }

            position +=
                Vector3.up * checkpointHeightOffset;

            Quaternion rotation =
                Quaternion.LookRotation(
                    direction.normalized,
                    Vector3.up
                );

            GameObject checkpoint = Instantiate(
                checkpointPrefab,
                position,
                rotation,
                checkpointParent
            );

            checkpoint.name =
                $"Checkpoint_{i}";

            TrackCheckpoint checkpointScript =
                checkpoint.GetComponent<TrackCheckpoint>();

            if (checkpointScript != null)
            {
                checkpointScript.checkpointIndex = i;
            }

            generatedCheckpoints.Add(checkpoint);
        }

        Debug.Log(
            $"Generated {generatedCheckpoints.Count} checkpoints."
        );
    }

    private void PositionStartFinish()
    {
        if (startFinishLine == null || trackSpline == null)
            return;

        Vector3 position =
            trackSpline.EvaluatePosition(startT);

        Vector3 direction =
            trackSpline.EvaluateTangent(startT);

        if (reverseDirection)
        {
            direction = -direction;
        }

        position += Vector3.up * startFinishHeightOffset;

        startFinishLine.position = position;

        startFinishLine.rotation =
            Quaternion.LookRotation(
                direction.normalized,
                Vector3.up
            );
    }

    private void ClearCheckpoints()
    {
        foreach (GameObject checkpoint in generatedCheckpoints)
        {
            if (checkpoint != null)
            {
                Destroy(checkpoint);
            }
        }

        generatedCheckpoints.Clear();
    }
}
