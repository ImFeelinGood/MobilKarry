using System.Collections.Generic;
using UnityEngine;
using Barmetler.RoadSystem;

namespace Barmetler.RoadSystem.Traffic
{
    [RequireComponent(typeof(Rigidbody))]
    public class TrafficCar : MonoBehaviour
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

        [Header("Stability (Optional)")]
        public Transform centerOfMass;
        public float angularDrag = 1.0f;

        [Header("Lane")]
        public DriveSide driveSide = DriveSide.LeftHandTraffic;
        [Tooltip("Meters offset from road centerline.")]
        public float laneOffset = 1.75f;

        [Header("Path Generation")]
        [Tooltip("Smaller = smoother path, more points.")]
        public float pathStepSize = 1.0f;
        public float minDistanceToRoadToConnect = 10f;
        public float minDistanceYScale = 1f;

        [Header("Repath By Steps")]
        public int repathEverySteps = 30;
        private int stepsSinceRepath;
        private int lastObservedPathIndex;

        [Header("Follow")]
        public float waypointReachDistance = 2.0f;
        public float lookAheadDistance = 12f;
        public float repathIfOffPathDistance = 10f;
        public float repathCooldown = 1.0f;

        [Header("Traffic Speed (m/s)")]
        public float speedLimit = 10f; // 10 m/s = 36 km/h
        public float minCreepSpeed = 1.0f;

        [Header("Intersection Slowdown (LookAhead-based)")]
        public float intersectionDetectPadding = 1.0f;           // extra meters added to intersection radius for detection
        [Range(0.1f, 1f)] public float intersectionMinSpeedFactor = 0.5f; // speed factor when right before intersection

        [Header("Physics Capability")]
        [Tooltip("Vehicle max capability in km/h (should be >= speedLimit*3.6).")]
        public float maxForwardSpeed = 130f; // km/h
        public float horsePower = 550f;
        public float brakePower = 2000f;
        public float handbrakeForce = 3000f;
        public float maxSteerAngle = 40f;
        public float steeringSpeed = 5f;
        public DriveTypes driveType = DriveTypes.RWD;
        public bool isStarted = true;

        [Header("Following Distance")]
        public LayerMask vehicleMask;
        public float detectDistance = 14f;
        public float detectRadius = 0.9f;
        public float minGap = 3f;
        public float slowDownGap = 10f;

        [Header("Stop Before Turn")]
        public bool stopOnlyOnTurn = true;
        public float turnAngleThreshold = 25f;
        public float stopBeforeTurnDistance = 3.5f;
        public float minStopTime = 0.35f;

        [Header("Slow Down Before Turn")]
        public float turnSlowdownDistance = 12f;                 // meters before the turn to start slowing
        [Range(0.1f, 1f)] public float turnMinSpeedFactor = 0.5f; // speed factor at the turn entry
        public float turnAngleForMaxSlowdown = 90f;              // degrees, bigger = stronger slowdown

        [Header("Stop Planning Decel (m/s^2)")]
        public float brakeDecel = 10f;
        private bool stopCommitted;

        [Header("Goal / Routing")]
        public bool autoPickGoals = true;
        public float goalReachedDistance = 4f;

        [Header("Debug Gizmos")]
        public bool drawPathGizmos = true;
        public bool drawCenterline = true;
        public bool drawLaneLine = true;
        public float gizmoPointSize = 0.18f;
        public int gizmoMaxPoints = 400;

        // runtime path
        private readonly List<Bezier.OrientedPoint> path = new();
        private int pathIndex;
        private Vector3 goalWorld;
        private float repathTimer;

        // controls
        private float throttle01;
        private float brake01;
        private float handbrake01;
        private float targetSteerAngle;
        private float currentSteerAngle;
        private float currentSpeedKmh;

        // stop plan
        private bool hasStopPlan;
        private Vector3 plannedStopPos;
        private float stopTimer;
        private bool allowedToGoAfterStop;
        private float plannedTurnAngle;

        public bool IsDestroyedOrDisabled => !this || !enabled || !gameObject.activeInHierarchy;

        // -------------------------
        // Unity
        // -------------------------
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

            if (autoPickGoals)
                PickNewGoalAndRepath();
        }

        private void FixedUpdate()
        {
            if (!roadSystem) return;
            if (path.Count < 2) return;

            currentSpeedKmh = Vector3.Dot(transform.forward, vehicleRB.linearVelocity) * 3.6f;

            repathTimer -= Time.fixedDeltaTime;

            // keep index stable + forward
            ResyncPathIndexToNearestForward();
            AdvancePathIndex();

            // count progressed path indices as "steps"
            int delta = pathIndex - lastObservedPathIndex;
            if (delta > 0)
            {
                stepsSinceRepath += delta;
                lastObservedPathIndex = pathIndex;
            }

            // repath every N steps (respect repathCooldown via repathTimer)
            if (repathTimer <= 0f && stepsSinceRepath >= repathEverySteps)
            {
                Repath(goalWorld);
                repathTimer = repathCooldown;
                // Repath() already resets step counters
            }

            // repath if physics drifted away
            if (repathTimer <= 0f && DistanceToLanePointXZ(pathIndex) > repathIfOffPathDistance)
            {
                Repath(goalWorld);
                repathTimer = repathCooldown;
            }

            // stop-plan for turns
            if (!hasStopPlan && stopOnlyOnTurn)
                TryPlanTurnStopAhead();

            // desired speed (m/s)
            float desiredSpeed = speedLimit;
            desiredSpeed = Mathf.Min(desiredSpeed, GetSpeedLimitFromCarAhead());
            desiredSpeed = Mathf.Min(desiredSpeed, GetSpeedLimitFromStopPlan());
            desiredSpeed = Mathf.Min(desiredSpeed, speedLimit * GetLookAheadIntersectionSpeedFactor());

            // lookahead target point
            Vector3 lookPoint = GetLookAheadPoint();

            // convert to steering angle
            targetSteerAngle = ComputeTargetSteerAngle(lookPoint);

            // convert speed target -> throttle/brake
            ComputeThrottleBrake(desiredSpeed, out throttle01, out brake01);

            // apply physics
            ApplyMotor();
            ApplyBrakes();
            ApplyHandbrake();
            ApplySteering();

            UpdateWheelVisuals();

            // goal reached
            if (autoPickGoals && Vector3.Distance(vehicleRB.position, goalWorld) <= goalReachedDistance)
                PickNewGoalAndRepath();
        }

        // -------------------------
        // Path / Goal
        // -------------------------

        private float GetLookAheadIntersectionSpeedFactor()
        {
            if (!roadSystem || roadSystem.Intersections == null) return 1f;
            if (path == null || path.Count < 2) return 1f;

            float scanDist = Mathf.Max(1f, lookAheadDistance);

            int start = Mathf.Clamp(pathIndex, 0, path.Count - 2);
            int end = Mathf.Min(path.Count - 2, start + 250);

            float bestApproach = float.PositiveInfinity; // along-path distance to the first detected intersection

            float accum = 0f;

            for (int i = start; i <= end; i++)
            {
                Vector3 a = path[i].position; a.y = 0f;
                Vector3 b = path[i + 1].position; b.y = 0f;

                Vector3 ab = b - a;
                float abSqr = ab.sqrMagnitude;
                if (abSqr < 0.0001f) continue;

                float segLen = Mathf.Sqrt(abSqr);

                // stop scanning if beyond lookAheadDistance and we already have a candidate
                if (accum > scanDist && !float.IsPositiveInfinity(bestApproach))
                    break;

                foreach (var inter in roadSystem.Intersections)
                {
                    if (!inter) continue;

                    Vector3 c = inter.transform.position; c.y = 0f;
                    float r = Mathf.Max(0.1f, inter.Radius) + intersectionDetectPadding;

                    // closest point on segment to intersection center
                    float t = Mathf.Clamp01(Vector3.Dot(c - a, ab) / abSqr);
                    Vector3 closest = a + ab * t;

                    float distToCenter = Vector3.Distance(closest, c);

                    // Detect if this path segment passes through/near the intersection
                    if (distToCenter <= r)
                    {
                        float approach = accum + segLen * t; // along-path distance from current segment start
                        if (approach < bestApproach) bestApproach = approach;
                    }
                }

                accum += segLen;

                // early out if found something very close
                if (bestApproach <= 0.2f) break;
            }

            if (float.IsPositiveInfinity(bestApproach)) return 1f;

            // Only slow down if intersection is within lookAheadDistance
            if (bestApproach >= scanDist) return 1f;

            float u = Mathf.Clamp01(bestApproach / scanDist); // 0 near, 1 far
            return Mathf.Lerp(intersectionMinSpeedFactor, 1f, u);
        }

        private void PickNewGoalAndRepath()
        {
            var intersections = roadSystem.Intersections;
            if (intersections == null || intersections.Length == 0) return;

            for (int tries = 0; tries < 15; tries++)
            {
                var inter = intersections[Random.Range(0, intersections.Length)];
                if (!inter || inter.AnchorPoints == null || inter.AnchorPoints.Length == 0) continue;

                var anch = inter.AnchorPoints[Random.Range(0, inter.AnchorPoints.Length)];
                if (!anch) continue;

                goalWorld = anch.transform.position;
                Repath(goalWorld);
                if (path.Count >= 2) return;
            }
        }

        private void Repath(Vector3 goal)
        {
            // NEW: use current path point as start to avoid ambiguous “closest anchor” near intersections
            Vector3 startPos = vehicleRB.position;
            if (path.Count > 0)
            {
                int idx = Mathf.Clamp(pathIndex, 0, path.Count - 1);
                startPos = path[idx].position; // centerline point on the graph
            }

            path.Clear();
            pathIndex = 0;

            hasStopPlan = false;
            allowedToGoAfterStop = false;

            var newPath = roadSystem.FindPath(
                startPos,                 // CHANGED (was vehicleRB.position)
                goal,
                yScale: minDistanceYScale,
                stepSize: Mathf.Max(0.1f, pathStepSize),
                minDstToRoadToConnect: minDistanceToRoadToConnect
            );

            // OPTIONAL (recommended): if it still fails, rebuild graph and retry once
            if (newPath == null || newPath.Count < 2)
            {
                roadSystem.ConstructGraph();
                newPath = roadSystem.FindPath(
                    startPos,
                    goal,
                    yScale: minDistanceYScale,
                    stepSize: Mathf.Max(0.1f, pathStepSize),
                    minDstToRoadToConnect: minDistanceToRoadToConnect
                );
            }

            if (newPath != null) path.AddRange(newPath);
        }

        private void AdvancePathIndex()
        {
            // move forward while close and not behind
            for (int i = 0; i < 8; i++)
            {
                if (pathIndex >= path.Count - 1) break;

                Vector3 p = LanePos(pathIndex);
                Vector3 to = p - vehicleRB.position; to.y = 0f;

                if (to.sqrMagnitude > 0.01f)
                {
                    Vector3 fwd = transform.forward; fwd.y = 0f; fwd.Normalize();
                    if (Vector3.Dot(fwd, to.normalized) < -0.1f)
                    {
                        pathIndex++;
                        continue;
                    }
                }

                if (to.magnitude <= waypointReachDistance)
                    pathIndex++;
                else
                    break;
            }
        }

        private void ResyncPathIndexToNearestForward()
        {
            if (path.Count == 0) return;

            int baseIdx = Mathf.Clamp(pathIndex, 0, path.Count - 1);
            int start = Mathf.Max(0, baseIdx - 3);
            int end = Mathf.Min(path.Count - 1, baseIdx + 70);

            Vector3 pos = vehicleRB.position; pos.y = 0f;
            Vector3 fwd = transform.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
            fwd.Normalize();

            int best = baseIdx;
            float bestSqr = float.PositiveInfinity;

            for (int i = start; i <= end; i++)
            {
                Vector3 p = LanePos(i); p.y = 0f;
                Vector3 to = p - pos;

                if (to.sqrMagnitude > 0.01f && Vector3.Dot(fwd, to.normalized) < 0.05f)
                    continue; // ignore behind

                float sqr = to.sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = i;
                }
            }

            pathIndex = best;
        }

        private Vector3 GetLookAheadPoint()
        {
            if (hasStopPlan && !allowedToGoAfterStop)
                return plannedStopPos;

            float dist = 0f;
            Vector3 last = vehicleRB.position;

            for (int i = pathIndex; i < path.Count; i++)
            {
                Vector3 p = LanePos(i);

                Vector3 to = p - vehicleRB.position; to.y = 0f;
                if (to.sqrMagnitude > 0.01f)
                {
                    Vector3 fwd = transform.forward; fwd.y = 0f; fwd.Normalize();
                    if (Vector3.Dot(fwd, to.normalized) < 0.05f)
                        continue; // skip behind
                }

                dist += Vector3.Distance(last, p);
                last = p;

                if (dist >= lookAheadDistance || i == path.Count - 1)
                    return p;
            }

            return LanePos(path.Count - 1);
        }

        // -------------------------
        // Stable Lane Position (NO FLIP)
        // -------------------------
        private Vector3 CenterPos(int i) => path[i].position;

        private Vector3 LanePos(int i)
        {
            i = Mathf.Clamp(i, 0, path.Count - 1);

            Vector3 p = CenterPos(i);

            Vector3 dir;
            if (i < path.Count - 1) dir = CenterPos(i + 1) - p;
            else dir = p - CenterPos(i - 1);

            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
            dir.Normalize();

            Vector3 right = Vector3.Cross(Vector3.up, dir).normalized;

            // continuity: prevent sudden sign flips of right-vector
            if (i > 0)
            {
                Vector3 prevDir = CenterPos(i) - CenterPos(i - 1);
                prevDir.y = 0f;
                if (prevDir.sqrMagnitude > 0.0001f)
                {
                    prevDir.Normalize();
                    Vector3 prevRight = Vector3.Cross(Vector3.up, prevDir).normalized;
                    if (Vector3.Dot(right, prevRight) < 0f)
                        right = -right;
                }
            }

            float sign = (driveSide == DriveSide.RightHandTraffic) ? 1f : -1f;
            return p + right * (laneOffset * sign);
        }

        private float DistanceToLanePointXZ(int i)
        {
            Vector3 a = vehicleRB.position; a.y = 0f;
            Vector3 b = LanePos(i); b.y = 0f;
            return Vector3.Distance(a, b);
        }

        // -------------------------
        // Traffic Rules
        // -------------------------
        private float GetSpeedLimitFromCarAhead()
        {
            Vector3 origin = vehicleRB.position + Vector3.up * 0.8f;
            Vector3 dir = transform.forward;

            if (Physics.SphereCast(origin, detectRadius, dir, out RaycastHit hit, detectDistance, vehicleMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.rigidbody == vehicleRB) return speedLimit;

                float d = hit.distance;
                if (d <= minGap) return 0f;
                if (d <= slowDownGap) return speedLimit * Mathf.Clamp01(d / slowDownGap);
            }

            return speedLimit;
        }

        private void TryPlanTurnStopAhead()
        {
            int maxAhead = Mathf.Min(path.Count - 2, pathIndex + 40);

            for (int i = Mathf.Max(pathIndex + 2, 2); i < maxAhead; i++)
            {
                Vector3 a = CenterPos(i - 1);
                Vector3 b = CenterPos(i);
                Vector3 c = CenterPos(i + 1);

                Vector3 dirIn = (b - a); dirIn.y = 0f;
                Vector3 dirOut = (c - b); dirOut.y = 0f;

                if (dirIn.sqrMagnitude < 0.001f || dirOut.sqrMagnitude < 0.001f) continue;

                float angle = Vector3.Angle(dirIn, dirOut);
                if (angle < turnAngleThreshold) continue;

                // stop point: before b along incoming direction, with lane offset stable
                dirIn.Normalize();
                Vector3 right = Vector3.Cross(Vector3.up, dirIn).normalized;

                float sign = (driveSide == DriveSide.RightHandTraffic) ? 1f : -1f;

                Vector3 stopCenter = b - dirIn * stopBeforeTurnDistance;
                Vector3 stopLane = stopCenter + right * (laneOffset * sign);

                float dst = Vector3.Distance(ProjectXZ(vehicleRB.position), ProjectXZ(stopLane));
                if (dst <= Mathf.Max(lookAheadDistance * 2f, 12f))
                {
                    hasStopPlan = true;
                    plannedStopPos = stopLane;     // keep this as "turn entry point"
                    plannedTurnAngle = angle;      // NEW: store turn sharpness
                    break;
                }
            }
        }

        private float GetSpeedLimitFromStopPlan()
        {
            if (!hasStopPlan) return speedLimit;

            // If we passed the planned turn point, clear plan so next turn can be planned
            Vector3 toTurn = plannedStopPos - vehicleRB.position;
            toTurn.y = 0f;

            Vector3 fwd = transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
            fwd.Normalize();

            if (toTurn.sqrMagnitude > 4f && Vector3.Dot(fwd, toTurn.normalized) < 0f)
            {
                hasStopPlan = false;
                return speedLimit;
            }

            float dst = Vector3.Distance(ProjectXZ(vehicleRB.position), ProjectXZ(plannedStopPos));

            // Only slow down when we're within the slowdown distance
            if (dst >= turnSlowdownDistance) return speedLimit;

            float u = Mathf.Clamp01(dst / turnSlowdownDistance);                 // 0 near, 1 far
            float distFactor = Mathf.Lerp(turnMinSpeedFactor, 1f, u);            // slow -> normal

            float angleT = Mathf.Clamp01(plannedTurnAngle / turnAngleForMaxSlowdown);
            float angleFactor = Mathf.Lerp(1f, turnMinSpeedFactor, angleT);      // sharper -> slower

            float factor = Mathf.Min(distFactor, angleFactor);

            return speedLimit * factor; // NEVER returns 0, so it will always commit to turning
        }

        private static Vector3 ProjectXZ(Vector3 v) => new Vector3(v.x, 0f, v.z);

        // -------------------------
        // Control Conversion
        // -------------------------
        private void ComputeThrottleBrake(float desiredSpeedMs, out float t01, out float b01)
        {
            float desiredKmh = desiredSpeedMs * 3.6f;
            float currentAbsKmh = Mathf.Abs(currentSpeedKmh);

            if (!isStarted)
            {
                t01 = 0f; b01 = 1f;
                return;
            }

            if (desiredKmh <= 0.1f)
            {
                t01 = 0f; b01 = 1f;
                return;
            }

            float error = desiredKmh - currentAbsKmh;

            if (error > 1.2f)
            {
                b01 = 0f;
                t01 = Mathf.Clamp01(error / 25f);
            }
            else if (error < -1.2f)
            {
                t01 = 0f;
                b01 = Mathf.Clamp01((-error) / 18f);
            }
            else
            {
                t01 = 0f;
                b01 = 0f;
            }

            // prevent stuck creep when we want to move
            if (desiredSpeedMs > minCreepSpeed && currentAbsKmh < 2f && b01 < 0.05f)
                t01 = Mathf.Max(t01, 0.2f);
        }

        private float ComputeTargetSteerAngle(Vector3 worldPoint)
        {
            Vector3 local = transform.InverseTransformPoint(worldPoint);
            local.y = 0f;

            if (local.sqrMagnitude < 0.001f) return 0f;

            float angleDeg = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            return Mathf.Clamp(angleDeg, -maxSteerAngle, maxSteerAngle);
        }

        // -------------------------
        // WheelCollider Physics (Ezereal-style)
        // -------------------------
        private void ApplyMotor()
        {
            float absSpeed = Mathf.Abs(currentSpeedKmh);
            float speedFactor = Mathf.InverseLerp(0f, maxForwardSpeed, absSpeed);
            float currentMotorTorque = Mathf.Lerp(horsePower, 0f, speedFactor);

            if (!isStarted || brake01 > 0.05f || throttle01 <= 0.001f || absSpeed >= maxForwardSpeed)
            {
                SetAllMotorTorque(0f);
                return;
            }

            float t = currentMotorTorque * throttle01;

            if (driveType == DriveTypes.RWD)
            {
                rearLeftWheelCollider.motorTorque = t;
                rearRightWheelCollider.motorTorque = t;
                frontLeftWheelCollider.motorTorque = 0f;
                frontRightWheelCollider.motorTorque = 0f;
            }
            else if (driveType == DriveTypes.FWD)
            {
                frontLeftWheelCollider.motorTorque = t;
                frontRightWheelCollider.motorTorque = t;
                rearLeftWheelCollider.motorTorque = 0f;
                rearRightWheelCollider.motorTorque = 0f;
            }
            else
            {
                frontLeftWheelCollider.motorTorque = t;
                frontRightWheelCollider.motorTorque = t;
                rearLeftWheelCollider.motorTorque = t;
                rearRightWheelCollider.motorTorque = t;
            }
        }

        private void ApplyBrakes()
        {
            float bt = brake01 * brakePower;

            frontLeftWheelCollider.brakeTorque = bt;
            frontRightWheelCollider.brakeTorque = bt;

            // slight rear bias
            rearLeftWheelCollider.brakeTorque = bt * 0.75f;
            rearRightWheelCollider.brakeTorque = bt * 0.75f;
        }

        private void ApplyHandbrake()
        {
            if (handbrake01 <= 0.001f) return;

            rearLeftWheelCollider.motorTorque = 0f;
            rearRightWheelCollider.motorTorque = 0f;

            float hb = handbrake01 * handbrakeForce;
            rearLeftWheelCollider.brakeTorque = Mathf.Max(rearLeftWheelCollider.brakeTorque, hb);
            rearRightWheelCollider.brakeTorque = Mathf.Max(rearRightWheelCollider.brakeTorque, hb);
        }

        private void ApplySteering()
        {
            float absSpeed = Mathf.Abs(currentSpeedKmh);

            // reduce steering at high speed
            float highSpeedFactor = Mathf.InverseLerp(20f, maxForwardSpeed, absSpeed);
            float adjusted = targetSteerAngle * (1f - highSpeedFactor);

            currentSteerAngle = Mathf.Lerp(currentSteerAngle, adjusted, Time.fixedDeltaTime * steeringSpeed);

            frontLeftWheelCollider.steerAngle = currentSteerAngle;
            frontRightWheelCollider.steerAngle = currentSteerAngle;
        }

        private void SetAllMotorTorque(float v)
        {
            frontLeftWheelCollider.motorTorque = v;
            frontRightWheelCollider.motorTorque = v;
            rearLeftWheelCollider.motorTorque = v;
            rearRightWheelCollider.motorTorque = v;
        }

        private void UpdateWheelVisuals()
        {
            UpdateWheel(frontLeftWheelCollider, frontLeftWheelMesh);
            UpdateWheel(frontRightWheelCollider, frontRightWheelMesh);
            UpdateWheel(rearLeftWheelCollider, rearLeftWheelMesh);
            UpdateWheel(rearRightWheelCollider, rearRightWheelMesh);
        }

        private void UpdateWheel(WheelCollider col, Transform mesh)
        {
            if (!mesh) return;
            col.GetWorldPose(out Vector3 pos, out Quaternion rot);
            mesh.SetPositionAndRotation(pos, rot);
        }
#if UNITY_EDITOR
        private void Update()
        {
            if (Application.isPlaying)
                UnityEditor.SceneView.RepaintAll();
        }

        private void OnDrawGizmos()
        {
            if (!drawPathGizmos) return;
            if (path == null || path.Count < 2) return;

            int count = Mathf.Min(path.Count, gizmoMaxPoints);

            if (drawCenterline)
            {
                Gizmos.color = new Color(1f, 1f, 1f, 0.35f);
                for (int i = 0; i < count - 1; i++)
                {
                    Gizmos.DrawLine(CenterPos(i), CenterPos(i + 1));
                }
            }

            if (drawLaneLine)
            {
                int startIdx = 0;

                if (Application.isPlaying)
                    startIdx = Mathf.Clamp(pathIndex, 0, count - 1);

                // (optional) draw travelled part faint
                if (Application.isPlaying && startIdx > 0)
                {
                    Gizmos.color = new Color(0f, 1f, 0.6f, 0.15f);
                    for (int i = 0; i < startIdx - 1; i++)
                        Gizmos.DrawLine(LanePos(i), LanePos(i + 1));
                }

                // draw remaining path bold
                Gizmos.color = new Color(0f, 1f, 0.6f, 0.85f);
                for (int i = startIdx; i < count; i++)
                {
                    Vector3 p = LanePos(i);
                    Gizmos.DrawSphere(p, gizmoPointSize);
                    if (i < count - 1) Gizmos.DrawLine(p, LanePos(i + 1));
                }
            }

            if (Application.isPlaying)
            {
                int idx = Mathf.Clamp(pathIndex, 0, path.Count - 1);

                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere(LanePos(idx), gizmoPointSize * 1.8f);

                Vector3 look = GetLookAheadPoint();
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(look, gizmoPointSize * 2.2f);
                Gizmos.DrawLine(transform.position + Vector3.up * 0.2f, look + Vector3.up * 0.2f);

                if (hasStopPlan && !allowedToGoAfterStop)
                {
                    Gizmos.color = new Color(1f, 0.7f, 0f, 1f);
                    Gizmos.DrawWireSphere(plannedStopPos, gizmoPointSize * 2.2f);
                }
            }
#endif
        }
    }
}