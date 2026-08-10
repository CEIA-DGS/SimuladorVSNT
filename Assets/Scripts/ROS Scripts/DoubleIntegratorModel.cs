using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using Unity.Robotics.ROSTCPConnector.ROSGeometry;
using RosMessageTypes.Geometry;

[RequireComponent(typeof(Rigidbody))]
public class DoubleIntegratorModel : MonoBehaviour
{
    [Header("ROS Configuration")]
    public string topicAccelName = "/cmd_accel";
    
    [Header("Settings")]
    public bool useLocalFrame = true; 

    private Rigidbody rb;
    private ROSConnection ros;
    private Vector3 targetLinearAccel;
    private Vector3 targetAngularAccel;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        ros = ROSConnection.GetOrCreateInstance();

        // Configura o Rigidbody para um modelo dinâmico ideal
        rb.useGravity = false; 
        rb.linearDamping = 0; 
        rb.angularDamping = 0;

        // Inscrição no tópico de aceleração
        ros.Subscribe<AccelMsg>(topicAccelName, ReceiveAccel);
    }

    private void ReceiveAccel(AccelMsg msg)
    {
        targetLinearAccel = msg.linear.From<FLU>();
        targetAngularAccel = -msg.angular.From<FLU>();
    }

    void FixedUpdate()
    {
        ApplyDoubleIntegratorPhysics();
    }

    private void ApplyDoubleIntegratorPhysics()
    {
        if (useLocalFrame)
        {
            // Aplica aceleração relativa aos eixos locais do objeto
            rb.AddRelativeForce(targetLinearAccel, ForceMode.Acceleration);
            rb.AddRelativeTorque(targetAngularAccel, ForceMode.Acceleration);
        }
        else
        {
            // Aplica aceleração global 
            rb.AddForce(targetLinearAccel, ForceMode.Acceleration);
            rb.AddTorque(targetAngularAccel, ForceMode.Acceleration);
        }
    }
}