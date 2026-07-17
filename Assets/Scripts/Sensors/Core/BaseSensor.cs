using UnityEngine;

// Classe base para os modelos de ruído
public abstract class BaseNoiseModel<T> : MonoBehaviour
{
    public abstract T ApplyNoise(T data);
}

// Classe base para todos os sensores
public abstract class BaseSensor<T> : MonoBehaviour
{
    protected BaseNoiseModel<T> noiseModel;

    protected virtual void Awake()
    {
        noiseModel = GetComponent<BaseNoiseModel<T>>();
    }

    public T GetProcessedData()
    {
        T rawData = GenerateRawData();
        
        if (noiseModel != null)
        {
            return noiseModel.ApplyNoise(rawData);
        }
        
        return rawData;
    }

    protected abstract T GenerateRawData();
}