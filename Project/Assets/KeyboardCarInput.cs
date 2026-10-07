using UnityEngine;

public class KeyboardCarInput : MonoBehaviour
{
    private CarController carController;

    private void Awake()
    {
        carController = GetComponent<CarController>();
    }

    private void Update()
    {
        float steering = Input.GetAxis("Horizontal");
        float throttle = Input.GetAxis("Vertical");

        carController.SetInputs(throttle, steering);
    }
}
