using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

public class CarAgent : Agent
{
    [Header("References")]
    public CarController carController;
    public LapTracker lapTracker;
    public TrackManager trackManager;
    public Rigidbody rb;

    [Header("Wall Sensors")]
    public float wallRayDistance = 20f;
    public float wallRayHeight = 0.3f;
    public LayerMask wallLayerMask;

    [Header("Wall Handling")]
    public float wallPenaltyPerSecond = 0.05f;
    public float maxWallContactSeconds = 2f;
    public float prolongedWallPenalty = -0.5f;

    private float wallContactTime;

    [Header("Episode Reset")]
    public Transform spawnPoint;

    [Header("Progress Reward")]
    public float progressRewardScale = 0.05f;

    [Header("Lap Time Reward")]
    public float fastLapTime = 30f;
    public float slowLapTime = 90f;

    public float minimumLapTimeBonus = 0.2f;
    public float maximumLapTimeBonus = 1.5f;

    [Header("Episode Limits")]
    public float maxSecondsToReachStart = 60f;
    public float maxSecondsWithoutCheckpoint = 45f;
    public float maxSecondsToFinish = 45f;

    private float timeSinceCheckpoint;

    private float previousDistanceToTarget;
    private Transform previousTarget;

    [Header("Observation Scaling")]
    public float distanceNormalization = 50f;
    public float angularVelocityNormalization = 5f;

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
    }

    private void FixedUpdate()
    {
        if (!lapTracker.LapStarted &&
            !lapTracker.WaitingForTrainingStart)
        {
            return;
        }

        timeSinceCheckpoint += Time.fixedDeltaTime;


        // Determine how much time the agent gets
        float allowedTime;

        if (lapTracker.WaitingForTrainingStart)
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


        // Reset if it takes too long
        if (timeSinceCheckpoint >= allowedTime)
        {
            AddReward(-0.5f);
            EndEpisode();
            return;
        }


        Transform target = GetCurrentTarget();

        if (target == null)
            return;


        float currentDistance =
            Vector3.Distance(
                transform.position,
                target.position
            );


        // Target changed
        // Start - CP0, CP0 - CP1, etc.
        if (target != previousTarget)
        {
            previousTarget = target;
            previousDistanceToTarget = currentDistance;
            return;
        }


        // Reward velocity toward current target
        Vector3 directionToTarget =
            (target.position - transform.position).normalized;

        float forwardProgress =
            Vector3.Dot(
                rb.velocity,
                directionToTarget
            );

        float normalizedProgress =
            forwardProgress /
            Mathf.Max(
                carController.maxSpeed,
                0.1f
            );

        AddReward(
            normalizedProgress * 0.001f
        );


        // Reward reduction in distance to target
        float progress =
            previousDistanceToTarget -
            currentDistance;

        AddReward(
            progress * progressRewardScale
        );

        previousDistanceToTarget =
            currentDistance;
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

        Physics.SyncTransforms();

        lapTracker.ResetForTrainingEpisode();

        previousTarget = GetCurrentTarget();

        if (previousTarget != null)
        {
            previousDistanceToTarget =
                Vector3.Distance(
                    transform.position,
                    previousTarget.position
                );
        }
        else
        {
            previousDistanceToTarget = 0f;
        }

        timeSinceCheckpoint = 0f;
        wallContactTime = 0f;
    }

    private void AddWallRayObservations(VectorSensor sensor)
    {
        Vector3 rayOrigin =
            transform.position + Vector3.up * wallRayHeight;

        float[] rayAngles =
        {
            0f,     
            20f,    
            -20f,   
            45f,    
            -45f, 
            90f,    
            -90f,   
            180f
        };

        foreach (float angle in rayAngles)
        {
            Vector3 worldDirection =
                Quaternion.AngleAxis(angle, transform.up) *
                transform.forward;

            float normalizedDistance = 1f;

            if (Physics.Raycast(
                rayOrigin,
                worldDirection,
                out RaycastHit hit,
                wallRayDistance,
                wallLayerMask
            ))
            {
                normalizedDistance =
                    hit.distance / wallRayDistance;

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
                    worldDirection * wallRayDistance,
                    Color.green
                );
            }

            sensor.AddObservation(normalizedDistance);
        }
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

        if (target != null)
        {
            Vector3 direction =
                target.position - transform.position;

            float distance =
                direction.magnitude;

            Vector3 localDirection =
                transform.InverseTransformDirection(
                    direction.normalized
                );

            sensor.AddObservation(localDirection.x);
            sensor.AddObservation(localDirection.z);

            sensor.AddObservation(
                Mathf.Clamp01(
                    distance / distanceNormalization
                )
            );
        }
        else
        {
            // Always provide the same number of observations
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
        }

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
        float steering =
            Mathf.Clamp(
                actions.ContinuousActions[0],
                -1f,
                1f
            );

        float longitudinalAction =
            Mathf.Clamp(
                actions.ContinuousActions[1],
                -1f,
                1f
            );

        float throttle = 0f;
        float brake = 0f;

        if (longitudinalAction > 0f)
        {
            throttle = longitudinalAction;
        }
        else if (longitudinalAction < 0f)
        {
            brake = -longitudinalAction;
        }

        carController.SetInputs(
            throttle,
            steering,
            brake
        );
    }

    private void OnCheckpointPassed(int checkpointIndex)
    {
        timeSinceCheckpoint = 0f;
        if (lapTracker.totalCheckpoints <= 0)
            return;

        AddReward(1f / lapTracker.totalCheckpoints);
    }

    private float CalculateLapTimeBonus(float lapTime)
    {
        float normalized =
            Mathf.InverseLerp(
                slowLapTime,
                fastLapTime,
                lapTime
            );

        return Mathf.Lerp(
            minimumLapTimeBonus,
            maximumLapTimeBonus,
            normalized
        );
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

        EndEpisode();
    }

    private void OnStartFinishCrossed()
    {
        if (lapTracker.WaitingForTrainingStart)
        {
            AddReward(0.05f);
        }

        timeSinceCheckpoint = 0f;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("wall"))
        {
            wallContactTime = 0f;
        }
    }

    private void OnCollisionStay(Collision collision)
    {
        if (!collision.gameObject.CompareTag("wall"))
            return;

        wallContactTime += Time.fixedDeltaTime;

        // Continuous penalty while scraping/pushing against a wall.
        AddReward(
            -wallPenaltyPerSecond * Time.fixedDeltaTime
        );

        // The car is clearly stuck.
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

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        ActionSegment<float> actions =
            actionsOut.ContinuousActions;

        actions[0] = Input.GetAxis("Horizontal");
        actions[1] = Input.GetAxis("Vertical");
    }
}
