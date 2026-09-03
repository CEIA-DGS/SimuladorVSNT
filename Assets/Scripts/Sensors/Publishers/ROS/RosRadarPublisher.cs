using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Sensor;

public class RosRadarPublisher : BasePublisher<RadarData>
{
    [Header("ROS Setup")]
    /// <summary>ROS topic the radar scan is published on.</summary>
    public string topicName = "/scan";
    /// <summary>Frame of reference stamped on the published scan.</summary>
    public string frameId = "lidar_link";

    private ROSConnection ros;

    protected override void SetupPublisher()
    {
        ros = ROSConnection.GetOrCreateInstance();
        ros.RegisterPublisher<LaserScanMsg>(topicName);
    }

    protected override void PublishMessage(RadarData data)
    {
        var msg = new LaserScanMsg
        {
            header = new RosMessageTypes.Std.HeaderMsg
            {
                frame_id = frameId,
                stamp = GetTimeStamp()
            },
            
            angle_min = data.AngleMin,
            angle_max = data.AngleMax,
            angle_increment = data.AngleIncrement,
            time_increment = data.TimeIncrement,
            scan_time = data.ScanTime,
            range_min = data.RangeMin,
            range_max = data.RangeMax,
            ranges = data.Ranges,
            intensities = new float[0] 
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