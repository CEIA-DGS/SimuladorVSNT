using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Allows manual control of the USV using the new Unity Input System (Keyboard).
/// Maps W/S to surge forces and A/D to yaw torques.
/// </summary>
[RequireComponent(typeof(UsvDynamics))]
public class UsvManualController : MonoBehaviour
{
    [Header("Thruster Limits")]
    
    /// <summary>Maximum force applied in the surge direction (W/S) in Newtons.</summary>
    [Tooltip("Maximum force applied in the surge direction (W/S) in Newtons.")]
    public float maxSurgeForce = 200f;
    
    /// <summary>Maximum torque applied in the yaw axis (A/D) in N.m.</summary>
    [Tooltip("Maximum torque applied in the yaw axis (A/D) in N.m.")]
    public float maxYawTorque = 80f;

    private UsvDynamics dynamics;

    private void Awake()
    {
        dynamics = GetComponent<UsvDynamics>();
    }

    private void FixedUpdate()
    {
        float surgeInput = 0f;
        float yawInput = 0f;

        // Ensure a keyboard is connected before polling states
        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed) surgeInput += 1f;
            if (Keyboard.current.sKey.isPressed) surgeInput -= 1f;
            
            if (Keyboard.current.dKey.isPressed) yawInput += 1f;
            if (Keyboard.current.aKey.isPressed) yawInput -= 1f;
        }

        // Calculate control effort (sway is intentionally zero for this model)
        float tauSway = 0f; 
        float tauYaw = yawInput * maxYawTorque; 
        float tauSurge = surgeInput * maxSurgeForce;

        Vector3 tau = new Vector3(tauSway, tauYaw, tauSurge);
        dynamics.SetCommand(tau);
    }
}