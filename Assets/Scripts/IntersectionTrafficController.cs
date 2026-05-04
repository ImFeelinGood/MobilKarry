using System.Collections.Generic;
using UnityEngine;

namespace Barmetler.RoadSystem.Traffic
{
    [RequireComponent(typeof(Intersection))]
    public class IntersectionTrafficController : MonoBehaviour
    {
        [Header("Behavior")]
        [Tooltip("Only one traffic AI may enter this intersection at a time.")]
        [SerializeField] private bool oneCarAtATime = true;

        [Tooltip("Extra distance after the intersection radius before the car is considered fully exited.")]
        [SerializeField] private float exitPadding = 4f;

        private readonly Queue<global::TrafficAIController> queue = new Queue<global::TrafficAIController>();
        private readonly HashSet<global::TrafficAIController> queued = new HashSet<global::TrafficAIController>();

        private global::TrafficAIController current;
        private Intersection intersection;

        public Vector3 Center => transform.position;
        public float InnerRadius => intersection != null ? intersection.Radius : 0f;
        public float ExitRadius => InnerRadius + exitPadding;
        public bool OneCarAtATime => oneCarAtATime;

        private void Awake()
        {
            intersection = GetComponent<Intersection>();
        }

        public void Register(global::TrafficAIController car)
        {
            if (car == null)
                return;

            if (current == car)
                return;

            if (queued.Contains(car))
                return;

            queued.Add(car);
            queue.Enqueue(car);
        }

        public bool TryAcquire(global::TrafficAIController car)
        {
            if (car == null)
                return false;

            if (!oneCarAtATime)
                return true;

            CleanupQueue();

            if (current == car)
                return true;

            if (current != null && current != car)
                return false;

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

        public void Release(global::TrafficAIController car)
        {
            if (car == null)
                return;

            if (current == car)
                current = null;

            if (queued.Contains(car))
                queued.Remove(car);

            CleanupQueue();
        }

        public bool IsOccupiedByOther(global::TrafficAIController car)
        {
            if (!oneCarAtATime)
                return false;

            return current != null && current != car;
        }

        private void CleanupQueue()
        {
            while (queue.Count > 0)
            {
                global::TrafficAIController car = queue.Peek();

                if (car == null || car.IsDestroyedOrDisabled)
                {
                    queue.Dequeue();

                    if (car != null)
                        queued.Remove(car);

                    continue;
                }

                break;
            }
        }
    }
}