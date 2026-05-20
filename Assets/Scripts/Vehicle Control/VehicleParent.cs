using System;
using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RVP
{
	[RequireComponent(typeof(Rigidbody))]
	[DisallowMultipleComponent]
	[AddComponentMenu("RVP/Vehicle Controllers/Vehicle Parent", 0)]

	// Vehicle root class
	public class VehicleParent : MonoBehaviour
	{
		[Tooltip("Accel axis is used for brake input")]
		[SerializeField]
		bool accelAxisIsBrake;

		[Tooltip("Brake input will act as reverse input")]
		[SerializeField]
		bool brakeIsReverse;

		[Tooltip("Automatically hold ebrake if it's pressed while parked")]
		[SerializeField] bool holdEbrakePark;

		[SerializeField] float burnoutThreshold = 0.9f;
		[SerializeField] float burnoutSpin = 5;
		[SerializeField] [Range(0, 0.9f)] float burnoutSmoothness = 0.5f;
		[SerializeField] Motor engine;

		bool stopUpshift;
		bool stopDownShift;

		Vector3 localVelocity; // Local space velocity

		[SerializeField] Wheel[] wheels;
		[SerializeField] HoverWheel[] hoverWheels;
		[SerializeField] WheelCheckGroup[] wheelGroups;
		bool wheelLoopDone;
		[SerializeField] bool hover;
		Vector3 wheelContactsVelocity; // Average velocity of wheel contact points

		[Tooltip("Lower center of mass by suspension height")]
		[SerializeField]
		bool suspensionCenterOfMass;
		[SerializeField] Vector3 centerOfMassOffset;

		[SerializeField] ForceMode wheelForceMode = ForceMode.Acceleration;
		[SerializeField] ForceMode suspensionForceMode = ForceMode.Acceleration;

		[Tooltip("Tow vehicle to instantiate")]
		[SerializeField]
		GameObject towVehicle;
		GameObject newTow;
		[NonSerialized] VehicleParent inputInherit; // Vehicle which to inherit input from

		[Header("Crashing")]
		[SerializeField] bool canCrash = true;
		[SerializeField] AudioSource crashSnd;
		[SerializeField] AudioClip[] crashClips;
		[SerializeField] ParticleSystem sparks;

		[Header("Camera")]
		[SerializeField] float cameraDistanceChange;
		[SerializeField] float cameraHeightChange;

		public Motor Engine { get => engine; set => engine = value; }
		public Rigidbody Rb { get; private set; }
		public Transform Tr { get; private set; }
		public Transform Norm { get; private set; }
		public WheelCheckGroup[] WheelGroups => wheelGroups;
		public float AccelInput { get; private set; }
		public float BrakeInput { get; private set; }
		public float SteerInput { get; private set; }
		public float EbrakeInput { get; private set; }
		public bool BoostButton { get; private set; }
		public bool UpshiftPressed { get; private set; }
		public bool DownshiftPressed { get; private set; }
		public float UpshiftHold { get; private set; }
		public float DownshiftHold { get; private set; }
		public float PitchInput { get; private set; }
		public float YawInput { get; private set; }
		public float RollInput { get; private set; }
		public float Burnout { get; private set; }
		public Vector3 LocalVelocity => localVelocity;
		public Vector3 LocalAngularVel { get; private set; }
		public Vector3 ForwardDir { get; private set; }
		public Vector3 RightDir { get; private set; }
		public Vector3 UpDir { get; private set; }
		public float ForwardDot { get; private set; }
		public float RightDot { get; private set; }
		public float UpDot { get; private set; }
		public float VelMag { get; private set; }
		public float SqrVelMag { get; private set; }
		public bool Reversing { get; private set; }
		public int GroundedWheels { get; private set; }
		public Vector3 WheelNormalAverage { get; private set; }
		public bool Crashing { get; private set; }
		public bool PlayCrashSounds { get; set; } = true;
		public bool PlayCrashSparks { get; set; } = true;
		public bool AccelAxisIsBrake => accelAxisIsBrake;
		public bool BrakeIsReverse { get => brakeIsReverse; set => brakeIsReverse = value; }
		public float BurnoutSpin => burnoutSpin;
		public Wheel[] Wheels { get => wheels; set => wheels = value; }
		public HoverWheel[] HoverWheels { get => hoverWheels; set => hoverWheels = value; }
		public bool Hover => hover;
		public ForceMode SuspensionForceMode => suspensionForceMode;
		public ForceMode WheelForceMode => wheelForceMode;
		public AudioSource CrashSnd => crashSnd;
		public AudioClip[] CrashClips => crashClips;
		public ParticleSystem Sparks => sparks;
		public float CameraDistanceChange => cameraDistanceChange;
		public float CameraHeightChange => cameraHeightChange;

		void Start()
		{
			Tr = transform;
			Rb = GetComponent<Rigidbody>();

			// Create normal orientation object
			GameObject normTemp = new(Tr.name + "'s Normal Orientation");
			Norm = normTemp.transform;

			SetCenterOfMass();

			// Instantiate tow vehicle
			if (towVehicle)
			{
				newTow = Instantiate(towVehicle, Vector3.zero, Tr.rotation);
				newTow.SetActive(false);
				newTow.transform.position = Tr.TransformPoint(newTow.GetComponent<Joint>().connectedAnchor - newTow.GetComponent<Joint>().anchor);
				newTow.GetComponent<Joint>().connectedBody = Rb;
				newTow.SetActive(true);
				newTow.GetComponent<VehicleParent>().inputInherit = this;
			}

			if (sparks) sparks.transform.parent = null;

			if (wheelGroups.Length > 0) StartCoroutine(WheelCheckLoop());
		}

		void Update()
		{
			// Shift single frame pressing logic
			if (stopUpshift)
			{
				UpshiftPressed = false;
				stopUpshift = false;
			}

			if (stopDownShift)
			{
				DownshiftPressed = false;
				stopDownShift = false;
			}

			if (UpshiftPressed) stopUpshift = true;

			if (DownshiftPressed) stopDownShift = true;

			if (inputInherit) InheritInputOneShot();

			// Norm orientation visualizing
			// Debug.DrawRay(norm.position, norm.forward, Color.blue);
			// Debug.DrawRay(norm.position, norm.up, Color.green);
			// Debug.DrawRay(norm.position, norm.right, Color.red);
		}

		void FixedUpdate()
		{
			if (inputInherit) InheritInput();

			if (wheelLoopDone && wheelGroups.Length > 0)
			{
				wheelLoopDone = false;
				StartCoroutine(WheelCheckLoop());
			}

			GetGroundedWheels();

			if (GroundedWheels > 0) Crashing = false;

			localVelocity = Tr.InverseTransformDirection(Rb.linearVelocity - wheelContactsVelocity);
			LocalAngularVel = Tr.InverseTransformDirection(Rb.angularVelocity);
			VelMag = Rb.linearVelocity.magnitude;
			SqrVelMag = Rb.linearVelocity.sqrMagnitude;
			ForwardDir = Tr.forward;
			RightDir = Tr.right;
			UpDir = Tr.up;
			ForwardDot = Vector3.Dot(ForwardDir, GlobalControl.worldUpDir);
			RightDot = Vector3.Dot(RightDir, GlobalControl.worldUpDir);
			UpDot = Vector3.Dot(UpDir, GlobalControl.worldUpDir);
			Norm.transform.position = Tr.position;
			Norm.transform.rotation = Quaternion.LookRotation(GroundedWheels == 0 ? UpDir : WheelNormalAverage, ForwardDir);

			// Check if performing a burnout
			if (GroundedWheels > 0 && !hover && !accelAxisIsBrake && burnoutThreshold >= 0 && AccelInput > burnoutThreshold &&
			    BrakeInput > burnoutThreshold)
				Burnout = Mathf.Lerp(Burnout, (5 - Mathf.Min(5, Mathf.Abs(localVelocity.z))) / 5 * Mathf.Abs(AccelInput),
					Time.fixedDeltaTime * (1 - burnoutSmoothness) * 10);
			else if (Burnout > 0.01f)
				Burnout = Mathf.Lerp(Burnout, 0, Time.fixedDeltaTime * (1 - burnoutSmoothness) * 10);
			else
				Burnout = 0;

			if (engine) Burnout *= engine.health;

			// Check if reversing
			if (brakeIsReverse && BrakeInput > 0 && localVelocity.z < 1 && Burnout == 0)
				Reversing = true;
			else if (localVelocity.z >= 0 || Burnout > 0) Reversing = false;
		}

		void OnEnable()
		{
			PlayerControlsHandler.AccelerateAction += SetAccel;
			PlayerControlsHandler.BrakeAction += SetBrake;
			PlayerControlsHandler.SteerAction += SetSteer;
			PlayerControlsHandler.BoostAction += SetBoost;
			PlayerControlsHandler.UpShiftAction += SetUpshift;
			PlayerControlsHandler.DownShiftAction += SetDownshift;
			PlayerControlsHandler.EBrakeAction += SetEbrake;
			PlayerControlsHandler.PitchAction += SetPitch;
			PlayerControlsHandler.YawAction += SetYaw;
			PlayerControlsHandler.RollAction += SetRoll;
		}

		void OnDisable()
		{
			PlayerControlsHandler.AccelerateAction -= SetAccel;
			PlayerControlsHandler.BrakeAction -= SetBrake;
			PlayerControlsHandler.SteerAction -= SetSteer;
			PlayerControlsHandler.BoostAction -= SetBoost;
			PlayerControlsHandler.UpShiftAction -= SetUpshift;
			PlayerControlsHandler.DownShiftAction -= SetDownshift;
			PlayerControlsHandler.EBrakeAction -= SetEbrake;
			PlayerControlsHandler.PitchAction -= SetPitch;
			PlayerControlsHandler.YawAction -= SetYaw;
			PlayerControlsHandler.RollAction -= SetRoll;
		}

		void SetAccel(float f)
		{
			f = Mathf.Clamp(f, -1, 1);
			AccelInput = f;
		}

		// Set brake input
		public void SetBrake(float f)
		{
			BrakeInput = accelAxisIsBrake ? -Mathf.Clamp(AccelInput, -1, 0) : Mathf.Clamp(f, -1, 1);
		}

		// Set steer input
		public void SetSteer(float f)
		{
			SteerInput = Mathf.Clamp(f, -1, 1);
		}

		// Set ebrake input
		public void SetEbrake(float f)
		{
			if ((f > 0 || EbrakeInput > 0) && holdEbrakePark && VelMag < 1 && AccelInput == 0 && (BrakeInput == 0 || !brakeIsReverse))
				EbrakeInput = 1;
			else
				EbrakeInput = Mathf.Clamp01(f);
		}

		void SetBoost(bool b)
		{
			BoostButton = b;
		}

		// Set pitch rotate input
		public void SetPitch(float f)
		{
			PitchInput = Mathf.Clamp(f, -1, 1);
		}

		// Set yaw rotate input
		public void SetYaw(float f)
		{
			YawInput = Mathf.Clamp(f, -1, 1);
		}

		// Set roll rotate input
		public void SetRoll(float f)
		{
			RollInput = Mathf.Clamp(f, -1, 1);
		}

		// Do upshift input
		public void PressUpshift()
		{
			UpshiftPressed = true;
		}

		// Do downshift input
		public void PressDownshift()
		{
			DownshiftPressed = true;
		}

		// Set held upshift input
		public void SetUpshift(float f)
		{
			UpshiftHold = f;
		}

		// Set held downshift input
		public void SetDownshift(float f)
		{
			DownshiftHold = f;
		}

		// Copy input from other vehicle
		void InheritInput()
		{
			AccelInput = inputInherit.AccelInput;
			BrakeInput = inputInherit.BrakeInput;
			SteerInput = inputInherit.SteerInput;
			EbrakeInput = inputInherit.EbrakeInput;
			PitchInput = inputInherit.PitchInput;
			YawInput = inputInherit.YawInput;
			RollInput = inputInherit.RollInput;
		}

		// Copy single-frame input from other vehicle
		void InheritInputOneShot()
		{
			UpshiftPressed = inputInherit.UpshiftPressed;
			DownshiftPressed = inputInherit.DownshiftPressed;
		}

		// Change the center of mass of the vehicle
		void SetCenterOfMass()
		{
			float susAverage = 0;

			// Get average suspension height
			if (suspensionCenterOfMass)
			{
				if (hover)
					for (int i = 0; i < hoverWheels.Length; i++)
						susAverage = i == 0 ? hoverWheels[i].hoverDistance : (susAverage + hoverWheels[i].hoverDistance) * 0.5f;
				else
					for (int i = 0; i < wheels.Length; i++)
					{
						float newSusDist = wheels[i].transform.parent.GetComponent<Suspension>().suspensionDistance;
						susAverage = i == 0 ? newSusDist : (susAverage + newSusDist) * 0.5f;
					}
			}

			Rb.centerOfMass = centerOfMassOffset + new Vector3(0, -susAverage, 0);
			Rb.inertiaTensor = Rb.inertiaTensor; // This is required due to decoupling of inertia tensor from center of mass in Unity 5.3
		}

		// Get the number of grounded wheels and the normals and velocities of surfaces they're sitting on
		void GetGroundedWheels()
		{
			GroundedWheels = 0;
			wheelContactsVelocity = Vector3.zero;

			if (hover)
				for (int i = 0; i < hoverWheels.Length; i++)
				{
					if (hoverWheels[i].grounded)
						WheelNormalAverage = i == 0
							? hoverWheels[i].contactPoint.normal
							: (WheelNormalAverage + hoverWheels[i].contactPoint.normal).normalized;

					if (hoverWheels[i].grounded) GroundedWheels++;
				}
			else
				for (int i = 0; i < wheels.Length; i++)
				{
					if (wheels[i].Grounded)
					{
						wheelContactsVelocity = i == 0 ? wheels[i].ContactVelocity : (wheelContactsVelocity + wheels[i].ContactVelocity) * 0.5f;
						WheelNormalAverage = i == 0 ? wheels[i].ContactPoint.normal : (WheelNormalAverage + wheels[i].ContactPoint.normal).normalized;
					}

					if (wheels[i].Grounded) GroundedWheels++;
				}
		}

		// Check for crashes and play collision sounds
		void OnCollisionEnter(Collision col)
		{
			if (col.contacts.Length > 0 && GroundedWheels == 0)
				foreach (ContactPoint curCol in col.contacts)
					if (!curCol.thisCollider.CompareTag("Underside") && curCol.thisCollider.gameObject.layer != GlobalControl.ignoreWheelCastLayer)
						if (Vector3.Dot(curCol.normal, col.relativeVelocity.normalized) > 0.2f && col.relativeVelocity.sqrMagnitude > 20)
						{
							bool checkTow = true;
							if (newTow) checkTow = !curCol.otherCollider.transform.IsChildOf(newTow.transform);

							if (checkTow)
							{
								Crashing = canCrash;

								if (crashSnd && crashClips.Length > 0 && PlayCrashSounds)
									crashSnd.PlayOneShot(crashClips[Random.Range(0, crashClips.Length)],
										Mathf.Clamp01(col.relativeVelocity.magnitude * 0.1f));

								if (sparks && PlayCrashSparks)
								{
									sparks.transform.position = curCol.point;
									sparks.transform.rotation = Quaternion.LookRotation(col.relativeVelocity.normalized, curCol.normal);
									sparks.Play();
								}
							}
						}
		}

		// Continuous collision checking
		void OnCollisionStay(Collision col)
		{
			if (col.contacts.Length > 0 && GroundedWheels == 0)
				foreach (ContactPoint curCol in col.contacts)
					if (!curCol.thisCollider.CompareTag("Underside") && curCol.thisCollider.gameObject.layer != GlobalControl.ignoreWheelCastLayer)
						if (col.relativeVelocity.sqrMagnitude < 5)
						{
							bool checkTow = true;

							if (newTow) checkTow = !curCol.otherCollider.transform.IsChildOf(newTow.transform);

							if (checkTow) Crashing = canCrash;
						}
		}

		void OnDestroy()
		{
			if (Norm) Destroy(Norm.gameObject);

			if (sparks) Destroy(sparks.gameObject);
		}

		// Loop through all wheel groups to check for wheel contacts
		IEnumerator WheelCheckLoop()
		{
			for (int i = 0; i < wheelGroups.Length; i++)
			{
				wheelGroups[i].Activate();
				wheelGroups[i == 0 ? wheelGroups.Length - 1 : i - 1].Deactivate();
				yield return new WaitForFixedUpdate();
			}

			wheelLoopDone = true;
		}
	}

	// Class for groups of wheels to check each FixedUpdate
	[Serializable]
	public class WheelCheckGroup
	{
		public Wheel[] wheels;
		public HoverWheel[] hoverWheels;

		public void Activate()
		{
			foreach (Wheel curWheel in wheels) curWheel.GetContact = true;

			foreach (HoverWheel curHover in hoverWheels) curHover.getContact = true;
		}

		public void Deactivate()
		{
			foreach (Wheel curWheel in wheels) curWheel.GetContact = false;

			foreach (HoverWheel curHover in hoverWheels) curHover.getContact = false;
		}
	}
}
