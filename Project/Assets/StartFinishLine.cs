using UnityEngine;

public class StartFinishLine : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        Rigidbody carRigidbody =
            other.attachedRigidbody;

        if (carRigidbody == null)
            return;

        LapTracker lapTracker =
            carRigidbody.GetComponentInParent<LapTracker>();

        if (lapTracker == null)
            return;

        lapTracker.CrossStartFinish();
    }
}
