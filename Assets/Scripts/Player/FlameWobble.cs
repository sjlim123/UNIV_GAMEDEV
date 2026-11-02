using UnityEngine;

// 라이터 불꽃: 위아래로 늘었다 줄었다 하며 좌우로 살짝 흔들린다
public class FlameWobble : MonoBehaviour
{
    float seed;

    void Awake() => seed = Random.value * 10f;

    void Update()
    {
        float t = Time.time * 11f + seed;
        float n = Mathf.PerlinNoise(t, seed);
        transform.localScale = new Vector3(1f - n * 0.15f, 0.8f + n * 0.45f, 1f - n * 0.15f);
        transform.localRotation = Quaternion.Euler(Mathf.Sin(t * 0.7f) * 6f, 0, Mathf.Cos(t * 0.9f) * 6f);
    }
}
