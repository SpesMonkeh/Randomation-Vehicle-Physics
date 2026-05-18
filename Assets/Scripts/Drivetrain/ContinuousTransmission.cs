using UnityEngine;

namespace RVP
{
    [DisallowMultipleComponent]
    [AddComponentMenu("RVP/Drivetrain/Transmission/Continuous Transmission", 1)]

    // Transmission subclass for continuously variable transmission
    public class ContinuousTransmission : Transmission
    {
        [Tooltip("Lerp value between min ratio and max ratio")]
        [Range(0, 1)]
        public float targetRatio;
        public float minRatio;
        public float maxRatio;
        [System.NonSerialized]
        public float currentRatio;
        public bool canReverse;
        [System.NonSerialized]
        public bool reversing;

        [Tooltip("How quickly the target ratio changes with manual shifting")]
        public float manualShiftRate = 0.5f;

        void FixedUpdate() {
            health = Mathf.Clamp01(health);

            // Set max RPM possible
            if (maxRPM == -1) {
                maxRPM = targetDrive.curve.keys[targetDrive.curve.length - 1].time * 1000;
            }

            if (health > 0) {
                if (automatic && vp.GroundedWheels > 0) {
                    // Automatically set the target ratio
                    targetRatio = (1 - vp.Burnout) * Mathf.Clamp01(Mathf.Abs(targetDrive.feedbackRPM) / Mathf.Max(0.01f, maxRPM * Mathf.Abs(currentRatio)));
                }
                else if (!automatic) {
                    // Manually set the target ratio
                    targetRatio = Mathf.Clamp01(targetRatio + (vp.UpshiftHold - vp.DownshiftHold) * manualShiftRate * Time.deltaTime);
                }
            }

            reversing = canReverse && vp.Burnout == 0 && vp.LocalVelocity.z < 1 && (vp.AccelInput < 0 || (vp.BrakeIsReverse && vp.BrakeInput > 0));
            currentRatio = Mathf.Lerp(minRatio, maxRatio, targetRatio) * (reversing ? -1 : 1);

            newDrive.curve = targetDrive.curve;
            newDrive.rpm = targetDrive.rpm / currentRatio;
            newDrive.torque = Mathf.Abs(currentRatio) * targetDrive.torque;
            SetOutputDrives(currentRatio);
        }
    }
}