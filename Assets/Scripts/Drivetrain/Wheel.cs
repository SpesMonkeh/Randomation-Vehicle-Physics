using UnityEngine;

namespace RVP
{
	[RequireComponent(typeof(DriveForce))]
	[ExecuteInEditMode]
	[DisallowMultipleComponent]
	[AddComponentMenu("RVP/Drivetrain/Wheel", 1)]

	// Class for the wheel
	public class Wheel : MonoBehaviour
	{
		[SerializeField] Transform tr;
		[SerializeField] Rigidbody rb;
		[SerializeField] VehicleParent vp;
		[field: SerializeField] public Suspension SuspensionParent { get; private set; }
		[field: SerializeField] public Transform Rim { get; private set; }
		[SerializeField] Transform tire;
		[SerializeField] Vector3 localVel;

		[Tooltip("Generate a sphere collider to represent the wheel for side collisions")] [SerializeField] bool generateHardCollider = true;
		[SerializeField] SphereCollider sphereCol; // Hard collider
		[SerializeField] Transform sphereColTr; // Hard collider transform

		[Header("Rotation")]
		[Tooltip("Bias for feedback RPM lerp between target RPM and raw RPM")]
		[SerializeField, Range(0, 1)] float feedbackRpmBias;

		[Tooltip("Curve for setting final RPM of wheel based on driving torque/brake force, x-axis = torque/brake force, y-axis = lerp between raw RPM and target RPM")]
		[SerializeField] AnimationCurve rpmBiasCurve = AnimationCurve.Linear(0, 0, 1, 1);

		[Tooltip("As the RPM of the wheel approaches this value, the RPM bias curve is interpolated with the default linear curve")]
		[SerializeField] float rpmBiasCurveLimit = Mathf.Infinity;
		[SerializeField, Range(0, 10)] float axleFriction;

		[Header("Friction")]
		[SerializeField, Range(0, 1)] float frictionSmoothness = 0.5f;
		[SerializeField] float forwardFriction = 1;
		[SerializeField] float sidewaysFriction = 1;
		[SerializeField] float forwardRimFriction = 0.5f;
		[SerializeField] float sidewaysRimFriction = 0.5f;
		[SerializeField] float forwardCurveStretch = 1;
		[SerializeField] float sidewaysCurveStretch = 1;
		[SerializeField] Vector3 frictionForce = Vector3.zero;

		[Tooltip("X-axis = slip, y-axis = friction")] [SerializeField] AnimationCurve forwardFrictionCurve = AnimationCurve.Linear(0, 0, 1, 1);

		[Tooltip("X-axis = slip, y-axis = friction")] [SerializeField] AnimationCurve sidewaysFrictionCurve = AnimationCurve.Linear(0, 0, 1, 1);
		[field: SerializeField] public float ForwardSlip { get; private set; }
		[field: SerializeField] public float SidewaysSlip { get; private set; }

		public enum SlipDependenceMode
		{
			dependent,
			forward,
			sideways,
			independent
		}

		[SerializeField] SlipDependenceMode slipDependence = SlipDependenceMode.sideways;
		[SerializeField, Range(0, 2)] float forwardSlipDependence = 2;
		[SerializeField, Range(0, 2)] float sidewaysSlipDependence = 2;

		[Tooltip("Adjusts how much friction the wheel has based on the normal of the ground surface. X-axis = normal dot product, y-axis = friction multiplier")]
		[SerializeField] AnimationCurve normalFrictionCurve = AnimationCurve.Linear(0, 1, 1, 1);

		[Tooltip("How much the suspension compression affects the wheel friction")]
		[SerializeField, Range(0, 1)] float compressionFrictionFactor = 0.5f;

		 public float TireRadius { get => tireRadius; private set => tireRadius = value; }
		[field: SerializeField] public float RimRadius { get; private set; }
		[field: SerializeField] public float TireWidth { get; private set; }
		[field: SerializeField] public float RimWidth { get; private set; }

		[SerializeField] float setTireWidth;
		[SerializeField] float tireWidthPrev;
		[SerializeField] float setTireRadius;
		[SerializeField] float tireRadiusPrev;
		[SerializeField] float setRimWidth;
		[SerializeField] float rimWidthPrev;
		[SerializeField] float setRimRadius;
		[SerializeField] float rimRadiusPrev;
		[field: SerializeField] public float ActualRadius { get; private set; }

		 public float TirePressure { get => tirePressure; private set => tirePressure = value; }
		[SerializeField] float setTirePressure;
		[SerializeField] float tirePressurePrev;
		[SerializeField] float initialTirePressure;
		[field: SerializeField] public bool Popped { get; private set; }
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
		[field: SerializeField] public DriveForce TargetDrive { get; private set; }
		[field: SerializeField] public float RawRPM { get; private set; }
		 public WheelContact ContactPoint => contactPoint;
		public bool GetContact { get => getContact; internal set => getContact = value; }
		public bool Grounded { get => grounded; private set => grounded = value; }
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

		[field: SerializeField] public float Damage { get; internal set; }
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
		[SerializeField] WheelContact contactPoint = new();

		[Header("Tire")]
		[SerializeField, Range(0, 1)] float tirePressure = 1;
		[Header("Damage")]
		[SerializeField] float detachForce = Mathf.Infinity;
		[Header("Size")]
		[SerializeField] float tireRadius;

		public float DetachForce => detachForce;

		public bool Connected { get => connected; private set => connected = value; }

		public Vector3 ContactVelocity { get => contactVelocity; private set => contactVelocity = value; }
		 internal Rigidbody RB => rb;
		 internal VehicleParent Vp => vp;
		 internal Transform Tire => tire;
		 internal Vector3 LocalVel => localVel;
		 internal bool GenerateHardCollider => generateHardCollider;
		 internal SphereCollider SphereCol => sphereCol;
		 internal Transform SphereColTr => sphereColTr;
		 internal float FeedbackRpmBias => feedbackRpmBias;
		 internal AnimationCurve RPMBiasCurve => rpmBiasCurve;
		 internal float RPMBiasCurveLimit => rpmBiasCurveLimit;
		 internal float AxleFriction => axleFriction;
		 internal float FrictionSmoothness => frictionSmoothness;
		 internal float ForwardFriction => forwardFriction;
		 internal float SidewaysFriction => sidewaysFriction;
		 internal float ForwardRimFriction => forwardRimFriction;
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
		 internal bool SetPopped => setPopped;
		 internal bool PoppedPrev => poppedPrev;
		 internal bool CanPop => canPop;
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
		 internal Transform TR { get => tr; private set => tr = value; }
		 internal float TravelDist { get => travelDist; private set => travelDist = value; }

		 void Start()
		{
			tr = transform;
			rb = tr.GetTopmostParentComponent<Rigidbody>();
			vp = tr.GetTopmostParentComponent<VehicleParent>();
			SuspensionParent = tr.parent.GetComponent<Suspension>();
			travelDist = SuspensionParent.targetCompression;
			canDetach = detachForce < Mathf.Infinity && Application.isPlaying;
			initialTirePressure = TirePressure;

			if (tr.childCount > 0)
			{
				// Get rim
				Rim = tr.GetChild(0);

				// Set up rim glow material
				if (rimGlow > 0 && Application.isPlaying)
				{
					rimMat = new Material(Rim.GetComponent<MeshRenderer>().sharedMaterial);
					rimMat.EnableKeyword("_EMISSION");
					Rim.GetComponent<MeshRenderer>().sharedMaterial = rimMat;
				}

				// Create detached wheel
				if (canDetach)
				{
					detachedWheel = new GameObject(vp.transform.name + " Detached Wheel")
					{
						layer = LayerMask.NameToLayer("Detachable Part")
					};
					detachFilter = detachedWheel.AddComponent<MeshFilter>();
					detachFilter.sharedMesh = Rim.GetComponent<MeshFilter>().sharedMesh;
					MeshRenderer detachRend = detachedWheel.AddComponent<MeshRenderer>();
					detachRend.sharedMaterial = Rim.GetComponent<MeshRenderer>().sharedMaterial;
					detachedCol = detachedWheel.AddComponent<MeshCollider>();
					detachedCol.convex = true;
					detachedBody = detachedWheel.AddComponent<Rigidbody>();
					detachedBody.mass = mass;
				}

				// Get tire
				if (Rim.childCount > 0)
				{
					tire = Rim.GetChild(0);
					if (deformAmount > 0 && Application.isPlaying)
					{
						tireMat = new Material(tire.GetComponent<MeshRenderer>().sharedMaterial);
						tire.GetComponent<MeshRenderer>().sharedMaterial = tireMat;
					}

					// Create detached tire
					if (canDetach)
					{
						detachedTire = new GameObject("Detached Tire");
						detachedTire.transform.parent = detachedWheel.transform;
						detachedTire.transform.localPosition = Vector3.zero;
						detachedTire.transform.localRotation = Quaternion.identity;
						detachTireFilter = detachedTire.AddComponent<MeshFilter>();
						detachTireFilter.sharedMesh = tire.GetComponent<MeshFilter>().sharedMesh;
						MeshRenderer detachTireRend = detachedTire.AddComponent<MeshRenderer>();
						detachTireRend.sharedMaterial = tireMat ? tireMat : tire.GetComponent<MeshRenderer>().sharedMaterial;
					}
				}

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
						sphereColTr.parent = TR;
						sphereColTr.localPosition = Vector3.zero;
						sphereColTr.localRotation = Quaternion.identity;
						sphereCol.radius = Mathf.Min(RimWidth * 0.5f, RimRadius * 0.5f);
						sphereCol.sharedMaterial = GlobalControl.frictionlessMatStatic;
					}

					if (canDetach)
					{
						detachedWheel.SetActive(false);
					}
				}
			}

			TargetDrive = GetComponent<DriveForce>();
			currentRPM = 0;
		}

		void FixedUpdate()
		{
			upDir = TR.up;
			ActualRadius = Popped ? RimRadius : Mathf.Lerp(RimRadius, TireRadius, TirePressure);
			circumference = Mathf.PI * ActualRadius * 2;
			localVel = rb.GetPointVelocity(forceApplicationPoint);

			// Get proper inputs
			actualEbrake = SuspensionParent.ebrakeEnabled ? SuspensionParent.ebrakeForce : 0;
			actualTargetRPM = TargetDrive.rpm * (SuspensionParent.driveInverted ? -1 : 1);
			actualTorque = SuspensionParent.driveEnabled ? Mathf.Lerp(TargetDrive.torque, Mathf.Abs(vp.AccelInput), vp.Burnout) : 0;

			if (GetContact)
			{
				GetWheelContact();
			}
			else if (Grounded)
			{
				ContactPoint.point += localVel * Time.fixedDeltaTime;
			}

			airTime = Grounded ? 0 : airTime + Time.fixedDeltaTime;
			forceApplicationPoint = applyForceAtGroundContact ? ContactPoint.point : TR.position;

			if (Connected)
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

			// Get travel distance
			TravelDist = SuspensionParent.compression < TravelDist || Grounded
				? SuspensionParent.compression
				: Mathf.Lerp(TravelDist, SuspensionParent.compression, SuspensionParent.extendSpeed * Time.fixedDeltaTime);

			PositionWheel();

			if (Connected)
			{
				// Update hard collider size upon changed radius or width
				if (generateHardCollider)
				{
					setRimWidth = RimWidth;
					setRimRadius = RimRadius;
					setTireWidth = TireWidth;
					setTireRadius = TireRadius;
					setTirePressure = TirePressure;

					if (rimWidthPrev != setRimWidth || rimRadiusPrev != setRimRadius)
					{
						sphereCol.radius = Mathf.Min(RimWidth * 0.5f, RimRadius * 0.5f);
						UpdatedSize = true;
					}
					else if (tireWidthPrev != setTireWidth || tireRadiusPrev != setTireRadius || tirePressurePrev != setTirePressure)
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

				GetSlip();
				ApplyFriction();

				// Burnout spinning
				if (vp.Burnout > 0 && TargetDrive.rpm != 0 && actualEbrake * vp.EbrakeInput == 0 && Connected && Grounded)
				{
					rb.AddForceAtPosition(
						SuspensionParent.forwardDir * -SuspensionParent.flippedSideFactor *
						(vp.SteerInput * vp.BurnoutSpin * currentRPM * Mathf.Min(0.1f, TargetDrive.torque) * 0.001f) * vp.Burnout *
						(Popped ? 0.5f : 1) * ContactPoint.surfaceFriction, SuspensionParent.tr.position, vp.WheelForceMode);
				}

				// Popping logic
				setPopped = Popped;

				if (poppedPrev != setPopped)
				{
					if (tire)
					{
						tire.gameObject.SetActive(!Popped);
					}

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
					TirePressure = Mathf.Clamp01(TirePressure - Time.fixedDeltaTime * 0.5f);

					if (Grounded)
					{
						airLeakTime += Mathf.Max(Mathf.Abs(currentRPM) * 0.001f, localVel.magnitude * 0.1f) * Time.timeScale *
						               TimeMaster.inverseFixedTimeFactor;

						if (airLeakTime > 1000 && TirePressure == 0)
						{
							Popped = true;
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

		void Update()
		{
			RotateWheel();

			if (!Application.isPlaying)
			{
				PositionWheel();
			}
			else
			{
				// Update tire and rim materials
				if (deformAmount > 0 && tireMat && Connected)
				{
					if (tireMat.HasProperty("_DeformNormal"))
					{
						// Deform tire (requires deform shader)
						Vector3 deformNormal = Grounded
							? ContactPoint.normal *
							  Mathf.Max(-SuspensionParent.penetration * (1 - SuspensionParent.compression) * 10, 1 - TirePressure) * deformAmount
							: Vector3.zero;
						tireMat.SetVector("_DeformNormal", new Vector4(deformNormal.x, deformNormal.y, deformNormal.z, 0));
					}
				}

				if (rimMat)
				{
					if (rimMat.HasProperty("_EmissionColor"))
					{
						// Make the rim glow
						float targetGlow = Connected && GroundSurfaceMaster.surfaceTypesStatic[ContactPoint.surfaceType].leaveSparks
							? Mathf.Abs(F.MaxAbs(ForwardSlip, SidewaysSlip))
							: 0;
						glowAmount = Popped ? Mathf.Lerp(glowAmount, targetGlow, (targetGlow > glowAmount ? 2 : 0.2f) * Time.deltaTime) : 0;
						glowColor = new Color(glowAmount, glowAmount * 0.5f, 0);
						rimMat.SetColor("_EmissionColor", Popped ? Color.Lerp(Color.black, glowColor, glowAmount * rimGlow) : Color.black);
					}
				}
			}
		}

		// Use raycasting to find the current contact point for the wheel
		void GetWheelContact()
		{
			float castDist = Mathf.Max(SuspensionParent.suspensionDistance * Mathf.Max(0.001f, SuspensionParent.targetCompression) + ActualRadius,
				0.001f);
			RaycastHit[] wheelHits = Physics.RaycastAll(SuspensionParent.maxCompressPoint, SuspensionParent.springDirection, castDist,
				GlobalControl.wheelCastMaskStatic);
			int hitIndex = 0;
			bool validHit = false;
			float hitDist = Mathf.Infinity;

			if (connected)
			{
				// Loop through raycast hits to find closest one
				for (int i = 0; i < wheelHits.Length; i++)
				{
					if (!wheelHits[i].transform.IsChildOf(vp.Tr) && wheelHits[i].distance < hitDist)
					{
						hitIndex = i;
						hitDist = wheelHits[i].distance;
						validHit = true;
					}
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

				if (!grounded && impactSnd && ((tireHitClips.Length > 0 && !Popped) || (rimHitClip && Popped)))
				{
					impactSnd.PlayOneShot(Popped ? rimHitClip : tireHitClips[Mathf.RoundToInt(Random.Range(0, tireHitClips.Length - 1))],
						Mathf.Clamp01(airTime * airTime));
					impactSnd.pitch = Mathf.Clamp(airTime * 0.2f + 0.8f, 0.8f, 1);
				}

				grounded = true;
				contactPoint.distance = hit.distance - ActualRadius;
				contactPoint.point = hit.point + localVel * Time.fixedDeltaTime;
				contactPoint.grounded = true;
				contactPoint.normal = hit.normal;
				contactPoint.relativeVelocity = tr.InverseTransformDirection(localVel);
				contactPoint.col = hit.collider;

				if (hit.collider.attachedRigidbody)
				{
					contactVelocity = hit.collider.attachedRigidbody.GetPointVelocity(contactPoint.point);
					contactPoint.relativeVelocity -= tr.InverseTransformDirection(ContactVelocity);
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

				if (contactPoint.col.CompareTag("Pop Tire") && canPop && Mathf.Approximately(airLeakTime, -1) && !Popped)
				{
					Deflate();
				}
			}
			else
			{
				grounded = false;
				contactPoint.distance = SuspensionParent.suspensionDistance;
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
			{
				RawRPM = (ContactPoint.relativeVelocity.x / circumference) * (Mathf.PI * 100) * -SuspensionParent.flippedSideFactor;
			}
			else
			{
				RawRPM = Mathf.Lerp(RawRPM, actualTargetRPM,
					(actualTorque + SuspensionParent.brakeForce * vp.BrakeInput + actualEbrake * vp.EbrakeInput) * Time.timeScale);
			}
		}

		// Calculate the current slip amount
		void GetSlip()
		{
			if (grounded)
			{
				SidewaysSlip = contactPoint.relativeVelocity.z * 0.1f / sidewaysCurveStretch;
				ForwardSlip = 0.01f * (RawRPM - currentRPM) / forwardCurveStretch;
			}
			else
			{
				SidewaysSlip = 0;
				ForwardSlip = 0;
			}
		}

		// Apply actual forces to rigidbody based on wheel simulation
		void ApplyFriction()
		{
			if (grounded)
			{
				float forwardSlipFactor = (int)slipDependence == 0 || (int)slipDependence == 1 ? ForwardSlip - SidewaysSlip : ForwardSlip;
				float sidewaysSlipFactor = (int)slipDependence == 0 || (int)slipDependence == 2 ? SidewaysSlip - ForwardSlip : SidewaysSlip;
				float forwardSlipDependenceFactor = Mathf.Clamp01(forwardSlipDependence - Mathf.Clamp01(Mathf.Abs(SidewaysSlip)));
				float sidewaysSlipDependenceFactor = Mathf.Clamp01(sidewaysSlipDependence - Mathf.Clamp01(Mathf.Abs(ForwardSlip)));

				float targetForceX = forwardFrictionCurve.Evaluate(Mathf.Abs(forwardSlipFactor)) * -System.Math.Sign(ForwardSlip) *
				                     (Popped ? forwardRimFriction : forwardFriction) * forwardSlipDependenceFactor *
				                     -SuspensionParent.flippedSideFactor;
				float targetForceZ = sidewaysFrictionCurve.Evaluate(Mathf.Abs(sidewaysSlipFactor)) * -System.Math.Sign(SidewaysSlip) *
				                     (Popped ? sidewaysRimFriction : sidewaysFriction) * sidewaysSlipDependenceFactor *
				                     normalFrictionCurve.Evaluate(Mathf.Clamp01(Vector3.Dot(contactPoint.normal, GlobalControl.worldUpDir))) *
				                     (vp.Burnout > 0 && Mathf.Abs(TargetDrive.rpm) != 0 && actualEbrake * vp.EbrakeInput == 0 && Grounded
					                     ? (1 - vp.Burnout) * (1 - Mathf.Abs(vp.AccelInput))
					                     : 1);

				Vector3 targetForce = tr.TransformDirection(targetForceX, 0, targetForceZ);
				float targetForceMultiplier =
					((1 - compressionFrictionFactor) + (1 - SuspensionParent.compression) * compressionFrictionFactor *
						Mathf.Clamp01(Mathf.Abs(SuspensionParent.tr.InverseTransformDirection(localVel).z) * 10)) * contactPoint.surfaceFriction;
				frictionForce = Vector3.Lerp(frictionForce, targetForce * targetForceMultiplier, 1 - frictionSmoothness);
				rb.AddForceAtPosition(frictionForce, forceApplicationPoint, vp.WheelForceMode);

				// If resting on a rigidbody, apply opposing force to it
				if (contactPoint.col.attachedRigidbody)
				{
					contactPoint.col.attachedRigidbody.AddForceAtPosition(-frictionForce, contactPoint.point, vp.WheelForceMode);
				}
			}
		}

		// Do torque and RPM calculations/simulation
		void ApplyDrive()
		{
			float brakeForce = 0;
			float brakeCheckValue = SuspensionParent.skidSteerBrake ? vp.LocalAngularVel.y : vp.LocalVelocity.z;

			// Set brake force
			if (vp.BrakeIsReverse)
			{
				if (brakeCheckValue > 0)
				{
					brakeForce = SuspensionParent.brakeForce * vp.BrakeInput;
				}
				else if (brakeCheckValue <= 0)
				{
					brakeForce = SuspensionParent.brakeForce * Mathf.Clamp01(vp.AccelInput);
				}
			}
			else
			{
				brakeForce = SuspensionParent.brakeForce * vp.BrakeInput;
			}

			brakeForce += axleFriction * 0.1f * (Mathf.Approximately(actualTorque, 0) ? 1 : 0);

			if (TargetDrive.rpm != 0)
			{
				brakeForce *= (1 - vp.Burnout);
			}

			// Set final RPM
			if (!SuspensionParent.jammed && Connected)
			{
				bool validTorque =
					(!(Mathf.Approximately(actualTorque, 0) && Mathf.Abs(actualTargetRPM) < 0.01f) && !Mathf.Approximately(actualTargetRPM, 0)) ||
					brakeForce + actualEbrake * vp.EbrakeInput > 0;

				currentRPM = Mathf.Lerp(RawRPM,
					Mathf.Lerp(Mathf.Lerp(RawRPM, actualTargetRPM, validTorque ? EvaluateTorque(actualTorque) : actualTorque), 0,
						Mathf.Max(brakeForce, actualEbrake * vp.EbrakeInput)),
					validTorque
						? EvaluateTorque(actualTorque + brakeForce + actualEbrake * vp.EbrakeInput)
						: actualTorque + brakeForce + actualEbrake * vp.EbrakeInput);

				TargetDrive.feedbackRPM = Mathf.Lerp(currentRPM, RawRPM, feedbackRpmBias);
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
			float torque = Mathf.Lerp(rpmBiasCurve.Evaluate(t), t, RawRPM / (rpmBiasCurveLimit * Mathf.Sign(actualTargetRPM)));
			return torque;
		}

		// Visual wheel positioning
		void PositionWheel()
		{
			if (SuspensionParent)
			{
				Rim.position = SuspensionParent.maxCompressPoint +
					SuspensionParent.springDirection * SuspensionParent.suspensionDistance *
					(Application.isPlaying ? TravelDist : SuspensionParent.targetCompression) +
					SuspensionParent.upDir *
					Mathf.Pow(
						Mathf.Max(Mathf.Abs(Mathf.Sin(SuspensionParent.sideAngle * Mathf.Deg2Rad)),
							Mathf.Abs(Mathf.Sin(SuspensionParent.casterAngle * Mathf.Deg2Rad))), 2) * ActualRadius +
					SuspensionParent.pivotOffset * SuspensionParent.tr.TransformDirection(Mathf.Sin(TR.localEulerAngles.y * Mathf.Deg2Rad), 0,
						Mathf.Cos(TR.localEulerAngles.y * Mathf.Deg2Rad)) - SuspensionParent.pivotOffset *
					(Application.isPlaying ? SuspensionParent.forwardDir : SuspensionParent.tr.forward);
			}

			if (Application.isPlaying && generateHardCollider && Connected)
			{
				sphereColTr.position = Rim.position;
			}
		}

		// Visual wheel rotation
		void RotateWheel()
		{
			if (TR && SuspensionParent)
			{
				float ackermannVal = Mathf.Sign(SuspensionParent.steerAngle) == SuspensionParent.flippedSideFactor
					? 1 + SuspensionParent.ackermannFactor
					: 1 - SuspensionParent.ackermannFactor;
				TR.localEulerAngles = new Vector3(
					SuspensionParent.camberAngle + SuspensionParent.casterAngle * SuspensionParent.steerAngle * SuspensionParent.flippedSideFactor,
					-SuspensionParent.toeAngle * SuspensionParent.flippedSideFactor + SuspensionParent.steerDegrees * ackermannVal, 0);
			}

			if (Application.isPlaying)
			{
				Rim.Rotate(Vector3.forward, currentRPM * SuspensionParent.flippedSideFactor * Time.deltaTime);

				if (Damage > 0)
				{
					Rim.localEulerAngles = new Vector3(Mathf.Sin(-Rim.localEulerAngles.z * Mathf.Deg2Rad) * Mathf.Clamp(Damage, 0, 10),
						Mathf.Cos(-Rim.localEulerAngles.z * Mathf.Deg2Rad) * Mathf.Clamp(Damage, 0, 10), Rim.localEulerAngles.z);
				}
				else if (Rim.localEulerAngles.x != 0 || Rim.localEulerAngles.y != 0)
				{
					Rim.localEulerAngles = new Vector3(0, 0, Rim.localEulerAngles.z);
				}
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
			Popped = false;
			TirePressure = initialTirePressure;
			airLeakTime = -1;
		}

		// Detach the wheel from the vehicle
		public void Detach()
		{
			if (Connected && canDetach)
			{
				Connected = false;
				detachedWheel.SetActive(true);
				detachedWheel.transform.position = Rim.position;
				detachedWheel.transform.rotation = Rim.rotation;
				detachedCol.sharedMaterial = Popped ? detachedRimMaterial : detachedTireMaterial;

				if (tire)
				{
					detachedTire.SetActive(!Popped);
					detachedCol.sharedMesh = airLeakTime >= 0 || Popped
						? (rimMeshLoose ? rimMeshLoose : detachFilter.sharedMesh)
						: (tireMeshLoose ? tireMeshLoose : detachTireFilter.sharedMesh);
				}
				else
				{
					detachedCol.sharedMesh = rimMeshLoose ? rimMeshLoose : detachFilter.sharedMesh;
				}

				rb.mass -= mass;
				detachedBody.linearVelocity = rb.GetPointVelocity(Rim.position);
				detachedBody.angularVelocity = rb.angularVelocity;

				Rim.gameObject.SetActive(false);

				if (sphereColTr)
				{
					sphereColTr.gameObject.SetActive(false);
				}
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
				{
					if (transform.GetChild(0).GetChild(0).GetComponent<MeshFilter>())
					{
						tireMesh = transform.GetChild(0).GetChild(0).GetComponent<MeshFilter>().sharedMesh;
					}
				}

				checker = tireMesh ? tireMesh : rimMesh;

				if (checker)
				{
					float maxWidth = 0;
					float maxRadius = 0;

					foreach (Vector3 curVert in checker.vertices)
					{
						if (new Vector2(curVert.x * scaler.localScale.x, curVert.y * scaler.localScale.y).magnitude > maxRadius)
						{
							maxRadius = new Vector2(curVert.x * scaler.localScale.x, curVert.y * scaler.localScale.y).magnitude;
						}

						if (Mathf.Abs(curVert.z * scaler.localScale.z) > maxWidth)
						{
							maxWidth = Mathf.Abs(curVert.z * scaler.localScale.z);
						}
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
							{
								maxRadius = new Vector2(curVert.x * scaler.localScale.x, curVert.y * scaler.localScale.y).magnitude;
							}

							if (Mathf.Abs(curVert.z * scaler.localScale.z) > maxWidth)
							{
								maxWidth = Mathf.Abs(curVert.z * scaler.localScale.z);
							}
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
			if (!Connected)
			{
				Connected = true;
				detachedWheel.SetActive(false);
				rb.mass += mass;
				Rim.gameObject.SetActive(true);

				if (sphereColTr)
				{
					sphereColTr.gameObject.SetActive(true);
				}
			}
		}

		// visualize wheel
		void OnDrawGizmosSelected()
		{
			TR = transform;

			if (TR.childCount > 0)
			{
				// Rim is the first child of this object
				Rim = TR.GetChild(0);

				// Tire mesh should be first child of rim
				if (Rim.childCount > 0)
				{
					tire = Rim.GetChild(0);
				}
			}

			float tireActualRadius = Mathf.Lerp(RimRadius, TireRadius, TirePressure);

			if (TirePressure < 1 && TirePressure > 0)
			{
				Gizmos.color = new Color(1, 1, 0, Popped ? 0.5f : 1);
				GizmosExtra.DrawWireCylinder(Rim.position, Rim.forward, tireActualRadius, TireWidth * 2);
			}

			Gizmos.color = Color.white;
			GizmosExtra.DrawWireCylinder(Rim.position, Rim.forward, TireRadius, TireWidth * 2);

			Gizmos.color = TirePressure == 0 || Popped ? Color.green : Color.cyan;
			GizmosExtra.DrawWireCylinder(Rim.position, Rim.forward, RimRadius, RimWidth * 2);

			Gizmos.color = new Color(1, 1, 1, TirePressure < 1 ? 0.5f : 1);
			GizmosExtra.DrawWireCylinder(Rim.position, Rim.forward, TireRadius, TireWidth * 2);

			Gizmos.color = TirePressure == 0 || Popped ? Color.green : Color.cyan;
			GizmosExtra.DrawWireCylinder(Rim.position, Rim.forward, RimRadius, RimWidth * 2);
		}

		// Destroy detached wheel
		void OnDestroy()
		{
			if (Application.isPlaying)
			{
				if (detachedWheel)
				{
					Destroy(detachedWheel);
				}
			}
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
