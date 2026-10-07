using UnityEngine;

[RequireComponent(typeof(Rigidbody))]

public class CarController : MonoBehaviour
{
    [Header("Movement")]
    public float acceleration = 20f;
    public float maxSpeed = 15f;

    [Header("Steering")]
    public float steeringStrength = 100f;
    // How much steering remains at maximum speed
    // 0.25 = 25% of normal steering at max speed
    [Range(0.05f, 1f)]
    public float highSpeedSteeringFactor = 0.25f;

    [Header("Grip")]
    public float sidewaysGrip = 5f;

    private Rigidbody rb;

    private float throttleInput;
    private float steeringInput;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        ApplyAcceleration();
        ApplySteering();
        ApplySidewaysGrip();
    }

    public void SetInputs(float throttle, float steering)
    {
        throttleInput = Mathf.Clamp(throttle, -1f, 1f);
        steeringInput = Mathf.Clamp(steering, -1f, 1f);
    }

    private void ApplyAcceleration()
    {
        float forwardSpeed = Vector3.Dot(rb.velocity, transform.forward);

        if (Mathf.Abs(forwardSpeed) < maxSpeed)
        {
            rb.AddForce(
                transform.forward * throttleInput * acceleration,
                ForceMode.Acceleration
            );
        }
    }

    private void ApplySteering()
    {
        float forwardSpeed =
            Mathf.Abs(Vector3.Dot(rb.velocity, transform.forward));

        if (forwardSpeed < 0.1f)
            return;

        // 0 at standstill, 1 at maximum speed
        float normalizedSpeed =
            Mathf.Clamp01(forwardSpeed / maxSpeed);

        // Full steering at low speed,
        // reduced steering at high speed
        float steeringFactor =
            Mathf.Lerp(
                1f,
                highSpeedSteeringFactor,
                normalizedSpeed
            );

        float steeringAmount =
            steeringInput *
            steeringStrength *
            steeringFactor *
            Time.fixedDeltaTime;

        Quaternion rotation =
            Quaternion.Euler(0f, steeringAmount, 0f);

        rb.MoveRotation(rb.rotation * rotation);
    }

    private void ApplySidewaysGrip()
    {
        Vector3 localVelocity =
            transform.InverseTransformDirection(rb.velocity);

        localVelocity.x =
            Mathf.Lerp(
                localVelocity.x,
                0f,
                sidewaysGrip * Time.fixedDeltaTime
            );

        rb.velocity =
            transform.TransformDirection(localVelocity);
    }

}
