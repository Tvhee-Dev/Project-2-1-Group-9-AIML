using UnityEngine;

public class StartFinishLine : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        LapTracker lapTracker =
            other.GetComponentInParent<LapTracker>();

        if (lapTracker != null)
        {
            lapTracker.CrossStartFinish();
        }
    }
}
