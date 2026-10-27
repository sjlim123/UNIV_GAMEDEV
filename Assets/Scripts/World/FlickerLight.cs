using UnityEngine;

// 불꽃이나 고장 난 전등처럼 깜빡이는 조명
[RequireComponent(typeof(Light))]
public class FlickerLight : MonoBehaviour
{
    public float baseIntensity = 1f;
    public float flickerAmount = 0.3f;
    public float speed = 8f;
    public bool outages; // 가끔 완전히 꺼짐 (고장 난 전등)

    Light lightSource;
    float seed, outageTimer, nextOutage;

    void Awake()
    {
        lightSource = GetComponent<Light>();
        seed = Random.value * 100f;
        nextOutage = Random.Range(2f, 8f);
    }

    void Update()
    {
        float n = Mathf.PerlinNoise(Time.time * speed, seed);
        float intensity = baseIntensity * (1 - flickerAmount + flickerAmount * 2 * n);
        if (outages)
        {
            nextOutage -= Time.deltaTime;
            if (nextOutage <= 0)
            {
                outageTimer = Random.Range(0.05f, 0.6f);
                nextOutage = Random.Range(1.5f, 9f);
            }
            if (outageTimer > 0)
            {
                outageTimer -= Time.deltaTime;
                intensity *= 0.05f;
            }
        }
        lightSource.intensity = intensity;
    }
}
