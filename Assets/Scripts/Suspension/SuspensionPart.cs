using UnityEngine;

namespace RVP
{
    /// Class for moving suspension parts
    [ExecuteInEditMode, DisallowMultipleComponent, AddComponentMenu("RVP2WoL/Suspension/Suspension Part", 1)]
    public class SuspensionPart : MonoBehaviour
    {
        Transform tr;
        Wheel wheel;
        public Suspension suspension;
        public bool isHub;

        [Header("Connections")]
        [Tooltip("Object to point at")]
        [SerializeField] Transform connectObject;

        [Tooltip("Local space point to point at in connectObj")]
        [SerializeField] Vector3 connectPoint;
        [SerializeField] Vector3 initialConnectPoint; // originally [NonSerialized]
        [SerializeField] Vector3 localConnectPoint; // Transformed connect point

        [Tooltip("Rotate to point at target?")]
        [SerializeField] bool rotate = true;

        [Tooltip("Scale along local z-axis to reach target?")]
        [SerializeField] bool stretch;
        [SerializeField] float initialDist;
        [SerializeField] Vector3 initialScale;

        [Header("Solid Axle")]
        [SerializeField] bool solidAxle;
        [SerializeField] bool invertRotation;

        [Tooltip("Does this part connect to a solid axle?")]
        [SerializeField] bool solidAxleConnector;

        // Wheels for solid axles
        [SerializeField] Wheel wheel1;
        [SerializeField] Wheel wheel2;
        [SerializeField] Vector3 wheelConnect1;
        [SerializeField] Vector3 wheelConnect2;

        [SerializeField] Vector3 parentUpDir; // Parent's up direction

        void Start()
        {
            tr = transform;
            initialConnectPoint = connectPoint;

            // Get the wheel
            if (suspension) {
                suspension.MovingParts.Add(this);

                if (suspension.Wheel) {
                    wheel = suspension.Wheel;
                }
            }

            // Get the initial distance from the target to use when stretching
            if (connectObject && !isHub && Application.isPlaying) {
                initialDist = Mathf.Max(Vector3.Distance(tr.position, connectObject.TransformPoint(connectPoint)), 0.01f);
                initialScale = tr.localScale;
            }
        }

        void Update() {
            if (!Application.isPlaying) {
                tr = transform;

                // Get the wheel
                if (suspension) {
                    if (suspension.Wheel) {
                        wheel = suspension.Wheel;
                    }
                }
            }

            if (tr) {
                if (!solidAxle && ((suspension && !solidAxleConnector) || solidAxleConnector)) {
                    // Transformations for hubs
                    if (isHub && wheel && !solidAxleConnector) {
                        if (wheel.RimTransform) {
                            tr.position = wheel.RimTransform.position;
                            tr.rotation = Quaternion.LookRotation(wheel.RimTransform.forward, suspension.UpDirection);
                            tr.localEulerAngles = new Vector3(tr.localEulerAngles.x, tr.localEulerAngles.y, -suspension.casterAngle * suspension.flippedSideFactor);
                        }
                    }
                    else if (isHub is false && connectObject)
                    {
                        localConnectPoint = connectObject.TransformPoint(connectPoint);

                        RotateTowardsConnectionPoint();
                        StretchSuspensionPart();
                    }
                }
                else if (solidAxle && wheel1 && wheel2)
                {
                    CalculateSolidAxleTransformations();
                }
            }
        }

        void CalculateSolidAxleTransformations()
        {
            if (wheel1.RimTransform && wheel2.RimTransform && wheel1.SuspensionParent && wheel2.SuspensionParent)
            {
                parentUpDir = tr.parent.up;
                wheelConnect1 = wheel1.RimTransform.TransformPoint(0, 0, -wheel1.SuspensionParent.pivotOffset);
                wheelConnect2 = wheel2.RimTransform.TransformPoint(0, 0, -wheel2.SuspensionParent.pivotOffset);
                tr.rotation = Quaternion.LookRotation((((wheelConnect1 + wheelConnect2) * 0.5f) - tr.position).normalized, parentUpDir);
                Vector2 local = tr.localEulerAngles;
                tr.localEulerAngles = new Vector3
                {
                    x = local.x,
                    y = local.y,
                    z = Vector3.Angle((wheelConnect1 - wheelConnect2).normalized, tr.parent.right) *
                        Mathf.Sign(Vector3.Dot((wheelConnect1 - wheelConnect2).normalized, parentUpDir)) * Mathf.Sign(tr.localPosition.z) *
                        (invertRotation ? -1 : 1)
                };
            }
        }

        void StretchSuspensionPart()
        {
            if (Application.isPlaying is false)
                return;
            if (stretch)
            {
                tr.localScale = new Vector3(tr.localScale.x, tr.localScale.y, initialScale.z * (Vector3.Distance(tr.position, localConnectPoint) / initialDist));
            }
        }

        void RotateTowardsConnectionPoint()
        {
            if (rotate) {
                tr.rotation = Quaternion.LookRotation((localConnectPoint - tr.position).normalized, (solidAxleConnector ? tr.parent.forward : suspension.upDir));

                // Don't set localEulerAngles if connected to a solid axle
                if (!solidAxleConnector)
                {
                    Vector2 local = tr.localEulerAngles;
                    tr.localEulerAngles = new Vector3(local.x, local.y, -suspension.CasterAngle * suspension.FlippedSideFactor);
                }
            }
        }

        void OnDrawGizmosSelected() {
            if (!tr)
            {
                tr = transform;
            }

            Gizmos.color = Color.green;

            // Visualize connections
            if (!isHub && connectObject && !solidAxle) {
                localConnectPoint = connectObject.TransformPoint(connectPoint);
                Gizmos.DrawLine(tr.position, localConnectPoint);
                Gizmos.DrawWireSphere(localConnectPoint, 0.01f);
            }
            else if (solidAxle && wheel1 && wheel2) {
                if (wheel1.RimTransform && wheel2.RimTransform && wheel1.SuspensionParent && wheel2.SuspensionParent)
                {
                    wheelConnect1 = wheel1.RimTransform.TransformPoint(0, 0, -wheel1.SuspensionParent.pivotOffset);
                    wheelConnect2 = wheel2.RimTransform.TransformPoint(0, 0, -wheel2.SuspensionParent.pivotOffset);
                    Gizmos.DrawLine(wheelConnect1, wheelConnect2);
                    Gizmos.DrawWireSphere(wheelConnect1, 0.01f);
                    Gizmos.DrawWireSphere(wheelConnect2, 0.01f);
                }
            }
        }
    }
}
