using System.Collections.Generic;
using UnityEngine;
using Barmetler.RoadSystem;

namespace Barmetler.RoadSystem.Traffic
{
    [RequireComponent(typeof(Rigidbody))]
    public class TrafficCarOld : MonoBehaviour
    {
        public enum DriveSide { RightHandTraffic, LeftHandTraffic }
        public enum DriveTypes { RWD, FWD, AWD }

        [Header("Road System")]
        public RoadSystem roadSystem;

        [Header("WheelCollider Rig")]
        public Rigidbody vehicleRB;
        public WheelCollider frontLeftWheelCollider;
        public WheelCollider frontRightWheelCollider;
        public WheelCollider rearLeftWheelCollider;
        public WheelCollider rearRightWheelCollider;

        [Header("Wheel Meshes (Optional)")]
        public Transform frontLeftWheelMesh;
        public Transform frontRightWheelMesh;
        public Transform rearLeftWheelMesh;
        public Transform rearRightWheelMesh;

        [Header("Vehicle Stability")]
        public Transform centerOfMass;
        public float angularDrag = 1f;

        [Header("Lane")]
        public DriveSide driveSide = DriveSide.LeftHandTraffic;
        public float laneOffset = 1.75f;
        public bool chooseNearestLaneOnSpawn = true;
        public float laneChooseDeadzone = 0.35f;
        public int laneChooseScanPoints = 100;

        [Header("Path")]
        public float pathStepSize = 1f;
        public float minDistanceToRoadToConnect = 10f;
        public float minDistanceYScale = 1f;
        public float waypointReachDistance = 2f;
        public float lookAheadDistance = 12f;
        public float goalReachedDistance = 5f;
        public float repathIfOffPathDistance = 10f;
        public float repathCooldown = 1f;

        [Header("Speed")]
        public bool isStarted = true;
        public float speedLimit = 10f; // m/s
        public float minCreepSpeed = 1f;
        public float maxForwardSpeed = 130f; // km/h
        public float horsePower = 550f;
        public float brakePower = 2000f;
        public float maxSteerAngle = 40f;
        public float steeringSpeed = 5f;
        public DriveTypes driveType = DriveTypes.RWD;

        [Header("Vehicle Detection")]
        public LayerMask vehicleMask;
        public float detectDistance = 14f;
        public float detectRadius = 0.9f;
        public float minGap = 3f;
        public float slowDownGap = 10f;

        [Header("Intersection")]
        public bool slowDownNearIntersection = true;
        public bool useIntersectionRightOfWay = true;
        public float intersectionDetectPadding = 1f;
        public float intersectionRequestDistance = 12f;
        public float intersectionClearBuffer = 2f;
        [Range(0.1f, 1f)] public float intersectionMinSpeedFactor = 0.45f;
        public float brakeDecel = 10f;

        private readonly List<Bezier.OrientedPoint> path = new();
        private int pathIndex;
        private int laneSideSign;
        private Vector3 goalWorld;
        private float repathTimer;

        private float throttle01;
        private float brake01;
        private float targetSteerAngle;
        private float currentSteerAngle;
        private float currentSpeedKmh;

        private Intersection requestedIntersection;
        private bool wasInsideRequestedIntersection;

        private void Awake()
        {
            if (!vehicleRB) vehicleRB = GetComponent<Rigidbody>();
            vehicleRB.interpolation = RigidbodyInterpolation.Interpolate;
            vehicleRB.angularDamping = angularDrag;

            if (centerOfMass)
                vehicleRB.centerOfMass = transform.InverseTransformPoint(centerOfMass.position);

            if (!roadSystem)
                roadSystem = FindFirstObjectByType<RoadSystem>();
        }

        private void Start()
        {
            if (!roadSystem)
            {
                enabled = false;
                return;
            }

            roadSystem.ConstructGraph();
            PickNewGoalAndRepath();
            AutoChooseLaneSideFromSpawn();
        }

        private void OnDisable()
        {
            ReleaseIntersectionRequest();
        }

        private void FixedUpdate()
        {
            if (!roadSystem || path.Count < 2) return;

            currentSpeedKmh = Vector3.Dot(transform.forward, vehicleRB.linearVelocity) * 3.6f;
            repathTimer -= Time.fixedDeltaTime;

            UpdatePathIndex();
            UpdateIntersectionRelease();

            if (NeedNewGoal())
                PickNewGoalAndRepath();
            else if (repathTimer <= 0f && DistanceToLanePointXZ(pathIndex) > repathIfOffPathDistance)
                Repath(goalWorld);

            if (path.Count < 2) return;

            float desiredSpeed = speedLimit;
            desiredSpeed = Mathf.Min(desiredSpeed, GetSpeedLimitFromTraffic());
            desiredSpeed = Mathf.Min(desiredSpeed, GetSpeedLimitFromIntersectionSlowdown());
            desiredSpeed = Mathf.Min(desiredSpeed, GetSpeedLimitFromIntersectionRightOfWay());

            Vector3 lookPoint = GetLookAheadPoint();
            targetSteerAngle = ComputeTargetSteerAngle(lookPoint);

            ComputeThrottleBrake(desiredSpeed, out throttle01, out brake01);

            ApplyMotor();
            ApplyBrakes();
            ApplySteering();
            UpdateWheelVisuals();
        }

        private bool NeedNewGoal()
        {
            if (path.Count < 2) return true;
            if (pathIndex < path.Count - 3) return false;
            return Vector3.Distance(ProjectXZ(vehicleRB.position), ProjectXZ(goalWorld)) <= goalReachedDistance;
        }

        private void PickNewGoalAndRepath()
        {
            if (roadSystem.Intersections == null || roadSystem.Intersections.Length == 0) return;

            for (int i = 0; i < 20; i++)
            {
                Intersection intersection = roadSystem.Intersections[Random.Range(0, roadSystem.Intersections.Length)];
                if (!intersection || intersection.AnchorPoints == null || intersection.AnchorPoints.Length == 0) continue;

                RoadAnchor anchor = intersection.AnchorPoints[Random.Range(0, intersection.AnchorPoints.Length)];
                if (!anchor) continue;

                goalWorld = anchor.transform.position;
                Repath(goalWorld);

                if (path.Count >= 2) return;
            }
        }

        private void Repath(Vector3 goal)
        {
            repathTimer = repathCooldown;
            path.Clear();
            pathIndex = 0;

            List<Bezier.OrientedPoint> newPath = roadSystem.FindPath(
                vehicleRB.position,
                goal,
                yScale: minDistanceYScale,
                stepSize: Mathf.Max(0.1f, pathStepSize),
                minDstToRoadToConnect: minDistanceToRoadToConnect
            );

            if (newPath == null || newPath.Count < 2)
            {
                roadSystem.ConstructGraph();
                newPath = roadSystem.FindPath(
                    vehicleRB.position,
                    goal,
                    yScale: minDistanceYScale,
                    stepSize: Mathf.Max(0.1f, pathStepSize),
                    minDstToRoadToConnect: minDistanceToRoadToConnect
                );
            }

            if (newPath != null)
                path.AddRange(newPath);
        }

        private void UpdatePathIndex()
        {
            int start = Mathf.Max(0, pathIndex - 1);
            int end = Mathf.Min(path.Count - 1, pathIndex + 10);

            Vector3 carPos = ProjectXZ(vehicleRB.position);
            Vector3 forward = ProjectXZ(transform.forward).normalized;

            int bestIndex = pathIndex;
            float bestDistance = float.PositiveInfinity;

            for (int i = start; i <= end; i++)
            {
                Vector3 point = ProjectXZ(LanePos(i));
                Vector3 toPoint = point - carPos;

                if (toPoint.sqrMagnitude > 0.01f && Vector3.Dot(forward, toPoint.normalized) < -0.15f)
                    continue;

                float distance = toPoint.sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = i;
                }
            }

            pathIndex = Mathf.Clamp(bestIndex, pathIndex, Mathf.Min(path.Count - 1, pathIndex + 3));

            while (pathIndex < path.Count - 1)
            {
                float distance = Vector3.Distance(ProjectXZ(vehicleRB.position), ProjectXZ(LanePos(pathIndex)));
                if (distance > waypointReachDistance) break;
                pathIndex++;
            }
        }

        private Vector3 GetLookAheadPoint()
        {
            float distance = 0f;
            Vector3 previous = vehicleRB.position;

            for (int i = pathIndex; i < path.Count; i++)
            {
                Vector3 point = LanePos(i);
                distance += Vector3.Distance(ProjectXZ(previous), ProjectXZ(point));
                previous = point;

                if (distance >= lookAheadDistance || i == path.Count - 1)
                    return point;
            }

            return LanePos(path.Count - 1);
        }

        private Vector3 LanePos(int index)
        {
            index = Mathf.Clamp(index, 0, path.Count - 1);

            float sign = laneSideSign != 0
                ? laneSideSign
                : (driveSide == DriveSide.RightHandTraffic ? 1f : -1f);

            return path[index].position + GetLaneRight(index) * (Mathf.Abs(laneOffset) * sign);
        }

        private Vector3 GetLaneRight(int index)
        {
            index = Mathf.Clamp(index, 0, path.Count - 1);

            Vector3 direction = Vector3.zero;

            if (index > 0)
                direction += ProjectXZ(path[index].position - path[index - 1].position).normalized;

            if (index < path.Count - 1)
                direction += ProjectXZ(path[index + 1].position - path[index].position).normalized;

            if (direction.sqrMagnitude < 0.0001f)
                direction = ProjectXZ(transform.forward);

            if (direction.sqrMagnitude < 0.0001f)
                direction = Vector3.forward;

            direction.Normalize();
            return Vector3.Cross(Vector3.up, direction).normalized;
        }

        private void AutoChooseLaneSideFromSpawn()
        {
            if (!chooseNearestLaneOnSpawn || path.Count < 2) return;

            Vector3 carPos = ProjectXZ(vehicleRB.position);
            int end = Mathf.Min(path.Count - 1, Mathf.Max(10, laneChooseScanPoints));

            int nearestIndex = 0;
            float nearestDistance = float.PositiveInfinity;

            for (int i = 0; i <= end; i++)
            {
                float distance = (ProjectXZ(path[i].position) - carPos).sqrMagnitude;
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestIndex = i;
                }
            }

            Vector3 center = ProjectXZ(path[nearestIndex].position);
            Vector3 right = GetLaneRight(nearestIndex);
            float offset = Mathf.Abs(laneOffset);

            float leftDistance = Vector3.Distance(center - right * offset, carPos);
            float rightDistance = Vector3.Distance(center + right * offset, carPos);

            if (Mathf.Abs(leftDistance - rightDistance) <= laneChooseDeadzone)
            {
                laneSideSign = 0;
                return;
            }

            int defaultSign = driveSide == DriveSide.RightHandTraffic ? 1 : -1;
            int pickedSign = rightDistance < leftDistance ? 1 : -1;
            laneSideSign = pickedSign == defaultSign ? 0 : pickedSign;
        }

        private float DistanceToLanePointXZ(int index)
        {
            return Vector3.Distance(ProjectXZ(vehicleRB.position), ProjectXZ(LanePos(index)));
        }

        private float GetSpeedLimitFromTraffic()
        {
            Vector3 origin = vehicleRB.position + Vector3.up * 0.8f;
            float limit = speedLimit;

            if (!Physics.SphereCast(origin, detectRadius, transform.forward, out RaycastHit hit, detectDistance, vehicleMask, QueryTriggerInteraction.Ignore))
                return limit;

            if (!hit.rigidbody || hit.rigidbody == vehicleRB)
                return limit;

            float distance = hit.distance;
            if (distance <= minGap) return 0f;
            if (distance <= slowDownGap)
                limit = speedLimit * Mathf.Clamp01(distance / slowDownGap);

            return limit;
        }

        private float GetSpeedLimitFromIntersectionSlowdown()
        {
            if (!slowDownNearIntersection || roadSystem.Intersections == null) return speedLimit;

            if (!TryGetUpcomingIntersection(out _, out float distanceToEdge, out _))
                return speedLimit;

            if (distanceToEdge >= lookAheadDistance) return speedLimit;

            float t = Mathf.Clamp01(distanceToEdge / Mathf.Max(0.1f, lookAheadDistance));
            float factor = Mathf.Lerp(intersectionMinSpeedFactor, 1f, t);
            return speedLimit * factor;
        }

        private float GetSpeedLimitFromIntersectionRightOfWay()
        {
            if (!useIntersectionRightOfWay) return speedLimit;

            if (!TryGetUpcomingIntersection(out Intersection intersection, out float distanceToEdge, out float centerDistance))
            {
                if (!wasInsideRequestedIntersection)
                    ReleaseIntersectionRequest();

                return speedLimit;
            }

            float requestDistance = Mathf.Max(intersectionRequestDistance, GetBrakingDistance() + 2f);
            if (distanceToEdge > requestDistance && requestedIntersection == null)
                return speedLimit;

            requestedIntersection = intersection;
            bool inside = centerDistance <= GetIntersectionRadius(intersection);

            if (inside)
                wasInsideRequestedIntersection = true;

            bool granted = IntersectionGate.Request(intersection, this, Time.time, distanceToEdge);
            if (granted || inside)
                return speedLimit;

            if (distanceToEdge <= intersectionClearBuffer)
                return 0f;

            float stopSpeed = Mathf.Sqrt(2f * Mathf.Max(0.1f, brakeDecel) * Mathf.Max(0.01f, distanceToEdge));
            return Mathf.Min(speedLimit, stopSpeed);
        }

        private bool TryGetUpcomingIntersection(out Intersection intersection, out float distanceToEdge, out float centerDistance)
        {
            intersection = null;
            distanceToEdge = 0f;
            centerDistance = 0f;

            if (roadSystem.Intersections == null || path.Count < 2) return false;

            float travelled = 0f;
            Vector3 previous = ProjectXZ(vehicleRB.position);

            int maxIndex = Mathf.Min(path.Count - 1, pathIndex + 80);
            for (int i = pathIndex; i <= maxIndex; i++)
            {
                Vector3 point = ProjectXZ(LanePos(i));
                travelled += Vector3.Distance(previous, point);
                previous = point;

                foreach (Intersection candidate in roadSystem.Intersections)
                {
                    if (!candidate) continue;

                    float radius = GetIntersectionRadius(candidate);
                    float distanceToCenter = Vector3.Distance(point, ProjectXZ(candidate.transform.position));

                    if (distanceToCenter <= radius)
                    {
                        intersection = candidate;
                        distanceToEdge = Mathf.Max(0f, travelled - radius);
                        centerDistance = Vector3.Distance(ProjectXZ(vehicleRB.position), ProjectXZ(candidate.transform.position));
                        return true;
                    }
                }
            }

            return false;
        }

        private void UpdateIntersectionRelease()
        {
            if (!requestedIntersection) return;

            float radius = GetIntersectionRadius(requestedIntersection);
            float distance = Vector3.Distance(ProjectXZ(vehicleRB.position), ProjectXZ(requestedIntersection.transform.position));
            bool inside = distance <= radius;

            if (inside)
                wasInsideRequestedIntersection = true;

            if (wasInsideRequestedIntersection && distance > radius + intersectionClearBuffer)
                ReleaseIntersectionRequest();
        }

        private void ReleaseIntersectionRequest()
        {
            if (requestedIntersection)
                IntersectionGate.Release(requestedIntersection, this, Time.time);

            requestedIntersection = null;
            wasInsideRequestedIntersection = false;
        }

        private float GetIntersectionRadius(Intersection intersection)
        {
            return Mathf.Max(0.1f, intersection.Radius) + Mathf.Max(0f, intersectionDetectPadding);
        }

        private float GetBrakingDistance()
        {
            float speedMs = Mathf.Abs(currentSpeedKmh) / 3.6f;
            return (speedMs * speedMs) / (2f * Mathf.Max(0.1f, brakeDecel));
        }

        private void ComputeThrottleBrake(float desiredSpeedMs, out float throttle, out float brake)
        {
            if (!isStarted || desiredSpeedMs <= 0.05f)
            {
                throttle = 0f;
                brake = 1f;
                return;
            }

            float desiredKmh = desiredSpeedMs * 3.6f;
            float currentKmh = Mathf.Max(0f, currentSpeedKmh);
            float error = desiredKmh - currentKmh;

            if (error > 1.2f)
            {
                throttle = Mathf.Clamp01(error / 25f);
                brake = 0f;
            }
            else if (error < -1.2f)
            {
                throttle = 0f;
                brake = Mathf.Clamp01(-error / 18f);
            }
            else
            {
                throttle = 0f;
                brake = 0f;
            }

            if (desiredSpeedMs > minCreepSpeed && currentKmh < 2f && brake < 0.05f)
                throttle = Mathf.Max(throttle, 0.2f);
        }

        private float ComputeTargetSteerAngle(Vector3 worldPoint)
        {
            Vector3 local = transform.InverseTransformPoint(worldPoint);
            local.y = 0f;

            if (local.sqrMagnitude < 0.001f) return 0f;

            float angle = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            return Mathf.Clamp(angle, -maxSteerAngle, maxSteerAngle);
        }

        private void ApplyMotor()
        {
            if (!isStarted || brake01 > 0.05f || throttle01 <= 0.001f || Mathf.Abs(currentSpeedKmh) >= maxForwardSpeed)
            {
                SetAllMotorTorque(0f);
                return;
            }

            float speedFactor = Mathf.InverseLerp(0f, Mathf.Max(1f, maxForwardSpeed), Mathf.Abs(currentSpeedKmh));
            float torque = Mathf.Lerp(horsePower, 0f, speedFactor) * throttle01;

            frontLeftWheelCollider.motorTorque = driveType == DriveTypes.RWD ? 0f : torque;
            frontRightWheelCollider.motorTorque = driveType == DriveTypes.RWD ? 0f : torque;
            rearLeftWheelCollider.motorTorque = driveType == DriveTypes.FWD ? 0f : torque;
            rearRightWheelCollider.motorTorque = driveType == DriveTypes.FWD ? 0f : torque;
        }

        private void ApplyBrakes()
        {
            float torque = brake01 * brakePower;
            frontLeftWheelCollider.brakeTorque = torque;
            frontRightWheelCollider.brakeTorque = torque;
            rearLeftWheelCollider.brakeTorque = torque * 0.75f;
            rearRightWheelCollider.brakeTorque = torque * 0.75f;
        }

        private void ApplySteering()
        {
            float speedFactor = Mathf.InverseLerp(20f, maxForwardSpeed, Mathf.Abs(currentSpeedKmh));
            float adjustedSteer = targetSteerAngle * (1f - speedFactor);

            currentSteerAngle = Mathf.Lerp(currentSteerAngle, adjustedSteer, Time.fixedDeltaTime * steeringSpeed);
            frontLeftWheelCollider.steerAngle = currentSteerAngle;
            frontRightWheelCollider.steerAngle = currentSteerAngle;
        }

        private void SetAllMotorTorque(float value)
        {
            frontLeftWheelCollider.motorTorque = value;
            frontRightWheelCollider.motorTorque = value;
            rearLeftWheelCollider.motorTorque = value;
            rearRightWheelCollider.motorTorque = value;
        }

        private void UpdateWheelVisuals()
        {
            UpdateWheel(frontLeftWheelCollider, frontLeftWheelMesh);
            UpdateWheel(frontRightWheelCollider, frontRightWheelMesh);
            UpdateWheel(rearLeftWheelCollider, rearLeftWheelMesh);
            UpdateWheel(rearRightWheelCollider, rearRightWheelMesh);
        }

        private static void UpdateWheel(WheelCollider collider, Transform mesh)
        {
            if (!mesh) return;
            collider.GetWorldPose(out Vector3 position, out Quaternion rotation);
            mesh.SetPositionAndRotation(position, rotation);
        }

        private static Vector3 ProjectXZ(Vector3 value)
        {
            return new Vector3(value.x, 0f, value.z);
        }

        private static class IntersectionGate
        {
            private class State
            {
                public TrafficCarOld current;
                public readonly List<Entry> queue = new();
            }

            private struct Entry
            {
                public TrafficCarOld car;
                public float requestTime;
                public float distance;
            }

            private static readonly Dictionary<Intersection, State> states = new();

            public static bool Request(Intersection intersection, TrafficCarOld car, float now, float distance)
            {
                if (!intersection || !car) return false;

                if (!states.TryGetValue(intersection, out State state))
                {
                    state = new State();
                    states.Add(intersection, state);
                }

                Cleanup(state);

                if (!state.current)
                {
                    state.current = car;
                    RemoveFromQueue(state, car);
                    return true;
                }

                if (state.current == car)
                    return true;

                int index = IndexOf(state, car);
                if (index < 0)
                {
                    state.queue.Add(new Entry
                    {
                        car = car,
                        requestTime = now,
                        distance = distance
                    });
                }
                else
                {
                    Entry entry = state.queue[index];
                    entry.distance = distance;
                    state.queue[index] = entry;
                }

                return false;
            }

            public static void Release(Intersection intersection, TrafficCarOld car, float now)
            {
                if (!intersection || !car) return;
                if (!states.TryGetValue(intersection, out State state)) return;

                Cleanup(state);

                if (state.current != car)
                {
                    RemoveFromQueue(state, car);
                    return;
                }

                state.current = null;
                Cleanup(state);

                if (state.queue.Count == 0) return;

                int nextIndex = 0;
                Entry next = state.queue[0];

                for (int i = 1; i < state.queue.Count; i++)
                {
                    Entry entry = state.queue[i];
                    if (entry.requestTime < next.requestTime - 0.0001f ||
                        (Mathf.Abs(entry.requestTime - next.requestTime) <= 0.0001f && entry.distance < next.distance))
                    {
                        nextIndex = i;
                        next = entry;
                    }
                }

                state.current = state.queue[nextIndex].car;
                state.queue.RemoveAt(nextIndex);
            }

            private static void Cleanup(State state)
            {
                if (state.current && (!state.current.enabled || !state.current.gameObject.activeInHierarchy))
                    state.current = null;

                for (int i = state.queue.Count - 1; i >= 0; i--)
                {
                    TrafficCarOld car = state.queue[i].car;
                    if (!car || !car.enabled || !car.gameObject.activeInHierarchy)
                        state.queue.RemoveAt(i);
                }
            }

            private static int IndexOf(State state, TrafficCarOld car)
            {
                for (int i = 0; i < state.queue.Count; i++)
                    if (state.queue[i].car == car)
                        return i;

                return -1;
            }

            private static void RemoveFromQueue(State state, TrafficCarOld car)
            {
                for (int i = state.queue.Count - 1; i >= 0; i--)
                    if (state.queue[i].car == car)
                        state.queue.RemoveAt(i);
            }
        }
    }
}
