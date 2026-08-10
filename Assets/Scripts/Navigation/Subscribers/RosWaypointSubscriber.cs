using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Dt;

/// <summary>
/// Subscribes to a ROS 2 topic to receive geographic waypoints and converts them 
/// into local Unity coordinates for the USV's waypoint manager.
/// </summary>
[RequireComponent(typeof(WaypointManager))]
public class RosWaypointSubscriber : MonoBehaviour
{
    [Header("ROS 2 Configuration")]
    
    /// <summary>The ROS 2 topic name for mission waypoints.</summary>
    public string topicName = "/usv/mission_waypoints";
    
    private ROSConnection ros;
    private WaypointManager waypointManager;

    private void Start()
    {
        waypointManager = GetComponent<WaypointManager>();
        
        ros = ROSConnection.GetOrCreateInstance();
        ros.Subscribe<WaypointArrayMsg>(topicName, ReceiveGpsPath);
    }

    /// <summary>
    /// Callback triggered when a new waypoint array is received from ROS.
    /// Converts GPS coordinates (Lat/Lon) to Unity local workspace coordinates.
    /// </summary>
    /// <param name="pathMsg">The array of geographic waypoints.</param>
    private void ReceiveGpsPath(WaypointArrayMsg pathMsg)
    {
        if (pathMsg.waypoints.Length < 2)
        {
            Debug.LogWarning("[RosWaypointSubscriber] Geographic path too short. Ignoring.");
            return;
        }

        var geoRef = MaritimeScenario.Real.GeoReferenceUTM.Instance;

        if (geoRef == null)
        {
            Debug.LogError("[RosWaypointSubscriber] GeoReferenceUTM not found in the scene! The vehicle cannot convert GPS to meters.");
            return;
        }

        Vector3[] unityWaypoints = new Vector3[pathMsg.waypoints.Length];

        for (int i = 0; i < pathMsg.waypoints.Length; i++)
        {
            double lat = pathMsg.waypoints[i].latitude;
            double lon = pathMsg.waypoints[i].longitude;

            Vector2 localPos = geoRef.GeographicToLocal(lat, lon);
            unityWaypoints[i] = new Vector3(localPos.x, 0f, localPos.y);
        }

        waypointManager.SetPath(unityWaypoints);
    }
}