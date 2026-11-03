using System.Collections.Generic;
using UnityEngine;

// 입체 버섯구름. 발치(지면)를 원점으로 하고 Size가 기둥 높이(m)다.
// Progress 0(폭발 순간) → 1(다 자란 구름): 하얗게 빛나는 불덩이가 솟아오르며 식어 가고, 아래에서 먼지 기둥이 따라 오르고,
// 위에서 갓이 퍼지며 연기 입자가 피어오른다. 땅에는 먼지 치마가 둥글게 번진다.
// 덩어리(구)마다 잡음으로 크기를 흔들어 부글거리게 한다. 재질은 발광 재질 원본(glow)을 복사해 만든다
public class MushroomCloud : MonoBehaviour
{
    public float Size = 200f;
    public float Progress { get; private set; }

    struct Blob
    {
        public Transform T;
        public Vector3 Dir;       // 가운데에서 바깥으로 (갓·치마) 또는 높이 (기둥)
        public float Scale, Seed, Height;
        public MeshRenderer R;
    }

    readonly List<Blob> cap = new List<Blob>(), stem = new List<Blob>(), skirt = new List<Blob>();
    Transform fireball;
    MeshRenderer fireballR;
    Material capMat, stemMat, dustMat, fireMat;
    Light glow;
    ParticleSystem capSmoke, stemSmoke;
    MaterialPropertyBlock block;
    float roll;

    public static MushroomCloud Create(Transform parent, Material glowTemplate, Material smokeParticle, float size)
    {
        var go = new GameObject("MushroomCloud");
        go.transform.SetParent(parent, false);
        var m = go.AddComponent<MushroomCloud>();
        m.Size = size;
        m.Build(glowTemplate, smokeParticle);
        return m;
    }

    void Build(Material glowTemplate, Material smokeParticle)
    {
        block = new MaterialPropertyBlock();
        Material Lit(Color baseColor, float smooth)
        {
            var m = glowTemplate != null ? new Material(glowTemplate) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetTexture("_BaseMap", null);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            m.SetColor("_BaseColor", baseColor);
            m.SetColor("_EmissionColor", Color.black);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", 0f);
            return m;
        }
        fireMat = Lit(new Color(1f, 0.6f, 0.3f), 0.1f);
        capMat = Lit(new Color(0.22f, 0.16f, 0.13f), 0.05f);
        stemMat = Lit(new Color(0.42f, 0.36f, 0.31f), 0.03f);
        dustMat = Lit(new Color(0.5f, 0.43f, 0.36f), 0.02f);

        var rng = new System.Random(7);
        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        fireball = Sphere("Fireball", fireMat, out fireballR);
        // 갓: 가운데 고리 14개 + 위쪽 6개 + 아래쪽 6개
        for (int i = 0; i < 26; i++)
        {
            float ring = i < 14 ? 1f : i < 20 ? 0.6f : 0.75f;
            float a = (i < 14 ? i / 14f : i < 20 ? (i - 14) / 6f : (i - 20) / 6f) * Mathf.PI * 2 + R(-0.15f, 0.15f);
            float y = i < 14 ? R(-0.04f, 0.06f) : i < 20 ? R(0.14f, 0.22f) : R(-0.2f, -0.12f);
            cap.Add(new Blob { T = Sphere("Cap", capMat, out var r), R = r, Dir = new Vector3(Mathf.Cos(a) * ring, y, Mathf.Sin(a) * ring), Scale = R(0.17f, 0.27f) * (i < 14 ? 1f : 0.8f), Seed = R(0f, 100f) });
        }
        // 기둥: 아래에서 위로 11개 (아래는 굵고 흐리게, 위는 갓 쪽으로 넓어진다)
        for (int i = 0; i < 11; i++)
        {
            float h = (i + 0.5f) / 11f;
            stem.Add(new Blob { T = Sphere("Stem", stemMat, out var r), R = r, Height = h, Dir = new Vector3(R(-0.03f, 0.03f), 0, R(-0.03f, 0.03f)), Scale = R(0.09f, 0.13f) + (h < 0.15f ? 0.05f : 0f) + Mathf.Clamp01((h - 0.75f) * 3f) * 0.08f, Seed = R(0f, 100f) });
        }
        // 땅의 먼지 치마: 둥근 고리 14개
        for (int i = 0; i < 14; i++)
        {
            float a = i / 14f * Mathf.PI * 2 + R(-0.1f, 0.1f);
            skirt.Add(new Blob { T = Sphere("Skirt", dustMat, out var r), R = r, Dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)), Scale = R(0.16f, 0.24f), Seed = R(0f, 100f) });
        }

        var lg = new GameObject("Glow");
        lg.transform.SetParent(transform, false);
        glow = lg.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = new Color(1f, 0.75f, 0.5f);
        glow.range = Size * 4f;
        glow.intensity = 0;
        glow.shadows = LightShadows.None;

        capSmoke = Smoke("CapSmoke", smokeParticle, 0.3f, 0.5f, 9f, 2.2f, 7);
        stemSmoke = Smoke("StemSmoke", smokeParticle, 0.12f, 0.22f, 6f, 1.6f, 5);
        SetProgress(0, 0);
    }

    Transform Sphere(string name, Material mat, out MeshRenderer r)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = name;
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(transform, false);
        r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return go.transform;
    }

    // 연기 입자 (크기는 Size 배율, 위로 천천히 오르며 커진다)
    ParticleSystem Smoke(string name, Material mat, float sizeMin, float sizeMax, float life, float speed, float rate)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.7f, life);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed) ;
        main.startSize = new ParticleSystem.MinMaxCurve(sizeMin * Size, sizeMax * Size);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 200;
        main.gravityModifier = -0.02f;
        var em = ps.emission;
        em.rateOverTime = rate;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.2f * Size;
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(new Color(0.5f, 0.3f, 0.2f), 0f), new GradientColorKey(new Color(0.22f, 0.17f, 0.14f), 0.4f), new GradientColorKey(new Color(0.16f, 0.14f, 0.13f), 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.55f, 0.15f), new GradientAlphaKey(0.45f, 0.6f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        var sz = ps.sizeOverLifetime;
        sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.6f, 1, 1.8f));
        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.maxParticleSize = 3f;
        return ps;
    }

    static float Smooth(float a, float b, float x)
    {
        float t = Mathf.Clamp01((x - a) / (b - a));
        return t * t * (3 - 2 * t);
    }

    // p: 0~1 진행, dt: 부글거림용 시간
    public void SetProgress(float p, float dt)
    {
        Progress = Mathf.Clamp01(p);
        roll += dt;
        float S = Size;
        // 불덩이: 0~0.12에서 급히 커지며 0.45까지 솟아오른다. 그 뒤 갓의 속이 된다
        float rise = Smooth(0.02f, 0.5f, p);
        float fy = Mathf.Lerp(0.1f, 0.88f, rise);
        float fr = Mathf.Lerp(0.02f, 0.32f, Smooth(0f, 0.14f, p)) + 0.1f * Smooth(0.3f, 1f, p);
        fireball.localPosition = new Vector3(0, fy * S, 0);
        fireball.localScale = Vector3.one * (fr * 2 * S) * (1 + 0.04f * Mathf.Sin(roll * 2.3f));
        // 색: 하양 → 노랑 → 주황 → 검붉게. 갓이 다 퍼지면 거의 꺼진다
        float heat = 1 - Smooth(0.05f, 0.75f, p);
        Color fire = heat > 0.66f ? Color.Lerp(new Color(1f, 0.55f, 0.2f), new Color(1f, 0.97f, 0.9f), (heat - 0.66f) * 3f)
            : heat > 0.33f ? Color.Lerp(new Color(0.75f, 0.15f, 0.05f), new Color(1f, 0.55f, 0.2f), (heat - 0.33f) * 3f)
            : Color.Lerp(new Color(0.12f, 0.05f, 0.04f), new Color(0.75f, 0.15f, 0.05f), heat * 3f);
        float fireStrength = Mathf.Lerp(0.6f, 14f, heat * heat);
        block.Clear();
        block.SetColor("_EmissionColor", fire * fireStrength);
        block.SetColor("_BaseColor", Color.Lerp(new Color(0.2f, 0.13f, 0.1f), new Color(1f, 0.7f, 0.4f), heat));
        fireballR.SetPropertyBlock(block);

        // 갓: 0.25부터 퍼진다. 속(불덩이 쪽)은 달아오르고 바깥은 연기
        float capGrow = Smooth(0.22f, 1f, p);
        float capR = Mathf.Lerp(0.05f, 0.62f, capGrow);
        float spin = roll * 0.04f;
        foreach (var b in cap)
        {
            float n = 0.85f + 0.3f * Mathf.PerlinNoise(b.Seed, roll * 0.25f);
            var d = Quaternion.Euler(0, spin * Mathf.Rad2Deg, 0) * b.Dir;
            b.T.localPosition = new Vector3(d.x * capR * S, fy * S + d.y * capR * S * 1.4f, d.z * capR * S);
            float sc = b.Scale * capR * 1.9f * n * S;
            b.T.localScale = Vector3.one * sc;
            b.T.localRotation = Quaternion.Euler(b.Seed * 3f, roll * 2f + b.Seed, 0);
            // 안쪽(불덩이 가까운) 면은 불빛을 머금는다
            float inner = Mathf.Clamp01(1.2f - b.Dir.magnitude) * heat;
            block.Clear();
            block.SetColor("_EmissionColor", fire * (inner * 3f + heat * 0.35f));
            block.SetColor("_BaseColor", Color.Lerp(new Color(0.2f, 0.15f, 0.12f), new Color(0.5f, 0.3f, 0.2f), heat * 0.6f));
            b.R.SetPropertyBlock(block);
            b.R.enabled = capGrow > 0.01f;
        }

        // 기둥: 불덩이를 따라 아래에서 위로 쌓인다 (위쪽은 늦게 나타난다)
        float stemTop = fy - 0.08f;
        foreach (var b in stem)
        {
            float h = b.Height * stemTop;
            float show = Smooth(b.Height * 0.5f - 0.05f, b.Height * 0.5f + 0.08f, p);
            float n = 0.8f + 0.4f * Mathf.PerlinNoise(b.Seed, roll * 0.3f);
            float width = Mathf.Lerp(0.6f, 1.3f, Smooth(0.3f, 1f, p));
            b.T.localPosition = new Vector3(b.Dir.x * S, h * S, b.Dir.z * S);
            b.T.localScale = Vector3.one * (b.Scale * 2.2f * width * n * S * show);
            b.T.localRotation = Quaternion.Euler(0, roll * 3f + b.Seed, 0);
            float glowK = Mathf.Clamp01((b.Height - 0.5f) * 2f) * heat;
            block.Clear();
            block.SetColor("_EmissionColor", fire * glowK * 1.2f);
            block.SetColor("_BaseColor", Color.Lerp(new Color(0.42f, 0.36f, 0.31f), new Color(0.6f, 0.4f, 0.3f), glowK));
            b.R.SetPropertyBlock(block);
            b.R.enabled = show > 0.01f;
        }

        // 땅의 먼지 치마: 바깥으로 둥글게 번진다
        float skirtR = Mathf.Lerp(0.05f, 0.9f, Smooth(0.03f, 0.7f, p));
        foreach (var b in skirt)
        {
            float n = 0.8f + 0.4f * Mathf.PerlinNoise(b.Seed, roll * 0.2f);
            b.T.localPosition = new Vector3(b.Dir.x * skirtR * S, 0.02f * S, b.Dir.z * skirtR * S);
            float sc = b.Scale * (0.5f + skirtR) * n * S;
            b.T.localScale = new Vector3(sc, sc * 0.45f, sc);
            block.Clear();
            block.SetColor("_EmissionColor", fire * heat * 0.25f);
            b.R.SetPropertyBlock(block);
            b.R.enabled = p > 0.02f;
        }

        glow.transform.localPosition = fireball.localPosition;
        glow.intensity = Mathf.Lerp(0f, 40000f, heat * heat) * (S / 200f) * (S / 200f);
        glow.color = fire;
        glow.range = S * 5f;

        // 연기 입자: 갓이 퍼질 때부터
        if (capSmoke != null)
        {
            capSmoke.transform.localPosition = new Vector3(0, fy * S, 0);
            var sh = capSmoke.shape;
            sh.radius = Mathf.Max(0.05f, capR * 0.9f) * S;
            bool on = p > 0.2f && p < 0.98f;
            if (on && !capSmoke.isEmitting) capSmoke.Play();
            else if (!on && capSmoke.isEmitting) capSmoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            stemSmoke.transform.localPosition = new Vector3(0, stemTop * 0.5f * S, 0);
            var ss = stemSmoke.shape;
            ss.shapeType = ParticleSystemShapeType.Box;
            ss.scale = new Vector3(0.18f * S, stemTop * S, 0.18f * S);
            bool son = p > 0.1f && p < 0.98f;
            if (son && !stemSmoke.isEmitting) stemSmoke.Play();
            else if (!son && stemSmoke.isEmitting) stemSmoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }
}
