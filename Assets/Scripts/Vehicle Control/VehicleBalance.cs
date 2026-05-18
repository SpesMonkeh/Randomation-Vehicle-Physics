using UnityEngine;

namespace RVP
{
    [RequireComponent(typeof(VehicleParent))]
    [DisallowMultipleComponent]
    [AddComponentMenu("RVP/Vehicle Controllers/Vehicle Balance", 4)]

    // Class for balancing vehicles
    public class VehicleBalance : MonoBehaviour
    {
        Transform tr;
        Rigidbody rb;
        VehicleParent vp;

        float actualPitchInput;
        Vector3 targetLean;
        Vector3 targetLeanActual;

        [Tooltip("Lean strength along each axis")]
        public Vector3 leanFactor;

        [Range(0, 0.99f)]
        public float leanSmoothness;

        [Tooltip("Adjusts the roll based on the speed, x-axis = speed, y-axis = roll amount")]
        public AnimationCurve leanRollCurve = AnimationCurve.Linear(0, 0, 10, 1);

        [Tooltip("Adjusts the pitch based on the speed, x-axis = speed, y-axis = pitch amount")]
        public AnimationCurve leanPitchCurve = AnimationCurve.Linear(0, 0, 1, 1);

        [Tooltip("Adjusts the yaw based on the speed, x-axis = speed, y-axis = yaw amount")]
        public AnimationCurve leanYawCurve = AnimationCurve.Linear(0, 0, 1, 1);

        [Tooltip("Speed above which endos (forward wheelies) aren't allowed")]
        public float endoSpeedThreshold;

        [Tooltip("Exponent for pitch input")]
        public float pitchExponent;

        [Tooltip("How much to lean when sliding sideways")]
        public float slideLeanFactor = 1;

        void Start() {
            tr = transform;
            rb = GetComponent<Rigidbody>();
            vp = GetComponent<VehicleParent>();
        }

        void FixedUpdate() {
            // Apply endo limit
            actualPitchInput = vp.Wheels.Length == 1 ? 0 : Mathf.Clamp(vp.PitchInput, -1, vp.VelMag > endoSpeedThreshold ? 0 : 1);

            if (vp.GroundedWheels > 0) {
                if (leanFactor != Vector3.zero) {
                    ApplyLean();
                }
            }
        }

        // Apply corrective balance forces
        void ApplyLean() {
            if (vp.GroundedWheels > 0) {
                Vector3 inverseWorldUp;
                inverseWorldUp = vp.Norm.InverseTransformDirection(Vector3.Dot(vp.WheelNormalAverage, GlobalControl.worldUpDir) <= 0 ? vp.WheelNormalAverage : Vector3.Lerp(GlobalControl.worldUpDir, vp.WheelNormalAverage, Mathf.Abs(Vector3.Dot(vp.Norm.up, GlobalControl.worldUpDir)) * 2));
                Debug.DrawRay(tr.position, vp.Norm.TransformDirection(inverseWorldUp), Color.white);

                // Calculate target lean direction
                targetLean = new Vector3(
                    Mathf.Lerp(
                        inverseWorldUp.x,
                        Mathf.Clamp(-vp.RollInput * leanFactor.z * leanRollCurve.Evaluate(Mathf.Abs(vp.LocalVelocity.z)) + Mathf.Clamp(vp.LocalVelocity.x * slideLeanFactor, -leanFactor.z * slideLeanFactor, leanFactor.z * slideLeanFactor), -leanFactor.z, leanFactor.z),
                        Mathf.Max(Mathf.Abs(F.MaxAbs(vp.SteerInput, vp.RollInput)))),
                    Mathf.Pow(Mathf.Abs(actualPitchInput), pitchExponent) * Mathf.Sign(actualPitchInput) * leanFactor.x,
                    inverseWorldUp.z * (1 - Mathf.Abs(F.MaxAbs(actualPitchInput * leanFactor.x, vp.RollInput * leanFactor.z))));
            }
            else {
                targetLean = vp.UpDir;
            }

            // Transform targetLean to world space
            targetLeanActual = Vector3.Lerp(targetLeanActual, vp.Norm.TransformDirection(targetLean), (1 - leanSmoothness) * Time.timeScale * TimeMaster.inverseFixedTimeFactor).normalized;
            Debug.DrawRay(tr.position, targetLeanActual, Color.black);

            // Apply pitch
            rb.AddTorque(
                vp.Norm.right * -(Vector3.Dot(vp.ForwardDir, targetLeanActual) * 20 - vp.LocalAngularVel.x) * 100 * (vp.Wheels.Length == 1 ? 1 : leanPitchCurve.Evaluate(Mathf.Abs(actualPitchInput))),
                ForceMode.Acceleration);

            // Apply yaw
            rb.AddTorque(
                vp.Norm.forward * (vp.GroundedWheels == 1 ? vp.SteerInput * leanFactor.y - vp.Norm.InverseTransformDirection(rb.angularVelocity).z : 0) * 100 * leanYawCurve.Evaluate(Mathf.Abs(vp.SteerInput)),
                ForceMode.Acceleration);

            // Apply roll
            rb.AddTorque(
                vp.Norm.up * (-Vector3.Dot(vp.RightDir, targetLeanActual) * 20 - vp.LocalAngularVel.z) * 100,
                ForceMode.Acceleration);

            // Turn vehicle during wheelies
            if (vp.GroundedWheels == 1 && leanFactor.y > 0) {
                rb.AddTorque(vp.Norm.TransformDirection(
                    new Vector3(0, 0, vp.SteerInput * leanFactor.y - vp.Norm.InverseTransformDirection(rb.angularVelocity).z)
                    ), ForceMode.Acceleration);
            }
        }
    }
}