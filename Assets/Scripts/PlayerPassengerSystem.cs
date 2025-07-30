using UnityEngine;

public class PlayerPassengerSystem : MonoBehaviour
{
    private PassengerPickup currentPassenger;

    public GameObject dropOffMarkerPrefab;
    private GameObject activeMarker;

    public void PickUpPassenger(PassengerPickup passenger)
    {
        currentPassenger = passenger;

        // Spawn drop-off marker
        if (dropOffMarkerPrefab && passenger.dropOffWaypoint != null)
        {
            activeMarker = Instantiate(dropOffMarkerPrefab, passenger.dropOffWaypoint.position, Quaternion.identity);
        }

        Debug.Log("Passenger picked up!");
    }

    public void DropOffPassenger()
    {
        if (currentPassenger == null) return;

        // Give reward
        CurrencyManager.Instance.AddRupiah(currentPassenger.rewardAmount);
        Debug.Log("Passenger dropped off! Reward: Rp" + currentPassenger.rewardAmount);

        // Remove marker
        if (activeMarker)
            Destroy(activeMarker);

        currentPassenger = null;
    }

    public bool HasPassenger()
    {
        return currentPassenger != null;
    }
}
