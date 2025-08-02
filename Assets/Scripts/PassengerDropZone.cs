using UnityEngine;

public class PassengerDropZone : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        var carController = other.GetComponent<Ezereal.EzerealCarController>();
        var passengerSystem = other.GetComponent<PlayerPassengerSystem>();

        if (passengerSystem != null && carController != null && passengerSystem.HasPassenger())
        {
            if (carController.stationary == false)
            {
                Debug.Log("Stop to drop off passenger!");
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
            if (carController.stationary == true)
            {
                passengerSystem.DropOffPassenger();
            }
        }
    }
}
