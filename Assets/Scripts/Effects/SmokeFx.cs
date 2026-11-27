using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// 진짜 연기처럼 보이는 연기: Higgsfield로 만든 연기 영상을 8×8 칸(64장면) 한 장에 묶은 텍스처를 입자가 사는 동안 차례로 넘긴다 (플립북).
//   Resources/Effects/T_SmokePuff_8x8 — 작은 덩어리가 뭉게뭉게 커졌다가 흩어지는 굵은 연기 (큰 연기 기둥, 폭격 건물 잔해·폭탄 구덩이, 불붙은 적, 발전기)
//   Resources/Effects/T_SmokeWisp_8x8 — 한 칸이 밑에서 실처럼 꼬이며 자라 오르는 연기 기둥 하나 (불 드럼통: 입자가 거의 제자리에서 자라며 오른다)
// 덩어리 그림은 위쪽에 빛을 받게 찍혀 있어서 많이 돌리지 않는다 (±15도 안팎, 거꾸로 돌면 아래에서 빛을 받는 것처럼 보인다)
// 장면이 열리면 맵 생성기가 만든 연기 입자(Smoke·BigSmoke, 예전에는 Unity 기본 흐린 동그라미)를 이 재질로 바꾸고, 불 드럼통 위에 가는 연기를 더한다.
// 맵 생성 코드와 장면 파일은 건드리지 않는다. 재질은 Resources/Effects/M_VfxSmoke(셰이더 변형을 빌드에 넣는 원본)를 복사해 쓴다
public static class SmokeFx
{
    public const int Tiles = 8;
    static Material puff, wisp;

    public static Material Puff => puff != null ? puff : puff = Make("Effects/T_SmokePuff_8x8", "VfxSmokePuff");
    public static Material Wisp => wisp != null ? wisp : wisp = Make("Effects/T_SmokeWisp_8x8", "VfxSmokeWisp");

    static Material Make(string texture, string name)
    {
        var template = Resources.Load<Material>("Effects/M_VfxSmoke");
        var m = template != null ? new Material(template) : new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        m.name = name;
        if (template == null)
        {
            Vfx.Configure(m, false, true, true);
            m.EnableKeyword("_FLIPBOOKBLENDING_ON");
            m.SetFloat("_FlipbookBlending", 1);
        }
        var tex = Resources.Load<Texture2D>(texture);
        if (tex != null)
        {
            m.SetTexture("_BaseMap", tex);
            m.mainTexture = tex;
        }
        m.SetColor("_BaseColor", Color.white);
        // 벽·바닥 앞 0.25 m에서만 부드럽게 사라지고, 카메라 0.15~0.6 m 안에서만 옅어진다 (바로 앞의 불붙은 적·발전기 연기가 지워지지 않게)
        m.SetVector("_SoftParticleFadeParams", new Vector4(0f, 1f / 0.25f, 0, 0));
        m.SetVector("_CameraFadeParams", new Vector4(0.15f, 1f / 0.45f, 0, 0));
        return m;
    }

    static readonly List<ParticleSystemVertexStream> Streams = new List<ParticleSystemVertexStream>
    {
        ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Normal, ParticleSystemVertexStream.Color,
        ParticleSystemVertexStream.UV, ParticleSystemVertexStream.UV2, ParticleSystemVertexStream.AnimBlend,
    };

    // 입자 하나가 사는 동안 64장면을 from→to 비율 구간만큼 넘기게 한다 (장면 사이는 섞어 부드럽게 넘어간다)
    public static void Apply(ParticleSystem ps, bool thin, float from = 0f, float to = 1f)
    {
        if (ps == null) return;
        var r = ps.GetComponent<ParticleSystemRenderer>();
        if (r == null) return;
        r.sharedMaterial = thin ? Wisp : Puff;
        r.SetActiveVertexStreams(Streams);
        var ts = ps.textureSheetAnimation;
        ts.enabled = true;
        ts.mode = ParticleSystemAnimationMode.Grid;
        ts.numTilesX = Tiles;
        ts.numTilesY = Tiles;
        ts.animation = ParticleSystemAnimationType.WholeSheet;
        ts.timeMode = ParticleSystemAnimationTimeMode.Lifetime;
        ts.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, from, 1, to * 0.999f));
        ts.startFrame = 0;
        ts.cycleCount = 1;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        SceneManager.sceneLoaded += (scene, mode) => Upgrade(scene);
        Upgrade(SceneManager.GetActiveScene());
    }

    // 맵 생성기가 만든 연기 (재질 M_AshParticle = 흐린 동그라미)를 바꾸고, 불 드럼통 위에 연기를 더한다
    static void Upgrade(Scene scene)
    {
        if (!scene.IsValid()) return;
        foreach (var root in scene.GetRootGameObjects())
            foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                var r = ps.GetComponent<ParticleSystemRenderer>();
                var m = r != null ? r.sharedMaterial : null;
                bool ash = m != null && m.name.StartsWith("M_AshParticle");
                if (ps.name == "BigSmoke" && ash) BigSmoke(ps);
                else if (ps.name == "Smoke" && ash) RubbleSmoke(ps, ps.transform.parent != null && ps.transform.parent.name == "Crater");
                else if (ps.name == "Flames" && ps.transform.parent != null && ps.transform.parent.name == "FireBarrel" && ps.transform.parent.Find("BarrelSmoke") == null)
                    BarrelSmoke(ps.transform.parent, ps.transform.localPosition);
                else if (ps.name == "ChimneySmoke") ChimneySmoke(ps);
            }
    }

    // 방공호 발전기 굴뚝 유리 구간 안에서 오르는 연기 (양은 Generator가 정한다).
    // 예전 장면 파일은 방출 모양이 Circle이라 입자가 관 아래 이음새에서 옆으로 퍼져 방에 흰 점으로 떠다녔다 → 관 축(위)으로 곧게 쏜다.
    // 관 지름이 8 cm라 입자를 관 지름쯤으로 두고(부드러운 가장자리가 관 밖으로 나오지 않는다) 덩어리 연기 그림을 쓴다.
    // 관 아래(1.95 m)에서 위 유리 구간 끝(2.92 m)까지 오르는 동안만 보이게, 빠르기를 고르게 하고 그 끝에서 다 사라지게 수명을 맞춘다
    // (그 위 이음새·천장 쪽에서 연기 가장자리가 관 옆으로 비치지 않게)
    static void ChimneySmoke(ParticleSystem ps)
    {
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 0f;
        shape.radius = 0.01f;
        shape.radiusThickness = 1f;
        var main = ps.main;
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 0.54f);
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.85f, 1.95f); // 0.93~1.05 m 오르고 끝난다 (투명도는 80%부터 줄어 유리 끝 2.92 m 언저리에서 거의 0)
        main.gravityModifier = 0f; // 장면 파일의 -0.01(위로 끌림)이 있으면 0.16 m 더 올라 천장 이음새까지 진한 채로 간다
        main.startSize = new ParticleSystem.MinMaxCurve(0.055f, 0.075f);
        main.startRotation = Tilt;
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.85f, 1, 1f));
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.12f), new GradientAlphaKey(1, 0.8f), new GradientAlphaKey(0, 1) });
        col.color = g;
        // 흔들림(noise) 0.025는 초당 4~7 cm씩 옆으로 밀어 2초 사이에 반지름 4 cm 관을 벗어났다. 0.005면 관 축에서 2 cm 안쪽에 머문다
        var noise = ps.noise;
        noise.strength = 0.005f;
        Apply(ps, false);
        ps.Clear();
    }

    // 덩어리 연기를 조금만 기울인다 (라디안)
    public static ParticleSystem.MinMaxCurve Tilt => new ParticleSystem.MinMaxCurve(-0.26f, 0.26f);

    // 큰 연기 기둥: 덩어리가 솟으며 부풀어 흩어진다
    static void BigSmoke(ParticleSystem ps)
    {
        Apply(ps, false, 0.12f, 1f);
        var main = ps.main;
        main.startColor = new Color(0.34f, 0.32f, 0.3f, 0.75f);
        main.startRotation = Tilt;
        var rot = ps.rotationOverLifetime;
        rot.z = new ParticleSystem.MinMaxCurve(-0.03f, 0.03f);
        var size = ps.sizeOverLifetime;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.6f, 1, 2.4f));
        ps.Clear();
        ps.Simulate(8f, true, true);
        ps.Play();
    }

    // 폭격 건물 잔해·폭탄 구덩이에서 오르는 연기 (굵은 연기를 옅게).
    // 맵 생성기의 투명도(최대 0.35)는 흐린 원에 맞춘 값이라, 연기 텍스처의 투명도와 곱해지면 밤에 보이지 않는다 → 투명도 곡선을 새로 준다
    static void RubbleSmoke(ParticleSystem ps, bool crater)
    {
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(new Color(0.85f, 0.85f, 0.87f), 1) },
            new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.65f, 0.6f), new GradientAlphaKey(0, 1) });
        col.color = g;
        // 구덩이는 잔해보다 조금 옅고 밝은 회색 (양은 맵 생성기 값 그대로 두어 세부 묘사 품질을 따른다)
        Apply(ps, false, crater ? 0.25f : 0.2f, 1f);
        var main = ps.main;
        main.startColor = crater ? new Color(0.38f, 0.37f, 0.36f, 0.45f) : new Color(0.33f, 0.32f, 0.31f, 0.55f); // 빛을 받지 않는 재질이라 밤에 떠 보이지 않게 어둡게
        main.startRotation = Tilt;
        ps.Clear();
        ps.Simulate(5f, true, true);
        ps.Play();
    }

    // 불 드럼통 위로 오르는 가는 연기: 연기 기둥 그림 하나가 불꽃 위에서 밑동을 붙인 채 자라 오르며 흩어진다 (몇 개만, 거의 제자리)
    static void BarrelSmoke(Transform barrel, Vector3 flamesLocal)
    {
        var go = new GameObject("BarrelSmoke");
        go.transform.SetParent(barrel, false);
        go.transform.localPosition = flamesLocal + Vector3.up * 0.45f;
        go.transform.localRotation = Quaternion.Euler(-90, 0, 0);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(3.5f, 5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.04f);
        main.startSize = new ParticleSystem.MinMaxCurve(1.8f, 2.4f);
        main.startColor = new Color(0.32f, 0.3f, 0.29f, 0.55f);
        main.startRotation = 0f; // 기둥이 똑바로 서게
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 8;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 6;
        shape.radius = 0.08f;
        var vel = ps.velocityOverLifetime; // 바람에 조금 밀린다
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(0.02f, 0.08f);
        vel.y = new ParticleSystem.MinMaxCurve(0f, 0.03f);
        vel.z = new ParticleSystem.MinMaxCurve(-0.02f, 0.02f);
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.15f), new GradientAlphaKey(0.6f, 0.6f), new GradientAlphaKey(0, 1) });
        col.color = g;
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.9f, 1, 1.15f));
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.pivot = new Vector3(0f, 0.45f, 0f); // 그림의 밑동(칸 아래)이 입자 자리, 곧 불꽃 위에 오게 위로 반 칸 올린다
        Apply(ps, true, 0.1f, 1f);
        GameSettings.RegisterParticles(ps, 0.7f); // 1.4초에 하나쯤, 세부 묘사 품질을 따른다
        ps.Simulate(4f, true, true);
        ps.Play();
    }
}
