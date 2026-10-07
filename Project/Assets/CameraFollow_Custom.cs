using UnityEngine;

public class CameraFollow_Custom : MonoBehaviour
{
    [Tooltip("The target to follow")]
    public Transform target;

    [Tooltip("Time to transition to new position")]
    public float smoothingTime = 0.1f;

    public Vector3 offset = new Vector3(0f, 3f, -6f);

    private Vector3 cameraVelocity;

    private void LateUpdate()
    {
        if (target == null)
            return;

        Vector3 desiredPosition =
            target.position +
            target.TransformDirection(offset);

        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref cameraVelocity,
            smoothingTime
        );

        transform.LookAt(
            target.position + Vector3.up
        );
    }
}
