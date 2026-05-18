using UnityEngine;

namespace RVP
{
    [RequireComponent(typeof(VehicleParent))]
    [DisallowMultipleComponent]
    [AddComponentMenu("RVP/Effects/Light Controller", 2)]

    // Class for controlling vehicle lights
    public class LightController : MonoBehaviour
    {
        VehicleParent vp;

        public bool headlightsOn;
        public bool highBeams;
        public bool brakelightsOn;
        public bool rightBlinkersOn;
        public bool leftBlinkersOn;
        public float blinkerInterval = 0.3f;
        bool blinkerIntervalOn;
        float blinkerSwitchTime;
        public bool reverseLightsOn;

        public Transmission transmission;
        GearboxTransmission gearTrans;
        ContinuousTransmission conTrans;

        public VehicleLight[] headlights;
        public VehicleLight[] brakeLights;
        public VehicleLight[] RightBlinkers;
        public VehicleLight[] LeftBlinkers;
        public VehicleLight[] ReverseLights;

        void Start() {
            vp = GetComponent<VehicleParent>();

            // Get transmission for using reverse lights
            if (transmission) {
                if (transmission is GearboxTransmission) {
                    gearTrans = transmission as GearboxTransmission;
                }
                else if (transmission is ContinuousTransmission) {
                    conTrans = transmission as ContinuousTransmission;
                }
            }
        }

        void Update() {
            // Activate blinkers
            if (leftBlinkersOn || rightBlinkersOn) {
                if (blinkerSwitchTime == 0) {
                    blinkerIntervalOn = !blinkerIntervalOn;
                    blinkerSwitchTime = blinkerInterval;
                }
                else {
                    blinkerSwitchTime = Mathf.Max(0, blinkerSwitchTime - Time.deltaTime);
                }
            }
            else {
                blinkerIntervalOn = false;
                blinkerSwitchTime = 0;
            }

            // Activate reverse lights
            if (gearTrans) {
                reverseLightsOn = gearTrans.curGearRatio < 0;
            }
            else if (conTrans) {
                reverseLightsOn = conTrans.reversing;
            }

            // Activate brake lights
            if (vp.AccelAxisIsBrake) {
                brakelightsOn = vp.AccelInput != 0 && Mathf.Sign(vp.AccelInput) != Mathf.Sign(vp.LocalVelocity.z) && Mathf.Abs(vp.LocalVelocity.z) > 1;
            }
            else {
                if (!vp.BrakeIsReverse) {
                    brakelightsOn = (vp.Burnout > 0 && vp.BrakeInput > 0) || vp.BrakeInput > 0;
                }
                else {
                    brakelightsOn = (vp.Burnout > 0 && vp.BrakeInput > 0) || ((vp.BrakeInput > 0 && vp.LocalVelocity.z > 1) || (vp.AccelInput > 0 && vp.LocalVelocity.z < -1));
                }
            }

            SetLights(headlights, highBeams, headlightsOn);
            SetLights(brakeLights, headlightsOn || highBeams, brakelightsOn);
            SetLights(RightBlinkers, rightBlinkersOn && blinkerIntervalOn);
            SetLights(LeftBlinkers, leftBlinkersOn && blinkerIntervalOn);
            SetLights(ReverseLights, reverseLightsOn);
        }

        // Set if lights are on or off based on the condition
        void SetLights(VehicleLight[] lights, bool condition) {
            foreach (VehicleLight curLight in lights) {
                curLight.on = condition;
            }
        }

        // Set if lights are on or off based on the first condition, and half on based on the second condition (see halfOn tooltip in VehicleLight)
        void SetLights(VehicleLight[] lights, bool condition, bool halfCondition) {
            foreach (VehicleLight curLight in lights) {
                curLight.on = condition;
                curLight.halfOn = halfCondition;
            }
        }
    }
}