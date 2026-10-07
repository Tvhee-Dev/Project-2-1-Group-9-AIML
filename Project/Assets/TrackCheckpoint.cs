using UnityEngine;

public class TrackCheckpoint : MonoBehaviour
{
    public int checkpointIndex;

    private void OnTriggerEnter(Collider other)
    {
        Rigidbody carRigidbody = other.attachedRigidbody;

        if (carRigidbody == null)
            return;

        LapTracker lapTracker =
            carRigidbody.GetComponent<LapTracker>();

        if (lapTracker == null)
            return;

        lapTracker.PassCheckpoint(checkpointIndex);
    }
}
