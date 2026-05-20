using UnityEngine;

namespace RVP
{

    /// Class for cycling through suspension properties
    [AddComponentMenu("RVP/Suspension/Suspension Property Setter", 3)]
    public class PropertyToggleSetter : MonoBehaviour
    {
        [Tooltip("Steering Controller")]
        public SteeringControl steerer;
        public Transmission transmission;

        [Tooltip("Suspensions with properties to be toggled")]
        public SuspensionPropertyToggle[] suspensionProperties;
        public PropertyTogglePreset[] presets;
        public int currentPreset;

        [Tooltip("Input manager button which increments the preset")]
        public string changeButton;

        void OnEnable()
        {
            PlayerControlsHandler.NextPresetAction += OnNextPreset;
        }

        void OnDisable()
        {
            PlayerControlsHandler.NextPresetAction -= OnNextPreset;
        }

        public void OnNextPreset()
        {
            currentPreset = (currentPreset + 1) % (presets.Length);

            if (steerer) {
                steerer.limitSteer = presets[currentPreset].limitSteer;
            }

            if (transmission) {
                transmission.skidSteerDrive = presets[currentPreset].skidSteerTransmission;
            }

            for (int i = 0; i < suspensionProperties.Length; i++) {
                for (int j = 0; j < suspensionProperties[i].Properties.Length; j++) {
                    suspensionProperties[i].SetProperty(j, presets[currentPreset].wheels[i].preset[j]);
                }
            }
        }
    }

    // Preset class
    [System.Serializable]
    public class PropertyTogglePreset
    {
        [Tooltip("Limit the steering range of wheels based on SteeringControl's curve?")]
        public bool limitSteer = true;
        [Tooltip("Transmission is adjusted for skid steering?")]
        public bool skidSteerTransmission;
        [Tooltip("Must be equal to the number of wheels")]
        public IndividualPreset[] wheels;
    }

    // Class for toggling the properties of SuspensionPropertyToggle instances
    [System.Serializable]
    public class IndividualPreset
    {
        [Tooltip("Must be equal to the SuspensionPropertyToggle properties array length")]
        public bool[] preset;
    }
}
