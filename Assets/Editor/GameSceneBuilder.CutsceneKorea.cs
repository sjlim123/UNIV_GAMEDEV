using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// GameSceneBuilder의 컷씬 거리 한국 길거리 꾸밈 (맵 개편 세션의 Korea*.cs 부품을 그대로 쓴다):
// 상가 빌딩(통유리 상점·유리문·가로 간판·돌출 간판·새시 창·실외기·옥상·홈통), 차선·횡단보도·정지선·연석·점자 블록·맨홀,
// 전봇대와 전선, 버스 정류장, 소화전, 가로수, 현수막. 거실 창밖 먼 줄의 삼일아파트 둘.
// 재질·그림은 도시 씬을 만들 때 저장된 것을 불러오고, 없으면 그때 만든다
public static partial class GameSceneBuilder
{
    static void CsKoreaMaterials()
    {
        var want = new Dictionary<string, string>
        {
            { "paver", "M_StreetPaver" }, { "granite", "M_StreetGranite" }, { "tactile", "M_StreetTactile" }, { "grate", "M_StreetGrate" }, { "manhole", "M_StreetManhole" }, { "busSign", "M_BusStopSign" },
            { "paintW", "M_RoadPaintWhite" }, { "paintY", "M_RoadPaintYellow" }, { "fenceGreen", "M_StreetFence" }, { "poleConcrete", "M_PolePost" }, { "cable", "M_Cable" }, { "insulator", "M_Insulator" },
            { "steelGrey", "M_SteelGrey" }, { "bark", "M_Bark" }, { "treeGrate", "M_TreeGrate" }, { "lampHead", "M_LampHead" }, { "shopGlass", "M_ShopGlass" },
            { "shutter", "M_Shutter" }, { "whitePaint", "M_WhitePaint" }, { "frameDark", "M_FrameDark" }, { "acUnit", "M_AcUnit" }, { "awningRed", "M_AwningRed" }, { "awningGreen", "M_AwningGreen" }, { "tileStone", "M_TileStone" },
            { "aptWall", "M_AptWall" }, { "aptBand", "M_AptBand" }, { "aptRail", "M_AptRail" }, { "aptGate", "M_AptGate" },
        };
        for (int i = 0; i < 3; i++) { want["aptNum" + i] = "M_AptStripe_" + (101 + i); want["aptPlate" + i] = "M_AptPlate_" + (101 + i); }
        for (int i = 0; i < KShops.Length; i++) { want["sign" + i] = "M_Sign_" + i; want["signLit" + i] = "M_SignLit_" + i; }
        for (int i = 0; i < KSideSigns.Length; i++) want["sideSign" + i] = "M_SideSign_" + i;
        for (int i = 0; i < KBanners.Length; i++) want["banner" + i] = "M_Banner_" + i;
        bool missing = false;
        foreach (var kv in want)
        {
            if (M.TryGetValue(kv.Key, out var have) && have != null) continue;
            var m = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/{kv.Value}.mat");
            if (m != null) M[kv.Key] = m;
            else missing = true;
        }
        if (missing)
        {
            Debug.Log("[GameSceneBuilder] 한국 길거리 재질이 없어 새로 만듭니다 (도시 씬을 먼저 만들어 두면 더 빠릅니다)");
            KoreaMaterials();
            KoreaShopMaterials();
            KoreaApartmentMaterials();
        }
        if (!M.ContainsKey("soot") || !M.ContainsKey("ashPile")) RuinMaterials();
        M["parkPaint"] = M["paintW"];
    }

    // 길가 상가 빌딩 (맵의 TallBuilding과 같은 부품): 덩어리 + 흰 새시 창·실외기 + 길 쪽 1층 통유리 상점·유리문 + 층마다 가로 간판·돌출 간판 + 옥상 + 홈통.
    // side: 길의 서쪽(-1)·동쪽(+1). 간판은 길 쪽 면과 남쪽 면(광장·카메라 쪽)에
    static void CsKoreaBuilding(Transform root, float side, float x0, float z0, float w, float d, float h)
    {
        var g = Group("Building", root, new Vector3(0, 0.15f, 0));
        var mat = Pick(M["wall"], M["brick"], M["cracked"]);
        CsBox("Mass", g, new Vector3(x0 + w / 2, h / 2, z0 + d / 2), new Vector3(w, h, d), mat, null, 3f);
        int floors = Mathf.Max(1, Mathf.FloorToInt((h - 3.5f) / 3.2f));
        var r = KRandom(new Vector3(x0, 0, z0), 57);
        int roadSide = side < 0 ? 3 : 2;
        KUpperWindows(g, x0, z0, w, d, 3.5f, floors, r);
        KTowerShopfront(g, roadSide, x0, z0, w, d, r);
        KTowerSigns(g, roadSide, 0, x0, z0, w, d, floors, r);
        KRooftop(g, x0, z0, w, d, h, r);
        KDownpipe(g, roadSide, 0.5f, x0, z0, w, d, h);
    }

    // 차선·횡단보도·정지선·연석·점자 블록·맨홀 (맵의 BuildRoadMarkings와 같은 재질·치수). 길: x -7~7, 광장(z 0~7) 앞에서 z 100까지
    static void CsKoreaRoadPaint(Transform root)
    {
        var yellow = new List<CombineInstance>();
        var white = new List<CombineInstance>();
        const float y = 0.004f, t = 0.006f;
        // 가운데 겹선(노랑)·차로선(흰 점선)·가장자리 실선
        foreach (float dx in new[] { -0.17f, 0.17f }) KLine(yellow, 0f, dx, 14.5f, 100f, true, 0.12f, y, t);
        foreach (float dx in new[] { -3.5f, 3.5f })
            for (float s = 15.5f; s < 98f; s += 8f) KLine(white, 0f, dx, s, s + 3f, true, 0.12f, y, t);
        foreach (float dx in new[] { -6.75f, 6.75f }) KLine(white, 0f, dx, 14.5f, 100f, true, 0.15f, y, t);
        // 횡단보도 둘 (광장 앞 z 9.3~12.9, 길 가운데 z 61.3~64.9): 줄무늬 12개 + 양쪽 차로의 정지선 (우측통행: 남쪽으로 가는 차는 서쪽 차로)
        foreach (float zc in new[] { 11.1f, 63.1f })
        {
            for (int k = 0; k < 12; k++)
                white.Add(new CombineInstance { mesh = BoxMesh(new Vector3(0.5f, t, 3.6f), 1.5f), transform = Matrix4x4.TRS(new Vector3(-5.5f + k * 1f, y, zc), Quaternion.identity, Vector3.one) });
            white.Add(new CombineInstance { mesh = BoxMesh(new Vector3(6.2f, t, 0.45f), 1.5f), transform = Matrix4x4.TRS(new Vector3(-3.5f, y, zc + 2.45f), Quaternion.identity, Vector3.one) });
            white.Add(new CombineInstance { mesh = BoxMesh(new Vector3(6.2f, t, 0.45f), 1.5f), transform = Matrix4x4.TRS(new Vector3(3.5f, y, zc - 2.45f), Quaternion.identity, Vector3.one) });
        }
        CombineInto(root, "RoadPaintYellow", yellow, M["paintY"]);
        CombineInto(root, "RoadPaintWhite", white, M["paintW"]);
        // 연석: 인도 가장자리(x ±7)의 화강암 연석 돌 (인도보다 1cm 높다), 횡단보도 앞 인도의 점자 블록
        var curb = new List<CombineInstance>();
        for (float s = 7.5f; s < 103f; s += 1f)
            foreach (float sx in new[] { -1f, 1f })
                KPart(curb, new Vector3(0.2f, 0.16f, 0.98f), 1f, new Vector3(sx * 6.95f, 0.08f, s), Quaternion.identity);
        CombineInto(root, "Curbs", curb, M["granite"]);
        foreach (float zc in new[] { 11.1f, 63.1f })
            foreach (float sx in new[] { -1f, 1f })
                Box("Tactile", root, new Vector3(sx * 7.75f, 0.153f, zc), new Vector3(1.2f, 0.006f, 3.6f), M["tactile"], 0.6f, false);
        // 맨홀 둘 (차로 안쪽)
        var disc = KDisc("manhole", 0.33f, 24, 0.008f);
        foreach (var (mx, mz) in new[] { (2.2f, 28f), (-1.6f, 52f) })
        {
            var go = new GameObject("Manhole");
            go.transform.SetParent(root, false);
            go.transform.localPosition = new Vector3(mx, 0.003f, mz);
            go.transform.localRotation = Quaternion.Euler(0, mz * 7f, 0);
            go.AddComponent<MeshFilter>().sharedMesh = disc;
            go.AddComponent<MeshRenderer>().sharedMaterial = M["manhole"];
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        }
    }

    // 길가 설비: 전봇대와 전선 (서쪽 인도에 24m마다, 동쪽은 아파트 너머 둘, 길 건너 전선), 버스 정류장, 소화전, 가로수, 방공호 앞 '민방위 대피소' 현수막
    static void CsKoreaStreetKit(Transform root)
    {
        var r = KRandom(new Vector3(3f, 0, 1f), 61);
        var poleMesh = KLathe("utility_pole", new[] { new Vector2(0.19f, 0f), new Vector2(0.17f, 2f), new Vector2(0.14f, 6f), new Vector2(0.11f, 10f), new Vector2(0.1f, 10.3f) }, 14, 2f);
        var wires = new List<CombineInstance>();
        var west = new List<Vector3[]>();
        foreach (float z in new[] { 8f, 32f, 56f, 80f, 104f })
            west.Add(BuildUtilityPole(root, new Vector3(-7.9f, KSideY, z), 90f, poleMesh, r));
        for (int i = 0; i + 1 < west.Count; i++)
        {
            for (int k = 0; k < 6; k++) KWire(wires, west[i][k], west[i + 1][k], KF(r, 0.7f, 1.0f), 0.012f, 10);
            KWire(wires, west[i][6], west[i + 1][6], KF(r, 1.2f, 1.6f), 0.035f, 10);
        }
        var east1 = BuildUtilityPole(root, new Vector3(7.9f, KSideY, 42f), 90f, poleMesh, r);
        var east2 = BuildUtilityPole(root, new Vector3(7.9f, KSideY, 90f), 90f, poleMesh, r);
        for (int k = 0; k < 3; k++)
        {
            KWire(wires, west[1][k], east1[k], KF(r, 0.8f, 1.1f), 0.012f, 10);
            KWire(wires, west[3][k], east2[k], KF(r, 0.8f, 1.1f), 0.012f, 10);
        }
        KWire(wires, east1[6], east2[6], KF(r, 1.2f, 1.6f), 0.035f, 10);
        // 끊어져 늘어진 선 하나 (서쪽 세 번째 전봇대에서 길바닥으로)
        var fall = west[2][4] + new Vector3(2.5f, -west[2][4].y + 0.05f, 1.5f);
        KWire(wires, west[2][4], fall, 0.3f, 0.012f, 8);
        CombineInto(root, "PowerLines", wires, M["cable"]);
        // 버스 정류장 (서쪽 인도 z 40, 길을 본다). 아이템 자리는 컷씬에 없으니 지운다
        var points = Group("KitPoints", root, Vector3.zero);
        BuildBusStop(root, new Vector3(-10.3f, 0, 40f), 90f, 3, r, points);
        Object.DestroyImmediate(points.gameObject);
        BuildHydrant(root, new Vector3(-8.0f, 0, 20f), KF(r, 0, 360));
        foreach (float z in new[] { 16f, 60f, 84f }) StreetTree(root, new Vector3(-8.4f, KSideY, z));
        foreach (float z in new[] { 48f, 70f, 98f }) StreetTree(root, new Vector3(8.4f, KSideY, z));
        // 방공호 벙커 앞면(서쪽 덩어리)의 '민방위 대피소 → 300m' 현수막 (길 쪽 +z를 본다)
        KBanner(root, new Vector3(-7.2f, 3.65f, 0.4f), new Vector3(-3.0f, 3.58f, 0.4f), Vector3.forward, 0, r);
    }

    // 거실 창밖 먼 줄(z 190·250)의 삼일아파트 둘: 한국 도시의 스카이라인 (들어갈 수 없는 겉모습, 그림자 없음)
    static void CsOutsideApartments(Transform outside)
    {
        var r = KRandom(new Vector3(1f, 0, 2f), 71);
        BuildApartment(outside, new Vector3(28f, CsCityGround, 198f), 12f, 26f, 11f, 11, 1, r);
        BuildApartment(outside, new Vector3(96f, CsCityGround, 252f), -20f, 26f, 11f, 12, 2, r);
        foreach (var name in new[] { "Apartment_102", "Apartment_103" })
        {
            var apt = outside.Find(name);
            if (apt == null) continue;
            foreach (var mr in apt.GetComponentsInChildren<MeshRenderer>()) mr.shadowCastingMode = ShadowCastingMode.Off;
            foreach (var l in apt.GetComponentsInChildren<Light>()) Object.DestroyImmediate(l.gameObject);
        }
    }
}
