using System.Collections.Generic;
using UnityEngine;

public class PlayerPassengerSystem : MonoBehaviour
{
    public int maxPassengers = 8;

    [SerializeField] private List<PassengerData> currentPassengers = new List<PassengerData>();
    public GameObject dropOffMarkerPrefab;

    [System.Serializable]
    public class PassengerData
    {
        public Transform waypoint;
        public int reward;
        public GameObject marker;
    }

    public void PickUpPassenger(PassengerPickup passengerPickup)
    {
        if (currentPassengers.Count >= maxPassengers) return;

        PassengerData data = new PassengerData
        {
            waypoint = passengerPickup.GetWaypoint(),
            reward = passengerPickup.GetReward(),
            marker = Instantiate(dropOffMarkerPrefab, passengerPickup.GetWaypoint().position, Quaternion.identity)
        };

        currentPassengers.Add(data);
    }

    public void DropOffPassenger()
    {
        if (currentPassengers.Count == 0) return;

        Vector3 pos = transform.position;
        PassengerData drop = null;

        foreach (var p in currentPassengers)
        {
            if (Vector3.Distance(pos, p.waypoint.position) < 10f)
            {
                drop = p;
                break;
            }
        }

        if (drop != null)
        {
            CurrencyManager.Instance.AddRupiah(drop.reward);
            Debug.Log("Dropped off passenger, Reward: " + drop.reward);

            if (drop.marker) Destroy(drop.marker);
            currentPassengers.Remove(drop);
        }
    }

    public bool HasPassenger() => currentPassengers.Count > 0;

    public bool CanPickUpPassenger() => currentPassengers.Count < maxPassengers;
}
