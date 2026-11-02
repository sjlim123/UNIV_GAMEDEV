using UnityEngine;

// 방공호 조명은 발전기 전력에 따라 밝기가 바뀐다. 전력이 바닥나면 붉은 비상등만 어둡게 맥박치듯 켜진다.
public class ShelterLights : MonoBehaviour
{
    public Light[] lights;
    public Light[] emergencyLights;

    float[] baseIntensity, emergencyBase;
    Renderer[] glass;     // 전구 유리 (꺼지면 발광도 끈다)
    bool[] glassOn;
    MaterialPropertyBlock darkBulb, litBulb;
    float seed;

    void Start()
    {
        baseIntensity = new float[lights.Length];
        glass = new Renderer[lights.Length];
        glassOn = new bool[lights.Length];
        for (int i = 0; i < lights.Length; i++)
        {
            baseIntensity[i] = lights[i].intensity;
            var bulb = lights[i].transform.parent;
            var g = bulb != null ? bulb.Find("Glass") : null;
            if (g != null) glass[i] = g.GetComponent<Renderer>();
            glassOn[i] = true;
        }
        litBulb = new MaterialPropertyBlock(); // 비어 있으면 머티리얼 값 그대로
        darkBulb = new MaterialPropertyBlock();
        darkBulb.SetColor("_EmissionColor", Color.black);
        darkBulb.SetColor("_BaseColor", new Color(0.12f, 0.11f, 0.1f));
        emergencyBase = new float[emergencyLights.Length];
        for (int i = 0; i < emergencyLights.Length; i++) emergencyBase[i] = emergencyLights[i].intensity;
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
            bool on = factor > 0.01f;
            lights[i].enabled = on;
            if (glass[i] != null && glassOn[i] != on)
            {
                glassOn[i] = on;
                glass[i].SetPropertyBlock(on ? litBulb : darkBulb);
            }
        }
        // 정전: 비상등이 어둡게 천천히 맥박친다
        for (int i = 0; i < emergencyLights.Length; i++)
        {
            var e = emergencyLights[i];
            e.enabled = power <= 0;
            if (e.enabled) e.intensity = emergencyBase[i] * (0.3f + 0.4f * (0.5f + 0.5f * Mathf.Sin(Time.time * 2.2f + i)));
        }
    }
}
