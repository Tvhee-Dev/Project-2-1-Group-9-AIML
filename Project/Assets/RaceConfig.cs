using UnityEngine;
using Unity.MLAgents;

public class RaceConfig : MonoBehaviour
{
    [Header("References")]
    public CarController carController;
    public CarAgent carAgent;
    public TrackManager trackManager;
    public LapTracker lapTracker;
    public DecisionRequester decisionRequester;

    [Header("Default Track Settings")]
    public int defaultCheckpointCount = 51;

    [Header("Default Car Settings")]
    public float defaultMaxSpeed = 15f;
    public float defaultAcceleration = 20f;
    public float defaultBrakeStrength = 30f;
    public float defaultSteeringStrength = 100f;
    [Range(0.05f, 1f)] public float defaultHighSpeedSteeringFactor = 0.25f;
    public float defaultMinimumForwardSpeed = 0f;
    public float defaultSidewaysGrip = 5f;

    [Header("Default Wall Sensor Settings")]
    public int defaultWallRayCount = 15;
    public float defaultWallRayFov = 180f;
    public float defaultWallRaySideDistance = 12f;
    public float defaultWallRayCenterDistance = 70f;
    public float defaultWallRayCenterPower = 1.5f;
    [Range(1f, 3f)] public float defaultWallRayAnglePower = 1.5f;
    public float defaultWallRayForwardOffset = 1.2f;
    public float defaultWallRayHeight = 0.3f;

    [Header("Default Wall Reward Settings")]
    public float defaultWallPenaltyPerSecond = 0.05f;
    public float defaultMaxWallContactSeconds = 0.75f;
    public float defaultProlongedWallPenalty = -1f;
    public float defaultWallImpactPenalty = 0.1f;

    [Header("Default Progress Reward Settings")]
    public float defaultCheckpointRewardPerLap = 1f;
    public float defaultTargetProgressRewardScale = 0.01f;
    public float defaultForwardProgressRewardPerSecond = 0.02f;

    [Header("Default Driving Quality Settings")]
    public float defaultTimePenaltyPerSecond = 0.05f;
    public float defaultExcessiveYawPenaltyPerSecond = 0.01f;
    public float defaultSlipPenaltyPerSecond = 0.005f;
    public float defaultAllowedYawRate = 2.2f;
    public float defaultAllowedSlipAngle = 30f;
    public float defaultMaximumSlipAngle = 75f;

    [Header("Default Lap Time Reward Settings")]
    public float defaultTargetLapTime = 46f;
    public float defaultLapTimeRewardAtTarget = 6f;
    public float defaultLapTimeRewardScale = 3f;

    [Header("Default Episode Limit Settings")]
    public float defaultMaxSecondsToReachStart = 60f;
    public float defaultMaxSecondsWithoutCheckpoint = 45f;
    public float defaultMaxSecondsToFinish = 45f;

    [Header("Default Observation Settings")]
    public float defaultDistanceNormalization = 50f;
    public float defaultAngularVelocityNormalization = 5f;

    [Header("Default Decision Settings")]
    [Min(1)] public int defaultDecisionPeriod = 1;
    public bool defaultTakeActionsBetweenDecisions = true;

    private void Start()
    {
        ApplyConfiguration();
    }

    public void ApplyConfiguration()
    {
        var parameters = Academy.Instance.EnvironmentParameters;

        int checkpointCount = GetInt(parameters, "checkpoint_count", defaultCheckpointCount);

        float maxSpeed = GetFloat(parameters, "max_speed", defaultMaxSpeed);
        float acceleration = GetFloat(parameters, "acceleration", defaultAcceleration);
        float brakeStrength = GetFloat(parameters, "brake_strength", defaultBrakeStrength);
        float steeringStrength = GetFloat(parameters, "steering_strength", defaultSteeringStrength);
        float highSpeedSteeringFactor = GetFloat(
            parameters, "high_speed_steering_factor", defaultHighSpeedSteeringFactor
        );
        float minimumForwardSpeed = GetFloat(
            parameters, "minimum_forward_speed", defaultMinimumForwardSpeed
        );
        float sidewaysGrip = GetFloat(parameters, "sideways_grip", defaultSidewaysGrip);

        int wallRayCount = GetInt(parameters, "wall_ray_count", defaultWallRayCount);
        float wallRayFov = GetFloat(parameters, "wall_ray_fov", defaultWallRayFov);
        float wallRaySideDistance = GetFloat(
            parameters, "wall_ray_side_distance", defaultWallRaySideDistance
        );
        float wallRayCenterDistance = GetFloat(
            parameters, "wall_ray_center_distance", defaultWallRayCenterDistance
        );
        float wallRayCenterPower = GetFloat(
            parameters, "wall_ray_center_power", defaultWallRayCenterPower
        );
        float wallRayAnglePower = GetFloat(
            parameters, "wall_ray_angle_power", defaultWallRayAnglePower
        );
        float wallRayForwardOffset = GetFloat(
            parameters, "wall_ray_forward_offset", defaultWallRayForwardOffset
        );
        float wallRayHeight = GetFloat(parameters, "wall_ray_height", defaultWallRayHeight);

        float wallPenaltyPerSecond = GetFloat(
            parameters, "wall_penalty_per_second", defaultWallPenaltyPerSecond
        );
        float maxWallContactSeconds = GetFloat(
            parameters, "max_wall_contact_seconds", defaultMaxWallContactSeconds
        );
        float prolongedWallPenalty = GetFloat(
            parameters, "prolonged_wall_penalty", defaultProlongedWallPenalty
        );
        float wallImpactPenalty = GetFloat(
            parameters, "wall_impact_penalty", defaultWallImpactPenalty
        );

        float checkpointRewardPerLap = GetFloat(
            parameters, "checkpoint_reward_per_lap", defaultCheckpointRewardPerLap
        );
        float targetProgressRewardScale = GetFloat(
            parameters, "target_progress_reward_scale", defaultTargetProgressRewardScale
        );
        float forwardProgressRewardPerSecond = GetFloat(
            parameters,
            "forward_progress_reward_per_second",
            defaultForwardProgressRewardPerSecond
        );

        float timePenaltyPerSecond = GetFloat(
            parameters, "time_penalty_per_second", defaultTimePenaltyPerSecond
        );
        float excessiveYawPenaltyPerSecond = GetFloat(
            parameters,
            "excessive_yaw_penalty_per_second",
            defaultExcessiveYawPenaltyPerSecond
        );
        float slipPenaltyPerSecond = GetFloat(
            parameters, "slip_penalty_per_second", defaultSlipPenaltyPerSecond
        );
        float allowedYawRate = GetFloat(
            parameters, "allowed_yaw_rate", defaultAllowedYawRate
        );
        float allowedSlipAngle = GetFloat(
            parameters, "allowed_slip_angle", defaultAllowedSlipAngle
        );
        float maximumSlipAngle = GetFloat(
            parameters, "maximum_slip_angle", defaultMaximumSlipAngle
        );

        float targetLapTime = GetFloat(
            parameters, "target_lap_time", defaultTargetLapTime
        );
        float lapTimeRewardAtTarget = GetFloat(
            parameters, "lap_time_reward_at_target", defaultLapTimeRewardAtTarget
        );
        float lapTimeRewardScale = GetFloat(
            parameters, "lap_time_reward_scale", defaultLapTimeRewardScale
        );

        float maxSecondsToReachStart = GetFloat(
            parameters, "max_seconds_to_reach_start", defaultMaxSecondsToReachStart
        );
        float maxSecondsWithoutCheckpoint = GetFloat(
            parameters,
            "max_seconds_without_checkpoint",
            defaultMaxSecondsWithoutCheckpoint
        );
        float maxSecondsToFinish = GetFloat(
            parameters, "max_seconds_to_finish", defaultMaxSecondsToFinish
        );

        float distanceNormalization = GetFloat(
            parameters, "distance_normalization", defaultDistanceNormalization
        );
        float angularVelocityNormalization = GetFloat(
            parameters,
            "angular_velocity_normalization",
            defaultAngularVelocityNormalization
        );

        int decisionPeriod = GetInt(
            parameters, "decision_period", defaultDecisionPeriod
        );
        bool takeActionsBetweenDecisions = GetFloat(
            parameters,
            "take_actions_between_decisions",
            defaultTakeActionsBetweenDecisions ? 1f : 0f
        ) >= 0.5f;

        checkpointCount = Mathf.Clamp(checkpointCount, 1, 100);
        maxSpeed = Mathf.Max(1f, maxSpeed);
        acceleration = Mathf.Max(0.1f, acceleration);
        brakeStrength = Mathf.Max(0.1f, brakeStrength);
        steeringStrength = Mathf.Max(1f, steeringStrength);
        highSpeedSteeringFactor = Mathf.Clamp(highSpeedSteeringFactor, 0.05f, 1f);
        minimumForwardSpeed = Mathf.Clamp(minimumForwardSpeed, 0f, maxSpeed);
        sidewaysGrip = Mathf.Max(0f, sidewaysGrip);

        wallRayCount = Mathf.Clamp(wallRayCount, 1, CarAgent.MaxWallRayCount);
        wallRayFov = Mathf.Clamp(wallRayFov, 1f, 180f);
        wallRaySideDistance = Mathf.Max(0.1f, wallRaySideDistance);
        wallRayCenterDistance = Mathf.Max(wallRaySideDistance, wallRayCenterDistance);
        wallRayCenterPower = Mathf.Max(0.01f, wallRayCenterPower);
        wallRayAnglePower = Mathf.Clamp(wallRayAnglePower, 1f, 3f);
        wallRayForwardOffset = Mathf.Max(0f, wallRayForwardOffset);

        wallPenaltyPerSecond = Mathf.Max(0f, wallPenaltyPerSecond);
        maxWallContactSeconds = Mathf.Max(0.05f, maxWallContactSeconds);
        prolongedWallPenalty = Mathf.Min(0f, prolongedWallPenalty);
        wallImpactPenalty = Mathf.Max(0f, wallImpactPenalty);
        checkpointRewardPerLap = Mathf.Max(0f, checkpointRewardPerLap);
        targetProgressRewardScale = Mathf.Max(0f, targetProgressRewardScale);
        forwardProgressRewardPerSecond = Mathf.Max(0f, forwardProgressRewardPerSecond);
        timePenaltyPerSecond = Mathf.Max(0f, timePenaltyPerSecond);
        excessiveYawPenaltyPerSecond = Mathf.Max(0f, excessiveYawPenaltyPerSecond);
        slipPenaltyPerSecond = Mathf.Max(0f, slipPenaltyPerSecond);
        allowedYawRate = Mathf.Max(0f, allowedYawRate);
        allowedSlipAngle = Mathf.Max(0f, allowedSlipAngle);
        maximumSlipAngle = Mathf.Max(allowedSlipAngle, maximumSlipAngle);
        targetLapTime = Mathf.Max(0.1f, targetLapTime);
        lapTimeRewardAtTarget = Mathf.Max(0f, lapTimeRewardAtTarget);
        lapTimeRewardScale = Mathf.Max(0.01f, lapTimeRewardScale);
        maxSecondsToReachStart = Mathf.Max(0.1f, maxSecondsToReachStart);
        maxSecondsWithoutCheckpoint = Mathf.Max(0.1f, maxSecondsWithoutCheckpoint);
        maxSecondsToFinish = Mathf.Max(0.1f, maxSecondsToFinish);
        distanceNormalization = Mathf.Max(0.1f, distanceNormalization);
        angularVelocityNormalization = Mathf.Max(0.1f, angularVelocityNormalization);
        decisionPeriod = Mathf.Clamp(decisionPeriod, 1, 20);

        carController.maxSpeed = maxSpeed;
        carController.acceleration = acceleration;
        carController.brakeStrength = brakeStrength;
        carController.steeringStrength = steeringStrength;
        carController.highSpeedSteeringFactor = highSpeedSteeringFactor;
        carController.minimumForwardSpeed = minimumForwardSpeed;
        carController.sidewaysGrip = sidewaysGrip;

        carAgent.wallRayCount = wallRayCount;
        carAgent.wallRayFov = wallRayFov;
        carAgent.wallRaySideDistance = wallRaySideDistance;
        carAgent.wallRayCenterDistance = wallRayCenterDistance;
        carAgent.wallRayCenterPower = wallRayCenterPower;
        carAgent.wallRayAnglePower = wallRayAnglePower;
        carAgent.wallRayForwardOffset = wallRayForwardOffset;
        carAgent.wallRayHeight = wallRayHeight;
        carAgent.wallPenaltyPerSecond = wallPenaltyPerSecond;
        carAgent.maxWallContactSeconds = maxWallContactSeconds;
        carAgent.prolongedWallPenalty = prolongedWallPenalty;
        carAgent.wallImpactPenalty = wallImpactPenalty;
        carAgent.checkpointRewardPerLap = checkpointRewardPerLap;
        carAgent.targetProgressRewardScale = targetProgressRewardScale;
        carAgent.forwardProgressRewardPerSecond = forwardProgressRewardPerSecond;
        carAgent.timePenaltyPerSecond = timePenaltyPerSecond;
        carAgent.excessiveYawPenaltyPerSecond = excessiveYawPenaltyPerSecond;
        carAgent.slipPenaltyPerSecond = slipPenaltyPerSecond;
        carAgent.allowedYawRate = allowedYawRate;
        carAgent.allowedSlipAngle = allowedSlipAngle;
        carAgent.maximumSlipAngle = maximumSlipAngle;
        carAgent.targetLapTime = targetLapTime;
        carAgent.lapTimeRewardAtTarget = lapTimeRewardAtTarget;
        carAgent.lapTimeRewardScale = lapTimeRewardScale;
        carAgent.maxSecondsToReachStart = maxSecondsToReachStart;
        carAgent.maxSecondsWithoutCheckpoint = maxSecondsWithoutCheckpoint;
        carAgent.maxSecondsToFinish = maxSecondsToFinish;
        carAgent.distanceNormalization = distanceNormalization;
        carAgent.angularVelocityNormalization = angularVelocityNormalization;

        if (decisionRequester != null)
        {
            decisionRequester.DecisionPeriod = decisionPeriod;
            decisionRequester.TakeActionsBetweenDecisions = takeActionsBetweenDecisions;
        }

        trackManager.GenerateCheckpoints(checkpointCount);
        lapTracker.SetTotalCheckpoints(checkpointCount);

        Debug.Log(
            "Race configuration: " +
            $"Checkpoints={checkpointCount}, " +
            $"MaxSpeed={maxSpeed}, " +
            $"Brake={brakeStrength}, " +
            $"Steering={steeringStrength}, " +
            $"HighSpeedSteering={highSpeedSteeringFactor}, " +
            $"Grip={sidewaysGrip}, " +
            $"Rays={wallRayCount}, " +
            $"CenterRayDistance={wallRayCenterDistance}, " +
            $"TimePenalty={timePenaltyPerSecond}, " +
            $"TargetLapTime={targetLapTime}, " +
            $"LapReward={lapTimeRewardAtTarget}, " +
            $"DecisionPeriod={decisionPeriod}"
        );
    }

    private static float GetFloat(
        EnvironmentParameters parameters,
        string name,
        float defaultValue
    )
    {
        return parameters.GetWithDefault(name, defaultValue);
    }

    private static int GetInt(
        EnvironmentParameters parameters,
        string name,
        int defaultValue
    )
    {
        return Mathf.RoundToInt(parameters.GetWithDefault(name, defaultValue));
    }
}
