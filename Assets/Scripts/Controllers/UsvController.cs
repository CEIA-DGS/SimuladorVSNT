using UnityEngine;

/// <summary>
/// Controls the Unmanned Surface Vehicle (USV) by calculating actuator commands
/// based on Line-of-Sight (LOS) guidance references and dynamic compensation.
/// </summary>
[RequireComponent(typeof(UsvDynamics))]
[RequireComponent(typeof(LosGuidance))]
public class UsvController : MonoBehaviour
{
    [Header("Outer Loop Gains (Kinematics)")]
    
    /// <summary>Proportional gain for surge velocity.</summary>
    [Tooltip("Proportional gain for surge velocity.")]
    public float Kp_surge = 2.0f;
    
    /// <summary>Proportional gain for heading (yaw) error.</summary>
    [Tooltip("Proportional gain for heading (yaw) error.")]
    public float Kp_yaw = 5.0f;
    
    /// <summary>Derivative gain for yaw rate.</summary>
    [Tooltip("Derivative gain for yaw rate.")]
    public float Kd_yaw = 3.0f;

    [Header("Actuation Limits")]
    
    /// <summary>Maximum force applied in the surge direction.</summary>
    public float maxSurgeForce = 400f;
    
    /// <summary>Maximum torque applied in the yaw axis.</summary>
    public float maxYawTorque = 200f;

    private UsvDynamics dynamics;
    private LosGuidance guidance;
    private Rigidbody rb;

    private void Awake()
    {
        dynamics = GetComponent<UsvDynamics>();
        guidance = GetComponent<LosGuidance>();
        rb = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        Vector3 localVel = transform.InverseTransformDirection(rb.linearVelocity);
        Vector3 localAngVel = transform.InverseTransformDirection(rb.angularVelocity);
        
        float u = localVel.z;
        float r = localAngVel.y;
        float currentYaw = NormalizeAngle(transform.eulerAngles.y * Mathf.Deg2Rad);

        float u_d = guidance.DesiredSpeed;
        float psi_d = guidance.DesiredHeading; 

        // Outer loop: Kinematics (Acceleration calculation)
        float error_u = u_d - u;
        float dot_u_cmd = Kp_surge * error_u;

        float error_psi = NormalizeAngle(psi_d - currentYaw);
        float dot_r_cmd = (Kp_yaw * error_psi) - (Kd_yaw * r);

        // Inner loop: Dynamic compensation
        float m11 = dynamics.mass + dynamics.addedMassSurge;
        float m33 = dynamics.inertiaYaw + dynamics.addedInertiaYaw;
        
        float d_u = dynamics.dampingSurge;
        float d_r = dynamics.dampingYaw;

        float tau_u = (m11 * dot_u_cmd) + (d_u * u);
        float tau_r = (m33 * dot_r_cmd) + (d_r * r);
        
        // Underactuated vehicle (sway force is zero)
        float tau_v = 0f; 

        tau_u = Mathf.Clamp(tau_u, -maxSurgeForce, maxSurgeForce);
        tau_r = Mathf.Clamp(tau_r, -maxYawTorque, maxYawTorque);

        Vector3 command = new Vector3(tau_v, tau_r, tau_u);
        dynamics.SetCommand(command);
    }

    /// <summary>
    /// Normalizes an angle to the range [-pi, pi].
    /// </summary>
    /// <param name="angle">The input angle in radians.</param>
    /// <returns>The normalized angle in radians.</returns>
    private float NormalizeAngle(float angle)
    {
        while (angle > Mathf.PI) angle -= 2f * Mathf.PI;
        while (angle <= -Mathf.PI) angle += 2f * Mathf.PI;
        return angle;
    }
}