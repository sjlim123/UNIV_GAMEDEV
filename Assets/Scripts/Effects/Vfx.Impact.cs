using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// 근접 공격이 벽·물건·바닥에 맞았을 때의 효과 (Vfx.cs의 먼지·파편·불꽃 시스템을 같이 쓴다).
// 재질(ImpactSurface.Classify)마다 겹을 다르게 쌓는다:
//   콘크리트·벽돌: 면에서 튀어나오는 짧은 가루 + 천천히 퍼지는 먼지 구름 + 벽을 타고 흘러내리는 가루 + 튀는 돌 조각과 잔모래 + 움푹 팬 자국
//   나무: 톱밥 + 가늘고 긴 나무 가시 + 파인 홈 자국,  쇠: 칠·녹 조각 + (쇠 무기면) 불꽃·섬광·긁힌 자국, (나무 방망이면) 우그러진 자국
//   유리: 반짝이는 얇은 조각 + 금 간 자국,  흙: 흙먼지 + 흙덩이,  천·모래주머니: 모래 먼지와 모래알,  시체: 피,  웅덩이: 물보라
// 자국은 벽에 붙는 납작한 입자라 40초 뒤 옅어져 사라지고, 종류마다 최대 40개까지만 남는다 (오래된 것부터 지워진다).
public static partial class Vfx
{
    public enum ImpactKind { Concrete, Brick, Wood, Metal, Glass, Dirt, Soft, Flesh, Water }

    static ParticleSystem chipsBrick, chipsGlass, chipsDirt, markCrater, markGouge, markScratch, markCrack;

    static readonly Color ConcreteDust = new Color(0.74f, 0.72f, 0.68f);
    static readonly Color BrickDust = new Color(0.66f, 0.42f, 0.32f);
    static readonly Color SawDust = new Color(0.8f, 0.64f, 0.42f);
    static readonly Color RustDust = new Color(0.55f, 0.34f, 0.2f);
    static readonly Color EarthDust = new Color(0.5f, 0.42f, 0.32f);
    static readonly Color SandDust = new Color(0.72f, 0.66f, 0.54f);

    const float MarkLife = 40f;
    static Collider markSurface;
    // 맞은 가루는 벽과 같은 빛을 받는데 LightAt 어림이 벽보다 밝게 나와 솜뭉치처럼 떠 보여서 조금 어둡게 누른다
    const float ImpactDustDim = 0.6f;

    // point: 맞은 자리, normal: 그 면이 보는 쪽, incoming: 무기가 들어온 방향 (카메라 앞쪽),
    // along: 휘두른 방향 (긁힌 자국·홈이 이 방향으로 길다), strength: 맨손 0.35 ~ 쇠 방망이 1.15,
    // hardTool: 쇠붙이 무기(단검·쇠 방망이·못 방망이)면 쇠에서 불꽃이 튀고 돌에서도 몇 개 튄다, mark: 자국을 남길지 (움직이는 문·물건이면 끈다),
    // surface: 맞은 콜라이더 (자국이 모서리 밖 허공으로 삐져나가지 않는지 이것으로 확인한다. null이면 자국을 남기지 않는다)
    public static void Impact(ImpactKind kind, Vector3 point, Vector3 normal, Vector3 incoming, Vector3 along, float strength, bool hardTool, bool mark, Collider surface = null)
    {
        Init();
        ImpactInit();
        var n = normal.sqrMagnitude > 0.0001f ? normal.normalized : -incoming.normalized;
        var reflect = Vector3.Reflect(incoming.normalized, n);
        // 튀어나가는 쪽: 대부분 면 바깥, 조금은 반사 방향, 살짝 위로
        var eject = (n * 0.8f + reflect * 0.35f + Vector3.up * 0.12f).normalized;
        var p = point + n * 0.03f; // 부드러운 입자 가장자리가 벽에 먹히지 않게 살짝 띄운다
        float s = Mathf.Max(0.1f, strength);
        bool light = s < 0.5f; // 맨손: 가루 조금, 조각·자국 없음
        markSurface = mark ? surface : null;
        if (markSurface == null) mark = false;
        switch (kind)
        {
            case ImpactKind.Concrete:
            case ImpactKind.Brick:
            {
                bool brick = kind == ImpactKind.Brick;
                var dustTint = brick ? BrickDust : ConcreteDust;
                Burst(p, eject, n, dustTint, Count((light ? 4 : 11) * s), light ? 0.7f : 1f);
                Cloud(p, n, dustTint, Count((light ? 2 : 8) * s), s);
                if (light) break;
                Trickle(point + n * 0.02f, n, dustTint, Count(5 * s));
                var sys = brick ? chipsBrick : chips;
                int bits = Count(9 * s);
                for (int i = 0; i < bits; i++)
                    ChipIn(sys, point + n * 0.02f + Random.insideUnitSphere * 0.02f, Kick(eject, n, 0.8f, 2.6f, 0.55f) * s + Vector3.up * Random.Range(0.1f, 1.1f),
                        Random.Range(0.004f, 0.01f), new Vector3(1.2f, 0.55f, 1f));
                int grit = Count(10 * s);
                for (int i = 0; i < grit; i++)
                    ChipIn(sys, point + n * 0.02f, Kick(eject, n, 2.5f, 4.5f, 0.7f), Random.Range(0.002f, 0.004f), Vector3.one);
                if (hardTool) SparkBurst(point + n * 0.02f, eject, 3, 1.5f, 3.5f, 0.7f, 0.55f);
                if (mark) Mark(markCrater, point, n, along, Random.Range(0.12f, 0.17f) * Mathf.Sqrt(s), 1f, (brick ? new Color(0.72f, 0.5f, 0.42f) : new Color(0.78f, 0.77f, 0.74f)));
                break;
            }
            case ImpactKind.Wood:
            {
                Burst(p, eject, n, SawDust, Count((light ? 3 : 7) * s), 0.75f);
                Cloud(p, n, SawDust, Count((light ? 1 : 3) * s), s * 0.7f);
                if (light) break;
                int splinters = Count((hardTool ? 9 : 7) * s);
                for (int i = 0; i < splinters; i++)
                    ChipIn(chipsWood, point + n * 0.02f + along * Random.Range(-0.03f, 0.03f), Kick(eject, n, 0.7f, 2.1f, 0.6f) * s + along * Random.Range(-0.6f, 0.6f) + Vector3.up * Random.Range(0.1f, 0.8f),
                        Random.Range(0.008f, 0.02f), new Vector3(0.45f, 0.3f, 2.8f));
                if (mark) Mark(markGouge, point, n, along, Random.Range(0.15f, 0.21f) * Mathf.Sqrt(s), hardTool ? 0.3f : 0.4f, new Color(0.62f, 0.46f, 0.3f));
                break;
            }
            case ImpactKind.Metal:
            {
                Burst(p, eject, n, RustDust, Count((light ? 2 : 4) * s), 0.5f);
                if (light) break;
                int flakes = Count(6 * s);
                for (int i = 0; i < flakes; i++)
                    ChipIn(chipsRust, point + n * 0.02f, Kick(eject, n, 0.4f, 1.6f, 0.7f) + Vector3.up * Random.Range(0f, 0.6f), Random.Range(0.006f, 0.015f), new Vector3(1.6f, 0.18f, 1.3f));
                if (hardTool)
                {
                    SparkBurst(point + n * 0.015f, (reflect + n * 0.6f).normalized, Mathf.RoundToInt(14 * s), 2.2f, 5.5f, 0.6f, 0.75f);
                    Flash(point + n * 0.04f, 0.16f * s, 0.06f, new Color(1f, 0.85f, 0.6f));
                    Pulse(point + n * 0.15f, new Color(1f, 0.72f, 0.42f), 1.8f * s, 1.5f, 0.07f, false);
                    if (mark) Mark(markScratch, point, n, along, Random.Range(0.16f, 0.22f) * Mathf.Sqrt(s), 0.32f, new Color(1f, 0.98f, 0.95f));
                }
                else if (mark) Mark(markCrater, point, n, along, Random.Range(0.1f, 0.13f) * Mathf.Sqrt(s), 1f, new Color(0.45f, 0.42f, 0.4f), 0.6f);
                break;
            }
            case ImpactKind.Glass:
            {
                Burst(p, eject, n, new Color(0.85f, 0.88f, 0.9f), Count(light ? 1 : 3), 0.35f);
                if (light) break;
                int shards = Count(10 * s);
                for (int i = 0; i < shards; i++)
                    ChipIn(chipsGlass, point + n * 0.015f + Random.insideUnitSphere * 0.02f, Kick(eject, n, 0.6f, 2.4f, 0.65f) * s + Vector3.down * Random.Range(0f, 0.6f),
                        Random.Range(0.008f, 0.022f), new Vector3(1.4f, 0.1f, 1f));
                Flash(point + n * 0.02f, 0.07f, 0.05f, new Color(0.8f, 0.9f, 1f, 0.7f));
                if (mark) Mark(markCrack, point, n, along, Random.Range(0.2f, 0.3f) * Mathf.Sqrt(s), 1f, new Color(0.95f, 0.98f, 1f));
                break;
            }
            case ImpactKind.Dirt:
            {
                Burst(p, eject, n, EarthDust, Count((light ? 4 : 10) * s), 1.1f);
                Cloud(p, n, EarthDust, Count((light ? 2 : 6) * s), s * 1.2f);
                if (light) break;
                int clods = Count(8 * s);
                for (int i = 0; i < clods; i++)
                    ChipIn(chipsDirt, point + n * 0.02f, Kick(eject, n, 0.6f, 2f, 0.6f) * s + Vector3.up * Random.Range(0.6f, 1.8f), Random.Range(0.008f, 0.024f), Vector3.one);
                break;
            }
            case ImpactKind.Soft:
            {
                Cloud(p, n, SandDust, Count((light ? 2 : 6) * s), s * 0.8f);
                if (light) break;
                int grains = Count(6 * s);
                for (int i = 0; i < grains; i++)
                    ChipIn(chipsDirt, point + n * 0.02f + Random.insideUnitSphere * 0.03f, n * Random.Range(0.1f, 0.5f) + Vector3.down * Random.Range(0.1f, 0.4f), Random.Range(0.003f, 0.006f), Vector3.one);
                break;
            }
            case ImpactKind.Flesh:
                HitEffects.Blood(point, n, Mathf.RoundToInt((light ? 4 : 12) * s));
                break;
            case ImpactKind.Water:
                Splash(point, Mathf.Clamp01(0.45f + 0.4f * s), incoming);
                break;
        }
    }

    // 면에서 확 튀어나오는 짧고 진한 가루 (맞은 순간의 "팍")
    static void Burst(Vector3 p, Vector3 eject, Vector3 n, Color tint, int count, float scale)
    {
        for (int i = 0; i < count; i++)
            Puff(p + Random.insideUnitSphere * 0.015f, (eject + Random.insideUnitSphere * 0.5f).normalized * Random.Range(0.7f, 2.2f) * scale + n * 0.2f,
                Random.Range(0.35f, 0.75f), Random.Range(0.05f, 0.11f) * scale, tint * ImpactDustDim, Random.Range(0.6f, 0.85f));
    }

    // 천천히 부풀며 떠도는 옅은 먼지 구름 (맞은 뒤 1~3초 남는다)
    static void Cloud(Vector3 p, Vector3 n, Color tint, int count, float s)
    {
        for (int i = 0; i < count; i++)
            Puff(p + n * Random.Range(0.02f, 0.08f) + Random.insideUnitSphere * 0.04f, n * Random.Range(0.15f, 0.55f) * s + Random.insideUnitSphere * 0.12f + Vector3.up * 0.06f,
                Random.Range(1.4f, 2.8f), Random.Range(0.14f, 0.3f) * Mathf.Lerp(0.8f, 1.1f, Mathf.Clamp01(s)), tint * ImpactDustDim, Random.Range(0.25f, 0.42f));
    }

    // 벽을 타고 흘러내리는 가는 가루 (바닥이나 천장이면 그냥 아래로 떨어진다)
    static void Trickle(Vector3 p, Vector3 n, Color tint, int count)
    {
        var down = Vector3.ProjectOnPlane(Vector3.down, n);
        if (down.sqrMagnitude < 0.05f) down = Vector3.down; else down.Normalize();
        for (int i = 0; i < count; i++)
        {
            var side = Vector3.Cross(n, down) * Random.Range(-0.04f, 0.04f);
            Puff(p + side + down * Random.Range(0f, 0.05f), down * Random.Range(0.12f, 0.4f) + n * 0.04f,
                Random.Range(1.1f, 2f), Random.Range(0.035f, 0.075f), tint * ImpactDustDim, Random.Range(0.45f, 0.65f));
        }
    }

    // 조각 하나의 속도: 대체로 eject 쪽, spread만큼 흩어지고, 면 안쪽으로는 가지 않는다
    static Vector3 Kick(Vector3 eject, Vector3 n, float speedMin, float speedMax, float spread)
    {
        var d = (eject + Random.insideUnitSphere * spread).normalized;
        if (Vector3.Dot(d, n) < 0.15f) d = (d + n * (0.15f - Vector3.Dot(d, n) + 0.1f)).normalized;
        return d * Random.Range(speedMin, speedMax);
    }

    // 벽에 붙는 자국 하나. aspect: 세로/가로 비 (홈·긁힘은 가늘고 길다), tint: 자국의 색 (빛을 받는 재질이라 조명은 따로 곱하지 않는다).
    // 네 귀퉁이가 맞은 면 위에 있어야 남긴다 (모서리 근처면 반으로 줄여 한 번 더 보고, 그래도 삐져나가면 남기지 않는다)
    static void Mark(ParticleSystem system, Vector3 point, Vector3 n, Vector3 along, float size, float aspect, Color tint, float alpha = 1f)
    {
        var a = Vector3.ProjectOnPlane(along, n);
        if (a.sqrMagnitude < 0.01f) a = Vector3.ProjectOnPlane(Random.onUnitSphere, n);
        a.Normalize();
        // 긴 자국은 휘두른 방향을 따르되 조금씩 비틀고, 둥근 자국은 아무렇게나 돌린다
        float twist = aspect < 0.9f ? Random.Range(-12f, 12f) : Random.Range(0f, 360f);
        a = Quaternion.AngleAxis(twist, n) * a;
        var b = Vector3.Cross(n, a);
        if (!FitsOnSurface(point, n, a * (size * 0.4f), b * (size * aspect * 0.4f)))
        {
            size *= 0.5f;
            if (!FitsOnSurface(point, n, a * (size * 0.4f), b * (size * aspect * 0.4f))) return;
        }
        // 사각형 메시는 -Z 쪽이 앞면이라 -n을 보게 돌린다 (앞면과 법선이 벽 바깥을 본다). 가로축이 a를 따른다
        var rot = Quaternion.LookRotation(-n, Vector3.Cross(-n, a));
        var e = new ParticleSystem.EmitParams
        {
            position = point + n * 0.005f,
            velocity = Vector3.zero,
            startLifetime = MarkLife,
            startSize3D = new Vector3(size, size * aspect, 1f),
            rotation3D = rot.eulerAngles,
            startColor = new Color(tint.r, tint.g, tint.b, alpha)
        };
        system.Emit(e, 1);
    }

    static bool FitsOnSurface(Vector3 point, Vector3 n, Vector3 ha, Vector3 hb)
    {
        if (markSurface == null) return false;
        for (int i = 0; i < 4; i++)
        {
            var corner = point + ((i & 1) == 0 ? ha : -ha) + ((i & 2) == 0 ? hb : -hb);
            if (!markSurface.Raycast(new Ray(corner + n * 0.04f, -n), out var h, 0.09f) || Vector3.Dot(h.normal, n) < 0.9f) return false;
        }
        return true;
    }

    static void ImpactInit()
    {
        if (chipsBrick != null) return;
        chipsBrick = ChipSystem("VfxChipsBrick", RuntimeVisuals.Mat(new Color(0.5f, 0.25f, 0.17f), 0.15f), 300);
        chipsGlass = ChipSystem("VfxChipsGlass", RuntimeVisuals.Mat(new Color(0.72f, 0.8f, 0.82f), 0.95f, 0.1f), 200);
        chipsDirt = ChipSystem("VfxChipsDirt", RuntimeVisuals.Mat(new Color(0.3f, 0.24f, 0.17f), 0.05f), 300);
        markCrater = MarkSystem("VfxMarkCrater", MarkTexture(0));
        markGouge = MarkSystem("VfxMarkGouge", MarkTexture(1));
        markScratch = MarkSystem("VfxMarkScratch", MarkTexture(2));
        markCrack = MarkSystem("VfxMarkCrack", MarkTexture(3));
        // 자국 시스템은 씬이 바뀌어도 남으므로, 다른 씬의 같은 자리에 자국이 뜨지 않게 지운다
        SceneManager.activeSceneChanged += (from, to) =>
        {
            foreach (var m in new[] { markCrater, markGouge, markScratch, markCrack })
                if (m != null) m.Clear();
        };
    }

    // 자국 입자: 벽에 납작하게 붙는 사각형 (메시 입자라 방향을 직접 준다). 벽과 같이 그 자리 조명을 매 프레임 받는다
    // (빛을 색에 구워 두면 손전등을 끄거나 자리를 떠난 뒤에도 40초 동안 어둠 속에서 하얗게 남았다). 부드러운 가장자리·카메라 페이드는 끈다
    static ParticleSystem MarkSystem(string name, Texture2D tex)
    {
        // 원본(M_VfxMark)은 편집기가 투명·반사광 보존(미리 곱한 알파, _ALPHAPREMULTIPLY_ON + SrcBlend One)으로 맞춰 둔 것을 그대로 쓴다.
        // 원본이 없을 때만 새로 만든다 (그때는 보통 알파 섞기: 키워드와 섞기 값이 어긋나면 가장자리가 두 번 옅어진다)
        var template = Resources.Load<Material>("Effects/M_VfxMark");
        Material mat;
        if (template != null) mat = new Material(template);
        else
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Lit"));
            Configure(mat, false, false, false);
            mat.SetFloat("_BlendModePreserveSpecular", 0);
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        }
        mat.name = name + "_mat";
        mat.SetTexture("_BaseMap", tex);
        mat.mainTexture = tex;
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_Smoothness", 0.1f);
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent - 10; // 먼지보다 먼저 (벽 바로 위)
        var ps = System(name, mat, 40);
        var main = ps.main;
        main.startRotation3D = true;
        main.ringBufferMode = ParticleSystemRingBufferMode.PauseUntilReplaced; // 수명이 다하면 투명하게 멈춰 있다가, 꽉 차면 가장 오래된 것부터 새 자국으로 바뀐다
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 0.85f), new GradientAlphaKey(0, 1) });
        col.color = g;
        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Mesh;
        r.mesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        r.alignment = ParticleSystemRenderSpace.World;
        r.maxParticleSize = 1f;
        return ps;
    }

    // 자국 그림 (흰색이 밝은 부분, 검은색이 어두운 부분, 알파가 모양). 0 팬 자국, 1 파인 홈, 2 긁힌 줄, 3 금 간 유리
    static Texture2D MarkTexture(int kind)
    {
        const int S = 128;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "VfxMark" + kind };
        var px = new Color[S * S];
        var rng = new global::System.Random(911 + kind);
        float R() => (float)rng.NextDouble();
        // 금·긁힘 선: 가운데(또는 한쪽 끝)에서 뻗는 꺾인 선분들
        var segs = new List<Vector4>();
        if (kind == 3)
            for (int k = 0; k < 11; k++)
            {
                float ang = k / 11f * Mathf.PI * 2 + R() * 0.4f, len = 0.45f + R() * 0.5f;
                var a = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * 0.05f;
                int steps = 4;
                for (int j = 0; j < steps; j++)
                {
                    ang += (R() - 0.5f) * 0.6f;
                    var b = a + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * (len / steps);
                    segs.Add(new Vector4(a.x, a.y, b.x, b.y));
                    a = b;
                }
            }
        if (kind == 0) // 팬 자국 둘레로 뻗는 가는 금
            for (int k = 0; k < 6; k++)
            {
                float ang = k / 6f * Mathf.PI * 2 + R() * 0.8f, len = 0.25f + R() * 0.35f;
                var a = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * 0.38f;
                for (int j = 0; j < 3; j++)
                {
                    ang += (R() - 0.5f) * 0.9f;
                    var b = a + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * (len / 3);
                    segs.Add(new Vector4(a.x, a.y, b.x, b.y));
                    a = b;
                }
            }
        if (kind == 2)
            for (int k = 0; k < 5; k++)
            {
                float y = (R() - 0.5f) * 0.55f, bend = (R() - 0.5f) * 0.25f;
                float x0 = -0.9f + R() * 0.3f, x1 = 0.6f + R() * 0.3f;
                var a = new Vector2(x0, y);
                for (int j = 1; j <= 6; j++)
                {
                    float x = Mathf.Lerp(x0, x1, j / 6f);
                    var b = new Vector2(x, y + bend * (x - x0) * (x - x0) * 0.4f);
                    segs.Add(new Vector4(a.x, a.y, b.x, b.y));
                    a = b;
                }
            }
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float u = (x + 0.5f) / S * 2 - 1, v = (y + 0.5f) / S * 2 - 1;
                float r = Mathf.Sqrt(u * u + v * v);
                float pu = u + 7.3f, pv = v + 5.9f; // Perlin은 음수 좌표에서 무늬가 거울처럼 되풀이되어 양수로 옮긴다
                float noise = Mathf.PerlinNoise(pu * 4.3f + 11.1f * kind, pv * 4.3f + 3.7f) * 0.65f + Mathf.PerlinNoise(pu * 13f + 5.5f, pv * 13f + 9.2f) * 0.35f;
                float b = 0.2f, a = 0f;
                switch (kind)
                {
                    case 0: // 팬 자국: 울퉁불퉁하게 떨어져 나간 밝은 속살(작은 구멍이 숭숭) + 가운데가 조금 더 어둡게 팬 곳 + 둘레로 뻗는 가는 금 + 튄 작은 조각 자국
                    {
                        float th = Mathf.Atan2(v, u);
                        float edge = 0.5f + (noise - 0.5f) * 0.5f + 0.1f * Mathf.Sin(3f * th + 1.7f) + 0.07f * Mathf.Sin(7f * th + 0.4f);
                        float patch = 1f - Step(edge - 0.08f, edge + 0.03f, r);
                        float pit = Mathf.PerlinNoise(pu * 18.3f + 4.4f, pv * 18.3f + 8.8f);
                        float patchB = Mathf.Lerp(0.78f, 0.3f, Mathf.Clamp01((pit - 0.58f) * 3f)) * (0.9f + noise * 0.2f);
                        patchB *= Mathf.Lerp(0.55f, 1f, Step(0f, edge * 0.6f, r));
                        float cd = SegDist(segs, u, v) / 0.018f;
                        float crack = Mathf.Exp(-cd * cd) * (1f - patch) * Mathf.Clamp01((1f - r) * 3f);
                        float specks = Mathf.Clamp01((Mathf.PerlinNoise(pu * 26.7f + 1.3f, pv * 26.7f + 7.1f) - 0.7f) * 6f) * Mathf.Clamp01(1.1f - r) * (1f - patch);
                        b = Mathf.Lerp(patchB, 0.12f, Mathf.Clamp01(crack + specks) * (1f - patch));
                        a = Mathf.Clamp01(patch * 0.8f + crack * 0.8f + specks * 0.5f) * Mathf.Clamp01((1f - r) * 5f);
                        break;
                    }
                    case 1: // 파인 홈: 가로로 긴 어두운 골 + 결을 따라 일어난 밝은 생나무 가장자리
                    {
                        float fiber = Mathf.PerlinNoise(pu * 2.5f + 3f, pv * 22.3f + 1f);
                        float halfW = (0.42f + (fiber - 0.5f) * 0.25f) * Mathf.Sqrt(Mathf.Clamp01(1f - u * u * 0.95f));
                        float d = Mathf.Abs(v) / Mathf.Max(0.01f, halfW);
                        float groove = 1f - Step(0.55f, 1f, d);
                        float lip = Mathf.Exp(-Mathf.Pow((d - 1f) / 0.22f, 2f)) * Mathf.Clamp01(1f - Mathf.Abs(u) * 0.8f);
                        b = Mathf.Lerp(1f, 0.12f + fiber * 0.12f, groove);
                        a = Mathf.Clamp01(groove * 0.85f + lip * 0.7f) * Mathf.Clamp01((1f - Mathf.Abs(u)) * 4f);
                        break;
                    }
                    case 2: // 긁힌 줄: 칠이 벗겨진 밝은 금속 선들 + 둘레의 옅은 그을음
                    case 3: // 금 간 유리: 가운데서 뻗는 흰 금 + 가운데 부서진 하얀 점
                    {
                        float best = SegDist(segs, u, v);
                        float w = kind == 2 ? 0.022f : 0.012f;
                        float line = Mathf.Exp(-best * best / (w * w));
                        b = 1f;
                        if (kind == 2)
                        {
                            float soot = Mathf.Exp(-best * best / 0.006f) * 0.35f;
                            a = Mathf.Clamp01(line + soot) * Mathf.Clamp01((1f - Mathf.Abs(u)) * 5f);
                            b = Mathf.Lerp(0.15f, 1f, line / Mathf.Max(0.001f, line + soot));
                        }
                        else
                        {
                            float core = Mathf.Exp(-r * r / 0.004f);
                            float halo = Mathf.Exp(-r * r / 0.03f) * 0.3f;
                            a = Mathf.Clamp01(line * 0.9f + core + halo) * Mathf.Clamp01((1f - r) * 6f);
                        }
                        break;
                    }
                }
                px[y * S + x] = new Color(b, b, b, a);
            }
        tex.SetPixels(px);
        tex.Apply(true);
        return tex;
    }

    // e0~e1 사이에서 0→1로 부드럽게 오른다 (Mathf.SmoothStep은 보간이라 이것과 다르다)
    static float Step(float e0, float e1, float x)
    {
        float t = Mathf.Clamp01((x - e0) / (e1 - e0));
        return t * t * (3f - 2f * t);
    }

    // 점 (u, v)에서 가장 가까운 선분까지의 거리
    static float SegDist(List<Vector4> segs, float u, float v)
    {
        float best = 9f;
        var q = new Vector2(u, v);
        foreach (var sg in segs)
        {
            var s0 = new Vector2(sg.x, sg.y);
            var ab = new Vector2(sg.z, sg.w) - s0;
            float t = Mathf.Clamp01(Vector2.Dot(q - s0, ab) / Mathf.Max(1e-5f, ab.sqrMagnitude));
            best = Mathf.Min(best, (q - (s0 + ab * t)).magnitude);
        }
        return best;
    }
}
