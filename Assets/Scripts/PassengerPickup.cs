using UnityEngine;

public class PassengerPickup : MonoBehaviour
{
    public Transform dropOffWaypoint;
    public int rewardAmount = 50000;
    public GameObject modelToHide;

    public float requiredStopSpeed = 5f;

    private bool pickedUp = false;
    private bool playerInside = false;

    private GameObject playerObject;

    private void OnTriggerEnter(Collider other)
    {
        if (pickedUp || !other.CompareTag("Player")) return;

        var carController = other.GetComponent<Ezereal.EzerealCarController>();
        if (carController != null && carController.currentSpeed > requiredStopSpeed)
        {
            // Show UI: "Slow down to pick up passenger"
            Debug.Log("Too fast to pick up passenger!");
            // TODO: UIManager.Instance.ShowPickupWarning(); ← Optional
        }

        playerInside = true;
        playerObject = other.gameObject;
    }

    private void OnTriggerStay(Collider other)
    {
        if (pickedUp || !other.CompareTag("Player")) return;

        var passengerSystem = other.GetComponent<PlayerPassengerSystem>();
        var carController = other.GetComponent<Ezereal.EzerealCarController>();

        if (passengerSystem != null && carController != null && !passengerSystem.HasPassenger())
        {
            if (carController.currentSpeed <= requiredStopSpeed)
            {
                passengerSystem.PickUpPassenger(this);
                pickedUp = true;

                if (modelToHide != null)
                    modelToHide.SetActive(false);

                GetComponent<Collider>().enabled = false;

                // Hide UI if shown
                Debug.Log("Passenger picked up!");
                // TODO: UIManager.Instance.HidePickupWarning(); ← Optional
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        playerInside = false;
        playerObject = null;

        // Hide warning UI if shown
        Debug.Log("Left pickup area");
        // TODO: UIManager.Instance.HidePickupWarning(); ← Optional
    }
}
