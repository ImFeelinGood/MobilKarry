using System.Collections.Generic;
using UnityEngine;
using Barmetler.RoadSystem;

namespace Barmetler.RoadSystem.Traffic
{
    public class TrafficSpawner : MonoBehaviour
    {
        public RoadSystem roadSystem;
        public TrafficCar carPrefab;

        public int spawnCount = 15;

        [Tooltip("How far from an anchor to spawn (avoid spawning inside intersection).")]
        public float spawnForwardOffset = 6f;

        [Tooltip("Minimum distance between spawn points.")]
        public float minSpawnSeparation = 6f;

        private readonly List<Vector3> spawnPoints = new List<Vector3>();

        private void Start()
        {
            if (!roadSystem) roadSystem = FindFirstObjectByType<RoadSystem>();
            if (!roadSystem || !carPrefab) return;

            roadSystem.ConstructGraph();

            BuildSpawnPoints();
            SpawnCars();
        }

        private void BuildSpawnPoints()
        {
            spawnPoints.Clear();

            foreach (var road in roadSystem.Roads)
            {
                if (!road || !road.start || !road.end) continue;

                // spawn near start anchor going into the road
                spawnPoints.Add(road.start.transform.position + road.start.transform.forward * spawnForwardOffset);

                // spawn near end anchor going into the road (anchor forward points into its road segment)
                spawnPoints.Add(road.end.transform.position + road.end.transform.forward * spawnForwardOffset);
            }
        }

        private void SpawnCars()
        {
            var used = new List<Vector3>();

            for (int i = 0; i < spawnCount; i++)
            {
                if (spawnPoints.Count == 0) break;

                Vector3 pos = spawnPoints[Random.Range(0, spawnPoints.Count)];

                bool ok = true;
                foreach (var u in used)
                {
                    if ((u - pos).sqrMagnitude < minSpawnSeparation * minSpawnSeparation)
                    {
                        ok = false;
                        break;
                    }
                }
                if (!ok) continue;

                used.Add(pos);

                var car = Instantiate(carPrefab, pos, Quaternion.identity);
                car.roadSystem = roadSystem;

                // optional: randomize speed a bit
                car.speedLimit *= Random.Range(0.85f, 1.15f);
            }
        }
    }
}