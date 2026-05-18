using UnityEngine;

namespace RVP
{
    [RequireComponent(typeof(VehicleParent))]
    [DisallowMultipleComponent]
    [AddComponentMenu("RVP/Stunt/Flip Control", 2)]

    // Class for in-air rotation of vehicles
    public class FlipControl : MonoBehaviour
    {
        Transform tr;
        Rigidbody rb;
        VehicleParent vp;

        public bool disableDuringCrash;
        public Vector3 flipPower;

        [Tooltip("Continue spinning if input is stopped")]
        public bool freeSpinFlip;

        [Tooltip("Stop spinning if input is stopped and vehicle is upright")]
        public bool stopFlip;

        [Tooltip("How quickly the vehicle will rotate upright in air")]
        public Vector3 rotationCorrection;
        Quaternion velDir;

        [Tooltip("Distance to check for ground for reference normal for rotation correction")]
        public float groundCheckDistance = 100;

        [Tooltip("Minimum dot product between ground normal and global up direction for rotation correction")]
        public float groundSteepnessLimit = 0.5f;

        [Tooltip("How quickly the vehicle will dive in the direction it's soaring")]
        public float diveFactor;

        void Start() {
            tr = transform;
            rb = GetComponent<Rigidbody>();
            vp = GetComponent<VehicleParent>();
        }

        void FixedUpdate() {
            if (vp.GroundedWheels == 0 && (!vp.Crashing || (vp.Crashing && !disableDuringCrash))) {
                velDir = Quaternion.LookRotation(GlobalControl.worldUpDir, rb.linearVelocity);

                if (flipPower != Vector3.zero) {
                    ApplyFlip();
                }

                if (stopFlip) {
                    ApplyStopFlip();
                }

                if (rotationCorrection != Vector3.zero) {
                    ApplyRotationCorrection();
                }

                if (diveFactor > 0) {
                    Dive();
                }
            }
        }

        // Apply flip forces
        void ApplyFlip() {
            Vector3 flipTorque;

            if (freeSpinFlip) {
                flipTorque = new Vector3(
                    vp.PitchInput * flipPower.x,
                    vp.YawInput * flipPower.y,
                    vp.RollInput * flipPower.z
                    );
            }
            else {
                flipTorque = new Vector3(
                    vp.PitchInput != 0 && Mathf.Abs(vp.LocalAngularVel.x) > 1 && System.Math.Sign(vp.PitchInput * Mathf.Sign(flipPower.x)) != System.Math.Sign(vp.LocalAngularVel.x) ? -vp.LocalAngularVel.x * Mathf.Abs(flipPower.x) : vp.PitchInput * flipPower.x - vp.LocalAngularVel.x * (1 - Mathf.Abs(vp.PitchInput)) * Mathf.Abs(flipPower.x),
                    vp.YawInput != 0 && Mathf.Abs(vp.LocalAngularVel.y) > 1 && System.Math.Sign(vp.YawInput * Mathf.Sign(flipPower.y)) != System.Math.Sign(vp.LocalAngularVel.y) ? -vp.LocalAngularVel.y * Mathf.Abs(flipPower.y) : vp.YawInput * flipPower.y - vp.LocalAngularVel.y * (1 - Mathf.Abs(vp.YawInput)) * Mathf.Abs(flipPower.y),
                    vp.RollInput != 0 && Mathf.Abs(vp.LocalAngularVel.z) > 1 && System.Math.Sign(vp.RollInput * Mathf.Sign(flipPower.z)) != System.Math.Sign(vp.LocalAngularVel.z) ? -vp.LocalAngularVel.z * Mathf.Abs(flipPower.z) : vp.RollInput * flipPower.z - vp.LocalAngularVel.z * (1 - Mathf.Abs(vp.RollInput)) * Mathf.Abs(flipPower.z)
                    );
            }

            rb.AddRelativeTorque(flipTorque, ForceMode.Acceleration);
        }

        // Counteract flipping with forces
        void ApplyStopFlip() {
            Vector3 stopFlipFactor = Vector3.zero;

            stopFlipFactor.x = vp.PitchInput * flipPower.x == 0 ? Mathf.Pow(Mathf.Clamp01(vp.UpDot), Mathf.Clamp(10 - Mathf.Abs(vp.LocalAngularVel.x), 2, 10)) * 10 : 0;
            stopFlipFactor.y = vp.YawInput * flipPower.y == 0 && vp.SqrVelMag > 5 ? Mathf.Pow(Mathf.Clamp01(Vector3.Dot(vp.ForwardDir, velDir * Vector3.up)), Mathf.Clamp(10 - Mathf.Abs(vp.LocalAngularVel.y), 2, 10)) * 10 : 0;
            stopFlipFactor.z = vp.RollInput * flipPower.z == 0 ? Mathf.Pow(Mathf.Clamp01(vp.UpDot), Mathf.Clamp(10 - Mathf.Abs(vp.LocalAngularVel.z), 2, 10)) * 10 : 0;

            rb.AddRelativeTorque(new Vector3(-vp.LocalAngularVel.x * stopFlipFactor.x, -vp.LocalAngularVel.y * stopFlipFactor.y, -vp.LocalAngularVel.z * stopFlipFactor.z), ForceMode.Acceleration);
        }

        // Apply forces to align vehicle with normal of ground surface that it will land on
        void ApplyRotationCorrection() {
            float actualForwardDot = vp.ForwardDot;
            float actualRightDot = vp.RightDot;
            float actualUpDot = vp.UpDot;

            if (groundCheckDistance > 0) {
                RaycastHit groundHit;

                if (Physics.Raycast(tr.position, (-GlobalControl.worldUpDir + rb.linearVelocity).normalized, out groundHit, groundCheckDistance, GlobalControl.groundMaskStatic)) {
                    if (Vector3.Dot(groundHit.normal, GlobalControl.worldUpDir) >= groundSteepnessLimit) {
                        actualForwardDot = Vector3.Dot(vp.ForwardDir, groundHit.normal);
                        actualRightDot = Vector3.Dot(vp.RightDir, groundHit.normal);
                        actualUpDot = Vector3.Dot(vp.UpDir, groundHit.normal);
                    }
                }
            }

            rb.AddRelativeTorque(new Vector3(
                vp.PitchInput * flipPower.x == 0 ? actualForwardDot * (1 - Mathf.Abs(actualRightDot)) * rotationCorrection.x - vp.LocalAngularVel.x * Mathf.Pow(actualUpDot, 2) * 10 : 0,
                vp.YawInput * flipPower.y == 0 && vp.SqrVelMag > 10 ? Vector3.Dot(vp.ForwardDir, velDir * Vector3.right) * Mathf.Abs(actualUpDot) * rotationCorrection.y - vp.LocalAngularVel.y * Mathf.Pow(actualUpDot, 2) * 10 : 0,
                vp.RollInput * flipPower.z == 0 ? -actualRightDot * (1 - Mathf.Abs(actualForwardDot)) * rotationCorrection.z - vp.LocalAngularVel.z * Mathf.Pow(actualUpDot, 2) * 10 : 0
                ), ForceMode.Acceleration);
        }

        // Apply diving force
        void Dive() {
            rb.AddTorque(velDir * Vector3.left * Mathf.Clamp01(vp.VelMag * 0.01f) * Mathf.Clamp01(vp.UpDot) * diveFactor, ForceMode.Acceleration);
        }
    }
}