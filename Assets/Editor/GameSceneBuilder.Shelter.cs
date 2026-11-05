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
        // 화로 화구 안의 불·숯 (Furnace가 발광 세기를 바꾼다)
        M["furnaceFire"] = Emissive("M_FurnaceFire", new Color(1f, 0.45f, 0.12f), new Color(5f, 1.7f, 0.35f));
        M["furnaceCoal"] = Emissive("M_FurnaceCoal", new Color(0.25f, 0.07f, 0.03f), new Color(1.8f, 0.4f, 0.08f));
        // 발전기 (주철·칠 벗겨진 틀·철창 안 불빛·달아오른 배기관·계기 글자판·바닥 기름), 발전기실 미닫이 철문 (어둡고 녹슨 강판)
        M["genIron"] = TexMat("M_GeneratorIron", "rusty_metal_02", new Color(0.28f, 0.28f, 0.29f), 0.4f, 0.65f);
        M["genFrame"] = TexMat("M_GeneratorFrame", "rusty_metal_02", new Color(0.5f, 0.38f, 0.16f), 0.3f, 0.45f);
        M["genGlow"] = Emissive("M_GeneratorGlow", new Color(0.3f, 0.08f, 0.03f), new Color(2.4f, 0.55f, 0.1f));
        M["genHot"] = Emissive("M_GeneratorHot", new Color(0.2f, 0.12f, 0.1f), new Color(1.5f, 0.22f, 0.04f));
        M["gaugeFace"] = Emissive("M_GaugeFace", new Color(0.85f, 0.8f, 0.65f), new Color(0.35f, 0.32f, 0.22f));
        M["oilStain"] = TexMat("M_OilStain", null, new Color(0.05f, 0.05f, 0.045f), 0.75f, 0.1f);
        M["chimneyGlass"] = TransparentMat("M_ChimneyGlass", new Color(0.62f, 0.68f, 0.7f, 0.10f), 0.7f); // 굴뚝 내열 유리 구간 (광택을 낮춰 안이 보인다; 알파는 셰이더에서 한 번만 곱하므로 0.10)
        M["doorRust"] = TexMat("M_DarkRustDoor", "rusty_metal_02", new Color(0.36f, 0.33f, 0.3f), 0.28f, 0.45f);

        // 바닥·천장
        Box("Floor", root, new Vector3(0, -0.1f, 0), new Vector3(10.3f, 0.2f, 8.3f), M["floor"], 2.5f);
        Box("Ceiling", root, new Vector3(0, 3.1f, 0), new Vector3(10.3f, 0.2f, 8.3f), M["dirty"], 2.5f);
        Box("GenFloor", root, new Vector3(7.65f, -0.1f, 0), new Vector3(5f, 0.2f, 5.3f), M["floor"], 2.5f);
        Box("GenCeiling", root, new Vector3(7.65f, 3.1f, 0), new Vector3(5f, 0.2f, 5.3f), M["dirty"], 2.5f);
        Box("CorridorFloor", root, new Vector3(0, -0.1f, 8.15f), new Vector3(2.3f, 0.2f, 8f), M["floor"], 2.5f);
        Box("CorridorCeiling", root, new Vector3(0, 2.7f, 8.15f), new Vector3(2.3f, 0.2f, 8f), M["dirty"], 2.5f);

        // 벽: 거실(가운데), 발전기실(동쪽), 복도(북쪽). 복도 동쪽 벽(z=8)에는 방폭문 구멍
        var mainWalls = RoomWalls(root, -5, -4, 10, 8, 3, t, M["wall"], Solid, new[] { DoorOp(5, 2.0f, 2.5f) }, Solid, new[] { DoorOp(4, 1.3f, 2.2f) });
        RoomWalls(root, 5, -2.5f, 5, 5, 3, t, M["wall"], Solid, Solid, null, Solid);
        RoomWalls(root, -1, 4, 2, 8, 2.6f, t, M["cracked"], null, Solid, Solid, new[] { DoorOp(4, 1.6f, 2.2f) });
        BuildGenRoomDoor(mainWalls[3], 4f - t / 2, 1.3f, 2.2f, t); // 발전기실 입구(동쪽 벽 z 0, 폭 1.3·높이 2.2)의 미닫이 철문

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
        // 화로: 거실 북쪽 벽 서편 (침대와 복도 입구 사이, 벽에 붙임). 난수를 쓰지 않아 다른 배치는 그대로다
        BuildFurnace(root, new Vector3(-2.55f, 0, 3.45f));

        // 물통
        Prim(PrimitiveType.Cylinder, "WaterBarrel1", root, new Vector3(-4.4f, 0.45f, -1.3f), new Vector3(0.55f, 0.45f, 0.55f), M["paintBlue"], null, true);
        Prim(PrimitiveType.Cylinder, "WaterBarrel2", root, new Vector3(-4.35f, 0.45f, -0.6f), new Vector3(0.55f, 0.45f, 0.55f), M["paintBlue"], null, true);
        // 소품 (Poly Haven CC0 모델): 군용 상자 위 구급함, 제작대 옆 공구함, 발전기실 연료통, 침대 머리맡 벽의 방독면
        PolyProp("old_military_crate", root, new Vector3(4.25f, 0, -1.65f), 88, 0.3f);
        PolyProp("medical_box", root, new Vector3(4.2f, 0.3f, -1.95f), 75, 0.35f, 0.2f, false);
        PolyProp("metal_tool_chest", root, new Vector3(1.65f, 0, 3.6f), 180, 0.35f, 0.4f);
        PolyProp("metal_jerrycan", root, new Vector3(6.0f, 0, -1.95f), 25, 0.45f, 0.3f);
        PolyProp("metal_jerrycan", root, new Vector3(6.45f, 0, -2.05f), -10, 0.45f, 0.3f);
        PolyProp("old_gas_mask", root, new Vector3(-4.78f, 1.95f, 3.05f), 90, 0.35f, 0, false); // 침대 머리맡 (서쪽 벽 북쪽 끝, 베개 위)
        // 방독면이 걸려 있던 자리: 3시 31분에 멈춘 벽시계(위)와 삼일은행 달력(아래) (GameSceneBuilder.ShelterWall.cs)
        BuildWallClock(root, new Vector3(-4.85f, 2.1f, 0.55f), 90f);
        BuildCalendar(root, new Vector3(-4.85f, 1.42f, 0.55f), 90f);

        // 출입구 (복도 끝 철문)
        var exit = Group("Exit", root, new Vector3(0, 0, 11.82f));
        var door = Box("SteelDoor", exit, new Vector3(0, 1.1f, 0), new Vector3(1.2f, 2.2f, 0.12f), M["metal"], 1.2f);
        door.AddComponent<ShelterExit>();
        HandWheel(exit, new Vector3(0, 1.15f, -0.1f), -1f); // 붉은 핸드휠 (연출이 "Wheel"을 돌린다, 링 반지름 0.2)
        BulkheadDoor(exit, door.transform, 1.2f, 2.2f, 0.12f, -1f, false); // 격벽식 철문 꾸밈 (GameSceneBuilder.Doors.cs)
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

        UnbatchInteractables();
        Debug.Log($"[GameSceneBuilder] Shelter: 같은 면에 겹친 상자 {FixCoplanarFaces()}곳을 고침");
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

    // 제작대: 어두운 나무판은 어두운 방에서 잘 안 보인다는 피드백에 따라 상판·선반·공구판을 밝은 강철로 만든다
    static void BuildWorkbench(Transform parent, Vector3 pos)
    {
        var g = Group("Workbench", parent, pos);
        Box("Top", g, new Vector3(0, 0.9f, 0), new Vector3(2.2f, 0.08f, 0.8f), M["steel"], 1.2f, false);
        foreach (var x in new[] { -1.02f, 1.02f })
            foreach (var z in new[] { -0.34f, 0.34f })
                Box("Leg", g, new Vector3(x, 0.43f, z), new Vector3(0.06f, 0.86f, 0.06f), M["metal"], 1f, false);
        Box("Shelf", g, new Vector3(0, 0.25f, 0), new Vector3(2.04f, 0.04f, 0.7f), M["steel"], 1.2f, false); // 다리 바깥면보다 안쪽에서 끝난다
        Box("Pegboard", g, new Vector3(0, 1.65f, 0.36f), new Vector3(2.2f, 1.1f, 0.03f), M["steel"], 1.2f, false);
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

    // 창고: 녹슨 강철 양문 수납장 (StorageBox). 바깥 가장자리에 경첩이 달린 두 문짝이 양옆으로 열리고 안의 선반 세 칸과 보관품이 보인다.
    // 몸체(앞이 열린 상자)·선반·보관품은 정적, 문짝은 경첩 축(DoorL·DoorR) 아래 비정적. 콜라이더는 몸체 하나 ([F] 대상).
    // 문짝 뒷면은 몸체 앞면보다 1.2cm 앞에 있어 닫혀도 겹쳐 깜빡이지 않는다
    static void BuildStorage(Transform parent, Vector3 pos)
    {
        var g = Group("Storage", parent, pos);
        var steel = M["metal"];
        var dark = M["tire"];
        const float W = 1.7f, H = 1.9f, D = 0.5f, wall = 0.03f, leafT = 0.03f;
        // 몸체: 바닥·천장·옆판 둘·뒤판 (뒤판은 옆판 안쪽에 끼운다)
        Box("Bottom", g, new Vector3(0, wall / 2, 0), new Vector3(W, wall, D), steel, 1.2f, false);
        Box("Top", g, new Vector3(0, H - wall / 2, 0), new Vector3(W, wall, D), steel, 1.2f, false);
        Box("SideL", g, new Vector3(-W / 2 + wall / 2, H / 2, 0), new Vector3(wall, H - wall * 2, D), steel, 1.2f, false);
        Box("SideR", g, new Vector3(W / 2 - wall / 2, H / 2, 0), new Vector3(wall, H - wall * 2, D), steel, 1.2f, false);
        Box("Back", g, new Vector3(0, H / 2, -D / 2 + wall / 2), new Vector3(W - wall * 2, H - wall * 2, wall), M["dirty"], 1.2f, false);
        // 선반 세 칸 (앞을 3cm 들인다)
        foreach (var y in new[] { 0.48f, 0.93f, 1.38f })
            Box("Shelf", g, new Vector3(0, y, -0.015f), new Vector3(W - wall * 2, 0.025f, D - 0.06f), steel, 1.2f, false);
        // 보관품. 맨 아래: 연료통(붉은 깡통)과 접은 담요
        Box("JerryCan", g, new Vector3(-0.5f, wall + 0.17f, -0.05f), new Vector3(0.3f, 0.34f, 0.14f), M["paintRed"], 1f, false, new Vector3(0, -8, 0));
        Prim(PrimitiveType.Cylinder, "CanCap", g, new Vector3(-0.42f, wall + 0.36f, -0.05f), new Vector3(0.06f, 0.02f, 0.06f), dark);
        Box("Blanket", g, new Vector3(0.35f, wall + 0.07f, -0.06f), new Vector3(0.5f, 0.14f, 0.3f), M["blanket"], 1f, false, new Vector3(0, 4, 0));
        // 둘째 칸: 통조림 두 줄, 물병 둘
        for (int i = 0; i < 6; i++)
            Prim(PrimitiveType.Cylinder, "Can", g, new Vector3(-0.7f + i * 0.12f, 0.48f + 0.0125f + 0.055f, -0.1f + (i % 2) * 0.12f), new Vector3(0.09f, 0.055f, 0.09f), steel);
        for (int i = 0; i < 2; i++)
            Prim(PrimitiveType.Capsule, "Bottle", g, new Vector3(0.3f + i * 0.16f, 0.48f + 0.0125f + 0.11f, -0.05f), new Vector3(0.08f, 0.11f, 0.08f), M["glass"]);
        // 셋째 칸: 상자 둘, 쇠 깡통
        Box("Box", g, new Vector3(-0.42f, 0.93f + 0.0125f + 0.11f, -0.04f), new Vector3(0.34f, 0.22f, 0.26f), M["cloth"], 1f, false, new Vector3(0, 6, 0));
        Box("Box", g, new Vector3(0.1f, 0.93f + 0.0125f + 0.09f, -0.06f), new Vector3(0.28f, 0.18f, 0.22f), M["cloth"], 1f, false, new Vector3(0, -10, 0));
        Box("Tin", g, new Vector3(0.55f, 0.93f + 0.0125f + 0.05f, -0.02f), new Vector3(0.2f, 0.1f, 0.14f), steel, 1f, false);
        // 맨 위: 깡통 넷과 천 꾸러미
        for (int i = 0; i < 4; i++)
            Prim(PrimitiveType.Cylinder, "Can", g, new Vector3(-0.55f + i * 0.14f, 1.38f + 0.0125f + 0.055f, -0.06f), new Vector3(0.09f, 0.055f, 0.09f), steel);
        Box("Bundle", g, new Vector3(0.4f, 1.38f + 0.0125f + 0.08f, -0.05f), new Vector3(0.36f, 0.16f, 0.24f), M["cloth"], 1f, false, new Vector3(0, 12, 0));
        // 문짝 둘: 바깥 가장자리 앞 모서리의 경첩 축에서 안쪽으로 뻗는다 (가운데에 1.6cm 틈). StorageBox가 축을 돌려 양옆으로 연다
        float leafW = W / 2 - 0.002f, leafH = H - 0.02f;
        var sb = g.gameObject.AddComponent<StorageBox>();
        foreach (int s in new[] { -1, 1 })
        {
            var hinge = Group(s < 0 ? "DoorL" : "DoorR", g, new Vector3(s * (W / 2 + 0.006f), H / 2, D / 2 + leafT / 2 + 0.012f));
            var leaf = Box("Leaf", hinge, new Vector3(-s * leafW / 2, 0, 0), new Vector3(leafW, leafH, leafT), steel, 1.2f, false, null, false).transform;
            // 앞면: 환기 살 넷(위), 세로 손잡이(가운데 쪽)와 받침, 바깥 가장자리 리벳, 아래 긁힌 발판 띠
            for (int i = 0; i < 4; i++)
                Box("Vent", leaf, new Vector3(0, leafH * 0.3f - i * 0.06f, leafT / 2 + 0.002f), new Vector3(0.24f, 0.02f, 0.008f), dark, 1f, false, null, false);
            Prim(PrimitiveType.Cylinder, "Handle", leaf, new Vector3(-s * (leafW / 2 - 0.09f), -0.02f, leafT / 2 + 0.035f), new Vector3(0.026f, 0.08f, 0.026f), dark);
            foreach (float y in new[] { -0.09f, 0.05f })
                Prim(PrimitiveType.Cylinder, "HandlePost", leaf, new Vector3(-s * (leafW / 2 - 0.09f), y, leafT / 2 + 0.018f), new Vector3(0.022f, 0.018f, 0.022f), dark, new Vector3(90, 0, 0));
            for (int i = 0; i < 5; i++)
                Prim(PrimitiveType.Sphere, "Rivet", leaf, new Vector3(s * (leafW / 2 - 0.05f), -leafH / 2 + 0.15f + i * (leafH - 0.3f) / 4f, leafT / 2 + 0.004f), new Vector3(0.016f, 0.016f, 0.01f), dark);
            Box("Kick", leaf, new Vector3(0, -leafH / 2 + 0.12f, leafT / 2 + 0.001f), new Vector3(leafW - 0.06f, 0.2f, 0.006f), M["dirty"], 1f, false, null, false);
            // 경첩 셋 (바깥 가장자리, 축 위)
            for (int i = 0; i < 3; i++)
                Prim(PrimitiveType.Cylinder, "Hinge", hinge, new Vector3(0, -leafH / 2 + 0.2f + i * (leafH - 0.4f) / 2f, 0), new Vector3(0.032f, 0.05f, 0.032f), dark);
            if (s < 0) sb.doorL = hinge; else sb.doorR = hinge;
        }
        var col = g.gameObject.AddComponent<BoxCollider>();
        col.center = new Vector3(0, H / 2, 0);
        col.size = new Vector3(W, H, D + 0.08f);
    }

    // 선반 (장식용 깡통·물병 포함). 도시 건물에서도 쓴다
    static Transform BuildShelf(Transform parent, Vector3 pos, float yaw, bool stocked)
    {
        var g = Group("Shelf", parent, pos, yaw);
        // 판자마다 콜라이더를 둔다. 선반 전체를 감싸는 상자 하나로 두면 선반 위 아이템이
        // 바닥 찾기 Raycast(시작점이 상자 안이라 감지 못함)에 걸려 맨 아래 판자 밑으로 떨어져 숨는다.
        Box("SideL", g, new Vector3(-0.78f, 0.9f, 0), new Vector3(0.04f, 1.8f, 0.45f), M["metal"], 1f, true);
        Box("SideR", g, new Vector3(0.78f, 0.9f, 0), new Vector3(0.04f, 1.8f, 0.45f), M["metal"], 1f, true);
        // 판자는 옆판 사이에 끼우고 앞을 1cm 들인다 (옆판 바깥면·앞면과 같은 면이 되면 지지직 깜빡인다)
        foreach (var y in new[] { 0.1f, 0.6f, 1.1f, 1.6f })
            Box("Board", g, new Vector3(0, y, -0.005f), new Vector3(1.52f, 0.03f, 0.43f), M["wood"], 1f, true);
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

    // 발전기 (디젤 발전기, 사실적으로): 쇠 스키드 위에 검은 주철 엔진 블록·가로누운 원통형 발전부·녹슨 붉은 연료통·냉각 팬 격자·계기판.
    // 엔진 옆구리(앞면 +Z, 거실 쪽)의 철창 안에는 배기관이 벌겋게 달아오르는 연소실이 보이고(Generator가 발광을 바꾼다),
    // 배기관은 머플러를 지나 위로 뻗어 끝이 열려 있다(연기). 몸체는 Vibe 아래에 두어 돌 때 떨리고, 콜라이더는 발전기 전체 하나.
    // Dead by Daylight의 발전기 분위기를 참고했지만 모양은 독자적으로 지었다 (상자 엔진·가로누운 발전부·세로 배기관·독립 계기판)
    static void BuildGenerator(Transform parent, Vector3 pos)
    {
        var g = Group("Generator", parent, pos, -90);
        var vibe = Group("Vibe", g, Vector3.zero);
        var iron = M["genIron"];
        var frame = M["genFrame"];
        var dark = M["tire"];
        var rust = M["metal"];

        // 스키드: 두 레일 + 가로대 + 고무 발 + 기름받이 판
        foreach (var z in new[] { -0.46f, 0.46f }) Box("Rail", vibe, new Vector3(0, 0.06f, z), new Vector3(1.7f, 0.08f, 0.08f), frame, 1f, false);
        foreach (var x in new[] { -0.8f, 0.8f }) Box("Cross", vibe, new Vector3(x, 0.06f, 0), new Vector3(0.08f, 0.08f, 0.84f), frame, 1f, false);
        foreach (var x in new[] { -0.78f, 0.78f })
            foreach (var z in new[] { -0.44f, 0.44f })
                Prim(PrimitiveType.Cylinder, "Foot", vibe, new Vector3(x, 0.012f, z), new Vector3(0.09f, 0.012f, 0.09f), dark);
        Box("Tray", vibe, new Vector3(0, 0.105f, 0), new Vector3(1.6f, 0.02f, 0.9f), iron, 1f, false);

        // 엔진 블록 (x -0.25): 몸통은 뒤쪽, 앞면은 철창 구멍(x -0.41~-0.09, y 0.30~0.50)을 남기고 네 조각으로 쌓는다
        const float bx = -0.25f;
        Box("Block", vibe, new Vector3(bx, 0.38f, -0.1f), new Vector3(0.62f, 0.52f, 0.36f), iron, 1f, false);
        Box("BlockL", vibe, new Vector3(bx - 0.235f, 0.38f, 0.18f), new Vector3(0.15f, 0.52f, 0.2f), iron, 1f, false);
        Box("BlockR", vibe, new Vector3(bx + 0.235f, 0.38f, 0.18f), new Vector3(0.15f, 0.52f, 0.2f), iron, 1f, false);
        Box("BlockLow", vibe, new Vector3(bx, 0.21f, 0.18f), new Vector3(0.32f, 0.18f, 0.2f), iron, 1f, false);
        Box("BlockHigh", vibe, new Vector3(bx, 0.57f, 0.18f), new Vector3(0.32f, 0.14f, 0.2f), iron, 1f, false);
        // 철창 안: 벌건 뒷판 + 달아오르는 배기관 가지 셋 + 세로 살과 테두리
        var glowGo = Box("Firebox", vibe, new Vector3(bx, 0.4f, 0.09f), new Vector3(0.3f, 0.19f, 0.01f), M["genGlow"], 1f, false, null, false);
        glowGo.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        var manifold = new List<Renderer>();
        foreach (var x in new[] { -0.1f, 0f, 0.1f })
        {
            var runner = Prim(PrimitiveType.Cylinder, "Runner", vibe, new Vector3(bx + x, 0.4f, 0.16f), new Vector3(0.05f, 0.1f, 0.05f), M["genHot"]);
            manifold.Add(runner.GetComponent<Renderer>());
        }
        for (int i = 0; i < 5; i++) Box("Bar", vibe, new Vector3(bx - 0.12f + i * 0.06f, 0.4f, 0.27f), new Vector3(0.012f, 0.2f, 0.012f), dark, 1f, false);
        Box("BarTop", vibe, new Vector3(bx, 0.495f, 0.27f), new Vector3(0.3f, 0.012f, 0.012f), dark, 1f, false);
        Box("BarBottom", vibe, new Vector3(bx, 0.305f, 0.27f), new Vector3(0.3f, 0.012f, 0.012f), dark, 1f, false);

        // 실린더 헤드·밸브 커버·볼트·오일 캡, 공기 여과기 (앞쪽 위)
        Box("Head", vibe, new Vector3(bx, 0.7f, -0.05f), new Vector3(0.56f, 0.12f, 0.42f), iron, 1f, false);
        Box("ValveCover", vibe, new Vector3(bx, 0.815f, -0.05f), new Vector3(0.46f, 0.11f, 0.3f), iron, 1f, false);
        for (int i = 0; i < 3; i++)
            foreach (var z in new[] { -0.15f, 0.05f })
                Prim(PrimitiveType.Cylinder, "Bolt", vibe, new Vector3(bx - 0.16f + i * 0.16f, 0.878f, z), new Vector3(0.022f, 0.008f, 0.022f), dark);
        Prim(PrimitiveType.Cylinder, "OilCap", vibe, new Vector3(bx + 0.15f, 0.885f, -0.17f), new Vector3(0.05f, 0.015f, 0.05f), dark);
        Prim(PrimitiveType.Cylinder, "AirFilter", vibe, new Vector3(bx + 0.02f, 0.98f, 0.08f), new Vector3(0.2f, 0.12f, 0.2f), iron, new Vector3(0, 0, 90));
        Prim(PrimitiveType.Cylinder, "FilterCap", vibe, new Vector3(bx - 0.11f, 0.98f, 0.08f), new Vector3(0.21f, 0.01f, 0.21f), dark, new Vector3(0, 0, 90));
        Prim(PrimitiveType.Cylinder, "Intake", vibe, new Vector3(bx + 0.12f, 0.93f, 0.08f), new Vector3(0.05f, 0.06f, 0.05f), iron);

        // 발전부: 플라이휠 덮개 + 가로누운 원통(냉각 리브·끝 뚜껑·환기 홈·접속함) + 받침
        Prim(PrimitiveType.Cylinder, "Flywheel", vibe, new Vector3(0.1f, 0.42f, 0), new Vector3(0.5f, 0.05f, 0.5f), iron, new Vector3(0, 0, 90));
        Prim(PrimitiveType.Cylinder, "Alternator", vibe, new Vector3(0.45f, 0.42f, 0), new Vector3(0.46f, 0.25f, 0.46f), iron, new Vector3(0, 0, 90));
        foreach (var x in new[] { 0.3f, 0.45f, 0.6f }) Prim(PrimitiveType.Cylinder, "Rib", vibe, new Vector3(x, 0.42f, 0), new Vector3(0.49f, 0.012f, 0.49f), iron, new Vector3(0, 0, 90));
        Prim(PrimitiveType.Cylinder, "AltCap", vibe, new Vector3(0.72f, 0.42f, 0), new Vector3(0.4f, 0.02f, 0.4f), dark, new Vector3(0, 0, 90));
        for (int i = 0; i < 3; i++) Box("Vent", vibe, new Vector3(0.745f, 0.36f + i * 0.06f, 0), new Vector3(0.01f, 0.012f, 0.22f), rust, 1f, false);
        Box("Cradle", vibe, new Vector3(0.45f, 0.17f, 0), new Vector3(0.4f, 0.1f, 0.5f), frame, 1f, false);
        Box("JBox", vibe, new Vector3(0.5f, 0.69f, -0.12f), new Vector3(0.16f, 0.1f, 0.12f), dark, 1f, false);

        // 연료통 (발전부 위): 녹슨 붉은 통 + 띠 둘 + 주입구 뚜껑 + 눈금 유리관 + 다리
        Box("Tank", vibe, new Vector3(0.4f, 0.9f, 0), new Vector3(0.7f, 0.24f, 0.44f), M["paintRed"], 1f, false);
        foreach (var x in new[] { 0.2f, 0.6f }) Box("Strap", vibe, new Vector3(x, 0.9f, 0), new Vector3(0.03f, 0.26f, 0.47f), dark, 1f, false);
        Prim(PrimitiveType.Cylinder, "FillerNeck", vibe, new Vector3(0.25f, 1.035f, 0.1f), new Vector3(0.09f, 0.015f, 0.09f), iron);
        Prim(PrimitiveType.Cylinder, "FillerCap", vibe, new Vector3(0.25f, 1.06f, 0.1f), new Vector3(0.08f, 0.012f, 0.08f), dark);
        Box("SightGlass", vibe, new Vector3(0.6f, 0.9f, 0.225f), new Vector3(0.02f, 0.16f, 0.01f), M["glass"], 1f, false);
        foreach (var x in new[] { 0.15f, 0.65f })
            foreach (var z in new[] { -0.15f, 0.15f })
                Box("TankLeg", vibe, new Vector3(x, 0.715f, z), new Vector3(0.04f, 0.13f, 0.04f), frame, 1f, false);

        // 냉각 팬 덮개 (엔진 끝): 네모 틀 안에 검은 라디에이터 판, 그 앞에 팬(Generator가 돌린다), 맨 앞에 가로 살
        const float sx = -0.86f;
        Box("ShroudT", vibe, new Vector3(sx, 0.78f, 0), new Vector3(0.08f, 0.06f, 0.66f), frame, 1f, false);
        Box("ShroudB", vibe, new Vector3(sx, 0.15f, 0), new Vector3(0.08f, 0.06f, 0.66f), frame, 1f, false);
        foreach (var z in new[] { -0.3f, 0.3f }) Box("ShroudS", vibe, new Vector3(sx, 0.465f, z), new Vector3(0.08f, 0.57f, 0.06f), frame, 1f, false);
        Prim(PrimitiveType.Cylinder, "Radiator", vibe, new Vector3(sx + 0.035f, 0.465f, 0), new Vector3(0.52f, 0.004f, 0.52f), dark, new Vector3(0, 0, 90));
        var fan = Group("Fan", vibe, new Vector3(sx - 0.012f, 0.465f, 0));
        Prim(PrimitiveType.Cylinder, "Hub", fan, Vector3.zero, new Vector3(0.09f, 0.015f, 0.09f), dark, new Vector3(0, 0, 90));
        for (int i = 0; i < 6; i++)
        {
            var blade = Group("Blade", fan, Vector3.zero);
            blade.localRotation = Quaternion.AngleAxis(i * 60f, Vector3.right) * Quaternion.AngleAxis(32f, Vector3.up);
            Box("BladeMesh", blade, new Vector3(0, 0.14f, 0), new Vector3(0.008f, 0.2f, 0.07f), rust, 1f, false);
        }
        for (int i = 0; i < 6; i++) Box("Slat", vibe, new Vector3(sx - 0.05f, 0.21f + i * 0.09f, 0), new Vector3(0.01f, 0.012f, 0.56f), dark, 1f, false);
        Box("WarnPlate", vibe, new Vector3(sx - 0.045f, 0.78f, 0), new Vector3(0.004f, 0.04f, 0.4f), M["blastStripe"], 1f, false);

        // 배기: 블록 위 뒤쪽에서 나와 머플러를 지나 위로. 지하라 연기를 방에 풀지 않고 굴뚝이 천장을 뚫고 나간다.
        // 떨리는 몸체(vibe)의 관은 주름 이음새까지, 그 위 굴뚝은 정적(g). 굴뚝 중간 두 군데는 내열 유리 구간이라 안에서 연기가 올라가는 것이 보인다 (ChimneySmoke, Generator가 양을 조절)
        const float ex = bx - 0.2f, ez = -0.2f;
        Prim(PrimitiveType.Cylinder, "ExhaustElbow", vibe, new Vector3(ex, 0.72f, ez), new Vector3(0.08f, 0.09f, 0.08f), rust);
        Prim(PrimitiveType.Cylinder, "Muffler", vibe, new Vector3(ex, 1.02f, ez), new Vector3(0.2f, 0.17f, 0.2f), rust);
        Prim(PrimitiveType.Cylinder, "ExhaustPipe", vibe, new Vector3(ex, 1.5f, ez), new Vector3(0.08f, 0.32f, 0.08f), rust);
        for (int i = 0; i < 5; i++)
            Prim(PrimitiveType.Cylinder, "Bellows", vibe, new Vector3(ex, 1.83f + i * 0.035f, ez), new Vector3(i % 2 == 0 ? 0.1f : 0.086f, 0.012f, i % 2 == 0 ? 0.1f : 0.086f), dark);
        var chimney = Group("Chimney", g, new Vector3(ex, 0, ez));
        Prim(PrimitiveType.Cylinder, "Stack1", chimney, new Vector3(0, 2.08f, 0), new Vector3(0.08f, 0.1f, 0.08f), rust);
        foreach (float cy in new[] { 2.19f, 2.49f, 2.69f, 2.93f })
            Prim(PrimitiveType.Cylinder, "Collar", chimney, new Vector3(0, cy, 0), new Vector3(0.1f, 0.016f, 0.1f), dark);
        Prim(PrimitiveType.Cylinder, "SightGlass1", chimney, new Vector3(0, 2.34f, 0), new Vector3(0.078f, 0.14f, 0.078f), M["chimneyGlass"]);
        Prim(PrimitiveType.Cylinder, "Stack2", chimney, new Vector3(0, 2.59f, 0), new Vector3(0.08f, 0.09f, 0.08f), rust);
        Prim(PrimitiveType.Cylinder, "SightGlass2", chimney, new Vector3(0, 2.81f, 0), new Vector3(0.078f, 0.11f, 0.078f), M["chimneyGlass"]);
        Prim(PrimitiveType.Cylinder, "Stack3", chimney, new Vector3(0, 2.965f, 0), new Vector3(0.08f, 0.03f, 0.08f), rust);
        Prim(PrimitiveType.Cylinder, "Flange", chimney, new Vector3(0, 2.985f, 0), new Vector3(0.26f, 0.012f, 0.26f), rust);
        for (int i = 0; i < 4; i++)
        {
            float a = i * 90f * Mathf.Deg2Rad;
            Prim(PrimitiveType.Cylinder, "Bolt", chimney, new Vector3(Mathf.Cos(a) * 0.1f, 2.974f, Mathf.Sin(a) * 0.1f), new Vector3(0.018f, 0.008f, 0.018f), dark);
        }
        Prim(PrimitiveType.Cylinder, "Soot", chimney, new Vector3(0, 2.994f, 0), new Vector3(0.6f, 0.002f, 0.6f), M["oilStain"]);
        // 굴뚝 안의 연기: 관 아래에서 천장까지 0.5m/s로 오르며 유리 구간을 지난다
        var smokeGo = new GameObject("ChimneySmoke");
        smokeGo.transform.SetParent(chimney, false);
        smokeGo.transform.localPosition = new Vector3(0, 1.95f, 0);
        smokeGo.transform.localRotation = Quaternion.Euler(-90, 0, 0);
        var smoke = smokeGo.AddComponent<ParticleSystem>();
        var sm = smoke.main;
        sm.startLifetime = new ParticleSystem.MinMaxCurve(1.9f, 2.2f);
        sm.startSpeed = new ParticleSystem.MinMaxCurve(0.46f, 0.56f);
        sm.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.12f);
        sm.startColor = new ParticleSystem.MinMaxGradient(new Color(0.62f, 0.62f, 0.64f, 0.85f), new Color(0.82f, 0.82f, 0.84f, 0.7f)); // 어두운 관 속이라 밝은 회색이어야 유리 너머로 읽힌다
        sm.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
        sm.gravityModifier = -0.01f;
        sm.simulationSpace = ParticleSystemSimulationSpace.Local;
        sm.maxParticles = 200;
        var se = smoke.emission;
        se.rateOverTime = 0f;
        var ss = smoke.shape;
        ss.shapeType = ParticleSystemShapeType.Circle;
        ss.radius = 0.03f;
        ss.radiusThickness = 1f;
        var sol = smoke.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.7f, 1, 1.3f));
        var scol = smoke.colorOverLifetime;
        scol.enabled = true;
        var sgrad = new Gradient();
        sgrad.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(1f, 0.85f), new GradientAlphaKey(0f, 1f) });
        scol.color = sgrad;
        var sn = smoke.noise;
        sn.enabled = true;
        sn.strength = 0.025f;
        sn.frequency = 1.6f;
        var sr = smokeGo.GetComponent<ParticleSystemRenderer>();
        sr.renderMode = ParticleSystemRenderMode.Billboard;
        sr.material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/Effects/M_VfxDust.mat");
        sr.shadowCastingMode = ShadowCastingMode.Off;

        // 계기판 (발전부 앞, 거실 쪽): 기둥 위 검은 상자에 계기 둘(바늘은 Generator가 돌린다)·빨간 정지 버튼·초록 표시등·열쇠 스위치·차단기 레버·경고 띠
        const float px = 0.55f;
        Box("PanelPost", vibe, new Vector3(px, 0.4f, 0.42f), new Vector3(0.05f, 0.6f, 0.05f), frame, 1f, false);
        Box("Panel", vibe, new Vector3(px, 0.86f, 0.42f), new Vector3(0.38f, 0.32f, 0.08f), dark, 1f, false);
        var needles = new Transform[2];
        for (int i = 0; i < 2; i++)
        {
            float dx = i == 0 ? -0.1f : 0.06f;
            Prim(PrimitiveType.Cylinder, "GaugeRim", vibe, new Vector3(px + dx, 0.91f, 0.463f), new Vector3(0.11f, 0.003f, 0.11f), rust, new Vector3(90, 0, 0));
            Prim(PrimitiveType.Cylinder, "Gauge", vibe, new Vector3(px + dx, 0.91f, 0.467f), new Vector3(0.1f, 0.003f, 0.1f), M["gaugeFace"], new Vector3(90, 0, 0));
            needles[i] = Group("Needle", vibe, new Vector3(px + dx, 0.91f, 0.472f));
            Box("Hand", needles[i], new Vector3(0, 0.02f, 0), new Vector3(0.004f, 0.04f, 0.002f), M["paintRed"], 1f, false, null, false);
        }
        Prim(PrimitiveType.Cylinder, "StopButton", vibe, new Vector3(px + 0.14f, 0.96f, 0.47f), new Vector3(0.035f, 0.012f, 0.035f), M["paintRed"], new Vector3(90, 0, 0));
        var lampGo = Prim(PrimitiveType.Sphere, "RunLamp", vibe, new Vector3(px + 0.14f, 0.885f, 0.465f), new Vector3(0.022f, 0.022f, 0.012f), M["radioLed"]);
        Prim(PrimitiveType.Cylinder, "KeySwitch", vibe, new Vector3(px + 0.14f, 0.81f, 0.465f), new Vector3(0.03f, 0.008f, 0.03f), dark, new Vector3(90, 0, 0));
        Box("Key", vibe, new Vector3(px + 0.14f, 0.81f, 0.478f), new Vector3(0.006f, 0.03f, 0.016f), rust, 1f, false);
        Box("Breaker", vibe, new Vector3(px - 0.13f, 0.78f, 0.47f), new Vector3(0.04f, 0.06f, 0.02f), dark, 1f, false, new Vector3(0, 0, 20));
        Box("WarnLabel", vibe, new Vector3(px, 0.735f, 0.462f), new Vector3(0.3f, 0.035f, 0.004f), M["blastStripe"], 1f, false);

        // 전선: 접속함에서 벽의 배전함으로 (바닥 쪽으로 내려갔다 벽을 타고 오른다), 계기판으로 들어가는 고무 전선
        Prim(PrimitiveType.Cylinder, "Conduit", vibe, new Vector3(0.5f, 0.69f, -0.5f), new Vector3(0.03f, 0.32f, 0.03f), dark, new Vector3(90, 0, 0));
        Prim(PrimitiveType.Cylinder, "ConduitUp", vibe, new Vector3(0.5f, 1.05f, -0.85f), new Vector3(0.03f, 0.36f, 0.03f), dark);
        Box("WallBox", g, new Vector3(0.5f, 1.5f, -0.9f), new Vector3(0.22f, 0.28f, 0.08f), dark, 1f, false);
        Prim(PrimitiveType.Cylinder, "Cable", vibe, new Vector3(0.62f, 0.62f, 0.22f), new Vector3(0.02f, 0.13f, 0.02f), dark, new Vector3(45, 0, 0));
        Prim(PrimitiveType.Cylinder, "Cable", vibe, new Vector3(0.66f, 0.6f, 0.2f), new Vector3(0.016f, 0.14f, 0.016f), dark, new Vector3(50, 10, 0));
        // 바닥의 기름 자국
        Stain("OilStain", g, new Vector3(-0.2f, 0.006f, 0.25f), 0.45f, 0.3f, M["oilStain"], 311);

        // 불티: 철창 안에서 앞·위로 조금씩 튀어 나온다 (Generator가 뿜는 양을 바꾼다)
        var psGo = new GameObject("Embers");
        psGo.transform.SetParent(vibe, false);
        psGo.transform.localPosition = new Vector3(bx, 0.4f, 0.2f);
        psGo.transform.localRotation = Quaternion.Euler(-60, 0, 0);
        var ps = psGo.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.01f, 0.024f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.75f, 0.35f), new Color(1f, 0.4f, 0.12f));
        main.gravityModifier = -0.02f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 80;
        var emission = ps.emission;
        emission.rateOverTime = 0f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(0.26f, 0.14f, 0.02f);
        shape.randomDirectionAmount = 0.3f;
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.2f;
        noise.frequency = 0.9f;
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(new Color(1f, 0.85f, 0.5f), 0), new GradientColorKey(new Color(1f, 0.4f, 0.1f), 0.6f), new GradientColorKey(new Color(0.4f, 0.1f, 0.05f), 1) },
            new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(0, 1) });
        col.color = grad;
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0.3f));
        psGo.GetComponent<ParticleSystemRenderer>().sharedMaterial = M["fire"];

        var colBox = g.gameObject.AddComponent<BoxCollider>();
        colBox.center = new Vector3(-0.05f, 0.6f, 0);
        colBox.size = new Vector3(1.9f, 1.3f, 1.0f);
        var gen = g.gameObject.AddComponent<Generator>();
        gen.vibe = vibe;
        gen.fan = fan;
        gen.needleA = needles[0];
        gen.needleB = needles[1];
        gen.chimney = smoke;
        gen.glow = glowGo.GetComponent<Renderer>();
        gen.lamp = lampGo.GetComponent<Renderer>();
        gen.manifold = manifold.ToArray();
        gen.embers = ps;
        gen.smokeDrift = new Vector3(0.12f, 0.5f, 0.05f);
        var hum = g.gameObject.AddComponent<AudioSource>();
        hum.loop = true;
        hum.playOnAwake = false;
        hum.spatialBlend = 1;
        hum.minDistance = 2f;
        hum.maxDistance = 24f;
        hum.rolloffMode = AudioRolloffMode.Linear;
        hum.volume = 0.45f;
        gen.hum = hum;
        g.gameObject.AddComponent<AudioLowPassFilter>().cutoffFrequency = 22000f;
    }

    // 발전기실 미닫이 철문: 거실 동쪽 벽(x 5)의 구멍(c: 벽 로컬 중심, width×height) 발전기실 쪽 면(로컬 -Z)에 레일을 달고,
    // 어둡고 녹슨 강판 문짝이 로컬 -X(남쪽)로 밀려 열린다 (SlidingDoor). 벽 면·문틀과 겹치는 면이 없게 문짝은 문틀에서 2cm 띄우고,
    // 레일 받침은 벽 속에 1cm 묻으며, 장식은 문짝 면에서 1mm 이상 띄운다 (겹친 면이 부들부들 떨리는 일이 없게)
    static void BuildGenRoomDoor(Transform wall, float c, float width, float height, float t)
    {
        var mat = M["doorRust"];
        var dark = M["tire"];
        var rust = M["metal"];
        DoorCasing(wall, c, width, height, t, rust);
        var frame = Group("GenDoor", wall, new Vector3(c, 0, 0));
        frame.gameObject.AddComponent<NavMeshModifier>().ignoreFromBuild = true; // 문간이 이어지게 문은 빼고 굽는다 (막기는 NavMeshObstacle)
        // 문간을 덮는 트리거 콜라이더: 열린 문짝은 벽 뒤로 밀려 거실에서는 보이지 않으므로, 거실에서 열린 문간을 보고 F를 눌러도 닫히게 한다
        // (상호작용 시선은 트리거에도 걸린다. 문간 안에 서 있으면 시선이 트리거 안에서 시작해 걸리지 않으니 발전기 쪽 상호작용을 가리지 않는다.
        //  좀비 시야·돌팔매는 트리거를 무시한다)
        var opening = frame.gameObject.AddComponent<BoxCollider>();
        opening.isTrigger = true;
        opening.center = new Vector3(0, height / 2, 0);
        opening.size = new Vector3(width, height, t + 0.5f);
        float lw = width + 0.24f, lh = height + 0.1f, lt = 0.06f;
        const float slide = 1.55f;
        float zc = -(t / 2 + 0.024f + 0.02f + lt / 2);
        var leafGo = Box("Leaf", frame, new Vector3(0, 0.02f + lh / 2, zc), new Vector3(lw, lh, lt), mat, 1.2f, true, null, false);
        var leaf = leafGo.transform;
        foreach (int s in new[] { -1, 1 })
        {
            float z = s * (lt / 2);
            // 가로 보강 띠 셋 + 리벳, 아래 발판, 세로 손잡이 막대
            foreach (float y in new[] { -lh * 0.3f, 0.05f, lh * 0.33f })
            {
                Box("Band", leaf, new Vector3(0, y, z + s * 0.008f), new Vector3(lw - 0.08f, 0.08f, 0.014f), rust, 1f, false, null, false);
                for (int i = 0; i < 6; i++)
                    Prim(PrimitiveType.Sphere, "Rivet", leaf, new Vector3(-lw / 2 + 0.09f + i * (lw - 0.18f) / 5f, y, z + s * 0.016f), new Vector3(0.02f, 0.02f, 0.012f), dark);
            }
            Box("Kick", leaf, new Vector3(0, -lh / 2 + 0.15f, z + s * 0.007f), new Vector3(lw - 0.06f, 0.26f, 0.012f), M["dirty"], 1f, false, null, false);
            Prim(PrimitiveType.Cylinder, "Handle", leaf, new Vector3(lw / 2 - 0.16f, -0.05f, z + s * 0.046f), new Vector3(0.028f, 0.16f, 0.028f), dark);
            foreach (float y in new[] { -0.19f, 0.09f })
                Prim(PrimitiveType.Cylinder, "HandlePost", leaf, new Vector3(lw / 2 - 0.16f, y, z + s * 0.026f), new Vector3(0.024f, 0.022f, 0.024f), dark, new Vector3(90, 0, 0));
        }
        // 눈높이의 작은 철망 창: 어두운 유리 + 양면의 가로살
        Box("Window", leaf, new Vector3(0.1f, 0.42f, 0), new Vector3(0.26f, 0.2f, lt + 0.004f), M["glass"], 1f, false, null, false);
        foreach (int s in new[] { -1, 1 })
            for (int i = 0; i < 3; i++)
                Box("Mesh", leaf, new Vector3(0.1f, 0.35f + i * 0.07f, s * (lt / 2 + 0.006f)), new Vector3(0.28f, 0.01f, 0.006f), dark, 1f, false, null, false);
        // 문짝 위 롤러 걸이 둘 (발전기실 쪽 면)
        foreach (float x in new[] { -lw / 2 + 0.2f, lw / 2 - 0.2f })
        {
            Box("Hanger", leaf, new Vector3(x, lh / 2 - 0.02f, -lt / 2 - 0.008f), new Vector3(0.05f, 0.24f, 0.012f), rust, 1f, false, null, false);
            Prim(PrimitiveType.Cylinder, "Roller", leaf, new Vector3(x, lh / 2 + 0.06f, -lt / 2 - 0.028f), new Vector3(0.09f, 0.012f, 0.09f), dark, new Vector3(90, 0, 0));
        }
        // 레일 (벽에 박은 받침 넷), 바닥 안내 턱, 열린 쪽 끝의 멈춤쇠
        float railY = 0.02f + lh + 0.08f;
        float railLen = lw + slide + 0.2f;
        float railX = -slide / 2;
        Box("Rail", frame, new Vector3(railX, railY, zc), new Vector3(railLen, 0.06f, 0.1f), rust, 1f, false);
        for (int i = 0; i < 4; i++)
            Box("RailMount", frame, new Vector3(railX - railLen / 2 + 0.15f + i * (railLen - 0.3f) / 3f, railY, -t / 2 - 0.02f), new Vector3(0.08f, 0.08f, 0.06f), dark, 1f, false);
        Box("Guide", frame, new Vector3(railX, 0.015f, zc + 0.052f), new Vector3(railLen - 0.1f, 0.03f, 0.03f), dark, 1f, false);
        Box("Stop", frame, new Vector3(-slide - lw / 2 - 0.035f, 1.1f, zc), new Vector3(0.05f, 0.5f, 0.12f), rust, 1f, false);

        var obstacle = leafGo.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.size = new Vector3(lw, lh, 0.3f);
        obstacle.carving = true;
        obstacle.carveOnlyStationary = false;
        var roll = frame.gameObject.AddComponent<AudioSource>();
        roll.loop = true;
        roll.playOnAwake = false;
        roll.spatialBlend = 1;
        roll.minDistance = 1.5f;
        roll.maxDistance = 18f;
        roll.rolloffMode = AudioRolloffMode.Linear;
        roll.volume = 0.55f;
        var door = frame.gameObject.AddComponent<SlidingDoor>();
        door.leaf = leaf;
        door.obstacle = obstacle;
        door.roll = roll;
        door.slideDistance = slide;
        door.insideDirection = -wall.forward;
    }

    // 반투명 유리 (URP Lit 투명): 굴뚝 내열 유리 구간. 파티클(3000)보다 뒤(3050)에 그려 안의 연기가 비친다.
    // URP 재질 검증(인스펙터·재임포트)은 큐를 3000 + _QueueOffset 으로, 블렌드를 '보존 스펙큘러' 상태로 다시 계산한다.
    // 검증이 만들어 내는 값과 같은 상태로 저장해야 다시 뒤집히지 않는다: _QueueOffset 50, SrcBlend One + _ALPHAPREMULTIPLY_ON
    // (알파는 셰이더에서 한 번만 곱한다 — 예전 SrcAlpha + 프리멀티 조합은 알파가 두 번 곱해졌다)
    static Material TransparentMat(string name, Color color, float smoothness)
    {
        var m = TexMat(name, null, color, smoothness);
        m.SetFloat("_Surface", 1);
        m.SetFloat("_Blend", 0);
        m.SetFloat("_BlendModePreserveSpecular", 1);
        m.SetFloat("_SrcBlend", (float)BlendMode.One);
        m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0);
        m.SetFloat("_AlphaClip", 0);
        m.SetFloat("_QueueOffset", 50);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        m.SetOverrideTag("RenderType", "Transparent");
        m.SetShaderPassEnabled("DepthOnly", false);
        m.SetShaderPassEnabled("SHADOWCASTER", false);
        m.SetShaderPassEnabled("MOTIONVECTORS", false);
        m.renderQueue = (int)RenderQueue.Transparent + 50;
        EditorUtility.SetDirty(m);
        return m;
    }

    // 화로 (제련): 앞면(화구)이 -Z(거실 쪽), 뒤가 북쪽 벽(z 3.85)에 붙는다. 몸통 x ±0.56, z -0.42~0.38.
    // 벽돌 몸통 + 쇠 띠·상판, 천장까지 이어진 연통, 화구 안은 발광 머티리얼, 그림자 없는 불빛과 불티.
    // 콜라이더: 몸통(내비메시 장애물·[F] 대상) + 열린 화구 문·장작 더미(뚫고 지나가지 않게, 모두 Furnace 자식이라 [F] 대상). 철판은 없음
    static void BuildFurnace(Transform parent, Vector3 pos)
    {
        var g = Group("Furnace", parent, pos);
        var brick = M["brick"];
        Box("Plinth", g, new Vector3(0, 0.06f, -0.02f), new Vector3(1.12f, 0.12f, 0.8f), M["dirty"], 1.5f, false);
        // 몸통: 화구 구멍(x ±0.25, y 0.38~0.72)을 남기고 앞면을 네 조각으로 쌓는다
        Box("Core", g, new Vector3(0, 0.62f, 0.1f), new Vector3(1f, 1f, 0.5f), brick, 1f, false);
        Box("FrontL", g, new Vector3(-0.375f, 0.62f, -0.25f), new Vector3(0.25f, 1f, 0.2f), brick, 1f, false);
        Box("FrontR", g, new Vector3(0.375f, 0.62f, -0.25f), new Vector3(0.25f, 1f, 0.2f), brick, 1f, false);
        Box("FrontLow", g, new Vector3(0, 0.25f, -0.25f), new Vector3(0.5f, 0.26f, 0.2f), brick, 1f, false);
        Box("FrontHigh", g, new Vector3(0, 0.92f, -0.25f), new Vector3(0.5f, 0.4f, 0.2f), brick, 1f, false);
        foreach (var y in new[] { 0.3f, 0.98f })
            Box("Band", g, new Vector3(0, y, 0), new Vector3(1.03f, 0.05f, 0.73f), M["metal"], 1f, false);
        Box("TopPlate", g, new Vector3(0, 1.145f, 0), new Vector3(1.06f, 0.05f, 0.74f), M["metal"], 1f, false);

        // 화구: 쇠 테두리, 안쪽 벽의 불(발광), 바닥의 숯, 반쯤 열린 쇠문
        Box("MouthT", g, new Vector3(0, 0.74f, -0.355f), new Vector3(0.6f, 0.04f, 0.02f), M["metal"], 1f, false);
        Box("MouthB", g, new Vector3(0, 0.36f, -0.355f), new Vector3(0.6f, 0.04f, 0.02f), M["metal"], 1f, false);
        Box("MouthL", g, new Vector3(-0.28f, 0.55f, -0.355f), new Vector3(0.04f, 0.42f, 0.02f), M["metal"], 1f, false);
        Box("MouthR", g, new Vector3(0.28f, 0.55f, -0.355f), new Vector3(0.04f, 0.42f, 0.02f), M["metal"], 1f, false);
        var fire = Box("FireGlow", g, new Vector3(0, 0.55f, -0.16f), new Vector3(0.5f, 0.34f, 0.02f), M["furnaceFire"], 1f, false, null, false);
        fire.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        Box("CoalBed", g, new Vector3(0, 0.41f, -0.24f), new Vector3(0.46f, 0.06f, 0.16f), M["furnaceCoal"], 1f, false);
        Box("Coal", g, new Vector3(-0.12f, 0.46f, -0.22f), new Vector3(0.09f, 0.06f, 0.08f), M["furnaceCoal"], 1f, false, new Vector3(10, 25, 0));
        Box("Coal", g, new Vector3(0.05f, 0.47f, -0.27f), new Vector3(0.1f, 0.07f, 0.07f), M["furnaceCoal"], 1f, false, new Vector3(-8, -30, 6));
        Box("Coal", g, new Vector3(0.15f, 0.455f, -0.2f), new Vector3(0.07f, 0.05f, 0.07f), M["furnaceCoal"], 1f, false, new Vector3(0, 50, -10));
        // 경첩(화구 오른쪽 x 0.31, z -0.37)에서 120도 열린 문 (몸통 앞으로 0.4m 튀어나오므로 콜라이더를 둔다)
        Box("MouthDoor", g, new Vector3(0.435f, 0.55f, -0.587f), new Vector3(0.5f, 0.36f, 0.025f), M["metal"], 1f, true, new Vector3(0, -120, 0));
        Prim(PrimitiveType.Cylinder, "Hinge", g, new Vector3(0.31f, 0.55f, -0.37f), new Vector3(0.03f, 0.19f, 0.03f), M["metal"]);

        // 연통: 상판에서 천장(y 3.0)까지
        var flue = new Vector3(0.18f, 0, 0.12f);
        Prim(PrimitiveType.Cylinder, "FlueCollar", g, flue + new Vector3(0, 1.2f, 0), new Vector3(0.26f, 0.04f, 0.26f), M["metal"]);
        Prim(PrimitiveType.Cylinder, "Flue", g, flue + new Vector3(0, 2.085f, 0), new Vector3(0.17f, 0.915f, 0.17f), M["metal"]);
        foreach (var y in new[] { 1.8f, 2.5f })
            Prim(PrimitiveType.Cylinder, "FlueRing", g, flue + new Vector3(0, y, 0), new Vector3(0.2f, 0.02f, 0.2f), M["metal"]);
        Prim(PrimitiveType.Cylinder, "CeilingFlange", g, flue + new Vector3(0, 2.98f, 0), new Vector3(0.32f, 0.02f, 0.32f), M["metal"]);

        // 장식: 왼쪽에 쌓아 둔 장작(막대기), 오른쪽 옆면에 기대 둔 철판
        var wood = Group("Firewood", g, new Vector3(-0.82f, 0, -0.05f));
        foreach (var (x, y, yaw) in new[] { (-0.11f, 0.05f, 4f), (0f, 0.05f, -3f), (0.11f, 0.05f, 6f), (-0.055f, 0.137f, -5f), (0.055f, 0.137f, 2f), (0f, 0.224f, 8f) })
            Prim(PrimitiveType.Cylinder, "Log", wood, new Vector3(x, y, 0), new Vector3(0.1f, 0.28f, 0.1f), M["wood"], new Vector3(90, yaw, 0));
        // 장작 더미 전체를 감싸는 낮은 콜라이더 (플레이어가 통나무를 뚫고 지나가지 않게)
        var woodCol = wood.gameObject.AddComponent<BoxCollider>();
        woodCol.center = new Vector3(0, 0.14f, 0);
        woodCol.size = new Vector3(0.34f, 0.28f, 0.58f);
        Box("IronPlate", g, new Vector3(0.6f, 0.2f, 0.05f), new Vector3(0.025f, 0.38f, 0.32f), M["metal"], 1f, false, new Vector3(0, 0, 14));
        Box("IronPlate", g, new Vector3(0.64f, 0.18f, 0.08f), new Vector3(0.025f, 0.34f, 0.28f), M["metal"], 1f, false, new Vector3(0, 6, 18));

        // 불빛: 화구 앞, 그림자 없음. 깜빡임·녹일 때 밝아지는 것은 Furnace가 한다
        var light = PointLight("FireLight", g, new Vector3(0, 0.6f, -0.65f), new Color(1f, 0.48f, 0.18f), 4f, 6f);

        // 불티: 화구에서 조금씩 튀어 올라 사라진다 (녹이는 동안은 Furnace가 더 뿜는다)
        var psGo = new GameObject("Embers");
        psGo.transform.SetParent(g, false);
        psGo.transform.localPosition = new Vector3(0, 0.62f, -0.42f);
        psGo.transform.localRotation = Quaternion.Euler(-90, 0, 0);
        var ps = psGo.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.03f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.78f, 0.38f), new Color(1f, 0.4f, 0.12f));
        main.gravityModifier = -0.03f; // 뜨거운 공기를 타고 떠오른다
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 80;
        var emission = ps.emission;
        emission.rateOverTime = 3f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(0.36f, 0.06f, 0.02f);
        shape.randomDirectionAmount = 0.25f;
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.25f;
        noise.frequency = 0.8f;
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(new Color(1f, 0.85f, 0.5f), 0), new GradientColorKey(new Color(1f, 0.4f, 0.1f), 0.6f), new GradientColorKey(new Color(0.4f, 0.1f, 0.05f), 1) },
            new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(0, 1) });
        col.color = grad;
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0.3f));
        psGo.GetComponent<ParticleSystemRenderer>().sharedMaterial = M["fire"];

        // 소리: 늘 나는 탁탁 소리 + 녹일 때의 불길 소리 (클립은 Furnace가 FurnaceAudio로 만든다)
        var mouth = Group("Mouth", g, new Vector3(0, 0.55f, -0.4f));
        var crackle = LoopSource(mouth.gameObject, 0.32f, 9f);
        var roar = LoopSource(mouth.gameObject, 0.6f, 16f);

        var box = g.gameObject.AddComponent<BoxCollider>();
        box.center = new Vector3(0, 0.6f, -0.02f);
        box.size = new Vector3(1.12f, 1.2f, 0.8f);
        var furnace = g.gameObject.AddComponent<Furnace>();
        furnace.fireLight = light;
        furnace.mouthGlow = fire.GetComponent<MeshRenderer>();
        furnace.embers = ps;
        furnace.crackle = crackle;
        furnace.roar = roar;
    }

    // 3D 반복 소리 (발전기 웅웅거림과 같은 설정)
    static AudioSource LoopSource(GameObject go, float volume, float maxDistance)
    {
        var src = go.AddComponent<AudioSource>();
        src.loop = true;
        src.playOnAwake = false;
        src.spatialBlend = 1;
        src.minDistance = 1f;
        src.maxDistance = maxDistance;
        src.rolloffMode = AudioRolloffMode.Linear;
        src.volume = volume;
        return src;
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
