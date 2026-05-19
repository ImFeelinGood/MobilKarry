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

        [Header("Spawn Initialization")]
        [Tooltip("When spawned by TrafficCarSpawnManager, prefer the first path goal in front of the spawn direction.")]
        public bool preferSpawnForwardForFirstPath = true;
        [Tooltip("How far in front a first goal must be. Higher = stricter, lower = easier to find a goal.")]
        [Range(-1f, 1f)] public float spawnForwardGoalDot = 0.15f;

        [Header("Anti U-Turn")]
        [Tooltip("Reject newly generated paths that immediately go behind the car. This still allows normal left/right turns.")]
        public bool preventImmediateUTurns = true;
        [Tooltip("Minimum dot product between car forward and the first useful path direction. -0.25 allows sharp turns but blocks backward/U-turn paths.")]
        [Range(-1f, 1f)] public float minInitialPathForwardDot = -0.25f;
        [Tooltip("Distance used to find the first useful direction of a newly generated path.")]
        public float initialPathDirectionCheckDistance = 8f;
        [Tooltip("Reject routes that enter an intersection and exit in almost the opposite direction.")]
        public bool preventIntersectionUTurns = true;
        [Tooltip("If entry direction dot exit direction is below this value, the route is treated as a U-turn.")]
        [Range(-1f, 0f)] public float intersectionUTurnDot = -0.65f;
        [Tooltip("How many random goals are tested before giving up. Increase this if your road network is large.")]
        public int goalPickAttempts = 80;

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
        [Tooltip("Extra offset before the detected intersection edge. Use 0 to stop exactly on the edge.")]
        public float stopBeforeIntersectionEdge = 0f;
        [Tooltip("Distance before the intersection edge where the car starts reducing speed.")]
        public float intersectionSlowDownDistance = 18f;
        [Range(0.1f, 1f)] public float intersectionMinSpeedFactor = 0.35f;
        public float brakeDecel = 10f;
        [Tooltip("Disable this car's front vehicle detection while it is already inside an intersection.")]
        public bool disableVehicleDetectionInsideIntersection = true;
        [Tooltip("Cars with a similar forward direction and lane position can enter the intersection together.")]
        [Range(0.5f, 1f)] public float sameLaneForwardDot = 0.85f;
        [Tooltip("Maximum sideways distance allowed to count as the same lane group.")]
        public float sameLaneMaxLateralDistance = 2.2f;

        [Header("Intersection Fail Safe")]
        [Tooltip("Removes stale intersection reservations so cars do not stay blocked forever when the visible way is clear.")]
        public bool useIntersectionFailSafe = true;
        [Tooltip("Maximum time a car may reserve an intersection before entering it. If it never enters, the reservation is cleared.")]
        public float maxTimeReservedBeforeEntering = 6f;
        [Tooltip("If a reserved car is this far from the intersection center, its reservation is considered stale.")]
        public float maxReservationDistanceFromCenter = 35f;

        [Header("Debug Gizmos")]
        public bool drawGizmos = true;
        [Tooltip("If true, gizmos are drawn only when this car is selected. If false, they are always drawn in the Scene view.")]
        public bool drawGizmosOnlyWhenSelected = true;
        public bool drawPathGizmo = true;
        public bool drawLaneGizmo = true;
        public bool drawLookAheadGizmo = true;
        public bool drawVehicleDetectionGizmo = true;
        public bool drawIntersectionGizmo = true;
        public Color pathGizmoColor = new Color(0f, 0.7f, 1f, 1f);
        public Color laneGizmoColor = new Color(0f, 1f, 0.25f, 1f);
        public Color lookAheadGizmoColor = Color.yellow;
        public Color vehicleDetectionGizmoColor = new Color(1f, 0.5f, 0f, 1f);
        public Color intersectionGizmoColor = new Color(1f, 0f, 0f, 0.9f);
        public Color stopLineGizmoColor = Color.magenta;

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
        private float intersectionRequestStartTime = -1f;

        private bool hasSpawnForwardOverride;
        private bool hasSpawnLaneOverride;
        private Vector3 spawnForwardOverride;

        /// <summary>
        /// Called by TrafficCarSpawnManager immediately after Instantiate.
        /// This prevents newly spawned cars from turning around or choosing the opposite traffic lane.
        /// </summary>
        public void InitializeFromSpawner(Vector3 preferredForward, int preferredLaneSign)
        {
            Vector3 flatForward = ProjectXZ(preferredForward);
            if (flatForward.sqrMagnitude > 0.0001f)
            {
                spawnForwardOverride = flatForward.normalized;
                hasSpawnForwardOverride = true;
            }

            if (preferredLaneSign != 0)
            {
                laneSideSign = preferredLaneSign > 0 ? 1 : -1;
                hasSpawnLaneOverride = true;
            }

            if (vehicleRB)
            {
                vehicleRB.linearVelocity = Vector3.zero;
                vehicleRB.angularVelocity = Vector3.zero;
            }
        }

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

            bool useSpawnForwardFilter = preferSpawnForwardForFirstPath && hasSpawnForwardOverride;

            if (useSpawnForwardFilter && TryPickNewGoalAndRepath(useSpawnForwardFilter: true))
            {
                hasSpawnForwardOverride = false;
                return;
            }

            if (TryPickNewGoalAndRepath(useSpawnForwardFilter: false))
                hasSpawnForwardOverride = false;
        }

        private bool TryPickNewGoalAndRepath(bool useSpawnForwardFilter)
        {
            int attempts = Mathf.Max(1, goalPickAttempts);

            for (int i = 0; i < attempts; i++)
            {
                Intersection intersection = roadSystem.Intersections[Random.Range(0, roadSystem.Intersections.Length)];
                if (!intersection || intersection.AnchorPoints == null || intersection.AnchorPoints.Length == 0) continue;

                RoadAnchor anchor = intersection.AnchorPoints[Random.Range(0, intersection.AnchorPoints.Length)];
                if (!anchor) continue;

                Vector3 candidateGoal = anchor.transform.position;

                if (useSpawnForwardFilter && !IsGoalInSpawnForwardDirection(candidateGoal))
                    continue;

                List<Bezier.OrientedPoint> candidatePath = FindRoadPath(candidateGoal);
                if (candidatePath == null || candidatePath.Count < 2)
                    continue;

                if (!IsCandidatePathAllowed(candidatePath))
                    continue;

                goalWorld = candidateGoal;
                ApplyPath(candidatePath);
                return true;
            }

            return false;
        }

        private bool IsGoalInSpawnForwardDirection(Vector3 candidateGoal)
        {
            if (!hasSpawnForwardOverride)
                return true;

            Vector3 toGoal = ProjectXZ(candidateGoal - vehicleRB.position);
            if (toGoal.sqrMagnitude < 0.0001f)
                return false;

            float dot = Vector3.Dot(toGoal.normalized, spawnForwardOverride.normalized);
            return dot >= spawnForwardGoalDot;
        }

        private void Repath(Vector3 goal)
        {
            List<Bezier.OrientedPoint> candidatePath = FindRoadPath(goal);
            if (candidatePath == null || candidatePath.Count < 2)
                return;

            if (!IsCandidatePathAllowed(candidatePath))
                return;

            ApplyPath(candidatePath);
        }

        private List<Bezier.OrientedPoint> FindRoadPath(Vector3 goal)
        {
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

            return newPath;
        }

        private void ApplyPath(List<Bezier.OrientedPoint> newPath)
        {
            repathTimer = repathCooldown;
            path.Clear();
            pathIndex = 0;
            path.AddRange(newPath);
        }

        private bool IsCandidatePathAllowed(List<Bezier.OrientedPoint> candidatePath)
        {
            if (candidatePath == null || candidatePath.Count < 2)
                return false;

            if (preventImmediateUTurns && !DoesCandidatePathStartForward(candidatePath))
                return false;

            if (preventIntersectionUTurns && DoesCandidatePathMakeImmediateIntersectionUTurn(candidatePath))
                return false;

            return true;
        }

        private bool DoesCandidatePathStartForward(List<Bezier.OrientedPoint> candidatePath)
        {
            Vector3 referenceForward = GetCurrentRouteForward();
            if (referenceForward.sqrMagnitude < 0.0001f)
                return true;

            Vector3 carPos = ProjectXZ(vehicleRB ? vehicleRB.position : transform.position);
            float minCheckDistance = Mathf.Max(waypointReachDistance, initialPathDirectionCheckDistance);
            int maxIndex = Mathf.Min(candidatePath.Count - 1, 30);

            for (int i = 1; i <= maxIndex; i++)
            {
                Vector3 toPoint = ProjectXZ(LanePos(candidatePath, i) - carPos);
                if (toPoint.magnitude < minCheckDistance)
                    continue;

                float dot = Vector3.Dot(referenceForward, toPoint.normalized);
                return dot >= minInitialPathForwardDot;
            }

            return true;
        }

        private bool DoesCandidatePathMakeImmediateIntersectionUTurn(List<Bezier.OrientedPoint> candidatePath)
        {
            if (roadSystem == null || roadSystem.Intersections == null || roadSystem.Intersections.Length == 0)
                return false;

            Vector3 carPos = ProjectXZ(vehicleRB ? vehicleRB.position : transform.position);
            Vector3 referenceForward = GetCurrentRouteForward();

            Intersection intersection = GetIntersectionContainingPoint(carPos);
            bool enteredIntersection = intersection != null;
            Vector3 entryDirection = referenceForward;

            if (entryDirection.sqrMagnitude < 0.0001f)
                entryDirection = Vector3.forward;

            Vector3 previous = carPos;
            int maxIndex = Mathf.Min(candidatePath.Count - 1, 120);

            for (int i = 1; i <= maxIndex; i++)
            {
                Vector3 point = ProjectXZ(LanePos(candidatePath, i));

                if (!enteredIntersection)
                {
                    foreach (Intersection candidate in roadSystem.Intersections)
                    {
                        if (!candidate) continue;

                        float radius = GetIntersectionRadius(candidate);
                        Vector3 center = ProjectXZ(candidate.transform.position);

                        if (!TryGetSegmentCircleEnterT(previous, point, center, radius, out _))
                            continue;

                        Vector3 segmentDirection = ProjectXZ(point - previous);
                        if (segmentDirection.sqrMagnitude > 0.0001f)
                            entryDirection = segmentDirection.normalized;

                        intersection = candidate;
                        enteredIntersection = true;
                        break;
                    }
                }
                else
                {
                    float radius = GetIntersectionRadius(intersection) + Mathf.Max(0f, intersectionClearBuffer);
                    float distanceFromCenter = Vector3.Distance(point, ProjectXZ(intersection.transform.position));

                    if (distanceFromCenter > radius)
                    {
                        Vector3 exitDirection = ProjectXZ(point - previous);
                        if (exitDirection.sqrMagnitude < 0.0001f)
                            return false;

                        exitDirection.Normalize();
                        entryDirection.Normalize();

                        float dot = Vector3.Dot(entryDirection, exitDirection);
                        return dot <= intersectionUTurnDot;
                    }
                }

                previous = point;
            }

            return false;
        }

        private Intersection GetIntersectionContainingPoint(Vector3 point)
        {
            if (roadSystem == null || roadSystem.Intersections == null)
                return null;

            Vector3 flatPoint = ProjectXZ(point);

            foreach (Intersection intersection in roadSystem.Intersections)
            {
                if (!intersection) continue;

                float radius = GetIntersectionRadius(intersection);
                float distance = Vector3.Distance(flatPoint, ProjectXZ(intersection.transform.position));

                if (distance <= radius)
                    return intersection;
            }

            return null;
        }

        private Vector3 GetCurrentRouteForward()
        {
            Vector3 referenceForward = hasSpawnForwardOverride
                ? spawnForwardOverride
                : ProjectXZ(transform.forward);

            if (vehicleRB && vehicleRB.linearVelocity.sqrMagnitude > 1f)
            {
                Vector3 velocityForward = ProjectXZ(vehicleRB.linearVelocity);
                if (velocityForward.sqrMagnitude > 0.0001f)
                    referenceForward = velocityForward.normalized;
            }

            if (referenceForward.sqrMagnitude < 0.0001f)
                return Vector3.zero;

            return referenceForward.normalized;
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
            return LanePos(path, index);
        }

        private Vector3 LanePos(List<Bezier.OrientedPoint> sourcePath, int index)
        {
            if (sourcePath == null || sourcePath.Count == 0)
                return vehicleRB ? vehicleRB.position : transform.position;

            index = Mathf.Clamp(index, 0, sourcePath.Count - 1);

            float sign = laneSideSign != 0
                ? laneSideSign
                : (driveSide == DriveSide.RightHandTraffic ? 1f : -1f);

            return sourcePath[index].position + GetLaneRight(sourcePath, index) * (Mathf.Abs(laneOffset) * sign);
        }

        private Vector3 GetLaneRight(int index)
        {
            return GetLaneRight(path, index);
        }

        private Vector3 GetLaneRight(List<Bezier.OrientedPoint> sourcePath, int index)
        {
            if (sourcePath == null || sourcePath.Count == 0)
                return Vector3.Cross(Vector3.up, ProjectXZ(transform.forward).normalized).normalized;

            index = Mathf.Clamp(index, 0, sourcePath.Count - 1);

            Vector3 direction = Vector3.zero;

            if (index > 0)
                direction += ProjectXZ(sourcePath[index].position - sourcePath[index - 1].position).normalized;

            if (index < sourcePath.Count - 1)
                direction += ProjectXZ(sourcePath[index + 1].position - sourcePath[index].position).normalized;

            if (direction.sqrMagnitude < 0.0001f)
                direction = ProjectXZ(transform.forward);

            if (direction.sqrMagnitude < 0.0001f)
                direction = Vector3.forward;

            direction.Normalize();
            return Vector3.Cross(Vector3.up, direction).normalized;
        }

        private void AutoChooseLaneSideFromSpawn()
        {
            if (hasSpawnLaneOverride) return;
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
            if (disableVehicleDetectionInsideIntersection && IsInsideAnyIntersection())
                return speedLimit;

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
            if (!slowDownNearIntersection || roadSystem.Intersections == null)
                return speedLimit;

            if (!TryGetUpcomingIntersection(out _, out float distanceToEdge, out _))
                return speedLimit;

            float distanceToStopLine = Mathf.Max(0f, GetDistanceToIntersectionStopLine(distanceToEdge));
            float slowDownDistance = Mathf.Max(0.1f, intersectionSlowDownDistance);

            if (distanceToStopLine >= slowDownDistance)
                return speedLimit;

            float t = Mathf.Clamp01(distanceToStopLine / slowDownDistance);
            float factor = Mathf.Lerp(intersectionMinSpeedFactor, 1f, t);

            return speedLimit * factor;
        }

        private float GetSpeedLimitFromIntersectionRightOfWay()
        {
            if (!useIntersectionRightOfWay)
                return speedLimit;

            if (!TryGetUpcomingIntersection(out Intersection intersection, out float distanceToEdge, out float centerDistance))
            {
                if (!wasInsideRequestedIntersection)
                    ReleaseIntersectionRequest();

                return speedLimit;
            }

            float distanceToStopLine = Mathf.Max(0f, GetDistanceToIntersectionStopLine(distanceToEdge));
            float requestDistance = Mathf.Max(intersectionRequestDistance, intersectionSlowDownDistance, GetBrakingDistance() + 1f);

            if (distanceToStopLine > requestDistance && requestedIntersection == null)
                return speedLimit;

            SetRequestedIntersection(intersection);
            bool inside = centerDistance <= GetIntersectionRadius(intersection);
            IntersectionApproach approach = GetCurrentIntersectionApproach();
            bool granted = IntersectionGate.Request(intersection, this, Time.time, distanceToStopLine, approach);

            if (granted)
            {
                if (inside)
                    wasInsideRequestedIntersection = true;

                return speedLimit;
            }

            // Smooth braking into the exact stop line
            if (distanceToStopLine <= 0.05f)
                return 0f;

            float stopSpeed = Mathf.Sqrt(2f * Mathf.Max(0.1f, brakeDecel) * distanceToStopLine);
            return Mathf.Min(speedLimit, stopSpeed);
        }

        private bool TryGetUpcomingIntersection(out Intersection intersection, out float distanceToEdge, out float centerDistance)
        {
            intersection = null;
            distanceToEdge = 0f;
            centerDistance = 0f;

            if (roadSystem.Intersections == null || path.Count < 2)
                return false;

            Vector3 carPos = ProjectXZ(vehicleRB.position);

            // If already inside an intersection, return it immediately.
            foreach (Intersection candidate in roadSystem.Intersections)
            {
                if (!candidate) continue;

                float radius = GetIntersectionRadius(candidate);
                Vector3 center = ProjectXZ(candidate.transform.position);
                float distToCenter = Vector3.Distance(carPos, center);

                if (distToCenter <= radius)
                {
                    intersection = candidate;
                    distanceToEdge = 0f;
                    centerDistance = distToCenter;
                    return true;
                }
            }

            float travelled = 0f;
            Vector3 previous = carPos;

            int maxIndex = Mathf.Min(path.Count - 1, pathIndex + 80);
            for (int i = pathIndex; i <= maxIndex; i++)
            {
                Vector3 point = ProjectXZ(LanePos(i));
                float segmentLength = Vector3.Distance(previous, point);

                Intersection bestIntersection = null;
                float bestEnterT = float.PositiveInfinity;
                float bestCenterDistance = 0f;

                foreach (Intersection candidate in roadSystem.Intersections)
                {
                    if (!candidate) continue;

                    float radius = GetIntersectionRadius(candidate);
                    Vector3 center = ProjectXZ(candidate.transform.position);

                    if (TryGetSegmentCircleEnterT(previous, point, center, radius, out float enterT))
                    {
                        if (enterT < bestEnterT)
                        {
                            bestEnterT = enterT;
                            bestIntersection = candidate;
                            bestCenterDistance = Vector3.Distance(carPos, center);
                        }
                    }
                }

                if (bestIntersection != null)
                {
                    intersection = bestIntersection;
                    distanceToEdge = travelled + (segmentLength * bestEnterT);
                    centerDistance = bestCenterDistance;
                    return true;
                }

                travelled += segmentLength;
                previous = point;
            }

            return false;
        }

        private static bool TryGetSegmentCircleEnterT(Vector3 start, Vector3 end, Vector3 center, float radius, out float enterT)
        {
            enterT = 0f;

            Vector3 d = end - start;
            float a = Vector3.Dot(d, d);

            if (a <= 0.0001f)
                return false;

            Vector3 f = start - center;
            float c = Vector3.Dot(f, f) - (radius * radius);

            // Start point is already inside the circle
            if (c <= 0f)
            {
                enterT = 0f;
                return true;
            }

            float b = 2f * Vector3.Dot(f, d);
            float discriminant = b * b - 4f * a * c;

            if (discriminant < 0f)
                return false;

            float sqrt = Mathf.Sqrt(discriminant);

            float t1 = (-b - sqrt) / (2f * a);
            float t2 = (-b + sqrt) / (2f * a);

            if (t1 >= 0f && t1 <= 1f)
            {
                enterT = t1;
                return true;
            }

            if (t2 >= 0f && t2 <= 1f)
            {
                enterT = t2;
                return true;
            }

            return false;
        }

        private bool IsInsideAnyIntersection()
        {
            if (roadSystem == null || roadSystem.Intersections == null)
                return false;

            Vector3 carPos = ProjectXZ(vehicleRB ? vehicleRB.position : transform.position);

            foreach (Intersection intersection in roadSystem.Intersections)
            {
                if (!intersection) continue;

                float radius = GetIntersectionRadius(intersection);
                float distance = Vector3.Distance(carPos, ProjectXZ(intersection.transform.position));

                if (distance <= radius)
                    return true;
            }

            return false;
        }

        private IntersectionApproach GetCurrentIntersectionApproach()
        {
            Vector3 forward = Vector3.zero;

            if (path.Count >= 2)
            {
                int current = Mathf.Clamp(pathIndex, 0, path.Count - 1);
                int next = Mathf.Min(path.Count - 1, current + 1);
                int previous = Mathf.Max(0, current - 1);

                if (next != current)
                    forward = ProjectXZ(LanePos(next) - LanePos(current));
                else if (previous != current)
                    forward = ProjectXZ(LanePos(current) - LanePos(previous));
            }

            if (forward.sqrMagnitude < 0.0001f)
                forward = ProjectXZ(transform.forward);

            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.forward;

            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

            int sign = laneSideSign != 0
                ? laneSideSign
                : (driveSide == DriveSide.RightHandTraffic ? 1 : -1);

            return new IntersectionApproach
            {
                position = ProjectXZ(vehicleRB ? vehicleRB.position : transform.position),
                forward = forward,
                right = right,
                laneSign = sign
            };
        }

        private void SetRequestedIntersection(Intersection intersection)
        {
            if (requestedIntersection == intersection)
                return;

            // If the car switches to another detected intersection, release the old one first.
            // Without this, the old intersection can stay locked even though it looks clear.
            ReleaseIntersectionRequest();

            requestedIntersection = intersection;
            wasInsideRequestedIntersection = false;
            intersectionRequestStartTime = Time.time;
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
            {
                ReleaseIntersectionRequest();
                return;
            }

            // Fail-safe: if this car reserved the intersection but never actually entered it,
            // clear the reservation so other cars are not blocked forever.
            if (useIntersectionFailSafe && !inside && intersectionRequestStartTime > 0f)
            {
                bool waitedTooLong = Time.time - intersectionRequestStartTime > Mathf.Max(0.5f, maxTimeReservedBeforeEntering);
                bool tooFarFromIntersection = distance > Mathf.Max(GetIntersectionRadius(requestedIntersection) + intersectionClearBuffer, maxReservationDistanceFromCenter);

                if (waitedTooLong || tooFarFromIntersection)
                    ReleaseIntersectionRequest();
            }
        }

        private bool IsStaleIntersectionBlocker(Intersection intersection, float now)
        {
            if (!useIntersectionFailSafe)
                return false;

            if (!intersection)
                return true;

            // If this car is still listed inside the gate, but it is no longer requesting
            // this intersection, then the gate state is stale.
            if (requestedIntersection != intersection)
                return true;

            float radius = GetIntersectionRadius(intersection);
            float distance = Vector3.Distance(ProjectXZ(vehicleRB ? vehicleRB.position : transform.position), ProjectXZ(intersection.transform.position));
            bool inside = distance <= radius;

            if (inside)
                return false;

            if (wasInsideRequestedIntersection && distance > radius + intersectionClearBuffer)
                return true;

            if (intersectionRequestStartTime > 0f && now - intersectionRequestStartTime > Mathf.Max(0.5f, maxTimeReservedBeforeEntering))
                return true;

            if (distance > Mathf.Max(radius + intersectionClearBuffer, maxReservationDistanceFromCenter))
                return true;

            return false;
        }

        private void ReleaseIntersectionRequest()
        {
            if (requestedIntersection)
                IntersectionGate.Release(requestedIntersection, this, Time.time);

            requestedIntersection = null;
            wasInsideRequestedIntersection = false;
            intersectionRequestStartTime = -1f;
        }

        private float GetIntersectionRadius(Intersection intersection)
        {
            return Mathf.Max(0.1f, intersection.Radius) + Mathf.Max(0f, intersectionDetectPadding);
        }

        private float GetDistanceToIntersectionStopLine(float distanceToEdge)
        {
            return distanceToEdge - Mathf.Max(0f, stopBeforeIntersectionEdge);
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

        private void OnDrawGizmos()
        {
            if (drawGizmosOnlyWhenSelected) return;
            DrawTrafficGizmos();
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmosOnlyWhenSelected) return;
            DrawTrafficGizmos();
        }

        private void DrawTrafficGizmos()
        {
            if (!drawGizmos) return;

            Vector3 carPosition = vehicleRB ? vehicleRB.position : transform.position;

            if (drawVehicleDetectionGizmo)
                DrawVehicleDetectionGizmo(carPosition);

            if (drawPathGizmo)
                DrawPathCenterGizmo();

            if (drawLaneGizmo)
                DrawLanePathGizmo();

            if (drawLookAheadGizmo && Application.isPlaying && path.Count >= 2)
                DrawLookAheadPointGizmo(carPosition);

            if (drawIntersectionGizmo)
                DrawIntersectionDebugGizmo(carPosition);
        }

        private void DrawVehicleDetectionGizmo(Vector3 carPosition)
        {
            Gizmos.color = vehicleDetectionGizmoColor;

            Vector3 origin = carPosition + Vector3.up * 0.8f;
            Vector3 end = origin + transform.forward * detectDistance;

            Gizmos.DrawLine(origin, end);
            Gizmos.DrawWireSphere(origin, detectRadius);
            Gizmos.DrawWireSphere(end, detectRadius);
        }

        private void DrawPathCenterGizmo()
        {
            if (!Application.isPlaying || path.Count < 2) return;

            Gizmos.color = pathGizmoColor;

            for (int i = 0; i < path.Count - 1; i++)
                Gizmos.DrawLine(path[i].position + Vector3.up * 0.08f, path[i + 1].position + Vector3.up * 0.08f);
        }

        private void DrawLanePathGizmo()
        {
            if (!Application.isPlaying || path.Count < 2) return;

            Gizmos.color = laneGizmoColor;

            for (int i = 0; i < path.Count - 1; i++)
                Gizmos.DrawLine(LanePos(i) + Vector3.up * 0.15f, LanePos(i + 1) + Vector3.up * 0.15f);

            Gizmos.DrawWireSphere(LanePos(pathIndex) + Vector3.up * 0.25f, 0.35f);
        }

        private void DrawLookAheadPointGizmo(Vector3 carPosition)
        {
            Vector3 lookPoint = GetLookAheadPoint();

            Gizmos.color = lookAheadGizmoColor;
            Gizmos.DrawLine(carPosition + Vector3.up * 0.35f, lookPoint + Vector3.up * 0.35f);
            Gizmos.DrawWireSphere(lookPoint + Vector3.up * 0.35f, 0.5f);
        }

        private void DrawIntersectionDebugGizmo(Vector3 carPosition)
        {
            if (!roadSystem || roadSystem.Intersections == null) return;

            foreach (Intersection intersection in roadSystem.Intersections)
            {
                if (!intersection) continue;

                float radius = GetIntersectionRadius(intersection);
                Vector3 center = intersection.transform.position + Vector3.up * 0.05f;

                Gizmos.color = intersection == requestedIntersection ? Color.red : intersectionGizmoColor;
                DrawWireCircleXZ(center, radius, 40);
            }

            if (!Application.isPlaying || path.Count < 2) return;

            if (!TryGetUpcomingIntersection(out Intersection upcoming, out float distanceToEdge, out _)) return;

            Vector3 edgePoint = GetPointAlongLanePath(distanceToEdge);
            Vector3 stopPoint = GetPointAlongLanePath(GetDistanceToIntersectionStopLine(distanceToEdge));

            Gizmos.color = intersectionGizmoColor;
            Gizmos.DrawLine(carPosition + Vector3.up * 0.45f, edgePoint + Vector3.up * 0.45f);
            Gizmos.DrawWireSphere(edgePoint + Vector3.up * 0.45f, 0.45f);

            Gizmos.color = stopLineGizmoColor;
            Gizmos.DrawWireSphere(stopPoint + Vector3.up * 0.55f, 0.6f);
            DrawStopLine(stopPoint, upcoming);
        }

        private Vector3 GetPointAlongLanePath(float targetDistance)
        {
            if (path.Count < 2)
                return vehicleRB ? vehicleRB.position : transform.position;

            targetDistance = Mathf.Max(0f, targetDistance);

            Vector3 previous = ProjectXZ(vehicleRB ? vehicleRB.position : transform.position);
            float travelled = 0f;

            int maxIndex = Mathf.Min(path.Count - 1, pathIndex + 80);
            for (int i = pathIndex; i <= maxIndex; i++)
            {
                Vector3 point = ProjectXZ(LanePos(i));
                float segmentLength = Vector3.Distance(previous, point);

                if (travelled + segmentLength >= targetDistance)
                {
                    float t = segmentLength <= 0.001f ? 0f : (targetDistance - travelled) / segmentLength;
                    Vector3 result = Vector3.Lerp(previous, point, Mathf.Clamp01(t));
                    return new Vector3(result.x, transform.position.y, result.z);
                }

                travelled += segmentLength;
                previous = point;
            }

            Vector3 fallback = LanePos(maxIndex);
            return new Vector3(fallback.x, transform.position.y, fallback.z);
        }

        private void DrawStopLine(Vector3 stopPoint, Intersection intersection)
        {
            Vector3 direction = intersection
                ? ProjectXZ(intersection.transform.position - stopPoint).normalized
                : ProjectXZ(transform.forward).normalized;

            if (direction.sqrMagnitude < 0.0001f)
                direction = ProjectXZ(transform.forward).normalized;

            if (direction.sqrMagnitude < 0.0001f)
                direction = Vector3.forward;

            Vector3 right = Vector3.Cross(Vector3.up, direction).normalized;
            float halfWidth = Mathf.Max(1.5f, Mathf.Abs(laneOffset) * 1.25f);

            Gizmos.DrawLine(stopPoint - right * halfWidth + Vector3.up * 0.08f, stopPoint + right * halfWidth + Vector3.up * 0.08f);
        }

        private static void DrawWireCircleXZ(Vector3 center, float radius, int segments)
        {
            if (radius <= 0f) return;

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

        private static Vector3 ProjectXZ(Vector3 value)
        {
            return new Vector3(value.x, 0f, value.z);
        }

        private struct IntersectionApproach
        {
            public Vector3 position;
            public Vector3 forward;
            public Vector3 right;
            public int laneSign;
        }

        private static class IntersectionGate
        {
            private class State
            {
                public readonly List<TrafficCarOld> currentCars = new();
                public readonly List<Entry> queue = new();
                public bool hasActiveApproach;
                public IntersectionApproach activeApproach;
            }

            private struct Entry
            {
                public TrafficCarOld car;
                public float requestTime;
                public float distance;
                public IntersectionApproach approach;
            }

            private static readonly Dictionary<Intersection, State> states = new();

            public static bool Request(Intersection intersection, TrafficCarOld car, float now, float distance, IntersectionApproach approach)
            {
                if (!intersection || !car) return false;

                if (!states.TryGetValue(intersection, out State state))
                {
                    state = new State();
                    states.Add(intersection, state);
                }

                Cleanup(intersection, state, now);

                if (state.currentCars.Count == 0)
                {
                    state.currentCars.Add(car);
                    state.activeApproach = approach;
                    state.hasActiveApproach = true;
                    RemoveFromQueue(state, car);
                    return true;
                }

                if (state.currentCars.Contains(car))
                    return true;

                if (state.hasActiveApproach && IsSameLaneGroup(state.activeApproach, approach, car))
                {
                    state.currentCars.Add(car);
                    RemoveFromQueue(state, car);
                    return true;
                }

                int index = IndexOf(state, car);
                if (index < 0)
                {
                    state.queue.Add(new Entry
                    {
                        car = car,
                        requestTime = now,
                        distance = distance,
                        approach = approach
                    });
                }
                else
                {
                    Entry entry = state.queue[index];
                    entry.distance = distance;
                    entry.approach = approach;
                    state.queue[index] = entry;
                }

                return false;
            }

            public static void Release(Intersection intersection, TrafficCarOld car, float now)
            {
                if (!intersection || !car) return;
                if (!states.TryGetValue(intersection, out State state)) return;

                Cleanup(intersection, state, now);

                if (!state.currentCars.Remove(car))
                {
                    RemoveFromQueue(state, car);
                    return;
                }

                if (state.currentCars.Count > 0)
                    return;

                state.hasActiveApproach = false;
                Cleanup(intersection, state, now);

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

                state.currentCars.Add(next.car);
                state.activeApproach = next.approach;
                state.hasActiveApproach = true;
                state.queue.RemoveAt(nextIndex);

                for (int i = state.queue.Count - 1; i >= 0; i--)
                {
                    Entry entry = state.queue[i];
                    if (IsSameLaneGroup(state.activeApproach, entry.approach, entry.car))
                    {
                        state.currentCars.Add(entry.car);
                        state.queue.RemoveAt(i);
                    }
                }
            }

            private static bool IsSameLaneGroup(IntersectionApproach active, IntersectionApproach incoming, TrafficCarOld incomingCar)
            {
                Vector3 activeForward = active.forward.sqrMagnitude > 0.0001f ? active.forward.normalized : Vector3.forward;
                Vector3 incomingForward = incoming.forward.sqrMagnitude > 0.0001f ? incoming.forward.normalized : Vector3.forward;
                float forwardDot = Vector3.Dot(activeForward, incomingForward);

                if (forwardDot < incomingCar.sameLaneForwardDot)
                    return false;

                if (active.laneSign != incoming.laneSign)
                    return false;

                Vector3 activeRight = active.right.sqrMagnitude > 0.0001f ? active.right.normalized : Vector3.Cross(Vector3.up, activeForward).normalized;
                Vector3 offset = incoming.position - active.position;
                float lateralDistance = Mathf.Abs(Vector3.Dot(offset, activeRight));

                return lateralDistance <= Mathf.Max(0.1f, incomingCar.sameLaneMaxLateralDistance);
            }

            private static void Cleanup(Intersection intersection, State state, float now)
            {
                for (int i = state.currentCars.Count - 1; i >= 0; i--)
                {
                    TrafficCarOld car = state.currentCars[i];
                    if (!car || !car.enabled || !car.gameObject.activeInHierarchy || car.IsStaleIntersectionBlocker(intersection, now))
                        state.currentCars.RemoveAt(i);
                }

                for (int i = state.queue.Count - 1; i >= 0; i--)
                {
                    TrafficCarOld car = state.queue[i].car;
                    if (!car || !car.enabled || !car.gameObject.activeInHierarchy)
                        state.queue.RemoveAt(i);
                }

                if (state.currentCars.Count == 0)
                    state.hasActiveApproach = false;
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
