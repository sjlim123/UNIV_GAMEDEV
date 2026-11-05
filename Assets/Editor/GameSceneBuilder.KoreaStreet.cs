using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// GameSceneBuilder의 한국 길거리 키트 (맵 개편 세션):
//  인도(I자 보도블록 판·화강석 연석·L형 측구·점자블록·빗물받이), 차선(중앙 황색 복선·흰 점선)·횡단보도·정지선·화살표·맨홀,
//  전봇대와 늘어진 전선(끊어져 늘어진 것도), 녹색 철제 보행자 울타리(쓰러진 것도), 볼라드, 버스 정류장(깨진 유리·광고판·쓰레기통), 가로수(불탄 은행나무), 소화전.
// 도시 배치 난수(rng)는 쓰지 않고 자리에서 뽑은 난수만 쓴다 → 건물 종류·소품·좀비 자리는 예전 도시와 똑같이 나온다
public static partial class GameSceneBuilder
{
    // 치수: 블록 가장자리 17, 건물 선 13.5 → 인도 3.5m (연석 0.2 포함). 길 폭 12m (양방향 2차로)
    const float KBlockHalf = 17f, KLotHalf = 13.5f, KCurbW = 0.2f, KSideY = 0.15f;

    static System.Random KRandom(Vector3 at, int salt) => new System.Random(unchecked(Mathf.RoundToInt(at.x * 7.3f) * 92821 + Mathf.RoundToInt(at.z * 7.3f) * 68917 + salt * 1013));
    static float KF(System.Random r, float a, float b) => a + (float)r.NextDouble() * (b - a);

    // ---------- 인도 ----------

    static void BuildSidewalkRing(Transform b)
    {
        float inner = KLotHalf, outer = KBlockHalf - KCurbW;
        float mid = (inner + outer) / 2f, dep = outer - inner;
        var off = new Vector2(b.localPosition.x, b.localPosition.z); // 블록 자리를 더해 블록끼리도 무늬가 이어진다 (46은 2m의 배수)
        Box("LotGround", b, new Vector3(0, KSideY / 2, 0), new Vector3(inner * 2, KSideY, inner * 2), M["floor"], 3f);
        // 보도블록 띠: 남·북(x -inner~inner), 서·동(z -outer~outer, 모퉁이 포함). 이름은 Sidewalk (발소리 = 돌)
        KSlab("Sidewalk", b, new Vector3(0, KSideY / 2, -mid), new Vector3(inner * 2, KSideY, dep), M["paver"], 2f, off + new Vector2(-inner, -outer));
        KSlab("Sidewalk", b, new Vector3(0, KSideY / 2, mid), new Vector3(inner * 2, KSideY, dep), M["paver"], 2f, off + new Vector2(-inner, inner));
        KSlab("Sidewalk", b, new Vector3(-mid, KSideY / 2, 0), new Vector3(dep, KSideY, outer * 2), M["paver"], 2f, off + new Vector2(-outer, -outer));
        KSlab("Sidewalk", b, new Vector3(mid, KSideY / 2, 0), new Vector3(dep, KSideY, outer * 2), M["paver"], 2f, off + new Vector2(inner, -outer));
        // 화강석 연석 (인도와 같은 높이, 1m마다 이음새)
        float cz = KBlockHalf - KCurbW / 2;
        Box("Sidewalk", b, new Vector3(0, KSideY / 2, -cz), new Vector3(KBlockHalf * 2, KSideY, KCurbW), M["granite"], 1f);
        Box("Sidewalk", b, new Vector3(0, KSideY / 2, cz), new Vector3(KBlockHalf * 2, KSideY, KCurbW), M["granite"], 1f);
        Box("Sidewalk", b, new Vector3(-cz, KSideY / 2, 0), new Vector3(KCurbW, KSideY, outer * 2), M["granite"], 1f);
        Box("Sidewalk", b, new Vector3(cz, KSideY / 2, 0), new Vector3(KCurbW, KSideY, outer * 2), M["granite"], 1f);
        // L형 측구: 연석 밖 콘크리트 띠 (길 높이)
        const float gw = 0.35f;
        float gz = KBlockHalf + gw / 2;
        Box("Gutter", b, new Vector3(0, 0.006f, -gz), new Vector3(KBlockHalf * 2 + gw * 2, 0.01f, gw), M["floor"], 1f, false);
        Box("Gutter", b, new Vector3(0, 0.006f, gz), new Vector3(KBlockHalf * 2 + gw * 2, 0.01f, gw), M["floor"], 1f, false);
        Box("Gutter", b, new Vector3(-gz, 0.006f, 0), new Vector3(gw, 0.01f, KBlockHalf * 2), M["floor"], 1f, false);
        Box("Gutter", b, new Vector3(gz, 0.006f, 0), new Vector3(gw, 0.01f, KBlockHalf * 2), M["floor"], 1f, false);
        // 횡단보도 들머리 (각 변 양 끝, 교차로 가운데에서 8.1m): 점자블록 띠 + 볼라드 둘
        var bollards = new List<CombineInstance>();
        var bands = new List<CombineInstance>();
        for (int side = 0; side < 4; side++)
            foreach (float along in new[] { -14.9f, 14.9f })
            {
                var tp = KSidePoint(side, along, KBlockHalf - KCurbW - 0.3f);
                var ts = side < 2 ? new Vector3(3.6f, 0.005f, 0.6f) : new Vector3(0.6f, 0.005f, 3.6f);
                Box("Tactile", b, tp + Vector3.up * (KSideY + 0.0025f), ts, M["tactile"], 0.3f, false);
                foreach (float d in new[] { -1.45f, 1.45f })
                {
                    var bp = KSidePoint(side, along + d, KBlockHalf - KCurbW - 0.45f);
                    KCylAt(bollards, bp + Vector3.up * (KSideY + 0.42f), 0.075f, 0.84f, Quaternion.identity);
                    KSphereAt(bollards, bp + Vector3.up * (KSideY + 0.84f), 0.075f);
                    KCylAt(bands, bp + Vector3.up * (KSideY + 0.62f), 0.079f, 0.1f, Quaternion.identity);
                    ColliderBox("BollardCollider", b, bp + Vector3.up * (KSideY + 0.45f), new Vector3(0.16f, 0.9f, 0.16f), Vector3.zero);
                }
            }
        CombineInto(b, "Bollards", bollards, M["steelGrey"]);
        CombineInto(b, "BollardBands", bands, M["blastStripe"]);
        // 빗물받이 (측구 위, 각 변 셋)
        for (int side = 0; side < 4; side++)
            foreach (float along in new[] { -7f, 0f, 7f })
            {
                var gp = KSidePoint(side, along + (side % 2 == 0 ? 1.3f : -1.1f), KBlockHalf + 0.18f);
                var gs = side < 2 ? new Vector3(0.5f, 0.006f, 0.3f) : new Vector3(0.3f, 0.006f, 0.5f);
                Box("Drain", b, gp + Vector3.up * 0.016f, gs, M["grate"], 0.5f, false);
            }
    }

    // 블록 변(0 남 1 북 2 서 3 동)의 한 점: along = 변을 따라, dist = 블록 가운데에서 변 쪽으로 (로컬, y 0)
    static Vector3 KSidePoint(int side, float along, float dist) =>
        side == 0 ? new Vector3(along, 0, -dist) : side == 1 ? new Vector3(along, 0, dist) : side == 2 ? new Vector3(-dist, 0, along) : new Vector3(dist, 0, along);

    static Vector3 KSideOut(int side) => side == 0 ? Vector3.back : side == 1 ? Vector3.forward : side == 2 ? Vector3.left : Vector3.right;
    static float KSideYaw(int side) => side < 2 ? 0f : 90f; // 변을 따라가는 방향이 로컬 +x가 되게

    // ---------- 차선·횡단보도·정지선·화살표·맨홀 ----------

    static void BuildRoadMarkings(Transform geo)
    {
        // 예전 BuildRoadLines가 쓰던 도시 난수(지워진 차선 Chance(0.2))만큼 먼저 소비한다 → 뒤의 블록 배치가 예전 도시와 같다
        foreach (var rc in RoadCenters)
            for (float s = -76; s <= 76; s += 6)
            {
                bool nearCross = false;
                foreach (var other in RoadCenters) if (Mathf.Abs(s - other) < 7) nearCross = true;
                if (!nearCross) rng.NextDouble();
            }
        var yellow = new List<CombineInstance>();
        var white = new List<CombineInstance>();
        const float y = 0.004f, t = 0.006f, end = 78f, gap = 10.3f; // gap: 교차로 가운데에서 차선이 끊기는 거리 (횡단보도 너머)
        foreach (var rc in RoadCenters)
            foreach (bool alongZ in new[] { true, false })
            {
                var cuts = new List<float> { -end };
                foreach (var o in RoadCenters) { cuts.Add(o - gap); cuts.Add(o + gap); }
                cuts.Add(alongZ ? end : KCityEastEdge - 8f); // x 방향 길은 동쪽 확장 구간까지
                for (int i = 0; i < cuts.Count; i += 2)
                {
                    float s0 = cuts[i], s1 = cuts[i + 1];
                    if (s1 - s0 < 1f) continue;
                    foreach (float dx in new[] { -0.17f, 0.17f }) KLine(yellow, rc, dx, s0, s1, alongZ, 0.12f, y, t);
                    foreach (float dx in new[] { -3f, 3f })
                        for (float s = s0 + 1f; s < s1 - 1.5f; s += 8f)
                            KLine(white, rc, dx, s, Mathf.Min(s + 3f, s1 - 0.4f), alongZ, 0.12f, y, t);
                }
            }
        // 교차로마다 네 팔의 횡단보도·정지선·차로 화살표 (둘레 길의 바깥쪽 팔은 없다)
        var straight = KPolygon("arrow_s_shaft", new[] { new Vector2(-0.15f, -1.2f), new Vector2(0.15f, -1.2f), new Vector2(0.15f, 0.4f), new Vector2(-0.15f, 0.4f) }, t);
        var head = KPolygon("arrow_s_head", new[] { new Vector2(-0.5f, 0.4f), new Vector2(0.5f, 0.4f), new Vector2(0f, 1.3f) }, t);
        // 좌회전 화살표: 차의 왼쪽(화살표 로컬 -x) 으로 꺾인다
        var leftShaft = KPolygon("arrow_l_shaft", new[] { new Vector2(-0.15f, -1.2f), new Vector2(0.15f, -1.2f), new Vector2(0.15f, 0.5f), new Vector2(-0.15f, 0.5f) }, t);
        var leftBar = KPolygon("arrow_l_bar", new[] { new Vector2(-0.55f, 0.2f), new Vector2(0.15f, 0.2f), new Vector2(0.15f, 0.5f), new Vector2(-0.55f, 0.5f) }, t);
        var leftHead = KPolygon("arrow_l_head", new[] { new Vector2(-0.55f, -0.15f), new Vector2(-0.55f, 0.85f), new Vector2(-1.15f, 0.35f) }, t); // 차로 안(가운데에서 0.35m 밖)에 머문다 — 중앙선과 겹치지 않게
        foreach (var ax in RoadCenters)
            foreach (var az in RoadCenters)
                for (int arm = 0; arm < 4; arm++)
                {
                    // arm: 0 북(+z) 1 남(-z) 2 동(+x) 3 서(-x)
                    if ((arm == 0 && az >= 69f) || (arm == 1 && az <= -69f) || (arm == 3 && ax <= -69f)) continue; // 동쪽 팔은 확장 구간(아파트 단지)으로 이어진다
                    var dir = arm == 0 ? Vector3.forward : arm == 1 ? Vector3.back : arm == 2 ? Vector3.right : Vector3.left;
                    var across = new Vector3(dir.z, 0, -dir.x); // 팔을 보고 선 운전자의 오른쪽 (dir 쪽으로 가는 차는 이 방향 차로에 있다)
                    var c = new Vector3(ax, 0, az);
                    var rot = Quaternion.LookRotation(dir, Vector3.up);
                    // 횡단보도: 줄무늬 12개 (0.5m, 틈 0.5m), 길이 3.6m, 교차로 가장자리(6m)에서 0.3m 밖
                    for (int k = 0; k < 12; k++)
                        white.Add(new CombineInstance { mesh = BoxMesh(new Vector3(0.5f, t, 3.6f), 1.5f), transform = Matrix4x4.TRS(c + dir * 8.1f + across * (-5.5f + k * 1f) + Vector3.up * y, rot, Vector3.one) });
                    // 정지선: 교차로로 들어오는 차로(팔 쪽에서 볼 때 across의 반대쪽 = 다가오는 차의 오른쪽)
                    white.Add(new CombineInstance { mesh = BoxMesh(new Vector3(5.65f, t, 0.45f), 1.5f), transform = Matrix4x4.TRS(c + dir * 10.5f - across * 3.175f + Vector3.up * y, rot, Vector3.one) }); // 중앙선과 0.35m 띄운다
                    // 화살표: 들어오는 차로 둘 (교차로를 향한다 = -dir). 가운데 쪽 차로(1.5)는 좌회전, 연석 쪽 차로(4.5)는 직진
                    var arrowRot = Quaternion.LookRotation(-dir, Vector3.up);
                    float ay = y - t / 2 + 0.0015f; // 차선·정지선 페인트보다 1.5mm 위
                    white.Add(new CombineInstance { mesh = straight, transform = Matrix4x4.TRS(c + dir * 13.8f - across * 4.5f + Vector3.up * ay, arrowRot, Vector3.one) });
                    white.Add(new CombineInstance { mesh = head, transform = Matrix4x4.TRS(c + dir * 13.8f - across * 4.5f + Vector3.up * ay, arrowRot, Vector3.one) });
                    var lp = c + dir * 13.8f - across * 1.5f + Vector3.up * ay;
                    white.Add(new CombineInstance { mesh = leftShaft, transform = Matrix4x4.TRS(lp, arrowRot, Vector3.one) });
                    white.Add(new CombineInstance { mesh = leftBar, transform = Matrix4x4.TRS(lp, arrowRot, Vector3.one) });
                    white.Add(new CombineInstance { mesh = leftHead, transform = Matrix4x4.TRS(lp, arrowRot, Vector3.one) });
                }
        CombineInto(geo, "RoadPaintYellow", yellow, M["paintY"]);
        CombineInto(geo, "RoadPaintWhite", white, M["paintW"]);
        // 맨홀 뚜껑: 길마다 두세 개 (차로 가운데를 피해 차로 안쪽에)
        var manholes = Group("Manholes", geo, Vector3.zero);
        var disc = KDisc("manhole", 0.33f, 24, 0.008f);
        var r = new System.Random(2323);
        foreach (var rc in RoadCenters)
            foreach (bool alongZ in new[] { true, false })
                for (int i = 0, n = 2 + r.Next(2); i < n; i++)
                {
                    float s = KF(r, -74f, 74f);
                    bool near = false;
                    foreach (var o in RoadCenters) if (Mathf.Abs(s - o) < 15f) near = true;
                    if (near) continue;
                    float d = KF(r, 1.2f, 4.6f) * (r.NextDouble() < 0.5 ? -1f : 1f);
                    if (Mathf.Abs(Mathf.Abs(d) - 3f) < 0.5f) d = Mathf.Sign(d) * (Mathf.Abs(d) < 3f ? 2.4f : 3.6f); // 차선 점선(3m) 위를 피한다
                    var p = alongZ ? new Vector3(rc + d, 0.003f, s) : new Vector3(s, 0.003f, rc + d);
                    var go = new GameObject("Manhole");
                    go.transform.SetParent(manholes, false);
                    go.transform.localPosition = p;
                    go.transform.localRotation = Quaternion.Euler(0, KF(r, 0, 360), 0);
                    go.AddComponent<MeshFilter>().sharedMesh = disc;
                    go.AddComponent<MeshRenderer>().sharedMaterial = M["manhole"];
                    GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
                }
    }

    // 길(rc)을 따라 s0~s1 구간의 페인트 띠 (dx: 길 가운데에서 옆으로)
    static void KLine(List<CombineInstance> parts, float rc, float dx, float s0, float s1, bool alongZ, float width, float y, float t)
    {
        float len = s1 - s0, mid = (s0 + s1) / 2f;
        var size = alongZ ? new Vector3(width, t, len) : new Vector3(len, t, width);
        var pos = alongZ ? new Vector3(rc + dx, y, mid) : new Vector3(mid, y, rc + dx);
        parts.Add(new CombineInstance { mesh = BoxMesh(size, 1.5f), transform = Matrix4x4.TRS(pos, Quaternion.identity, Vector3.one) });
    }

    // ---------- 길가 설비 ----------

    static void BuildStreetFurniture(Transform geo, Transform points)
    {
        var street = Group("StreetFurniture", geo, Vector3.zero);
        BuildUtilityPoles(street);
        foreach (var bx in new[] { -46f, 0f, 46f, KEastBlockX })
            foreach (var bz in new[] { -46f, 0f, 46f })
            {
                var c = new Vector3(bx, 0, bz);
                var r = KRandom(c, 11);
                var blockGroup = Group($"StreetKit_{bx}_{bz}", street, c);
                int busSide = bx >= KEastBlockX ? r.Next(3) : r.Next(4); // 동쪽 블록의 동쪽 변은 둘레 벽
                // 버스 정류장: 블록마다 한 변, 변의 along 1.8~5.8
                var busPos = KSidePoint(busSide, 3.8f, KBlockHalf - 0.8f);
                BuildBusStop(blockGroup, busPos, KSideYaw(busSide), busSide, r, points);
                // 녹색 울타리: 버스 정류장이 없는 변 중 둘 (연석 안쪽 0.45m)
                int fenceA = (busSide + 1 + r.Next(3)) % 4, fenceB = (fenceA + 1 + r.Next(2)) % 4;
                if (fenceB == busSide) fenceB = (fenceB + 1) % 4;
                foreach (int side in new[] { fenceA, fenceB })
                    if (side != busSide) BuildStreetFence(blockGroup, side, r);
                // 소화전: 블록 둘 중 하나, 모퉁이 가까이
                if (r.NextDouble() < 0.5)
                {
                    int hs = r.Next(4);
                    BuildHydrant(blockGroup, KSidePoint(hs, KF(r, -12.5f, -11.5f), KBlockHalf - 0.75f), KF(r, 0, 360));
                }
                // 가로수: 변마다 along ±8 (절반은 가운데 0에도), 연석 안쪽 1.0m의 나무 구덩이. 버스 정류장 자리(1.8~6.4)는 비켜 간다
                for (int side = 0; side < 4; side++)
                    foreach (float along in new[] { -8f, 8f, 0f })
                    {
                        if (along == 0f && r.NextDouble() < 0.5) continue;
                        StreetTree(blockGroup, KSidePoint(side, along, KBlockHalf - KCurbW - 1.0f) + Vector3.up * KSideY);
                    }
            }
    }

    // 예전 배치 함수가 쓰던 만큼 도시 난수를 소비한다 (뒤의 배치가 바뀌지 않게)
    static void KBurnDraws(int n)
    {
        for (int i = 0; i < n; i++) rng.NextDouble();
    }

    // 전봇대: x 방향 길은 북쪽 인도(둘레 길 69는 남쪽), z 방향 길은 동쪽 인도(69는 서쪽)에 블록마다 둘(±11). 사이를 전선이 잇는다
    static int kFallenPoles;

    static void BuildUtilityPoles(Transform parent)
    {
        kFallenPoles = 0;
        var poleMesh = KLathe("utility_pole", new[] { new Vector2(0.19f, 0f), new Vector2(0.17f, 2f), new Vector2(0.14f, 6f), new Vector2(0.11f, 10f), new Vector2(0.1f, 10.3f) }, 14, 2f);
        foreach (var rc in RoadCenters)
            foreach (bool alongX in new[] { true, false })
            {
                float sideDist = rc >= 69f ? rc - 6.6f : rc + 6.6f;
                var wires = new List<CombineInstance>();
                var tops = new List<Vector3[]>();
                var r = new System.Random(unchecked(Mathf.RoundToInt(rc) * 131 + (alongX ? 7 : 3)));
                var poleList = new List<float>();
                foreach (var bc in alongX ? new[] { -46f, 0f, 46f, KEastBlockX } : new[] { -46f, 0f, 46f })
                    foreach (var d in new[] { -11f, 11f })
                        poleList.Add(bc + d);
                foreach (float along in poleList)
                {
                    var pos = alongX ? new Vector3(along, KSideY, sideDist) : new Vector3(sideDist, KSideY, along);
                    float yaw = alongX ? 0f : 90f; // 팔은 길과 직각 (로컬 z가 길과 직각이 되게 로컬 x를 길 방향에 맞춘다)
                    // 쓰러진 전봇대 (도시에 다섯까지): 밑동이 부러져 길 쪽으로 기운 채 전선에 걸려 있다. 출발점·해치·둘레 근처는 뺀다
                    var toRoad = alongX ? new Vector3(0, 0, Mathf.Sign(rc - sideDist)) : new Vector3(Mathf.Sign(rc - sideDist), 0, 0);
                    bool fallen = kFallenPoles < 5 && Mathf.Abs(along) < 60f && Vector3.Distance(pos, SpawnPos) > 25f && Vector3.Distance(pos, CityHatchPos) > 25f && KRandom(pos, 77).NextDouble() < 0.09;
                    if (fallen) kFallenPoles++;
                    tops.Add(BuildUtilityPole(parent, pos, yaw, poleMesh, r, fallen, toRoad));
                }
                // 전선: 이웃 전봇대끼리 (팔 위 애자 3+3, 아래 통신선 다발). 둘레 길의 마지막 전봇대 너머로는 끊긴 선이 늘어진다
                for (int i = 0; i < tops.Count; i++)
                {
                    if (i + 1 < tops.Count)
                    {
                        var a = tops[i]; var b = tops[i + 1];
                        for (int k = 0; k < 6; k++) KWire(wires, a[k], b[k], KF(r, 0.7f, 1.0f), 0.012f, 6);
                        KWire(wires, a[6], b[6], KF(r, 1.2f, 1.6f), 0.035f, 6);
                    }
                    // 끊어져 늘어진 선 (4대 중 하나꼴): 애자에서 길바닥으로
                    if (r.NextDouble() < 0.25)
                    {
                        int k = r.Next(6);
                        var fall = tops[i][k] + new Vector3(KF(r, -3f, 3f), -tops[i][k].y + 0.05f, KF(r, -3f, 3f));
                        KWire(wires, tops[i][k], fall, KF(r, 0.2f, 0.5f), 0.012f, 5);
                    }
                }
                CombineInto(parent, "PowerLines", wires, M["cable"]);
            }
    }

    // 전봇대 하나. 돌려주는 것: 전선 걸이 7곳 (팔 위 애자 6 + 통신선 걸이 1)의 월드 자리. fallen이면 towardRoad 쪽으로 74~80° 기울고 밑동만 남는다
    static Vector3[] BuildUtilityPole(Transform parent, Vector3 pos, float yaw, Mesh poleMesh, System.Random r, bool fallen = false, Vector3 towardRoad = default(Vector3))
    {
        var g = Group("UtilityPole", parent, pos, yaw);
        g.localRotation *= Quaternion.Euler(KF(r, -1.2f, 1.2f), 0, KF(r, -1.2f, 1.2f)); // 살짝 기운다
        if (fallen)
        {
            var axis = Vector3.Cross(Vector3.up, towardRoad).normalized;
            g.rotation = Quaternion.AngleAxis(KF(r, 74f, 80f), axis) * g.rotation;
            g.position = pos + towardRoad * 0.35f + Vector3.up * 0.3f;
            // 밑동 (원뿔대) + 삐져나온 철근
            var stump = new GameObject("Stump");
            stump.transform.SetParent(parent, false);
            stump.transform.position = pos;
            stump.AddComponent<MeshFilter>().sharedMesh = KLathe("pole_stump", new[] { new Vector2(0.2f, 0f), new Vector2(0.19f, 0.5f), new Vector2(0.1f, 0.8f) }, 12, 1f);
            stump.AddComponent<MeshRenderer>().sharedMaterial = M["poleConcrete"];
            GameObjectUtility.SetStaticEditorFlags(stump, StaticEditorFlags.BatchingStatic);
            var sc = stump.AddComponent<CapsuleCollider>();
            sc.radius = 0.2f; sc.height = 0.8f; sc.center = new Vector3(0, 0.4f, 0);
            Rebar(stump.transform, new Vector3(0.05f, 0.75f, 0.05f), new Vector3(0.3f, 1f, 0.2f), 0.5f, r);
            Rebar(stump.transform, new Vector3(-0.06f, 0.75f, -0.03f), new Vector3(-0.4f, 0.8f, 0.3f), 0.45f, r);
        }
        var post = new GameObject("Post");
        post.transform.SetParent(g, false);
        post.AddComponent<MeshFilter>().sharedMesh = poleMesh;
        post.AddComponent<MeshRenderer>().sharedMaterial = M["poleConcrete"];
        GameObjectUtility.SetStaticEditorFlags(post, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
        var cap = g.gameObject.AddComponent<CapsuleCollider>();
        cap.radius = 0.2f;
        cap.height = 10.4f;
        cap.center = new Vector3(0, 5.2f, 0);
        // 아래 경고 띠 (노랑·검정 고리)
        var ringsY = new List<CombineInstance>();
        var ringsK = new List<CombineInstance>();
        for (int i = 0; i < 6; i++)
            KCylAt(i % 2 == 0 ? ringsY : ringsK, new Vector3(0, 1.0f + i * 0.2f + 0.1f, 0), 0.185f - i * 0.002f, 0.2f, Quaternion.identity);
        CombineInto(g, "BandYellow", ringsY, M["blastStripe"]);
        CombineInto(g, "BandBlack", ringsK, M["tire"]);
        // 팔 둘 + 애자, 통신선 걸이, 변압기(5대 중 2대), 꼭대기 캡
        var steel = new List<CombineInstance>();
        var ceramic = new List<CombineInstance>();
        var hooks = new Vector3[7];
        float[] armY = { 8.7f, 9.5f };
        for (int a = 0; a < 2; a++)
        {
            KPart(steel, new Vector3(0.09f, 0.09f, 2.1f), 1f, new Vector3(0, armY[a], 0), Quaternion.identity);
            KPart(steel, new Vector3(0.05f, 0.5f, 0.05f), 1f, new Vector3(0.14f, armY[a] - 0.3f, 0.5f), Quaternion.Euler(-32f, 0, 0)); // 버팀대
            KPart(steel, new Vector3(0.05f, 0.5f, 0.05f), 1f, new Vector3(0.14f, armY[a] - 0.3f, -0.5f), Quaternion.Euler(32f, 0, 0));
            for (int k = 0; k < 3; k++)
            {
                float z = -0.85f + k * 0.85f;
                KCylAt(ceramic, new Vector3(0, armY[a] + 0.13f, z), 0.055f, 0.18f, Quaternion.identity);
                KCylAt(ceramic, new Vector3(0, armY[a] + 0.2f, z), 0.07f, 0.03f, Quaternion.identity);
                hooks[a * 3 + k] = g.TransformPoint(new Vector3(0, armY[a] + 0.24f, z));
            }
        }
        KPart(steel, new Vector3(0.12f, 0.16f, 0.12f), 1f, new Vector3(0.12f, 5.8f, 0), Quaternion.identity); // 통신선 걸이
        hooks[6] = g.TransformPoint(new Vector3(0.18f, 5.8f, 0));
        KCylAt(steel, new Vector3(0, 10.34f, 0), 0.12f, 0.06f, Quaternion.identity);
        if (r.NextDouble() < 0.4)
        {
            KCylAt(steel, new Vector3(0, 7.3f, 0.55f), 0.3f, 0.95f, Quaternion.identity);
            KCylAt(steel, new Vector3(0, 7.8f, 0.55f), 0.33f, 0.04f, Quaternion.identity);
            KCylAt(steel, new Vector3(0, 6.8f, 0.55f), 0.33f, 0.04f, Quaternion.identity);
            KPart(steel, new Vector3(0.1f, 0.1f, 0.6f), 1f, new Vector3(0, 6.9f, 0.28f), Quaternion.identity);
            KPart(steel, new Vector3(0.1f, 0.1f, 0.6f), 1f, new Vector3(0, 7.7f, 0.28f), Quaternion.identity);
            KRod(steel, new Vector3(0, 8.0f, 0.55f), new Vector3(0, 8.7f, 0.2f), 0.02f); // 변압기 인입선
        }
        // 전단지·번호판 (눈높이 작은 판)
        KPart(steel, new Vector3(0.14f, 0.2f, 0.01f), 1f, new Vector3(0.0f, 2.9f, 0.185f), Quaternion.identity);
        CombineInto(g, "Hardware", steel, M["steelGrey"]);
        CombineInto(g, "Insulators", ceramic, M["insulator"]);
        return hooks;
    }

    // 녹색 보행자 울타리: side 변의 연석 안쪽(0.45m)에 2m 마디로 along -10~10. 몇 마디는 빠지거나 쓰러져 있다
    static void BuildStreetFence(Transform parent, int side, System.Random r)
    {
        var parts = new List<CombineInstance>();
        var rot = Quaternion.Euler(0, KSideYaw(side), 0);
        float dist = KBlockHalf - KCurbW - 0.45f;
        for (float a = -10f; a < 10f - 0.01f; a += 2f)
        {
            var c = KSidePoint(side, a + 1f, dist) + Vector3.up * KSideY;
            double roll = r.NextDouble();
            if (roll < 0.14) continue; // 빠진 마디
            bool fallen = roll < 0.26;
            var segRot = rot;
            if (fallen)
            {
                // 인도 안쪽으로 쓰러져 누웠다 (기둥이 뽑힌 채, 밑동을 축으로)
                segRot = rot * Quaternion.Euler(side == 0 || side == 2 ? 82f : -82f, 0, KF(r, -6f, 6f));
            }
            KFenceSegment(parts, c, segRot);
            if (!fallen) ColliderBox("FenceCollider", parent, c + Vector3.up * 0.5f, new Vector3(2.0f, 1.0f, 0.1f), new Vector3(0, KSideYaw(side), 0));
        }
        CombineInto(parent, "Fence", parts, M["fenceGreen"]);
    }

    // 울타리 한 마디 (로컬 x 방향 2m, 원점 = 바닥 가운데): 기둥 둘, 가로대 셋, 세로살 일곱
    static void KFenceSegment(List<CombineInstance> parts, Vector3 pos, Quaternion rot)
    {
        foreach (float x in new[] { -0.98f, 0.98f })
        {
            KPart(parts, new Vector3(0.06f, 1.0f, 0.06f), 1f, pos + rot * new Vector3(x, 0.5f, 0), rot);
            KSphereAt(parts, pos + rot * new Vector3(x, 1.02f, 0), 0.04f);
        }
        foreach (float y in new[] { 0.14f, 0.56f, 0.94f })
            KBar(parts, new Vector3(1.92f, 0.045f, 0.045f), pos + rot * new Vector3(0, y, 0), rot);
        for (int i = 0; i < 5; i++) // 살 7 → 5, 뚜껑 없는 막대 (메시 용량)
            KBar(parts, new Vector3(0.022f, 0.8f, 0.022f), pos + rot * new Vector3(-0.7f + i * 0.35f, 0.54f, 0), rot);
    }

    // 버스 정류장 (로컬 x = 인도 방향, +z = 길 쪽): 강관 기둥 넷, 지붕, 뒤 유리벽(반은 깨짐), 벤치, 표지판 기둥, 광고판, 쓰레기통 둘
    static void BuildBusStop(Transform parent, Vector3 pos, float yaw, int side, System.Random r, Transform points)
    {
        var g = Group("BusStop", parent, pos + Vector3.up * KSideY, yaw);
        // 변마다 +z가 길 쪽이 되게 돌린다 (남·서 변은 바깥이 -방향)
        if (side == 0 || side == 2) g.localRotation *= Quaternion.Euler(0, 180f, 0);
        var frame = new List<CombineInstance>();
        var steel = M["steelGrey"];
        foreach (float x in new[] { -1.9f, 1.9f })
        {
            KCylAt(frame, new Vector3(x, 1.3f, -0.5f), 0.04f, 2.6f, Quaternion.identity);
            KCylAt(frame, new Vector3(x, 1.3f, 0.55f), 0.04f, 2.6f, Quaternion.identity);
            KCylAt(frame, new Vector3(x, 2.6f, 0.02f), 0.035f, 1.1f, Quaternion.Euler(90, 0, 0));
        }
        KPart(frame, new Vector3(4.2f, 0.07f, 1.5f), 1f, new Vector3(0, 2.66f, 0.02f), Quaternion.Euler(-4f, 0, 0));
        KPart(frame, new Vector3(4.2f, 0.28f, 0.05f), 1f, new Vector3(0, 2.5f, 0.78f), Quaternion.identity); // 앞 가리개
        // 벤치
        KPart(frame, new Vector3(2.0f, 0.06f, 0.42f), 1f, new Vector3(0, 0.46f, -0.18f), Quaternion.identity);
        KPart(frame, new Vector3(2.0f, 0.06f, 0.1f), 1f, new Vector3(0, 0.8f, -0.42f), Quaternion.Euler(-12f, 0, 0));
        foreach (float x in new[] { -0.85f, 0.85f })
        {
            KPart(frame, new Vector3(0.05f, 0.46f, 0.05f), 1f, new Vector3(x, 0.23f, -0.18f), Quaternion.identity);
            KPart(frame, new Vector3(0.05f, 0.4f, 0.05f), 1f, new Vector3(x, 0.66f, -0.42f), Quaternion.Euler(-12f, 0, 0));
        }
        CombineInto(g, "Frame", frame, steel);
        // 뒤 유리벽: 멀쩡하거나 깨짐 (틀만 남고 조각이 매달림, 바닥에 유리 가루)
        bool broken = r.NextDouble() < 0.55;
        if (!broken) Box("BackGlass", g, new Vector3(0, 1.32f, -0.55f), new Vector3(3.76f, 2.2f, 0.02f), M["shopGlass"], 1f, false).GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        else
        {
            var shards = new List<CombineInstance>();
            for (int i = 0, n = 5 + r.Next(5); i < n; i++)
            {
                float sx = KF(r, -1.8f, 1.8f), sy = KF(r, 0.3f, 2.3f);
                bool top = r.NextDouble() < 0.6;
                KPart(shards, new Vector3(KF(r, 0.12f, 0.4f), KF(r, 0.15f, 0.5f), 0.02f), 1f, new Vector3(sx, top ? 2.4f - KF(r, 0.1f, 0.3f) : 0.25f + KF(r, 0.05f, 0.2f), -0.55f), Quaternion.Euler(0, 0, KF(r, -25f, 25f)));
            }
            for (int i = 0; i < 14; i++)
                KPart(shards, new Vector3(KF(r, 0.04f, 0.12f), 0.006f, KF(r, 0.04f, 0.12f)), 1f, new Vector3(KF(r, -2f, 2f), 0.004f, KF(r, -0.9f, 0.4f)), Quaternion.Euler(0, KF(r, 0, 360), 0));
            CombineInto(g, "Shards", shards, M["shopGlass"]);
        }
        ColliderBox("BackCollider", g, new Vector3(0, 1.3f, -0.55f), new Vector3(3.8f, 2.6f, 0.1f), Vector3.zero);
        ColliderBox("BenchCollider", g, new Vector3(0, 0.3f, -0.25f), new Vector3(2.0f, 0.6f, 0.5f), Vector3.zero);
        // 표지판 기둥 (길 쪽 모서리)
        var sign = new List<CombineInstance>();
        KCylAt(sign, new Vector3(2.45f, 1.45f, 0.45f), 0.035f, 2.9f, Quaternion.identity);
        KPart(sign, new Vector3(0.7f, 0.36f, 0.03f), 1f, new Vector3(2.45f + 0.37f, 2.55f, 0.45f), Quaternion.identity); // 판은 기둥 옆에 (기둥이 글자를 가리지 않게)
        KPart(sign, new Vector3(0.1f, 0.03f, 0.03f), 1f, new Vector3(2.45f + 0.05f, 2.7f, 0.45f), Quaternion.identity);
        KPart(sign, new Vector3(0.1f, 0.03f, 0.03f), 1f, new Vector3(2.45f + 0.05f, 2.4f, 0.45f), Quaternion.identity);
        CombineInto(g, "SignPole", sign, steel);
        // 쿼드는 -z를 보므로 길 쪽(+z) 면은 180도 돌린다
        Prim(PrimitiveType.Quad, "SignFace", g, new Vector3(2.45f + 0.37f, 2.55f, 0.468f), new Vector3(0.66f, 0.33f, 1f), M["busSign"], new Vector3(0, 180, 0));
        Prim(PrimitiveType.Quad, "SignBack", g, new Vector3(2.45f + 0.37f, 2.55f, 0.432f), new Vector3(0.66f, 0.33f, 1f), M["busSign"]);
        ColliderBox("SignCollider", g, new Vector3(2.45f, 1.45f, 0.45f), new Vector3(0.1f, 2.9f, 0.1f), Vector3.zero);
        // 광고판 (뒤 유리 옆, 어두운 유리와 꺼진 등)
        var ad = Group("AdPanel", g, new Vector3(-2.35f, 0, -0.5f));
        Box("Frame", ad, new Vector3(0, 1.3f, 0), new Vector3(0.9f, 2.0f, 0.14f), steel, 1f, true);
        Box("Glass", ad, new Vector3(0, 1.3f, 0.075f), new Vector3(0.8f, 1.9f, 0.01f), M["shopGlass"], 1f, false);
        Box("Tube", ad, new Vector3(0, 1.3f, 0.062f), new Vector3(0.04f, 1.7f, 0.01f), M["tire"], 1f, false);
        // 쓰레기통 둘 (일반·재활용)
        var bins = Group("Bins", g, new Vector3(-3.05f, 0, 0.1f));
        Box("BinA", bins, new Vector3(-0.25f, 0.45f, 0), new Vector3(0.42f, 0.9f, 0.42f), M["fenceGreen"], 1f, true, new Vector3(0, KF(r, -8, 8), 0));
        Box("BinB", bins, new Vector3(0.25f, 0.45f, 0), new Vector3(0.42f, 0.9f, 0.42f), M["paintBlue"], 1f, true, new Vector3(0, KF(r, -8, 8), 0));
        Box("Lid", bins, new Vector3(-0.25f, 0.92f, 0), new Vector3(0.44f, 0.04f, 0.44f), M["tire"], 1f, false);
        Box("Lid", bins, new Vector3(0.25f, 0.92f, 0), new Vector3(0.44f, 0.04f, 0.44f), M["tire"], 1f, false);
        // 벤치 위 아이템 자리 (바깥 자리: 돌만 나온다)
        OutdoorItemPoint(points, g.TransformPoint(new Vector3(0.6f, 0.55f, -0.18f)));
    }

    // 소화전: 붉은 몸통·뚜껑·토출구 둘
    static void BuildHydrant(Transform parent, Vector3 pos, float yaw)
    {
        var g = Group("Hydrant", parent, pos + Vector3.up * KSideY, yaw);
        var parts = new List<CombineInstance>();
        KCylAt(parts, new Vector3(0, 0.32f, 0), 0.11f, 0.64f, Quaternion.identity);
        KCylAt(parts, new Vector3(0, 0.03f, 0), 0.15f, 0.06f, Quaternion.identity);
        KSphereAt(parts, new Vector3(0, 0.66f, 0), 0.11f);
        KCylAt(parts, new Vector3(0, 0.78f, 0), 0.045f, 0.08f, Quaternion.identity);
        foreach (float s in new[] { -1f, 1f }) KCylAt(parts, new Vector3(s * 0.15f, 0.42f, 0), 0.055f, 0.14f, Quaternion.Euler(0, 0, 90));
        CombineInto(g, "Body", parts, M["paintRed"]);
        ColliderBox("HydrantCollider", g, new Vector3(0, 0.4f, 0), new Vector3(0.36f, 0.8f, 0.24f), Vector3.zero);
    }

    // 가로수 (불탄 은행나무): 격자 덮개가 있는 나무 구덩이, 그을린 줄기, 가지 몇, 잔가지 (난수는 자리에서 뽑는다)
    static void StreetTree(Transform parent, Vector3 pos)
    {
        var r = KRandom(parent.TransformPoint(pos), 5);
        var g = Group("StreetTree", parent, pos, KF(r, 0, 360));
        Box("TreeGrate", g, new Vector3(0, 0.012f, 0), new Vector3(1.2f, 0.024f, 1.2f), M["treeGrate"], 0.6f, false);
        Box("TreeSoil", g, new Vector3(0, 0.004f, 0), new Vector3(0.5f, 0.012f, 0.5f), M["ashPile"], 1f, false);
        bool stump = r.NextDouble() < 0.18;
        float h = stump ? KF(r, 1.4f, 2.4f) : KF(r, 4.2f, 5.6f);
        var trunk = new GameObject("Trunk");
        trunk.transform.SetParent(g, false);
        trunk.transform.localRotation = Quaternion.Euler(KF(r, -4f, 4f), 0, KF(r, -4f, 4f));
        string key = "trunk_" + (stump ? "s" : "t") + Mathf.RoundToInt(h * 10f);
        trunk.AddComponent<MeshFilter>().sharedMesh = KLathe(key, new[] { new Vector2(0.2f, 0f), new Vector2(0.16f, 0.4f), new Vector2(0.13f, h * 0.45f), new Vector2(0.1f, h * 0.8f), new Vector2(stump ? 0.09f : 0.05f, h) }, 10, 1f);
        trunk.AddComponent<MeshRenderer>().sharedMaterial = M["bark"];
        GameObjectUtility.SetStaticEditorFlags(trunk, StaticEditorFlags.BatchingStatic);
        var cap = g.gameObject.AddComponent<CapsuleCollider>();
        cap.radius = 0.18f;
        cap.height = Mathf.Min(h, 3f);
        cap.center = new Vector3(0, cap.height / 2f, 0);
        var limbs = new List<CombineInstance>();
        if (!stump)
        {
            int n = 3 + r.Next(3);
            for (int i = 0; i < n; i++)
            {
                float y0 = KF(r, h * 0.5f, h * 0.9f);
                float a = KF(r, 0, Mathf.PI * 2f);
                var dir = new Vector3(Mathf.Cos(a) * KF(r, 0.5f, 1f), KF(r, 0.5f, 1.1f), Mathf.Sin(a) * KF(r, 0.5f, 1f)).normalized;
                float len = KF(r, 1.0f, 2.2f);
                var start = new Vector3(0, y0, 0);
                var end = start + dir * len;
                KRod(limbs, start, end, 0.045f);
                for (int k = 0; k < 2; k++)
                {
                    var from = Vector3.Lerp(start, end, KF(r, 0.45f, 0.95f));
                    var tdir = (dir + new Vector3(KF(r, -0.7f, 0.7f), KF(r, 0.2f, 0.9f), KF(r, -0.7f, 0.7f))).normalized;
                    var tend = from + tdir * KF(r, 0.5f, 1.1f);
                    KRod(limbs, from, tend, 0.02f);
                    if (r.NextDouble() < 0.6) KRod(limbs, Vector3.Lerp(from, tend, 0.6f), tend + new Vector3(KF(r, -0.4f, 0.4f), KF(r, 0.1f, 0.5f), KF(r, -0.4f, 0.4f)), 0.012f);
                }
            }
        }
        else
        {
            // 부러진 꼭대기: 쪼개진 조각 몇 개
            for (int i = 0; i < 3; i++)
                KPart(limbs, new Vector3(0.05f, KF(r, 0.2f, 0.5f), 0.05f), 1f, new Vector3(KF(r, -0.06f, 0.06f), h + 0.1f, KF(r, -0.06f, 0.06f)), Quaternion.Euler(KF(r, -25f, 25f), KF(r, 0, 360), KF(r, -25f, 25f)));
        }
        CombineInto(trunk.transform, "Limbs", limbs, M["bark"]);
    }
}
