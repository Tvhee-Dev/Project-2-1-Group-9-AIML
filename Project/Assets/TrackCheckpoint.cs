using UnityEngine;

public class TrackCheckpoint : MonoBehaviour
{
    public int checkpointIndex;

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log(
            $"Checkpoint {checkpointIndex} touched by {other.gameObject.name}"
        );

        Rigidbody carRigidbody = other.attachedRigidbody;

        if (carRigidbody == null)
        {
            Debug.LogWarning("Object entering checkpoint has no Rigidbody.");
            return;
        }

        LapTracker lapTracker =
            carRigidbody.GetComponent<LapTracker>();

        if (lapTracker == null)
        {
            Debug.LogWarning(
                $"Could not find LapTracker on {carRigidbody.gameObject.name}"
            );
            return;
        }

        lapTracker.PassCheckpoint(checkpointIndex);
    }
}
