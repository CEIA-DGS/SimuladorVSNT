using UnityEngine;

/// <summary>
/// Implements Line-of-Sight (LOS) guidance logic to calculate desired heading 
/// and speed based on the current path segment.
/// </summary>
[RequireComponent(typeof(WaypointManager))]
public class LosGuidance : MonoBehaviour
{
    [Header("LOS Parameters")]
    
    /// <summary>Lookahead distance (Delta). Larger values smooth the curve, smaller values make it more aggressive.</summary>
    [Tooltip("Lookahead distance (Delta). Larger values smooth the curve, smaller values make it more aggressive.")]
    public float lookaheadDistance = 5.0f;
    
    /// <summary>Desired cruise speed (m/s) when following the path.</summary>
    [Tooltip("Desired cruise speed (m/s)")]
    public float cruiseSpeed = 10.0f;

    /// <summary>Calculated reference heading in radians.</summary>
    public float DesiredHeading { get; private set; }
    
    /// <summary>Calculated reference speed in m/s.</summary>
    public float DesiredSpeed { get; private set; } 

    private WaypointManager wpManager;

    private void Awake()
    {
        wpManager = GetComponent<WaypointManager>();
    }

    /// <summary>
    /// Runs on the physics step (not per frame) because the guidance references feed
    /// UsvController, which also runs in FixedUpdate. Computing them per frame would
    /// make the result depend on the frame rate and break run-to-run reproducibility.
    /// </summary>
    private void FixedUpdate()
    {
        if (!wpManager.IsMissionActive)
        {
            DesiredSpeed = 0f;
            return;
        }

        DesiredSpeed = cruiseSpeed;
        CalculateGuidance();
    }

    /// <summary>
    /// Calculates the cross-track error and applies the LOS guidance law to find the desired heading.
    /// </summary>
    private void CalculateGuidance()
    {
        Vector3 pK = wpManager.CurrentPk;
        Vector3 pK1 = wpManager.CurrentPk1;
        Vector3 pos = transform.position;

        // Path Angle (Gamma_p)
        float dx = pK1.x - pK.x;
        float dz = pK1.z - pK.z;
        float gammaP = Mathf.Atan2(dx, dz);

        // Cross-Track Error (y_e)
        float crossTrackError = -(pos.x - pK.x) * Mathf.Cos(gammaP) + (pos.z - pK.z) * Mathf.Sin(gammaP);

        // LOS Orientation Law
        float chiR = Mathf.Atan(crossTrackError / lookaheadDistance);
        DesiredHeading = gammaP + chiR;
        DesiredHeading = NormalizeAngle(DesiredHeading);
    }

    /// <summary>
    /// Normalizes an angle to the range [-pi, pi].
    /// </summary>
    /// <param name="angle">The input angle in radians.</param>
    /// <returns>The normalized angle in radians.</returns>
    private float NormalizeAngle(float angle)
    {
        while (angle > Mathf.PI) angle -= 2 * Mathf.PI;
        while (angle < -Mathf.PI) angle += 2 * Mathf.PI;
        return angle;
    }
}