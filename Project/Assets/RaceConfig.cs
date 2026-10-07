using UnityEngine;
using Unity.MLAgents;

public class RaceConfig : MonoBehaviour
{
    [Header("References")]
    public CarController carController;
    public TrackManager trackManager;
    public LapTracker lapTracker;

    [Header("Defaults when not training")]
    public int defaultCheckpointCount = 10;
    public float defaultMaxSpeed = 15f;
    public float defaultAcceleration = 20f;
    public float defaultSteeringStrength = 100f;

    private void Start()
    {
        ApplyConfiguration();
    }

    public void ApplyConfiguration()
    {
        var parameters = Academy.Instance.EnvironmentParameters;

        int checkpointCount = Mathf.RoundToInt(
            parameters.GetWithDefault(
                "checkpoint_count",
                defaultCheckpointCount
            )
        );

        float maxSpeed = parameters.GetWithDefault(
            "max_speed",
            defaultMaxSpeed
        );

        float acceleration = parameters.GetWithDefault(
            "acceleration",
            defaultAcceleration
        );

        float steeringStrength = parameters.GetWithDefault(
            "steering_strength",
            defaultSteeringStrength
        );


        // Protect against invalid values
        checkpointCount = Mathf.Clamp(checkpointCount, 1, 100);

        maxSpeed = Mathf.Max(1f, maxSpeed);
        acceleration = Mathf.Max(0.1f, acceleration);
        steeringStrength = Mathf.Max(1f, steeringStrength);


        // Apply car settings
        carController.maxSpeed = maxSpeed;
        carController.acceleration = acceleration;
        carController.steeringStrength = steeringStrength;


        // Create checkpoints
        trackManager.GenerateCheckpoints(checkpointCount);

        // Tell lap system how many checkpoints exist
        lapTracker.SetTotalCheckpoints(checkpointCount);


        Debug.Log(
            $"Race configuration: " +
            $"Checkpoints={checkpointCount}, " +
            $"MaxSpeed={maxSpeed}, " +
            $"Acceleration={acceleration}, " +
            $"Steering={steeringStrength}"
        );
    }
}
