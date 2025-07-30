using UnityEngine;

public class PassengerManager : MonoBehaviour
{
    public GameObject passengerPrefab;
    public Transform[] pickupSpawnPoints;
    public Transform[] dropOffWaypoints;
    public int maxPassengerCount = 8;

    private void Start()
    {
        SpawnPassengers();
    }

    void SpawnPassengers()
    {
        // Shuffle pickup spawn points
        ShuffleArray(pickupSpawnPoints);

        for (int i = 0; i < Mathf.Min(maxPassengerCount, pickupSpawnPoints.Length); i++)
        {
            Transform spawnPoint = pickupSpawnPoints[i];
            GameObject passengerObj = Instantiate(passengerPrefab, spawnPoint.position, spawnPoint.rotation);

            PassengerPickup pickup = passengerObj.GetComponent<PassengerPickup>();
            if (pickup != null && dropOffWaypoints.Length > 0)
            {
                pickup.dropOffWaypoint = dropOffWaypoints[Random.Range(0, dropOffWaypoints.Length)];
            }
        }
    }

    // Fisher-Yates shuffle
    void ShuffleArray(Transform[] array)
    {
        for (int i = array.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (array[i], array[j]) = (array[j], array[i]);
        }
    }
}