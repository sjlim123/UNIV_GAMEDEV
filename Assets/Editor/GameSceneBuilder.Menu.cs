using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// GameSceneBuilder의 메인 메뉴 배경 씬 부분
public static partial class GameSceneBuilder
{
    // ================= 메인 메뉴 배경 =================

    // 메인 화면: 방공호 입구 터널 안에서 비 오는 바깥 거리를 내다본다.
    // 메뉴가 왼쪽에 있으므로 왼쪽은 어두운 터널 벽, 오른쪽에 입구가 비스듬히 열린다.
    // 터널 입구는 z=0, 터널은 -z로 14m 들어간다. 바깥 거리는 +z로 이어진다
    const float TunnelRadius = 2.2f, TunnelWallH = 1.15f, TunnelLength = 14f;

    static void BuildMenuScene(GameObject zombiePrefab)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        BeginMeshes(GenDir + "/MenuMeshes.asset");
        rng = new System.Random(5);
        var root = new GameObject("MenuBackdrop").transform;

        // ---------- 바깥 거리 (비에 젖은 아스팔트) ----------
        var street = Group("Street", root, new Vector3(0, 0, 6));
        Box("Ground", root, new Vector3(0, -0.1f, 30), new Vector3(70, 0.2f, 90), M["wetAsphalt"], 5f);
        Box("SidewalkL", street, new Vector3(-10, 0.075f, 25), new Vector3(6, 0.15f, 100), M["wetConcrete"], 3f);
        Box("SidewalkR", street, new Vector3(10, 0.075f, 25), new Vector3(6, 0.15f, 100), M["wetConcrete"], 3f);
        var lines = new List<CombineInstance>();
        var dash = BoxMesh(new Vector3(0.15f, 0.02f, 2.4f), 1f);
        for (float z = 4; z < 70; z += 6)
            lines.Add(new CombineInstance { mesh = dash, transform = Matrix4x4.TRS(new Vector3(0, 0.005f, z), Quaternion.identity, Vector3.one) });
        CombineInto(street, "RoadLines", lines, M["line"]);
        // 입구 앞 횡단보도 (흰 페인트)
        var crossing = new List<CombineInstance>();
        var stripe = BoxMesh(new Vector3(0.55f, 0.02f, 2.6f), 1f);
        for (int i = 0; i < 11; i++)
            crossing.Add(new CombineInstance { mesh = stripe, transform = Matrix4x4.TRS(new Vector3(-6.2f + i * 1.25f, 0.006f, 0.6f), Quaternion.identity, Vector3.one) });
        CombineInto(street, "Crosswalk", crossing, M["crossPaint"]);
        // 물웅덩이: 가로등 불빛이 번져 보이게 아주 매끈한 검은 판
        foreach (var (x, z, w, d) in new[] { (1.6f, -2.2f, 2.4f, 1.3f), (-2.4f, 1.8f, 1.8f, 1.1f), (3.8f, 4.5f, 2.8f, 1.6f), (-0.8f, 9f, 3.2f, 1.4f), (2.2f, 16f, 2.6f, 1.2f) })
            Box("Puddle", street, new Vector3(x, 0.004f, z), new Vector3(w, 0.004f, d), M["puddle"], 1f, false, new Vector3(0, R(-25, 25), 0));

        // 길 양옆 건물
        foreach (var side in new[] { -1f, 1f })
            for (float z = -4; z < 75;)
            {
                float d = R(8f, 13f), w = R(8f, 12f), h = R(9f, 24f);
                float x0 = side < 0 ? -13 - w : 13;
                var g = Group("Building", street, new Vector3(0, 0.15f, 0));
                Box("Mass", g, new Vector3(x0 + w / 2, h / 2, z + d / 2), new Vector3(w, h, d), Pick(M["wall"], M["brick"], M["cracked"]), 3f);
                FacadeWindows(g, x0, z, w, d, 3.5f, Mathf.FloorToInt((h - 3.5f) / 3.2f));
                Box("Shutter", g, new Vector3(side < 0 ? x0 + w + 0.06f : x0 - 0.06f, 1.4f, z + d / 2), new Vector3(0.1f, 2.6f, Mathf.Min(5, d - 2)), M["metal"], 1.5f);
                z += d + R(0.5f, 3f);
            }

        WreckedCar(street, new Vector3(-3f, 0, 9), 18, false);
        WreckedCar(street, new Vector3(3.5f, 0, 22), -70, true);
        WreckedCar(street, new Vector3(-2f, 0, 38), 160, false);
        RubblePile(street, new Vector3(5.5f, 0, 6), 10, 1.4f);
        RubblePile(street, new Vector3(-5.5f, 0, 27), 12, 1.6f);
        for (int k = 0; k < 3; k++)
            Box("Barrier", street, new Vector3(5f + k * 2.1f, 0.45f, 14), new Vector3(2f, 0.9f, 0.6f), M["dirty"], 2f, true, new Vector3(0, R(-10, 10), k == 1 ? 80 : 0));
        FireBarrel(street, new Vector3(-4.6f, 0, 4.5f));
        LampPost(street, new Vector3(-8.5f, 0.15f, 12), true);
        LampPost(street, new Vector3(8.5f, 0.15f, 30));
        DeadTree(street, new Vector3(9f, 0.15f, 6));
        DeadTree(street, new Vector3(-9f, 0.15f, 30));

        // 입구 바로 밖에 버려진 쓰레기 봉투·타이어 (Poly Haven CC0 모델)
        PolyProp("trashbag", root, new Vector3(1.3f, 0, 1.5f), 30, 0.55f);
        PolyProp("trashbag", root, new Vector3(1.85f, 0, 2.15f), -60, 0.55f);
        PolyProp("old_tyre", root, new Vector3(-1.85f, 0.33f, -0.9f), 90, 0.35f, 0, true, new Vector3(0, 0, -14));

        // 안개와 비 속에 서 있는 좀비 실루엣
        PoseZombie(zombiePrefab, new Vector3(1.8f, 0, 16), 200);
        PoseZombie(zombiePrefab, new Vector3(-2.4f, 0, 30), 170);

        // ---------- 방공호 입구 터널 ----------
        BuildMenuTunnel(root);

        var camGo = new GameObject("MenuCamera");
        camGo.tag = "MainCamera";
        // 왼쪽 벽 가까이에서 입구를 비스듬히 본다: 입구가 화면 오른쪽 절반을 차지하고 왼쪽은 어두운 벽이 된다
        camGo.transform.position = new Vector3(-1.3f, 1.4f, -4.9f);
        camGo.transform.rotation = Quaternion.Euler(2f, -5f, 0);
        var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 55f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 120f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        var data = cam.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = true;
        data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        camGo.AddComponent<AudioListener>();
        var bg = root.gameObject.AddComponent<MainMenuBackground>();
        bg.cameraTransform = camGo.transform;
        bg.driftAmount = 0.22f; // 터널 안이라 아주 조금만 흔들린다
        bg.lookDrift = 0.5f;

        // 비: 터널 밖 거리 위에만 내린다. 입구 바로 앞은 빗줄기가 잘 보이게 더 촘촘히
        AddRain(root, new Vector3(0, 0, 23.5f), new Vector3(36, 14, 45), 5200);
        AddRain(root, new Vector3(1.5f, 0, 5.5f), new Vector3(12, 8, 9), 2600);

        // 반사 프로브: 젖은 바닥·물웅덩이에 환한 입구와 가로등 불빛이 비친다 (씬이 열릴 때 한 번 찍는다)
        var probeGo = new GameObject("WetReflections");
        probeGo.transform.SetParent(root, false);
        probeGo.transform.localPosition = new Vector3(0, 1.6f, -1f);
        var probe = probeGo.AddComponent<ReflectionProbe>();
        probe.mode = ReflectionProbeMode.Realtime;
        probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
        probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
        probe.boxProjection = true;
        probe.size = new Vector3(14f, 6f, 26f);
        probe.resolution = 256;
        probe.intensity = 1.2f;

        // 빛: 흐린 하늘의 푸른 달빛 + 밝은 안개(입구가 환하게 보이고 터널 안은 어둡다)
        var moon = new GameObject("Moonlight").AddComponent<Light>();
        moon.type = LightType.Directional;
        moon.color = new Color(0.58f, 0.66f, 0.85f);
        moon.intensity = 0.7f;
        moon.shadows = LightShadows.Soft;
        moon.transform.rotation = Quaternion.Euler(38, 200, 0);
        RenderSettings.sun = moon;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.1f, 0.11f, 0.15f);
        RenderSettings.skybox = null;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.042f;
        RenderSettings.fogColor = new Color(0.33f, 0.38f, 0.48f);
        cam.backgroundColor = RenderSettings.fogColor;
        AddVolume(MakeProfile("MenuProfile", 0.95f, -28f, new Color(0.86f, 0.9f, 1.05f), 0.45f, 16f));

        UnbatchInteractables();
        Debug.Log($"[GameSceneBuilder] MainMenu: 같은 면에 겹친 상자 {FixCoplanarFaces()}곳을 고침");
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(scene, SceneDir + "/MainMenu.unity");
    }

    // 둥근 천장의 콘크리트 터널 (쇠 고리로 보강) + 입구 테두리 + 열린 방폭문 + 안쪽 끝의 철문.
    // 아치는 상자 토막을 반원으로 이어 붙인다 (토막의 로컬 x = 반지름 방향, y = 접선 방향)
    static void BuildMenuTunnel(Transform root)
    {
        var tunnel = Group("ShelterEntrance", root, Vector3.zero);
        const float t = 0.3f;
        float zc = -TunnelLength / 2;

        Box("Floor", tunnel, new Vector3(0, 0f, zc), new Vector3(TunnelRadius * 2, 0.08f, TunnelLength), M["wetConcrete"], 2f);
        foreach (var side in new[] { -1f, 1f })
            Box("Wall", tunnel, new Vector3(side * (TunnelRadius + t / 2), TunnelWallH / 2, zc), new Vector3(t, TunnelWallH, TunnelLength), M["dirty"], 2f);
        Arch(tunnel, "Vault", TunnelRadius, t, new Vector3(0, TunnelWallH, zc), TunnelLength, M["dirty"], 16, 2.5f);

        // 쇠 고리: 1.6m마다 벽과 천장을 따라 둘러 댄다
        for (float z = -0.8f; z > -TunnelLength + 0.5f; z -= 1.6f)
        {
            Arch(tunnel, "Rib", TunnelRadius - 0.06f, 0.12f, new Vector3(0, TunnelWallH, z), 0.22f, M["metal"], 16, 1f);
            foreach (var side in new[] { -1f, 1f })
                Box("RibWall", tunnel, new Vector3(side * (TunnelRadius - 0.06f), TunnelWallH / 2, z), new Vector3(0.12f, TunnelWallH, 0.22f), M["metal"], 1f, false);
        }

        // 입구 테두리 (두꺼운 콘크리트 고리) + 노랑·검정 경고 띠
        Arch(tunnel, "Collar", TunnelRadius, 0.7f, new Vector3(0, TunnelWallH, 0.25f), 0.5f, M["dirty"], 16, 2f);
        foreach (var side in new[] { -1f, 1f })
            Box("CollarWall", tunnel, new Vector3(side * (TunnelRadius + 0.35f), TunnelWallH / 2, 0.25f), new Vector3(0.7f, TunnelWallH, 0.5f), M["dirty"], 2f);
        Arch(tunnel, "Hazard", TunnelRadius - 0.02f, 0.06f, new Vector3(0, TunnelWallH, 0.03f), 0.06f, M["blastStripe"], 24, 1f);

        // 입구 오른쪽 경첩에 매달려 바깥으로 열린 방폭문
        var hinge = Group("BlastDoorHinge", tunnel, new Vector3(TunnelRadius - 0.1f, 0, 0.55f), 0);
        hinge.localRotation = Quaternion.Euler(0, 118, 0);
        Box("BlastDoor", hinge, new Vector3(-1.05f, 1.35f, 0.12f), new Vector3(2.1f, 2.7f, 0.22f), M["metal"], 1.5f);
        for (int i = 0; i < 6; i++)
            Box("DoorStripe", hinge, new Vector3(-1.95f + i * 0.36f, 0.25f, 0.235f), new Vector3(0.18f, 0.3f, 0.01f), M["blastStripe"], 1f, false);
        Prim(PrimitiveType.Cylinder, "DoorWheel", hinge, new Vector3(-1.05f, 1.35f, 0.26f), new Vector3(0.5f, 0.03f, 0.5f), M["metal"], new Vector3(90, 0, 0));

        // 터널 안쪽 끝: 방공호로 들어가는 철문과 붉은 경고등
        Box("BackWall", tunnel, new Vector3(0, 1.7f, -TunnelLength - 0.15f), new Vector3(TunnelRadius * 2 + 0.6f, 3.4f, 0.3f), M["wall"], 2f);
        Box("InnerDoor", tunnel, new Vector3(0, 1.1f, -TunnelLength + 0.02f), new Vector3(1.3f, 2.2f, 0.12f), M["metal"], 1.2f, false);
        Box("InnerLamp", tunnel, new Vector3(0, 2.45f, -TunnelLength + 0.05f), new Vector3(0.22f, 0.1f, 0.1f), M["redLamp"], 1f, false);
        PointLight("InnerRed", tunnel, new Vector3(0, 2.3f, -TunnelLength + 0.6f), new Color(1f, 0.15f, 0.1f), 3f, 6f);

        // 천장에 매단 작업등 (가끔 깜빡인다) — 터널 안을 겨우 비춘다
        var lampPos = new Vector3(0.5f, TunnelWallH + TunnelRadius - 0.45f, -2.6f);
        Prim(PrimitiveType.Cylinder, "LampCord", tunnel, lampPos + Vector3.up * 0.22f, new Vector3(0.01f, 0.2f, 0.01f), M["tire"]);
        Prim(PrimitiveType.Sphere, "LampBulb", tunnel, lampPos, new Vector3(0.1f, 0.13f, 0.1f), M["bulb"]);
        var lamp = PointLight("TunnelLamp", tunnel, lampPos + Vector3.down * 0.1f, new Color(1f, 0.72f, 0.45f), 2.6f, 7f, true);
        Flicker(lamp, 0.25f, 6f, true);

        // 터널 안 짐: 군용 상자·금속 연료통·방독면 (Poly Haven CC0 모델)
        PolyProp("old_military_crate", tunnel, new Vector3(1.5f, 0.04f, -3.3f), 92, 0.3f);
        PolyProp("metal_jerrycan", tunnel, new Vector3(1.6f, 0.04f, -1.7f), -20, 0.45f, 0.3f);
        PolyProp("metal_jerrycan", tunnel, new Vector3(1.85f, 0.04f, -2.1f), 15, 0.45f, 0.3f);
        PolyProp("old_gas_mask", tunnel, new Vector3(-TunnelRadius + 0.1f, 1.25f, -2.2f), 90, 0.35f, 0, false);

        // 오른쪽 벽을 따라가는 배관 두 줄과 받침
        foreach (var (y, r) in new[] { (0.95f, 0.07f), (0.7f, 0.05f) })
            Prim(PrimitiveType.Cylinder, "Pipe", tunnel, new Vector3(TunnelRadius - 0.14f, y, -TunnelLength / 2), new Vector3(r * 2, TunnelLength / 2, r * 2), M["metal"], new Vector3(90, 0, 0));
        for (float z = -1.2f; z > -TunnelLength; z -= 2.4f)
            Box("PipeBracket", tunnel, new Vector3(TunnelRadius - 0.1f, 0.82f, z), new Vector3(0.2f, 0.36f, 0.05f), M["metal"], 1f, false);

        // 입구 위에서 떨어지는 빗물 줄기
        var drip = new GameObject("Drip");
        drip.transform.SetParent(tunnel, false);
        drip.transform.localPosition = new Vector3(0, TunnelWallH + TunnelRadius + 0.05f, 0.62f);
        var ps = drip.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = 0.9f;
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.02f);
        main.startColor = new Color(0.75f, 0.82f, 0.95f, 0.6f);
        main.gravityModifier = 1.2f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var em = ps.emission;
        em.rateOverTime = 60;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(TunnelRadius * 1.7f, 0.02f, 0.05f);
        var pr = drip.GetComponent<ParticleSystemRenderer>();
        pr.renderMode = ParticleSystemRenderMode.Stretch;
        pr.velocityScale = 0.06f;
        pr.sharedMaterial = M["rain"];
    }

    // 반원 아치: center(아치 중심, 로컬)에서 radius만큼 떨어진 곳에 두께 thick, 길이 length(z)의 토막 segments개를 둘러 놓는다
    static void Arch(Transform parent, string name, float radius, float thick, Vector3 center, float length, Material mat, int segments, float tile)
    {
        float rm = radius + thick / 2;
        float step = 180f / segments;
        float chord = 2 * rm * Mathf.Sin(step * 0.5f * Mathf.Deg2Rad) * 1.06f; // 이음매에 틈이 보이지 않게 조금 겹친다
        for (int i = 0; i < segments; i++)
        {
            float a = (i + 0.5f) * step;
            var pos = center + new Vector3(Mathf.Cos(a * Mathf.Deg2Rad) * rm, Mathf.Sin(a * Mathf.Deg2Rad) * rm, 0);
            Box(name, parent, pos, new Vector3(thick, chord, length), mat, tile, true, new Vector3(0, 0, a));
        }
    }

    static void PoseZombie(GameObject prefab, Vector3 pos, float yaw)
    {
        // 실행하면 디자인 시트의 일반 좀비 몸을 짓고 이 자세로 서서 흔들린다 (CharacterFigure)
        var z = new GameObject("MenuZombie");
        z.transform.position = pos;
        z.transform.rotation = Quaternion.Euler(0, yaw, 0);
        var f = z.AddComponent<CharacterFigure>();
        f.kind = FigureKind.Zombie;
        f.variant = Mathf.Abs(Mathf.RoundToInt(pos.z)) % 4;
        f.glowTemplate = M["zEye"];
        f.bodyPose = new Vector3(14, 0, 4);
        f.armLPose = new Vector3(-70, 0, -6);
        f.armRPose = new Vector3(-55, 0, 8);
        f.headPose = new Vector3(8, 15, 24);
        f.legLPose = new Vector3(12, 0, 0);
        f.legRPose = new Vector3(-8, 0, 0);
    }
}
