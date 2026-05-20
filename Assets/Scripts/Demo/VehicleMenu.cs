using UnityEngine;
using UnityEngine.UI;

namespace RVP
{
	[DisallowMultipleComponent]
	[AddComponentMenu("RVP/Demo Scripts/Vehicle Menu", 0)]

	// Class for the menu in the demo
	public class VehicleMenu : MonoBehaviour
	{
		[SerializeField] CameraControl cam;
		[SerializeField] Vector3 spawnPoint;
		[SerializeField] Vector3 spawnRot;
		[SerializeField] GameObject[] vehicles;
		[SerializeField] GameObject chaseVehicle;
		[SerializeField] GameObject chaseVehicleDamage;
		[SerializeField, Min(0)] float chaseCarSpawnTime; // Hidden in inspector
		[SerializeField] GameObject newVehicle; // Hidden in inspector
		[SerializeField] Toggle autoShiftToggle;
		[SerializeField] Toggle assistToggle;
		[SerializeField] Toggle stuntToggle;
		[SerializeField] Toggle camToggle;
		[SerializeField] VehicleHud hud;

		void Update()
		{
			cam.StayFlat = camToggle.isOn;
			chaseCarSpawnTime = Mathf.Max(0, chaseCarSpawnTime - Time.deltaTime);
		}

		/// Spawns a vehicle from the vehicles array at the index
		public void SpawnVehicle(int vehicle)
		{
			newVehicle = Instantiate(vehicles[vehicle], spawnPoint, Quaternion.LookRotation(spawnRot, GlobalControl.worldUpDir));
			cam.TargetVehicle = newVehicle.transform;
			cam.Initialize();

			if (newVehicle.GetComponent<VehicleAssist>()) newVehicle.GetComponent<VehicleAssist>().enabled = assistToggle.isOn;

			Transmission trans = newVehicle.GetComponentInChildren<Transmission>();
			if (trans)
			{
				trans.automatic = autoShiftToggle.isOn;
				newVehicle.GetComponent<VehicleParent>().BrakeIsReverse = autoShiftToggle.isOn;

				if (trans is ContinuousTransmission && !autoShiftToggle.isOn) newVehicle.GetComponent<VehicleParent>().BrakeIsReverse = true;
			}

			if (newVehicle.GetComponent<FlipControl>() && newVehicle.GetComponent<StuntDetect>())
			{
				newVehicle.GetComponent<FlipControl>().flipPower = stuntToggle.isOn && assistToggle.isOn ? new Vector3(10, 10, -10) : Vector3.zero;
				newVehicle.GetComponent<FlipControl>().rotationCorrection =
					stuntToggle.isOn ? Vector3.zero : assistToggle.isOn ? new Vector3(5, 1, 10) : Vector3.zero;
				newVehicle.GetComponent<FlipControl>().stopFlip = assistToggle.isOn;
			}

			if (!hud)
				return;
			hud.stuntMode = stuntToggle.isOn;
			hud.Initialize(newVehicle);
		}

		/// Spawns a chasing vehicle
		public void SpawnChaseVehicle()
		{
			if (chaseCarSpawnTime == 0)
			{
				chaseCarSpawnTime = 1;
				GameObject chaseCar = Instantiate(chaseVehicle, spawnPoint, Quaternion.LookRotation(spawnRot, GlobalControl.worldUpDir));
				chaseCar.GetComponent<FollowAI>().target = newVehicle.transform;
			}
		}

		/// Spawns a damageable chasing vehicle
		public void SpawnChaseVehicleDamage()
		{
			if (chaseCarSpawnTime == 0)
			{
				chaseCarSpawnTime = 1;
				GameObject chaseCar = Instantiate(chaseVehicleDamage, spawnPoint, Quaternion.LookRotation(spawnRot, GlobalControl.worldUpDir));
				chaseCar.GetComponent<FollowAI>().target = newVehicle.transform;
			}
		}
	}
}
