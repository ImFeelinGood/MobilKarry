using UnityEngine;

namespace Barmetler.RoadSystem.Traffic
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TrafficCarOld))]
    public class TrafficCarPlayerProximity : MonoBehaviour
    {
        public enum PlayerDetectionState
        {
            Clear,
            Slow,
            Stop
        }

        [Header("Traffic Car")]
        [SerializeField] private TrafficCarOld trafficCar;

        [Header("Player Detection Zones")]
        [Tooltip("The larger trigger box. The traffic car slows down while the player is inside this zone.")]
        [SerializeField] private BoxCollider slowZone;

        [Tooltip("The smaller trigger box. The traffic car stops while the player is inside this zone.")]
        [SerializeField] private BoxCollider stopZone;

        [Tooltip("Use a separate layer that only contains the player car.")]
        [SerializeField] private LayerMask playerCarMask;

        [Header("Speed")]
        [Tooltip("Speed used when the player is inside the slow zone. This value is in metres per second.")]
        [Min(0f)]
        [SerializeField] private float playerSlowSpeed = 3f;

        [Tooltip("Restore the traffic car's original speed limit when the player leaves both zones.")]
        [SerializeField] private bool restoreOriginalSpeed = true;

        [Header("Runtime Debug")]
        [SerializeField] private PlayerDetectionState currentState = PlayerDetectionState.Clear;
        [SerializeField] private bool playerInsideSlowZone;
        [SerializeField] private bool playerInsideStopZone;

        private readonly Collider[] overlapResults = new Collider[16];
        private float originalSpeedLimit;
        private bool speedWasOverridden;

        public PlayerDetectionState CurrentState => currentState;

        private void Reset()
        {
            trafficCar = GetComponent<TrafficCarOld>();
        }

        private void Awake()
        {
            if (!trafficCar)
                trafficCar = GetComponent<TrafficCarOld>();

            if (trafficCar)
                originalSpeedLimit = trafficCar.speedLimit;

            SetZonesAsTriggers();
        }

        private void OnEnable()
        {
            if (!trafficCar)
                trafficCar = GetComponent<TrafficCarOld>();

            if (trafficCar)
                originalSpeedLimit = trafficCar.speedLimit;
        }

        private void FixedUpdate()
        {
            if (!trafficCar)
                return;

            playerInsideStopZone = IsPlayerInsideZone(stopZone);
            playerInsideSlowZone = !playerInsideStopZone && IsPlayerInsideZone(slowZone);

            if (playerInsideStopZone)
            {
                currentState = PlayerDetectionState.Stop;
                SetSpeedLimit(0f);
                return;
            }

            if (playerInsideSlowZone)
            {
                currentState = PlayerDetectionState.Slow;
                SetSpeedLimit(Mathf.Min(originalSpeedLimit, playerSlowSpeed));
                return;
            }

            currentState = PlayerDetectionState.Clear;

            if (restoreOriginalSpeed && speedWasOverridden)
            {
                trafficCar.speedLimit = originalSpeedLimit;
                speedWasOverridden = false;
            }
        }

        private bool IsPlayerInsideZone(BoxCollider zone)
        {
            if (!zone || !zone.enabled || playerCarMask.value == 0)
                return false;

            Vector3 worldCenter = zone.transform.TransformPoint(zone.center);
            Vector3 scaledSize = Vector3.Scale(zone.size, Abs(zone.transform.lossyScale));
            Vector3 halfExtents = scaledSize * 0.5f;

            int hitCount = Physics.OverlapBoxNonAlloc(
                worldCenter,
                halfExtents,
                overlapResults,
                zone.transform.rotation,
                playerCarMask,
                QueryTriggerInteraction.Collide
            );

            for (int i = 0; i < hitCount; i++)
            {
                Collider detectedCollider = overlapResults[i];
                overlapResults[i] = null;

                if (!detectedCollider)
                    continue;

                if (detectedCollider.transform.IsChildOf(transform))
                    continue;

                if (trafficCar.vehicleRB && detectedCollider.attachedRigidbody == trafficCar.vehicleRB)
                    continue;

                return true;
            }

            return false;
        }

        private void SetSpeedLimit(float value)
        {
            trafficCar.speedLimit = Mathf.Max(0f, value);
            speedWasOverridden = true;
        }

        private void OnDisable()
        {
            RestoreOriginalSpeedLimit();
        }

        private void OnDestroy()
        {
            RestoreOriginalSpeedLimit();
        }

        private void RestoreOriginalSpeedLimit()
        {
            if (!trafficCar || !restoreOriginalSpeed || !speedWasOverridden)
                return;

            trafficCar.speedLimit = originalSpeedLimit;
            speedWasOverridden = false;
        }

        private void OnValidate()
        {
            if (!trafficCar)
                trafficCar = GetComponent<TrafficCarOld>();

            playerSlowSpeed = Mathf.Max(0f, playerSlowSpeed);
            SetZonesAsTriggers();
        }

        private void SetZonesAsTriggers()
        {
            if (slowZone)
                slowZone.isTrigger = true;

            if (stopZone)
                stopZone.isTrigger = true;
        }

        private static Vector3 Abs(Vector3 value)
        {
            return new Vector3(
                Mathf.Abs(value.x),
                Mathf.Abs(value.y),
                Mathf.Abs(value.z)
            );
        }
    }
}
