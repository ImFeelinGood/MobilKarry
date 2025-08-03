using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PassengerManager : MonoBehaviour
{
    private List<GameObject> activePassengers = new List<GameObject>();
    public GameObject passengerPrefab;
    public Transform[] pickupSpawnPoints;
    public Transform[] dropOffWaypoints;
    public int maxPassengerCount = 8;
    public int minFare = 5000;
    public int maxFare = 20000;

    private void Start()
    {
        SpawnPassengers();
    }

    void SpawnPassengers()
    {
        ShuffleArray(pickupSpawnPoints);

        for (int i = 0; i < pickupSpawnPoints.Length && activePassengers.Count < maxPassengerCount; i++)
        {
            Transform spawnPoint = pickupSpawnPoints[i];

            GameObject passengerObj = Instantiate(passengerPrefab, spawnPoint.position, spawnPoint.rotation);
            activePassengers.Add(passengerObj);

            PassengerPickup pickup = passengerObj.GetComponent<PassengerPickup>();
            if (pickup != null && dropOffWaypoints.Length > 0)
            {
                pickup.dropOffWaypoint = dropOffWaypoints[Random.Range(0, dropOffWaypoints.Length)];
                pickup.rewardAmount = Random.Range(5000, 20001);
                pickup.manager = this; // so it can notify when despawned
            }
        }
    }

    void ShuffleArray(Transform[] array)
    {
        for (int i = array.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (array[i], array[j]) = (array[j], array[i]);
        }
    }

    public void NotifyPassengerRemoved(GameObject passenger)
    {
        activePassengers.Remove(passenger);

        // Optional: delay before respawning
        StartCoroutine(RespawnPassengerAfterDelay());
    }

    private IEnumerator RespawnPassengerAfterDelay()
    {
        yield return new WaitForSeconds(3f); // delay before respawn
        SpawnPassengers();
    }
}