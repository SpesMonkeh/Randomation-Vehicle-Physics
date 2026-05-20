using System;
using UnityEngine;
using static UnityEngine.Mathf;
using Random = UnityEngine.Random;

namespace RVP
{
	[RequireComponent(typeof(DriveForce)), ExecuteInEditMode, DisallowMultipleComponent, AddComponentMenu("RVP2WoL/Drivetrain/Wheel", 1)]
	public sealed class Wheel : MonoBehaviour
	{
		[SerializeField] Vector3 localVel;
		[SerializeField] Transform wheelTransform;
		[SerializeField] Rigidbody wheelBody;
		[SerializeField] VehicleParent vehicleParent;
		[SerializeField] Suspension suspensionParent;
		[SerializeField] Transform rimTransform;
		[SerializeField] Transform tireTransform;

		[Tooltip("Generate a sphere collider to represent the wheel for side collisions")]
		[SerializeField] bool generateHardCollider = true;
		[SerializeField] SphereCollider sphereCol; // Hard collider
		[SerializeField] Transform sphereColTr; // Hard collider transform

		[Header("Rotation")]
		[Tooltip("Bias for feedback RPM lerp between target RPM and raw RPM")]
		[SerializeField] [Range(0, 1)] float feedbackRpmBias;

		[Tooltip(
			"Curve for setting final RPM of wheel based on driving torque/brake force, x-axis = torque/brake force, y-axis = lerp between raw RPM and target RPM")]
		[SerializeField] AnimationCurve rpmBiasCurve = AnimationCurve.Linear(0, 0, 1, 1);

		[Tooltip("As the RPM of the wheel approaches this value, the RPM bias curve is interpolated with the default linear curve")]
		[SerializeField] float rpmBiasCurveLimit = Infinity;
		[SerializeField] [Range(0, 10)] float axleFriction;

		[Header("Friction")]
		[SerializeField] [Range(0, 1)] float frictionSmoothness = 0.5f;
		[SerializeField] float forwardFriction = 1;
		[SerializeField] float sidewaysFriction = 1;
		[SerializeField] float forwardRimFriction = 0.5f;
		[SerializeField] float sidewaysRimFriction = 0.5f;
		[SerializeField] float forwardCurveStretch = 1;
		[SerializeField] float sidewaysCurveStretch = 1;
		[SerializeField] Vector3 frictionForce = Vector3.zero;

		[Tooltip("X-axis = slip, y-axis = friction")]
		[SerializeField] AnimationCurve forwardFrictionCurve = AnimationCurve.Linear(0, 0, 1, 1);
		[Tooltip("X-axis = slip, y-axis = friction")]
		[SerializeField] AnimationCurve sidewaysFrictionCurve = AnimationCurve.Linear(0, 0, 1, 1);
		[SerializeField] float forwardSlip;
		[SerializeField] float sidewaysSlip;


		internal float ForwardSlip => forwardSlip;
		internal float SidewaysSlip => sidewaysSlip;

		public enum SlipDependenceMode
		{
			Dependent,
			Forward,
			Sideways,
			Independent
		}

		[SerializeField] SlipDependenceMode slipDependence = SlipDependenceMode.Sideways;
		[SerializeField] [Range(0, 2)] float forwardSlipDependence = 2;
		[SerializeField] [Range(0, 2)] float sidewaysSlipDependence = 2;

		[Tooltip(
			"Adjusts how much friction the wheel has based on the normal of the ground surface. X-axis = normal dot product, y-axis = friction multiplier")]
		[SerializeField] AnimationCurve normalFrictionCurve = AnimationCurve.Linear(0, 1, 1, 1);

		[Tooltip("How much the suspension compression affects the wheel friction")]
		[SerializeField] [Range(0, 1)] float compressionFrictionFactor = 0.5f;

		[SerializeField] float rimRadius;
		[SerializeField] float rimWidth;
		[SerializeField] float tireWidth;
		[SerializeField] float setTireWidth;
		[SerializeField] float tireWidthPrev;
		[SerializeField] float setTireRadius;
		[SerializeField] float tireRadiusPrev;
		[SerializeField] float setRimWidth;
		[SerializeField] float rimWidthPrev;
		[SerializeField] float setRimRadius;
		[SerializeField] float rimRadiusPrev;
		[SerializeField] float actualRadius;

		[SerializeField] float setTirePressure;
		[SerializeField] float tirePressurePrev;
		[SerializeField] float initialTirePressure;
		[SerializeField] bool popped;

		[SerializeField] bool setPopped;
		[SerializeField] bool poppedPrev;
		[SerializeField] bool canPop;

		[Tooltip("Requires deform shader")]
		[SerializeField] float deformAmount;
		[SerializeField] Material rimMat;
		[SerializeField] Material tireMat;
		[SerializeField] float airLeakTime = -1;

		[Range(0, 1)] [SerializeField] float rimGlow;
		[SerializeField] float glowAmount;
		[SerializeField] Color glowColor;
		[field: SerializeField] public bool UpdatedSize { get; private set; }
		[field: SerializeField] public bool UpdatedPopped { get; private set; }
		[SerializeField] float currentRPM;
		[SerializeField] DriveForce targetDrive;
		[SerializeField] float rawRPM;
		[SerializeField] float airTime;
		[SerializeField] Vector3 upDir; // Up direction
		[SerializeField] float circumference;
		[SerializeField] float actualEbrake;
		[SerializeField] float actualTargetRPM;
		[SerializeField] float actualTorque;
		[SerializeField] Vector3 contactVelocity;

		[SerializeField] Vector3 forceApplicationPoint; // Point at which friction forces are applied

		[Tooltip("Apply friction forces at ground point")] [SerializeField] bool applyForceAtGroundContact;

		[Header("Audio")]
		[SerializeField] AudioSource impactSnd;
		[SerializeField] AudioClip[] tireHitClips;
		[SerializeField] AudioClip rimHitClip;
		[SerializeField] AudioClip tireAirClip;
		[SerializeField] AudioClip tirePopClip;

		[SerializeField] float damage;
		[SerializeField] float mass = 0.05f;
		[SerializeField] bool canDetach;
		[SerializeField] bool connected = true;

		[SerializeField] Mesh tireMeshLoose; // Tire mesh for detached wheel collider
		[SerializeField] Mesh rimMeshLoose; // Rim mesh for detached wheel collider
		[SerializeField] GameObject detachedWheel;
		[SerializeField] GameObject detachedTire;
		[SerializeField] MeshCollider detachedCol;
		[SerializeField] Rigidbody detachedBody;
		[SerializeField] MeshFilter detachFilter;
		[SerializeField] MeshFilter detachTireFilter;
		[SerializeField] PhysicsMaterial detachedTireMaterial;
		[SerializeField] PhysicsMaterial detachedRimMaterial;

		[SerializeField] float travelDist;
		[SerializeField] bool grounded;
		[SerializeField] bool getContact = true;
		[SerializeField] readonly WheelContact contactPoint = new();

		[Header("Tire")]
		[SerializeField] [Range(0, 1)] float tirePressure = 1;
		[Header("Damage")]
		[SerializeField] float detachForce = Infinity;
		[Header("Size")]
		[SerializeField] float tireRadius;

		internal bool GetContact { get => getContact; set => getContact = value; }
		internal bool Grounded { get => grounded; private set => grounded = value; }
		internal bool SetPopped => setPopped;
		internal bool PoppedPrev => poppedPrev;
		internal bool CanPop => canPop;
		internal bool Popped { get => popped; private set => popped = value; }
		internal float ActualRadius { get => actualRadius; private set => actualRadius = value; }
		internal float TirePressure { get => tirePressure; private set => tirePressure = value; }
		public float DetachForce => detachForce;

		public bool Connected => connected;
		internal bool GenerateHardCollider => generateHardCollider;
		internal float Damage => damage;
		internal float FeedbackRpmBias => feedbackRpmBias;
		internal float RPMBiasCurveLimit => rpmBiasCurveLimit;
		internal float AxleFriction => axleFriction;
		internal float SidewaysFriction => sidewaysFriction;
		internal float ForwardFriction => forwardFriction;
		internal float ForwardRimFriction => forwardRimFriction;
		internal float FrictionSmoothness => frictionSmoothness;
		internal float TireRadius { get => tireRadius; private set => tireRadius = value; }
		internal float RimRadius { get => rimRadius; private set => rimRadius = value; }
		internal float TireWidth { get => tireWidth; private set => tireWidth = value;}
		internal float RimWidth { get => rimWidth; private set => rimWidth = value; }

		internal Vector3 LocalVel => localVel;
		internal Vector3 ContactVelocity { get => contactVelocity; private set => contactVelocity = value; }
		internal Rigidbody WheelBody => wheelBody;
		internal VehicleParent VehicleParent => vehicleParent;
		internal Transform TireTransform => tireTransform;
		internal SphereCollider SphereCol => sphereCol;
		internal Transform SphereColTr => sphereColTr;
		internal AnimationCurve RPMBiasCurve => rpmBiasCurve;
		internal float SidewaysRimFriction => sidewaysRimFriction;
		internal float ForwardCurveStretch => forwardCurveStretch;
		internal float SidewaysCurveStretch => sidewaysCurveStretch;
		internal Vector3 FrictionForce => frictionForce;
		internal AnimationCurve ForwardFrictionCurve => forwardFrictionCurve;
		internal AnimationCurve SidewaysFrictionCurve => sidewaysFrictionCurve;
		internal SlipDependenceMode SlipDependence => slipDependence;
		internal float ForwardSlipDependence => forwardSlipDependence;
		internal float SidewaysSlipDependence => sidewaysSlipDependence;
		internal AnimationCurve NormalFrictionCurve => normalFrictionCurve;
		internal float CompressionFrictionFactor => compressionFrictionFactor;
		internal float SetTireWidth => setTireWidth;
		internal float TireWidthPrev => tireWidthPrev;
		internal float SetTireRadius => setTireRadius;
		internal float TireRadiusPrev => tireRadiusPrev;
		internal float SetRimWidth => setRimWidth;
		internal float RimWidthPrev => rimWidthPrev;
		internal float SetRimRadius => setRimRadius;
		internal float RimRadiusPrev => rimRadiusPrev;
		internal float SetTirePressure => setTirePressure;
		internal float TirePressurePrev => tirePressurePrev;
		internal float InitialTirePressure => initialTirePressure;
		internal float DeformAmount => deformAmount;
		internal Material RimMat => rimMat;
		internal Material TireMat => tireMat;
		internal float AirLeakTime => airLeakTime;
		internal float RimGlow => rimGlow;
		internal float GlowAmount => glowAmount;
		internal Color GlowColor => glowColor;
		internal float CurrentRPM => currentRPM;
		internal float AirTime => airTime;
		internal Vector3 UpDir => upDir;
		internal float Circumference => circumference;
		internal float ActualEbrake => actualEbrake;
		internal float ActualTargetRPM => actualTargetRPM;
		internal float ActualTorque => actualTorque;
		internal Vector3 ForceApplicationPoint => forceApplicationPoint;
		internal bool ApplyForceAtGroundContact => applyForceAtGroundContact;
		internal AudioSource ImpactSnd => impactSnd;
		internal AudioClip[] TireHitClips => tireHitClips;
		internal AudioClip RimHitClip => rimHitClip;
		internal AudioClip TireAirClip => tireAirClip;
		internal AudioClip TirePopClip => tirePopClip;
		internal float Mass => mass;
		internal bool CanDetach => canDetach;
		internal Mesh TireMeshLoose => tireMeshLoose;
		internal Mesh RimMeshLoose => rimMeshLoose;
		internal GameObject DetachedWheel => detachedWheel;
		internal GameObject DetachedTire => detachedTire;
		internal MeshCollider DetachedCol => detachedCol;
		internal Rigidbody DetachedBody => detachedBody;
		internal MeshFilter DetachFilter => detachFilter;
		internal MeshFilter DetachTireFilter => detachTireFilter;
		internal PhysicsMaterial DetachedTireMaterial => detachedTireMaterial;
		internal PhysicsMaterial DetachedRimMaterial => detachedRimMaterial;
		internal Transform WheelTransform => wheelTransform;
		internal float TravelDist { get => travelDist; private set => travelDist = value; }
		internal float RawRPM { get => rawRPM; private set => rawRPM = value;}
		internal Transform RimTransform => rimTransform;
		internal Suspension SuspensionParent => suspensionParent;
		internal DriveForce TargetDrive { get => targetDrive; private set => targetDrive = value; }
		internal WheelContact ContactPoint => contactPoint;

		void SetupGlowMaterial()
		{
			if (rimGlow <= 0 || Application.isPlaying is false)
				return;

			rimMat = new Material(rimTransform.GetComponent<MeshRenderer>().sharedMaterial);
			rimMat.EnableKeyword("_EMISSION");
			rimTransform.GetComponent<MeshRenderer>().sharedMaterial = rimMat;
		}

		void Start()
		{
			wheelTransform = transform;
			wheelBody = wheelTransform.GetTopmostParentComponent<Rigidbody>();
			vehicleParent = wheelTransform.GetTopmostParentComponent<VehicleParent>();
			suspensionParent = wheelTransform.parent.GetComponent<Suspension>();
			travelDist = suspensionParent.targetCompression;
			canDetach = detachForce < Infinity && Application.isPlaying;
			initialTirePressure = TirePressure;

			if (wheelTransform.childCount > 0)
			{
				rimTransform = wheelTransform.GetChild(0);

				SetupGlowMaterial();
				CreateDetachedWheel();
				InitializeTireComponents();
				GenerateCollider();
			}

			targetDrive = GetComponent<DriveForce>();
			currentRPM = 0;
		}

		void GenerateCollider()
		{
			if (Application.isPlaying)
			{
				// Generate hard collider
				if (generateHardCollider)
				{
					GameObject sphereColNew = new("Rim Collider")
					{
						layer = GlobalControl.ignoreWheelCastLayer
					};
					sphereColTr = sphereColNew.transform;
					sphereCol = sphereColNew.AddComponent<SphereCollider>();
					sphereColTr.parent = wheelTransform;
					sphereColTr.localPosition = Vector3.zero;
					sphereColTr.localRotation = Quaternion.identity;
					sphereCol.radius = Min(RimWidth * 0.5f, RimRadius * 0.5f);
					sphereCol.sharedMaterial = GlobalControl.frictionlessMatStatic;
				}

				if (canDetach)
					detachedWheel.SetActive(false);
			}
		}

		void InitializeTireComponents()
		{
			if (rimTransform.childCount > 0)
			{
				tireTransform = rimTransform.GetChild(0);
				if (deformAmount > 0 && Application.isPlaying)
				{
					tireMat = new Material(tireTransform.GetComponent<MeshRenderer>().sharedMaterial);
					tireTransform.GetComponent<MeshRenderer>().sharedMaterial = tireMat;
				}
				CreateDetachedTire();
			}
		}

		void CreateDetachedTire()
		{
			if (canDetach)
			{
				detachedTire = new GameObject("Detached Tire");
				Transform tireTf = detachedTire.transform;
				tireTf.parent = tireTf;
				tireTf.localPosition = Vector3.zero;
				tireTf.localRotation = Quaternion.identity;
				detachTireFilter = detachedTire.AddComponent<MeshFilter>();
				detachTireFilter.sharedMesh = tireTransform.GetComponent<MeshFilter>().sharedMesh;
				MeshRenderer detachTireRend = detachedTire.AddComponent<MeshRenderer>();
				detachTireRend.sharedMaterial = tireMat ? tireMat : tireTransform.GetComponent<MeshRenderer>().sharedMaterial;
			}
		}

		void CreateDetachedWheel()
		{
			if (canDetach)
			{
				detachedWheel = new GameObject(vehicleParent.transform.name + " Detached Wheel")
				{
					layer = LayerMask.NameToLayer("Detachable Part")
				};
				detachFilter = detachedWheel.AddComponent<MeshFilter>();
				detachFilter.sharedMesh = rimTransform.GetComponent<MeshFilter>().sharedMesh;
				MeshRenderer detachRend = detachedWheel.AddComponent<MeshRenderer>();
				detachRend.sharedMaterial = rimTransform.GetComponent<MeshRenderer>().sharedMaterial;
				detachedCol = detachedWheel.AddComponent<MeshCollider>();
				detachedCol.convex = true;
				detachedBody = detachedWheel.AddComponent<Rigidbody>();
				detachedBody.mass = mass;
			}
		}

		void FixedUpdate()
		{
			upDir = wheelTransform.up;
			ActualRadius = popped ? RimRadius : Lerp(RimRadius, TireRadius, TirePressure);
			circumference = PI * ActualRadius * 2;
			localVel = wheelBody.GetPointVelocity(forceApplicationPoint);

			// Get proper inputs
			actualEbrake = suspensionParent.EBrakeEnabled ? suspensionParent.ebrakeForce : 0;
			actualTargetRPM = TargetDrive.rpm * (suspensionParent.DriveInverted ? -1 : 1);
			actualTorque = suspensionParent.DriveEnabled ? Lerp(TargetDrive.torque, Abs(vehicleParent.AccelInput), vehicleParent.Burnout) : 0;

			if (GetContact)
				GetWheelContact();
			else if (Grounded) ContactPoint.point += localVel * Time.fixedDeltaTime;

			airTime = Grounded ? 0 : airTime + Time.fixedDeltaTime;
			forceApplicationPoint = applyForceAtGroundContact ? ContactPoint.point : wheelTransform.position;

			if (connected)
			{
				GetRawRPM();
				ApplyDrive();
			}
			else
			{
				RawRPM = 0;
				currentRPM = 0;
				TargetDrive.feedbackRPM = 0;
			}
			CalculateTravelDistance();
			PositionWheel();

			if (connected)
			{
				UpdateWheelSize();

				GetSlip();
				ApplyFriction();

				// Burnout spinning
				if (vehicleParent.Burnout > 0 && TargetDrive.rpm != 0 && actualEbrake * vehicleParent.EbrakeInput == 0 && connected && Grounded)
					wheelBody.AddForceAtPosition(
						suspensionParent.forwardDir
						* (-suspensionParent.flippedSideFactor
						   * (vehicleParent.SteerInput
						      * vehicleParent.BurnoutSpin
						      * currentRPM
						      * Min(0.1f, TargetDrive.torque)
						      * 0.001f)
						   * vehicleParent.Burnout
						   * (popped
							   ? 0.5f
							   : 1)
						   * ContactPoint.surfaceFriction),
						suspensionParent.tr.position,
						vehicleParent.WheelForceMode);

				// Popping logic
				setPopped = popped;

				if (poppedPrev != setPopped)
				{
					if (tireTransform) tireTransform.gameObject.SetActive(!popped);

					UpdatedPopped = true;
				}
				else
				{
					UpdatedPopped = false;
				}

				poppedPrev = setPopped;

				// Air leak logic
				if (airLeakTime >= 0)
				{
					TirePressure = Clamp01(TirePressure - Time.fixedDeltaTime * 0.5f);

					if (Grounded)
					{
						airLeakTime += Max(Abs(currentRPM) * 0.001f, localVel.magnitude * 0.1f) * Time.timeScale *
						               TimeMaster.inverseFixedTimeFactor;

						if (airLeakTime > 1000 && TirePressure == 0)
						{
							popped = true;
							airLeakTime = -1;

							if (impactSnd && tirePopClip)
							{
								impactSnd.PlayOneShot(tirePopClip);
								impactSnd.pitch = 1;
							}
						}
					}
				}
			}
		}

		void UpdateWheelSize()
		{
			if (generateHardCollider is false)
				return;

			// Update hard collider size upon changed radius or width
			setRimWidth = RimWidth;
			setRimRadius = RimRadius;
			setTireWidth = TireWidth;
			setTireRadius = TireRadius;
			setTirePressure = TirePressure;

			if (!Mathf.Approximately(rimWidthPrev, setRimWidth) || !Approximately(rimRadiusPrev, setRimRadius))
			{
				sphereCol.radius = Min(RimWidth * 0.5f, RimRadius * 0.5f);
				UpdatedSize = true;
			}
			else if (!Mathf.Approximately(tireWidthPrev, setTireWidth) || !Approximately(tireRadiusPrev, setTireRadius) || !Mathf.Approximately(tirePressurePrev, setTirePressure))
			{
				UpdatedSize = true;
			}
			else
			{
				UpdatedSize = false;
			}

			rimWidthPrev = setRimWidth;
			rimRadiusPrev = setRimRadius;
			tireWidthPrev = setTireWidth;
			tireRadiusPrev = setTireRadius;
			tirePressurePrev = setTirePressure;
		}

		void CalculateTravelDistance()
		{
			TravelDist = suspensionParent.compression < TravelDist || Grounded
				? suspensionParent.compression
				: Lerp(TravelDist, suspensionParent.compression, suspensionParent.extendSpeed * Time.fixedDeltaTime);
		}

		void Update()
		{
			RotateVisualWheel();

			if (!Application.isPlaying)
			{
				PositionWheel();
			}
			else
			{
				// Update tire and rim materials
				if (deformAmount > 0 && tireMat && connected)
					if (tireMat.HasProperty("_DeformNormal"))
					{
						// Deform tire (requires deform shader)
						Vector3 deformNormal = Grounded
							? ContactPoint.normal *
							  (Max(-suspensionParent.penetration * (1 - suspensionParent.compression) * 10, 1 - TirePressure) * deformAmount)
							: Vector3.zero;
						tireMat.SetVector("_DeformNormal", new Vector4(deformNormal.x, deformNormal.y, deformNormal.z, 0));
					}

				if (rimMat)
					if (rimMat.HasProperty("_EmissionColor"))
					{
						// Make the rim glow
						float targetGlow = connected && GroundSurfaceMaster.surfaceTypesStatic[ContactPoint.surfaceType].leaveSparks
							? Abs(F.MaxAbs(forwardSlip, sidewaysSlip))
							: 0;
						glowAmount = popped ? Lerp(glowAmount, targetGlow, (targetGlow > glowAmount ? 2 : 0.2f) * Time.deltaTime) : 0;
						glowColor = new Color(glowAmount, glowAmount * 0.5f, 0);
						rimMat.SetColor("_EmissionColor", popped ? Color.Lerp(Color.black, glowColor, glowAmount * rimGlow) : Color.black);
					}
			}
		}

		// Use raycasting to find the current contact point for the wheel
		void GetWheelContact()
		{
			float castDist = Max(suspensionParent.suspensionDistance * Max(0.001f, suspensionParent.targetCompression) + ActualRadius,
				0.001f);
			RaycastHit[] wheelHits = Physics.RaycastAll(suspensionParent.maxCompressPoint, suspensionParent.springDirection, castDist,
				GlobalControl.wheelCastMaskStatic);
			int hitIndex = 0;
			bool validHit = false;
			float hitDist = Infinity;

			if (connected)
			{
				// Loop through raycast hits to find closest one
				for (int i = 0; i < wheelHits.Length; i++)
					if (!wheelHits[i].transform.IsChildOf(vehicleParent.Tr) && wheelHits[i].distance < hitDist)
					{
						hitIndex = i;
						hitDist = wheelHits[i].distance;
						validHit = true;
					}
			}
			else
			{
				validHit = false;
			}

			// Set contact point variables
			if (validHit)
			{
				RaycastHit hit = wheelHits[hitIndex];

				if (!grounded && impactSnd && ((tireHitClips.Length > 0 && !popped) || (rimHitClip && popped)))
				{
					impactSnd.PlayOneShot(popped ? rimHitClip : tireHitClips[RoundToInt(Random.Range(0, tireHitClips.Length - 1))],
						Clamp01(airTime * airTime));
					impactSnd.pitch = Clamp(airTime * 0.2f + 0.8f, 0.8f, 1);
				}

				grounded = true;
				contactPoint.distance = hit.distance - ActualRadius;
				contactPoint.point = hit.point + localVel * Time.fixedDeltaTime;
				contactPoint.grounded = true;
				contactPoint.normal = hit.normal;
				contactPoint.relativeVelocity = wheelTransform.InverseTransformDirection(localVel);
				contactPoint.col = hit.collider;

				if (hit.collider.attachedRigidbody)
				{
					contactVelocity = hit.collider.attachedRigidbody.GetPointVelocity(contactPoint.point);
					contactPoint.relativeVelocity -= wheelTransform.InverseTransformDirection(ContactVelocity);
				}
				else
				{
					ContactVelocity = Vector3.zero;
				}

				if (hit.collider.TryGetComponent(out GroundSurfaceInstance curSurface))
				{
					contactPoint.surfaceFriction = curSurface.friction;
					contactPoint.surfaceType = curSurface.surfaceType;
				}
				else if (hit.collider.TryGetComponent(out TerrainSurface curTerrain))
				{
					contactPoint.surfaceType = curTerrain.GetDominantSurfaceTypeAtPoint(ContactPoint.point);
					contactPoint.surfaceFriction = curTerrain.GetFriction(ContactPoint.surfaceType);
				}
				else
				{
					PhysicsMaterial sharedMat = hit.collider.sharedMaterial;
					if (sharedMat)
						contactPoint.surfaceFriction = sharedMat
							? sharedMat.dynamicFriction * 2
							: 1.0f;

					contactPoint.surfaceType = 0;
				}

				if (contactPoint.col.CompareTag("Pop Tire") && canPop && Approximately(airLeakTime, -1) && !popped) Deflate();
			}
			else
			{
				grounded = false;
				contactPoint.distance = suspensionParent.suspensionDistance;
				contactPoint.point = Vector3.zero;
				contactPoint.grounded = false;
				contactPoint.normal = upDir;
				contactPoint.relativeVelocity = Vector3.zero;
				contactPoint.col = null;
				contactVelocity = Vector3.zero;
				contactPoint.surfaceFriction = 0;
				contactPoint.surfaceType = 0;
			}
		}

		// Calculate what the RPM of the wheel would be based purely on its velocity
		void GetRawRPM()
		{
			if (grounded)
				RawRPM = ContactPoint.relativeVelocity.x / circumference * (PI * 100) * -suspensionParent.flippedSideFactor;
			else
				RawRPM = Lerp(RawRPM, actualTargetRPM,
					(actualTorque + suspensionParent.brakeForce * vehicleParent.BrakeInput + actualEbrake * vehicleParent.EbrakeInput) * Time.timeScale);
		}

		// Calculate the current slip amount
		void GetSlip()
		{
			if (grounded)
			{
				sidewaysSlip = contactPoint.relativeVelocity.z * 0.1f / sidewaysCurveStretch;
				forwardSlip = 0.01f * (RawRPM - currentRPM) / forwardCurveStretch;
			}
			else
			{
				sidewaysSlip = 0;
				forwardSlip = 0;
			}
		}

		// Apply actual forces to rigidbody based on wheel simulation
		void ApplyFriction()
		{
			if (grounded)
			{
				float forwardSlipFactor = (int)slipDependence == 0 || (int)slipDependence == 1 ? forwardSlip - sidewaysSlip : forwardSlip;
				float sidewaysSlipFactor = (int)slipDependence == 0 || (int)slipDependence == 2 ? sidewaysSlip - forwardSlip : sidewaysSlip;
				float forwardSlipDependenceFactor = Clamp01(forwardSlipDependence - Clamp01(Abs(sidewaysSlip)));
				float sidewaysSlipDependenceFactor = Clamp01(sidewaysSlipDependence - Clamp01(Abs(forwardSlip)));

				float targetForceX = forwardFrictionCurve.Evaluate(Abs(forwardSlipFactor)) * -Math.Sign(forwardSlip) *
				                     (popped ? forwardRimFriction : forwardFriction) * forwardSlipDependenceFactor *
				                     -suspensionParent.flippedSideFactor;
				float targetForceZ = sidewaysFrictionCurve.Evaluate(Abs(sidewaysSlipFactor)) * -Math.Sign(sidewaysSlip) *
				                     (popped ? sidewaysRimFriction : sidewaysFriction) * sidewaysSlipDependenceFactor *
				                     normalFrictionCurve.Evaluate(Clamp01(Vector3.Dot(contactPoint.normal, GlobalControl.worldUpDir))) *
				                     (vehicleParent.Burnout > 0 && Abs(TargetDrive.rpm) != 0 && actualEbrake * vehicleParent.EbrakeInput == 0 && Grounded
					                     ? (1 - vehicleParent.Burnout) * (1 - Abs(vehicleParent.AccelInput))
					                     : 1);

				Vector3 targetForce = wheelTransform.TransformDirection(targetForceX, 0, targetForceZ);
				float targetForceMultiplier =
					(1 - compressionFrictionFactor + (1 - suspensionParent.compression) * compressionFrictionFactor *
						Clamp01(Abs(suspensionParent.tr.InverseTransformDirection(localVel).z) * 10)) * contactPoint.surfaceFriction;
				frictionForce = Vector3.Lerp(frictionForce, targetForce * targetForceMultiplier, 1 - frictionSmoothness);
				wheelBody.AddForceAtPosition(frictionForce, forceApplicationPoint, vehicleParent.WheelForceMode);

				// If resting on a rigidbody, apply opposing force to it
				if (contactPoint.col.attachedRigidbody)
					contactPoint.col.attachedRigidbody.AddForceAtPosition(-frictionForce, contactPoint.point, vehicleParent.WheelForceMode);
			}
		}

		// Do torque and RPM calculations/simulation
		void ApplyDrive()
		{
			float brakeForce = 0;
			float brakeCheckValue = suspensionParent.SkidSteerBrake ? vehicleParent.LocalAngularVel.y : vehicleParent.LocalVelocity.z;

			// Set brake force
			if (vehicleParent.BrakeIsReverse)
			{
				if (brakeCheckValue > 0)
					brakeForce = suspensionParent.brakeForce * vehicleParent.BrakeInput;
				else if (brakeCheckValue <= 0) brakeForce = suspensionParent.brakeForce * Clamp01(vehicleParent.AccelInput);
			}
			else
			{
				brakeForce = suspensionParent.brakeForce * vehicleParent.BrakeInput;
			}

			brakeForce += axleFriction * 0.1f * (Approximately(actualTorque, 0) ? 1 : 0);

			if (TargetDrive.rpm != 0) brakeForce *= 1 - vehicleParent.Burnout;

			// Set final RPM
			if (!suspensionParent.Jammed && connected)
			{
				bool validTorque =
					(!(Approximately(actualTorque, 0) && Abs(actualTargetRPM) < 0.01f) && !Approximately(actualTargetRPM, 0)) ||
					brakeForce + actualEbrake * vehicleParent.EbrakeInput > 0;

				currentRPM = Lerp(RawRPM,
					Lerp(Lerp(RawRPM, actualTargetRPM, validTorque ? EvaluateTorque(actualTorque) : actualTorque), 0,
						Max(brakeForce, actualEbrake * vehicleParent.EbrakeInput)),
					validTorque
						? EvaluateTorque(actualTorque + brakeForce + actualEbrake * vehicleParent.EbrakeInput)
						: actualTorque + brakeForce + actualEbrake * vehicleParent.EbrakeInput);

				TargetDrive.feedbackRPM = Lerp(currentRPM, RawRPM, feedbackRpmBias);
			}
			else
			{
				currentRPM = 0;
				TargetDrive.feedbackRPM = 0;
			}
		}

		// Extra method for evaluating torque to make the ApplyDrive method more readable
		float EvaluateTorque(float t)
		{
			float torque = Lerp(rpmBiasCurve.Evaluate(t), t, RawRPM / (rpmBiasCurveLimit * Sign(actualTargetRPM)));
			return torque;
		}

		// Visual wheel positioning
		void PositionWheel()
		{
			if (suspensionParent)
				rimTransform.position = suspensionParent.maxCompressPoint +
					suspensionParent.springDirection * (suspensionParent.suspensionDistance *
					                                    (Application.isPlaying ? TravelDist : suspensionParent.targetCompression)) +
					suspensionParent.upDir * (Pow(
						Max(Abs(Sin(suspensionParent.sideAngle * Deg2Rad)),
							Abs(Sin(suspensionParent.casterAngle * Deg2Rad))), 2) * ActualRadius) +
					suspensionParent.pivotOffset * suspensionParent.tr.TransformDirection(Sin(wheelTransform.localEulerAngles.y * Deg2Rad), 0,
						Cos(wheelTransform.localEulerAngles.y * Deg2Rad)) - suspensionParent.pivotOffset *
					(Application.isPlaying ? suspensionParent.forwardDir : suspensionParent.tr.forward);

			if (Application.isPlaying && generateHardCollider && connected) sphereColTr.position = rimTransform.position;
		}

		void RotateVisualWheel()
		{
			if (wheelTransform && suspensionParent)
			{
				float ackermannVal = Approximately(Sign(suspensionParent.steerAngle), suspensionParent.flippedSideFactor)
					? 1 + suspensionParent.ackermannFactor
					: 1 - suspensionParent.ackermannFactor;
				wheelTransform.localEulerAngles = new Vector3(
					suspensionParent.camberAngle + suspensionParent.casterAngle * suspensionParent.steerAngle * suspensionParent.flippedSideFactor,
					-suspensionParent.toeAngle * suspensionParent.flippedSideFactor + suspensionParent.steerDegrees * ackermannVal, 0);
			}

			if (Application.isPlaying)
			{
				rimTransform.Rotate(Vector3.forward, currentRPM * suspensionParent.flippedSideFactor * Time.deltaTime);

				if (Damage > 0)
					rimTransform.localEulerAngles = new Vector3(Sin(-rimTransform.localEulerAngles.z * Deg2Rad) * Clamp(Damage, 0, 10),
						Cos(-rimTransform.localEulerAngles.z * Deg2Rad) * Clamp(Damage, 0, 10), rimTransform.localEulerAngles.z);
				else if (rimTransform.localEulerAngles.x != 0 || rimTransform.localEulerAngles.y != 0) rimTransform.localEulerAngles = new Vector3(0, 0, rimTransform.localEulerAngles.z);
			}
		}

		// Begin deflating the tire/leaking air
		public void Deflate()
		{
			airLeakTime = 0;

			if (impactSnd && tireAirClip)
			{
				impactSnd.PlayOneShot(tireAirClip);
				impactSnd.pitch = 1;
			}
		}

		public void FixTire()
		{
			popped = false;
			TirePressure = initialTirePressure;
			airLeakTime = -1;
		}

		// Detach the wheel from the vehicle
		public void Detach()
		{
			if (connected && canDetach)
			{
				connected = false;
				detachedWheel.SetActive(true);
				detachedWheel.transform.position = rimTransform.position;
				detachedWheel.transform.rotation = rimTransform.rotation;
				detachedCol.sharedMaterial = popped ? detachedRimMaterial : detachedTireMaterial;

				if (tireTransform)
				{
					detachedTire.SetActive(!popped);
					detachedCol.sharedMesh = airLeakTime >= 0 || popped
						? rimMeshLoose ? rimMeshLoose : detachFilter.sharedMesh
						: tireMeshLoose
							? tireMeshLoose
							: detachTireFilter.sharedMesh;
				}
				else
				{
					detachedCol.sharedMesh = rimMeshLoose ? rimMeshLoose : detachFilter.sharedMesh;
				}

				wheelBody.mass -= mass;
				detachedBody.linearVelocity = wheelBody.GetPointVelocity(rimTransform.position);
				detachedBody.angularVelocity = wheelBody.angularVelocity;

				rimTransform.gameObject.SetActive(false);

				if (sphereColTr) sphereColTr.gameObject.SetActive(false);
			}
		}

		// Automatically sets wheel dimensions based on rim/tire meshes
		public void GetWheelDimensions(float radiusMargin, float widthMargin)
		{
			Mesh rimMesh = null;
			Mesh tireMesh = null;
			Mesh checker;
			Transform scaler = transform;

			if (transform.childCount > 0)
			{
				if (transform.GetChild(0).GetComponent<MeshFilter>())
				{
					rimMesh = transform.GetChild(0).GetComponent<MeshFilter>().sharedMesh;
					scaler = transform.GetChild(0);
				}

				if (transform.GetChild(0).childCount > 0)
					if (transform.GetChild(0).GetChild(0).GetComponent<MeshFilter>())
						tireMesh = transform.GetChild(0).GetChild(0).GetComponent<MeshFilter>().sharedMesh;

				checker = tireMesh ? tireMesh : rimMesh;

				if (checker)
				{
					float maxWidth = 0;
					float maxRadius = 0;

					foreach (Vector3 curVert in checker.vertices)
					{
						if (new Vector2(curVert.x * scaler.localScale.x, curVert.y * scaler.localScale.y).magnitude > maxRadius)
							maxRadius = new Vector2(curVert.x * scaler.localScale.x, curVert.y * scaler.localScale.y).magnitude;

						if (Abs(curVert.z * scaler.localScale.z) > maxWidth) maxWidth = Abs(curVert.z * scaler.localScale.z);
					}

					TireRadius = maxRadius + radiusMargin;
					TireWidth = maxWidth + widthMargin;

					if (tireMesh && rimMesh)
					{
						maxWidth = 0;
						maxRadius = 0;

						foreach (Vector3 curVert in rimMesh.vertices)
						{
							if (new Vector2(curVert.x * scaler.localScale.x, curVert.y * scaler.localScale.y).magnitude > maxRadius)
								maxRadius = new Vector2(curVert.x * scaler.localScale.x, curVert.y * scaler.localScale.y).magnitude;

							if (Abs(curVert.z * scaler.localScale.z) > maxWidth) maxWidth = Abs(curVert.z * scaler.localScale.z);
						}

						RimRadius = maxRadius + radiusMargin;
						RimWidth = maxWidth + widthMargin;
					}
					else
					{
						RimRadius = maxRadius * 0.5f + radiusMargin;
						RimWidth = maxWidth * 0.5f + widthMargin;
					}
				}
				else
				{
					Debug.LogError("No rim or tire meshes found for getting wheel dimensions.", this);
				}
			}
		}

		// Attach the wheel back onto its vehicle if detached
		public void Reattach()
		{
			if (!connected)
			{
				connected = true;
				detachedWheel.SetActive(false);
				wheelBody.mass += mass;
				rimTransform.gameObject.SetActive(true);

				if (sphereColTr) sphereColTr.gameObject.SetActive(true);
			}
		}

		// visualize wheel
		void OnDrawGizmosSelected()
		{
			wheelTransform = transform;

			if (wheelTransform.childCount > 0)
			{
				// Rim is the first child of this object
				rimTransform = wheelTransform.GetChild(0);

				// Tire mesh should be first child of rim
				if (rimTransform.childCount > 0) tireTransform = rimTransform.GetChild(0);
			}

			float tireActualRadius = Lerp(RimRadius, TireRadius, TirePressure);

			if (TirePressure < 1 && TirePressure > 0)
			{
				Gizmos.color = new Color(1, 1, 0, popped ? 0.5f : 1);
				GizmosExtra.DrawWireCylinder(rimTransform.position, rimTransform.forward, tireActualRadius, TireWidth * 2);
			}

			Gizmos.color = Color.white;
			GizmosExtra.DrawWireCylinder(rimTransform.position, rimTransform.forward, TireRadius, TireWidth * 2);

			Gizmos.color = TirePressure == 0 || popped ? Color.green : Color.cyan;
			GizmosExtra.DrawWireCylinder(rimTransform.position, rimTransform.forward, RimRadius, RimWidth * 2);

			Gizmos.color = new Color(1, 1, 1, TirePressure < 1 ? 0.5f : 1);
			GizmosExtra.DrawWireCylinder(rimTransform.position, rimTransform.forward, TireRadius, TireWidth * 2);

			Gizmos.color = TirePressure == 0 || popped ? Color.green : Color.cyan;
			GizmosExtra.DrawWireCylinder(rimTransform.position, rimTransform.forward, RimRadius, RimWidth * 2);
		}

		// Destroy detached wheel
		void OnDestroy()
		{
			if (Application.isPlaying)
				if (detachedWheel)
					Destroy(detachedWheel);
		}
	}

	// Contact point class
	public class WheelContact
	{
		public bool grounded; // Is the contact point grounded?
		public Collider col; // The collider of the contact point
		public Vector3 point; // The position of the contact point
		public Vector3 normal; // The normal of the contact point
		public Vector3 relativeVelocity; // Relative velocity between the wheel and the contact point object
		public float distance; // Distance from the suspension to the contact point minus the wheel radius
		public float surfaceFriction; // Friction of the contact surface
		public int surfaceType; // The surface type identified by the surface types array of GroundSurfaceMaster
	}
}
