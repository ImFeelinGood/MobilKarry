using UnityEngine;

public class PassengerPickup : MonoBehaviour
{
    [Header("Passenger Info")]
    public Transform dropOffWaypoint;
    public int rewardAmount;

    [Header("Lifetime")]
    public float lifetime = 60f;
    private float lifeTimer;

    [Header("Passenger Appearance")]
    public GameObject[] passengerModels;
    private GameObject activeModel;

    [Header("Look At Player")]
    [SerializeField] private float rotationSpeed = 5f;
    [SerializeField] private bool lockVerticalRotation = true;
    [SerializeField] private bool reverseDirection = false;

    private Transform player;
    private bool pickedUp;

    [HideInInspector]
    public PassengerManager manager;

    private void Start()
    {
        FindPlayer();
        SpawnPassengerModel();
    }

    private void Update()
    {
        if (pickedUp)
            return;

        lifeTimer += Time.deltaTime;

        if (lifeTimer >= lifetime)
        {
            Destroy(gameObject);
            return;
        }

        RotatePassengerTowardsPlayer();
    }

    private void FindPlayer()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");

        if (playerObject != null)
        {
            player = playerObject.transform;
        }
        else
        {
            Debug.LogWarning("PassengerPickup could not find an object with the Player tag.");
        }
    }

    private void SpawnPassengerModel()
    {
        if (passengerModels == null || passengerModels.Length == 0)
            return;

        int randomIndex = Random.Range(0, passengerModels.Length);

        Vector3 spawnPosition = transform.position + Vector3.up;

        activeModel = Instantiate(
            passengerModels[randomIndex],
            spawnPosition,
            transform.rotation,
            transform
        );
    }

    private void RotatePassengerTowardsPlayer()
    {
        if (player == null || activeModel == null)
            return;

        Vector3 direction = player.position - activeModel.transform.position;

        if (lockVerticalRotation)
            direction.y = 0f;

        if (direction.sqrMagnitude <= 0.001f)
            return;

        if (reverseDirection)
            direction = -direction;

        Quaternion targetRotation = Quaternion.LookRotation(direction);

        activeModel.transform.rotation = Quaternion.Slerp(
            activeModel.transform.rotation,
            targetRotation,
            rotationSpeed * Time.deltaTime
        );
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        var carController =
            other.GetComponent<Ezereal.EzerealCarController>();

        var passengerSystem =
            other.GetComponent<PlayerPassengerSystem>();

        if (
            passengerSystem != null &&
            carController != null &&
            passengerSystem.HasPassenger()
        )
        {
            if (!carController.stationary)
            {
                Debug.Log("Stop to pick up passenger!");
            }
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (pickedUp || !other.CompareTag("Player"))
            return;

        var passengerSystem =
            other.GetComponent<PlayerPassengerSystem>();

        var carController =
            other.GetComponent<Ezereal.EzerealCarController>();

        if (
            passengerSystem == null ||
            carController == null ||
            !carController.stationary
        )
        {
            return;
        }

        if (passengerSystem.CanPickUpPassenger())
        {
            passengerSystem.PickUpPassenger(this);

            if (activeModel != null)
                Destroy(activeModel);

            pickedUp = true;

            Collider passengerCollider = GetComponent<Collider>();

            if (passengerCollider != null)
                passengerCollider.enabled = false;

            Debug.Log("Passenger picked up.");

            Destroy(gameObject);
        }
        else
        {
            Debug.Log("Cannot pick up more passengers.");
        }
    }

    private void OnDestroy()
    {
        if (manager != null)
        {
            manager.NotifyPassengerRemoved(gameObject);
        }
    }

    public Transform GetWaypoint()
    {
        return dropOffWaypoint;
    }

    public int GetReward()
    {
        return rewardAmount;
    }
}