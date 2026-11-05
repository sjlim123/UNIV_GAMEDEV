using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// 웅덩이 효과: 씬의 웅덩이(M_Puddle 재질, 납작한 얼룩 메시)를 모아 두고, 플레이어·좀비가 밟으면 그 자리에서 물결 고리가 퍼지며
// (셰이더 BunkerLab/Puddle이 읽는 전역 배열 _PuddleRipples) 물방울이 튄다 (Vfx.Splash). 비가 오면 셰이더에 빗방울 물결을 켠다.
// 씬이 바뀔 때마다 저절로 다시 모은다 (게임 시작 때 하나 만들어 두고 씬을 넘어 남긴다)
public class PuddleFx : MonoBehaviour
{
    const int MaxRipples = 16;
    static PuddleFx instance;
    static readonly int RipplesId = Shader.PropertyToID("_PuddleRipples");
    static readonly int RainId = Shader.PropertyToID("_PuddleRain");

    // 타원으로 본 웅덩이 하나: 가운데와 두 반지름 축 (월드). 얼룩 메시는 반지름 1 안팎의 울퉁불퉁한 원이라 0.77쯤을 안쪽으로 친다
    struct Puddle
    {
        public Vector3 center, axisX, axisZ;
    }

    readonly List<Puddle> puddles = new List<Puddle>();
    readonly Vector4[] ripples = new Vector4[MaxRipples]; // x,z 자리 / y 시작 시각(_Time.y 기준 = 씬 시작부터) / w 세기
    int next;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (instance != null) return;
        var go = new GameObject("PuddleFx");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<PuddleFx>();
        SceneManager.sceneLoaded += (scene, mode) => { if (instance != null) instance.Collect(); };
        instance.Collect();
    }

    void Collect()
    {
        puddles.Clear();
        for (int i = 0; i < MaxRipples; i++) ripples[i] = new Vector4(0, -1000f, 0, 0);
        next = 0;
        foreach (var r in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            var m = r.sharedMaterial;
            if (m == null || !m.name.StartsWith("M_Puddle")) continue;
            var t = r.transform;
            puddles.Add(new Puddle
            {
                center = t.position,
                axisX = t.right * Mathf.Abs(t.lossyScale.x),
                axisZ = t.forward * Mathf.Abs(t.lossyScale.z),
            });
        }
        Shader.SetGlobalVectorArray(RipplesId, ripples);
    }

    // 이 자리(발밑)가 웅덩이 안인가
    public static bool Inside(Vector3 p)
    {
        if (instance == null) return false;
        foreach (var pd in instance.puddles)
        {
            if (Mathf.Abs(p.y - pd.center.y) > 0.6f) continue;
            var d = p - pd.center;
            d.y = 0;
            float ax = pd.axisX.sqrMagnitude, az = pd.axisZ.sqrMagnitude;
            if (ax < 1e-6f || az < 1e-6f) continue;
            float u = Vector3.Dot(d, pd.axisX) / ax, v = Vector3.Dot(d, pd.axisZ) / az;
            if (u * u + v * v <= 0.6f) return true;
        }
        return false;
    }

    // 발을 디뎠다: 웅덩이 안이면 물결과 물방울을 내고 true. sound면 그 자리에서 첨벙 소리도 낸다 (플레이어·좀비는 제 발소리 채널로 내므로 false)
    public static bool Step(Vector3 pos, float strength, bool sound)
    {
        if (!Inside(pos)) return false;
        instance.AddRipple(pos, strength);
        Vfx.Splash(pos, strength);
        if (sound) ProceduralAudio.PlayAt(ProceduralAudio.Splash(), pos, 0.35f + 0.4f * strength, Random.Range(0.9f, 1.1f), 1f, 18f);
        return true;
    }

    void AddRipple(Vector3 pos, float strength)
    {
        ripples[next] = new Vector4(pos.x, Time.timeSinceLevelLoad, pos.z, Mathf.Clamp01(strength));
        next = (next + 1) % MaxRipples;
    }

    void Update()
    {
        Shader.SetGlobalVectorArray(RipplesId, ripples);
        Shader.SetGlobalFloat(RainId, Weather.Instance != null && Weather.Current == WeatherKind.Rain ? 1f : 0f);
    }
}
