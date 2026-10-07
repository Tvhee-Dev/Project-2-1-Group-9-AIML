using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

public class TrackManager : MonoBehaviour
{
    [Header("Track")]
    public SplineContainer trackSpline;

    [Header("Start / Finish")]
    public Transform startFinishLine;

    [Header("Spawn")]
    public Transform spawnPoint;

    [Tooltip("Distance behind the Start/Finish line.")]
    public float spawnDistanceBehindStart = 100f;

    [Tooltip("Vertical offset for the spawn position.")]
    public float spawnHeightOffset = 0.5f;

    [Header("Checkpoint generation")]
    public GameObject checkpointPrefab;
    public Transform checkpointParent;

    [Tooltip("Vertical offset from the spline")]
    public float checkpointHeightOffset = 0f;

    [Header("Track direction")]
    [Tooltip("Enable if cars drive opposite to the spline direction")]
    public bool reverseDirection = false;

    private readonly List<GameObject> generatedCheckpoints =
        new List<GameObject>();


    public void GenerateCheckpoints(int checkpointCount)
    {
        ClearCheckpoints();

        if (trackSpline == null)
        {
            Debug.LogError(
                "TrackManager: No track spline assigned."
            );
            return;
        }

        if (startFinishLine == null)
        {
            Debug.LogError(
                "TrackManager: No Start/Finish line assigned."
            );
            return;
        }

        if (checkpointPrefab == null)
        {
            Debug.LogError(
                "TrackManager: No checkpoint prefab assigned."
            );
            return;
        }

        if (checkpointCount <= 0)
        {
            Debug.LogError(
                "TrackManager: Checkpoint count must be greater than 0."
            );
            return;
        }

        Spline spline = trackSpline.Spline;

        float totalLength = spline.GetLength();

        if (totalLength <= 0f)
        {
            Debug.LogError(
                "TrackManager: Spline has no length."
            );
            return;
        }


        // Find where the manually positioned Start/Finish line
        // lies on the spline.
        Vector3 localStartPosition =
            trackSpline.transform.InverseTransformPoint(
                startFinishLine.position
            );

        SplineUtility.GetNearestPoint(
            spline,
            (float3)localStartPosition,
            out float3 nearestPoint,
            out float startT
        );


        float startDistance =
            spline.ConvertIndexUnit(
                startT,
                PathIndexUnit.Normalized,
                PathIndexUnit.Distance
            );


        // Divide the full lap into equal sections
        // Start -> CP0 -> CP1 -> ... -> CPn-1 -> CPn -> Finish
        // checkpointCount + 1 gaps in total

        float checkpointSpacing =
            totalLength /
            (checkpointCount + 1f);


        for (int i = 0; i < checkpointCount; i++)
        {
            float offset =
                checkpointSpacing * (i + 1);


            float checkpointDistance;

            if (reverseDirection)
            {
                checkpointDistance =
                    startDistance - offset;
            }
            else
            {
                checkpointDistance =
                    startDistance + offset;
            }


            checkpointDistance =
                Mathf.Repeat(
                    checkpointDistance,
                    totalLength
                );


            float t =
                spline.ConvertIndexUnit(
                    checkpointDistance,
                    PathIndexUnit.Distance,
                    PathIndexUnit.Normalized
                );


            Vector3 position =
                trackSpline.EvaluatePosition(t);

            Vector3 direction =
                trackSpline.EvaluateTangent(t);


            if (reverseDirection)
            {
                direction = -direction;
            }


            position +=
                Vector3.up *
                checkpointHeightOffset;


            Quaternion rotation =
                Quaternion.LookRotation(
                    direction.normalized,
                    Vector3.up
                );


            GameObject checkpoint =
                Instantiate(
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


            generatedCheckpoints.Add(
                checkpoint
            );
        }


        PositionSpawnPoint(
            spline,
            totalLength,
            startDistance
        );


        Debug.Log(
            $"Generated {generatedCheckpoints.Count} checkpoints. " +
            $"Spacing = {checkpointSpacing:F2}"
        );
    }


    private void PositionSpawnPoint(
        Spline spline,
        float totalLength,
        float startDistance
    )
    {
        if (spawnPoint == null)
            return;


        // "Behind" Start/Finish means opposite the direction in which the car will drive after crossing the line
        float spawnDistance;

        if (reverseDirection)
        {
            spawnDistance =
                startDistance +
                spawnDistanceBehindStart;
        }
        else
        {
            spawnDistance =
                startDistance -
                spawnDistanceBehindStart;
        }


        spawnDistance =
            Mathf.Repeat(
                spawnDistance,
                totalLength
            );


        float spawnT =
            spline.ConvertIndexUnit(
                spawnDistance,
                PathIndexUnit.Distance,
                PathIndexUnit.Normalized
            );


        Vector3 position =
            trackSpline.EvaluatePosition(
                spawnT
            );

        Vector3 direction =
            trackSpline.EvaluateTangent(
                spawnT
            );


        if (reverseDirection)
        {
            direction = -direction;
        }


        position +=
            Vector3.up *
            spawnHeightOffset;


        spawnPoint.position =
            position;

        spawnPoint.rotation =
            Quaternion.LookRotation(
                direction.normalized,
                Vector3.up
            );
    }


    private void ClearCheckpoints()
    {
        foreach (
            GameObject checkpoint
            in generatedCheckpoints
        )
        {
            if (checkpoint != null)
            {
                Destroy(checkpoint);
            }
        }

        generatedCheckpoints.Clear();
    }


    public Transform GetCheckpointTransform(
        int index
    )
    {
        if (
            index < 0 ||
            index >= generatedCheckpoints.Count
        )
        {
            return null;
        }

        return generatedCheckpoints[
            index
        ].transform;
    }


    public Transform GetNextTarget(
        int checkpointIndex
    )
    {
        if (
            checkpointIndex >=
            generatedCheckpoints.Count
        )
        {
            return startFinishLine;
        }

        return generatedCheckpoints[
            checkpointIndex
        ].transform;
    }
}
