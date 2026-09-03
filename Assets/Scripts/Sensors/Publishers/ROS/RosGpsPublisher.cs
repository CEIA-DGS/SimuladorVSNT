using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Sensor;

public class RosGpsPublisher : BasePublisher<GpsData>
{
    [Header("ROS Setup")]
    /// <summary>ROS topic the GPS fix is published on.</summary>
    public string topicName = "/gps/fix";
    /// <summary>Frame of reference stamped on the published fix.</summary>
    public string frameId = "gps_link";

    private ROSConnection ros;

    protected override void SetupPublisher()
    {
        ros = ROSConnection.GetOrCreateInstance();
        ros.RegisterPublisher<NavSatFixMsg>(topicName);
    }

    protected override void PublishMessage(GpsData data)
    {
        // Se o [0] (East) e o [8] (Up) são zero, significa que não passou pelo modelo de ruído
        byte covType = NavSatFixMsg.COVARIANCE_TYPE_DIAGONAL_KNOWN;
        if (data.PositionCovariance[0] == 0 && data.PositionCovariance[8] == 0)
        {
            covType = NavSatFixMsg.COVARIANCE_TYPE_UNKNOWN;
        }

        var msg = new NavSatFixMsg
        {
            header = new RosMessageTypes.Std.HeaderMsg
            {
                frame_id = frameId,
                stamp = GetTimeStamp()
            },
            status = new NavSatStatusMsg 
            { 
                status = NavSatStatusMsg.STATUS_FIX, 
                service = NavSatStatusMsg.SERVICE_GPS 
            },
            latitude = data.Latitude,
            longitude = data.Longitude,
            altitude = data.Altitude,
            position_covariance = data.PositionCovariance,
            position_covariance_type = covType
        };

        ros.Publish(topicName, msg);
    }

    private RosMessageTypes.BuiltinInterfaces.TimeMsg GetTimeStamp()
    {
        float timeNow = Time.time;
        return new RosMessageTypes.BuiltinInterfaces.TimeMsg
        {
            sec = (int)Mathf.Floor(timeNow),
            nanosec = (uint)Mathf.Floor((timeNow - Mathf.Floor(timeNow)) * 1e9f)
        };
    }
}