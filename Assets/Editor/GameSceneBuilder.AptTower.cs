using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;

// GameSceneBuilder의 삼일아파트 102동 탑 (Apt102 씬): 탑 구조와, 터·방 꾸밈 쪽이 같이 쓰는 도우미.
// 탑은 속이 찬 덩어리다. 걸어 다니는 방·층계참·계단 통로만 비우고 나머지는 상자로 메운다 (벽 너머로 빛·시선이 새지 않고, 떨어질 빈틈이 없다).
// 다만 내비메시 굽기는 상자 콜라이더를 속이 빈 껍데기로 본다. 메운 상자 속에도 걷는 면이 생기므로 그 속은 AptTwSolidCuts가 따로 뺀다.
// 좌표는 모두 탑 로컬: 원점 = 바닥 가운데, +z = 발코니 앞면(마당 쪽), +x = 폭격 맞은 끝. 숫자는 설계표 그대로 적어 검산할 수 있게 둔다.
//   x  01호 -12.75..-8.45 | 계단실 A -8.25..-4.75 (서쪽 통로 ..-6.55 · 가운데 벽 · 동쪽 통로 -6.45..) | 02호 -4.55..-0.1 | 03호 0.1..4.55
//      | 계단실 B 4.75..8.25 (서쪽 통로 ..6.45 · 가운데 벽 · 동쪽 통로 6.55..) | 04호 8.45..12.75. 안쪽 벽(두께 0.2) 가운데 x = -8.35, -4.65, 0, 4.65, 8.35
//   z  계단실: 남쪽 층계참 -5.25..-2.2 | 계단 통로 -2.2..2.2 | 북쪽 층계참 2.2..5.25. 바깥벽 두께 0.25
//   y  AptY(L) = 2.8·L 이 L층 바닥 윗면 (L1 = 2층 … L7 = 8층, 옥상 22.4). 바닥판 0.2, 천장까지 2.6
// 올라가는 길: 무너진 끝 벽(204호) → B1 B2 → 4층을 가로질러 → A3 A4 → 6층 큰 방 → B5 B6 B7 → 옥상. 내려오는 길: 04호 줄 바닥 구멍 H8..H2
public static partial class GameSceneBuilder
{
    // 층 바닥 윗면 높이 (L1 = 2층 = 2.8)
    static float AptY(int level) => 2.8f * level;

    // 양 끝 좌표로 상자를 만든다 ([x0..x1]×[y0..y1]×[z0..z1], parent 로컬). 좌표는 숫자 그대로 적어 검산하기 쉽게 한다
    static GameObject AptTBox(string name, Transform parent, float x0, float x1, float y0, float y1, float z0, float z1, Material mat, float tile = 3f, bool collider = true)
    {
        var centre = new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, (z0 + z1) * 0.5f);
        var size = new Vector3(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0), Mathf.Abs(z1 - z0));
        return Box(name, parent, centre, size, mat, tile, collider);
    }

    // 두께 0.2 바닥판 (윗면 yTop). hole이 있으면 구멍 둘레를 네 조각으로 나눈다. 조각마다 OccluderStatic. name은 발소리 재질을 정한다 ("Floor" = 실내, "Rubble_Roof" = 자갈)
    // hole: Rect(xMin, zMin, width, depth), parent 로컬
    static void AptTSlab(Transform parent, string name, float x0, float x1, float z0, float z1, float yTop, Rect? hole = null)
    {
        void Piece(float a0, float a1, float b0, float b1)
        {
            if (a1 - a0 < 0.01f || b1 - b0 < 0.01f) return;
            var go = AptTBox(name, parent, a0, a1, yTop - 0.2f, yTop, b0, b1, M["floor"]);
            // 얇아서 Box가 가리개로 치지 않는다. 층과 층 사이를 가리는 건 바닥판뿐이라 따로 표시한다
            GameObjectUtility.SetStaticEditorFlags(go, GameObjectUtility.GetStaticEditorFlags(go) | StaticEditorFlags.OccluderStatic);
        }
        if (hole == null) { Piece(x0, x1, z0, z1); return; }
        // SlabWithHole과 같은 네 조각: 구멍 서쪽·동쪽은 판 깊이 전체, 남쪽·북쪽은 구멍 폭만큼 (구멍이 판 밖으로 나가면 판 안쪽만 뚫는다)
        float hx0 = Mathf.Max(x0, hole.Value.xMin), hx1 = Mathf.Min(x1, hole.Value.xMax);
        float hz0 = Mathf.Max(z0, hole.Value.yMin), hz1 = Mathf.Min(z1, hole.Value.yMax);
        Piece(x0, hx0, z0, z1);
        Piece(hx1, x1, z0, z1);
        Piece(hx0, hx1, z0, hz0);
        Piece(hx0, hx1, hz1, z1);
    }

    // 곧은 경사 계단/경사로. foot = 아래 끝 가운데(바닥 높이), yaw -90 = +z로 오름, +90 = -z로 오름. steps 0 = 경사로(보이는 판 + 쐐기)
    // 판정은 BuildStairs와 같다: 밑이 꽉 찬 볼록 쐐기 + 같은 경사면의 얇은 상자. 그룹 로컬 +x가 오르는 방향이고, 걷는 판정 둘의 이름이 surfaceName이다 (발소리·점검이 이름으로 찾는다)
    static void AptTFlight(Transform g, string name, Vector3 foot, float yaw, float run, float rise, float width, int steps, string surfaceName, Material mat)
    {
        var f = Group(name, g, foot, yaw);
        float th = Mathf.Atan2(rise, run), cos = Mathf.Cos(th), sin = Mathf.Sin(th);
        var tilt = new Vector3(0, 0, th * Mathf.Rad2Deg); // 로컬 X가 오르막을 따라 th만큼 들린다
        if (steps > 0)
        {
            // 계단 단: 단 앞 모서리가 경사면 위에 오도록 바닥부터 쌓은 덩어리 (보이기만 한다). 맨 윗단은 위층 바닥이 맡는다
            float sr = run / steps, sh = rise / steps;
            var parts = new List<CombineInstance>();
            for (int k = 1; k < steps; k++)
                parts.Add(new CombineInstance { mesh = BoxMesh(new Vector3(sr, k * sh, width), 1.5f), transform = Matrix4x4.TRS(new Vector3((k + 0.5f) * sr, k * sh / 2, 0), Quaternion.identity, Vector3.one) });
            CombineInto(f, "Stairs", parts, mat);
            // 윗면이 경사면과 겹치는 얇은 상자 (가운데를 경사면 법선 반대로 0.1 내린다)
            ColliderBox(surfaceName, f, new Vector3(run / 2 + sin * 0.1f, rise / 2 - cos * 0.1f, 0), new Vector3(run / cos, 0.2f, width), tilt);
        }
        else
        {
            // 경사로: 쓰러진 판 하나가 보이는 면이자 걷는 판정이다 (두께 0.26, 윗면이 경사면)
            Box(surfaceName, f, new Vector3(run / 2 + sin * 0.13f, rise / 2 - cos * 0.13f, 0), new Vector3(run / cos, 0.26f, width), mat, 3f, true, tilt);
        }
        var wedge = new GameObject(surfaceName);
        wedge.transform.SetParent(f, false);
        var mc = wedge.AddComponent<MeshCollider>();
        mc.sharedMesh = WedgeMesh(run, rise, width);
        mc.convex = true;
        wedge.isStatic = true;
    }

    // 불꽃 입자 + 흔들리는 주황 점광 (FireBarrel의 불꽃 부분). pos = 불꽃 밑동 (parent 로컬)
    static void AptTFire(Transform parent, Vector3 pos, float intensity, float range, bool shadows)
    {
        var l = PointLight("Fire", parent, pos + Vector3.up * 0.5f, new Color(1f, 0.5f, 0.18f), intensity, range, shadows);
        Flicker(l, 0.35f, 9f, false);

        var psGo = new GameObject("Flames");
        psGo.transform.SetParent(parent, false);
        psGo.transform.localPosition = pos;
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

    // 재가 떨어지는 고정 입자 (AddAshFall의 설정을 고정 상자에). centre/size = 월드
    // 도시처럼 플레이어를 따라다니면 실내에도 재가 내리므로, 하늘이 뚫린 자리 위에만 세워 둔다
    static void AptTAsh(Transform dyn, Vector3 centre, Vector3 size, float rate, int maxParticles)
    {
        var go = new GameObject("AshFall");
        go.transform.SetParent(dyn, false);
        go.transform.position = centre;
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = 9f;
        main.startSpeed = 0.2f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.07f);
        main.startColor = new Color(0.6f, 0.6f, 0.58f, 0.75f);
        main.gravityModifier = 0.03f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = maxParticles;
        main.prewarm = true;
        var emission = ps.emission;
        emission.rateOverTime = rate;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = size;
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.35f;
        noise.frequency = 0.3f;
        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = M["ash"];
    }

    // 내비메시에서 빼는 영역 (NavMeshModifierVolume, area 1). 이름은 "NavCut". centre/size = parent 로컬
    static void AptTNavCut(Transform parent, Vector3 centre, Vector3 size)
    {
        var go = new GameObject("NavCut");
        go.transform.SetParent(parent, false);
        var vol = go.AddComponent<NavMeshModifierVolume>();
        vol.center = centre;
        vol.size = size;
        vol.area = 1; // Not Walkable
    }

    // 탑 전체. geo 아래 "Tower" 그룹을 만들고 그 로컬 좌표로 짓는다. 맨 끝에서 BuildAptTowerRooms를 부른다
    static void BuildAptTower(Transform geo, Transform points, Transform dyn, Vector3 pos, float yaw, System.Random r)
    {
        var tower = Group("Tower", geo, pos, yaw);
        AptTwFacade(tower, r);
        AptTwShell(tower);
        AptTwCores(tower);
        AptTwWalls(tower, r);
        AptTwFloors(tower);
        AptTwFlights(tower, r);
        AptTwRoof(tower, r);
        AptTwNavCuts(tower);
        BuildAptTowerRooms(tower, points, dyn, r);
    }

    // ---------- 겉모습 ----------

    // 겉은 도시의 102동 그대로 (폭 26·깊이 11·14층, +x 끝 7m는 8층까지만 남은 파손 동). 속을 채운 덩어리와 옥상 잔해만 지우고, 잠긴 현관문은 살펴보는 문으로 바꾼다.
    // 창·난간·실외기·동 번호 띠·옥탑·현관은 그대로 쓴다 (BuildApartment는 고치지 않는다)
    static void AptTwFacade(Transform tower, System.Random r)
    {
        BuildApartment(tower, Vector3.zero, 0f, 26f, 11f, 14, 1, r, true);
        var apt = tower.Find("Apartment_102");
        if (apt == null) throw new System.InvalidOperationException("Apartment_102 그룹이 없다. BuildApartment가 만드는 이름이 바뀌었는지 확인할 것");
        // Mass·MassEnd = 속이 찬 몸통 (아래에서 방을 파낸 덩어리로 다시 짓는다), Damage = 도시용 옥상 잔해·연기 (옥상은 싸움터로 다시 짓는다)
        foreach (var name in new[] { "Mass", "MassEnd", "Damage" })
        {
            var c = apt.Find(name);
            if (c != null) Object.DestroyImmediate(c.gameObject);
        }
        // 옥탑의 물탱크는 싸움터의 쓰러진 물탱크("Tank")와 이름이 같다. 점검이 이름으로 찾으므로 겉모습 쪽 이름을 바꿔 둔다
        var penthouseTank = apt.Find("Roof/Tank");
        if (penthouseTank != null) penthouseTank.name = "PenthouseTank";
        foreach (var locked in apt.GetComponentsInChildren<LockedDoor>(true))
        {
            var go = locked.gameObject;
            Object.DestroyImmediate(locked); // "집으로 돌아갈 수 없다"는 도시의 글이라 여기와 맞지 않는다
            var note = go.AddComponent<InspectNote>();
            note.prompt = "현관문 - 열어 본다";
            note.inOrder = true; // 마지막 줄이 들어갈 길(무너진 끝 벽)을 알려 준다
            note.lines = new[]
            {
                "안에서 쇠사슬을 감아 놓았다. 이쪽으로는 못 들어간다.",
                "유리 너머로 쌓아 올린 가구가 보인다. 꿈쩍도 하지 않는다.",
                "문틀이 뒤틀려 열리지 않는다. 다른 길을 찾아야 한다.",
                "무너진 쪽 벽이 뚫려 있었다. 그리로 올라가 보자.",
            };
        }
    }

    // ---------- 몸통 ----------

    // 바깥 껍데기와 호실 줄 채움. 겉면(x ±13, z ±5.5)은 지운 Mass·MassEnd와 같은 자리라 창·띠가 그대로 붙는다
    static void AptTwShell(Transform tower)
    {
        var s = Group("Shell", tower, Vector3.zero);
        var wall = M["aptWall"];
        AptTBox("Fill", s, -13f, 13f, 0f, 2.6f, -5.5f, 5.5f, wall, 4f);            // 1층은 통째로 막혀 있다
        AptTBox("Fill", s, -13f, 6f, 22.4f, 39.2f, -5.5f, 5.5f, wall, 4f);          // 9층 위로 남은 몸통 (MassTop). 동쪽 면 x = 6이 잘린 단면
        AptTBox("Fill", s, -13f, 13f, 2.6f, 22.4f, -5.5f, -5.25f, wall, 4f);        // 뒷벽
        AptTBox("Fill", s, -13f, 13f, 2.6f, 22.4f, 5.25f, 5.5f, wall, 4f);          // 앞벽
        AptTBox("Fill", s, -13f, -8.25f, 2.6f, 22.4f, -5.25f, 5.25f, wall, 4f);     // 01호 줄은 통째로 막혀 있다 (계단실 A의 서쪽 벽까지)
        // 동쪽 끝 벽: 2층 북쪽에 뚫린 자리 (z 2.2..5.0, y 2.8..5.2) 로 들어온다
        AptTBox("Fill", s, 12.75f, 13f, 2.6f, 22.4f, -5.25f, 2.2f, wall, 4f);
        AptTBox("Fill", s, 12.75f, 13f, 5.2f, 22.4f, 2.2f, 5.25f, wall, 4f);
        AptTBox("Fill", s, 12.75f, 13f, 2.6f, 5.2f, 5.0f, 5.25f, wall, 4f);
        AptTBox("Floor", s, 12.75f, 13f, 2.6f, 2.8f, 2.2f, 5.0f, M["floor"]);       // 뚫린 자리의 문턱 (밟고 지나간다)

        // 02호 줄: 4층(L3 402호)·6층(L5 큰 방)만 비었다
        AptTBox("Fill", s, -4.55f, -0.1f, 2.6f, 8.2f, -5.25f, 5.25f, wall, 4f);
        AptTBox("Fill", s, -4.55f, -0.1f, 11.0f, 13.8f, -5.25f, 5.25f, wall, 4f);
        AptTBox("Fill", s, -4.55f, -0.1f, 16.6f, 22.4f, -5.25f, 5.25f, wall, 4f);
        // 03호 줄: 3층 북쪽 절반(L2 303호 숨는 방)·4층(L3 403호)·6층(L5 큰 방)만 비었다
        AptTBox("Fill", s, 0.1f, 4.55f, 2.6f, 5.4f, -5.25f, 5.25f, wall, 4f);
        AptTBox("Fill", s, 0.1f, 4.55f, 5.4f, 8.2f, -5.25f, 0.0f, wall, 4f);
        AptTBox("Fill", s, 0.1f, 4.55f, 11.0f, 13.8f, -5.25f, 5.25f, wall, 4f);
        AptTBox("Fill", s, 0.1f, 4.55f, 16.6f, 22.4f, -5.25f, 5.25f, wall, 4f);
        // 04호 줄(x 8.45..12.75)은 2~8층이 모두 비었다: 채움 없음
    }

    // 계단실 둘. 층마다 [남쪽 층계참 / 서쪽 통로 / 동쪽 통로 / 북쪽 층계참] 가운데 길이 아닌 칸을 메운다 (F = 층계참, ↑ = 계단, o = 아래 계단의 위쪽 빈 통, ■ = 채움)
    //   L   계단실 B (x 4.75..8.25)      계단실 A (x -8.25..-4.75)
    //   1   F  / ■   / ↑B1 / ■           ■
    //   2   ■  / ↑B2 / o   / F           ■
    //   3   F  / o   / ■   / ■           F / ■   / ↑A3 / ■
    //   4   ■  / ■   / ■   / ■           ■ / ↑A4 / o   / F
    //   5   F  / ■   / ↑B5 / ■           F / o   / ■   / ■
    //   6   ■  / ↑B6 / o   / F           ■
    //   7   F  / o   / ↑B7 / ■           ■
    // 채움 높이는 그 층 바닥판 밑면(AptY(L) - 0.2)부터 위층 바닥판 밑면까지라, 채움 밑면이 곧 아래 칸의 천장(2.6)이다
    static void AptTwCores(Transform tower)
    {
        var a = Group("CoreA", tower, Vector3.zero);
        var b = Group("CoreB", tower, Vector3.zero);
        var wall = M["aptWall"];

        // 계단실 A
        AptTBox("Fill", a, -8.25f, -4.75f, 2.6f, 8.2f, -5.25f, 5.25f, wall, 4f);      // L1·L2 전부
        AptTBox("Fill", a, -8.25f, -6.55f, 8.2f, 11.0f, -2.2f, 2.2f, wall, 4f);       // L3 서쪽 통로
        AptTBox("Fill", a, -8.25f, -4.75f, 8.2f, 11.0f, 2.2f, 5.25f, wall, 4f);       // L3 북
        AptTBox("Fill", a, -8.25f, -4.75f, 11.0f, 13.8f, -5.25f, -2.2f, wall, 4f);    // L4 남
        AptTBox("Fill", a, -6.45f, -4.75f, 13.8f, 16.6f, -2.2f, 2.2f, wall, 4f);      // L5 동쪽 통로
        AptTBox("Fill", a, -8.25f, -4.75f, 13.8f, 16.6f, 2.2f, 5.25f, wall, 4f);      // L5 북
        AptTBox("Fill", a, -8.25f, -4.75f, 16.6f, 22.4f, -5.25f, 5.25f, wall, 4f);    // L6·L7 전부
        AptTBox("Fill", a, -6.55f, -6.45f, 8.2f, 16.6f, -2.2f, 2.2f, wall);           // 두 통로 사이 가운데 벽

        // 계단실 B
        AptTBox("Fill", b, 4.75f, 6.45f, 2.6f, 5.4f, -2.2f, 2.2f, wall, 4f);          // L1 서쪽 통로
        AptTBox("Fill", b, 4.75f, 8.25f, 2.6f, 5.4f, 2.2f, 5.25f, wall, 4f);          // L1 북
        AptTBox("Fill", b, 4.75f, 8.25f, 5.4f, 8.2f, -5.25f, -2.2f, wall, 4f);        // L2 남
        AptTBox("Fill", b, 6.55f, 8.25f, 8.2f, 11.0f, -2.2f, 2.2f, wall, 4f);         // L3 동쪽 통로 (무너져 막혔다)
        AptTBox("Fill", b, 4.75f, 8.25f, 8.2f, 11.0f, 2.2f, 5.25f, wall, 4f);         // L3 북
        AptTBox("Fill", b, 4.75f, 8.25f, 11.0f, 13.8f, -5.25f, 5.25f, wall, 4f);      // L4 전부 (그래서 4층에서 계단실 A로 건너가야 한다)
        AptTBox("Fill", b, 4.75f, 6.45f, 13.8f, 16.6f, -2.2f, 2.2f, wall, 4f);        // L5 서쪽 통로
        AptTBox("Fill", b, 4.75f, 8.25f, 13.8f, 16.6f, 2.2f, 5.25f, wall, 4f);        // L5 북
        AptTBox("Fill", b, 4.75f, 8.25f, 16.6f, 19.4f, -5.25f, -2.2f, wall, 4f);      // L6 남
        AptTBox("Fill", b, 4.75f, 8.25f, 19.4f, 22.2f, 2.2f, 5.25f, wall, 4f);        // L7 북 (옥상 계단 머리 밑)
        // 가운데 벽: 옥상 위로 1.6 솟아 계단 구멍의 서쪽을 막는다. 설계는 1.1(23.5)이지만 뛰어서 오르는 높이(0.9 + 턱 0.4 = 1.3)보다 낮으면
        // 계단 머리 터에서 벽 위로 올라 남쪽 잔해 더미까지 걸어가고, 더미 위에서는 보이지 않는 난간(더미 윗면에서 1.4)도 넘는다. 더미·싸움터 벽과 같은 높이로 올린다
        AptTBox("Fill", b, 6.45f, 6.55f, 2.6f, 24.0f, -2.2f, 2.2f, wall);
    }

    // 호실과 계단실 사이의 안쪽 벽 넷 (W2 x -4.65, 세대 사이 벽 x 0, W3 x 4.65, W4 x 8.35). 층마다 문턱 판 + 벽 띠.
    // 벽 띠는 Wall(yaw -90): 로컬 X가 +z라 구멍 자리 op.c = z + 5.25. 문(폭 1.6·높이 2.2)은 남쪽 층계참 옆 z -4.5..-2.9
    static void AptTwWalls(Transform tower, System.Random r)
    {
        var g = Group("Walls", tower, Vector3.zero);
        var door = new[] { new Op(1.55f, 1.6f, 0, 2.2f) };
        for (int L = 1; L <= 7; L++)
        {
            bool crossing = L == 3 || L == 5; // 계단실 사이를 건너가는 층 (4층·6층)

            // W2: 계단실 A ↔ 402호(L3) / 큰 방(L5)
            var w2 = AptTwWallBand(g, "W2_L" + L, -4.65f, L, crossing ? door : Solid);
            if (crossing) DoorCasing(w2, 1.55f, 1.6f, 2.2f, 0.2f, M["wood"]);

            // 세대 사이 벽: L3은 뚫어 놓은 구멍 (z 2.4..4.2, 높이 2.4), L5는 z -5.25..3.8이 트여 두 집이 큰 방 하나다
            AptTwWallBand(g, "PW_L" + L, 0f, L,
                L == 3 ? new[] { new Op(8.55f, 1.8f, 0, 2.4f) } : L == 5 ? new[] { new Op(4.525f, 9.05f, 0, 2.6f) } : Solid);

            // W3: 계단실 B ↔ 303호(L2, 판자로 막혀 앉아서만 지나는 문 z 3.0..4.4) / 403호(L3) / 큰 방(L5)
            var w3 = AptTwWallBand(g, "W3_L" + L, 4.65f, L, L == 2 ? new[] { new Op(8.95f, 1.4f, 0, 2.2f) } : crossing ? door : Solid);
            if (L == 2) BoardedDoorway(w3, 8.95f, 1.4f, 2.2f, CrawlGap, r, 0.2f);
            else if (crossing) DoorCasing(w3, 1.55f, 1.6f, 2.2f, 0.2f, M["wood"]);

            // W4: 계단실 B ↔ 04호 줄. 계단실 남쪽 층계참이 있는 층(L1·L3·L5·L7)마다 문
            bool south = L % 2 == 1;
            var w4 = AptTwWallBand(g, "W4_L" + L, 8.35f, L, south ? door : Solid);
            if (south) DoorCasing(w4, 1.55f, 1.6f, 2.2f, 0.2f, M["wood"]);
        }
    }

    // 안쪽 벽 한 층 (가운데 x = xc, z -5.25..5.25): 문턱 판(바닥판 높이, 문 자리에서 밟는다) + 벽 띠(높이 2.6)
    static Transform AptTwWallBand(Transform g, string name, float xc, int level, Op[] ops)
    {
        float y = AptY(level);
        AptTBox("Floor", g, xc - 0.1f, xc + 0.1f, y - 0.2f, y, -5.25f, 5.25f, M["floor"]);
        return Wall(g, name, new Vector3(xc, y, -5.25f), -90f, 10.5f, 2.6f, 0.2f, M["aptWall"], ops);
    }

    // 바닥판 (윗면 AptY(L), 이름 Floor). 04호 줄의 구멍 H2..H7이 옥상에서 2층까지 한 층씩 뛰어내리는 굴뚝이다:
    // 구멍은 위아래로 엇갈려 한 번에 한 층만 떨어진다. H4만 H3 위로 2m 겹쳐 놓았다 (걸어 나가면 두 층을 떨어지는 함정)
    static void AptTwFloors(Transform tower)
    {
        var f = Group("Floors", tower, Vector3.zero);
        // 04호 줄 (L1 204호 … L7 804호)
        AptTSlab(f, "Floor", 8.45f, 12.75f, -5.25f, 5.25f, AptY(1));
        AptTSlab(f, "Floor", 8.45f, 12.75f, -5.25f, 5.25f, AptY(2), new Rect(9.6f, 2.6f, 2f, 2f));     // H2 z 2.6..4.6 → 204호
        AptTSlab(f, "Floor", 8.45f, 12.75f, -5.25f, 5.25f, AptY(3), new Rect(9.6f, -3.8f, 2f, 2f));    // H3 z -3.8..-1.8 → 304호
        AptTSlab(f, "Floor", 8.45f, 12.75f, -5.25f, 5.25f, AptY(4), new Rect(9.6f, -3.8f, 2f, 3.4f));  // H4 z -3.8..-0.4 → 404호 (z -1.8..-0.4 만 바닥이 받는다)
        AptTSlab(f, "Floor", 8.45f, 12.75f, -5.25f, 5.25f, AptY(5), new Rect(9.6f, 2.6f, 2f, 2f));     // H5 z 2.6..4.6 → 504호
        AptTSlab(f, "Floor", 8.45f, 12.75f, -5.25f, 5.25f, AptY(6), new Rect(9.6f, -2.4f, 2f, 2f));    // H6 z -2.4..-0.4 → 604호
        AptTSlab(f, "Floor", 8.45f, 12.75f, -5.25f, 5.25f, AptY(7), new Rect(9.6f, 2.6f, 2f, 2f));     // H7 z 2.6..4.6 → 704호
        // 02호·03호 줄
        AptTSlab(f, "Floor", -4.55f, -0.1f, -5.25f, 5.25f, AptY(3));    // 402호
        AptTSlab(f, "Floor", -4.55f, -0.1f, -5.25f, 5.25f, AptY(5));    // 큰 방 서쪽 (602)
        AptTSlab(f, "Floor", 0.1f, 4.55f, 0.0f, 5.25f, AptY(2));        // 303호 (북쪽 절반)
        AptTSlab(f, "Floor", 0.1f, 4.55f, -5.25f, 5.25f, AptY(3));      // 403호
        AptTSlab(f, "Floor", 0.1f, 4.55f, -5.25f, 5.25f, AptY(5));      // 큰 방 동쪽 (603)
        // 계단실 A: 층계참과 계단 밑판
        AptTSlab(f, "Floor", -8.25f, -4.75f, -5.25f, -2.2f, AptY(3));   // L3 남
        AptTSlab(f, "Floor", -6.45f, -4.75f, -2.2f, 2.2f, AptY(3));     // L3 동쪽 통로 (A3 밑)
        AptTSlab(f, "Floor", -8.25f, -6.55f, -2.2f, 2.2f, AptY(4));     // L4 서쪽 통로 (A4 밑)
        AptTSlab(f, "Floor", -8.25f, -4.75f, 2.2f, 5.25f, AptY(4));     // L4 북
        AptTSlab(f, "Floor", -8.25f, -4.75f, -5.25f, -2.2f, AptY(5));   // L5 남
        // 계단실 B
        AptTSlab(f, "Floor", 4.75f, 8.25f, -5.25f, -2.2f, AptY(1));     // L1 남
        AptTSlab(f, "Floor", 6.55f, 8.25f, -2.2f, 2.2f, AptY(1));       // L1 동쪽 통로 (B1 밑)
        AptTSlab(f, "Floor", 4.75f, 6.45f, -2.2f, 2.2f, AptY(2));       // L2 서쪽 통로 (B2 밑)
        AptTSlab(f, "Floor", 4.75f, 8.25f, 2.2f, 5.25f, AptY(2));       // L2 북
        AptTSlab(f, "Floor", 4.75f, 8.25f, -5.25f, -2.2f, AptY(3));     // L3 남
        AptTSlab(f, "Floor", 4.75f, 8.25f, -5.25f, -2.2f, AptY(5));     // L5 남
        AptTSlab(f, "Floor", 6.55f, 8.25f, -2.2f, 2.2f, AptY(5));       // L5 동쪽 통로 (B5 밑)
        AptTSlab(f, "Floor", 4.75f, 6.45f, -2.2f, 2.2f, AptY(6));       // L6 서쪽 통로 (B6 밑)
        AptTSlab(f, "Floor", 4.75f, 8.25f, 2.2f, 5.25f, AptY(6));       // L6 북
        AptTSlab(f, "Floor", 4.75f, 8.25f, -5.25f, -2.2f, AptY(7));     // L7 남
        AptTSlab(f, "Floor", 6.55f, 8.25f, -2.2f, 2.2f, AptY(7));       // L7 동쪽 통로 (B7 밑. 아래는 B5의 빈 통이라 떠 있는 판이다)
    }

    // 계단 일곱 (폭 1.7, 길이 4.4, 높이 2.8 = 한 층, 16단). 통로 가운데 x: 계단실 B 동 7.4·서 5.6, 계단실 A 동 -5.6·서 -7.4
    static void AptTwFlights(Transform tower, System.Random r)
    {
        var lamps = Group("StairLamps", tower, Vector3.zero);
        void Flight(string name, float x, int level, bool north)
        {
            var foot = new Vector3(x, AptY(level), north ? -2.2f : 2.2f);
            AptTFlight(tower, name, foot, north ? -90f : 90f, 4.4f, 2.8f, 1.7f, 16, "Floor", M["dirty"]);
            // 계단 어귀의 초록 등: 올라갈 수 있는 계단이라는 표시 (빨강 = 돌아갈 길, 주황 = 불). 등갓은 층계참 천장 끝에 붙이고 빛은 발치 위 2.3m
            Box("ExitLamp", lamps, foot + new Vector3(0, 2.55f, north ? -0.03f : 0.03f), new Vector3(0.25f, 0.1f, 0.06f), M["bulb"], 1f, false);
            PointLight("ExitLight", lamps, foot + new Vector3(0, 2.3f, 0), new Color(0.35f, 1f, 0.5f), 1.2f, 3f);
        }
        Flight("B1", 7.4f, 1, true);    // (7.4, 2.8, -2.2) → (7.4, 5.6, 2.2)
        Flight("B2", 5.6f, 2, false);   // (5.6, 5.6, 2.2) → (5.6, 8.4, -2.2)
        Flight("A3", -5.6f, 3, true);   // (-5.6, 8.4, -2.2) → (-5.6, 11.2, 2.2)
        Flight("A4", -7.4f, 4, false);  // (-7.4, 11.2, 2.2) → (-7.4, 14.0, -2.2)
        Flight("B5", 7.4f, 5, true);    // (7.4, 14.0, -2.2) → (7.4, 16.8, 2.2)
        Flight("B6", 5.6f, 6, false);   // (5.6, 16.8, 2.2) → (5.6, 19.6, -2.2)
        Flight("B7", 7.4f, 7, true);    // (7.4, 19.6, -2.2) → (7.4, 22.4, 2.2) 옥상 계단 머리

        // 4층(L3) 계단실 B 남쪽 층계참: 무너져 막힌 동쪽 통로 어귀에서 잔해가 쏟아져 나와 있다 (위로 가는 계단이 끊겼다는 표시)
        // 더미 가운데를 막힌 면(z -2.2)에 바짝 붙여 절반은 벽 속에 묻힌다: 층계참 길(B2 도착 → 서쪽 문)은 비워 둔다. 철근은 머리 위에서 삐져나온다
        RubblePile(tower, new Vector3(7.4f, 8.4f, -2.45f), 5, 0.5f);
        Rebar(tower, new Vector3(7.0f, 10.6f, -2.2f), new Vector3(0.2f, -0.3f, -1f), 0.7f, r);
        Rebar(tower, new Vector3(7.8f, 10.3f, -2.2f), new Vector3(-0.3f, -0.1f, -1f), 0.6f, r);
    }

    // ---------- 옥상 (y 22.4) ----------

    // 계단 머리 터 x 6..8.25, z 2.2..5.25 (서쪽은 남은 몸통의 잘린 면) → 판자 문 밑으로 기어서 → 싸움터 x 8.45..12.75 (하늘이 뚫린 04호 줄 지붕).
    // 싸움터는 내비메시가 따로 떨어진 섬이라 옥상의 적은 계단으로 내려오지 못하고, 아래의 적은 계단 머리에서 멈춘다
    static void AptTwRoof(Transform tower, System.Random r)
    {
        var roof = Group("Roof", tower, Vector3.zero);
        // 바닥판 (윗면 22.4). 계단실 B 위는 실내 바닥 소리, 밖으로 드러난 두 곳은 자갈 소리. 동쪽 통로 위(x 6.55..8.25, z -2.2..2.2)는 B7이 올라오는 구멍이라 비운다
        AptTSlab(roof, "Floor", 4.75f, 8.25f, -5.25f, -2.2f, 22.4f);
        AptTSlab(roof, "Floor", 4.75f, 6.45f, -2.2f, 2.2f, 22.4f);
        AptTSlab(roof, "Floor", 8.25f, 8.45f, -5.25f, 5.25f, 22.4f);                                         // W4 자리 문턱
        AptTSlab(roof, "Rubble_Roof", 4.75f, 8.25f, 2.2f, 5.25f, 22.4f);                                     // 계단 머리 터
        AptTSlab(roof, "Rubble_Roof", 8.45f, 12.75f, -5.25f, 5.25f, 22.4f, new Rect(10.6f, -4.9f, 2f, 2f));  // 싸움터. H8 = 남동쪽 구덩이 → 804호

        // 계단 구멍의 남쪽을 막는 잔해 더미 (1.6: 뛰어도 못 오른다)
        AptTBox("RoofHeap", roof, 6.0f, 8.25f, 22.4f, 24.0f, -5.25f, -2.2f, M["dirty"]);
        RubblePile(roof, new Vector3(7.1f, 24.0f, -3.7f), 5, 0.7f);

        // 계단 머리 터와 싸움터 사이 남은 벽: 터 옆은 2.5 높이에 판자로 막힌 문 (z 3.1..4.5, 앉아야 지나간다 = 적은 못 지나간다), 그 남쪽은 1.6
        var pw = Wall(roof, "PadWall", new Vector3(8.35f, 22.4f, 2.2f), -90f, 3.05f, 2.5f, 0.2f, M["aptWall"], new[] { new Op(1.6f, 1.4f, 0, 2.2f) });
        BoardedDoorway(pw, 1.6f, 1.4f, 2.2f, CrawlGap, r, 0.2f);
        AptTBox("ArenaWall", roof, 8.25f, 8.45f, 22.4f, 24.0f, -5.25f, 2.2f, M["aptWall"]);

        // 바깥 세 변의 부서진 난간 (바깥벽 위, 높이 들쭉날쭉)
        RaggedWall(roof, "ParapetN", new Vector3(6.0f, 22.4f, 5.375f), 0f, 7f, 0.3f, 1.4f, 0.7f, 0.25f, M["aptWall"], r);
        RaggedWall(roof, "ParapetE", new Vector3(12.875f, 22.4f, 5.5f), 90f, 11f, 0.2f, 1.5f, 0.6f, 0.25f, M["aptWall"], r);
        RaggedWall(roof, "ParapetS", new Vector3(13f, 22.4f, -5.375f), 180f, 7f, 0.35f, 1.6f, 0.8f, 0.25f, M["aptWall"], r);

        // 보이지 않는 난간 (두께 0.1, 옥상 위 3m까지: 뛰기 0.9 + 턱 오르기 0.4로는 못 넘는다). 낮은 벽 위도 같은 높이까지 막는다
        AptTwGuard(roof, 6f, 13f, 22.4f, 25.4f, 5.25f, 5.35f);          // 북
        AptTwGuard(roof, 12.75f, 12.85f, 22.4f, 25.4f, -5.5f, 5.5f);    // 동
        AptTwGuard(roof, 6f, 13f, 22.4f, 25.4f, -5.35f, -5.25f);        // 남
        AptTwGuard(roof, 8.3f, 8.4f, 24.0f, 25.4f, -5.25f, 2.2f);       // 싸움터 서쪽 벽(1.6) 위
        AptTwGuard(roof, 8.3f, 8.4f, 24.9f, 25.4f, 2.2f, 5.25f);        // 판자 문 벽(2.5) 위

        // 쓰러진 물탱크: 서쪽 길 1.75는 누구나, 동쪽 길 1.05는 플레이어만 (내비메시에서 뺀다). 돌 던지는 놈을 피하는 엄폐물
        AptTBox("Tank", roof, 10.2f, 11.7f, 22.4f, 24.0f, -0.9f, 1.5f, M["paintBlue"]);
    }

    static void AptTwGuard(Transform roof, float x0, float x1, float y0, float y1, float z0, float z1)
    {
        ColliderBox("LedgeGuard", roof, new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, (z0 + z1) * 0.5f), new Vector3(x1 - x0, y1 - y0, z1 - z0), Vector3.zero);
    }

    // ---------- 내비메시에서 빼는 자리 ----------

    // 길이 이어지지 않는 바닥은 내비메시에서 뺀다: 남으면 그 위를 배회 목적지로 잡은 적이 벽에 붙어 서성인다. 탑에서 일부러 남기는 섬은 옥상 싸움터 하나뿐이다
    static void AptTwNavCuts(Transform tower)
    {
        AptTNavCut(tower, new Vector3(-3.5f, 42.5f, 0f), new Vector3(19.5f, 7f, 11.5f));      // 남은 몸통 꼭대기 (옥탑 포함)
        // 설계의 영역(z -5.4..-2.0)은 더미 북쪽 면(-2.2)을 0.2 넘어 하늘이 뚫린 B7 계단 어귀 위에 걸린다. 더미 면 안쪽(-2.25)에서 끝낸다 (더미 위 메시는 가장자리에서 0.5 안쪽에만 생긴다)
        AptTNavCut(tower, new Vector3(7.1f, 24.6f, -3.825f), new Vector3(2.6f, 1.6f, 3.15f)); // 옥상 잔해 더미 위
        AptTNavCut(tower, new Vector3(2.3f, 6.7f, 2.6f), new Vector3(4.6f, 2.6f, 5.4f));      // 303호 (기어서만 들어간다)
        AptTNavCut(tower, new Vector3(10.6f, 6.7f, 0f), new Vector3(4.5f, 2.6f, 10.7f));      // 304호 (문 없음, 구멍으로만)
        AptTNavCut(tower, new Vector3(10.6f, 9.5f, 0f), new Vector3(4.5f, 2.6f, 10.7f));      // 404호 (플레이어가 도망쳐 들어가는 방)
        AptTNavCut(tower, new Vector3(10.6f, 12.3f, 0f), new Vector3(4.5f, 2.6f, 10.7f));     // 504호
        AptTNavCut(tower, new Vector3(10.6f, 17.9f, 0f), new Vector3(4.5f, 2.6f, 10.7f));     // 704호
        AptTNavCut(tower, new Vector3(12.225f, 23.5f, 0.3f), new Vector3(1.05f, 3f, 2.8f));   // 옥상 물탱크 동쪽 좁은 길
        // 설계표에 없는 보험 셋: 아무도 못 오르는 윗면인데, 굽고 나면 1㎡ 안팎의 외딴 메시 조각으로 남을 수 있다
        AptTNavCut(tower, new Vector3(10.95f, 24.6f, 0.3f), new Vector3(1.7f, 1.6f, 2.6f));   // 물탱크 윗면
        AptTNavCut(tower, new Vector3(-6.5f, 3.3f, 6.55f), new Vector3(3.6f, 1.2f, 2.2f));    // 현관 캐노피 윗면 (서)
        AptTNavCut(tower, new Vector3(6.5f, 3.3f, 6.55f), new Vector3(3.6f, 1.2f, 2.2f));     // 현관 캐노피 윗면 (동)
        AptTwSolidCuts(tower);
    }

    // 메운 상자의 속. 내비메시 굽기는 상자 콜라이더를 겉면 삼각형으로만 본다 (속이 빈 껍데기): 상자 밑면이 아래 상자·바닥판의 윗면과 겹치면 그 면이 상자 속의 바닥이 되고,
    // 그 위로 2m가 넘게 비어 있으면(상자 높이) 걷는 곳으로 굽힌다. 도시의 굽기 결과가 그렇다: CityNavMesh.asset 에는 101~103동 Mass 상자 속 바닥(x 94.17..104, y 0.17)에 메시가 있다.
    // 그대로 두면 탑 속에 아무도 못 가는 메시가 열다섯 곳 생기고(1층 덩어리 속 240㎡ 등), 벽 가까이의 적이 그 속을 배회 목적지로 잡아 벽에 붙어 서성인다.
    // 그래서 그런 속마다 걷지 못하는 영역을 넣는다 (괄호 안 = 상자 속 바닥이 되는 면의 높이). 영역은 걷는 곳과 맞닿은 면에서 0.2 넘게 안쪽으로 줄인다:
    // 굽는 격자 한 칸(0.167)이 영역 밖으로 번져도 층계참·계단의 메시를 깎지 않는다. 상자 속 메시는 면에서 0.5 넘게 떨어져 생기므로 줄여도 다 덮인다.
    // 영역 밑면은 상자 속 바닥보다 0.2~0.3 아래에 둔다. 에디터에서 구워 확인한 규칙 (11-16): 영역은 바닥 면의 높이가 그 안에 든 바닥만 지운다.
    // 머리 위 빈 곳에 걸치기만 한 바닥은 그대로다 (마당 뚜껑 영역이 바닥 0.8m 위에 떠 있어도 마당 메시는 남는다).
    // 밑면을 바닥 면과 같은 높이에 두면 지워지지 않는다 (9층 위 몸통 속을 22.4에 맞췄더니 175㎡가 통째로 남았다). 그래서 밑면은 반드시 바닥 면보다 아래다
    static void AptTwSolidCuts(Transform tower)
    {
        AptTwCut(tower, -12.8f, 12.8f, -0.3f, 2.3f, -5.3f, 5.3f);      // 1층 덩어리 속 (땅 윗면 0)
        AptTwCut(tower, -12.8f, 4.35f, 2.3f, 5.2f, -5.05f, 5.05f);     // 01호 줄·계단실 A·02호·03호 아래 채움 속 (1층 덩어리 윗면 2.6)
        AptTwCut(tower, 4.95f, 6.25f, 2.3f, 5.2f, -2.0f, 2.0f);        // 계단실 B L1 서쪽 통로 채움 속 (2.6)
        AptTwCut(tower, 4.95f, 8.05f, 2.3f, 5.2f, 2.4f, 5.05f);        // 계단실 B L1 북 채움 속 (2.6)
        AptTwCut(tower, 0.3f, 4.35f, 5.1f, 8.0f, -5.05f, -0.2f);       // 03호 L2 남쪽 절반 채움 속 (5.4)
        AptTwCut(tower, -8.05f, -6.75f, 7.9f, 10.8f, -2.0f, 2.0f);     // 계단실 A L3 서쪽 통로 채움 속 (8.2)
        AptTwCut(tower, -8.05f, -4.95f, 7.9f, 10.8f, 2.4f, 5.05f);     // 계단실 A L3 북 채움 속 (8.2)
        AptTwCut(tower, 4.95f, 8.05f, 11.0f, 13.6f, -5.05f, 5.05f);    // 계단실 B L4 채움 속 (L3 동쪽 통로·북 채움 윗면 11.0). 밑은 L3 남쪽 층계참·B2 통의 천장
        AptTwCut(tower, 4.95f, 6.25f, 13.5f, 16.4f, -2.0f, 2.0f);      // 계단실 B L5 서쪽 통로 채움 속 (13.8)
        AptTwCut(tower, 4.95f, 8.05f, 13.5f, 16.4f, 2.4f, 5.05f);      // 계단실 B L5 북 채움 속 (13.8)
        AptTwCut(tower, -8.05f, -4.95f, 16.6f, 22.2f, -5.05f, 5.05f);  // 계단실 A L6·L7 채움 속 (L5 동쪽 통로·북 채움 윗면 16.6). 밑은 L5 남쪽 층계참·A4 통의 천장
        AptTwCut(tower, -12.8f, 5.8f, 22.1f, 25.0f, -5.3f, 5.3f);      // 9층 위 몸통 속 (아래 채움과 옥상 바닥판 윗면 22.4). 밑은 L7 남쪽 층계참·B6 통의 천장 (x 4.75..5.8): 걸치기만 하므로 지워지지 않는다
    }

    // 양 끝 좌표로 적는 AptTNavCut ([x0..x1]×[y0..y1]×[z0..z1], 탑 로컬)
    static void AptTwCut(Transform tower, float x0, float x1, float y0, float y1, float z0, float z1)
    {
        AptTNavCut(tower, new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, (z0 + z1) * 0.5f), new Vector3(x1 - x0, y1 - y0, z1 - z0));
    }
}
