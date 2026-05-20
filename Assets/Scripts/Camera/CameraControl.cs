using UnityEngine;
using static UnityEngine.Mathf;

namespace RVP
{
	[RequireComponent(typeof(Camera))]
	[RequireComponent(typeof(AudioListener))]
	[DisallowMultipleComponent]
	[AddComponentMenu("RVP/Camera/Camera Control", 0)]
	public class CameraControl : MonoBehaviour
	{
		[SerializeField] Camera vehicleCamera;
		[SerializeField] Rigidbody targetBody;
		[SerializeField] Transform camTransform;
		[SerializeField] Transform targetVehicle;
		[SerializeField] VehicleParent vehicleParent;

		[SerializeField, Min(0)] float height;
		[SerializeField, Min(0)] float distance;

		[SerializeField] float xInput; // READONLY
		[SerializeField] float yInput; // READONLY
		[SerializeField] Vector3 lookDir;
		[SerializeField] float smoothYRot;
		[SerializeField] Transform lookObj;
		[SerializeField] Vector3 forwardLook;
		[SerializeField] Vector3 upLook;
		[SerializeField] Vector3 targetForward;
		[SerializeField] Vector3 targetUp;
		[Tooltip("Should the camera stay flat? (Local y-axis always points up)")]
		[SerializeField] bool stayFlat;

		[Tooltip("Mask for which objects will be checked in between the camera and target vehicle")]
		[SerializeField] LayerMask castMask;

		internal bool StayFlat { get => stayFlat; set => stayFlat = value; }
		internal float Height => height;
		internal float Distance => distance;
		internal float XInput => xInput;
		internal float YInput => yInput;
		internal float SmoothYRot => smoothYRot;
		internal Camera VehicleCamera => vehicleCamera;
		internal Rigidbody TargetBody => targetBody;
		internal Transform CamTransform => camTransform;
		internal Transform TargetVehicle { get => targetVehicle; set => targetVehicle = value; }
		internal Transform LookObj => lookObj;
		internal VehicleParent VehicleParent => vehicleParent;
		internal Vector3 UpLook => upLook;
		internal Vector3 LookDir => lookDir;
		internal Vector3 ForwardLook => forwardLook;
		internal Vector3 TargetUp => targetUp;
		internal Vector3 TargetForward => targetForward;
		internal LayerMask CastMask => castMask;

		void Start()
		{
			camTransform = transform;
			vehicleCamera = GetComponent<Camera>();
			Initialize();
		}

		public void Initialize()
		{
			// lookObj is an object used to help position and rotate the camera
			if (!lookObj)
			{
				GameObject lookTemp = new("Camera Looker");
				lookObj = lookTemp.transform;
			}

			// Set variables based on target vehicle's properties
			if (targetVehicle)
			{
				vehicleParent = targetVehicle.GetComponent<VehicleParent>();
				distance += vehicleParent.CameraDistanceChange;
				height += vehicleParent.CameraHeightChange;
				forwardLook = targetVehicle.forward;
				upLook = targetVehicle.up;
				targetBody = targetVehicle.GetComponent<Rigidbody>();
			}

			// Set the audio listener update mode to fixed, because the camera moves in FixedUpdate
			// This is necessary for doppler effects to sound correct
			GetComponent<AudioListener>().velocityUpdateMode = AudioVelocityUpdateMode.Fixed;

			if (!targetVehicle || !targetBody)
			{
				Debug.LogError($"{name}: Target vehicle or target body not set.", this);
			}

		}

		void FixedUpdate()
		{
			if (targetVehicle.gameObject.activeSelf is false)
				return;

			if (vehicleParent.GroundedWheels > 0)
				targetForward = StayFlat
					? new Vector3(vehicleParent.Norm.up.x, 0, vehicleParent.Norm.up.z)
					: vehicleParent.Norm.up;

			// Alternate case to have the airborne forward direction match the vehicle's velocity
			/*else {
				    targetForward = targetBody.linearVelocity.normalized;
				}*/

			targetUp = StayFlat
				? GlobalControl.worldUpDir
				: vehicleParent.Norm.forward;
			lookDir = Vector3.Slerp(lookDir, xInput is 0 && yInput is 0
					? Vector3.forward
					: new Vector3(xInput, 0, yInput).normalized,
				0.1f * TimeMaster.inverseFixedTimeFactor);
			smoothYRot = Lerp(smoothYRot, targetBody.angularVelocity.y, 0.02f * TimeMaster.inverseFixedTimeFactor);

			// Determine the upwards direction of the camera
			RaycastHit hit;
			if (Physics.Raycast(targetVehicle.position, -targetUp, out hit, 1, castMask) && !StayFlat)
				upLook = Vector3.Lerp(upLook, Vector3.Dot(hit.normal, targetUp) > 0.5
						? hit.normal
						: targetUp,
					0.05f * TimeMaster.inverseFixedTimeFactor);
			else
				upLook = Vector3.Lerp(upLook, targetUp, 0.05f * TimeMaster.inverseFixedTimeFactor);

			// Calculate rotation and position variables
			forwardLook = Vector3.Lerp(forwardLook, targetForward, 0.05f * TimeMaster.inverseFixedTimeFactor);
			lookObj.rotation = Quaternion.LookRotation(forwardLook, upLook);
			lookObj.position = targetVehicle.position;
			Vector3 lookDirActual = (lookDir - new Vector3(
					Sin(smoothYRot),
					0,
					Cos(smoothYRot)) * (Abs(smoothYRot) * 0.2f))
				.normalized;
			Vector3 forwardDir = lookObj.TransformDirection(lookDirActual);
			Vector3 localOffset = lookObj.TransformPoint(-lookDirActual * distance -
				lookDirActual * Min(targetBody.linearVelocity.magnitude * 0.05f, 2) + Vector3.up * height);

			// Check if there is an object between the camera and target vehicle and move the camera in front of it
			if (Physics.Linecast(targetVehicle.position, localOffset, out hit, castMask))
				camTransform.position = hit.point + (targetVehicle.position - localOffset).normalized * (vehicleCamera.nearClipPlane + 0.1f);
			else
				camTransform.position = localOffset;

			camTransform.rotation = Quaternion.LookRotation(forwardDir, lookObj.up);
		}

		/// Sets the rotation input of the <see cref="vehicleCamera"/>.
		public void SetInput(float x, float y)
		{
			xInput = x;
			yInput = y;
		}

		void OnDestroy()
		{
			if (lookObj)
				Destroy(lookObj.gameObject);
		}
	}
}
