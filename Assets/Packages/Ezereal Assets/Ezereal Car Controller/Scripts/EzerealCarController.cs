using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.UI;

namespace Ezereal
{
    public class EzerealCarController : MonoBehaviour // This is the main system resposible for car control.
    {
        [Header("Tutorial")]
        public float CurrentSpeed => currentSpeed;
        public float AccelerationInput => currentAccelerationValue;
        public float BrakeInput => currentBrakeValue;
        public float HandbrakeInput => currentHandbrakeValue;
        public float SteerInput => targetSteerAngle;
        public bool IsStarted => isStarted;
        public AutomaticGears CurrentGear => currentGear;

        [Header("Driving Mode")]
        public bool useGearbox = false; // true = old behavior, false = new W/S direction logic
        [SerializeField] private float directionChangeThreshold = 1f; // km/h

        [Header("Ezereal References")]

        [SerializeField] EzerealLightController ezerealLightController;
        [SerializeField] EzerealSoundController ezerealSoundController;
        [SerializeField] EzerealWheelFrictionController ezerealWheelFrictionController;
        [SerializeField] private CarStatus carStatus;

        [Header("References")]

        public Rigidbody vehicleRB;
        public WheelCollider frontLeftWheelCollider;
        public WheelCollider frontRightWheelCollider;
        public WheelCollider rearLeftWheelCollider;
        public WheelCollider rearRightWheelCollider;
        WheelCollider[] wheels;

        [SerializeField] Transform frontLeftWheelMesh;
        [SerializeField] Transform frontRightWheelMesh;
        [SerializeField] Transform rearLeftWheelMesh;
        [SerializeField] Transform rearRightWheelMesh;

        [SerializeField] Transform steeringWheel;

        [SerializeField] TMP_Text currentGearTMP_UI;
        [SerializeField] TMP_Text currentGearTMP_Dashboard;

        [SerializeField] TMP_Text currentSpeedTMP_UI;
        [SerializeField] TMP_Text currentSpeedTMP_Dashboard;
        [SerializeField] Slider accelerationSlider;

        [Header("Settings")]
        public bool isStarted = true;

        public float maxForwardSpeed = 130f; // 100f default
        public float defaultmaxForwardSpeed = 130f;
        public float maxReverseSpeed = 30f; // 30f default
        public float horsePower = 550f; // 100f0 default
        public float defaulthorsePower = 550f;
        public float brakePower = 2000f; // 2000f default
        public float handbrakeForce = 3000f; // 3000f default
        public float maxSteerAngle = 40f; // 30f default
        public float steeringSpeed = 5f; // 0.5f default
        public float stopThreshold = 5f; // 1f default. At what speed car will make a full stop
        public float decelerationSpeed = 0.5f; // 0.5f default
        public float maxSteeringWheelRotation = 100f; // 360 for real steering wheel. 120 would be more suitable for racing.

        [Header("Manual Upright")]
        [SerializeField] private Key uprightKey = Key.R;
        [SerializeField] private float manualUprightLiftHeight = 0.75f;
        [SerializeField] private bool resetVelocityWhenManualUpright = true;

        private bool manualUprightRequested = false;

        [Header("Drive Type")]
        public DriveTypes driveType = DriveTypes.RWD;

        [Header("Gearbox")]
        public AutomaticGears currentGear = AutomaticGears.Drive;

        [Header("Debug Info")]
        public bool stationary = true;
        [SerializeField] public float currentSpeed = 0f;
        [SerializeField] float currentAccelerationValue = 0f;
        [SerializeField] float currentBrakeValue = 0f;
        [SerializeField] float currentHandbrakeValue = 0f;
        [SerializeField] float currentSteerAngle = 0f;
        [SerializeField] float targetSteerAngle = 0f;
        [SerializeField] float FrontLeftWheelRPM = 0f;
        [SerializeField] float FrontRightWheelRPM = 0f;
        [SerializeField] float RearLeftWheelRPM = 0f;
        [SerializeField] float RearRightWheelRPM = 0f;

        [SerializeField] float speedFactor = 0f; // Leave at zero. Responsible for smooth acceleration and near-top-speed slowdown.

        public void ToggleGearbox()
        {
            SetUseGearbox(!useGearbox);
        }

        public void SetUseGearbox(bool enabled)
        {
            useGearbox = enabled;
        }

        private void Update()
        {
            if (Keyboard.current == null)
                return;

            if (Keyboard.current[uprightKey].wasPressedThisFrame)
            {
                manualUprightRequested = true;
            }
        }

        private void Awake()
        {
            carStatus = GetComponent<CarStatus>();

            wheels = new WheelCollider[]
            {
            frontLeftWheelCollider,
            frontRightWheelCollider,
            rearLeftWheelCollider,
            rearRightWheelCollider,
            };

            if (ezerealLightController == null)
            {
                Debug.LogWarning("EzerealLightController reference is missing. Ignore or attach one if you want to have light controls.");
            }

            if (ezerealSoundController == null)
            {
                Debug.LogWarning("EzerealSoundController reference is missing. Ignore or attach one if you want to have engine sounds.");
            }

            if (ezerealWheelFrictionController == null)
            {
                Debug.LogWarning("EzerealWheelFrictionController reference is missing. Ignore or attach one if you want to have friction controls.");
            }

            if (vehicleRB == null)
            {
                Debug.LogError("VehicleRB reference is missing for EzerealCarController!");
            }

            if (isStarted)
            {
                Debug.Log("Car is started.");

                if (ezerealLightController != null)
                {
                    ezerealLightController.MiscLightsOn();
                }

                if (ezerealSoundController != null)
                {
                    ezerealSoundController.TurnOnEngineSound();
                }
            }

            maxForwardSpeed = defaultmaxForwardSpeed;
            horsePower = defaulthorsePower; 
        }

        void OnStartCar()
        {
            isStarted = !isStarted;

            if (isStarted)
            {
                Debug.Log("Car started.");

                if (ezerealLightController != null)
                {
                    ezerealLightController.MiscLightsOn();
                }

                if (ezerealSoundController != null)
                {
                    ezerealSoundController.TurnOnEngineSound();
                }

            }
            else if (!isStarted)
            {
                Debug.Log("Car turned off");

                if (ezerealLightController != null)
                {
                    ezerealLightController.AllLightsOff();
                }

                if (ezerealSoundController != null)
                {
                    ezerealSoundController.TurnOffEngineSound();
                }

                frontLeftWheelCollider.motorTorque = 0;
                frontRightWheelCollider.motorTorque = 0;
                rearLeftWheelCollider.motorTorque = 0;
                rearRightWheelCollider.motorTorque = 0;
            }


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

        private void ClearMotorTorque()
        {
            frontLeftWheelCollider.motorTorque = 0f;
            frontRightWheelCollider.motorTorque = 0f;
            rearLeftWheelCollider.motorTorque = 0f;
            rearRightWheelCollider.motorTorque = 0f;
        }

        private void SetMotorTorque(float torque)
        {
            ClearMotorTorque();

            switch (driveType)
            {
                case DriveTypes.FWD:
                    frontLeftWheelCollider.motorTorque = torque;
                    frontRightWheelCollider.motorTorque = torque;
                    break;

                case DriveTypes.RWD:
                    rearLeftWheelCollider.motorTorque = torque;
                    rearRightWheelCollider.motorTorque = torque;
                    break;

                case DriveTypes.AWD:
                    frontLeftWheelCollider.motorTorque = torque;
                    frontRightWheelCollider.motorTorque = torque;
                    rearLeftWheelCollider.motorTorque = torque;
                    rearRightWheelCollider.motorTorque = torque;
                    break;
            }
        }

        private void SetFootBrake(float brakeTorque)
        {
            // keep same style as your current brake logic: front wheels only
            frontLeftWheelCollider.brakeTorque = brakeTorque;
            frontRightWheelCollider.brakeTorque = brakeTorque;
        }

        private void SetBrakeLights(bool isBraking)
        {
            if (!isStarted || ezerealLightController == null) return;

            if (isBraking) ezerealLightController.BrakeLightsOn();
            else ezerealLightController.BrakeLightsOff();
        }

        private void SetReverseLights(bool isReversing)
        {
            if (!isStarted || ezerealLightController == null) return;

            if (isReversing) ezerealLightController.ReverseLightsOn();
            else ezerealLightController.ReverseLightsOff();
        }

        private void DirectionBasedDrive()
        {
            float forwardInput = Mathf.Clamp01(currentAccelerationValue); // W
            float reverseInput = Mathf.Clamp01(currentBrakeValue);        // S

            float motorTorque = 0f;
            float brakeTorque = 0f;

            if (IsMovingForward())
            {
                // Car still moving forward:
                // S should brake first, not instantly reverse
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
                // Car still moving backward:
                // W should brake first, not instantly go forward
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
                // Near zero speed: direction can change
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
            SetFootBrake(brakeTorque);
            SetBrakeLights(brakeTorque > 0f);
            SetReverseLights(motorTorque < 0f || IsMovingBackward());

            // optional UI text
            if (motorTorque < 0f || IsMovingBackward())
                UpdateGearText("R");
            else if (motorTorque > 0f || IsMovingForward())
                UpdateGearText("D");
            else
                UpdateGearText("N");
        }

        private void ShutdownCar()
        {
            isStarted = false;
            Debug.Log("Car shutdown.");

            if (ezerealLightController != null)
                ezerealLightController.AllLightsOff();

            if (ezerealSoundController != null)
                ezerealSoundController.TurnOffEngineSound();

            frontLeftWheelCollider.motorTorque = 0;
            frontRightWheelCollider.motorTorque = 0;
            rearLeftWheelCollider.motorTorque = 0;
            rearRightWheelCollider.motorTorque = 0;
        }

        void OnAccelerate(InputValue accelerationValue)
        {
            currentAccelerationValue = accelerationValue.Get<float>();
            //Debug.Log("Acceleration: " + currentAccelerationValue.ToString());
        }

        void Acceleration()
        {
            bool hasDriveInput = useGearbox
                ? currentAccelerationValue > 0f
                : (currentAccelerationValue > 0f || currentBrakeValue > 0f);

            if (hasDriveInput)
            {
                if (carStatus != null)
                {
                    if (carStatus.isOutOfFuel)
                    {
                        ShutdownCar();
                        return;
                    }

                    carStatus.ConsumeFuel(carStatus.fuelConsumptionPerSecond * Time.deltaTime);
                }
            }

            if (!isStarted)
            {
                ClearMotorTorque();
                return;
            }

            if (!useGearbox)
            {
                DirectionBasedDrive();
                UpdateAccelerationSlider();
                return;
            }

            // ===== OLD GEARBOX BEHAVIOR =====
            if (currentGear == AutomaticGears.Neutral)
            {
                ClearMotorTorque();
                return;
            }

            if (currentGear == AutomaticGears.Drive)
            {
                speedFactor = Mathf.InverseLerp(0, maxForwardSpeed, currentSpeed);
                float currentMotorTorque = Mathf.Lerp(horsePower, 0, speedFactor);

                if (currentAccelerationValue > 0f && currentSpeed < maxForwardSpeed)
                {
                    if (driveType == DriveTypes.RWD)
                    {
                        rearLeftWheelCollider.motorTorque = currentMotorTorque * currentAccelerationValue;
                        rearRightWheelCollider.motorTorque = currentMotorTorque * currentAccelerationValue;
                    }
                    else if (driveType == DriveTypes.FWD)
                    {
                        frontLeftWheelCollider.motorTorque = currentMotorTorque * currentAccelerationValue;
                        frontRightWheelCollider.motorTorque = currentMotorTorque * currentAccelerationValue;
                    }
                    else if (driveType == DriveTypes.AWD)
                    {
                        frontLeftWheelCollider.motorTorque = currentMotorTorque * currentAccelerationValue;
                        frontRightWheelCollider.motorTorque = currentMotorTorque * currentAccelerationValue;
                        rearLeftWheelCollider.motorTorque = currentMotorTorque * currentAccelerationValue;
                        rearRightWheelCollider.motorTorque = currentMotorTorque * currentAccelerationValue;
                    }
                }
                else
                {
                    ClearMotorTorque();
                }
            }

            if (currentGear == AutomaticGears.Reverse)
            {
                if (currentAccelerationValue > 0f && currentSpeed > -maxReverseSpeed)
                {
                    float reverseInput = currentAccelerationValue;

                    if (driveType == DriveTypes.RWD)
                    {
                        rearLeftWheelCollider.motorTorque = -reverseInput * horsePower;
                        rearRightWheelCollider.motorTorque = -reverseInput * horsePower;
                    }
                    else if (driveType == DriveTypes.FWD)
                    {
                        frontLeftWheelCollider.motorTorque = -reverseInput * horsePower;
                        frontRightWheelCollider.motorTorque = -reverseInput * horsePower;
                    }
                    else if (driveType == DriveTypes.AWD)
                    {
                        frontLeftWheelCollider.motorTorque = -reverseInput * horsePower;
                        frontRightWheelCollider.motorTorque = -reverseInput * horsePower;
                        rearLeftWheelCollider.motorTorque = -reverseInput * horsePower;
                        rearRightWheelCollider.motorTorque = -reverseInput * horsePower;
                    }
                }
                else
                {
                    ClearMotorTorque();
                }
            }

            UpdateAccelerationSlider();
        }

        void OnBrake(InputValue brakeValue)
        {
            currentBrakeValue = brakeValue.Get<float>();
        }

        void Braking()
        {
            if (!useGearbox)
                return; // braking is already handled inside DirectionBasedDrive()

            if (currentBrakeValue > 0f)
            {
                SetFootBrake(currentBrakeValue * brakePower);
                SetBrakeLights(true);
            }
            else
            {
                SetFootBrake(0f);
                SetBrakeLights(false);
            }
        }

        void OnHandbrake(InputValue handbrakeValue)
        {
            currentHandbrakeValue = handbrakeValue.Get<float>();

            if (isStarted)
            {
                if (currentHandbrakeValue > 0)
                {
                    if (ezerealWheelFrictionController != null)
                    {
                        ezerealWheelFrictionController.StartDrifting(currentHandbrakeValue);
                    }

                    if (ezerealLightController != null)
                    {
                        ezerealLightController.HandbrakeLightOn();
                    }
                }
                else
                {
                    if (ezerealWheelFrictionController != null)
                    {
                        ezerealWheelFrictionController.StopDrifting();
                    }

                    if (ezerealLightController != null)
                    {
                        ezerealLightController.HandbrakeLightOff();
                    }
                }
            }
        }

        void Handbraking()
        {
            if (currentHandbrakeValue > 0f)
            {
                rearLeftWheelCollider.motorTorque = 0;
                rearRightWheelCollider.motorTorque = 0;
                rearLeftWheelCollider.brakeTorque = currentHandbrakeValue * handbrakeForce;
                rearRightWheelCollider.brakeTorque = currentHandbrakeValue * handbrakeForce;


            }
            else
            {
                rearLeftWheelCollider.brakeTorque = 0;
                rearRightWheelCollider.brakeTorque = 0;
            }
        }

        void OnSteer(InputValue turnValue)
        {
            targetSteerAngle = turnValue.Get<float>() * maxSteerAngle;
        }

        void Steering()
        {
            float adjustedspeedFactor = Mathf.InverseLerp(20, maxForwardSpeed, currentSpeed); //minimum speed affecting steerAngle is 20
            float adjustedTurnAngle = targetSteerAngle * (1 - adjustedspeedFactor); //based on current speed.
            currentSteerAngle = Mathf.Lerp(currentSteerAngle, adjustedTurnAngle, Time.deltaTime * steeringSpeed);

            frontLeftWheelCollider.steerAngle = currentSteerAngle;
            frontRightWheelCollider.steerAngle = currentSteerAngle;

            UpdateWheel(frontLeftWheelCollider, frontLeftWheelMesh);
            UpdateWheel(frontRightWheelCollider, frontRightWheelMesh);
            UpdateWheel(rearLeftWheelCollider, rearLeftWheelMesh);
            UpdateWheel(rearRightWheelCollider, rearRightWheelMesh);
        }

        void Slowdown()
        {
            if (vehicleRB != null)
            {
                if (currentAccelerationValue == 0 && currentBrakeValue == 0 && currentHandbrakeValue == 0)
                {
#if UNITY_6000_0_OR_NEWER
                    vehicleRB.linearVelocity = Vector3.Lerp(vehicleRB.linearVelocity, Vector3.zero, Time.deltaTime * decelerationSpeed);
#else
                    vehicleRB.velocity = Vector3.Lerp(vehicleRB.velocity, Vector3.zero, Time.deltaTime * decelerationSpeed);
#endif
                }
            }
        }

        void OnDownShift()
        {
            if (!useGearbox) return;

            switch (currentGear)
            {
                case AutomaticGears.Reverse:
                    break;

                case AutomaticGears.Neutral:
                    currentGear--;
                    UpdateGearText("R");
                    if (isStarted && ezerealLightController != null)
                        ezerealLightController.ReverseLightsOn();
                    break;

                case AutomaticGears.Drive:
                    currentGear--;
                    UpdateGearText("N");
                    break;
            }
        }

        void OnUpShift()
        {
            if (!useGearbox) return;

            switch (currentGear)
            {
                case AutomaticGears.Reverse:
                    currentGear++;
                    UpdateGearText("N");
                    if (isStarted && ezerealLightController != null)
                        ezerealLightController.ReverseLightsOff();
                    break;

                case AutomaticGears.Neutral:
                    currentGear++;
                    UpdateGearText("D");
                    break;

                case AutomaticGears.Drive:
                    break;
            }
        }

        private void FixedUpdate()
        {
            if (manualUprightRequested)
            {
                manualUprightRequested = false;
                Upright();
            }

            Acceleration();

            Braking();

            Handbraking();

            Steering();

            Slowdown();

            RotateSteeringWheel();

            if
                (
                    Mathf.Abs(frontLeftWheelCollider.rpm) < stopThreshold &&
                    Mathf.Abs(frontRightWheelCollider.rpm) < stopThreshold &&
                    Mathf.Abs(rearLeftWheelCollider.rpm) < stopThreshold &&
                    Mathf.Abs(rearRightWheelCollider.rpm) < stopThreshold
                )
            {
                stationary = true;
            }
            else
            {
                stationary = false;
            }

            if (vehicleRB != null) // Unity uses m/s as for default. So I convert from m/s to km/h. For mph use 2.23694f instead of 3.6f.
            {
#if UNITY_6000_0_OR_NEWER
                currentSpeed = Vector3.Dot(vehicleRB.gameObject.transform.forward, vehicleRB.linearVelocity);
                currentSpeed *= 3.6f;
                UpdateSpeedText(currentSpeed);
#else
                currentSpeed = Vector3.Dot(vehicleRB.gameObject.transform.forward, vehicleRB.velocity);
                currentSpeed *= 3.6f; 
                UpdateSpeedText(currentSpeed);
#endif

            }


            FrontLeftWheelRPM = frontLeftWheelCollider.rpm;
            FrontRightWheelRPM = frontRightWheelCollider.rpm;
            RearLeftWheelRPM = rearLeftWheelCollider.rpm;
            RearRightWheelRPM = rearRightWheelCollider.rpm;
        }

        private void UpdateWheel(WheelCollider col, Transform mesh)
        {
            col.GetWorldPose(out Vector3 position, out Quaternion rotation);
            mesh.SetPositionAndRotation(position, rotation);
        }


        void RotateSteeringWheel()
        {
            float currentXAngle = steeringWheel.transform.localEulerAngles.x; // Maximum steer angle in degrees

            // Calculate the rotation based on the steer angle
            float normalizedSteerAngle = Mathf.Clamp(frontLeftWheelCollider.steerAngle, -maxSteerAngle, maxSteerAngle);
            float rotation = Mathf.Lerp(maxSteeringWheelRotation, -maxSteeringWheelRotation, (normalizedSteerAngle + maxSteerAngle) / (2 * maxSteerAngle));

            // Set the local rotation of the steering wheel
            steeringWheel.localRotation = Quaternion.Euler(currentXAngle, 0, rotation);
        }

        void UpdateGearText(string gear)
        {
            currentGearTMP_UI.text = gear;
            currentGearTMP_Dashboard.text = gear;
        }

        void UpdateSpeedText(float speed)
        {
            speed = Mathf.Abs(speed);

            currentSpeedTMP_UI.text = speed.ToString("F0");
            currentSpeedTMP_Dashboard.text = speed.ToString("F0");
        }

        void UpdateAccelerationSlider()
        {
            if (!useGearbox)
            {
                float targetValue = 0f;

                if (IsMovingBackward() || (IsNearlyStopped() && currentBrakeValue > 0f && currentAccelerationValue == 0f))
                    targetValue = currentBrakeValue;
                else
                    targetValue = currentAccelerationValue;

                accelerationSlider.value = Mathf.Lerp(accelerationSlider.value, targetValue, Time.deltaTime * 15f);
                return;
            }

            if (currentGear == AutomaticGears.Drive || currentGear == AutomaticGears.Reverse)
            {
                accelerationSlider.value = Mathf.Lerp(accelerationSlider.value, currentAccelerationValue, Time.deltaTime * 15f);
            }
            else
            {
                accelerationSlider.value = 0;
            }
        }

        public bool InAir()
        {
            foreach (WheelCollider wheel in wheels)
            {
                if (wheel.GetGroundHit(out _))
                {
                    return false;
                }
            }
            return true;
        }

        private void Upright()
        {
            if (vehicleRB == null)
                return;

            ClearMotorTorque();

            frontLeftWheelCollider.brakeTorque = 0f;
            frontRightWheelCollider.brakeTorque = 0f;
            rearLeftWheelCollider.brakeTorque = 0f;
            rearRightWheelCollider.brakeTorque = 0f;

            if (resetVelocityWhenManualUpright)
            {
#if UNITY_6000_0_OR_NEWER
                vehicleRB.linearVelocity = Vector3.zero;
#else
        vehicleRB.velocity = Vector3.zero;
#endif

                vehicleRB.angularVelocity = Vector3.zero;
            }

            Quaternion uprightRotation = Quaternion.Euler(
                0f,
                vehicleRB.rotation.eulerAngles.y,
                0f
            );

            Vector3 liftedPosition = vehicleRB.position + Vector3.up * manualUprightLiftHeight;

            vehicleRB.MovePosition(liftedPosition);
            vehicleRB.MoveRotation(uprightRotation);
        }
    }
}
