using System.Collections.Generic;
using UnityEngine;
using Barmetler.RoadSystem;
using Barmetler.RoadSystem.Traffic;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(RoadSystemNavigator))]
public class TrafficAIController : MonoBehaviour
{
    public enum TrafficDriveType
    {
        FWD,
        RWD,
        AWD
    }

    [Header("Steering Smoothness")]
    [SerializeField] private bool useDynamicLookAhead = true;

    [SerializeField] private int minLookAheadPointIndex = 5;
    [SerializeField] private int maxLookAheadPointIndex = 14;

    [SerializeField] private float laneTargetSmoothSpeed = 6f;
    [SerializeField] private float steerInputSmoothSpeed = 4f;

    [SerializeField] private float maxSteerInputPerFrame = 0.08f;

    [SerializeField] private float turnSteeringMultiplier = 0.65f;
    [SerializeField] private float intersectionSteeringMultiplier = 0.55f;

    private Vector3 smoothedLaneTarget;
    private bool hasSmoothedLaneTarget;

    private float smoothedSteerInput;

    [Header("Intersection Controller")]
    [SerializeField] private bool autoAddIntersectionController = true;

    [SerializeField] private float intersectionPathDetectPadding = 4f;

    [SerializeField] private float intersectionReleasePadding = 6f;

    private IntersectionTrafficController reservedIntersectionController;
    private IntersectionTrafficController debugUpcomingIntersectionController;
    private float debugIntersectionDistance;

    public bool IsDestroyedOrDisabled => this == null || !isActiveAndEnabled || !gameObject.activeInHierarchy;

    [Header("Road References")]
    [SerializeField] private RoadSystem roadSystem;
    [SerializeField] private RoadSystemNavigator navigator;

    [Header("Wheel Colliders")]
    [SerializeField] private WheelCollider frontLeftWheelCollider;
    [SerializeField] private WheelCollider frontRightWheelCollider;
    [SerializeField] private WheelCollider rearLeftWheelCollider;
    [SerializeField] private WheelCollider rearRightWheelCollider;

    [Header("Wheel Meshes")]
    [SerializeField] private Transform frontLeftWheelMesh;
    [SerializeField] private Transform frontRightWheelMesh;
    [SerializeField] private Transform rearLeftWheelMesh;
    [SerializeField] private Transform rearRightWheelMesh;

    [Header("Driving Side")]
    [Tooltip("False = left side driving. True = right side driving.")]
    [SerializeField] private bool rightHandDriving = false;

    [SerializeField] private float laneOffset = 2.2f;

    [Header("Vehicle Movement")]
    [SerializeField] private TrafficDriveType driveType = TrafficDriveType.RWD;

    [SerializeField] private float maxForwardSpeed = 80f;
    [SerializeField] private float maxReverseSpeed = 25f;
    [SerializeField] private float horsePower = 550f;
    [SerializeField] private float brakePower = 2000f;
    [SerializeField] private float handbrakeForce = 3000f;
    [SerializeField] private float maxSteerAngle = 40f;
    [SerializeField] private float steeringSpeed = 5f;
    [SerializeField] private float decelerationSpeed = 0.5f;
    [SerializeField] private float directionChangeThreshold = 1f;
    [SerializeField] private float stopThreshold = 5f;

    [Header("AI Speed")]
    [SerializeField] private Vector2 randomSpeedRangeKmh = new Vector2(25f, 55f);
    [SerializeField] private float normalAccelerationInput = 1f;
    [SerializeField] private float normalBrakeInput = 1f;
    [SerializeField] private float speedTolerance = 2f;

    [Header("Path Following")]
    [SerializeField] private int lookAheadPointIndex = 6;
    [SerializeField] private float steeringSensitivity = 2.5f;
    [SerializeField] private float goalRefreshDistance = 20f;
    [SerializeField] private float roadPointSpacing = 5f;

    [Header("Maintain Distance")]
    [SerializeField] private LayerMask trafficCarLayer;
    [SerializeField] private float frontCheckDistance = 15f;
    [SerializeField] private float frontCheckRadius = 1.5f;
    [SerializeField] private float maintainDistance = 10f;

    [Header("Intersection Detection")]
    [SerializeField] private float intersectionLookAheadDistance = 28f;
    [SerializeField] private float intersectionSlowdownDistance = 22f;
    [SerializeField] private float intersectionStopDistance = 10f;
    [SerializeField] private float intersectionCheckExtraRadius = 3f;
    [SerializeField] private float intersectionClearDelay = 0.75f;

    [Header("Intersection Speed")]
    [SerializeField] private float intersectionSlowSpeedKmh = 16f;
    [SerializeField] private float intersectionStopSpeedKmh = 0f;

    [Header("Turn Detection")]
    [SerializeField] private float turnDetectAngle = 35f;
    [SerializeField] private float turnSlowdownDistance = 18f;
    [SerializeField] private float turnTargetSpeedKmh = 18f;

    [Header("Debug")]
    [SerializeField] private bool drawDebug = true;

    public float CurrentSpeed => currentSpeed;
    public float AccelerationInput => currentAccelerationInput;
    public float BrakeInput => currentBrakeInput;
    public float SteerInput => targetSteerAngle;
    public bool IsStarted => isStarted;

    private Rigidbody vehicleRB;
    private WheelCollider[] wheels;

    private bool isStarted = true;
    private bool stationary = true;

    private float currentSpeed;
    private float currentAccelerationInput;
    private float currentBrakeInput;
    private float currentHandbrakeInput;

    private float currentSteerAngle;
    private float targetSteerAngle;

    private float targetSpeedKmh;

    private int closestPathIndex;
    private int targetPathIndex;

    private Intersection committedIntersection;
    private float committedIntersectionTimer;

    private void Awake()
    {
        vehicleRB = GetComponent<Rigidbody>();

        if (navigator == null)
            navigator = GetComponent<RoadSystemNavigator>();

        wheels = new WheelCollider[]
        {
            frontLeftWheelCollider,
            frontRightWheelCollider,
            rearLeftWheelCollider,
            rearRightWheelCollider
        };

        targetSpeedKmh = Random.Range(randomSpeedRangeKmh.x, randomSpeedRangeKmh.y);

        if (navigator != null && roadSystem != null)
            navigator.currentRoadSystem = roadSystem;
    }

    private void Start()
    {
        SetRandomGoal();
    }

    private void FixedUpdate()
    {
        UpdateCurrentSpeed();

        if (!isStarted)
        {
            ClearMotorTorque();
            SetFootBrake(brakePower);
            return;
        }

        UpdateAIInput();

        Acceleration();
        Braking();
        Handbraking();
        Steering();
        Slowdown();
        UpdateStationaryState();
        UpdateWheelRPMVisuals();
    }

    private int GetDynamicLookAheadIndex()
    {
        if (!useDynamicLookAhead)
            return lookAheadPointIndex;

        float speed01 = Mathf.InverseLerp(0f, maxForwardSpeed, Mathf.Abs(currentSpeed));

        return Mathf.RoundToInt(
            Mathf.Lerp(minLookAheadPointIndex, maxLookAheadPointIndex, speed01)
        );
    }

    private Vector3 GetSmoothedLaneTarget(Vector3 rawTargetPoint)
    {
        if (!hasSmoothedLaneTarget)
        {
            smoothedLaneTarget = rawTargetPoint;
            hasSmoothedLaneTarget = true;
            return smoothedLaneTarget;
        }

        smoothedLaneTarget = Vector3.Lerp(
            smoothedLaneTarget,
            rawTargetPoint,
            Time.fixedDeltaTime * laneTargetSmoothSpeed
        );

        return smoothedLaneTarget;
    }

    private float GetSmoothedSteerInput(float rawSteerInput)
    {
        float limitedSteerInput = Mathf.MoveTowards(
            smoothedSteerInput,
            rawSteerInput,
            maxSteerInputPerFrame
        );

        smoothedSteerInput = Mathf.Lerp(
            smoothedSteerInput,
            limitedSteerInput,
            Time.fixedDeltaTime * steerInputSmoothSpeed
        );

        return smoothedSteerInput;
    }

    private void UpdateAIInput()
    {
        if (navigator == null || roadSystem == null)
        {
            currentAccelerationInput = 0f;
            currentBrakeInput = 1f;
            targetSteerAngle = 0f;
            return;
        }

        if (navigator.CurrentPoints == null || navigator.CurrentPoints.Count < 2)
        {
            SetRandomGoal();

            currentAccelerationInput = 0f;
            currentBrakeInput = 1f;
            targetSteerAngle = 0f;
            return;
        }

        if (Vector3.Distance(transform.position, navigator.Goal) <= goalRefreshDistance)
        {
            SetRandomGoal();
        }

        List<Bezier.OrientedPoint> points = navigator.CurrentPoints;

        closestPathIndex = GetClosestPathIndex(points);

        int dynamicLookAhead = GetDynamicLookAheadIndex();
        targetPathIndex = Mathf.Clamp(closestPathIndex + dynamicLookAhead, 0, points.Count - 1);

        Vector3 rawTargetPoint = GetLanePoint(points, targetPathIndex);
        Vector3 targetPoint = GetSmoothedLaneTarget(rawTargetPoint);

        Vector3 localTarget = transform.InverseTransformPoint(targetPoint);

        float rawSteerInput = Mathf.Clamp(localTarget.x / Mathf.Max(2f, localTarget.z), -1f, 1f);
        rawSteerInput *= steeringSensitivity;
        rawSteerInput = Mathf.Clamp(rawSteerInput, -1f, 1f);

        float desiredSpeed = targetSpeedKmh;

        bool hasCarAhead = CheckCarAhead(out float carAheadDistance);
        bool hasTurnAhead = CheckTurnAhead(points, closestPathIndex, out float turnDistance);

        bool nearIntersection = debugUpcomingIntersectionController != null &&
                                debugIntersectionDistance <= intersectionSlowdownDistance;

        if (hasTurnAhead && turnDistance <= turnSlowdownDistance)
        {
            rawSteerInput *= turnSteeringMultiplier;
        }

        if (nearIntersection)
        {
            rawSteerInput *= intersectionSteeringMultiplier;
        }

        float steerInput = GetSmoothedSteerInput(rawSteerInput);

        targetSteerAngle = steerInput * maxSteerAngle;

        if (hasCarAhead)
        {
            float distanceFactor = Mathf.InverseLerp(2f, maintainDistance, carAheadDistance);
            desiredSpeed = Mathf.Lerp(0f, desiredSpeed, distanceFactor);
        }

        if (hasTurnAhead && turnDistance <= turnSlowdownDistance)
        {
            desiredSpeed = Mathf.Min(desiredSpeed, turnTargetSpeedKmh);
        }

        desiredSpeed = ApplyIntersectionLogic(desiredSpeed, points);

        ApplyAISpeedControl(desiredSpeed);
    }

    private float ApplyIntersectionLogic(float desiredSpeed, List<Bezier.OrientedPoint> points)
    {
        debugUpcomingIntersectionController = null;
        debugIntersectionDistance = float.MaxValue;

        ReleaseIntersectionIfExited();

        if (!TryGetIntersectionAheadByPath(
                points,
                closestPathIndex,
                out Intersection intersection,
                out IntersectionTrafficController controller,
                out float distanceToIntersection,
                out bool isInsideIntersection))
        {
            return desiredSpeed;
        }

        debugUpcomingIntersectionController = controller;
        debugIntersectionDistance = distanceToIntersection;

        if (controller == null)
            return desiredSpeed;

        bool alreadyReserved = reservedIntersectionController == controller;

        if (isInsideIntersection)
        {
            if (alreadyReserved || controller.TryAcquire(this))
            {
                reservedIntersectionController = controller;
                return Mathf.Min(desiredSpeed, intersectionSlowSpeedKmh);
            }

            return intersectionStopSpeedKmh;
        }

        if (distanceToIntersection <= intersectionSlowdownDistance)
        {
            desiredSpeed = Mathf.Min(desiredSpeed, intersectionSlowSpeedKmh);
        }

        if (distanceToIntersection <= intersectionStopDistance)
        {
            controller.Register(this);

            if (alreadyReserved || controller.TryAcquire(this))
            {
                reservedIntersectionController = controller;
                return Mathf.Min(desiredSpeed, intersectionSlowSpeedKmh);
            }

            return intersectionStopSpeedKmh;
        }

        return desiredSpeed;
    }

    private bool TryGetIntersectionAheadByPath(
        List<Bezier.OrientedPoint> points,
        int startIndex,
        out Intersection nearestIntersection,
        out IntersectionTrafficController nearestController,
        out float distanceAlongPath,
        out bool isInsideIntersection)
    {
        nearestIntersection = null;
        nearestController = null;
        distanceAlongPath = float.MaxValue;
        isInsideIntersection = false;

        if (points == null || points.Count < 2)
            return false;

        Intersection[] intersections = GetAvailableIntersections();

        if (intersections == null || intersections.Length == 0)
            return false;

        Vector3 carPosition = transform.position;
        int safeStartIndex = Mathf.Clamp(startIndex, 0, points.Count - 1);

        foreach (Intersection intersection in intersections)
        {
            if (intersection == null)
                continue;

            float detectRadius = Mathf.Max(
                3f,
                intersection.Radius + intersectionCheckExtraRadius + intersectionPathDetectPadding
            );

            float flatDistanceToCar = FlatDistance(carPosition, intersection.transform.position);

            if (flatDistanceToCar <= detectRadius)
            {
                IntersectionTrafficController controller = GetOrCreateIntersectionController(intersection);

                nearestIntersection = intersection;
                nearestController = controller;
                distanceAlongPath = 0f;
                isInsideIntersection = true;
                return true;
            }

            float accumulatedDistance = 0f;

            for (int i = safeStartIndex; i < points.Count - 1; i++)
            {
                Vector3 currentPoint = points[i].position;
                Vector3 nextPoint = points[i + 1].position;

                float segmentLength = FlatDistance(currentPoint, nextPoint);
                accumulatedDistance += segmentLength;

                if (accumulatedDistance > intersectionLookAheadDistance)
                    break;

                float pointDistanceToIntersection = FlatDistance(nextPoint, intersection.transform.position);

                if (pointDistanceToIntersection <= detectRadius)
                {
                    if (accumulatedDistance < distanceAlongPath)
                    {
                        nearestIntersection = intersection;
                        nearestController = GetOrCreateIntersectionController(intersection);
                        distanceAlongPath = accumulatedDistance;
                        isInsideIntersection = false;
                    }

                    break;
                }
            }
        }

        return nearestIntersection != null;
    }

    private IntersectionTrafficController GetOrCreateIntersectionController(Intersection intersection)
    {
        if (intersection == null)
            return null;

        IntersectionTrafficController controller = intersection.GetComponent<IntersectionTrafficController>();

        if (controller == null && autoAddIntersectionController)
        {
            controller = intersection.gameObject.AddComponent<IntersectionTrafficController>();
        }

        return controller;
    }

    private Intersection[] GetAvailableIntersections()
    {
        if (roadSystem != null && roadSystem.Intersections != null && roadSystem.Intersections.Length > 0)
            return roadSystem.Intersections;

#if UNITY_2023_1_OR_NEWER
        return FindObjectsByType<Intersection>(FindObjectsSortMode.None);
#else
    return FindObjectsOfType<Intersection>();
#endif
    }

    private void ReleaseIntersectionIfExited()
    {
        if (reservedIntersectionController == null)
            return;

        float distanceFromReservedIntersection = FlatDistance(
            transform.position,
            reservedIntersectionController.Center
        );

        if (distanceFromReservedIntersection > reservedIntersectionController.ExitRadius + intersectionReleasePadding)
        {
            reservedIntersectionController.Release(this);
            reservedIntersectionController = null;
        }
    }

    private float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private void OnDisable()
    {
        if (reservedIntersectionController != null)
        {
            reservedIntersectionController.Release(this);
            reservedIntersectionController = null;
        }
    }

    private void ApplyAISpeedControl(float desiredSpeedKmh)
    {
        float absSpeed = Mathf.Abs(currentSpeed);

        if (desiredSpeedKmh <= 0.1f)
        {
            currentAccelerationInput = 0f;
            currentBrakeInput = 1f;
            return;
        }

        if (absSpeed < desiredSpeedKmh - speedTolerance)
        {
            currentAccelerationInput = normalAccelerationInput;
            currentBrakeInput = 0f;
        }
        else if (absSpeed > desiredSpeedKmh + speedTolerance)
        {
            currentAccelerationInput = 0f;
            currentBrakeInput = normalBrakeInput;
        }
        else
        {
            currentAccelerationInput = 0.25f;
            currentBrakeInput = 0f;
        }
    }

    private void Acceleration()
    {
        float forwardInput = Mathf.Clamp01(currentAccelerationInput);
        float reverseInput = 0f;

        float motorTorque = 0f;
        float brakeTorque = 0f;

        if (IsMovingForward())
        {
            if (reverseInput > 0f)
            {
                brakeTorque = reverseInput * brakePower;
            }
            else if (forwardInput > 0f && currentSpeed < maxForwardSpeed)
            {
                float speed01 = Mathf.InverseLerp(0f, maxForwardSpeed, currentSpeed);
                float availableTorque = Mathf.Lerp(horsePower, 0f, speed01);

                motorTorque = availableTorque * forwardInput;
            }
        }
        else if (IsMovingBackward())
        {
            if (forwardInput > 0f)
            {
                brakeTorque = forwardInput * brakePower;
            }
            else if (reverseInput > 0f && Mathf.Abs(currentSpeed) < maxReverseSpeed)
            {
                float speed01 = Mathf.InverseLerp(0f, maxReverseSpeed, Mathf.Abs(currentSpeed));
                float availableTorque = Mathf.Lerp(horsePower, 0f, speed01);

                motorTorque = -availableTorque * reverseInput;
            }
        }
        else
        {
            if (forwardInput > 0f && reverseInput <= 0f)
            {
                motorTorque = horsePower * forwardInput;
            }
            else if (reverseInput > 0f && forwardInput <= 0f)
            {
                motorTorque = -horsePower * reverseInput;
            }
            else if (forwardInput > 0f && reverseInput > 0f)
            {
                brakeTorque = brakePower;
            }
        }

        SetMotorTorque(motorTorque);

        if (brakeTorque > 0f)
            SetFootBrake(brakeTorque);
    }

    private void Braking()
    {
        if (currentBrakeInput > 0f)
        {
            SetFootBrake(currentBrakeInput * brakePower);
        }
        else
        {
            SetFootBrake(0f);
        }
    }

    private void Handbraking()
    {
        if (currentHandbrakeInput > 0f)
        {
            rearLeftWheelCollider.motorTorque = 0f;
            rearRightWheelCollider.motorTorque = 0f;

            rearLeftWheelCollider.brakeTorque = currentHandbrakeInput * handbrakeForce;
            rearRightWheelCollider.brakeTorque = currentHandbrakeInput * handbrakeForce;
        }
        else
        {
            rearLeftWheelCollider.brakeTorque = 0f;
            rearRightWheelCollider.brakeTorque = 0f;
        }
    }

    private void Steering()
    {
        float adjustedSpeedFactor = Mathf.InverseLerp(20f, maxForwardSpeed, Mathf.Abs(currentSpeed));
        float adjustedTurnAngle = targetSteerAngle * (1f - adjustedSpeedFactor);

        currentSteerAngle = Mathf.Lerp(
            currentSteerAngle,
            adjustedTurnAngle,
            Time.fixedDeltaTime * steeringSpeed
        );

        frontLeftWheelCollider.steerAngle = currentSteerAngle;
        frontRightWheelCollider.steerAngle = currentSteerAngle;
    }

    private void Slowdown()
    {
        if (vehicleRB == null)
            return;

        if (currentAccelerationInput == 0f && currentBrakeInput == 0f && currentHandbrakeInput == 0f)
        {
#if UNITY_6000_0_OR_NEWER
            vehicleRB.linearVelocity = Vector3.Lerp(
                vehicleRB.linearVelocity,
                Vector3.zero,
                Time.fixedDeltaTime * decelerationSpeed
            );
#else
            vehicleRB.velocity = Vector3.Lerp(
                vehicleRB.velocity,
                Vector3.zero,
                Time.fixedDeltaTime * decelerationSpeed
            );
#endif
        }
    }

    private void SetMotorTorque(float torque)
    {
        ClearMotorTorque();

        switch (driveType)
        {
            case TrafficDriveType.FWD:
                frontLeftWheelCollider.motorTorque = torque;
                frontRightWheelCollider.motorTorque = torque;
                break;

            case TrafficDriveType.RWD:
                rearLeftWheelCollider.motorTorque = torque;
                rearRightWheelCollider.motorTorque = torque;
                break;

            case TrafficDriveType.AWD:
                frontLeftWheelCollider.motorTorque = torque;
                frontRightWheelCollider.motorTorque = torque;
                rearLeftWheelCollider.motorTorque = torque;
                rearRightWheelCollider.motorTorque = torque;
                break;
        }
    }

    private void ClearMotorTorque()
    {
        frontLeftWheelCollider.motorTorque = 0f;
        frontRightWheelCollider.motorTorque = 0f;
        rearLeftWheelCollider.motorTorque = 0f;
        rearRightWheelCollider.motorTorque = 0f;
    }

    private void SetFootBrake(float brakeTorque)
    {
        frontLeftWheelCollider.brakeTorque = brakeTorque;
        frontRightWheelCollider.brakeTorque = brakeTorque;
    }

    private void UpdateCurrentSpeed()
    {
        if (vehicleRB == null)
            return;

#if UNITY_6000_0_OR_NEWER
        currentSpeed = Vector3.Dot(transform.forward, vehicleRB.linearVelocity) * 3.6f;
#else
        currentSpeed = Vector3.Dot(transform.forward, vehicleRB.velocity) * 3.6f;
#endif
    }

    private void UpdateStationaryState()
    {
        stationary =
            Mathf.Abs(frontLeftWheelCollider.rpm) < stopThreshold &&
            Mathf.Abs(frontRightWheelCollider.rpm) < stopThreshold &&
            Mathf.Abs(rearLeftWheelCollider.rpm) < stopThreshold &&
            Mathf.Abs(rearRightWheelCollider.rpm) < stopThreshold;
    }

    private bool IsNearlyStopped()
    {
        return Mathf.Abs(currentSpeed) <= directionChangeThreshold;
    }

    private bool IsMovingForward()
    {
        return currentSpeed > directionChangeThreshold;
    }

    private bool IsMovingBackward()
    {
        return currentSpeed < -directionChangeThreshold;
    }

    private int GetClosestPathIndex(List<Bezier.OrientedPoint> points)
    {
        int closestIndex = 0;
        float closestDistance = float.MaxValue;

        for (int i = 0; i < points.Count; i++)
        {
            float distance = Vector3.SqrMagnitude(transform.position - points[i].position);

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestIndex = i;
            }
        }

        return closestIndex;
    }

    private Vector3 GetPathForward(List<Bezier.OrientedPoint> points, int index)
    {
        if (points == null || points.Count < 2)
            return transform.forward;

        int nextIndex = Mathf.Clamp(index + 1, 0, points.Count - 1);
        int previousIndex = Mathf.Clamp(index - 1, 0, points.Count - 1);

        Vector3 forward = points[nextIndex].position - points[previousIndex].position;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.001f)
        {
            forward = points[index].forward;
            forward.y = 0f;
        }

        if (forward.sqrMagnitude < 0.001f)
            forward = transform.forward;

        return forward.normalized;
    }

    private Vector3 GetLanePoint(List<Bezier.OrientedPoint> points, int index)
    {
        Vector3 forward = GetPathForward(points, index);
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

        float side = rightHandDriving ? 1f : -1f;

        return points[index].position + right * laneOffset * side;
    }

    private bool CheckCarAhead(out float distance)
    {
        distance = frontCheckDistance;

        Vector3 origin = transform.position + Vector3.up * 0.7f + transform.forward * 2f;

        if (Physics.SphereCast(
            origin,
            frontCheckRadius,
            transform.forward,
            out RaycastHit hit,
            frontCheckDistance,
            trafficCarLayer,
            QueryTriggerInteraction.Ignore))
        {
            if (hit.transform != transform && !hit.transform.IsChildOf(transform))
            {
                distance = hit.distance;
                return true;
            }
        }

        return false;
    }

    private bool CheckTurnAhead(List<Bezier.OrientedPoint> points, int startIndex, out float distance)
    {
        distance = float.MaxValue;

        if (points == null || points.Count < 5)
            return false;

        int currentIndex = Mathf.Clamp(startIndex, 0, points.Count - 1);
        int futureIndex = Mathf.Clamp(startIndex + lookAheadPointIndex + 4, 0, points.Count - 1);

        Vector3 currentForward = GetPathForward(points, currentIndex);
        Vector3 futureForward = GetPathForward(points, futureIndex);

        float angle = Vector3.Angle(currentForward, futureForward);

        if (angle >= turnDetectAngle)
        {
            distance = Vector3.Distance(transform.position, points[futureIndex].position);
            return true;
        }

        return false;
    }

    public void SetRandomGoal()
    {
        if (roadSystem == null)
            return;

        if (roadSystem.Roads == null || roadSystem.Roads.Length == 0)
            return;

        Road randomRoad = roadSystem.Roads[Random.Range(0, roadSystem.Roads.Length)];

        Bezier.OrientedPoint[] roadPoints = randomRoad.GetEvenlySpacedPoints(roadPointSpacing);

        if (roadPoints == null || roadPoints.Length == 0)
            return;

        Bezier.OrientedPoint randomPoint = roadPoints[Random.Range(0, roadPoints.Length)];
        randomPoint = randomPoint.ToWorldSpace(randomRoad.transform);

        navigator.Goal = randomPoint.position;
        navigator.CalculateWayPointsSync();

        hasSmoothedLaneTarget = false;
        smoothedSteerInput = 0f;
    }

    public void InitializeTrafficAI(RoadSystem system, bool useRightHandDriving)
    {
        roadSystem = system;
        rightHandDriving = useRightHandDriving;

        if (navigator == null)
            navigator = GetComponent<RoadSystemNavigator>();

        navigator.currentRoadSystem = roadSystem;

        targetSpeedKmh = Random.Range(randomSpeedRangeKmh.x, randomSpeedRangeKmh.y);

        SetRandomGoal();
    }

    private void UpdateWheelRPMVisuals()
    {
        UpdateWheel(frontLeftWheelCollider, frontLeftWheelMesh);
        UpdateWheel(frontRightWheelCollider, frontRightWheelMesh);
        UpdateWheel(rearLeftWheelCollider, rearLeftWheelMesh);
        UpdateWheel(rearRightWheelCollider, rearRightWheelMesh);
    }

    private void UpdateWheel(WheelCollider wheelCollider, Transform wheelMesh)
    {
        if (wheelCollider == null || wheelMesh == null)
            return;

        wheelCollider.GetWorldPose(out Vector3 position, out Quaternion rotation);
        wheelMesh.SetPositionAndRotation(position, rotation);
    }

    public bool InAir()
    {
        foreach (WheelCollider wheel in wheels)
        {
            if (wheel == null)
                continue;

            if (wheel.GetGroundHit(out _))
                return false;
        }

        return true;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawDebug)
            return;

        Gizmos.color = Color.yellow;

        Vector3 origin = transform.position + Vector3.up * 0.7f + transform.forward * 2f;
        Gizmos.DrawWireSphere(origin, frontCheckRadius);
        Gizmos.DrawLine(origin, origin + transform.forward * frontCheckDistance);

        if (navigator != null && navigator.CurrentPoints != null)
        {
            Gizmos.color = Color.cyan;

            foreach (Bezier.OrientedPoint point in navigator.CurrentPoints)
            {
                Gizmos.DrawSphere(point.position, 0.25f);
            }

            Gizmos.color = Color.green;

            if (navigator.CurrentPoints.Count > 0)
            {
                int debugTargetIndex = Mathf.Clamp(targetPathIndex, 0, navigator.CurrentPoints.Count - 1);
                Vector3 lanePoint = GetLanePoint(navigator.CurrentPoints, debugTargetIndex);

                Gizmos.DrawSphere(lanePoint, 0.75f);
                Gizmos.DrawLine(transform.position, lanePoint);
            }
        }

        if (roadSystem != null && roadSystem.Intersections != null)
        {
            Gizmos.color = Color.red;

            foreach (Intersection intersection in roadSystem.Intersections)
            {
                if (intersection == null)
                    continue;

                float radius = Mathf.Max(3f, intersection.Radius + intersectionCheckExtraRadius);
                Gizmos.DrawWireSphere(intersection.transform.position + Vector3.up, radius);
            }
        }
    }
}