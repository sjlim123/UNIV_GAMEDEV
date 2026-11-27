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

    // 갓 달린 천장 전등 (Tripo 모델): 불빛은 갓 테두리보다 10 cm 아래에 있어 갓이 위쪽 빛을 막아야 한다.
    // 그림자로 막으면 '높음' 이상에서만 보이므로(낮음·중간은 전구 그림자를 끈다) 빛에 가림판(쿠키)을 씌워 모든 품질에서 같게 막는다.
    // 가림판은 갓 안쪽도 어둡게 하므로, 갓 안쪽까지만 닿는 아주 짧은 보조 빛(ShadeFill)을 더한다 (천장은 0.33 m 위라 닿지 않는다)
    const string ShadeLampName = "Tripo_shelter_ceiling_lamp_01";
    const float ShadeCutDeg = 54.5f;  // 빛에서 본 갓 테두리: 바로 위에서 54.5° 안쪽은 갓에 가린다 (테두리 반지름 0.143 m, 빛보다 0.102 m 위)
    const float ShadeSoftDeg = 10f;   // 그림자 테두리를 부드럽게 (전구 유리 크기만큼 번진다)
    const float ShadeFillIntensity = 1.5f, ShadeFillRange = 0.3f;
    static Cubemap shadeCookie;
    Light[] shadeFill;

    void Start()
    {
        EmissionFix.Ensure(transform); // 방공호 전체(전구·라디오·비상등·발전기·화로)의 발광 키워드 보강
        baseIntensity = new float[lights.Length];
        glass = new Renderer[lights.Length];
        glassOn = new bool[lights.Length];
        shadeFill = new Light[lights.Length];
        for (int i = 0; i < lights.Length; i++)
        {
            baseIntensity[i] = lights[i].intensity;
            var bulb = lights[i].transform.parent;
            var g = bulb != null ? bulb.Find("Glass") : null;
            if (g != null) glass[i] = g.GetComponent<Renderer>();
            glassOn[i] = true;
            if (bulb != null && bulb.Find(ShadeLampName) != null) shadeFill[i] = AddShade(lights[i]);
        }
        litBulb = new MaterialPropertyBlock(); // 비어 있으면 머티리얼 값 그대로
        darkBulb = new MaterialPropertyBlock();
        darkBulb.SetColor("_EmissionColor", Color.black);
        darkBulb.SetColor("_BaseColor", new Color(0.12f, 0.11f, 0.1f));
        emergencyBase = new float[emergencyLights.Length];
        for (int i = 0; i < emergencyLights.Length; i++) emergencyBase[i] = emergencyLights[i].intensity;
        seed = Random.value * 50;

        var s = GameManager.Instance.State;
        GameManager.Instance.UI.ShowMessage(s.Turn <= 1 // 첫날(새 게임)에는 목표를, 그 뒤로는 며칠차인지를 알린다
            ? $"핵폭발 이후, 방공호. 북쪽 복도 끝 출입구로 나가 물자를 모아 {GameState.MaxTurns}일을 버텨라."
            : $"방공호로 돌아왔다. ({s.Turn}일차 / {GameState.MaxTurns}일)" + EnemyPace.ArrivalNote(s.Turn), 5f);
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
            if (shadeFill[i] != null)
            {
                shadeFill[i].enabled = on;
                shadeFill[i].intensity = ShadeFillIntensity * factor * flicker;
            }
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

    // 불빛에 갓 가림판을 씌우고, 갓 안쪽만 비추는 보조 빛을 단다
    static Light AddShade(Light main)
    {
        main.cookie = ShadeCookie();
        var fill = new GameObject("ShadeFill").AddComponent<Light>();
        fill.transform.SetParent(main.transform.parent, false);
        fill.transform.localPosition = main.transform.localPosition;
        fill.type = LightType.Point;
        fill.color = main.color;
        fill.intensity = ShadeFillIntensity;
        fill.range = ShadeFillRange;
        fill.shadows = LightShadows.None;
        return fill;
    }

    // 점광원 가림판(큐브맵): 바로 위에서 ShadeCutDeg 안쪽 방향은 검게, 나머지는 희게. 한 번 만들어 모든 전등이 같이 쓴다
    static Cubemap ShadeCookie()
    {
        if (shadeCookie != null) return shadeCookie;
        const int N = 64;
        shadeCookie = new Cubemap(N, TextureFormat.RGBA32, false) { name = "ShadeCookie", wrapMode = TextureWrapMode.Clamp };
        var px = new Color[N * N];
        for (int f = 0; f < 6; f++)
        {
            var face = (CubemapFace)f;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float u = (x + 0.5f) / N * 2 - 1, v = (y + 0.5f) / N * 2 - 1;
                    Vector3 d;
                    switch (face)
                    {
                        case CubemapFace.PositiveX: d = new Vector3(1, -v, -u); break;
                        case CubemapFace.NegativeX: d = new Vector3(-1, -v, u); break;
                        case CubemapFace.PositiveY: d = new Vector3(u, 1, v); break;
                        case CubemapFace.NegativeY: d = new Vector3(u, -1, -v); break;
                        case CubemapFace.PositiveZ: d = new Vector3(u, -v, 1); break;
                        default: d = new Vector3(-u, -v, -1); break;
                    }
                    float k = Mathf.Clamp01((Vector3.Angle(d, Vector3.up) - ShadeCutDeg + ShadeSoftDeg * 0.5f) / ShadeSoftDeg);
                    px[y * N + x] = new Color(k, k, k, k);
                }
            shadeCookie.SetPixels(px, face);
        }
        shadeCookie.Apply(false, true);
        return shadeCookie;
    }
}
