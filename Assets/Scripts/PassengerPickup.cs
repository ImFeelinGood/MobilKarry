using UnityEngine;

public class PassengerPickup : MonoBehaviour
{
    [Header("Passenger Info")]
    public Transform dropOffWaypoint;
    public int rewardAmount = 50000;

    [Header("Passenger Appearance")]
    public GameObject[] passengerModels; // Assign different models in inspector
    private GameObject activeModel;

    [Header("Settings")]
    public float requiredStopSpeed = 5f;

    private bool pickedUp = false;

    private void Start()
    {
        // Spawn random visual model
        if (passengerModels != null && passengerModels.Length > 0)
        {
            int randIndex = Random.Range(0, passengerModels.Length);
            activeModel = Instantiate(passengerModels[randIndex], transform);
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (pickedUp || !other.CompareTag("Player")) return;

        var passengerSystem = other.GetComponent<PlayerPassengerSystem>();
        var carController = other.GetComponent<Ezereal.EzerealCarController>();

        if (passengerSystem != null && carController != null && carController.currentSpeed <= requiredStopSpeed)
        {
            if (passengerSystem.CanPickUpPassenger()) // Only if under limit
            {
                // Send info to system
                passengerSystem.PickUpPassenger(this);

                // Cleanup visuals
                if (activeModel != null)
                    Destroy(activeModel);

                pickedUp = true;
                GetComponent<Collider>().enabled = false;

                Debug.Log("Passenger picked up.");
            }
            else
            {
                Debug.Log("Cannot pick up more passengers.");
            }
        }
    }

    public Transform GetWaypoint() => dropOffWaypoint;
    public int GetReward() => rewardAmount;
}
