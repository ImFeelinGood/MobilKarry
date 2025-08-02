using UnityEngine;

public class PassengerPickup : MonoBehaviour
{
    [Header("Passenger Info")]
    public Transform dropOffWaypoint;
    public int rewardAmount = 50000;

    [Header("Passenger Appearance")]
    public GameObject[] passengerModels;
    private GameObject activeModel;

    private bool pickedUp = false;

    private void Start()
    {
        if (passengerModels != null && passengerModels.Length > 0)
        {
            int randIndex = Random.Range(0, passengerModels.Length);
            activeModel = Instantiate(passengerModels[randIndex], transform);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        var carController = other.GetComponent<Ezereal.EzerealCarController>();
        var passengerSystem = other.GetComponent<PlayerPassengerSystem>();

        if (passengerSystem != null && carController != null && passengerSystem.HasPassenger())
        {
            if (carController.stationary == false)
            {
                Debug.Log("Stop to pick up passenger!");
                // TODO: UIManager.Instance.ShowDropoffWarning();
            }
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (pickedUp || !other.CompareTag("Player")) return;

        var passengerSystem = other.GetComponent<PlayerPassengerSystem>();
        var carController = other.GetComponent<Ezereal.EzerealCarController>();

        if (passengerSystem != null && carController != null && carController.stationary == true)
        {
            if (passengerSystem.CanPickUpPassenger())
            {
                passengerSystem.PickUpPassenger(this);

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
