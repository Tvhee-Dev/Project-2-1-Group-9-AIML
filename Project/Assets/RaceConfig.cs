using UnityEngine;
using Unity.MLAgents;

public class RaceConfig : MonoBehaviour
{
    [Header("References")]
    public CarController carController;
    public CarAgent carAgent;
    public TrackManager trackManager;
    public LapTracker lapTracker;


    [Header("Default Track Settings")]
    public int defaultCheckpointCount = 10;


    [Header("Default Car Settings")]
    public float defaultMaxSpeed = 15f;
    public float defaultAcceleration = 20f;
    public float defaultSteeringStrength = 100f;


    [Header("Default Wall Sensor Settings")]
    public int defaultWallRayCount = 9;
    public float defaultWallRayFov = 180f;

    public float defaultWallRaySideDistance = 12f;
    public float defaultWallRayCenterDistance = 30f;

    public float defaultWallRayCenterPower = 1.5f;


    private void Start()
    {
        ApplyConfiguration();
    }


    public void ApplyConfiguration()
    {
        var parameters =
            Academy.Instance.EnvironmentParameters;

        // Track settings
   
        int checkpointCount =
            Mathf.RoundToInt(
                parameters.GetWithDefault(
                    "checkpoint_count",
                    defaultCheckpointCount
                )
            );

        // Car settings

        float maxSpeed =
            parameters.GetWithDefault(
                "max_speed",
                defaultMaxSpeed
            );

        float acceleration =
            parameters.GetWithDefault(
                "acceleration",
                defaultAcceleration
            );

        float steeringStrength =
            parameters.GetWithDefault(
                "steering_strength",
                defaultSteeringStrength
            );

        // Wall sensor settings

        int wallRayCount =
            Mathf.RoundToInt(
                parameters.GetWithDefault(
                    "wall_ray_count",
                    defaultWallRayCount
                )
            );

        float wallRayFov =
            parameters.GetWithDefault(
                "wall_ray_fov",
                defaultWallRayFov
            );

        float wallRaySideDistance =
            parameters.GetWithDefault(
                "wall_ray_side_distance",
                defaultWallRaySideDistance
            );

        float wallRayCenterDistance =
            parameters.GetWithDefault(
                "wall_ray_center_distance",
                defaultWallRayCenterDistance
            );

        float wallRayCenterPower =
            parameters.GetWithDefault(
                "wall_ray_center_power",
                defaultWallRayCenterPower
            );

        // Protect against bad values

        checkpointCount =
            Mathf.Clamp(
                checkpointCount,
                1,
                100
            );

        maxSpeed =
            Mathf.Max(
                1f,
                maxSpeed
            );

        acceleration =
            Mathf.Max(
                0.1f,
                acceleration
            );

        steeringStrength =
            Mathf.Max(
                1f,
                steeringStrength
            );

        wallRayCount =
            Mathf.Clamp(
                wallRayCount,
                1,
                CarAgent.MaxWallRayCount
            );

        wallRayFov =
            Mathf.Clamp(
                wallRayFov,
                1f,
                180f
            );

        wallRaySideDistance =
            Mathf.Max(
                0.1f,
                wallRaySideDistance
            );

        wallRayCenterDistance =
            Mathf.Max(
                wallRaySideDistance,
                wallRayCenterDistance
            );

        wallRayCenterPower =
            Mathf.Max(
                0.01f,
                wallRayCenterPower
            );

        // Apply car settings

        carController.maxSpeed =
            maxSpeed;

        carController.acceleration =
            acceleration;

        carController.steeringStrength =
            steeringStrength;

        // Apply sensor settings

        carAgent.wallRayCount =
            wallRayCount;

        carAgent.wallRayFov =
            wallRayFov;

        carAgent.wallRaySideDistance =
            wallRaySideDistance;

        carAgent.wallRayCenterDistance =
            wallRayCenterDistance;

        carAgent.wallRayCenterPower =
            wallRayCenterPower;

        // Generate track

        trackManager.GenerateCheckpoints(
            checkpointCount
        );

        lapTracker.SetTotalCheckpoints(
            checkpointCount
        );


        Debug.Log(
            $"Race configuration: " +
            $"Checkpoints={checkpointCount}, " +
            $"MaxSpeed={maxSpeed}, " +
            $"Acceleration={acceleration}, " +
            $"Steering={steeringStrength}, " +
            $"Rays={wallRayCount}, " +
            $"FOV={wallRayFov}, " +
            $"SideRayDistance={wallRaySideDistance}, " +
            $"CenterRayDistance={wallRayCenterDistance}, " +
            $"CenterPower={wallRayCenterPower}"
        );
    }
}
