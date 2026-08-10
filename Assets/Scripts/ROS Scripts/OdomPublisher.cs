using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using Unity.Robotics.ROSTCPConnector.ROSGeometry;
using RosMessageTypes.Nav;
using RosMessageTypes.Geometry;
using RosMessageTypes.Tf2;

public class OdomPublisher : MonoBehaviour
{
    [Header("ROS Connection")]
    public string topicOdometryName = "/ground_truth/odom";
    public string topicTFName = "/tf";
    
    [Header("Frame IDs")]
    public string frameId = "map";
    public string childFrameId = "base_link";
    
    [Header("Publish Options")]
    public bool publishOdometry = true;
    public bool publishTF = true;
    public float publishFrequency = 30.0f;
    public bool useGlobalPose = false;

    private Rigidbody rb;
    private ROSConnection ros;
    private Vector3 initialPosition;
    private Quaternion initialRotation;
    
    private float timeElapsed;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        ros = ROSConnection.GetOrCreateInstance();

        // Registro condicional dos publicadores
        if (publishOdometry)
            ros.RegisterPublisher<OdometryMsg>(topicOdometryName);
        
        if (publishTF)
            ros.RegisterPublisher<TFMessageMsg>(topicTFName);

        initialPosition = transform.position;
        initialRotation = transform.rotation;
    }

    void FixedUpdate()
    {
        float publishPeriod = 1.0f / publishFrequency;
        timeElapsed += Time.fixedDeltaTime;

        if (timeElapsed >= publishPeriod)
        {
            StatePublisherLogic();
            timeElapsed = 0;
        }
    }

    private void StatePublisherLogic()
    {
        // 1. Cálculo de Transformada
        Vector3 pos;
        Quaternion rot;

        if (useGlobalPose)
        {
            pos = transform.position;
            rot = transform.rotation;
        }
        else
        {
            // Calcula relativo ao ponto inicial
            pos = Quaternion.Inverse(initialRotation) * (transform.position - initialPosition);
            rot = Quaternion.Inverse(initialRotation) * transform.rotation;
        }

        // 2. Preparação do Header (Reutilizado para Odom e TF)
        var header = new RosMessageTypes.Std.HeaderMsg
        {
            frame_id = frameId,
            stamp = GetTimeStamp()
        };

        // 3. Publicação de Odometria (Odom)
        if (publishOdometry)
        {
            // Velocidade local (Body Frame)
            Vector3 linVel = transform.InverseTransformDirection(rb.linearVelocity);
            Vector3 angVel = transform.InverseTransformDirection(rb.angularVelocity);

            OdometryMsg odomMsg = new OdometryMsg
            {
                header = header,
                child_frame_id = childFrameId,
                pose = new PoseWithCovarianceMsg
                {
                    pose = new PoseMsg
                    {
                        position = pos.To<FLU>(),
                        orientation = rot.To<FLU>()
                    },
                    covariance = new double[36]
                },
                twist = new TwistWithCovarianceMsg
                {
                    twist = new TwistMsg
                    {
                        linear = linVel.To<FLU>(),
                        angular = (-angVel).To<FLU>() // Convenção de sinal ROS
                    },
                    covariance = new double[36]
                }
            };
            ros.Publish(topicOdometryName, odomMsg);
        }

        // 4. Publicação de Transform (TF)
        if (publishTF)
        {
            TransformStampedMsg tfStamped = new TransformStampedMsg
            {
                header = header,
                child_frame_id = childFrameId,
                transform = new TransformMsg
                {
                    translation = pos.To<FLU>(), // Conversão para Vector3Msg implícita
                    rotation = rot.To<FLU>()    // Conversão para QuaternionMsg implícita
                }
            };

            TFMessageMsg tfMessage = new TFMessageMsg(new TransformStampedMsg[] { tfStamped });
            ros.Publish(topicTFName, tfMessage);
        }
    }

    private RosMessageTypes.BuiltinInterfaces.TimeMsg GetTimeStamp()
    {
        return new RosMessageTypes.BuiltinInterfaces.TimeMsg
        {
            sec = (int)Mathf.Floor(Time.time),
            nanosec = (uint)Mathf.Floor((Time.time - Mathf.Floor(Time.time)) * 1e9f)
        };
    }
}