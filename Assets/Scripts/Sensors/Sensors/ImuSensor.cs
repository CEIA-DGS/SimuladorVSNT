using UnityEngine;

public class ImuSensor : BaseSensor<ImuData>
{
    private Rigidbody parentRb;
    private Vector3 lastVelocity;
    private float lastTime;

    protected override void Awake()
    {
        base.Awake();
        parentRb = GetComponentInParent<Rigidbody>();

        if (parentRb == null)
        {
            Debug.LogWarning($"[ImuSensor] Nenhum Rigidbody em {gameObject.name}. O sensor lerá apenas gravidade.");
        }

        lastTime = Time.fixedTime;
        lastVelocity = Vector3.zero;
    }

    protected override ImuData GenerateRawData()
    {
        ImuData data = new ImuData();
        
        float currentTime = Time.fixedTime;
        float dt = currentTime - lastTime;
        
        if (dt <= 0f) dt = Time.fixedDeltaTime; 

        Vector3 currentVelocity = Vector3.zero;
        Vector3 globalAngularVel = Vector3.zero;

        if (parentRb != null)
        {
            currentVelocity = parentRb.GetPointVelocity(transform.position);
            globalAngularVel = parentRb.angularVelocity;
        }

        // 1. Aceleração Linear 
        Vector3 globalAccel = (currentVelocity - lastVelocity) / dt;

        // 2. Adiciona a Força da Gravidade
        globalAccel -= Physics.gravity;

        // 3. Converte para o referencial local do sensor
        data.LinearAcceleration = transform.InverseTransformDirection(globalAccel);
        data.AngularVelocity = transform.InverseTransformDirection(globalAngularVel);
        data.Orientation = transform.rotation;

        // 4. Salva o estado para a próxima iteração
        lastVelocity = currentVelocity;
        lastTime = currentTime;

        // 5. Inicializa covariâncias
        data.LinearAccelerationCovariance = new double[9];
        data.AngularVelocityCovariance = new double[9];
        data.OrientationCovariance = new double[9];

        return data;
    }
}