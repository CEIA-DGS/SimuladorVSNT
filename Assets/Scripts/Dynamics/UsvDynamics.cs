using UnityEngine;

/// <summary>
/// Handles the 3-DOF (Surge, Sway, Yaw) hydrodynamic modeling of the USV.
/// Integrates forces and overrides the Unity Rigidbody to ensure accurate marine dynamics.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class UsvDynamics : MonoBehaviour
{
    [Header("Mass Matrix (M)")]
    /// <summary>Rigid body mass, in kilograms.</summary>
    public float mass = 30f;
    /// <summary>Yaw moment of inertia, in kg.m2.</summary>
    public float inertiaYaw = 15f;
    /// <summary>Added mass in surge, from the water accelerated along with the hull.</summary>
    public float addedMassSurge = 5f; 
    /// <summary>Added mass in sway. Much larger than in surge, because the hull pushes far more water sideways.</summary>
    public float addedMassSway = 20f; 
    /// <summary>Added moment of inertia in yaw.</summary>
    public float addedInertiaYaw = 10f;

    [Header("Linear Damping Matrix (D)")]
    /// <summary>Linear damping in surge. Sets the top speed reached for a given thrust.</summary>
    public float dampingSurge = 10f;
    /// <summary>Linear damping in sway. Resists sideways drift.</summary>
    public float dampingSway = 40f;
    /// <summary>Linear damping in yaw. Damps the rate of turn.</summary>
    public float dampingYaw = 20f;

    private Rigidbody rb;
    private Vector3 currentTauCommand = Vector3.zero;

    private float m11, m22, m33;
    
    private float last_u = 0f;
    private float last_v = 0f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        
        // Configure Rigidbody to act purely as a collision container
        rb.mass = mass;
        rb.linearDamping = 0f; 
        rb.angularDamping = 0f;
        rb.automaticCenterOfMass = false;
        rb.centerOfMass = Vector3.zero;
        rb.automaticInertiaTensor = false;
        rb.inertiaTensor = Vector3.one;

        m11 = mass + addedMassSurge;
        m22 = mass + addedMassSway;
        m33 = inertiaYaw + addedInertiaYaw;
    }

    /// <summary>
    /// Receives and stores the control forces and torques to be applied in the next physics step.
    /// </summary>
    /// <param name="tau">Vector containing forces and torques (x: sway, y: yaw, z: surge).</param>
    public void SetCommand(Vector3 tau)
    {
        currentTauCommand = tau;
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        Vector3 localVel = transform.InverseTransformDirection(rb.linearVelocity);
        Vector3 localAngVel = transform.InverseTransformDirection(rb.angularVelocity);

        float u_read = localVel.z;
        float v_read = localVel.x;
        float r = localAngVel.y;

        // Compensate for frame rotation over the last timestep
        float angle = r * dt;
        float cos_r = Mathf.Cos(angle);
        float sin_r = Mathf.Sin(angle);

        float u_rot = last_v * sin_r + last_u * cos_r;
        float v_rot = last_v * cos_r - last_u * sin_r;

        // Isolate external physics impulses (e.g., collisions)
        float u_col = u_read - u_rot;
        float v_col = v_read - v_rot;

        float u = last_u + u_col;
        float v = last_v + v_col;

        // Apply deadzone to prevent floating-point drift
        if (Mathf.Abs(v) < 0.005f) v = 0f;
        if (Mathf.Abs(r) < 0.005f) r = 0f;

        float dampingForceSurge = dampingSurge * u;
        float dampingForceSway  = dampingSway * v;
        float dampingTorqueYaw  = dampingYaw * r;

        float sumSurge = currentTauCommand.z - dampingForceSurge;
        float sumSway  = currentTauCommand.x - dampingForceSway;
        float sumYaw   = currentTauCommand.y - dampingTorqueYaw;

        float dot_u = sumSurge / m11;
        float dot_v = sumSway  / m22;
        float dot_r = sumYaw   / m33;

        u += dot_u * dt;
        v += dot_v * dt;
        r += dot_r * dt;

        last_u = u;
        last_v = v;

        // Override Rigidbody state with custom hydrodynamic integration
        rb.linearVelocity = transform.TransformDirection(new Vector3(v, 0, u));
        rb.angularVelocity = transform.TransformDirection(new Vector3(0, r, 0));

        currentTauCommand = Vector3.zero;
    }
}