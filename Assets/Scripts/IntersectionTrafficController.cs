using System.Collections.Generic;
using UnityEngine;

namespace Barmetler.RoadSystem.Traffic
{
    [RequireComponent(typeof(Barmetler.RoadSystem.Intersection))]
    public class IntersectionTrafficController : MonoBehaviour
    {
        [Header("Behavior")]
        [Tooltip("Only allow one car inside the intersection at a time (simple + safe).")]
        public bool oneCarAtATime = true;

        [Tooltip("How far from the intersection center counts as 'exited'.")]
        public float exitPadding = 2f;

        private readonly Queue<TrafficCar> queue = new Queue<TrafficCar>();
        private readonly HashSet<TrafficCar> queued = new HashSet<TrafficCar>();
        private TrafficCar current;

        private Barmetler.RoadSystem.Intersection intersection;

        public Vector3 Center => transform.position;
        public float InnerRadius => intersection ? intersection.Radius : 0f;
        public float ExitRadius => InnerRadius + exitPadding;

        private void Awake()
        {
            intersection = GetComponent<Barmetler.RoadSystem.Intersection>();
        }

        public void Register(TrafficCar car)
        {
            if (!car) return;
            if (queued.Contains(car)) return;
            if (current == car) return;

            queued.Add(car);
            queue.Enqueue(car);
        }

        public bool TryAcquire(TrafficCar car)
        {
            if (!car) return false;

            if (!oneCarAtATime)
                return true;

            if (current != null && current != car)
                return false;

            // Clean up dead entries
            while (queue.Count > 0 && (!queue.Peek() || queue.Peek().IsDestroyedOrDisabled))
            {
                var dead = queue.Dequeue();
                if (dead) queued.Remove(dead);
            }

            // If nobody is reserved yet, only allow head of queue
            if (current == null)
            {
                if (queue.Count == 0)
                {
                    current = car;
                    return true;
                }

                if (queue.Peek() == car)
                {
                    queue.Dequeue();
                    queued.Remove(car);
                    current = car;
                    return true;
                }

                return false;
            }

            return current == car;
        }

        public void Release(TrafficCar car)
        {
            if (!car) return;
            if (current == car) current = null;

            // If the car was queued but left, keep things consistent
            if (queued.Contains(car))
                queued.Remove(car);
        }

        public bool IsOccupiedByOther(TrafficCar car)
        {
            if (!oneCarAtATime) return false;
            return current != null && current != car;
        }
    }
}