using System.Collections.Generic;
using UnityEngine;
using Barmetler.RoadSystem;

namespace Barmetler.RoadSystem.Traffic
{
    /// <summary>
    /// Spawns and despawns TrafficCarOld instances based on the player's position.
    ///
    /// Behavior:
    /// - Keeps cars around the player within playerRadius.
    /// - Does not spawn more than maxCarsInsideRadius cars inside the radius.
    /// - If the number of cars inside the radius drops below minCarsBeforeRespawn,
    ///   it spawns new cars until targetCarsAfterRespawn is reached.
    /// - Cars that stay outside playerRadius for outOfRangeDespawnDelay seconds are despawned.
    /// </summary>
    public class TrafficCarSpawnManager : MonoBehaviour
    {
        [Header("References")]
        public Transform player;
        public string playerTag = "Player";
        public RoadSystem roadSystem;
        public GameObject[] carPrefabs;
        public Transform spawnedCarParent;

        [Header("Population")]
        [Min(1f)] public float playerRadius = 120f;
        [Min(0)] public int maxCarsInsideRadius = 10;
        [Min(0)] public int minCarsBeforeRespawn = 7;
        [Min(0)] public int targetCarsAfterRespawn = 10;
        [Min(0.1f)] public float checkInterval = 0.5f;
        [Min(0f)] public float outOfRangeDespawnDelay = 5f;

        [Header("Spawn Position")]
        [Tooltip("Cars will not spawn too close to the player. This prevents cars appearing directly in front of the camera/player.")]
        [Min(0f)] public float minSpawnDistanceFromPlayer = 35f;

        [Tooltip("Distance between sampled points on roads. Smaller value = more accurate spawn choices, but heavier.")]
        [Min(0.25f)] public float roadSampleStep = 4f;

        [Tooltip("Extra Y offset so the car does not spawn slightly under the road.")]
        public float spawnHeightOffset = 0.25f;

        [Tooltip("Minimum distance from other spawned cars when selecting a spawn point.")]
        [Min(0f)] public float minDistanceFromOtherCars = 8f;

        [Tooltip("Number of attempts to find a valid road spawn point per car.")]
        [Min(1)] public int spawnAttemptsPerCar = 40;

        [Header("Lane / Direction")]
        [Tooltip("Used if the prefab does not have TrafficCarOld attached.")]
        public float defaultLaneOffset = 1.75f;

        [Tooltip("Used if the prefab does not have TrafficCarOld attached.")]
        public TrafficCarOld.DriveSide defaultDriveSide = TrafficCarOld.DriveSide.LeftHandTraffic;

        [Tooltip("If true, spawned cars may face either road direction. If false, cars follow the road's stored forward direction.")]
        public bool allowBothRoadDirections = true;

        [Tooltip("Pass the chosen spawn forward direction into TrafficCarOld so its first path does not go backward.")]
        public bool initializeTrafficCarSpawnDirection = true;

        [Tooltip("Pass the chosen lane side into TrafficCarOld so it does not auto-pick the opposite lane after spawning.")]
        public bool initializeTrafficCarLaneSide = true;

        [Header("Spawn Blocking")]
        [Tooltip("Optional. If set, the spawner will avoid spawning cars where this sphere overlaps the selected layers.")]
        public LayerMask spawnBlockMask;
        [Min(0.1f)] public float spawnBlockRadius = 2f;

        [Header("Startup")]
        public bool constructRoadGraphOnStart = true;

        [Tooltip("If true, existing TrafficCarOld objects already in the scene will be managed by this spawner.")]
        public bool registerExistingCarsOnStart = false;

        [Header("Debug Gizmos")]
        public bool drawGizmos = true;
        public Color playerRadiusColor = new Color(0f, 0.7f, 1f, 0.85f);
        public Color minimumSpawnRadiusColor = new Color(1f, 0.8f, 0f, 0.85f);

        private readonly List<ManagedCar> managedCars = new();
        private float nextCheckTime;

        private class ManagedCar
        {
            public GameObject gameObject;
            public TrafficCarOld trafficCar;
            public float outOfRangeTimer;
        }

        private void Awake()
        {
            AutoFindReferences();
        }

        private void Start()
        {
            if (roadSystem && constructRoadGraphOnStart)
                roadSystem.ConstructGraph();

            if (registerExistingCarsOnStart)
                RegisterExistingSceneCars();

            ForcePopulationCheck();
        }

        private void Update()
        {
            if (Time.time < nextCheckTime)
                return;

            nextCheckTime = Time.time + checkInterval;
            ForcePopulationCheck();
        }

        /// <summary>
        /// Manually runs the despawn and spawn checks.
        /// Useful if you want to call it after changing radius/settings at runtime.
        /// </summary>
        public void ForcePopulationCheck()
        {
            if (!IsReady())
                return;

            CleanupNullCars();
            UpdateOutOfRangeTimers();
            SpawnIfNeeded();
        }

        /// <summary>
        /// Finds common scene references automatically if they are not assigned in the Inspector.
        /// </summary>
        private void AutoFindReferences()
        {
            if (!player && !string.IsNullOrEmpty(playerTag))
            {
                GameObject playerObject = GameObject.FindGameObjectWithTag(playerTag);
                if (playerObject)
                    player = playerObject.transform;
            }

            if (!roadSystem)
                roadSystem = FindFirstObjectByType<RoadSystem>();
        }

        /// <summary>
        /// Registers cars that already exist in the scene so the manager can despawn them too.
        /// </summary>
        private void RegisterExistingSceneCars()
        {
            TrafficCarOld[] existingCars = FindObjectsByType<TrafficCarOld>(FindObjectsSortMode.None);

            foreach (TrafficCarOld car in existingCars)
            {
                if (!car || IsAlreadyManaged(car.gameObject))
                    continue;

                if (roadSystem && !car.roadSystem)
                    car.roadSystem = roadSystem;

                managedCars.Add(new ManagedCar
                {
                    gameObject = car.gameObject,
                    trafficCar = car,
                    outOfRangeTimer = 0f
                });
            }
        }

        /// <summary>
        /// Removes destroyed/null cars from the internal list.
        /// </summary>
        private void CleanupNullCars()
        {
            for (int i = managedCars.Count - 1; i >= 0; i--)
            {
                if (managedCars[i] == null || !managedCars[i].gameObject)
                    managedCars.RemoveAt(i);
            }
        }

        /// <summary>
        /// Despawns cars that have stayed outside the player radius for the configured delay.
        /// </summary>
        private void UpdateOutOfRangeTimers()
        {
            for (int i = managedCars.Count - 1; i >= 0; i--)
            {
                ManagedCar car = managedCars[i];

                if (!car.gameObject)
                {
                    managedCars.RemoveAt(i);
                    continue;
                }

                bool insideRadius = IsInsidePlayerRadius(car.gameObject.transform.position);

                if (insideRadius)
                {
                    car.outOfRangeTimer = 0f;
                    continue;
                }

                car.outOfRangeTimer += checkInterval;

                if (car.outOfRangeTimer >= outOfRangeDespawnDelay)
                {
                    Destroy(car.gameObject);
                    managedCars.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Spawns new cars if the number of managed cars inside the radius is below the minimum threshold.
        /// </summary>
        private void SpawnIfNeeded()
        {
            int insideCount = CountCarsInsidePlayerRadius();

            if (insideCount >= minCarsBeforeRespawn)
                return;

            int targetCount = Mathf.Clamp(targetCarsAfterRespawn, minCarsBeforeRespawn, maxCarsInsideRadius);
            int carsToSpawn = Mathf.Max(0, targetCount - insideCount);

            for (int i = 0; i < carsToSpawn; i++)
            {
                insideCount = CountCarsInsidePlayerRadius();

                if (insideCount >= maxCarsInsideRadius)
                    break;

                if (!TrySpawnCar())
                    break;
            }
        }

        /// <summary>
        /// Attempts to spawn one car on a valid road point around the player.
        /// </summary>
        private bool TrySpawnCar()
        {
            GameObject prefab = GetRandomPrefab();
            if (!prefab)
                return false;

            if (!TryGetRoadSpawnPose(prefab, out Vector3 spawnPosition, out Quaternion spawnRotation, out Vector3 spawnForward, out int spawnLaneSign))
                return false;

            GameObject spawned = Instantiate(prefab, spawnPosition, spawnRotation, spawnedCarParent);
            TrafficCarOld trafficCar = spawned.GetComponentInChildren<TrafficCarOld>();

            if (trafficCar)
            {
                if (roadSystem)
                    trafficCar.roadSystem = roadSystem;

                if (initializeTrafficCarSpawnDirection || initializeTrafficCarLaneSide)
                {
                    Vector3 preferredForward = initializeTrafficCarSpawnDirection ? spawnForward : Vector3.zero;
                    int preferredLaneSign = initializeTrafficCarLaneSide ? spawnLaneSign : 0;
                    trafficCar.InitializeFromSpawner(preferredForward, preferredLaneSign);
                }
            }

            managedCars.Add(new ManagedCar
            {
                gameObject = spawned,
                trafficCar = trafficCar,
                outOfRangeTimer = 0f
            });

            return true;
        }

        /// <summary>
        /// Picks a random prefab from the assigned carPrefabs array.
        /// </summary>
        private GameObject GetRandomPrefab()
        {
            if (carPrefabs == null || carPrefabs.Length == 0)
                return null;

            for (int i = 0; i < 20; i++)
            {
                GameObject prefab = carPrefabs[Random.Range(0, carPrefabs.Length)];
                if (prefab)
                    return prefab;
            }

            return null;
        }

        /// <summary>
        /// Finds a road point inside the player radius, not too close to the player, and not too close to other cars.
        /// </summary>
        private bool TryGetRoadSpawnPose(GameObject prefab, out Vector3 spawnPosition, out Quaternion spawnRotation, out Vector3 spawnForward, out int spawnLaneSign)
        {
            spawnPosition = Vector3.zero;
            spawnRotation = Quaternion.identity;
            spawnForward = Vector3.forward;
            spawnLaneSign = 0;

            Road[] roads = roadSystem ? roadSystem.Roads : null;
            if (roads == null || roads.Length == 0)
                return false;

            for (int attempt = 0; attempt < spawnAttemptsPerCar; attempt++)
            {
                Road road = roads[Random.Range(0, roads.Length)];
                if (!road)
                    continue;

                Bezier.OrientedPoint[] roadPoints = road.GetEvenlySpacedPoints(Mathf.Max(0.25f, roadSampleStep));
                if (roadPoints == null || roadPoints.Length == 0)
                    continue;

                Bezier.OrientedPoint point = roadPoints[Random.Range(0, roadPoints.Length)].ToWorldSpace(road.transform);

                Vector3 forward = ProjectXZ(point.forward).normalized;
                if (forward.sqrMagnitude < 0.0001f)
                    forward = road.transform.forward;

                forward = ProjectXZ(forward).normalized;
                if (forward.sqrMagnitude < 0.0001f)
                    forward = Vector3.forward;

                if (allowBothRoadDirections && Random.value < 0.5f)
                    forward = -forward;

                Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
                float laneOffset = GetPrefabLaneOffset(prefab);
                int laneSign = GetPrefabLaneSign(prefab);

                Vector3 candidate = point.position + right * laneOffset * laneSign;
                candidate.y = point.position.y + spawnHeightOffset;

                if (!IsInsideSpawnRing(candidate))
                    continue;

                if (!IsFarEnoughFromManagedCars(candidate))
                    continue;

                if (IsBlocked(candidate))
                    continue;

                spawnPosition = candidate;
                spawnRotation = Quaternion.LookRotation(forward, Vector3.up);
                spawnForward = forward;
                spawnLaneSign = laneSign;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Reads lane offset from prefab TrafficCarOld if available.
        /// </summary>
        private float GetPrefabLaneOffset(GameObject prefab)
        {
            TrafficCarOld car = prefab ? prefab.GetComponentInChildren<TrafficCarOld>() : null;
            return car ? Mathf.Abs(car.laneOffset) : Mathf.Abs(defaultLaneOffset);
        }

        /// <summary>
        /// Reads drive side from prefab TrafficCarOld if available.
        /// </summary>
        private int GetPrefabLaneSign(GameObject prefab)
        {
            TrafficCarOld car = prefab ? prefab.GetComponentInChildren<TrafficCarOld>() : null;
            TrafficCarOld.DriveSide driveSide = car ? car.driveSide : defaultDriveSide;

            return driveSide == TrafficCarOld.DriveSide.RightHandTraffic ? 1 : -1;
        }

        /// <summary>
        /// Checks whether a candidate point is within the player radius but not too close to the player.
        /// </summary>
        private bool IsInsideSpawnRing(Vector3 position)
        {
            Vector3 playerPos = ProjectXZ(player.position);
            Vector3 pos = ProjectXZ(position);
            float distance = Vector3.Distance(playerPos, pos);

            return distance <= playerRadius && distance >= minSpawnDistanceFromPlayer;
        }

        /// <summary>
        /// Checks whether a position is inside the player radius.
        /// </summary>
        private bool IsInsidePlayerRadius(Vector3 position)
        {
            return Vector3.Distance(ProjectXZ(player.position), ProjectXZ(position)) <= playerRadius;
        }

        /// <summary>
        /// Prevents spawning too close to cars already managed by this spawner.
        /// </summary>
        private bool IsFarEnoughFromManagedCars(Vector3 position)
        {
            if (minDistanceFromOtherCars <= 0f)
                return true;

            float minDistanceSqr = minDistanceFromOtherCars * minDistanceFromOtherCars;
            Vector3 candidate = ProjectXZ(position);

            foreach (ManagedCar car in managedCars)
            {
                if (car == null || !car.gameObject)
                    continue;

                float distanceSqr = (ProjectXZ(car.gameObject.transform.position) - candidate).sqrMagnitude;
                if (distanceSqr < minDistanceSqr)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Optional physics check to prevent spawning inside obstacles or other vehicles.
        /// </summary>
        private bool IsBlocked(Vector3 position)
        {
            if (spawnBlockMask.value == 0)
                return false;

            return Physics.CheckSphere(position, spawnBlockRadius, spawnBlockMask, QueryTriggerInteraction.Ignore);
        }

        /// <summary>
        /// Counts managed cars currently inside the player radius.
        /// </summary>
        private int CountCarsInsidePlayerRadius()
        {
            int count = 0;

            foreach (ManagedCar car in managedCars)
            {
                if (car == null || !car.gameObject)
                    continue;

                if (IsInsidePlayerRadius(car.gameObject.transform.position))
                    count++;
            }

            return count;
        }

        /// <summary>
        /// Checks if a car is already managed by this spawner.
        /// </summary>
        private bool IsAlreadyManaged(GameObject carObject)
        {
            foreach (ManagedCar car in managedCars)
            {
                if (car != null && car.gameObject == carObject)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Basic validation before running spawn/despawn logic.
        /// </summary>
        private bool IsReady()
        {
            if (!player)
            {
                AutoFindReferences();
                if (!player) return false;
            }

            if (!roadSystem)
            {
                AutoFindReferences();
                if (!roadSystem) return false;
            }

            if (carPrefabs == null || carPrefabs.Length == 0)
                return false;

            maxCarsInsideRadius = Mathf.Max(0, maxCarsInsideRadius);
            minCarsBeforeRespawn = Mathf.Clamp(minCarsBeforeRespawn, 0, maxCarsInsideRadius);
            targetCarsAfterRespawn = Mathf.Clamp(targetCarsAfterRespawn, minCarsBeforeRespawn, maxCarsInsideRadius);
            playerRadius = Mathf.Max(1f, playerRadius);
            minSpawnDistanceFromPlayer = Mathf.Clamp(minSpawnDistanceFromPlayer, 0f, playerRadius);

            return true;
        }

        private static Vector3 ProjectXZ(Vector3 value)
        {
            return new Vector3(value.x, 0f, value.z);
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos)
                return;

            Transform center = player ? player : transform;

            Gizmos.color = playerRadiusColor;
            DrawWireCircleXZ(center.position, playerRadius, 64);

            Gizmos.color = minimumSpawnRadiusColor;
            DrawWireCircleXZ(center.position, minSpawnDistanceFromPlayer, 48);
        }

        private static void DrawWireCircleXZ(Vector3 center, float radius, int segments)
        {
            if (radius <= 0f)
                return;

            segments = Mathf.Max(8, segments);
            Vector3 previous = center + new Vector3(radius, 0f, 0f);

            for (int i = 1; i <= segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                Vector3 next = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }
    }
}
