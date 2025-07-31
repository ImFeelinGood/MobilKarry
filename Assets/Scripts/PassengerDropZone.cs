using UnityEngine;

public class PassengerDropZone : MonoBehaviour
{
    public float dropStopSpeed = 5f; // km/h

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        var carController = other.GetComponent<Ezereal.EzerealCarController>();
        var passengerSystem = other.GetComponent<PlayerPassengerSystem>();

        if (passengerSystem != null && carController != null && passengerSystem.HasPassenger())
        {
            // Change carController.currentSpeed to carController.stationary = false
            if (carController.currentSpeed > dropStopSpeed)
            {
                Debug.Log("Too fast to drop off passenger!");
                // TODO: UIManager.Instance.ShowDropoffWarning();
            }
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        var passengerSystem = other.GetComponent<PlayerPassengerSystem>();
        var carController = other.GetComponent<Ezereal.EzerealCarController>();

        if (passengerSystem != null && carController != null && passengerSystem.HasPassenger())
        {
            // Change carController.currentSpeed to carController.stationary = true
            if (carController.currentSpeed <= dropStopSpeed)
            {
                passengerSystem.DropOffPassenger();
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        // TODO: UIManager.Instance.HideDropoffWarning();
        Debug.Log("Left drop-off zone");
    }
}
