using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// GameSceneBuilder의 폐허 도시(City) 씬 부분
public static partial class GameSceneBuilder
{
    // ================= 폐허 도시 =================

    static readonly float[] RoadCenters = { -69f, -23f, 23f, 69f };
    static readonly Vector3 SpawnPos = new Vector3(-23f, 0.05f, -22.5f);

    static void BuildCityScene(GameObject playerPrefab, GameObject zombiePrefab, GameObject dogPrefab)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        BeginMeshes(GenDir + "/CityMeshes.asset");
        rng = new System.Random(31);

        var root = new GameObject("City").transform;
        var geo = Group("Geometry", root, Vector3.zero);
        var points = Group("SpawnPoints", root, Vector3.zero);

        for (int gx = 0; gx < 8; gx++)
            for (int gz = 0; gz < 8; gz++)
                Box("Ground", geo, new Vector3(-85 + 10.625f + gx * 21.25f, -0.1f, -85 + 10.625f + gz * 21.25f), new Vector3(21.25f, 0.2f, 21.25f), M["asphalt"], 5f);
        BuildRoadLines(geo);

        foreach (var bx in new[] { -46f, 0f, 46f })
            foreach (var bz in new[] { -46f, 0f, 46f })
                BuildBlock(geo, points, new Vector3(bx, 0, bz));

        BuildStreetProps(geo, points);
        BuildHatch(geo, new Vector3(-23f, 0, -26.5f));
        BuildBoundary(geo);

        // 도로 위 좀비 등장 지점
        for (int i = 0; i < 18; i++)
        {
            var p = RandomRoadPoint();
            if (Vector3.Distance(p, SpawnPos) < 30) { i--; continue; }
            Spawn<ZombieSpawnPoint>(points, p);
        }

        // 하늘·안개·달빛
        var moon = new GameObject("Moonlight").AddComponent<Light>();
        moon.type = LightType.Directional;
        moon.color = new Color(0.55f, 0.62f, 0.75f);
        moon.intensity = 0.9f;
        moon.shadows = LightShadows.Soft;
        moon.shadowStrength = 0.7f;
        moon.transform.rotation = Quaternion.Euler(38, -35, 0);
        RenderSettings.sun = moon;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.27f, 0.29f, 0.31f);
        RenderSettings.skybox = null;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.03f;
        RenderSettings.fogColor = new Color(0.16f, 0.17f, 0.17f);
        RenderSettings.reflectionIntensity = 0.2f;

        AddVolume(MakeProfile("CityProfile", 1.4f, -35f, new Color(0.92f, 1f, 0.95f), 0.4f, 10f));

        var player = PlacePlayer(playerPrefab, SpawnPos, 0, RenderSettings.fogColor);
        AddAshFall(player.transform);

        var director = new GameObject("CityDirector").AddComponent<CityDirector>();
        director.zombiePrefab = zombiePrefab;
        director.dogPrefab = dogPrefab;

        // 내비메시 굽기 (좀비·들개가 다닐 길)
        var surface = geo.gameObject.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Children;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.BuildNavMesh();
        string navPath = GenDir + "/CityNavMesh.asset";
        AssetDatabase.DeleteAsset(navPath);
        AssetDatabase.CreateAsset(surface.navMeshData, navPath);

        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(scene, SceneDir + "/City.unity");

        // 오클루전 컬링: 건물에 가려 안 보이는 것은 그리지 않는다 (저사양 PC 최적화)
        StaticOcclusionCulling.Compute();
        EditorSceneManager.SaveScene(scene, SceneDir + "/City.unity");
    }

    static Vector3 RandomRoadPoint()
    {
        float rc = Pick(RoadCenters) + R(-4f, 4f);
        float along = R(-74f, 74f);
        return Chance(0.5) ? new Vector3(rc, 0, along) : new Vector3(along, 0, rc);
    }

    static void BuildRoadLines(Transform geo)
    {
        var parts = new List<CombineInstance>();
        var dash = BoxMesh(new Vector3(0.15f, 0.02f, 2.4f), 1f);
        foreach (var rc in RoadCenters)
            for (float s = -76; s <= 76; s += 6)
            {
                bool nearCross = false;
                foreach (var other in RoadCenters) if (Mathf.Abs(s - other) < 7) nearCross = true;
                if (nearCross || Chance(0.2)) continue; // 지워진 차선
                parts.Add(new CombineInstance { mesh = dash, transform = Matrix4x4.TRS(new Vector3(rc, 0.005f, s), Quaternion.identity, Vector3.one) });
                parts.Add(new CombineInstance { mesh = dash, transform = Matrix4x4.TRS(new Vector3(s, 0.005f, rc), Quaternion.Euler(0, 90, 0), Vector3.one) });
            }
        CombineInto(geo, "RoadLines", parts, M["line"]);
    }

    static void BuildBlock(Transform geo, Transform points, Vector3 c)
    {
        var b = Group($"Block_{c.x}_{c.z}", geo, c);
        Box("Sidewalk", b, new Vector3(0, 0.075f, 0), new Vector3(34, 0.15f, 34), M["floor"], 3f);

        for (int ix = 0; ix < 2; ix++)
            for (int iz = 0; iz < 2; iz++)
            {
                bool trader = c.x == 0 && c.z == -46 && ix == 0 && iz == 1;
                bool nearSpawn = c.x == -46 && c.z == -46 && ix == 1 && iz == 1;
                double r = rng.NextDouble();
                if (trader || nearSpawn || r < 0.5) EnterableBuilding(b, points, ix, iz, trader);
                else if (r < 0.75) TallBuilding(b, points, ix, iz);
                else if (r < 0.9) CollapsedBuilding(b, points, ix, iz);
                else EmptyLot(b, points, ix, iz);
            }

        // 모퉁이 가로등
        foreach (var x in new[] { -16.3f, 16.3f })
            foreach (var z in new[] { -16.3f, 16.3f })
                if (Chance(0.8)) LampPost(b, new Vector3(x, 0.15f, z));

        // 인도 위 잔해, 말라 죽은 나무
        for (int i = 0; i < 3; i++)
        {
            var side = rng.Next(4);
            float along = R(-15, 15);
            var p = side == 0 ? new Vector3(along, 0.15f, -16f) : side == 1 ? new Vector3(along, 0.15f, 16f) : side == 2 ? new Vector3(-16f, 0.15f, along) : new Vector3(16f, 0.15f, along);
            if (Chance(0.5)) DeadTree(b, p);
            else RubblePile(b, p, 4, 0.8f);
        }
    }

    // 블록 안 13x13m 구역의 바깥 모서리(도로 쪽)에 붙도록 건물 크기와 위치를 정한다
    static void LotRect(int ix, int iz, out float x0, out float z0, out float w, out float d)
    {
        w = R(9.5f, 13f);
        d = R(9.5f, 13f);
        x0 = ix == 0 ? -14.5f : 14.5f - w;
        z0 = iz == 0 ? -14.5f : 14.5f - d;
    }

    static void EnterableBuilding(Transform block, Transform points, int ix, int iz, bool trader)
    {
        LotRect(ix, iz, out float x0, out float z0, out float w, out float d);
        var g = Group("Building", block, new Vector3(0, 0.15f, 0));
        const float h = 3.6f, t = 0.3f;
        var mat = Pick(M["wall"], M["cracked"], M["brick"]);

        // 바깥쪽 두 면: 하나는 문, 하나는 창문. 0=남 1=북 2=서 3=동
        int xSide = ix == 0 ? 2 : 3, zSide = iz == 0 ? 0 : 1;
        int doorSide = Chance(0.5) ? xSide : zSide;
        int winSide = doorSide == xSide ? zSide : xSide;
        float len(int side) => side < 2 ? w : d;
        var ops = new Op[4][];
        for (int s = 0; s < 4; s++) ops[s] = Solid;
        float doorC = R(2f, len(doorSide) - 2f);
        ops[doorSide] = new[] { DoorOp(doorC) };
        ops[winSide] = new[] { WinOp(len(winSide) * 0.3f), WinOp(len(winSide) * 0.7f) };
        int backSide = doorSide == xSide ? (zSide == 0 ? 1 : 0) : (xSide == 2 ? 3 : 2);
        if (Chance(0.5)) ops[backSide] = new[] { WinOp(len(backSide) * 0.5f) };

        var walls = RoomWalls(g, x0, z0, w, d, h, t, mat, ops[0], ops[1], ops[2], ops[3]);
        Box("Floor", g, new Vector3(x0 + w / 2, 0.02f, z0 + d / 2), new Vector3(w - t, 0.04f, d - t), M["floor"], 2.5f);

        // 2층 설계: 계단은 문 맞은편(늘 막힌 벽)을 따라 오른다.
        // 도시 난수와 따로 둔 난수를 써서 나머지 도시 배치(건물 종류·소품·좀비 자리)는 예전과 똑같이 나온다
        var cityRng = rng;
        rng = new System.Random(9000 + ix * 2 + iz + Mathf.RoundToInt(block.localPosition.x) * 31 + Mathf.RoundToInt(block.localPosition.z) * 1009);
        var plan = PlanUpstairs(x0, z0, w, d, t, doorSide ^ 1);
        var upRng = rng;
        rng = cityRng;

        // 층 사이 바닥판 (계단 구멍만 비움). 바깥에서는 층을 나누는 띠로 보인다
        Vector3 c0 = plan.P(plan.open, 0, 0), c1 = plan.P(plan.Top, StairWidth, 0);
        SlabWithHole(g, x0 - t / 2, x0 + w + t / 2, z0 - t / 2, z0 + d + t / 2,
            Mathf.Min(c0.x, c1.x), Mathf.Max(c0.x, c1.x), Mathf.Min(c0.z, c1.z), Mathf.Max(c0.z, c1.z), h + 0.15f, 0.3f, M["dirty"]);

        // 2층 바깥벽: 창문 구멍은 예전 겉모습 창문 자리에 낸다 (쓰는 난수 수도 같다)
        int floors = rng.Next(1, 5);
        var ops2 = UpperWindowOps(x0, z0, w, d, t, plan.junctions);
        var g2 = Group("Floor2", g, new Vector3(0, Floor2Y, 0));
        RoomWalls(g2, x0, z0, w, d, Floor2H, t, mat, ops2[0], ops2[1], ops2[2], ops2[3]);
        float roofY = Floor2Y + Floor2H;
        Box("Roof", g, new Vector3(x0 + w / 2, roofY + 0.15f, z0 + d / 2), new Vector3(w + t, 0.3f, d + t), M["dirty"], 3f);

        // 그 위층 (들어갈 수 없는 겉모습). 예전 층수에서 2층 한 층을 뺀 만큼이라 건물 높이는 거의 그대로다
        if (floors > 1)
        {
            float upperH = (floors - 1) * 3.2f;
            Box("Upper", g, new Vector3(x0 + w / 2, roofY + 0.3f + upperH / 2, z0 + d / 2), new Vector3(w + t, upperH, d + t), mat, 3f);
        }
        FacadeWindows(g, x0 - t / 2, z0 - t / 2, w + t, d + t, roofY + 0.3f, floors - 1);

        if (!trader && Chance(0.7)) AddDoor(walls[doorSide], doorC + ShiftFor(doorSide, t), 1.4f, 2.3f, Pick(M["wood"], M["metal"]));

        // 내부: 선반, 탁자/계산대, 상자, 잔해. 계단 벽 쪽 띠(계단 + 지나갈 길)는 비워 둔다
        Vector3 center = new Vector3(x0 + w / 2, 0, z0 + d / 2);
        float keep = StairWidth + 1.5f, bLo = keep + 0.65f, bHi = plan.depth - 0.65f;
        Vector3 Inside(float u, float v)
        {
            var q = new Vector3(x0 + 0.8f + u * (w - 1.6f), 0.04f, z0 + 0.8f + v * (d - 1.6f));
            float b = plan.B(q), f = (b - 0.65f) / (plan.depth - 1.3f);
            return q + plan.I * (bLo + f * (bHi - bLo) - b);
        }
        float backYaw = backSide == 0 ? 0 : backSide == 1 ? 180 : backSide == 2 ? 90 : -90;
        Vector3 backPos = backSide == 0 ? Inside(0.5f, 0) : backSide == 1 ? Inside(0.5f, 1) : backSide == 2 ? Inside(0, 0.5f) : Inside(1, 0.5f);
        var shelf = BuildShelf(g, backPos, backYaw, true);
        var shelfWorld = shelf.position;
        Spawn<ItemSpawnPoint>(points, shelfWorld + shelf.rotation * new Vector3(-0.4f, 0.65f, 0));
        Spawn<ItemSpawnPoint>(points, shelfWorld + shelf.rotation * new Vector3(0.4f, 1.15f, 0));
        Transform shelf2 = null;
        if (Chance(0.6))
        {
            shelf2 = BuildShelf(g, Vector3.Lerp(backPos, Inside(0.5f, 0.5f), 0.45f) + new Vector3(R(-1, 1), 0, R(-1, 1)), backYaw + (Chance(0.3) ? R(-25, 25) : 0), Chance(0.5));
            Spawn<ItemSpawnPoint>(points, shelf2.position + shelf2.rotation * new Vector3(0, 0.15f, 0));
        }

        // 계산대는 계단 길과 문 앞을 막지 않게 안쪽으로 당긴다
        var tableLocal = Inside(R(0.2f, 0.8f), R(0.2f, 0.8f));
        float tb = plan.B(tableLocal);
        tableLocal += plan.I * (Mathf.Clamp(tb, keep + 1.2f, plan.depth - 2.3f) - tb);
        // 두 번째 선반·잡상인 자리와 겹치지 않게 벽을 따라 옆으로 민다 (난수는 더 쓰지 않는다)
        if (shelf2 != null) tableLocal = PushAlong(tableLocal, shelf2.localPosition, 1.7f, plan);
        if (trader) tableLocal = PushAlong(tableLocal, Inside(0.5f, 0.5f), 1.6f, plan);
        var tablePos = g.TransformPoint(tableLocal);
        if (Chance(0.6))
        {
            var tg = Group("Counter", g, tableLocal, R(0, 360));
            Box("Top", tg, new Vector3(0, 0.95f, 0), new Vector3(2.2f, 0.06f, 0.7f), M["wood"], 1.2f);
            Box("Body", tg, new Vector3(0, 0.46f, 0), new Vector3(2.1f, 0.92f, 0.6f), M["wood"], 1.2f);
            Spawn<ItemSpawnPoint>(points, tablePos + Vector3.up * 1.0f);
        }
        else
        {
            var tg = Group("TableTipped", g, tableLocal, R(0, 360));
            Box("Top", tg, new Vector3(0, 0.45f, 0), new Vector3(1.4f, 0.05f, 0.8f), M["wood"], 1.2f, true, new Vector3(0, 0, 75));
            Spawn<ItemSpawnPoint>(points, tablePos + new Vector3(0.8f, 0.1f, 0.5f));
        }
        for (int i = 0, n = rng.Next(1, 4); i < n; i++)
        {
            var p = Inside(R(0, 1), R(0, 1));
            Box("Crate", g, p + Vector3.up * 0.3f, new Vector3(0.7f, 0.6f, 0.6f), M["wood"], 1f, true, new Vector3(0, R(0, 90), 0));
            if (Chance(0.5)) Spawn<ItemSpawnPoint>(points, g.TransformPoint(p) + Vector3.up * 0.65f);
        }
        for (int i = 0; i < 4; i++)
            Box("Debris", g, Inside(R(0, 1), R(0, 1)) + Vector3.up * 0.05f, new Vector3(R(0.1f, 0.5f), 0.06f, R(0.1f, 0.4f)), Pick(M["cracked"], M["wood"], M["metal"]), 1f, false, new Vector3(R(-10, 10), R(0, 180), 0));

        if (Chance(0.3) && !trader) Spawn<ZombieSpawnPoint>(points, g.TransformPoint(center));
        if (Chance(0.25) && !trader)
        {
            var l = PointLight("BrokenLight", g, center + Vector3.up * (h - 0.3f), new Color(0.75f, 0.85f, 0.8f), 5f, 8f);
            Flicker(l, 0.4f, 12f, true);
            Box("Tube", g, center + Vector3.up * (h - 0.1f), new Vector3(1.2f, 0.05f, 0.1f), M["bulb"], 1f, false);
        }
        if (trader) BuildTrader(g, Inside(0.5f, 0.5f));

        // 계단과 2층 안 (따로 둔 난수)
        rng = upRng;
        BuildStairs(g, plan);
        BuildUpstairs(g2, points, plan, trader);
        rng = cityRng;
    }

    // ---------- 2층 건물 (계단으로 올라가는 위층) ----------

    // 1층 바닥 윗면(0.04) → 2층 바닥 윗면(3.9). 계단은 폭 1.7m, 길이 6m, 약 33도 (22단, 단 높이 0.18m)
    const float Floor2Y = 3.9f, Floor2H = 3.2f;
    const float StairWidth = 1.7f, StairRun = 6f, StairRise = Floor2Y - 0.04f;
    const int StairSteps = 22;
    const float StairOpenAt = 1.6f; // 계단 시작에서 이만큼 지난 곳부터 위층 바닥에 구멍 (머리 위 2.5m 이상)
    const float HallWidth = 2.0f;   // 2층 계단 난간과 방 칸막이 사이 복도 폭

    // 계단 벽 기준 좌표: a = 계단 벽을 따라 오르는 방향, b = 벽에서 방 안쪽. 원점 = 계단 벽 안쪽 면의 구석 (건물 로컬, y=0)
    class UpPlan
    {
        public Vector3 O, R, I;
        public float yaw, yawIn;      // 로컬 X를 R / I 방향으로 돌리는 회전
        public float len, depth;      // 실내 길이(a), 깊이(b)
        public float s0, open;        // 계단 시작 a, 위층 바닥 구멍 시작 a
        public bool hasQ, qDoor;      // 방을 둘로 나누는 벽, 그 벽의 문
        public float Q, doorA, doorB, qDoorC;
        public readonly List<Vector3> junctions = new List<Vector3>(); // 칸막이·난간이 바깥벽에 닿는 자리
        public float Top => s0 + StairRun;
        public float RoomB0 => StairWidth + HallWidth + 0.075f;
        public Vector3 P(float a, float b, float y) => O + R * a + I * b + Vector3.up * y;
        public float B(Vector3 q) => Vector3.Dot(q - O, I);
    }

    // p가 from에서 minDist보다 가까우면 계단 벽 방향(a)으로 밀어낸다. 실내(0.9 ~ len-0.9) 밖으로는 나가지 않는다
    static Vector3 PushAlong(Vector3 p, Vector3 from, float minDist, UpPlan plan)
    {
        var d = p - from;
        d.y = 0;
        if (d.magnitude >= minDist) return p;
        float along = Vector3.Dot(d, plan.R);
        float a = Vector3.Dot(p - plan.O, plan.R);
        float pushed = a + (along >= 0 ? 1 : -1) * minDist - along;
        // 한쪽 끝에 막히면 반대쪽으로 민다
        if (pushed < 0.9f || pushed > plan.len - 0.9f) pushed = a + (along >= 0 ? -1 : 1) * minDist - along;
        pushed = Mathf.Clamp(pushed, 0.9f, plan.len - 0.9f);
        return p + plan.R * (pushed - a);
    }

    static UpPlan PlanUpstairs(float x0, float z0, float w, float d, float t, int side)
    {
        var p = new UpPlan();
        float ix0 = x0 + t / 2, ix1 = x0 + w - t / 2, iz0 = z0 + t / 2, iz1 = z0 + d - t / 2;
        bool fwd = Chance(0.5); // 계단이 오르는 방향
        if (side < 2)
        {
            p.I = side == 0 ? Vector3.forward : Vector3.back;
            p.R = fwd ? Vector3.right : Vector3.left;
            p.O = new Vector3(fwd ? ix0 : ix1, 0, side == 0 ? iz0 : iz1);
            p.len = w - t;
            p.depth = d - t;
        }
        else
        {
            p.I = side == 2 ? Vector3.right : Vector3.left;
            p.R = fwd ? Vector3.forward : Vector3.back;
            p.O = new Vector3(side == 2 ? ix0 : ix1, 0, fwd ? iz0 : iz1);
            p.len = d - t;
            p.depth = w - t;
        }
        p.yaw = Mathf.Atan2(-p.R.z, p.R.x) * Mathf.Rad2Deg;
        p.yawIn = Mathf.Atan2(-p.I.z, p.I.x) * Mathf.Rad2Deg;
        // 실내 길이 9.2m 이상: 아래 진입 1.4m 이상 + 계단 6m + 위 도착 1.7m 이상
        p.s0 = Mathf.Clamp((p.len - StairRun) * 0.45f, 1.4f, 2.2f);
        p.open = p.s0 + StairOpenAt;

        // 2층 칸막이: 복도 벽(계단 벽과 나란함) + 80%는 방을 둘로 나누는 벽. 문은 폭 1.6m
        p.hasQ = Chance(0.8);
        p.Q = Mathf.Lerp(4.2f, p.len - 4.2f, R(0f, 1f));
        p.doorA = p.hasQ ? p.Q - 1.125f : p.len * R(0.3f, 0.7f);
        p.doorB = p.Q + 1.125f;
        p.qDoor = p.hasQ && Chance(0.5);
        p.qDoorC = R(1.2f, p.depth - p.RoomB0 - 1.2f);

        float hall = StairWidth + HallWidth;
        p.junctions.Add(p.P(0, hall, 0));
        p.junctions.Add(p.P(p.len, hall, 0));
        p.junctions.Add(p.P(p.open, 0, 0));
        if (p.hasQ) p.junctions.Add(p.P(p.Q, p.depth, 0));
        return p;
    }

    // 2층 바깥벽 창문 구멍. 예전 겉모습 창문(FacadeWindows 한 층)과 같은 자리, 같은 수의 난수를 쓴다
    static Op[][] UpperWindowOps(float x0, float z0, float w, float d, float t, List<Vector3> junctions)
    {
        var res = new Op[4][];
        for (int side = 0; side < 4; side++)
        {
            float len = side < 2 ? w + t : d + t;
            int cols = Mathf.Max(1, Mathf.FloorToInt(len / 3f));
            var list = new List<Op>();
            for (int i = 0; i < cols; i++)
            {
                bool skip = Chance(0.08);
                float c = (i + 0.5f) * len / cols - t / 2;
                var wp = side == 0 ? new Vector3(x0 + c, 0, z0) : side == 1 ? new Vector3(x0 + c, 0, z0 + d)
                       : side == 2 ? new Vector3(x0, 0, z0 + c) : new Vector3(x0 + w, 0, z0 + c);
                // 칸막이나 난간이 닿는 자리는 창문을 내지 않는다
                foreach (var j in junctions)
                {
                    float along = side < 2 ? Mathf.Abs(j.x - wp.x) : Mathf.Abs(j.z - wp.z);
                    float across = side < 2 ? Mathf.Abs(j.z - wp.z) : Mathf.Abs(j.x - wp.x);
                    if (across < 0.6f && along < 1.0f) skip = true;
                }
                if (!skip) list.Add(new Op(c, 1.2f, 0.95f, 2.45f));
            }
            res[side] = list.ToArray();
        }
        return res;
    }

    // 직사각형 구멍(계단 구멍)이 난 판. 구멍 둘레 네 조각으로 만든다
    static void SlabWithHole(Transform g, float X0, float X1, float Z0, float Z1, float hx0, float hx1, float hz0, float hz1, float y, float thick, Material mat)
    {
        void Piece(float a0, float a1, float b0, float b1)
        {
            if (a1 - a0 < 0.01f || b1 - b0 < 0.01f) return;
            Box("Slab", g, new Vector3((a0 + a1) / 2, y, (b0 + b1) / 2), new Vector3(a1 - a0, thick, b1 - b0), mat, 3f);
        }
        Piece(X0, hx0, Z0, Z1);
        Piece(hx1, X1, Z0, Z1);
        Piece(hx0, hx1, Z0, hz0);
        Piece(hx0, hx1, hz1, Z1);
    }

    // 1층 → 2층 계단: 보이는 계단 덩어리(콜라이더 없음) + 보이지 않는 경사 판정 + 손잡이·난간
    static void BuildStairs(Transform g, UpPlan p)
    {
        const float y0 = 0.04f;
        float run = StairRun / StairSteps, rise = StairRise / StairSteps, slope = StairRise / StairRun;
        float th = Mathf.Atan2(StairRise, StairRun) * Mathf.Rad2Deg;
        float cos = Mathf.Cos(th * Mathf.Deg2Rad), sin = Mathf.Sin(th * Mathf.Deg2Rad);
        var q = Quaternion.Euler(0, p.yaw, 0);
        var tilt = new Vector3(0, p.yaw, th); // 로컬 X가 오르막을 따라 th만큼 들린다

        // 계단 단: 단 앞 모서리가 경사면 위에 오도록 바닥부터 쌓은 콘크리트 덩어리
        var steps = new List<CombineInstance>();
        for (int k = 1; k < StairSteps; k++)
            steps.Add(new CombineInstance { mesh = BoxMesh(new Vector3(run, k * rise, StairWidth), 1.5f), transform = Matrix4x4.TRS(p.P(p.s0 + (k + 0.5f) * run, StairWidth / 2, y0 + k * rise / 2), q, Vector3.one) });
        CombineInto(g, "Stairs", steps, M["dirty"]);

        // 걷는 판정: 밑이 꽉 찬 쐐기 콜라이더 + 같은 경사면의 얇은 상자 (내비메시 굽기용 보험)
        var ramp = new GameObject("StairRamp");
        ramp.transform.SetParent(g, false);
        ramp.transform.localPosition = p.P(p.s0, StairWidth / 2, y0);
        ramp.transform.localRotation = q;
        var mc = ramp.AddComponent<MeshCollider>();
        mc.sharedMesh = WedgeMesh(StairRun, StairRise, StairWidth);
        mc.convex = true;
        ramp.isStatic = true;
        float am = p.s0 + StairRun / 2, ym = y0 + StairRise / 2;
        ColliderBox("StairRampTop", g, p.P(am + sin * 0.1f, StairWidth / 2, ym - cos * 0.1f), new Vector3(StairRun / cos, 0.2f, StairWidth), tilt);

        // 열린 쪽 보이지 않는 난간 판정 (계단 옆으로 떨어지지 않게)
        float g0 = p.s0 + 0.6f, gm = (g0 + p.Top) / 2, gy = y0 + (gm - p.s0) * slope;
        ColliderBox("StairGuard", g, p.P(gm - sin * 0.5f, StairWidth + 0.03f, gy + cos * 0.5f), new Vector3((p.Top - g0) / cos, 1.0f, 0.06f), tilt);

        var rails = new List<CombineInstance>();
        void Bar(Vector3 pos, Vector3 size, Quaternion rot) => rails.Add(new CombineInstance { mesh = BoxMesh(size, 1f), transform = Matrix4x4.TRS(pos, rot, Vector3.one) });

        // 계단 손잡이: 경사 막대 + 기둥 (계단 구멍 안쪽이라 2층 바닥판과 겹치지 않는다)
        float hb = StairWidth - 0.05f, h0 = p.s0 + 0.3f, h1 = p.Top - 0.05f, hm = (h0 + h1) / 2;
        Bar(p.P(hm, hb, y0 + (hm - p.s0) * slope + 0.9f), new Vector3((h1 - h0) / cos, 0.05f, 0.05f), Quaternion.Euler(tilt));
        foreach (float a in new[] { p.s0 + 0.35f, p.s0 + 2.2f, p.s0 + 4.1f, p.Top - 0.1f })
        {
            int k = Mathf.Min(StairSteps - 1, Mathf.FloorToInt((a - p.s0) / run));
            float foot = y0 + k * rise, head = y0 + (a - p.s0) * slope + 0.9f;
            Bar(p.P(a, hb, (foot + head) / 2), new Vector3(0.05f, head - foot, 0.05f), q);
        }

        // 2층 계단 구멍 둘레 난간: 긴 쪽 + 아래 끝 쪽 (위 끝은 계단 입구라 비운다)
        float fy = Floor2Y, rb = StairWidth + 0.03f, ra0 = p.open - 0.055f, ra1 = p.Top, rm = (ra0 + ra1) / 2, ea = p.open - 0.03f;
        foreach (float ry in new[] { 0.5f, 1.0f })
        {
            Bar(p.P(rm, rb, fy + ry), new Vector3(ra1 - ra0, 0.05f, 0.05f), q);
            Bar(p.P(ea, (StairWidth + 0.005f) / 2, fy + ry), new Vector3(0.05f, 0.05f, StairWidth + 0.005f), q);
        }
        int n = Mathf.CeilToInt((ra1 - 0.025f - ea) / 1.1f);
        for (int i = 0; i <= n; i++)
            Bar(p.P(Mathf.Lerp(ea, ra1 - 0.025f, i / (float)n), rb, fy + 0.5f), new Vector3(0.05f, 1.0f, 0.05f), q);
        foreach (float b in new[] { 0.05f, StairWidth / 2 })
            Bar(p.P(ea, b, fy + 0.5f), new Vector3(0.05f, 1.0f, 0.05f), q);
        CombineInto(g, "Rails", rails, M["metal"]);

        ColliderBox("RailGuard", g, p.P(rm, rb, fy + 0.55f), new Vector3(ra1 - ra0, 1.1f, 0.06f), new Vector3(0, p.yaw, 0));
        ColliderBox("RailGuard", g, p.P(ea, StairWidth / 2, fy + 0.55f), new Vector3(0.06f, 1.1f, StairWidth), new Vector3(0, p.yaw, 0));
    }

    // 계단 경사로 쐐기 메시 (x: 0→len 동안 0→height 로 오름, z는 가운데 기준 폭)
    static Mesh WedgeMesh(float len, float height, float width)
    {
        string key = $"wedge_{len:0.00}_{height:0.00}_{width:0.00}";
        if (meshCache.TryGetValue(key, out var cached)) return cached;
        float hw = width / 2;
        var m = new Mesh { name = key };
        m.vertices = new[]
        {
            new Vector3(0, 0, -hw), new Vector3(len, 0, -hw), new Vector3(len, height, -hw),
            new Vector3(0, 0, hw), new Vector3(len, 0, hw), new Vector3(len, height, hw),
        };
        m.triangles = new[] { 0, 3, 5, 0, 5, 2, 0, 1, 4, 0, 4, 3, 1, 2, 5, 1, 5, 4, 0, 2, 1, 3, 4, 5 };
        m.RecalculateNormals();
        m.RecalculateBounds();
        AssetDatabase.AddObjectToAsset(m, meshContainer);
        meshCache[key] = m;
        return m;
    }

    // 보이지 않는 충돌 상자 (경사로·난간 판정용, 내비메시 굽기에 들어간다)
    static GameObject ColliderBox(string name, Transform parent, Vector3 pos, Vector3 size, Vector3 euler)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.AddComponent<BoxCollider>().size = size;
        go.isStatic = true;
        return go;
    }

    // 2층 좀비 자리 (탐험 시작 때 지킴이 좀비가 선다)
    static void SpawnUpstairsPoint(Transform points, Vector3 worldPos)
    {
        var go = new GameObject("ZombieSpawnPoint_Up");
        go.transform.SetParent(points, true);
        go.transform.position = worldPos;
        go.AddComponent<ZombieSpawnPoint>().upstairs = true;
    }

    // 2층 안: 복도 칸막이, 방, 가구, 아이템·좀비 자리, 희미한 등 (g2 로컬 y=0이 2층 바닥)
    static void BuildUpstairs(Transform g2, Transform points, UpPlan p, bool trader)
    {
        float hall = StairWidth + HallWidth, rb0 = p.RoomB0, rb1 = p.depth;
        var pmat = Pick(M["wall"], M["cracked"], M["dirty"]);
        var hallOps = p.hasQ ? new[] { DoorOp(p.doorA, 1.6f), DoorOp(p.doorB, 1.6f) } : new[] { DoorOp(p.doorA, 1.6f) };
        Wall(g2, "Partition", p.P(0, hall, 0), p.yaw, p.len, Floor2H, 0.15f, pmat, hallOps);
        if (p.hasQ) Wall(g2, "Partition", p.P(p.Q, rb0, 0), p.yawIn, rb1 - rb0, Floor2H, 0.15f, pmat, p.qDoor ? new[] { DoorOp(p.qDoorC, 1.6f) } : Solid);

        // 좀비 자리 1~2개 (상인 건물은 없음), 등은 적어도 하나
        bool zA = !trader, zB = !trader && Chance(0.5);
        bool litHall = Chance(0.4), litA = Chance(0.6), litB = p.hasQ && Chance(0.4);
        if (!litHall && !litA && !litB) litA = true;
        if (p.hasQ)
        {
            UpRoom(g2, points, p, 0, p.Q - 0.075f, rb0, rb1, true, zA, litA);
            UpRoom(g2, points, p, p.Q + 0.075f, p.len, rb0, rb1, false, zB, litB);
        }
        else UpRoom(g2, points, p, 0, p.len, rb0, rb1, p.doorA > p.len / 2, zA, litA);

        // 계단 아래쪽 끝 위의 막다른 층계참: 상자·드럼통
        var cp = p.P(0.5f, 0.5f, 0);
        Box("Crate", g2, cp + Vector3.up * 0.3f, new Vector3(0.7f, 0.6f, 0.6f), M["wood"], 1f, true, new Vector3(0, R(0, 90), 0));
        if (Chance(0.7)) Spawn<ItemSpawnPoint>(points, g2.TransformPoint(cp) + Vector3.up * 0.65f);
        if (Chance(0.6)) Prim(PrimitiveType.Cylinder, "Barrel", g2, p.P(0.45f, 1.4f, 0.45f), new Vector3(0.55f, 0.45f, 0.55f), Pick(M["metal"], M["paintBlue"]), null, true);
        if (!p.hasQ && zB) SpawnUpstairsPoint(points, g2.TransformPoint(p.P(1.7f, 2.3f, 0)));
        for (int i = 0; i < 3; i++)
            Box("Debris", g2, p.P(R(0.3f, p.len - 0.3f), R(StairWidth + 0.3f, hall - 0.3f), 0.03f), new Vector3(R(0.1f, 0.5f), 0.06f, R(0.1f, 0.4f)), Pick(M["cracked"], M["wood"], M["metal"]), 1f, false, new Vector3(R(-10, 10), R(0, 180), 0));
        if (litHall) UpLight(g2, p.P((p.open + p.Top) / 2, StairWidth / 2 + 0.5f, 0), p.yaw);
    }

    // 2층 방 하나 (a: ra0~ra1, b: rb0~rb1, 문은 rb0 쪽 칸막이). outerLow: 문에서 먼 끝이 ra0 쪽인지
    static void UpRoom(Transform g2, Transform points, UpPlan p, float ra0, float ra1, float rb0, float rb1, bool outerLow, bool zombie, bool lit)
    {
        float sg = outerLow ? 1 : -1, aOut = outerLow ? ra0 : ra1;
        float A(float x) => aOut + sg * x;
        float am = (ra0 + ra1) / 2, bm = (rb0 + rb1) / 2;
        float face = Mathf.Atan2(p.I.x, p.I.z) * Mathf.Rad2Deg; // 방 안쪽(+b)을 보는 회전

        // 칸막이에 등을 댄 선반 (문에서 먼 쪽)
        var shelf = BuildShelf(g2, p.P(A(0.95f), rb0 + 0.25f, 0), face, Chance(0.7));
        Spawn<ItemSpawnPoint>(points, shelf.position + shelf.rotation * new Vector3(-0.4f, 0.65f, 0));
        if (Chance(0.5)) Spawn<ItemSpawnPoint>(points, shelf.position + shelf.rotation * new Vector3(0.4f, 1.15f, 0));

        // 바깥 끝 벽의 매트리스
        if (Chance(0.5))
        {
            var bed = Group("Mattress", g2, p.P(A(0.6f), bm + R(-0.4f, 0.4f), 0), p.yaw + R(-6, 6));
            Box("Pad", bed, new Vector3(0, 0.11f, 0), new Vector3(0.9f, 0.22f, 1.9f), M["blanket"], 1f);
            Box("Pillow", bed, new Vector3(0, 0.27f, 0.7f), new Vector3(0.5f, 0.1f, 0.3f), M["cloth"], 1f, false);
            if (Chance(0.35)) Spawn<ItemSpawnPoint>(points, bed.position + Vector3.up * 0.3f);
        }

        // 먼 구석의 상자 (가끔 두 단)
        var cp = p.P(A(0.5f), rb1 - 0.5f, 0);
        Box("Crate", g2, cp + Vector3.up * 0.3f, new Vector3(0.7f, 0.6f, 0.6f), M["wood"], 1f, true, new Vector3(0, R(0, 90), 0));
        bool stacked = Chance(0.4);
        if (stacked) Box("Crate", g2, cp + Vector3.up * 0.85f, new Vector3(0.55f, 0.5f, 0.5f), M["wood"], 1f, true, new Vector3(0, R(0, 90), 0));
        if (Chance(0.5)) Spawn<ItemSpawnPoint>(points, g2.TransformPoint(cp) + Vector3.up * (stacked ? 1.15f : 0.65f));

        // 바깥벽 쪽 책상과 의자
        if (Chance(0.7))
        {
            var tg = Group("Desk", g2, p.P(am + R(-0.2f, 0.2f), rb1 - 0.55f, 0), p.yaw + R(-5, 5));
            Box("Top", tg, new Vector3(0, 0.76f, 0), new Vector3(1.4f, 0.05f, 0.8f), M["wood"], 1.2f);
            foreach (var lx in new[] { -0.62f, 0.62f })
                foreach (var lz in new[] { -0.32f, 0.32f })
                    Box("Leg", tg, new Vector3(lx, 0.37f, lz), new Vector3(0.05f, 0.74f, 0.05f), M["wood"], 1f, false);
            Spawn<ItemSpawnPoint>(points, tg.position + Vector3.up * 0.85f);
            var chair = Group("Chair", g2, p.P(am + R(-0.5f, 0.5f), rb1 - 1.3f, 0), R(0, 360));
            Box("Seat", chair, new Vector3(0, 0.45f, 0), new Vector3(0.45f, 0.05f, 0.45f), M["wood"], 1f);
            Box("Back", chair, new Vector3(0, 0.75f, -0.21f), new Vector3(0.45f, 0.55f, 0.04f), M["wood"], 1f, false);
            foreach (var lx in new[] { -0.2f, 0.2f })
                foreach (var lz in new[] { -0.2f, 0.2f })
                    Box("Leg", chair, new Vector3(lx, 0.22f, lz), new Vector3(0.04f, 0.45f, 0.04f), M["wood"], 1f, false);
        }

        for (int i = 0; i < 3; i++)
            Box("Debris", g2, p.P(R(ra0 + 0.4f, ra1 - 0.4f), R(rb0 + 0.4f, rb1 - 0.4f), 0.03f), new Vector3(R(0.1f, 0.5f), 0.06f, R(0.1f, 0.4f)), Pick(M["cracked"], M["wood"], M["metal"]), 1f, false, new Vector3(R(-10, 10), R(0, 180), 0));

        if (zombie) SpawnUpstairsPoint(points, g2.TransformPoint(p.P(am, bm, 0)));
        if (lit) UpLight(g2, p.P(am, bm, 0), p.yaw);
    }

    // 2층 천장의 희미한 형광등 (그림자 없음, 일부는 깜빡이거나 꺼졌다 켜진다). 범위가 짧아 1층 바닥까지 새지 않는다
    static void UpLight(Transform g2, Vector3 floorPos, float yaw)
    {
        var col = Chance(0.5) ? new Color(0.75f, 0.85f, 0.8f) : new Color(1f, 0.75f, 0.5f);
        var l = PointLight("DimLight", g2, floorPos + Vector3.up * (Floor2H - 0.35f), col, R(2.5f, 4f), 5f);
        if (Chance(0.6)) Flicker(l, R(0.25f, 0.45f), R(6f, 14f), Chance(0.5));
        Box("Tube", g2, floorPos + Vector3.up * (Floor2H - 0.1f), new Vector3(1.2f, 0.05f, 0.1f), M["bulb"], 1f, false, new Vector3(0, yaw, 0));
    }

    static void FacadeWindows(Transform g, float x0, float z0, float w, float d, float baseY, int floors)
    {
        var parts = new List<CombineInstance>();
        var win = BoxMesh(new Vector3(1.1f, 1.5f, 0.06f), 1f);
        for (int f = 0; f < floors; f++)
        {
            float y = baseY + f * 3.2f + 1.7f;
            for (int side = 0; side < 4; side++)
            {
                float len = side < 2 ? w : d;
                int cols = Mathf.Max(1, Mathf.FloorToInt(len / 3f));
                for (int i = 0; i < cols; i++)
                {
                    if (Chance(0.08)) continue;
                    float u = (i + 0.5f) * len / cols;
                    Vector3 p; Quaternion q;
                    if (side == 0) { p = new Vector3(x0 + u, y, z0 - 0.02f); q = Quaternion.identity; }
                    else if (side == 1) { p = new Vector3(x0 + u, y, z0 + d + 0.02f); q = Quaternion.identity; }
                    else if (side == 2) { p = new Vector3(x0 - 0.02f, y, z0 + u); q = Quaternion.Euler(0, 90, 0); }
                    else { p = new Vector3(x0 + w + 0.02f, y, z0 + u); q = Quaternion.Euler(0, 90, 0); }
                    parts.Add(new CombineInstance { mesh = win, transform = Matrix4x4.TRS(p, q, Vector3.one) });
                }
            }
        }
        CombineInto(g, "Windows", parts, M["glass"]);
    }

    static void TallBuilding(Transform block, Transform points, int ix, int iz)
    {
        LotRect(ix, iz, out float x0, out float z0, out float w, out float d);
        var g = Group("Tower", block, new Vector3(0, 0.15f, 0));
        float h = R(10f, 24f);
        var mat = Pick(M["wall"], M["brick"], M["cracked"]);
        Box("Mass", g, new Vector3(x0 + w / 2, h / 2, z0 + d / 2), new Vector3(w, h, d), mat, 3f);
        int floors = Mathf.FloorToInt((h - 3.5f) / 3.2f);
        FacadeWindows(g, x0, z0, w, d, 3.5f, floors);
        // 1층 녹슨 셔터
        int xSide = ix == 0 ? 2 : 3, zSide = iz == 0 ? 0 : 1;
        float sz = zSide == 0 ? z0 - 0.06f : z0 + d + 0.06f;
        Box("ShutterZ", g, new Vector3(x0 + w / 2, 1.4f, sz), new Vector3(Mathf.Min(6, w - 2), 2.6f, 0.1f), M["metal"], 1.5f);
        float sx = xSide == 2 ? x0 - 0.06f : x0 + w + 0.06f;
        Box("ShutterX", g, new Vector3(sx, 1.4f, z0 + d / 2), new Vector3(0.1f, 2.6f, Mathf.Min(6, d - 2)), M["metal"], 1.5f);
        RubblePile(g, new Vector3(sx + (xSide == 2 ? -1.2f : 1.2f), 0, z0 + R(1, d - 1)), 6, 1.0f);
        if (Chance(0.5)) Spawn<ItemSpawnPoint>(points, g.TransformPoint(new Vector3(sx + (xSide == 2 ? -0.8f : 0.8f), 0.1f, z0 + d * 0.3f)));
    }

    static void CollapsedBuilding(Transform block, Transform points, int ix, int iz)
    {
        LotRect(ix, iz, out float x0, out float z0, out float w, out float d);
        var g = Group("Ruin", block, new Vector3(0, 0.15f, 0));
        var mat = Pick(M["cracked"], M["brick"]);
        Box("Floor", g, new Vector3(x0 + w / 2, 0.02f, z0 + d / 2), new Vector3(w, 0.04f, d), M["floor"], 2.5f);
        // 무너져 높이가 들쭉날쭉한 벽
        for (int side = 0; side < 4; side++)
        {
            float len = side < 2 ? w : d;
            for (float s = 0; s < len - 0.1f;)
            {
                float seg = Mathf.Min(R(1.0f, 2.2f), len - s);
                float hh = Chance(0.18) ? 0 : R(0.4f, 3.8f);
                if (hh > 0)
                {
                    Vector3 p = side == 0 ? new Vector3(x0 + s + seg / 2, hh / 2, z0) : side == 1 ? new Vector3(x0 + s + seg / 2, hh / 2, z0 + d)
                              : side == 2 ? new Vector3(x0, hh / 2, z0 + s + seg / 2) : new Vector3(x0 + w, hh / 2, z0 + s + seg / 2);
                    var size = side < 2 ? new Vector3(seg, hh, 0.3f) : new Vector3(0.3f, hh, seg);
                    Box("Wall", g, p, size, mat, 2.5f, true, new Vector3(0, 0, R(-2, 2)));
                }
                s += seg;
            }
        }
        Vector3 c = new Vector3(x0 + w / 2, 0, z0 + d / 2);
        RubblePile(g, c + new Vector3(R(-2, 2), 0, R(-2, 2)), 14, 1.6f);
        Box("FallenSlab", g, c + new Vector3(R(-2, 2), 1.0f, R(-2, 2)), new Vector3(w * 0.55f, 0.25f, d * 0.35f), M["dirty"], 3f, true, new Vector3(R(18, 32), R(0, 360), 0));
        Box("Beam", g, c + new Vector3(R(-3, 3), 0.8f, R(-3, 3)), new Vector3(0.25f, 0.25f, 4.5f), M["metal"], 1.5f, true, new Vector3(R(10, 25), R(0, 360), 0));
        for (int i = 0; i < 3; i++)
            Spawn<ItemSpawnPoint>(points, g.TransformPoint(c + new Vector3(R(-w / 2 + 1, w / 2 - 1), 0.5f, R(-d / 2 + 1, d / 2 - 1))));
        if (Chance(0.4)) Spawn<ZombieSpawnPoint>(points, g.TransformPoint(c + new Vector3(2.5f, 0, -2.5f)));
        if (Chance(0.35)) FireBarrel(g, c + new Vector3(R(-3, 3), 0, R(-3, 3)));
    }

    static void EmptyLot(Transform block, Transform points, int ix, int iz)
    {
        LotRect(ix, iz, out float x0, out float z0, out float w, out float d);
        var g = Group("Lot", block, new Vector3(0, 0.15f, 0));
        Vector3 c = new Vector3(x0 + w / 2, 0, z0 + d / 2);
        RubblePile(g, c + new Vector3(-2, 0, 1), 10, 1.3f);
        WreckedCar(g, c + new Vector3(2, 0, -1.5f), R(0, 360), Chance(0.3));
        // 쓰러진 철조망 울타리
        for (int i = 0; i < 6; i++)
            Box("FencePost", g, new Vector3(x0 + 1 + i * (w - 2) / 5f, 0.9f, z0 + 0.5f), new Vector3(0.06f, 1.8f, 0.06f), M["metal"], 1f, true, new Vector3(R(-12, 12), 0, R(-12, 12)));
        Spawn<ItemSpawnPoint>(points, g.TransformPoint(c + new Vector3(-2, 0.4f, 2.5f)));
        Spawn<ZombieSpawnPoint>(points, g.TransformPoint(c));
    }

    static void RubblePile(Transform parent, Vector3 center, int count, float spread)
    {
        var g = Group("Rubble", parent, center);
        for (int i = 0; i < count; i++)
        {
            float s = R(0.25f, 1.1f) * spread;
            var size = new Vector3(s * R(0.6f, 1.4f), s * R(0.3f, 0.7f), s * R(0.6f, 1.4f));
            var p = new Vector3(R(-spread, spread), size.y * R(0.1f, 0.4f), R(-spread, spread));
            Box("Chunk", g, p, size, Pick(M["cracked"], M["dirty"], M["brick"]), 1.5f, true, new Vector3(R(-25, 25), R(0, 360), R(-25, 25)));
        }
        if (Chance(0.4))
            Prim(PrimitiveType.Cylinder, "Rebar", g, new Vector3(0, 0.4f, 0), new Vector3(0.03f, 0.8f, 0.03f), M["metal"], new Vector3(R(20, 60), R(0, 360), 0));
    }

    static void DeadTree(Transform parent, Vector3 pos)
    {
        var g = Group("DeadTree", parent, pos, R(0, 360));
        float lean = R(-8, 8);
        Prim(PrimitiveType.Cylinder, "Trunk", g, new Vector3(0, 1.6f, 0), new Vector3(0.22f, 1.6f, 0.22f), M["wood"], new Vector3(lean, 0, 0), true);
        for (int i = 0; i < 4; i++)
            Prim(PrimitiveType.Cylinder, "Branch", g, new Vector3(R(-0.3f, 0.3f), R(2.2f, 3.1f), R(-0.3f, 0.3f)), new Vector3(0.07f, R(0.5f, 0.9f), 0.07f), M["wood"], new Vector3(R(-60, 60), R(0, 360), R(-60, 60)));
    }

    static void LampPost(Transform parent, Vector3 pos, bool forceLit = false)
    {
        var g = Group("LampPost", parent, pos, R(0, 360));
        bool bent = !forceLit && Chance(0.2);
        var tilt = bent ? new Vector3(R(8, 20), 0, 0) : Vector3.zero;
        var pole = Group("Pole", g, Vector3.zero);
        pole.localRotation = Quaternion.Euler(tilt);
        Prim(PrimitiveType.Cylinder, "Pole", pole, new Vector3(0, 3f, 0), new Vector3(0.14f, 3f, 0.14f), M["metal"], null, true);
        Box("Arm", pole, new Vector3(0, 5.9f, 0.6f), new Vector3(0.08f, 0.08f, 1.3f), M["metal"], 1f, false);
        Box("Head", pole, new Vector3(0, 5.8f, 1.2f), new Vector3(0.3f, 0.12f, 0.5f), M["metal"], 1f, false);
        if (!bent && (forceLit || Chance(0.3)))
        {
            Box("Glow", pole, new Vector3(0, 5.73f, 1.2f), new Vector3(0.24f, 0.02f, 0.4f), M["lantern"], 1f, false);
            var l = PointLight("StreetLight", pole, new Vector3(0, 5.4f, 1.2f), new Color(1f, 0.6f, 0.3f), 35f, 18f);
            Flicker(l, 0.15f, 20f, true);
        }
    }

    static void FireBarrel(Transform parent, Vector3 pos)
    {
        var g = Group("FireBarrel", parent, pos);
        Prim(PrimitiveType.Cylinder, "Barrel", g, new Vector3(0, 0.45f, 0), new Vector3(0.6f, 0.45f, 0.6f), M["metal"], null, true);
        var l = PointLight("Fire", g, new Vector3(0, 1.3f, 0), new Color(1f, 0.5f, 0.18f), 16f, 14f);
        Flicker(l, 0.35f, 9f, false);

        var psGo = new GameObject("Flames");
        psGo.transform.SetParent(g, false);
        psGo.transform.localPosition = new Vector3(0, 0.85f, 0);
        psGo.transform.localRotation = Quaternion.Euler(-90, 0, 0);
        var ps = psGo.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.0f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
        main.startColor = new Color(1f, 0.6f, 0.25f, 0.8f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 120;
        var emission = ps.emission;
        emission.rateOverTime = 45;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 8;
        shape.radius = 0.22f;
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(new Color(1f, 0.8f, 0.4f), 0), new GradientColorKey(new Color(1f, 0.35f, 0.1f), 0.5f), new GradientColorKey(new Color(0.3f, 0.1f, 0.05f), 1) },
            new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(0.9f, 0.15f), new GradientAlphaKey(0, 1) });
        col.color = grad;
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0.2f));
        psGo.GetComponent<ParticleSystemRenderer>().sharedMaterial = M["fire"];
    }

    static void WreckedCar(Transform parent, Vector3 pos, float yaw, bool flipped)
    {
        var g = Group("WreckedCar", parent, pos, yaw);
        var body = Group("Body", g, Vector3.zero);
        if (flipped) body.localRotation = Quaternion.Euler(0, 0, 180);
        float yBase = flipped ? 0.95f : 0.35f;
        var paint = Pick(M["metal"], M["paintRed"], M["paintBlue"]);
        Box("Chassis", body, new Vector3(0, flipped ? -0.6f : 0.65f, 0), new Vector3(1.8f, 0.8f, 4.3f), paint, 1.5f);
        Box("Cabin", body, new Vector3(0, flipped ? -1.3f : 1.35f, -0.25f), new Vector3(1.65f, 0.65f, 2.2f), paint, 1.5f);
        Box("Windshield", body, new Vector3(0, flipped ? -1.3f : 1.35f, 0.86f), new Vector3(1.55f, 0.55f, 0.02f), M["glass"], 1f, false);
        foreach (var x in new[] { -0.85f, 0.85f })
            foreach (var z in new[] { -1.4f, 1.4f })
                if (!Chance(0.15))
                    Prim(PrimitiveType.Cylinder, "Wheel", body, new Vector3(x, flipped ? -0.05f : 0.35f, z), new Vector3(0.65f, 0.12f, 0.65f), M["tire"], new Vector3(0, 0, 90));
        if (flipped) g.localPosition += Vector3.up * yBase;
    }

    static void BuildStreetProps(Transform geo, Transform points)
    {
        var props = Group("StreetProps", geo, Vector3.zero);
        for (int i = 0; i < 16; i++)
        {
            float rc = Pick(RoadCenters);
            float along = R(-72f, 72f);
            bool alongZ = Chance(0.5);
            var p = alongZ ? new Vector3(rc + R(-3.5f, 3.5f), 0, along) : new Vector3(along, 0, rc + R(-3.5f, 3.5f));
            if (Vector3.Distance(p, SpawnPos) < 9) continue;
            float yaw = (alongZ ? 0 : 90) + R(-25, 25) + (Chance(0.2) ? 90 : 0);
            WreckedCar(props, p, yaw, Chance(0.15));
            if (Chance(0.45)) Spawn<ItemSpawnPoint>(points, p + Quaternion.Euler(0, yaw, 0) * new Vector3(1.4f, 0.2f, R(-1, 1)));
        }
        for (int i = 0; i < 9; i++)
        {
            var p = RandomRoadPoint();
            if (Vector3.Distance(p, SpawnPos) < 8) continue;
            RubblePile(props, p, rng.Next(6, 12), R(1f, 1.8f));
            if (Chance(0.3)) Spawn<ItemSpawnPoint>(points, p + new Vector3(1.8f, 0.3f, 0));
        }
        for (int i = 0; i < 6; i++)
        {
            var p = RandomRoadPoint();
            float yaw = R(0, 360);
            for (int k = 0; k < 3; k++)
                Box("Barrier", props, p + Quaternion.Euler(0, yaw, 0) * new Vector3(k * 2.1f, 0.45f, 0), new Vector3(2f, 0.9f, 0.6f), M["dirty"], 2f, true, new Vector3(0, yaw + R(-8, 8), Chance(0.2) ? 80 : 0));
        }
        FireBarrel(props, new Vector3(-19.5f, 0, -19.5f)); // 출발 지점 근처
        FireBarrel(props, new Vector3(1.5f, 0, -21f));
        FireBarrel(props, new Vector3(24f, 0, 26f));
        FireBarrel(props, new Vector3(-26f, 0, 40f));
    }

    static void BuildHatch(Transform geo, Vector3 pos)
    {
        var g = Group("ShelterHatch", geo, pos);
        Box("Bunker", g, new Vector3(0, 1.3f, 0), new Vector3(3.2f, 2.6f, 3f), M["dirty"], 2f);
        Box("Roof", g, new Vector3(0, 2.7f, 0), new Vector3(3.5f, 0.2f, 3.3f), M["cracked"], 2f);
        Prim(PrimitiveType.Cylinder, "Vent", g, new Vector3(0.9f, 3.2f, -0.6f), new Vector3(0.25f, 0.5f, 0.25f), M["metal"], null, true);
        var door = Box("HatchDoor", g, new Vector3(0, 1.05f, 1.53f), new Vector3(1.1f, 2.1f, 0.08f), M["metal"], 1.2f);
        door.AddComponent<CityExit>();
        Prim(PrimitiveType.Cylinder, "Wheel", g, new Vector3(0, 1.1f, 1.6f), new Vector3(0.4f, 0.02f, 0.4f), M["metal"], new Vector3(90, 0, 0));
        Box("Lamp", g, new Vector3(0, 2.3f, 1.55f), new Vector3(0.22f, 0.12f, 0.1f), M["redLamp"], 1f, false);
        var l = PointLight("BeaconLight", g, new Vector3(0, 2.25f, 1.9f), new Color(1f, 0.12f, 0.08f), 8f, 10f);
        Flicker(l, 0.1f, 3f, false);
        RubblePile(g, new Vector3(-2.3f, 0, 0.3f), 5, 0.8f);
        Box("Sandbag1", g, new Vector3(1.9f, 0.25f, 1.2f), new Vector3(0.9f, 0.5f, 0.45f), M["cloth"], 1f, true, new Vector3(0, 15, 0));
        Box("Sandbag2", g, new Vector3(1.85f, 0.7f, 1.15f), new Vector3(0.8f, 0.4f, 0.42f), M["cloth"], 1f, true, new Vector3(0, -10, 0));
    }

    static void BuildTrader(Transform parent, Vector3 pos)
    {
        var g = Group("Trader", parent, pos, R(0, 360));
        Prim(PrimitiveType.Capsule, "Coat", g, new Vector3(0, 0.85f, 0), new Vector3(0.6f, 0.82f, 0.5f), M["coat"]);
        var head = Group("Head", g, new Vector3(0, 1.72f, 0));
        Prim(PrimitiveType.Sphere, "Face", head, Vector3.zero, new Vector3(0.24f, 0.27f, 0.25f), M["skin"]);
        Prim(PrimitiveType.Cylinder, "HatBrim", head, new Vector3(0, 0.12f, 0), new Vector3(0.42f, 0.012f, 0.42f), M["coat"]);
        Prim(PrimitiveType.Cylinder, "HatCrown", head, new Vector3(0, 0.19f, 0), new Vector3(0.25f, 0.07f, 0.25f), M["coat"]);
        Box("Scarf", head, new Vector3(0, -0.08f, 0.08f), new Vector3(0.24f, 0.1f, 0.12f), M["paintRed"], 1f, false);
        Box("Pack", g, new Vector3(0, 1.1f, -0.33f), new Vector3(0.5f, 0.7f, 0.3f), M["blanket"], 1f, false);
        var col = g.gameObject.AddComponent<CapsuleCollider>();
        col.height = 1.9f;
        col.radius = 0.35f;
        col.center = new Vector3(0, 0.95f, 0);
        g.gameObject.AddComponent<Trader>().head = head;

        var crate = Box("Crate", g, new Vector3(0.9f, 0.3f, 0.3f), new Vector3(0.7f, 0.6f, 0.6f), M["wood"], 1f, true);
        crate.transform.localRotation = Quaternion.Euler(0, 15, 0);
        Prim(PrimitiveType.Cylinder, "Lantern", g, new Vector3(0.9f, 0.73f, 0.3f), new Vector3(0.12f, 0.12f, 0.12f), M["lantern"]);
        var l = PointLight("LanternLight", g, new Vector3(0.9f, 0.95f, 0.3f), new Color(1f, 0.65f, 0.35f), 6f, 9f, true);
        Flicker(l, 0.12f, 6f, false);
        Box("Goods1", g, new Vector3(-0.8f, 0.2f, 0.4f), new Vector3(0.5f, 0.4f, 0.4f), M["wood"], 1f, true, new Vector3(0, -20, 0));
        Prim(PrimitiveType.Cylinder, "Goods2", g, new Vector3(-0.8f, 0.55f, 0.4f), new Vector3(0.12f, 0.12f, 0.12f), M["metal"]);
    }

    static void BuildBoundary(Transform geo)
    {
        var g = Group("Boundary", geo, Vector3.zero);
        for (int side = 0; side < 4; side++)
        {
            float yaw = side * 90;
            var rot = Quaternion.Euler(0, yaw, 0);
            Box("WallOfRubble", g, rot * new Vector3(0, 5, 80), new Vector3(170, 10, 3), M["cracked"], 4f, true, new Vector3(0, yaw, 0));
            Box("Invisible", g, rot * new Vector3(0, 15, 78.2f), new Vector3(170, 30, 0.5f), M["cracked"], 4f, true, new Vector3(0, yaw, 0)).GetComponent<MeshRenderer>().enabled = false;
            for (int i = 0; i < 10; i++)
                RubblePile(g, rot * new Vector3(R(-75, 75), 0, 77.5f), 6, 2f);
        }
    }

    static void AddAshFall(Transform player)
    {
        var go = new GameObject("AshFall");
        go.transform.SetParent(player, false);
        go.transform.localPosition = new Vector3(0, 7, 0);
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = 9f;
        main.startSpeed = 0.2f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.07f);
        main.startColor = new Color(0.6f, 0.6f, 0.58f, 0.75f);
        main.gravityModifier = 0.03f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 2500;
        main.prewarm = true;
        var emission = ps.emission;
        emission.rateOverTime = 220;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(32, 1, 32);
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.35f;
        noise.frequency = 0.3f;
        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = M["ash"];
    }
}
