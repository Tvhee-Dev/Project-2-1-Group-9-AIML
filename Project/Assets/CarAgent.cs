using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using System.IO;
using System.Globalization;

public class CarAgent : Agent
{
    private static float sessionFastestLap = Mathf.Infinity;
    private static readonly object fastestLapLock = new object();

    private string bestLapRequestPath;

    // Ray geometry is precomputed once whenever RaceConfig changes it.
    private readonly Vector3[] wallRayLocalDirections =
        new Vector3[MaxWallRayCount];

    private readonly float[] wallRayDistances =
        new float[MaxWallRayCount];

    // Filled by WallRayBatchManager before ML-Agents collects observations.
    private readonly float[] wallRayObservations =
        new float[MaxWallRayCount];

    private int activeWallRayCount;
    private QueryParameters wallQueryParameters;
    private DecisionRequester decisionRequester;

    private Transform currentTarget;
    private Transform currentLookAheadTarget;
    private Transform previousTarget;

    [Header("References")]
    public CarController carController;
    public LapTracker lapTracker;
    public TrackManager trackManager;
    public Rigidbody rb;

    [Header("Wall Sensors")]

    public const int MaxWallRayCount = 80;

    public int wallRayCount = 9;
    public float wallRayFov = 180f;
    public float wallRaySideDistance = 12f;
    public float wallRayCenterDistance = 30f;
    public float wallRayCenterPower = 1.5f;

    [Range(0.5f, 3f)]
    public float wallRayAnglePower = 1.5f;

    public float wallRayForwardOffset = 1.2f;
    public float wallRayHeight = 0f;
    public LayerMask wallLayerMask;

    [Header("Wall Handling")]
    public float wallPenaltyPerSecond = 0.05f;
    public float maxWallContactSeconds = 0.75f;
    public float prolongedWallPenalty = -1f;
    public float wallImpactPenalty = 0.10f;

    private float wallContactTime;
    private int wallContactCount;

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
    public float targetLapTime = 52.02f;
    public float lapTimeRewardAtTarget = 6f;
    public float lapTimeRewardScale = 3f;

    [Header("Episode Limits")]
    public float maxSecondsToReachStart = 60f;
    public float maxSecondsWithoutCheckpoint = 45f;
    public float maxSecondsToFinish = 45f;

    private float timeSinceCheckpoint;
    private float previousDistanceToTarget;

    [Header("Episode Reset")]
    public Transform spawnPoint;

    [Header("Observation Scaling")]
    public float distanceNormalization = 50f;
    public float angularVelocityNormalization = 5f;

    [Header("Action Debug")]
    public float lastThrottle;
    public float lastBrake;
    public float lastSteering;

    [Header("Training Logging")]
    [Tooltip("Enable only when individual lap logs are useful. Disable on high-throughput server runs.")]
    public bool logEveryLap = false;

    private int episodeNumber;

    // Used by WallRayBatchManager.
    internal int ActiveWallRayCount => activeWallRayCount;

    protected override void Awake()
    {
        base.Awake();

        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (carController == null)
            carController = GetComponent<CarController>();

        if (lapTracker == null)
            lapTracker = GetComponent<LapTracker>();

        decisionRequester = GetComponent<DecisionRequester>();

        // Safe default until the first batch has been calculated.
        for (int i = 0; i < MaxWallRayCount; i++)
            wallRayObservations[i] = 1f;
    }

    public override void Initialize()
    {
        lapTracker.CheckpointPassed += OnCheckpointPassed;
        lapTracker.LapCompleted += OnLapCompleted;
        lapTracker.StartFinishCrossed += OnStartFinishCrossed;

        bestLapRequestPath =
            System.Environment.GetEnvironmentVariable("BEST_LAP_REQUEST_PATH");

        if (!string.IsNullOrEmpty(bestLapRequestPath))
        {
            string directory = Path.GetDirectoryName(bestLapRequestPath);

            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
        }

        // Fallback for manually configured scenes.
        // RaceConfig should call this again AFTER applying environment parameters.
        RebuildWallRayCache();
    }

    private void FixedUpdate()
    {
        bool waitingForStart = lapTracker.WaitingForTrainingStart;

        if (!waitingForStart && !lapTracker.LapStarted)
            return;

        float dt = Time.fixedDeltaTime;
        timeSinceCheckpoint += dt;

        float allowedTime;

        if (waitingForStart)
        {
            allowedTime = maxSecondsToReachStart;
        }
        else if (
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

        if (timeSinceCheckpoint >= allowedTime)
        {
            AddReward(-1f);
            EndEpisode();
            return;
        }

        Vector3 velocity = rb.velocity;
        Vector3 angularVelocity = rb.angularVelocity;
        Quaternion rotation = rb.rotation;
        Vector3 position = rb.position;

        float maxSpeed = Mathf.Max(carController.maxSpeed, 0.1f);
        float speed = velocity.magnitude;
        float speedAmount = Mathf.Clamp01(speed / maxSpeed);

        Vector3 localVelocity =
            Quaternion.Inverse(rotation) * velocity;

        float reward = 0f;

        if (!waitingForStart)
        {
            reward -= timePenaltyPerSecond * dt;

            if (
                excessiveYawPenaltyPerSecond != 0f &&
                speedAmount > 0f
            )
            {
                float excessiveYaw =
                    Mathf.InverseLerp(
                        allowedYawRate,
                        angularVelocityNormalization,
                        Mathf.Abs(angularVelocity.y)
                    );

                reward -=
                    excessiveYawPenaltyPerSecond *
                    excessiveYaw *
                    speedAmount *
                    dt;
            }

            if (
                slipPenaltyPerSecond != 0f &&
                speedAmount > 0f
            )
            {
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

                reward -=
                    slipPenaltyPerSecond *
                    excessiveSlip *
                    speedAmount *
                    dt;
            }
        }

        if (wallContactCount > 0)
        {
            wallContactTime += dt;
            reward -= wallPenaltyPerSecond * dt;

            if (wallContactTime >= maxWallContactSeconds)
            {
                AddReward(reward + prolongedWallPenalty);
                EndEpisode();
                return;
            }
        }
        else
        {
            wallContactTime = 0f;
        }

        Transform target = currentTarget;

        if (target == null)
        {
            if (reward != 0f)
                AddReward(reward);

            return;
        }

        Vector3 toTarget =
            target.position -
            position;

        float currentDistance = toTarget.magnitude;

        Vector3 directionToTarget =
            currentDistance > 0.0001f
                ? toTarget / currentDistance
                : Vector3.zero;

        if (target != previousTarget)
        {
            previousTarget = target;
            previousDistanceToTarget = currentDistance;

            if (reward != 0f)
                AddReward(reward);

            return;
        }

        float distanceProgress =
            previousDistanceToTarget -
            currentDistance;

        previousDistanceToTarget = currentDistance;

        reward +=
            distanceProgress *
            targetProgressRewardScale;

        float forwardProgress =
            Vector3.Dot(
                velocity,
                directionToTarget
            );

        float normalizedProgress =
            forwardProgress / maxSpeed;

        reward +=
            normalizedProgress *
            forwardProgressRewardPerSecond *
            dt;

        if (reward != 0f)
            AddReward(reward);
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

        lapTracker.ResetForTrainingEpisode();

        RefreshTargetCache();

        previousTarget = currentTarget;

        previousDistanceToTarget =
            currentTarget != null
                ? Vector3.Distance(
                    rb.position,
                    currentTarget.position
                )
                : 0f;

        timeSinceCheckpoint = 0f;
        wallContactTime = 0f;
        wallContactCount = 0;

        episodeNumber++;
    }

    private void RequestBestLapSnapshot(float lapTime)
    {
        lock (fastestLapLock)
        {
            if (lapTime >= sessionFastestLap)
                return;

            sessionFastestLap = lapTime;

            Debug.Log(
                $"NEW SESSION FASTEST LAP: {lapTime:F3}s"
            );

            if (string.IsNullOrEmpty(bestLapRequestPath))
                return;

            string contents =
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{{\"lap_time\":{0:F3},\"episode\":{1}}}",
                    lapTime,
                    episodeNumber
                );

            string tempPath =
                bestLapRequestPath + ".tmp";

            File.WriteAllText(
                tempPath,
                contents
            );

            if (File.Exists(bestLapRequestPath))
                File.Delete(bestLapRequestPath);

            File.Move(
                tempPath,
                bestLapRequestPath
            );
        }
    }

    /// <summary>
    /// Rebuilds all ray geometry that is constant for this configuration.
    /// RaceConfig should call this after assigning YAML/environment parameters.
    /// </summary>
    public void RebuildWallRayCache()
    {
        activeWallRayCount =
            Mathf.Clamp(
                wallRayCount,
                1,
                MaxWallRayCount
            );

        wallQueryParameters =
            new QueryParameters(
                wallLayerMask.value,
                false,
                QueryTriggerInteraction.UseGlobal,
                Physics.queriesHitBackfaces
            );

        for (int i = 0; i < activeWallRayCount; i++)
        {
            float fraction =
                activeWallRayCount == 1
                    ? 0.5f
                    : (float)i /
                      (activeWallRayCount - 1);

            float signedFraction =
                fraction * 2f - 1f;

            float shapedFraction =
                Mathf.Sign(signedFraction) *
                Mathf.Pow(
                    Mathf.Abs(signedFraction),
                    wallRayAnglePower
                );

            float angle =
                shapedFraction *
                wallRayFov *
                0.5f;

            float halfFov = wallRayFov * 0.5f;

            float centerAmount =
                halfFov > 0.0001f
                    ? 1f -
                      Mathf.Abs(angle / halfFov)
                    : 1f;

            centerAmount = Mathf.Clamp01(centerAmount);

            float shapedCenterAmount =
                Mathf.Pow(
                    centerAmount,
                    wallRayCenterPower
                );

            wallRayDistances[i] =
                Mathf.Lerp(
                    wallRaySideDistance,
                    wallRayCenterDistance,
                    shapedCenterAmount
                );

            wallRayLocalDirections[i] =
                Quaternion.AngleAxis(
                    angle,
                    Vector3.up
                ) *
                Vector3.forward;

            // Never expose stale data after a config rebuild.
            wallRayObservations[i] = 1f;
        }

        for (
            int i = activeWallRayCount;
            i < MaxWallRayCount;
            i++
        )
        {
            wallRayObservations[i] = 1f;
        }
    }

    /// <summary>
    /// Returns whether this agent will request a new decision on this Academy step.
    /// This mirrors DecisionRequester.ShouldRequestDecision().
    /// </summary>
    internal bool NeedsWallRayUpdate(int academyStepCount)
    {
        if (
            !isActiveAndEnabled ||
            !gameObject.activeInHierarchy
        )
        {
            return false;
        }

        if (
            decisionRequester == null ||
            !decisionRequester.enabled
        )
        {
            // This project uses DecisionRequester. If it is absent/disabled,
            // do not waste CPU raycasting every Academy step.
            return false;
        }

        int period =
            Mathf.Max(
                1,
                decisionRequester.DecisionPeriod
            );

        int decisionStep =
            Mathf.Clamp(
                decisionRequester.DecisionStep,
                0,
                period - 1
            );

        return
            academyStepCount % period ==
            decisionStep;
    }

    /// <summary>
    /// Creates one RaycastCommand using cached local geometry and current car pose.
    /// Called only by WallRayBatchManager.
    /// </summary>
    internal RaycastCommand CreateWallRayCommand(int rayIndex)
    {
        Quaternion rotation = rb.rotation;

        Vector3 rayOrigin =
            rb.position +
            (rotation * Vector3.forward) *
            wallRayForwardOffset +
            Vector3.up *
            wallRayHeight;

        Vector3 worldDirection =
            rotation *
            wallRayLocalDirections[rayIndex];

        return new RaycastCommand(
            rayOrigin,
            worldDirection,
            wallQueryParameters,
            wallRayDistances[rayIndex]
        );
    }

    internal float GetWallRayDistance(int rayIndex)
    {
        return wallRayDistances[rayIndex];
    }

    internal void SetWallRayObservation(
        int rayIndex,
        float normalizedDistance
    )
    {
        wallRayObservations[rayIndex] =
            Mathf.Clamp01(normalizedDistance);
    }

    private void AddWallRayObservations(VectorSensor sensor)
    {
        int activeRayCount =
            Mathf.Clamp(
                activeWallRayCount,
                1,
                MaxWallRayCount
            );

        // No physics work occurs here anymore.
        // WallRayBatchManager populated these values in AgentPreStep.
        for (int i = 0; i < activeRayCount; i++)
        {
            sensor.AddObservation(
                wallRayObservations[i]
            );
        }

        // Preserve fixed observation count.
        for (
            int i = activeRayCount;
            i < MaxWallRayCount;
            i++
        )
        {
            sensor.AddObservation(1f);
        }
    }

    private void AddPointObservation(
        VectorSensor sensor,
        Transform point,
        Quaternion inverseRotation,
        Vector3 carPosition
    )
    {
        if (point == null)
        {
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            return;
        }

        Vector3 localPoint =
            inverseRotation *
            (
                point.position -
                carPosition
            );

        sensor.AddObservation(
            Mathf.Clamp(
                localPoint.x /
                distanceNormalization,
                -1f,
                1f
            )
        );

        sensor.AddObservation(
            Mathf.Clamp(
                localPoint.z /
                distanceNormalization,
                -1f,
                1f
            )
        );

        float planarDistance =
            Mathf.Sqrt(
                localPoint.x * localPoint.x +
                localPoint.z * localPoint.z
            );

        sensor.AddObservation(
            Mathf.Clamp01(
                planarDistance /
                distanceNormalization
            )
        );
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        Quaternion rotation = rb.rotation;
        Quaternion inverseRotation = Quaternion.Inverse(rotation);

        Vector3 position = rb.position;
        Vector3 velocity = rb.velocity;
        Vector3 angularVelocity = rb.angularVelocity;

        Vector3 localVelocity =
            inverseRotation *
            velocity;

        float maxSpeed =
            Mathf.Max(
                carController.maxSpeed,
                0.1f
            );

        sensor.AddObservation(
            Mathf.Clamp(
                localVelocity.x / maxSpeed,
                -1f,
                1f
            )
        );

        sensor.AddObservation(
            Mathf.Clamp(
                localVelocity.z / maxSpeed,
                -1f,
                1f
            )
        );

        sensor.AddObservation(
            Mathf.Clamp(
                angularVelocity.y /
                angularVelocityNormalization,
                -1f,
                1f
            )
        );

        Transform target = currentTarget;
        Transform lookAheadTarget = currentLookAheadTarget;

        if (
            target != null &&
            lookAheadTarget != null
        )
        {
            Vector3 futureDirection =
                lookAheadTarget.position -
                target.position;

            float futureDistance =
                futureDirection.magnitude;

            Vector3 normalizedFutureDirection =
                futureDistance > 0.0001f
                    ? futureDirection /
                      futureDistance
                    : Vector3.zero;

            Vector3 localFutureDirection =
                inverseRotation *
                normalizedFutureDirection;

            sensor.AddObservation(localFutureDirection.x);
            sensor.AddObservation(localFutureDirection.z);

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

        AddPointObservation(
            sensor,
            target,
            inverseRotation,
            position
        );

        float progress = 0f;

        if (lapTracker.totalCheckpoints > 0)
        {
            progress =
                (float)lapTracker.NextCheckpoint /
                lapTracker.totalCheckpoints;
        }

        sensor.AddObservation(progress);

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
        timeSinceCheckpoint = 0f;

        int checkpointCount =
            Mathf.Max(
                1,
                lapTracker.totalCheckpoints
            );

        AddReward(
            checkpointRewardPerLap /
            checkpointCount
        );

        float progressFraction =
            (checkpointIndex + 1f) /
            (lapTracker.totalCheckpoints + 1f);

        float targetTimeAtCheckpoint =
            targetLapTime *
            progressFraction;

        float actualTime =
            lapTracker.GetCurrentLapTime();

        float paceDifference =
            targetTimeAtCheckpoint -
            actualTime;

        float paceScore =
            Mathf.Clamp01(
                1f +
                paceDifference /
                paceToleranceSeconds
            );

        AddReward(
            paceScore *
            paceRewardPerLap /
            checkpointCount
        );

        RefreshTargetCache();
    }

    private float CalculateLapTimeBonus(float lapTime)
    {
        float exponent =
            Mathf.Min(
                (targetLapTime - lapTime) /
                lapTimeRewardScale,
                3f
            );

        return
            lapTimeRewardAtTarget *
            Mathf.Exp(exponent);
    }

    private void OnLapCompleted(float lapTime)
    {
        float lapTimeBonus =
            CalculateLapTimeBonus(lapTime);

        AddReward(1f);
        AddReward(lapTimeBonus);

        if (logEveryLap)
        {
            Debug.Log(
                $"Lap completed in {lapTime:F2}s | " +
                $"Time bonus: {lapTimeBonus:F3} | " +
                $"Episode reward: {GetCumulativeReward():F3}"
            );
        }

        RequestBestLapSnapshot(lapTime);

        EndEpisode();
    }

    private void OnStartFinishCrossed()
    {
        timeSinceCheckpoint = 0f;
        RefreshTargetCache();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!collision.gameObject.CompareTag("wall"))
            return;

        wallContactCount++;
        wallContactTime = 0f;

        float maxSpeed =
            Mathf.Max(
                carController.maxSpeed,
                0.1f
            );

        float impactAmount =
            Mathf.Clamp01(
                collision.relativeVelocity.magnitude /
                maxSpeed
            );

        AddReward(
            -wallImpactPenalty *
            impactAmount
        );
    }

    private void OnCollisionExit(Collision collision)
    {
        if (!collision.gameObject.CompareTag("wall"))
            return;

        wallContactCount =
            Mathf.Max(
                0,
                wallContactCount - 1
            );

        if (wallContactCount == 0)
            wallContactTime = 0f;
    }

    public void RefreshTargetCache()
    {
        currentTarget = ResolveCurrentTarget();
        currentLookAheadTarget = ResolveLookAheadTarget();
    }

    private Transform ResolveCurrentTarget()
    {
        if (
            lapTracker == null ||
            trackManager == null
        )
        {
            return null;
        }

        if (lapTracker.WaitingForTrainingStart)
            return trackManager.startFinishLine;

        return trackManager.GetNextTarget(
            lapTracker.NextCheckpoint
        );
    }

    private Transform ResolveLookAheadTarget()
    {
        if (
            lapTracker == null ||
            trackManager == null
        )
        {
            return null;
        }

        if (lapTracker.WaitingForTrainingStart)
            return trackManager.GetNextTarget(0);

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
            actions[0] = 0;
        else if (right && !left)
            actions[0] = 2;
        else
            actions[0] = 1;

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
