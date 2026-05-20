using UnityEngine;
using UnityEngine.InputSystem;

namespace RVP
{
    /// Class for setting the camera input with the input manager
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CameraControl))]
    [AddComponentMenu("RVP2WoL/Camera/Basic Camera Input", 1)]
    public class BasicCameraInput : MonoBehaviour
    {
        CameraControl cam;

        void OnEnable()
        {
            PlayerControlsHandler.LookAction += OnLook;
        }

        void OnDisable()
        {
            PlayerControlsHandler.LookAction -= OnLook;
        }

        void Awake()
        {
            cam = GetComponent<CameraControl>();
        }

        void OnLook(Vector2 input)
        {
           cam.SetInput(input.x, input.y);
        }
    }
}
