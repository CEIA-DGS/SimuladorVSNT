using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using Unity.Robotics.ROSTCPConnector.ROSGeometry;
using RosMessageTypes.Sensor;

public class RosImuPublisher : BasePublisher<ImuData>
{
    [Header("ROS Setup")]
    public string topicName = "/imu/data";
    public string frameId = "imu_link";

    private ROSConnection ros;
    private uint sequenceCount = 0;

    protected override void SetupPublisher()
    {
        ros = ROSConnection.GetOrCreateInstance();
        ros.RegisterPublisher<ImuMsg>(topicName);
    }

    protected override void PublishMessage(ImuData data)
    {
        var msg = new ImuMsg
        {
            header = new RosMessageTypes.Std.HeaderMsg
            {
                seq = ++sequenceCount,
                frame_id = frameId,
                stamp = GetTimeStamp()
            },
            
            // Conversões Left-Handed para Right-Handed
            linear_acceleration = data.LinearAcceleration.To<FLU>(),
            angular_velocity = (-data.AngularVelocity).To<FLU>(),
            orientation = data.Orientation.To<FLU>(),

            linear_acceleration_covariance = data.LinearAccelerationCovariance,
            angular_velocity_covariance = data.AngularVelocityCovariance,
            orientation_covariance = data.OrientationCovariance
        };

        ros.Publish(topicName, msg);
    }

    private RosMessageTypes.BuiltinInterfaces.TimeMsg GetTimeStamp()
    {
        float timeNow = Time.time;
        return new RosMessageTypes.BuiltinInterfaces.TimeMsg
        {
            sec = (uint)Mathf.Floor(timeNow),
            nanosec = (uint)Mathf.Floor((timeNow - Mathf.Floor(timeNow)) * 1e9f)
        };
    }
}