using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// GameSceneBuilder의 방공호(Shelter) 씬 부분
public static partial class GameSceneBuilder
{
    // ================= 방공호 =================

    static void BuildShelterScene(GameObject playerPrefab, GameObject zombiePrefab)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        BeginMeshes(GenDir + "/ShelterMeshes.asset");
        rng = new System.Random(7);
        var root = new GameObject("Shelter").transform;
        const float t = 0.3f;

        // 방폭문 노랑 경고 줄무늬, 라디오 다이얼·표시등
        M["blastStripe"] = TexMat("M_BlastDoorStripe", "rusty_metal_02", new Color(0.72f, 0.55f, 0.1f), 0.3f, 0.3f);
        M["radioDial"] = Emissive("M_RadioDial", new Color(1f, 0.78f, 0.45f), new Color(1.6f, 0.95f, 0.4f));
        M["radioLed"] = Emissive("M_RadioLed", new Color(0.3f, 1f, 0.4f), new Color(0.3f, 2.2f, 0.5f));

        // 바닥·천장
        Box("Floor", root, new Vector3(0, -0.1f, 0), new Vector3(10.3f, 0.2f, 8.3f), M["floor"], 2.5f);
        Box("Ceiling", root, new Vector3(0, 3.1f, 0), new Vector3(10.3f, 0.2f, 8.3f), M["dirty"], 2.5f);
        Box("GenFloor", root, new Vector3(7.65f, -0.1f, 0), new Vector3(5f, 0.2f, 5.3f), M["floor"], 2.5f);
        Box("GenCeiling", root, new Vector3(7.65f, 3.1f, 0), new Vector3(5f, 0.2f, 5.3f), M["dirty"], 2.5f);
        Box("CorridorFloor", root, new Vector3(0, -0.1f, 8.15f), new Vector3(2.3f, 0.2f, 8f), M["floor"], 2.5f);
        Box("CorridorCeiling", root, new Vector3(0, 2.7f, 8.15f), new Vector3(2.3f, 0.2f, 8f), M["dirty"], 2.5f);

        // 벽: 거실(가운데), 발전기실(동쪽), 복도(북쪽). 복도 동쪽 벽(z=8)에는 방폭문 구멍
        RoomWalls(root, -5, -4, 10, 8, 3, t, M["wall"], Solid, new[] { DoorOp(5, 2.0f, 2.5f) }, Solid, new[] { DoorOp(4, 1.3f, 2.2f) });
        RoomWalls(root, 5, -2.5f, 5, 5, 3, t, M["wall"], Solid, Solid, null, Solid);
        RoomWalls(root, -1, 4, 2, 8, 2.6f, t, M["cracked"], null, Solid, Solid, new[] { DoorOp(4, 1.6f, 2.2f) });

        // 천장 배관
        Prim(PrimitiveType.Cylinder, "Pipe1", root, new Vector3(0, 2.82f, -3.6f), new Vector3(0.12f, 5.1f, 0.12f), M["metal"], new Vector3(0, 0, 90));
        Prim(PrimitiveType.Cylinder, "Pipe2", root, new Vector3(0, 2.9f, -3.3f), new Vector3(0.07f, 5.1f, 0.07f), M["metal"], new Vector3(0, 0, 90));
        Prim(PrimitiveType.Cylinder, "Pipe3", root, new Vector3(0.75f, 2.45f, 8f), new Vector3(0.08f, 4f, 0.08f), M["metal"], new Vector3(90, 0, 0));
        Prim(PrimitiveType.Cylinder, "Pipe4", root, new Vector3(4.8f, 2.85f, 0), new Vector3(0.06f, 3.9f, 0.06f), M["metal"], new Vector3(90, 0, 0));

        // 조명
        var lights = new List<Light>();
        lights.Add(Bulb(root, new Vector3(-2.5f, 2.75f, 0.5f), 14f, 10f, true));
        lights.Add(Bulb(root, new Vector3(2.8f, 2.75f, 2.8f), 9f, 8f, false));
        lights.Add(Bulb(root, new Vector3(0, 2.4f, 8.2f), 5f, 7f, false));
        lights.Add(Bulb(root, new Vector3(7.5f, 2.75f, 0), 8f, 8f, false));
        lights.Add(Bulb(root, new Vector3(2.2f, 2.75f, -2.4f), 9f, 9f, false));
        var emergency = PointLight("EmergencyLight", root, new Vector3(0, 2.85f, -3.7f), new Color(1f, 0.12f, 0.08f), 10f, 14f);
        Box("EmergencyLamp", root, new Vector3(0, 2.95f, -3.82f), new Vector3(0.25f, 0.1f, 0.12f), M["redLamp"], 1f, false);
        var shelterLights = root.gameObject.AddComponent<ShelterLights>();
        shelterLights.lights = lights.ToArray();
        shelterLights.emergencyLights = new[] { emergency };

        BuildBed(root, new Vector3(-4.3f, 0, 2.3f));
        BuildWorkbench(root, new Vector3(3.2f, 0, 3.45f));
        BuildStorage(root, new Vector3(-3.4f, 0, -3.55f));
        BuildShelf(root, new Vector3(-1.2f, 0, -3.62f), 0, true);
        BuildShelf(root, new Vector3(3.6f, 0, -3.62f), 0, true);
        BuildTable(root, new Vector3(0.6f, 0, -0.4f));
        BuildGenerator(root, new Vector3(8.9f, 0, 0));

        // 물통
        Prim(PrimitiveType.Cylinder, "WaterBarrel1", root, new Vector3(-4.4f, 0.45f, -1.3f), new Vector3(0.55f, 0.45f, 0.55f), M["paintBlue"], null, true);
        Prim(PrimitiveType.Cylinder, "WaterBarrel2", root, new Vector3(-4.35f, 0.45f, -0.6f), new Vector3(0.55f, 0.45f, 0.55f), M["paintBlue"], null, true);
        Box("Crate", root, new Vector3(4.2f, 0.3f, -1.6f), new Vector3(0.8f, 0.6f, 0.6f), M["wood"], 1f, true, new Vector3(0, 12, 0));
        Box("Crate2", root, new Vector3(4.3f, 0.82f, -1.6f), new Vector3(0.55f, 0.44f, 0.5f), M["wood"], 1f, true, new Vector3(0, -8, 0));

        // 출입구 (복도 끝 철문)
        var exit = Group("Exit", root, new Vector3(0, 0, 11.82f));
        var door = Box("SteelDoor", exit, new Vector3(0, 1.1f, 0), new Vector3(1.2f, 2.2f, 0.12f), M["metal"], 1.2f);
        door.AddComponent<ShelterExit>();
        Prim(PrimitiveType.Cylinder, "Wheel", exit, new Vector3(0, 1.15f, -0.1f), new Vector3(0.45f, 0.02f, 0.45f), M["metal"], new Vector3(90, 0, 0));
        Box("FrameL", exit, new Vector3(-0.68f, 1.15f, -0.02f), new Vector3(0.14f, 2.3f, 0.18f), M["metal"], 1f, false);
        Box("FrameR", exit, new Vector3(0.68f, 1.15f, -0.02f), new Vector3(0.14f, 2.3f, 0.18f), M["metal"], 1f, false);
        Box("FrameT", exit, new Vector3(0, 2.3f, -0.02f), new Vector3(1.5f, 0.14f, 0.18f), M["metal"], 1f, false);
        Box("ExitLamp", exit, new Vector3(0, 2.45f, -0.08f), new Vector3(0.2f, 0.1f, 0.1f), M["redLamp"], 1f, false);
        PointLight("ExitLight", exit, new Vector3(0, 2.4f, -0.4f), new Color(1f, 0.15f, 0.1f), 3f, 5f);

        // 정전 때 좀비가 들어오는 옛 통로와 방폭문 (복도 동쪽)
        var blastDoor = BuildInvasionPassage(root, t, out var zombieEntry);

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.12f, 0.105f, 0.09f);
        RenderSettings.skybox = null;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.018f;
        RenderSettings.fogColor = new Color(0.05f, 0.045f, 0.04f);
        RenderSettings.reflectionIntensity = 0.3f;

        AddVolume(MakeProfile("ShelterProfile", 0.85f, -20f, new Color(1f, 0.95f, 0.88f), 0.35f, 14f));
        PlacePlayer(playerPrefab, new Vector3(0, 0.05f, -2f), 0, RenderSettings.fogColor);

        var invasion = new GameObject("ShelterInvasion").AddComponent<ShelterInvasion>();
        invasion.zombiePrefab = zombiePrefab;
        invasion.door = blastDoor;
        invasion.spawnPoints = new[] { zombieEntry };

        // 내비메시 굽기 (정전 때 들어온 좀비가 다닐 길). 방폭문은 빼고 구워 문간이 통로와 이어지게 한다
        var surface = root.gameObject.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Children;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.BuildNavMesh();
        string navPath = GenDir + "/ShelterNavMesh.asset";
        AssetDatabase.DeleteAsset(navPath);
        AssetDatabase.CreateAsset(surface.navMeshData, navPath);

        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(scene, SceneDir + "/Shelter.unity");
    }

    static Light Bulb(Transform parent, Vector3 pos, float intensity, float range, bool shadows)
    {
        var g = Group("Bulb", parent, pos);
        Prim(PrimitiveType.Sphere, "Glass", g, Vector3.zero, Vector3.one * 0.11f, M["bulb"]).GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        Prim(PrimitiveType.Cylinder, "Cord", g, new Vector3(0, 0.15f, 0), new Vector3(0.01f, 0.13f, 0.01f), M["tire"]);
        Prim(PrimitiveType.Cylinder, "Socket", g, new Vector3(0, 0.07f, 0), new Vector3(0.04f, 0.03f, 0.04f), M["metal"]);
        return PointLight("Light", g, new Vector3(0, -0.08f, 0), new Color(1f, 0.72f, 0.45f), intensity, range, shadows);
    }

    static void BuildBed(Transform parent, Vector3 pos)
    {
        var g = Group("Bed", parent, pos);
        Box("Frame", g, new Vector3(0, 0.25f, 0), new Vector3(0.95f, 0.08f, 2f), M["metal"], 1f, false);
        foreach (var x in new[] { -0.44f, 0.44f })
            foreach (var z in new[] { -0.96f, 0.96f })
                Box("Leg", g, new Vector3(x, 0.12f, z), new Vector3(0.05f, 0.25f, 0.05f), M["metal"], 1f, false);
        Box("HeadBoard", g, new Vector3(0, 0.5f, 0.98f), new Vector3(0.95f, 0.5f, 0.04f), M["metal"], 1f, false);
        Box("Mattress", g, new Vector3(0, 0.37f, 0), new Vector3(0.9f, 0.16f, 1.92f), M["cloth"], 1f, false);
        Box("Pillow", g, new Vector3(0, 0.5f, 0.72f), new Vector3(0.6f, 0.1f, 0.34f), M["cloth"], 1f, false, new Vector3(-6, 0, 0));
        Box("Blanket", g, new Vector3(0.02f, 0.47f, -0.35f), new Vector3(0.94f, 0.05f, 1.15f), M["blanket"], 1f, false, new Vector3(0, 0, -2));
        var col = g.gameObject.AddComponent<BoxCollider>();
        col.center = new Vector3(0, 0.35f, 0);
        col.size = new Vector3(1f, 0.7f, 2.05f);
        g.gameObject.AddComponent<Bed>();
    }

    static void BuildWorkbench(Transform parent, Vector3 pos)
    {
        var g = Group("Workbench", parent, pos);
        Box("Top", g, new Vector3(0, 0.9f, 0), new Vector3(2.2f, 0.08f, 0.8f), M["wood"], 1.2f, false);
        foreach (var x in new[] { -1.02f, 1.02f })
            foreach (var z in new[] { -0.34f, 0.34f })
                Box("Leg", g, new Vector3(x, 0.43f, z), new Vector3(0.06f, 0.86f, 0.06f), M["metal"], 1f, false);
        Box("Shelf", g, new Vector3(0, 0.25f, 0), new Vector3(2.1f, 0.04f, 0.7f), M["wood"], 1.2f, false);
        Box("Pegboard", g, new Vector3(0, 1.65f, 0.36f), new Vector3(2.2f, 1.1f, 0.03f), M["wood"], 1.2f, false);
        Box("Vise", g, new Vector3(0.85f, 1.0f, -0.2f), new Vector3(0.18f, 0.12f, 0.22f), M["metal"], 1f, false);
        Prim(PrimitiveType.Cylinder, "HammerHandle", g, new Vector3(-0.4f, 0.96f, -0.1f), new Vector3(0.03f, 0.15f, 0.03f), M["wood"], new Vector3(90, 30, 0));
        Box("HammerHead", g, new Vector3(-0.33f, 0.96f, 0.03f), new Vector3(0.12f, 0.05f, 0.05f), M["metal"], 1f, false, new Vector3(0, 30, 0));
        Box("Wrench", g, new Vector3(0.2f, 1.7f, 0.33f), new Vector3(0.04f, 0.3f, 0.02f), M["metal"], 1f, false, new Vector3(0, 0, 10));
        Box("Saw", g, new Vector3(-0.5f, 1.75f, 0.33f), new Vector3(0.5f, 0.12f, 0.01f), M["metal"], 1f, false);
        Box("ToolBox", g, new Vector3(0.1f, 0.38f, 0), new Vector3(0.55f, 0.22f, 0.3f), M["paintRed"], 1f, false);
        Box("Scrap", g, new Vector3(-0.7f, 0.96f, 0.1f), new Vector3(0.3f, 0.02f, 0.25f), M["metal"], 1f, false, new Vector3(0, 20, 0));
        var col = g.gameObject.AddComponent<BoxCollider>();
        col.center = new Vector3(0, 0.5f, 0);
        col.size = new Vector3(2.2f, 1f, 0.8f);
        g.gameObject.AddComponent<Workbench>();
    }

    static void BuildStorage(Transform parent, Vector3 pos)
    {
        var g = Group("Storage", parent, pos);
        foreach (var x in new[] { -0.42f, 0.42f })
        {
            Box("Locker", g, new Vector3(x, 0.95f, 0), new Vector3(0.8f, 1.9f, 0.5f), M["metal"], 1.2f, false);
            Box("Seam", g, new Vector3(x, 0.95f, 0.255f), new Vector3(0.02f, 1.8f, 0.01f), M["tire"], 1f, false);
            for (int i = 0; i < 4; i++)
                Box("Vent", g, new Vector3(x - 0.2f, 1.6f - i * 0.06f, 0.255f), new Vector3(0.22f, 0.02f, 0.01f), M["tire"], 1f, false);
            Box("Handle", g, new Vector3(x + 0.06f, 1.0f, 0.27f), new Vector3(0.03f, 0.15f, 0.03f), M["metal"], 1f, false);
        }
        var col = g.gameObject.AddComponent<BoxCollider>();
        col.center = new Vector3(0, 0.95f, 0);
        col.size = new Vector3(1.7f, 1.9f, 0.55f);
        g.gameObject.AddComponent<StorageBox>();
    }

    // 선반 (장식용 깡통·물병 포함). 도시 건물에서도 쓴다
    static Transform BuildShelf(Transform parent, Vector3 pos, float yaw, bool stocked)
    {
        var g = Group("Shelf", parent, pos, yaw);
        // 판자마다 콜라이더를 둔다. 선반 전체를 감싸는 상자 하나로 두면 선반 위 아이템이
        // 바닥 찾기 Raycast(시작점이 상자 안이라 감지 못함)에 걸려 맨 아래 판자 밑으로 떨어져 숨는다.
        Box("SideL", g, new Vector3(-0.78f, 0.9f, 0), new Vector3(0.04f, 1.8f, 0.45f), M["metal"], 1f, true);
        Box("SideR", g, new Vector3(0.78f, 0.9f, 0), new Vector3(0.04f, 1.8f, 0.45f), M["metal"], 1f, true);
        foreach (var y in new[] { 0.1f, 0.6f, 1.1f, 1.6f })
            Box("Board", g, new Vector3(0, y, 0), new Vector3(1.6f, 0.03f, 0.45f), M["wood"], 1f, true);
        // 뒤판: 좀비 시야(Linecast)가 판자 사이로 선반을 뚫고 지나가지 않게 막는다
        Box("Back", g, new Vector3(0, 0.9f, -0.215f), new Vector3(1.52f, 1.8f, 0.02f), M["metal"], 1f, true);
        if (stocked)
            foreach (var y in new[] { 0.6f, 1.1f, 1.6f })
                for (int i = 0; i < 5; i++)
                {
                    if (Chance(0.35)) continue;
                    float x = -0.6f + i * 0.3f + R(-0.05f, 0.05f);
                    // 아이템이 생기는 자리(0.6 판 왼쪽, 1.1 판 오른쪽)는 장식을 비워 아이템이 상자 속에 묻히지 않게 한다.
                    // 난수는 그대로 소비해서 나머지 배치는 바뀌지 않게 한다.
                    bool keep = !(y == 0.6f && i == 1) && !(y == 1.1f && i == 3);
                    if (Chance(0.5))
                    {
                        float z = R(-0.1f, 0.1f);
                        if (keep) Prim(PrimitiveType.Cylinder, "Can", g, new Vector3(x, y + 0.08f, z), new Vector3(0.09f, 0.065f, 0.09f), M["metal"]);
                    }
                    else
                    {
                        float z = R(-0.1f, 0.1f), turn = R(-20, 20);
                        if (keep) Box("Box", g, new Vector3(x, y + 0.12f, z), new Vector3(0.22f, 0.2f, 0.18f), M["cloth"], 1f, false, new Vector3(0, turn, 0));
                    }
                }
        GameObjectUtility.SetStaticEditorFlags(g.gameObject, StaticEditorFlags.BatchingStatic);
        return g;
    }

    static void BuildTable(Transform parent, Vector3 pos)
    {
        var g = Group("Table", parent, pos);
        Box("Top", g, new Vector3(0, 0.76f, 0), new Vector3(1.6f, 0.06f, 0.9f), M["wood"], 1.2f, true);
        foreach (var x in new[] { -0.72f, 0.72f })
            foreach (var z in new[] { -0.38f, 0.38f })
                Box("Leg", g, new Vector3(x, 0.37f, z), new Vector3(0.06f, 0.73f, 0.06f), M["wood"], 1f, true);
        BuildRadio(g, new Vector3(-0.45f, 0.79f, 0.15f), 10);
        Box("Map", g, new Vector3(0.25f, 0.795f, -0.05f), new Vector3(0.6f, 0.005f, 0.45f), M["cloth"], 1f, false, new Vector3(0, -8, 0));
        Prim(PrimitiveType.Cylinder, "Mug", g, new Vector3(0.6f, 0.84f, 0.25f), new Vector3(0.08f, 0.05f, 0.08f), M["metal"]);
        var chair = Group("Chair", g, new Vector3(0.2f, 0, -0.85f), 10);
        Box("Seat", chair, new Vector3(0, 0.45f, 0), new Vector3(0.45f, 0.05f, 0.45f), M["wood"], 1f, true);
        Box("Back", chair, new Vector3(0, 0.75f, -0.21f), new Vector3(0.45f, 0.55f, 0.04f), M["wood"], 1f, false);
        foreach (var x in new[] { -0.2f, 0.2f })
            foreach (var z in new[] { -0.2f, 0.2f })
                Box("Leg", chair, new Vector3(x, 0.22f, z), new Vector3(0.04f, 0.45f, 0.04f), M["wood"], 1f, false);
    }

    static void BuildGenerator(Transform parent, Vector3 pos)
    {
        var g = Group("Generator", parent, pos, -90);
        Box("Base", g, new Vector3(0, 0.06f, 0), new Vector3(1.5f, 0.12f, 0.9f), M["metal"], 1f, false);
        Box("Body", g, new Vector3(-0.15f, 0.55f, 0), new Vector3(1.0f, 0.8f, 0.8f), M["metal"], 1f, false);
        Prim(PrimitiveType.Cylinder, "Engine", g, new Vector3(0.5f, 0.5f, 0), new Vector3(0.45f, 0.3f, 0.45f), M["metal"], new Vector3(0, 0, 90));
        Box("Tank", g, new Vector3(-0.15f, 1.08f, 0), new Vector3(0.8f, 0.25f, 0.55f), M["paintRed"], 1f, false);
        Box("Panel", g, new Vector3(-0.15f, 0.6f, 0.41f), new Vector3(0.5f, 0.35f, 0.03f), M["tire"], 1f, false);
        Box("Gauge", g, new Vector3(-0.25f, 0.68f, 0.43f), new Vector3(0.08f, 0.08f, 0.01f), M["bulb"], 1f, false);
        Prim(PrimitiveType.Cylinder, "Exhaust", g, new Vector3(0.45f, 1.85f, -0.25f), new Vector3(0.08f, 1.15f, 0.08f), M["metal"]);
        var col = g.gameObject.AddComponent<BoxCollider>();
        col.center = new Vector3(0, 0.6f, 0);
        col.size = new Vector3(1.5f, 1.2f, 0.9f);
        var gen = g.gameObject.AddComponent<Generator>();
        var hum = g.gameObject.AddComponent<AudioSource>();
        hum.loop = true;
        hum.playOnAwake = false;
        hum.spatialBlend = 1;
        hum.minDistance = 1.5f;
        hum.maxDistance = 16f;
        hum.rolloffMode = AudioRolloffMode.Linear;
        hum.volume = 0.3f;
        gen.hum = hum;
    }

    // 배터리 라디오 (정전 중에도 켜진다). 앞면이 -Z(의자 쪽)를 본다
    static void BuildRadio(Transform parent, Vector3 pos, float yaw)
    {
        var g = Group("Radio", parent, pos, yaw);
        Box("Body", g, new Vector3(0, 0.1f, 0), new Vector3(0.38f, 0.2f, 0.17f), M["tire"], 1f, false);
        Box("FacePlate", g, new Vector3(0, 0.1f, -0.088f), new Vector3(0.35f, 0.17f, 0.008f), M["metal"], 1f, false);
        for (int i = 0; i < 5; i++)
            Box("Grille", g, new Vector3(-0.085f, 0.045f + i * 0.026f, -0.094f), new Vector3(0.14f, 0.012f, 0.006f), M["tire"], 1f, false);
        Box("DialWindow", g, new Vector3(0.08f, 0.145f, -0.093f), new Vector3(0.14f, 0.045f, 0.004f), M["radioDial"], 1f, false);
        var needle = Box("Needle", g, new Vector3(0.035f, 0.145f, -0.097f), new Vector3(0.005f, 0.04f, 0.004f), M["tire"], 1f, false, null, false);
        foreach (var x in new[] { 0.045f, 0.12f })
            Prim(PrimitiveType.Cylinder, "Knob", g, new Vector3(x, 0.06f, -0.1f), new Vector3(0.035f, 0.012f, 0.035f), M["metal"], new Vector3(90, 0, 0));
        Prim(PrimitiveType.Sphere, "Led", g, new Vector3(0.16f, 0.175f, -0.093f), Vector3.one * 0.014f, M["radioLed"]);
        Prim(PrimitiveType.Cylinder, "Antenna", g, new Vector3(0.15f, 0.38f, 0.05f), new Vector3(0.008f, 0.2f, 0.008f), M["metal"], new Vector3(0, 0, -20));
        Box("Handle", g, new Vector3(0, 0.26f, 0), new Vector3(0.24f, 0.02f, 0.02f), M["metal"], 1f, false);
        foreach (var x in new[] { -0.11f, 0.11f })
            Box("HandlePost", g, new Vector3(x, 0.23f, 0), new Vector3(0.02f, 0.06f, 0.02f), M["metal"], 1f, false);
        var col = g.gameObject.AddComponent<BoxCollider>();
        col.center = new Vector3(0, 0.14f, 0);
        col.size = new Vector3(0.44f, 0.3f, 0.24f);

        var glow = PointLight("RadioGlow", g, new Vector3(0.06f, 0.16f, -0.3f), new Color(1f, 0.7f, 0.35f), 1.5f, 2.2f);
        var speaker = g.gameObject.AddComponent<AudioSource>();
        speaker.loop = true;
        speaker.playOnAwake = false;
        speaker.spatialBlend = 1;
        speaker.minDistance = 1f;
        speaker.maxDistance = 14f;
        speaker.rolloffMode = AudioRolloffMode.Linear;
        speaker.volume = 0.35f;
        var radio = g.gameObject.AddComponent<Radio>();
        radio.speaker = speaker;
        radio.glow = glow;
        radio.needle = needle.transform;
    }

    // 정전 때 좀비가 들어오는 옛 통로: 복도 동쪽 벽의 방폭문 → 동쪽으로 가다 북쪽으로 꺾여, 끝의 무너진 구멍으로 이어진다.
    // 좀비는 꺾인 곳 너머(entry)에서 나타나므로 복도에서는 생기는 순간이 보이지 않는다.
    static BlastDoor BuildInvasionPassage(Transform root, float t, out Transform entry)
    {
        const float h = 2.6f;
        var g = Group("OldTunnel", root, Vector3.zero);
        var mat = M["cracked"];
        // A: 문(x=1)에서 동쪽으로 x=6까지 (z 7~9),  B: 동쪽 끝에서 북쪽으로 z=13까지 (x 4~6)
        Box("FloorA", g, new Vector3(3.65f, -0.1f, 8f), new Vector3(5f, 0.2f, 2.3f), M["floor"], 2.5f);
        Box("CeilingA", g, new Vector3(3.65f, h + 0.1f, 8f), new Vector3(5f, 0.2f, 2.3f), M["dirty"], 2.5f);
        Box("FloorB", g, new Vector3(5f, -0.1f, 11.15f), new Vector3(2.3f, 0.2f, 4f), M["floor"], 2.5f);
        Box("CeilingB", g, new Vector3(5f, h + 0.1f, 11.15f), new Vector3(2.3f, 0.2f, 4f), M["dirty"], 2.5f);
        Wall(g, "WallAS", new Vector3(1f, 0, 7f), 0, 5.15f, h, t, mat, Solid);
        Wall(g, "WallAN", new Vector3(1f, 0, 9f), 0, 5.15f, h, t, mat, new[] { DoorOp(4f, 1.7f, h) });
        Wall(g, "WallE", new Vector3(6f, 0, 6.85f), -90, 6.3f, h, t, mat, Solid);
        Wall(g, "WallBW", new Vector3(4f, 0, 9.15f), -90, 4f, h, t, mat, Solid);
        // 끝벽의 무너진 구멍. 턱(0.6m)이 높아 플레이어는 넘어갈 수 없다
        Wall(g, "WallBN", new Vector3(3.85f, 0, 13f), 0, 2.3f, h, t, mat, new[] { new Op(1.15f, 1.1f, 0.6f, 1.9f) });
        Box("Dark", g, new Vector3(5f, 1.1f, 13.75f), new Vector3(1.6f, 2.2f, 0.3f), M["tire"], 1f, false);
        Box("DarkL", g, new Vector3(4.3f, 1.1f, 13.45f), new Vector3(0.1f, 2.2f, 0.6f), M["tire"], 1f, false);
        Box("DarkR", g, new Vector3(5.7f, 1.1f, 13.45f), new Vector3(0.1f, 2.2f, 0.6f), M["tire"], 1f, false);
        Box("DebrisSlope", g, new Vector3(5f, 0.75f, 13.4f), new Vector3(1f, 0.25f, 0.6f), M["dirty"], 1.5f, false, new Vector3(-25, 0, 0));

        // 잔해·배관 (콜라이더 없음: 좁은 통로의 내비메시를 막지 않게)
        Box("Chunk", g, new Vector3(4.45f, 0.12f, 12.55f), new Vector3(0.45f, 0.24f, 0.35f), M["cracked"], 1.5f, false, new Vector3(8, 25, -6));
        Box("Chunk", g, new Vector3(5.62f, 0.1f, 12.45f), new Vector3(0.35f, 0.2f, 0.4f), M["dirty"], 1.5f, false, new Vector3(-10, -30, 12));
        Box("Chunk", g, new Vector3(5.1f, 0.7f, 12.98f), new Vector3(0.5f, 0.18f, 0.3f), M["cracked"], 1.5f, false, new Vector3(0, 15, 10));
        Box("Slab", g, new Vector3(4.6f, 0.05f, 11.4f), new Vector3(0.6f, 0.06f, 0.5f), M["dirty"], 1.5f, false, new Vector3(0, 40, 3));
        Prim(PrimitiveType.Cylinder, "Pipe", g, new Vector3(3.4f, 2.4f, 7.4f), new Vector3(0.07f, 2.4f, 0.07f), M["metal"], new Vector3(0, 0, 90));
        Prim(PrimitiveType.Cylinder, "Pipe", g, new Vector3(5.6f, 2.4f, 11.1f), new Vector3(0.07f, 1.8f, 0.07f), M["metal"], new Vector3(90, 0, 0));
        entry = Group("ZombieEntry", g, new Vector3(5f, 0, 12.2f), 180);

        // 방폭문: 로컬 +Z가 통로 쪽(월드 +X), 문짝은 로컬 +X(월드 -Z)로 밀려 벽 속에 숨는다
        var frame = Group("BlastDoor", root, new Vector3(1f, 0, 8f), 90);
        frame.gameObject.AddComponent<NavMeshModifier>().ignoreFromBuild = true; // 문간이 이어지게 문은 빼고 굽는다
        var leaf = Box("Leaf", frame, new Vector3(0, 1.125f, 0), new Vector3(1.7f, 2.25f, 0.2f), M["metal"], 1.2f, true, null, false);
        var lt = leaf.transform;
        foreach (var side in new[] { -1f, 1f })
        {
            // 위아래 노랑·검정 경고 띠, 보강대, 가운데 붉은 판 (벽 두께 안에 들어가게 얇게)
            foreach (var y in new[] { -0.9f, 0.9f })
                for (int i = 0; i < 6; i++)
                    Box("Stripe", lt, new Vector3(-0.85f + (i + 0.5f) * (1.7f / 6), y, side * 0.105f), new Vector3(1.7f / 6, 0.22f, 0.01f), i % 2 == 0 ? M["blastStripe"] : M["tire"], 1f, false, null, false);
            foreach (var y in new[] { -0.35f, 0.35f })
                Box("Bar", lt, new Vector3(0, y, side * 0.115f), new Vector3(1.6f, 0.08f, 0.03f), M["metal"], 1f, false, null, false);
            Box("Plate", lt, new Vector3(0, 0, side * 0.11f), new Vector3(0.5f, 0.3f, 0.02f), M["paintRed"], 1f, false, null, false);

            // 문틀: 줄무늬 기둥 + 위 보
            float z = side * 0.19f;
            foreach (var x in new[] { -0.88f, 0.88f })
                for (int k = 0; k < 8; k++)
                    Box("FrameStripe", frame, new Vector3(x, 0.275f * (k + 0.5f), z), new Vector3(0.16f, 0.275f, 0.08f), k % 2 == 0 ? M["blastStripe"] : M["tire"], 1f, false);
            Box("Lintel", frame, new Vector3(0, 2.28f, z), new Vector3(1.92f, 0.16f, 0.08f), M["metal"], 1f, false);
        }
        // 닫혀 있을 때만 켜지는 내비메시 장애물 (BlastDoor가 켜고 끈다)
        var obstacle = leaf.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.size = new Vector3(1.7f, 2.25f, 0.3f);
        obstacle.carving = true;
        obstacle.carveOnlyStationary = false;

        Box("WarnLamp", frame, new Vector3(-1.12f, 2.05f, -0.21f), new Vector3(0.14f, 0.14f, 0.1f), M["redLamp"], 1f, false);
        var warn = PointLight("WarnLight", frame, new Vector3(-0.9f, 2.0f, -0.5f), new Color(1f, 0.22f, 0.08f), 6f, 7f);
        var motor = frame.gameObject.AddComponent<AudioSource>();
        motor.loop = true;
        motor.playOnAwake = false;
        motor.spatialBlend = 1;
        motor.minDistance = 2f;
        motor.maxDistance = 30f;
        motor.rolloffMode = AudioRolloffMode.Linear;
        motor.volume = 0.8f;

        var door = frame.gameObject.AddComponent<BlastDoor>();
        door.leaf = lt;
        door.obstacle = obstacle;
        door.warningLight = warn;
        door.motor = motor;
        door.width = 1.6f;
        door.slideDistance = 1.75f;
        door.outsideArea = new Bounds(new Vector3(3.7f, 1f, 10.05f), new Vector3(5.1f, 4f, 6.5f)); // x 1.15~6.25, z 6.8~13.3
        return door;
    }
}
