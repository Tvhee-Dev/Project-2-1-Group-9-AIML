using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using System.IO;
using System.Globalization;

public class CarAgent : Agent
{
    [Header("References")]
    public CarController carController;
    public LapTracker lapTracker;
    public TrackManager trackManager;
    public Rigidbody rb;

    [Header("Wall Sensors")]

    public const int MaxWallRayCount = 21;

    public int wallRayCount = 9;

    public float wallRayFov = 180f;

    public float wallRaySideDistance = 12f;
    public float wallRayCenterDistance = 30f;

    public float wallRayCenterPower = 1.5f;

    [Range(1f, 3f)]
    public float wallRayAnglePower = 1.5f;
    public float wallRayForwardOffset = 1.2f;

    public float wallRayHeight = 0.3f;
    public LayerMask wallLayerMask;

    [Header("Wall Handling")]
    public float wallPenaltyPerSecond = 0.05f;
    public float maxWallContactSeconds = 0.75f;
    public float prolongedWallPenalty = -1f;
    public float wallImpactPenalty = 0.10f;

    private float wallContactTime;

    [Header("Progress Reward")]
    [Tooltip("Total checkpoint reward available over one completed lap.")]
    public float checkpointRewardPerLap = 1f;
    public float targetProgressRewardScale = 0.01f;
    public float forwardProgressRewardPerSecond = 0.02f;

    [Header("Pace Reward")]
    public float paceRewardPerLap = 4f;
    public float paceToleranceSeconds = 10f;

    [Header("Driving Quality")]
    public float timePenaltyPerSecond = 0.05f;
    public float excessiveYawPenaltyPerSecond = 0.01f;
    public float slipPenaltyPerSecond = 0.005f;
    public float allowedYawRate = 2.2f;
    public float allowedSlipAngle = 30f;
    public float maximumSlipAngle = 75f;

    [Header("Lap Time Reward")]
    public float targetLapTime = 46f;
    public float lapTimeRewardAtTarget = 4f;
    public float lapTimeRewardScale = 3f;

    public float maximumLapTimeBonus = 4f;
    public float lapTimeBonusExponent = 1.5f;

    [Header("Episode Limits")]
    public float maxSecondsToReachStart = 60f;
    public float maxSecondsWithoutCheckpoint = 45f;
    public float maxSecondsToFinish = 45f;

    private float timeSinceCheckpoint;

    private float previousDistanceToTarget;
    private Transform previousTarget;

    [Header("Episode Reset")]
    public Transform spawnPoint;

    [Header("Observation Scaling")]
    public float distanceNormalization = 50f;
    public float angularVelocityNormalization = 5f;

    [Header("Action Debug")]
    public float lastThrottle;
    public float lastBrake;
    public float lastSteering;

    [Header("Split Logging")]
    public bool writeCheckpointSplits = true;
    public string driverLabel = "AI";

    private string splitCsvPath;
    private float lastSplitTime;
    private int episodeNumber;

    protected override void Awake()
    {
        base.Awake();
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (carController == null)
            carController = GetComponent<CarController>();

        if (lapTracker == null)
            lapTracker = GetComponent<LapTracker>();
    }

    public override void Initialize()
    {
        lapTracker.CheckpointPassed += OnCheckpointPassed;
        lapTracker.LapCompleted += OnLapCompleted;
        lapTracker.StartFinishCrossed += OnStartFinishCrossed;

        splitCsvPath = Path.Combine(Application.persistentDataPath, "checkpoint_splits.csv");

        if (writeCheckpointSplits && !File.Exists(splitCsvPath))
        {
            File.WriteAllText(
                splitCsvPath,
                "driver,episode,checkpoint,split_seconds,lap_seconds,speed\n"
            );
        }

        Debug.Log($"Split CSV: {splitCsvPath}");
    }

    private void FixedUpdate()
    {
        if (!lapTracker.LapStarted)
        {
            return;
        }

        float dt = Time.fixedDeltaTime;

        timeSinceCheckpoint += dt;

        // Determine how much time the agent gets
        float allowedTime;

        if (
            lapTracker.NextCheckpoint >=
            lapTracker.totalCheckpoints
        )
        {
            allowedTime = maxSecondsToFinish;
        }
        else
        {
            allowedTime = maxSecondsWithoutCheckpoint;
        }

        // End the episode if progress takes too long
        if (timeSinceCheckpoint >= allowedTime)
        {
            AddReward(-1f);
            EndEpisode();
            return;
        }

        float maxSpeed =
            Mathf.Max(carController.maxSpeed, 0.1f);

        Vector3 localVelocity =
            transform.InverseTransformDirection(
                rb.velocity
            );

        float speedAmount =
            Mathf.Clamp01(
                rb.velocity.magnitude / maxSpeed
            );

        // Small incentive to finish sooner
        AddReward(
            -timePenaltyPerSecond * dt
        );

        // Penalize excessive rotation while moving
        float excessiveYaw =
            Mathf.InverseLerp(
                allowedYawRate,
                angularVelocityNormalization,
                Mathf.Abs(rb.angularVelocity.y)
            );

        AddReward(
            -excessiveYawPenaltyPerSecond *
            excessiveYaw *
            speedAmount *
            dt
        );

        // Penalize severe sideways sliding
        float slipAngle =
            Mathf.Abs(
                Mathf.Atan2(
                    localVelocity.x,
                    Mathf.Abs(localVelocity.z) + 0.1f
                ) * Mathf.Rad2Deg
            );

        float excessiveSlip =
            Mathf.InverseLerp(
                allowedSlipAngle,
                maximumSlipAngle,
                slipAngle
            );

        AddReward(
            -slipPenaltyPerSecond *
            excessiveSlip *
            speedAmount *
            dt
        );

        Transform target = GetCurrentTarget();

        if (target == null)
            return;

        float currentDistance = Vector3.Distance(transform.position, target.position);

        if (target != previousTarget)
        {
            previousTarget = target;
            previousDistanceToTarget = currentDistance;
            return;
        }

        float distanceProgress =
            previousDistanceToTarget - currentDistance;

        previousDistanceToTarget = currentDistance;

        AddReward(
            distanceProgress * targetProgressRewardScale
        );

        Vector3 directionToTarget =
            (
                target.position -
                transform.position
            ).normalized;

        float forwardProgress =
            Vector3.Dot(
                rb.velocity,
                directionToTarget
            );


        float normalizedProgress =
            forwardProgress / maxSpeed;

        AddReward(normalizedProgress * forwardProgressRewardPerSecond * dt);
    }

    private void OnDestroy()
    {
        if (lapTracker != null)
        {
            lapTracker.CheckpointPassed -= OnCheckpointPassed;
            lapTracker.LapCompleted -= OnLapCompleted;
            lapTracker.StartFinishCrossed -= OnStartFinishCrossed;
        }
    }

    public override void OnEpisodeBegin()
    {
        if (spawnPoint != null)
        {
            rb.position = spawnPoint.position;
            rb.rotation = spawnPoint.rotation;
        }

        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        carController.SetInputs(0f, 0f, 0f);

        lastThrottle = 0f;
        lastBrake = 0f;
        lastSteering = 0f;

        Physics.SyncTransforms();

        lapTracker.ResetForTrainingEpisode();

        previousTarget = GetCurrentTarget();

        previousDistanceToTarget = previousTarget != null
            ? Vector3.Distance(transform.position, previousTarget.position)
            : 0f;

        timeSinceCheckpoint = 0f;
        wallContactTime = 0f;
        episodeNumber++;
    }


    private void WriteSplit(string checkpoint, float splitTime)
    {
        if (!writeCheckpointSplits)
            return;

        string line = string.Format(
            CultureInfo.InvariantCulture,
            "{0},{1},{2},{3:F3},{4:F3},{5:F3}\n",
            driverLabel,
            episodeNumber,
            checkpoint,
            splitTime,
            lapTracker.GetCurrentLapTime(),
            rb.velocity.magnitude
        );

        File.AppendAllText(splitCsvPath, line);
    }

    private void AddWallRayObservations(VectorSensor sensor)
    {
        Vector3 rayOrigin =
            transform.position +
            transform.forward * wallRayForwardOffset +
            Vector3.up * wallRayHeight;

        int activeRayCount =
            Mathf.Clamp(
                wallRayCount,
                1,
                MaxWallRayCount
            );

        for (int i = 0; i < activeRayCount; i++)
        {
            float fraction = activeRayCount == 1 ? 0.5f : (float)i / (activeRayCount - 1);

            float signedFraction = fraction * 2f - 1f;

            float shapedFraction =
                Mathf.Sign(signedFraction) *
                Mathf.Pow(
                    Mathf.Abs(signedFraction),
                    wallRayAnglePower
                );

            float angle = shapedFraction * wallRayFov * 0.5f;

            float centerAmount = 1f - Mathf.Abs(angle / (wallRayFov * 0.5f));

            centerAmount = Mathf.Clamp01(centerAmount);

            float shapedCenterAmount = Mathf.Pow(centerAmount, wallRayCenterPower);

            float rayDistance = Mathf.Lerp(wallRaySideDistance, wallRayCenterDistance, shapedCenterAmount);

            Vector3 worldDirection =
                Quaternion.AngleAxis(
                    angle,
                    transform.up
                ) *
                transform.forward;

            float normalizedDistance = 1f;

            if (Physics.Raycast(rayOrigin, worldDirection, out RaycastHit hit, rayDistance, wallLayerMask))
            {
                normalizedDistance = hit.distance / rayDistance;

                Debug.DrawRay(
                    rayOrigin,
                    worldDirection * hit.distance,
                    Color.red
                );
            }
            else
            {
                Debug.DrawRay(
                    rayOrigin,
                    worldDirection * rayDistance,
                    Color.green
                );
            }

            sensor.AddObservation(normalizedDistance);
        }

        // Fill unused observation slots
        for (
            int i = activeRayCount;
            i < MaxWallRayCount;
            i++
        )
        {
            sensor.AddObservation(1f);
        }
    }

    private void AddPointObservation(VectorSensor sensor, Transform point)
    {
        if (point == null)
        {
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            return;
        }

        Vector3 localPoint = transform.InverseTransformPoint(point.position);

        sensor.AddObservation(
            Mathf.Clamp(localPoint.x / distanceNormalization, -1f, 1f)
        );

        sensor.AddObservation(
            Mathf.Clamp(localPoint.z / distanceNormalization, -1f, 1f)
        );

        sensor.AddObservation(
            Mathf.Clamp01(
                new Vector2(localPoint.x, localPoint.z).magnitude /
                distanceNormalization
            )
        );
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // Car Movement

        Vector3 localVelocity =
            transform.InverseTransformDirection(rb.velocity);

        float maxSpeed =
            Mathf.Max(carController.maxSpeed, 0.1f);

        sensor.AddObservation(
            Mathf.Clamp(localVelocity.x / maxSpeed, -1f, 1f)
        );

        sensor.AddObservation(
            Mathf.Clamp(localVelocity.z / maxSpeed, -1f, 1f)
        );

        sensor.AddObservation(
            Mathf.Clamp(
                rb.angularVelocity.y /
                angularVelocityNormalization,
                -1f,
                1f
            )
        );

        // Next Checkpoint

        Transform target = GetCurrentTarget();

        Transform lookAheadTarget = GetLookAheadTarget();

        if (target != null && lookAheadTarget != null)
        {
            Vector3 futureDirection =
                lookAheadTarget.position -
                target.position;

            float futureDistance =
                futureDirection.magnitude;

            Vector3 localFutureDirection =
                transform.InverseTransformDirection(
                    futureDirection.normalized
                );

            sensor.AddObservation(
                localFutureDirection.x
            );

            sensor.AddObservation(
                localFutureDirection.z
            );

            sensor.AddObservation(
                Mathf.Clamp01(
                    futureDistance /
                    distanceNormalization
                )
            );
        }
        else
        {
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
        }

        AddPointObservation(sensor, target);

        // Track Progress

        float progress = 0f;

        if (lapTracker.totalCheckpoints > 0)
        {
            progress =
                (float)lapTracker.NextCheckpoint /
                lapTracker.totalCheckpoints;
        }

        sensor.AddObservation(progress);

        // Distance to walls
        AddWallRayObservations(sensor);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        ActionSegment<int> discrete =
            actions.DiscreteActions;

        float steering;
        switch (discrete[0])
        {
            case 0:
                steering = -1f;
                break;

            case 2:
                steering = 1f;
                break;

            default:
                steering = 0f;
                break;
        }

        float throttle =
            discrete[1] == 1
                ? 1f
                : 0f;

        float brake =
            discrete[2] == 1
                ? 1f
                : 0f;


        carController.SetInputs(
            throttle,
            steering,
            brake
        );

        lastThrottle = throttle;
        lastBrake = brake;
        lastSteering = steering;
    }

    private void OnCheckpointPassed(int checkpointIndex)
    {
        float now = Time.time;

        WriteSplit(
            checkpointIndex.ToString(),
            now - lastSplitTime
        );

        lastSplitTime = now;
        timeSinceCheckpoint = 0f;

        int checkpointCount = Mathf.Max(1, lapTracker.totalCheckpoints);

        AddReward(
            checkpointRewardPerLap /
            checkpointCount
        );

        float progressFraction =
            (checkpointIndex + 1f) /
            (lapTracker.totalCheckpoints + 1f);

        float targetTimeAtCheckpoint = targetLapTime * progressFraction;

        float actualTime = lapTracker.GetCurrentLapTime();

        float paceDifference = targetTimeAtCheckpoint - actualTime;

        float paceScore = Mathf.Clamp01(1f + paceDifference / paceToleranceSeconds);

        AddReward(
            paceScore *
            paceRewardPerLap /
            checkpointCount
        );
    }

    private float CalculateLapTimeBonus(float lapTime)
    {
        float exponent = Mathf.Min(
            (targetLapTime - lapTime) / lapTimeRewardScale,
            3f
        );

        return lapTimeRewardAtTarget * Mathf.Exp(exponent);
    }

    private void OnLapCompleted(float lapTime)
    {
        float lapTimeBonus =
            CalculateLapTimeBonus(lapTime);

        AddReward(1f);

        AddReward(lapTimeBonus);

        Debug.Log(
            $"Lap completed in {lapTime:F2}s | " +
            $"Time bonus: {lapTimeBonus:F3} | " +
            $"Episode reward: {GetCumulativeReward():F3}"
        );

        WriteSplit(
    "Finish",
    Time.time - lastSplitTime
);

        EndEpisode();
    }

    private void OnStartFinishCrossed()
    {
        lastSplitTime = Time.time;
        timeSinceCheckpoint = 0f;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!collision.gameObject.CompareTag("wall"))
            return;

        float maxSpeed =
            Mathf.Max(carController.maxSpeed, 0.1f);

        float impactAmount =
            Mathf.Clamp01(
                collision.relativeVelocity.magnitude /
                maxSpeed
            );

        AddReward(
            -wallImpactPenalty * impactAmount
        );

        wallContactTime = 0f;
    }

    private void OnCollisionStay(Collision collision)
    {
        if (!collision.gameObject.CompareTag("wall"))
            return;

        wallContactTime += Time.fixedDeltaTime;

        // Continuous penalty while scraping/pushing against a wall
        AddReward(
            -wallPenaltyPerSecond * Time.fixedDeltaTime
        );

        // The car is clearly stuck
        if (wallContactTime >= maxWallContactSeconds)
        {
            AddReward(prolongedWallPenalty);

            EndEpisode();
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.CompareTag("wall"))
        {
            wallContactTime = 0f;
        }
    }

    private Transform GetCurrentTarget()
    {
        if (lapTracker.WaitingForTrainingStart)
        {
            return trackManager.startFinishLine;
        }

        return trackManager.GetNextTarget(
            lapTracker.NextCheckpoint
        );
    }

    private Transform GetLookAheadTarget()
    {
        if (lapTracker.WaitingForTrainingStart)
        {
            return trackManager.GetNextTarget(0);
        }

        int lookAheadIndex =
            lapTracker.NextCheckpoint + 1;

        if (
            lookAheadIndex >=
            lapTracker.totalCheckpoints
        )
        {
            return trackManager.startFinishLine;
        }

        return trackManager.GetNextTarget(
            lookAheadIndex
        );
    }

    public override void Heuristic(
    in ActionBuffers actionsOut
)
    {
        ActionSegment<int> actions =
            actionsOut.DiscreteActions;

        bool left =
            Input.GetKey(KeyCode.A) ||
            Input.GetKey(KeyCode.LeftArrow);

        bool right =
            Input.GetKey(KeyCode.D) ||
            Input.GetKey(KeyCode.RightArrow);


        if (left && !right)
        {
            actions[0] = 0; // left
        }
        else if (right && !left)
        {
            actions[0] = 2; // right
        }
        else
        {
            actions[0] = 1; // straight
        }

        bool throttle =
            Input.GetKey(KeyCode.W) ||
            Input.GetKey(KeyCode.UpArrow);

        actions[1] =
            throttle ? 1 : 0;

        bool brake =
            Input.GetKey(KeyCode.S) ||
            Input.GetKey(KeyCode.DownArrow);

        actions[2] =
            brake ? 1 : 0;
    }
}
