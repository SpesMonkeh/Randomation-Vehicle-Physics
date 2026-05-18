using UnityEngine;

namespace RVP
{
    [RequireComponent(typeof(VehicleParent))]
    [DisallowMultipleComponent]
    [AddComponentMenu("RVP/Vehicle Controllers/Vehicle Assist", 1)]

    // Class for assisting vehicle performance
    public class VehicleAssist : MonoBehaviour
    {
        Transform tr;
        Rigidbody rb;
        VehicleParent vp;

        [Header("Drift")]

        [Tooltip("Variables are multiplied based on the number of wheels grounded out of the total number of wheels")]
        public bool basedOnWheelsGrounded;
        float groundedFactor;

        [Tooltip("How much to assist with spinning while drifting")]
        public float driftSpinAssist;
        public float driftSpinSpeed;
        public float driftSpinExponent = 1;

        [Tooltip("Automatically adjust drift angle based on steer input magnitude")]
        public bool autoSteerDrift;
        public float maxDriftAngle = 70;
        float targetDriftAngle;

        [Tooltip("Adjusts the force based on drift speed, x-axis = speed, y-axis = force")]
        public AnimationCurve driftSpinCurve = AnimationCurve.Linear(0, 0, 10, 1);

        [Tooltip("How much to push the vehicle forward while drifting")]
        public float driftPush;

        [Tooltip("Straighten out the vehicle when sliding slightly")]
        public bool straightenAssist;

        [Header("Downforce")]
        public float downforce = 1;
        public bool invertDownforceInReverse;
        public bool applyDownforceInAir;

        [Tooltip("X-axis = speed, y-axis = force")]
        public AnimationCurve downforceCurve = AnimationCurve.Linear(0, 0, 20, 1);

        [Header("Roll Over")]

        [Tooltip("Automatically roll over when rolled over")]
        public bool autoRollOver;

        [Tooltip("Roll over with steer input")]
        public bool steerRollOver;

        [System.NonSerialized]
        public bool rolledOver;

        [Tooltip("Distance to check on sides to see if rolled over")]
        public float rollCheckDistance = 1;
        public float rollOverForce = 1;

        [Tooltip("Maximum speed at which vehicle can be rolled over with assists")]
        public float rollSpeedThreshold;

        [Header("Air")]

        [Tooltip("Increase angular drag immediately after jumping")]
        public bool angularDragOnJump;
        float initialAngularDrag;
        float angDragTime = 0;

        public float fallSpeedLimit = Mathf.Infinity;
        public bool applyFallLimitUpwards;

        void Start() {
            tr = transform;
            rb = GetComponent<Rigidbody>();
            vp = GetComponent<VehicleParent>();
            initialAngularDrag = rb.angularDamping;
        }

        void FixedUpdate() {
            if (vp.GroundedWheels > 0) {
                groundedFactor = basedOnWheelsGrounded ? vp.GroundedWheels / (vp.Hover ? vp.HoverWheels.Length : vp.Wheels.Length) : 1;

                angDragTime = 20;
                rb.angularDamping = initialAngularDrag;

                if (driftSpinAssist > 0) {
                    ApplySpinAssist();
                }

                if (driftPush > 0) {
                    ApplyDriftPush();
                }
            }
            else {
                if (angularDragOnJump) {
                    angDragTime = Mathf.Max(0, angDragTime - Time.timeScale * TimeMaster.inverseFixedTimeFactor);
                    rb.angularDamping = angDragTime > 0 && vp.UpDot > 0.5 ? 10 : initialAngularDrag;
                }
            }

            if (downforce > 0) {
                ApplyDownforce();
            }

            if (autoRollOver || steerRollOver) {
                RollOver();
            }

            if (Mathf.Abs(vp.LocalVelocity.y) > fallSpeedLimit && (vp.LocalVelocity.y < 0 || applyFallLimitUpwards)) {
                rb.AddRelativeForce(Vector3.down * vp.LocalVelocity.y, ForceMode.Acceleration);
            }
        }

        // Apply assist for steering and drifting
        void ApplySpinAssist() {
            // Get desired rotation speed
            float targetTurnSpeed = 0;

            // Auto steer drift
            if (autoSteerDrift) {
                int steerSign = 0;
                if (vp.SteerInput != 0) {
                    steerSign = (int)Mathf.Sign(vp.SteerInput);
                }

                targetDriftAngle = (steerSign != Mathf.Sign(vp.LocalVelocity.x) ? vp.SteerInput : steerSign) * -maxDriftAngle;
                Vector3 velDir = new Vector3(vp.LocalVelocity.x, 0, vp.LocalVelocity.z).normalized;
                Vector3 targetDir = new Vector3(Mathf.Sin(targetDriftAngle * Mathf.Deg2Rad), 0, Mathf.Cos(targetDriftAngle * Mathf.Deg2Rad)).normalized;
                Vector3 driftTorqueTemp = velDir - targetDir;
                targetTurnSpeed = driftTorqueTemp.magnitude * Mathf.Sign(driftTorqueTemp.z) * steerSign * driftSpinSpeed - vp.LocalAngularVel.y * Mathf.Clamp01(Vector3.Dot(velDir, targetDir)) * 2;
            }
            else {
                targetTurnSpeed = vp.SteerInput * driftSpinSpeed * (vp.LocalVelocity.z < 0 ? (vp.AccelAxisIsBrake ? Mathf.Sign(vp.AccelInput) : Mathf.Sign(F.MaxAbs(vp.AccelInput, -vp.BrakeInput))) : 1);
            }

            rb.AddRelativeTorque(
                new Vector3(0, (targetTurnSpeed - vp.LocalAngularVel.y) * driftSpinAssist * driftSpinCurve.Evaluate(Mathf.Abs(Mathf.Pow(vp.LocalVelocity.x, driftSpinExponent))) * groundedFactor, 0),
                ForceMode.Acceleration);

            float rightVelDot = Vector3.Dot(tr.right, rb.linearVelocity.normalized);

            if (straightenAssist && vp.SteerInput == 0 && Mathf.Abs(rightVelDot) < 0.1f && vp.SqrVelMag > 5) {
                rb.AddRelativeTorque(
                    new Vector3(0, rightVelDot * 100 * Mathf.Sign(vp.LocalVelocity.z) * driftSpinAssist, 0),
                    ForceMode.Acceleration);
            }
        }

        // Apply downforce
        void ApplyDownforce() {
            if (vp.GroundedWheels > 0 || applyDownforceInAir) {
                rb.AddRelativeForce(
                    new Vector3(0, downforceCurve.Evaluate(Mathf.Abs(vp.LocalVelocity.z)) * -downforce * (applyDownforceInAir ? 1 : groundedFactor) * (invertDownforceInReverse ? Mathf.Sign(vp.LocalVelocity.z) : 1), 0),
                    ForceMode.Acceleration);

                // Reverse downforce
                if (invertDownforceInReverse && vp.LocalVelocity.z < 0) {
                    rb.AddRelativeTorque(
                        new Vector3(downforceCurve.Evaluate(Mathf.Abs(vp.LocalVelocity.z)) * downforce * (applyDownforceInAir ? 1 : groundedFactor), 0, 0),
                        ForceMode.Acceleration);
                }
            }
        }

        // Assist with rolling back over if upside down or on side
        void RollOver() {
            RaycastHit rollHit;

            // Check if rolled over
            if (vp.GroundedWheels == 0 && vp.VelMag < rollSpeedThreshold && vp.UpDot < 0.8 && rollCheckDistance > 0) {
                if (Physics.Raycast(tr.position, vp.UpDir, out rollHit, rollCheckDistance, GlobalControl.groundMaskStatic)
                    || Physics.Raycast(tr.position, vp.RightDir, out rollHit, rollCheckDistance, GlobalControl.groundMaskStatic)
                    || Physics.Raycast(tr.position, -vp.RightDir, out rollHit, rollCheckDistance, GlobalControl.groundMaskStatic)) {
                    rolledOver = true;
                }
                else {
                    rolledOver = false;
                }
            }
            else {
                rolledOver = false;
            }

            // Apply roll over force
            if (rolledOver) {
                if (steerRollOver && vp.SteerInput != 0) {
                    rb.AddRelativeTorque(
                        new Vector3(0, 0, -vp.SteerInput * rollOverForce),
                        ForceMode.Acceleration);
                }
                else if (autoRollOver) {
                    rb.AddRelativeTorque(
                        new Vector3(0, 0, -Mathf.Sign(vp.RightDot) * rollOverForce),
                        ForceMode.Acceleration);
                }
            }
        }

        // Assist for accelerating while drifting
        void ApplyDriftPush() {
            float pushFactor = (vp.AccelAxisIsBrake ? vp.AccelInput : vp.AccelInput - vp.BrakeInput) * Mathf.Abs(vp.LocalVelocity.x) * driftPush * groundedFactor * (1 - Mathf.Abs(Vector3.Dot(vp.ForwardDir, rb.linearVelocity.normalized)));

            rb.AddForce(
                vp.Norm.TransformDirection(new Vector3(Mathf.Abs(pushFactor) * Mathf.Sign(vp.LocalVelocity.x), Mathf.Abs(pushFactor) * Mathf.Sign(vp.LocalVelocity.z), 0)),
                ForceMode.Acceleration);
        }
    }
}
