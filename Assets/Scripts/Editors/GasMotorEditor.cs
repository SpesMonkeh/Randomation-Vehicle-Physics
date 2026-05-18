#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace RVP
{
	[CustomEditor(typeof(GasMotor))] [CanEditMultipleObjects]
	public class GasMotorEditor : Editor
	{
		float topSpeed;

		const float PI_X100 = Mathf.PI * 100;
		const float TAU = Mathf.PI * 2;

		public override void OnInspectorGUI()
		{
			GasMotor targetScript = (GasMotor)target;
			bool reachedEnd = false;
			string endOutput = "";

			if (targetScript.outputDrives != null)
			{
				if (targetScript.outputDrives.Length > 0)
				{
					topSpeed = targetScript.torqueCurve.keys[targetScript.torqueCurve.length - 1].time * 1000;
					DriveForce nextOutput = targetScript.outputDrives[0];

					while (!reachedEnd)
					{
						if (nextOutput)
						{
							if (nextOutput.TryGetComponent(out Transmission nextTrans))
							{
								switch (nextTrans)
								{
									case GearboxTransmission nextGearbox:
										topSpeed /= nextGearbox.gears[^1].ratio;
										break;
									case ContinuousTransmission nextConTrans:
										topSpeed /= nextConTrans.maxRatio;
										break;
								}

								if (nextTrans.outputDrives.Length > 0)
								{
									nextOutput = nextTrans.outputDrives[0];
								}
								else
								{
									topSpeed = -1;
									reachedEnd = true;
									endOutput = nextTrans.transform.name;
								}
							}
							else if (nextOutput.TryGetComponent(out Suspension nextSus))
							{
								if (nextSus.wheel)
								{
									topSpeed /= PI_X100;
									topSpeed *= nextSus.wheel.TireRadius * TAU;
								}
								else
								{
									topSpeed = -1;
								}

								reachedEnd = true;
								endOutput = nextSus.transform.name;
							}
							else
							{
								topSpeed = -1;
								reachedEnd = true;
								endOutput = targetScript.transform.name;
							}
						}
						else
						{
							topSpeed = -1;
							reachedEnd = true;
							endOutput = targetScript.transform.name;
						}
					}
				}
				else
				{
					topSpeed = -1;
					endOutput = targetScript.transform.name;
				}
			}
			else
			{
				topSpeed = -1;
				endOutput = targetScript.transform.name;
			}

			if (Mathf.Approximately(topSpeed, -1))
				EditorGUILayout.HelpBox("Motor drive doesn't reach any wheels.  (Ends at " + endOutput + ")", MessageType.Warning);
			else if (targets.Length == 1)
				EditorGUILayout.LabelField(
					"Top Speed (Estimate): " + (topSpeed * 2.23694f).ToString("0.00") + " mph || " + (topSpeed * 3.6f).ToString("0.00") + " km/h",
					EditorStyles.boldLabel);

			DrawDefaultInspector();
		}
	}
}
#endif
