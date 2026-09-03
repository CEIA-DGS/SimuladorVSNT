using UnityEngine;

public abstract class BasePublisher<T> : MonoBehaviour
{
    [Header("Publisher Settings")]
    /// <summary>Publication rate, in hertz.</summary>
    [Tooltip("Frequência de publicação em Hertz (Hz)")]
    public float publishFrequency = 10f;
    
    protected BaseSensor<T> sensor;
    private float timeElapsed = 0f;

    protected virtual void Start()
    {
        sensor = GetComponent<BaseSensor<T>>();
        
        if (sensor == null)
        {
            Debug.LogError($"[{gameObject.name}] BasePublisher não encontrou um BaseSensor compatível no mesmo objeto!");
            return;
        }

        SetupPublisher();
    }

    void FixedUpdate()
    {
        if (sensor == null || publishFrequency <= 0) return;

        timeElapsed += Time.fixedDeltaTime;
        float publishPeriod = 1.0f / publishFrequency;

        if (timeElapsed >= publishPeriod)
        {
            T data = sensor.GetProcessedData();
            PublishMessage(data);
            timeElapsed -= publishPeriod; 
        }
    }

    // A classe filha implementa qual tópico registrar no ROS
    protected abstract void SetupPublisher();

    // A classe filha implementa como converter 'T' para uma mensagem do ROS
    protected abstract void PublishMessage(T data);
}