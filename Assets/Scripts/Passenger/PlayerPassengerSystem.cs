using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class PlayerPassengerSystem : MonoBehaviour
{
    public WaypointArrow waypointArrow;

    public TMP_Text passengerCountText;

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

    private void Start()
    {
        UpdatePassengerUI();
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

        UpdateWaypointArrow();
        UpdatePassengerUI();
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

        UpdateWaypointArrow();
        UpdatePassengerUI();
    }

    private void UpdateWaypointArrow()
    {
        if (waypointArrow != null)
        {
            if (currentPassengers.Count == 0)
            {
                waypointArrow?.ClearTarget();
                return;
            }

            Transform closest = null;
            float closestDist = Mathf.Infinity;
            Vector3 pos = transform.position;

            foreach (var p in currentPassengers)
            {
                float dist = Vector3.Distance(pos, p.waypoint.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = p.waypoint;
                }
            }

            waypointArrow?.SetTarget(closest);
        }
    }

    private void UpdatePassengerUI()
    {
        if (passengerCountText != null)
        {
            passengerCountText.text = $"Passengers: {currentPassengers.Count}/{maxPassengers}";
        }
    }

    public bool HasPassenger() => currentPassengers.Count > 0;

    public bool CanPickUpPassenger() => currentPassengers.Count < maxPassengers;
}
