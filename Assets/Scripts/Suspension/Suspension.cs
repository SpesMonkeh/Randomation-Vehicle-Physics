using System;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.Mathf;

namespace RVP
{
	[RequireComponent(typeof(DriveForce))]
	[ExecuteInEditMode]
	[DisallowMultipleComponent]
	[AddComponentMenu("RVP/Suspension/Suspension", 0)]

	// Class for the suspensions
	public class Suspension : MonoBehaviour
	{
		[NonSerialized]
		public Transform tr;
		Rigidbody rb;
		VehicleParent vp;

		// Variables for inverting certain values on opposite sides of the vehicle
		[NonSerialized]
		public bool flippedSide;
		[NonSerialized]
		public float flippedSideFactor;
		[NonSerialized]
		public Quaternion initialRotation;

		public Wheel wheel;
		CapsuleCollider compressCol; // The hard collider

		[Tooltip("Generate a capsule collider for hard compressions")]
		public bool generateHardCollider = true;

		[Tooltip("Multiplier for the radius of the hard collider")]
		public float hardColliderRadiusFactor = 1;
		float hardColliderRadiusFactorPrev;
		float setHardColliderRadiusFactor;
		Transform compressTr; // Transform component of the hard collider

		[Header("Brakes and Steering")]
		public float brakeForce;
		public float ebrakeForce;

		[Range(-180, 180)]
		public float steerRangeMin;
		[Range(-180, 180)]
		public float steerRangeMax;

		[Tooltip("How much the wheel is steered")]
		public float steerFactor = 1;
		[Range(-1, 1)]
		public float steerAngle;
		[NonSerialized]
		public float steerDegrees;

		[Tooltip("Effect of Ackermann steering geometry")]
		public float ackermannFactor;

		[Tooltip("The camber of the wheel as it travels, x-axis = compression, y-axis = angle")]
		public AnimationCurve camberCurve = AnimationCurve.Linear(0, 0, 1, 0);
		[Range(-89.999f, 89.999f)]
		public float camberOffset;
		[NonSerialized]
		public float camberAngle;

		[Tooltip("Adjust the camber as if it was connected to a solid axle, opposite wheel must be set")]
		public bool solidAxleCamber;
		public Suspension oppositeWheel;

		[Tooltip("Angle at which the suspension points out to the side")]
		[Range(-89.999f, 89.999f)]
		public float sideAngle;
		[Range(-89.999f, 89.999f)]
		public float casterAngle;
		[Range(-89.999f, 89.999f)]
		public float toeAngle;

		[Tooltip("Wheel offset from its pivot point")]
		public float pivotOffset;
		[NonSerialized]
		public List<SuspensionPart> movingParts = new();

		[Header("Spring")]
		public float suspensionDistance;
		[NonSerialized]
		public float compression;

		[Tooltip("Should be left at 1 unless testing suspension travel")]
		[Range(0, 1)]
		public float targetCompression;
		[NonSerialized]
		public float penetration; // How deep the ground is interesecting with the wheel's tire
		public float springForce;

		[Tooltip("Force of the curve depending on it's compression, x-axis = compression, y-axis = force")]
		public AnimationCurve springForceCurve = AnimationCurve.Linear(0, 0, 1, 1);

		[Tooltip("Exponent for spring force based on compression")]
		public float springExponent = 1;
		public float springDampening;

		[Tooltip("How quickly the suspension extends if it's not grounded")]
		public float extendSpeed = 20;

		[Tooltip("Apply forces to prevent the wheel from intersecting with the ground, not necessary if generating a hard collider")]
		public bool applyHardContactForce = true;
		public float hardContactForce = 50;
		public float hardContactSensitivity = 2;

		[Tooltip("Apply suspension forces at ground point")]
		public bool applyForceAtGroundContact = true;

		[Tooltip("Apply suspension forces along local up direction instead of ground normal")]
		public bool leaningForce;

		[NonSerialized]
		public Vector3 maxCompressPoint; // Position of the wheel when the suspension is compressed all the way
		[NonSerialized]
		public Vector3 springDirection;
		[NonSerialized]
		public Vector3 upDir; // Local up direction
		[NonSerialized]
		public Vector3 forwardDir; // Local forward direction

		[NonSerialized]
		public DriveForce targetDrive; // The drive being passed into the wheel

		[SerializeField] SuspensionPropertyToggle properties; // Originally [NonSerialized]
		[SerializeField] bool steerEnabled = true; // Originally [NonSerialized]
		[SerializeField] bool steerInverted; // Originally [NonSerialized]
		[SerializeField] bool driveEnabled = true; // Originally [NonSerialized]
		[SerializeField] bool driveInverted; // Originally [NonSerialized]
		[SerializeField] bool eBrakeEnabled = true; // Originally [NonSerialized]
		[SerializeField] bool skidSteerBrake; // Originally [NonSerialized]

		[Header("Damage")]
		[Tooltip("Point around which the suspension pivots when damaged")]
		[SerializeField] Vector3 damagePivot;

		[Tooltip("Compression amount to remain at when wheel is detached")]
		[SerializeField, Range(0, 1)] float detachedCompression = 0.5f;

		[SerializeField] float jamForce = Infinity;
		[SerializeField] bool jammed; // Originally [NonSerialized]

		internal Vector2 DamagePivot => damagePivot;
		internal float DetachedCompression => detachedCompression;
		internal float JamForce => jamForce;
		internal bool Jammed { get => jammed; set => jammed = value; }
		internal bool SteerEnabled => steerEnabled;
		internal bool SteerInverted => steerInverted;
		internal bool DriveEnabled => driveEnabled;
		internal bool DriveInverted => driveInverted;
		internal bool EBrakeEnabled => eBrakeEnabled;
		internal bool SkidSteerBrake => skidSteerBrake;
		internal float SteerFactor => steerFactor;
		internal float SteerAngle => steerAngle;
		internal float SteerDegrees => steerDegrees;
		internal float AckermannFactor => ackermannFactor;
		internal float CamberAngle => camberAngle;
		internal Wheel Wheel => wheel;

		void Start()
		{
			tr = transform;
			rb = tr.GetTopmostParentComponent<Rigidbody>();
			vp = tr.GetTopmostParentComponent<VehicleParent>();
			targetDrive = GetComponent<DriveForce>();
			flippedSide = Vector3.Dot(tr.forward, vp.transform.right) < 0;
			flippedSideFactor = flippedSide ? -1 : 1;
			initialRotation = tr.localRotation;

			if (Application.isPlaying)
			{
				GetCamber();

				// Generate the hard collider
				if (generateHardCollider)
				{
					GameObject cap = new("Compress Collider");
					cap.layer = GlobalControl.ignoreWheelCastLayer;
					compressTr = cap.transform;
					compressTr.parent = tr;
					compressTr.localPosition = Vector3.zero;
					compressTr.localEulerAngles = new Vector3(camberAngle, 0, -casterAngle * flippedSideFactor);
					compressCol = cap.AddComponent<CapsuleCollider>();
					compressCol.direction = 1;
					setHardColliderRadiusFactor = hardColliderRadiusFactor;
					hardColliderRadiusFactorPrev = setHardColliderRadiusFactor;
					compressCol.radius = wheel.RimWidth * hardColliderRadiusFactor;
					compressCol.height = (wheel.Popped ? wheel.RimRadius : Lerp(wheel.RimRadius, wheel.TireRadius, wheel.TirePressure)) * 2;
				}

				compressCol.sharedMaterial = GlobalControl.frictionlessMatStatic;
			}

			steerRangeMax = Max(steerRangeMin, steerRangeMax);

			properties = GetComponent<SuspensionPropertyToggle>();
			if (properties) UpdateProperties();
		}

		void FixedUpdate()
		{
			upDir = tr.up;
			forwardDir = tr.forward;
			targetCompression = 1;

			GetCamber();

			GetSpringVectors();

			if (wheel.Connected)
			{
				compression = Min(targetCompression, suspensionDistance > 0 ? Clamp01(wheel.ContactPoint.distance / suspensionDistance) : 0);
				penetration = Min(0, wheel.ContactPoint.distance);
			}
			else
			{
				compression = detachedCompression;
				penetration = 0;
			}

			if (targetCompression > 0) ApplySuspensionForce();

			// Set hard collider size if it is changed during play mode
			if (generateHardCollider)
			{
				setHardColliderRadiusFactor = hardColliderRadiusFactor;

				if (hardColliderRadiusFactorPrev != setHardColliderRadiusFactor || wheel.UpdatedSize || wheel.UpdatedPopped)
				{
					if (wheel.RimWidth > wheel.ActualRadius)
					{
						compressCol.direction = 2;
						compressCol.radius = wheel.ActualRadius * hardColliderRadiusFactor;
						compressCol.height = wheel.RimWidth * 2;
					}
					else
					{
						compressCol.direction = 1;
						compressCol.radius = wheel.RimWidth * hardColliderRadiusFactor;
						compressCol.height = wheel.ActualRadius * 2;
					}
				}

				hardColliderRadiusFactorPrev = setHardColliderRadiusFactor;
			}

			// Set the drive of the wheel
			if (wheel.Connected)
			{
				if (wheel.TargetDrive)
				{
					targetDrive.active = driveEnabled;
					targetDrive.feedbackRPM = wheel.TargetDrive.feedbackRPM;
					wheel.TargetDrive.SetDrive(targetDrive);
				}
			}
			else
			{
				targetDrive.feedbackRPM = targetDrive.rpm;
			}
		}

		void Update()
		{
			GetCamber();

			if (Application.isPlaying is false)
				GetSpringVectors();
			SetVehicleSteerAngle();
		}

		void SetVehicleSteerAngle()
		{
			float range = steerAngle > 0
				? steerRangeMax
				: steerRangeMin;
			steerDegrees = Abs(steerAngle) * range;
		}

		// Apply suspension forces to support vehicles
		void ApplySuspensionForce()
		{
			if (wheel.Grounded && wheel.Connected)
			{
				// Get velocity of ground to offset from local vertical velocity
				Rigidbody groundBody = wheel.ContactPoint.col.attachedRigidbody;
				Vector3 groundVel = Vector3.zero;
				if (groundBody) groundVel = groundBody.linearVelocity;

				// Get the local vertical velocity
				float travelVelocity = vp.Norm.InverseTransformDirection(rb.GetPointVelocity(tr.position) - groundVel).z;

				// Apply the suspension force
				if (suspensionDistance > 0 && targetCompression > 0)
				{
					Vector3 appliedSuspensionForce =
						(leaningForce
							? Vector3.Lerp(upDir, vp.Norm.forward, Abs(Pow(Vector3.Dot(vp.Norm.forward, vp.UpDir), 5)))
							: vp.Norm.forward) * (springForce * (Pow(springForceCurve.Evaluate(1 - compression), Max(1, springExponent)) -
							                                     (1 - targetCompression) - springDampening * Clamp(travelVelocity, -1, 1)));

					rb.AddForceAtPosition(
						appliedSuspensionForce,
						applyForceAtGroundContact ? wheel.ContactPoint.point : wheel.WheelTransform.position,
						vp.SuspensionForceMode);

					// If wheel is resting on a rigidbody, apply opposing force to it
					ApplyOpposingForceToRestingBody(groundBody, appliedSuspensionForce);
				}
				ApplyHardContactForce(travelVelocity);

			}
		}

		void ApplyOpposingForceToRestingBody(Rigidbody groundBody, Vector3 suspensionForce)
		{
			if (groundBody)
				groundBody.AddForceAtPosition(-suspensionForce, wheel.ContactPoint.point, vp.SuspensionForceMode);
		}

		void ApplyHardContactForce(float velocity)
		{
			if (Approximately(compression, 0) && generateHardCollider is false && applyHardContactForce)
			{
				float maxForce = -hardContactSensitivity * TimeMaster.fixedTimeFactor;
				float zForce = Clamp(velocity, maxForce, 0) + penetration;
				Vector3 force = -vp.Norm.TransformDirection(0, 0, zForce) * (hardContactForce * Clamp01(TimeMaster.fixedTimeFactor));
				Vector3 position = applyForceAtGroundContact
					? wheel.ContactPoint.point
					: wheel.WheelTransform.position;
				rb.AddForceAtPosition(force, position, vp.SuspensionForceMode);}
		}

		// Calculate the direction of the spring
		void GetSpringVectors()
		{
			if (!Application.isPlaying)
			{
				tr = transform;
				flippedSide = Vector3.Dot(tr.forward, vp.transform.right) < 0;
				flippedSideFactor = flippedSide ? -1 : 1;
			}

			maxCompressPoint = tr.position;

			float casterDir = -Sin(casterAngle * Deg2Rad) * flippedSideFactor;
			float sideDir = -Sin(sideAngle * Deg2Rad);

			springDirection = tr.TransformDirection(casterDir, Max(Abs(casterDir), Abs(sideDir)) - 1, sideDir).normalized;
		}

		/// Calculate the camber angle
		void GetCamber()
		{
			if (solidAxleCamber && oppositeWheel && wheel.Connected)
			{
				if (oppositeWheel.Wheel.RimTransform && wheel.RimTransform)
				{
					Vector3 axleDir = tr.InverseTransformDirection((oppositeWheel.Wheel.RimTransform.position - wheel.RimTransform.position).normalized);
					camberAngle = Atan2(axleDir.z, axleDir.y) * Rad2Deg + 90 + camberOffset;
				}
			}
			else
			{
				camberAngle = camberCurve.Evaluate(Application.isPlaying && wheel.Connected
					? wheel.TravelDist
					: targetCompression) + camberOffset;
			}
		}

		// Update the toggleable properties
		public void UpdateProperties()
		{
			if (properties)
				foreach (SuspensionToggledProperty curProperty in properties.Properties)
					switch ((int)curProperty.Property)
					{
						case 0:
							steerEnabled = curProperty.IsEnabled;
							break;
						case 1:
							steerInverted = curProperty.IsEnabled;
							break;
						case 2:
							driveEnabled = curProperty.IsEnabled;
							break;
						case 3:
							driveInverted = curProperty.IsEnabled;
							break;
						case 4:
							eBrakeEnabled = curProperty.IsEnabled;
							break;
						case 5:
							skidSteerBrake = curProperty.IsEnabled;
							break;
					}
		}

		// Visualize steer range
		void OnDrawGizmosSelected()
		{
			if (!tr) tr = transform;

			if (wheel)
				if (wheel.RimTransform)
				{
					Vector3 wheelPoint = wheel.RimTransform.position;

					float camberSin = -Sin(camberAngle * Deg2Rad);
					float steerSin = Sin(Lerp(steerRangeMin, steerRangeMax, (steerAngle + 1) * 0.5f) * Deg2Rad);
					float minSteerSin = Sin(steerRangeMin * Deg2Rad);
					float maxSteerSin = Sin(steerRangeMax * Deg2Rad);

					Gizmos.color = Color.magenta;

					Gizmos.DrawWireSphere(wheelPoint, 0.05f);

					Gizmos.DrawLine(wheelPoint, wheelPoint + tr.TransformDirection(minSteerSin,
						camberSin * (1 - Abs(minSteerSin)),
						Cos(steerRangeMin * Deg2Rad) * (1 - Abs(camberSin))
					).normalized);

					Gizmos.DrawLine(wheelPoint, wheelPoint + tr.TransformDirection(maxSteerSin,
						camberSin * (1 - Abs(maxSteerSin)),
						Cos(steerRangeMax * Deg2Rad) * (1 - Abs(camberSin))
					).normalized);

					Gizmos.DrawLine(wheelPoint + tr.TransformDirection(minSteerSin,
							camberSin * (1 - Abs(minSteerSin)),
							Cos(steerRangeMin * Deg2Rad) * (1 - Abs(camberSin))
						).normalized * 0.9f,
						wheelPoint + tr.TransformDirection(maxSteerSin,
							camberSin * (1 - Abs(maxSteerSin)),
							Cos(steerRangeMax * Deg2Rad) * (1 - Abs(camberSin))
						).normalized * 0.9f);

					Gizmos.DrawLine(wheelPoint, wheelPoint + tr.TransformDirection(steerSin,
						camberSin * (1 - Abs(steerSin)),
						Cos(steerRangeMin * Deg2Rad) * (1 - Abs(camberSin))
					).normalized);
				}

			Gizmos.color = Color.red;

			Gizmos.DrawWireSphere(tr.TransformPoint(damagePivot), 0.05f);
		}
	}
}
