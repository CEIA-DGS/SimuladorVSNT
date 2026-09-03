using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using Unity.Robotics.ROSTCPConnector.ROSGeometry;
using RosMessageTypes.Nav;
public class TrueStatePublisher : MonoBehaviour
{
    private Rigidbody bodyAUV;
    ROSConnection ros;
    private Vector3 initialPosition;
    private Quaternion initialRotation;
    /// <summary>ROS topic the ground-truth state is published on.</summary>
    public string topicOdometryName = "/auv/ground_truth";
    /// <summary>Time between publications, in seconds.</summary>
    public float publishMessagePeriod = 1/30.0f;
    /// <summary>Whether the pose is reported in world coordinates.</summary>
    public bool useGlobalPose = false;
    private float timeOdometryElapsed;

    void Start()
    {
        bodyAUV = GetComponent<Rigidbody>();
        ros = ROSConnection.GetOrCreateInstance();
        ros.RegisterPublisher<OdometryMsg>(topicOdometryName);
        initialPosition = bodyAUV.transform.position;
        initialRotation = bodyAUV.transform.rotation;
    }

    void FixedUpdate()
    {
        OdometryPublisherMsg();
    }

    private void OdometryPublisherMsg()
    {
        timeOdometryElapsed += Time.deltaTime;

        if (timeOdometryElapsed > publishMessagePeriod)
        {
            Vector3 positionMeasurement;
            Quaternion orientationMeasurement;

            if (useGlobalPose)
            {
                positionMeasurement = bodyAUV.transform.position;
                orientationMeasurement = bodyAUV.transform.rotation;
            }
            else
            {
                positionMeasurement = initialRotation * (bodyAUV.transform.position - initialPosition);
                orientationMeasurement = Quaternion.Inverse(initialRotation) * bodyAUV.transform.rotation;
            }


            Vector3 linearVelMeasurement = transform.InverseTransformDirection(bodyAUV.linearVelocity);
            Vector3 angularVelMeasurement = transform.InverseTransformDirection(bodyAUV.angularVelocity);

            OdometryMsg AUVOdometry = new OdometryMsg();

            AUVOdometry.header.frame_id = "map";
            AUVOdometry.header.stamp.sec = (int)Mathf.Floor(Time.time);
            AUVOdometry.header.stamp.nanosec = (uint)Mathf.Floor((Time.time - Mathf.Floor(Time.time)) * 1e9f);
            AUVOdometry.child_frame_id = "base_link";

            AUVOdometry.pose.pose.position = positionMeasurement.To<FLU>();
            AUVOdometry.pose.pose.orientation = orientationMeasurement.To<FLU>();
            AUVOdometry.pose.covariance = new double[36];

            AUVOdometry.twist.twist.linear = linearVelMeasurement.To<FLU>();
            AUVOdometry.twist.twist.angular = -angularVelMeasurement.To<FLU>();
            AUVOdometry.twist.covariance = new double[36];

            ros.Publish(topicOdometryName, AUVOdometry);
            timeOdometryElapsed = 0;
        }
    }
}
