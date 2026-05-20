// Copyright © Christian Holm Christensen
// 18/05/2026

using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RVP
{
    [RequireComponent(typeof(VehicleParent)), DisallowMultipleComponent, AddComponentMenu("RVP2WoL/Input/Player Controls Handler")]
	public sealed class PlayerControlsHandler : MonoBehaviour
	{
		[SerializeField] InputActionAsset inputActionAsset;

		void Awake()
		{
			if (!inputActionAsset)
			{
				Debug.LogError($"EXIT :: {name}: No input action asset assigned.", this);
				enabled = false;
				return;
			}
			inputActionAsset.Enable();
		}

		internal static Action RestartAction = () => { };
		internal static Action<float> SteerAction = _ => { };
		internal static Action<float> AccelerateAction = _ => { };
		internal static Action<float> BrakeAction = _ => { };
		internal static Action<float> EBrakeAction = _ => { };
		internal static Action<bool> BoostAction = _ => { };
		internal static Action<float> UpShiftAction			= _ => { };
		internal static Action<float> DownShiftAction		= _ => { };
		internal static Action<float> PitchAction			= _ => { };
		internal static Action<float> YawAction = _ => { };
		internal static Action<float> RollAction = _ => { };
		internal static Action<Vector2> LookAction = _ => { };

		void OnLook(InputAction.CallbackContext ctx)
		{
			Vector2 input = ctx.ReadValue<Vector2>();
			LookAction?.Invoke(input);
		}

		void OnRestart(InputAction.CallbackContext ctx)
		{
			if (ctx.phase is InputActionPhase.Performed)
				RestartAction?.Invoke();
		}

		void OnAccelerate(InputAction.CallbackContext ctx)
		{
			float value = ctx.ReadValue<float>();
			AccelerateAction?.Invoke(value);
		}

		void OnBrake(InputAction.CallbackContext ctx)
		{
			float value = ctx.ReadValue<float>();
			BrakeAction?.Invoke(value);
		}

		void OnBoost(InputAction.CallbackContext ctx)
		{
			switch (ctx.phase)
			{
				case InputActionPhase.Performed:
					BoostAction?.Invoke(true);
					break;
				case InputActionPhase.Canceled:
					BoostAction?.Invoke(false);
					break;
				case InputActionPhase.Disabled:
				case InputActionPhase.Waiting:
				case InputActionPhase.Started:
				default:
					break;
			}
		}

		void OnUpshift(InputAction.CallbackContext ctx)
		{
			float value = ctx.ReadValue<float>();
			switch (ctx.phase)
			{
				case InputActionPhase.Performed:
					UpShiftAction?.Invoke(value);
					break;
				case InputActionPhase.Canceled:
				case InputActionPhase.Started:
				case InputActionPhase.Waiting:
				case InputActionPhase.Disabled:
				default:
					break;
			}
		}

		void OnDownshift(InputAction.CallbackContext ctx)
		{
			float value = ctx.ReadValue<float>();
			switch (ctx.phase)
			{
				case InputActionPhase.Performed:
					DownShiftAction?.Invoke(value);
					break;
				case InputActionPhase.Canceled:
				case InputActionPhase.Started:
				case InputActionPhase.Waiting:
				case InputActionPhase.Disabled:
				default:
					break;
			}
		}
		void OnEBrake(InputAction.CallbackContext ctx)
		{
			float value = ctx.ReadValue<float>();
			EBrakeAction?.Invoke(value);
		}

		void OnSteer(InputAction.CallbackContext ctx)
		{
			Vector2 steer = ctx.ReadValue<Vector2>();
			SteerAction?.Invoke(steer.x);
		}

		void OnPitch(InputAction.CallbackContext ctx)
		{
			Vector2 pitch = ctx.ReadValue<Vector2>();
			PitchAction?.Invoke(pitch.x);
		}

		void OnYaw(InputAction.CallbackContext ctx)
		{
			Vector2 yaw = ctx.ReadValue<Vector2>();
			YawAction?.Invoke(yaw.x);
		}

		void OnRoll(InputAction.CallbackContext ctx)
		{
			Vector2 roll = ctx.ReadValue<Vector2>();
			RollAction?.Invoke(roll.x);
		}
	}
}
