using UnityEngine;
using System;

namespace RVP
{
	/// Class for changing the properties of the suspension
	[RequireComponent(typeof(Suspension))] [DisallowMultipleComponent] [AddComponentMenu("RVP2WoL/Suspension/Suspension Property", 2)]
	public class SuspensionPropertyToggle : MonoBehaviour
	{
		[SerializeField] Suspension suspension;
		[SerializeField] SuspensionToggledProperty[] properties = Array.Empty<SuspensionToggledProperty>();

		internal Suspension Suspension => suspension;
		internal SuspensionToggledProperty[] Properties => properties;

		void Awake()
		{
			suspension = GetComponent<Suspension>();
		}

		/// Toggle a property in the properties array at index
		public void ToggleProperty(int index)
		{
			if (properties.Length - 1 < index)
				return;

			properties[index].IsEnabled = !properties[index].IsEnabled;
			suspension.UpdateProperties();
		}

		/// Set a property in the properties array at index to the value
		public void SetProperty(int index, bool value)
		{
			if (properties.Length - 1 < index)
				return;
			properties[index].IsEnabled = value;
			suspension.UpdateProperties();
		}
	}

	/// Class for a single property
	[Serializable]
	internal class SuspensionToggledProperty
	{
		internal enum Properties
		{
			SteerEnable,
			SteerInvert,
			DriveEnable,
			DriveInvert,
			EBrakeEnable,
			SkidSteerBrake // skidSteerBrake = brake is specially adjusted for skid steering
		}

		[SerializeField] bool isEnabled;
		[SerializeField] Properties property;

		internal bool IsEnabled { get => isEnabled; set => isEnabled = value; }
		internal Properties Property { get => property; set => property = value; }
	}
}
