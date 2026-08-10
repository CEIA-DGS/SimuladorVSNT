using UnityEngine;

/// <summary>
/// Manages the active waypoint path and handles transitions between segments 
/// using a virtual half-plane condition.
/// </summary>
public class WaypointManager : MonoBehaviour
{
    [Header("Transition Settings")]
    
    /// <summary>Distance (m) before P_k+1 where the virtual transition half-plane is positioned.</summary>
    [Tooltip("Distance (m) before P_k+1 where the virtual transition half-plane is positioned.")]
    public float planeOffset = 1.0f; 

    /// <summary>Indicates whether the vehicle is currently following a valid path.</summary>
    public bool IsMissionActive { get; private set; }
    
    /// <summary>The start waypoint of the current path segment (P_k).</summary>
    public Vector3 CurrentPk { get; private set; }
    
    /// <summary>The end waypoint of the current path segment (P_k+1).</summary>
    public Vector3 CurrentPk1 { get; private set; }

    private Vector3[] path;
    private int currentSegmentIndex = 0;

    /// <summary>
    /// Assigns a new path to the manager and starts the mission.
    /// </summary>
    /// <param name="newPath">Array of waypoints in local Unity coordinates.</param>
    public void SetPath(Vector3[] newPath)
    {
        path = newPath;
        currentSegmentIndex = 0;
        
        if (path.Length >= 2)
        {
            UpdateCurrentSegment();
            IsMissionActive = true;
        }
    }

    private void Update()
    {
        if (!IsMissionActive || path == null) return;

        CheckWaypointTransition();
    }

    /// <summary>
    /// Checks if the USV has crossed the virtual half-plane associated with the current destination waypoint.
    /// </summary>
    private void CheckWaypointTransition()
    {
        Vector3 pathVector = CurrentPk1 - CurrentPk;
        Vector3 pathDir = pathVector.normalized;

        // Virtual half-plane condition for waypoint switching
        Vector3 virtualPlanePoint = CurrentPk1 - (pathDir * planeOffset);
        Vector3 vectorToUsv = transform.position - virtualPlanePoint;
        float dotProduct = Vector3.Dot(pathDir, vectorToUsv);

        if (dotProduct > 0)
        {
            AdvanceSegment();
        }
    }

    /// <summary>
    /// Advances the mission to the next path segment or finishes the mission if the last waypoint is reached.
    /// </summary>
    private void AdvanceSegment()
    {
        currentSegmentIndex++;

        if (currentSegmentIndex >= path.Length - 1)
        {
            IsMissionActive = false;
            Debug.Log("[WaypointManager] Mission Finished. End of waypoints reached.");
        }
        else
        {
            UpdateCurrentSegment();
        }
    }

    /// <summary>
    /// Updates the P_k and P_k+1 references to the current segment indices.
    /// </summary>
    private void UpdateCurrentSegment()
    {
        CurrentPk = path[currentSegmentIndex];
        CurrentPk1 = path[currentSegmentIndex + 1];
    }
}