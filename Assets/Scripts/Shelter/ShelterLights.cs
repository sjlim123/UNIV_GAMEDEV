using UnityEngine;

// 방공호 조명은 발전기 전력에 따라 밝기가 바뀐다. 전력이 바닥나면 붉은 비상등만 켜진다.
public class ShelterLights : MonoBehaviour
{
    public Light[] lights;
    public Light[] emergencyLights;

    float[] baseIntensity;
    float seed;

    void Start()
    {
        baseIntensity = new float[lights.Length];
        for (int i = 0; i < lights.Length; i++) baseIntensity[i] = lights[i].intensity;
        seed = Random.value * 50;

        var s = GameManager.Instance.State;
        GameManager.Instance.UI.ShowMessage(s.Turn == 0
            ? "핵폭발 이후, 방공호. 북쪽 복도 끝 출입구로 나가 물자를 모아 200턴을 버텨라."
            : $"방공호로 돌아왔다. (턴 {s.Turn} / {GameState.MaxTurns})", 5f);
    }

    void Update()
    {
        float power = GameManager.Instance.State.Power;
        float factor = power <= 0 ? 0 : 1;
        if (power > 0 && power < 15)
        {
            // 전력이 거의 없으면 불안하게 깜빡인다
            float n = Mathf.PerlinNoise(Time.time * 6, seed);
            factor = n < 0.35f ? 0.15f : Mathf.Lerp(0.5f, 1f, n);
        }
        for (int i = 0; i < lights.Length; i++)
        {
            float flicker = 0.95f + 0.05f * Mathf.PerlinNoise(Time.time * 3, seed + i);
            lights[i].intensity = baseIntensity[i] * factor * flicker;
            lights[i].enabled = factor > 0.01f;
        }
        foreach (var e in emergencyLights) e.enabled = power <= 0;
    }
}
