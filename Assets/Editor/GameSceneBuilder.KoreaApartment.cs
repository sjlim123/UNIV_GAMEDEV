using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// GameSceneBuilder의 한국 아파트 (맵 개편 세션): 판상형 '삼일아파트' 동(발코니 창 줄, 옆면 동 번호 띠와 로고, 옥탑·물탱크·안테나, 현관 캐노피),
// 단지 마당(주차선·버려진 차·과속방지턱·놀이터·경비실·정문 표지·울타리). 들어갈 수 없는 겉모습(탑 자리 또는 빈터 자리에 짓는다)
public static partial class GameSceneBuilder
{
    const float AptFloorH = 2.8f;

    // 동쪽 확장 구역: x=92 블록 세 개(bz -46·0·46)에 101·102·103동과 단지 마당. 도시 난수를 쓰는 가로등은 모든 난수 사용자 뒤(BuildCityScene 끝)에 짓는다
    const float KEastBlockX = 92f, KCityEastEdge = 126f;

    static void BuildAptZone(Transform geo, Transform points)
    {
        var zone = Group("AptZone", geo, Vector3.zero);
        int num = 0;
        foreach (var bz in new[] { -46f, 0f, 46f })
        {
            var c = new Vector3(KEastBlockX, 0, bz);
            var b = Group("AptBlock_" + KEastBlockX + "_" + bz, zone, c);
            var r = KRandom(c, 44);
            BuildSidewalkRing(b);
            // 마당 = 서쪽 절반 (정문은 x=69 길 쪽), 동 = 동쪽 절반 (발코니가 서쪽 마당을 본다, 동 번호 띠는 남북 끝면)
            var yard = Group("AptYard", b, new Vector3(-6.75f, KSideY, 0));
            Box("YardAsphalt", yard, new Vector3(0, 0.004f, 0), new Vector3(13.4f, 0.008f, 26.9f), M["asphalt"], 5f, false);
            BuildAptYard(yard, 13.4f, 26.9f, Vector3.left, r, points);
            Spawn<ZombieSpawnPoint>(points, yard.TransformPoint(new Vector3(2f, 0, KF(r, -8f, 8f))));
            BuildApartment(b, new Vector3(7.0f, KSideY, 0), -90f, 26f, 11f, 14, num, r, num == 1); // 102동은 폭격 파손 동
            num++;
            foreach (var x in new[] { -16.3f, 16.3f })
                foreach (var z in new[] { -16.3f, 16.3f })
                    if (Chance(0.8)) LampPost(b, new Vector3(x, KSideY, z));
        }
        // 확장 구간 북·남 둘레 벽 앞 잔해 (RubblePile은 도시 난수를 쓰므로 여기서)
        for (int i = 0; i < 6; i++) RubblePile(zone, new Vector3(R(80f, KCityEastEdge - 4f), 0, i % 2 == 0 ? 77.5f : -77.5f), 5, 1.8f);
    }

    static void KoreaApartmentMaterials()
    {
        var aptNum = new List<Color32[]>();
        var aptSizes = new[] { 36, 48, 56, 96, 128 }; // 정문 영문 36, 표지 48, 띠 56·96, 정문 128
        var dohyeon = KFontFor("Fonts/DoHyeon-Regular", "삼일아파트동SAMILPT", aptSizes);
        var pretendard = KFontFor("Fonts/Pretendard-Bold", "삼일아파트동SAMILPT", aptSizes);
        var aptPlate = new List<Color32[]>();
        foreach (int n in new[] { 101, 102, 103 }) { aptNum.Add(AptStripePixels(n, dohyeon)); aptPlate.Add(AptPlatePixels(n, dohyeon)); }
        var gate = AptGatePixels(dohyeon, pretendard);
        KReleaseFonts();
        for (int i = 0; i < 3; i++)
        {
            M["aptNum" + i] = KLitMat("M_AptStripe_" + (101 + i), KSaveTex("apt_stripe_" + (101 + i), aptNum[i], 192, 2048, KTexKind.Color, false), null, Color.white, 0.2f, 0f);
            M["aptPlate" + i] = KLitMat("M_AptPlate_" + (101 + i), KSaveTex("apt_plate_" + (101 + i), aptPlate[i], 256, 64, KTexKind.Color, false), null, Color.white, 0.35f, 0f);
        }
        M["aptGate"] = KLitMat("M_AptGate", KSaveTex("apt_gate", gate, 1024, 256, KTexKind.Color, false), null, Color.white, 0.3f, 0f);
        M["aptWall"] = TexMat("M_AptWall", "concrete_wall_008", new Color(0.8f, 0.76f, 0.7f), 0.1f);
        M["aptBand"] = TexMat("M_AptBand", "concrete_wall_008", new Color(0.55f, 0.5f, 0.46f), 0.1f);
        M["aptRail"] = TexMat("M_AptRail", "rusty_metal_02", new Color(0.35f, 0.36f, 0.38f), 0.4f, 0.5f);
        M["parkPaint"] = M.ContainsKey("paintW") ? M["paintW"] : M["crossPaint"];
    }

    // 동 번호 띠 192×2048 (세로, 1m≈53px → 3.6m×38.6m 한 장): 어두운 살구색 띠, 꼭대기에 로고(원)·'삼일'·큰 번호, 7m 높이에 번호 한 번 더 (밤에 길에서 읽히도록),
    // 층 이음선과 위에서 흘러내린 비 얼룩 (참고: 옆면 번호 띠 느낌만, 상호는 삼일)
    static Color32[] AptStripePixels(int number, KFont big)
    {
        const int w = 192, h = 2048;
        var px = new Color[w * h];
        var band = new Color(0.62f, 0.36f, 0.28f);
        for (int j = 0; j < h; j++)
            for (int i = 0; i < w; i++)
            {
                var c = band * (0.88f + 0.24f * GrimeFbm(i, j, 2048, 8, 3, 4100 + number));
                float soot = Smooth01(0.6f, 0.9f, GrimeFbm(i, j, 2048, 3, 3, 4200 + number));
                c = Color.Lerp(c, new Color(0.12f, 0.1f, 0.09f), soot * 0.5f);
                float streak = Smooth01(0.62f, 0.9f, GrimeFbm(i, 5, 2048, 24, 2, 4150 + number)) * Smooth01(0.25f, 0.95f, j / (float)h);
                c = Color.Lerp(c, c * 0.62f, streak);
                if ((j + 16) % 149 < 2) c *= 0.82f; // 층마다(2.8m) 옅은 이음선
                px[j * w + i] = c;
            }
        // 로고: 흰 원 안 작은 원
        for (int j = 0; j < h; j++)
            for (int i = 0; i < w; i++)
            {
                float dx = (i - w * 0.5f) / 40f, dy = (j - h * 0.945f) / 40f;
                float d = dx * dx + dy * dy;
                if (d < 1f) px[j * w + i] = Color.Lerp(new Color(0.96f, 0.92f, 0.84f), band, Smooth01(0.3f, 0.6f, d) * 0.9f);
            }
        var ink = new Color(0.1f, 0.08f, 0.07f);
        KText(px, w, h, big, "삼일", 56, w * 0.5f, h * 0.905f, new Color(0.96f, 0.92f, 0.84f), 0.5f);
        KText(px, w, h, big, number.ToString(), 96, w * 0.5f, h * 0.855f, ink, 0.5f);
        KText(px, w, h, big, number.ToString(), 96, w * 0.5f, h * 0.175f, ink, 0.5f);
        var res = new Color32[w * h];
        for (int k = 0; k < res.Length; k++) { px[k].a = 1f; res[k] = KC(px[k]); }
        return res;
    }

    // 현관 위 동 표지 256×64 (1.4m×0.35m): 남색 판에 테두리와 '101동'
    static Color32[] AptPlatePixels(int number, KFont font)
    {
        const int w = 256, h = 64;
        var px = new Color[w * h];
        for (int j = 0; j < h; j++)
            for (int i = 0; i < w; i++)
            {
                var c = new Color(0.1f, 0.14f, 0.26f) * (0.85f + 0.3f * GrimeFbm(i, j, 256, 6, 3, 4600 + number));
                if (i < 3 || i >= w - 3 || j < 3 || j >= h - 3) c = new Color(0.7f, 0.68f, 0.6f);
                px[j * w + i] = c;
            }
        KText(px, w, h, font, number + "동", 48, w * 0.5f, h * 0.5f, new Color(0.95f, 0.93f, 0.88f), 0.5f);
        var res = new Color32[w * h];
        for (int k = 0; k < res.Length; k++) { px[k].a = 1f; res[k] = KC(px[k]); }
        return res;
    }

    // 정문 표지 1024×256: 돌판에 '삼일아파트'
    static Color32[] AptGatePixels(KFont big, KFont small)
    {
        const int w = 1024, h = 256;
        var px = new Color[w * h];
        for (int j = 0; j < h; j++)
            for (int i = 0; i < w; i++)
            {
                float n = GrimeFbm(i, j, 1024, 20, 4, 4300);
                var c = new Color(0.3f, 0.3f, 0.32f) * (0.8f + 0.4f * n);
                if (GrimeNoise(i, j, 1024, 256, 4400) > 0.9f) c *= 1.4f;
                px[j * w + i] = c;
            }
        KText(px, w, h, big, "삼일아파트", 128, w * 0.5f, h * 0.58f, new Color(0.9f, 0.86f, 0.78f), 0.5f);
        KText(px, w, h, small, "SAMIL APT · 101 - 103", 36, w * 0.5f, h * 0.2f, new Color(0.75f, 0.72f, 0.66f), 0.5f);
        var res = new Color32[w * h];
        for (int k = 0; k < res.Length; k++) { px[k].a = 1f; res[k] = KC(px[k]); }
        return res;
    }

    // 아파트 한 동: 원점 = 바닥 가운데, 로컬 +z = 앞(발코니 창 면, 길 쪽), 가로 width(x)·깊이 depth(z)·floors 층. numIndex 0~2 → 101~103동
    static void BuildApartment(Transform parent, Vector3 pos, float yaw, float width, float depth, int floors, int numIndex, System.Random r, bool damaged = false)
    {
        var g = Group("Apartment_" + (101 + numIndex), parent, pos, yaw);
        float h = floors * AptFloorH;
        // 파손 동: 로컬 +x 끝 7m 구간의 cutFloor층 이상이 날아갔다 (그 구간은 cutFloor층까지만 남는 토막)
        float cutX = damaged ? width / 2f - 7f : float.MaxValue;
        int cutFloor = damaged ? 8 : floors;
        float endH = cutFloor * AptFloorH;
        if (!damaged) Box("Mass", g, new Vector3(0, h / 2f, 0), new Vector3(width, h, depth), M["aptWall"], 4f);
        else
        {
            float mainW = cutX + width / 2f, endW = width / 2f - cutX;
            Box("Mass", g, new Vector3(-width / 2f + mainW / 2f, h / 2f, 0), new Vector3(mainW, h, depth), M["aptWall"], 4f);
            Box("MassEnd", g, new Vector3(cutX + endW / 2f, endH / 2f, 0), new Vector3(endW, endH, depth), M["aptWall"], 4f);
        }
        // 앞면 칸 나누기: 계단실마다 [창 창 계단 창 창] — 계단실 칸(2m)은 앞으로 0.2m 튀어나오고 반 층 높이의 층계참 창, 1층엔 현관
        int stairs = Mathf.Max(1, Mathf.RoundToInt(width / 13f));
        const float stairW = 2.0f, bayOut = 0.2f;
        float bayW = (width - stairs * stairW) / (stairs * 4);
        var colX = new List<float>();
        var colStair = new List<bool>();
        float cx = -width / 2f;
        for (int k = 0; k < stairs; k++)
        {
            for (int u = 0; u < 2; u++) { colX.Add(cx + bayW / 2f); colStair.Add(false); cx += bayW; }
            colX.Add(cx + stairW / 2f); colStair.Add(true); cx += stairW;
            for (int u = 0; u < 2; u++) { colX.Add(cx + bayW / 2f); colStair.Add(false); cx += bayW; }
        }
        // 층 띠 (앞·뒤): 발코니 아래 어두운 띠
        var bands = new List<CombineInstance>();
        var glass = new List<CombineInstance>();
        var frames = new List<CombineInstance>();
        var rails = new List<CombineInstance>();
        var shards = new List<CombineInstance>();
        var holes = new List<CombineInstance>();
        foreach (float s in new[] { 1f, -1f })
            for (int f = 0; f < floors; f++)
            {
                float bw = f >= cutFloor ? cutX + width / 2f : width; // 파손 동 위층 띠는 남은 몸체만
                KPart(bands, new Vector3(bw + 0.04f, 0.35f, 0.06f), 2f, new Vector3(-width / 2f + bw / 2f, f * AptFloorH + 0.17f, s * (depth / 2f + 0.03f)), Quaternion.identity);
            }
        for (int c = 0; c < colX.Count; c++)
        {
            float x = colX[c];
            if (colStair[c])
            {
                float bayH = x > cutX ? endH : h;
                Box("StairBay", g, new Vector3(x, bayH / 2f, depth / 2f + bayOut / 2f), new Vector3(stairW + 0.2f, bayH, bayOut), M["aptWall"], 4f);
                for (int f = 0; f < floors; f++)
                    foreach (float s in new[] { 1f, -1f })
                    {
                        if ((s > 0 && f == 0) || (x > cutX && f >= cutFloor)) continue; // 앞면 1층은 현관 자리, 파손 구간 위층은 없다
                        float z = s > 0 ? depth / 2f + bayOut + 0.03f : -(depth / 2f + 0.03f);
                        KAptWindow(glass, frames, shards, holes, new Vector3(x, f * AptFloorH + 2.1f, z), 0.9f, 1.0f, r);
                    }
                continue;
            }
            for (int f = 0; f < floors; f++)
            {
                if (x > cutX && f >= cutFloor) continue; // 파손 구간 위층은 없다
                float y0 = f * AptFloorH;
                foreach (float s in new[] { 1f, -1f })
                {
                    float z = s * (depth / 2f + 0.03f);
                    bool front = s > 0;
                    float ww = front ? bayW - 0.5f : 1.1f, wh = front ? 1.6f : 1.2f, wy = y0 + (front ? 1.35f : 1.5f);
                    KAptWindow(glass, frames, shards, holes, new Vector3(x, wy, z), ww, wh, r);
                    if (!front) continue;
                    KBar(frames, new Vector3(0.05f, wh, 0.08f), new Vector3(x, wy, z), Quaternion.identity); // 가운데 새시 기둥
                    // 앞면 발코니 난간 (창 아래)
                    KBar(rails, new Vector3(ww, 0.04f, 0.04f), new Vector3(x, y0 + 0.5f, z + 0.1f), Quaternion.identity);
                    for (int q = 0; q < 4; q++) KBar(rails, new Vector3(0.02f, 0.3f, 0.02f), new Vector3(x - ww / 2f + (q + 0.5f) * ww / 4f, y0 + 0.35f, z + 0.1f), Quaternion.identity); // 난간 살 6 → 4, 뚜껑 없는 막대 (메시 용량)
                    if (r.NextDouble() < 0.3) KAcUnit(g, new Vector3(x + KF(r, -0.5f, 0.5f), y0 + 0.62f, z + 0.25f), Quaternion.identity, r);
                }
            }
        }
        CombineInto(g, "Bands", bands, M["aptBand"]);
        CombineInto(g, "Glass", glass, M["glass"]);
        CombineInto(g, "Frames", frames, M["whitePaint"]);
        CombineInto(g, "Rails", rails, M["aptRail"]);
        CombineInto(g, "Shards", shards, M["glass"]);
        CombineInto(g, "Holes", holes, M["soot"]);
        // 옆면 동 번호 띠 (양 옆): 폭 3.6m, 바닥 0.3m부터 옥상 아래까지 한 장 (192×2048). 번호는 꼭대기와 7m 높이 두 곳
        foreach (float s in new[] { -1f, 1f })
        {
            float sy0 = 0.3f, sy1 = (damaged && s > 0 ? endH : h) - 0.2f; // 파손 동의 날아간 끝은 토막 높이까지만
            var face = Prim(PrimitiveType.Quad, "NumberStripe", g, new Vector3(s * (width / 2f + 0.012f), (sy0 + sy1) / 2f, 0), new Vector3(3.6f, sy1 - sy0, 1f), M["aptNum" + numIndex], new Vector3(0, s > 0 ? -90f : 90f, 0));
            face.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }
        // 옥상: 난간, 옥탑(계단실·승강기실), 물탱크 둘, 안테나
        var top = Group("Roof", g, new Vector3(0, h, 0));
        var par = new List<CombineInstance>();
        float rw = damaged ? cutX + width / 2f : width, rx = -width / 2f + rw / 2f; // 파손 동은 남은 몸체 위만
        KPart(par, new Vector3(rw, 1.0f, 0.2f), 2f, new Vector3(rx, 0.5f, depth / 2f - 0.1f), Quaternion.identity);
        KPart(par, new Vector3(rw, 1.0f, 0.2f), 2f, new Vector3(rx, 0.5f, -depth / 2f + 0.1f), Quaternion.identity);
        if (!damaged) KPart(par, new Vector3(0.2f, 1.0f, depth - 0.4f), 2f, new Vector3(width / 2f - 0.1f, 0.5f, 0), Quaternion.identity);
        KPart(par, new Vector3(0.2f, 1.0f, depth - 0.4f), 2f, new Vector3(-width / 2f + 0.1f, 0.5f, 0), Quaternion.identity);
        CombineInto(top, "Parapet", par, M["aptWall"]);
        for (int k = 0; k < stairs; k++)
        {
            float x = -width / 2f + (k + 0.5f) * width / stairs;
            if (x > cutX) continue; // 파손 구간 위 옥탑은 날아갔다
            Box("Penthouse", top, new Vector3(x, 1.5f, -depth * 0.15f), new Vector3(3.4f, 3.0f, 3.2f), M["aptWall"], 2f);
            Box("PenthouseDoor", top, new Vector3(x, 1.0f, -depth * 0.15f + 1.61f), new Vector3(0.9f, 2.0f, 0.04f), M["metal"], 1f, false);
            var tk = new List<CombineInstance>();
            KCylAt(tk, new Vector3(x + 2.6f, 1.0f, depth * 0.2f), 0.8f, 1.8f, Quaternion.identity);
            KCylAt(tk, new Vector3(x + 2.6f, 1.95f, depth * 0.2f), 0.55f, 0.1f, Quaternion.identity);
            foreach (float lx in new[] { -0.55f, 0.55f }) foreach (float lz in new[] { -0.55f, 0.55f }) KPart(tk, new Vector3(0.08f, 0.2f, 0.08f), 1f, new Vector3(x + 2.6f + lx, 0.1f, depth * 0.2f + lz), Quaternion.identity);
            CombineInto(top, "Tank", tk, k % 2 == 0 ? M["blastStripe"] : M["paintBlue"]);
        }
        var ant = new List<CombineInstance>();
        float antX = damaged ? -(width / 2f - 1.5f) : width / 2f - 1.5f;
        KRod(ant, new Vector3(antX, 0, -depth / 2f + 1.2f), new Vector3(antX, 4.5f, -depth / 2f + 1.2f), 0.03f);
        foreach (float ay in new[] { 3.2f, 3.8f, 4.3f }) KPart(ant, new Vector3(1.6f - (ay - 3.2f), 0.02f, 0.02f), 1f, new Vector3(antX, ay, -depth / 2f + 1.2f), Quaternion.identity);
        CombineInto(top, "Antenna", ant, M["steelGrey"]);
        // 현관: 계단실 칸 1층 — 캐노피·유리문·계단·'101동' 표지
        for (int k = 0; k < stairs; k++)
        {
            float x = -width / 2f + (k + 0.5f) * width / stairs; // = 계단실 칸 가운데
            var ent = Group("Entrance", g, new Vector3(x, 0, depth / 2f + bayOut));
            Box("Canopy", ent, new Vector3(0, 2.75f, 0.85f), new Vector3(3.2f, 0.12f, 1.8f), M["dirty"], 2f);
            foreach (float px in new[] { -1.4f, 1.4f }) Box("CanopyPost", ent, new Vector3(px, 1.35f, 1.6f), new Vector3(0.1f, 2.7f, 0.1f), M["steelGrey"], 1f);
            Box("DoorDark", ent, new Vector3(0, 1.15f, 0.015f), new Vector3(1.6f, 2.26f, 0.012f), M["soot"], 1f, false); // 유리 뒤 어두운 현관 안
            foreach (float fx in new[] { -0.81f, 0.81f }) Box("DoorFrame", ent, new Vector3(fx, 1.15f, 0.05f), new Vector3(0.08f, 2.3f, 0.08f), M["frameDark"], 1f, false);
            Box("DoorFrame", ent, new Vector3(0, 2.26f, 0.05f), new Vector3(1.7f, 0.08f, 0.08f), M["frameDark"], 1f, false);
            Box("DoorRail", ent, new Vector3(0, 1.0f, 0.05f), new Vector3(1.54f, 0.07f, 0.07f), M["frameDark"], 1f, false);
            Box("DoorHandle", ent, new Vector3(0.12f, 1.05f, 0.1f), new Vector3(0.04f, 0.3f, 0.03f), M["steelGrey"], 1f, false);
            var doorGlass = Box("DoorGlass", ent, new Vector3(0, 1.15f, 0.05f), new Vector3(1.54f, 2.2f, 0.012f), M["shopGlass"], 1f, true);
            doorGlass.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            doorGlass.AddComponent<LockedDoor>(); // 잠긴 현관문: 윤곽선·안내는 기존 문처럼, 열리지 않고 집에 못 돌아간다는 글
            var dc = doorGlass.GetComponent<BoxCollider>();
            if (dc != null) dc.size = new Vector3(1.54f, 2.2f, 0.25f); // 유리보다 두꺼운 판정 (조준이 쉽게)
            Box("Steps", ent, new Vector3(0, 0.08f, 0.55f), new Vector3(2.6f, 0.16f, 1.1f), M["floor"], 1.5f);
            var plate = Prim(PrimitiveType.Quad, "DongPlate", ent, new Vector3(0, 2.5f, 0.03f), new Vector3(1.4f, 0.35f, 1f), M["aptPlate" + numIndex], new Vector3(0, 180f, 0));
            plate.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }
        // 그을음: 앞면 몇 층에 불길 자국
        for (int i = 0, n = 1 + r.Next(3); i < n; i++)
        {
            float x = KF(r, -width / 2f + 2f, Mathf.Min(width / 2f - 2f, cutX - 1f)), y = KF(r, 2f, h - 4f);
            Stain("Scorch", g, new Vector3(x, y, depth / 2f + 0.04f), KF(r, 1.5f, 3f), KF(r, 2f, 4f), M["soot"], 4500 + i * 7 + numIndex, new Vector3(-90f, 0, 0));
        }
        KDownpipeLocal(g, new Vector3(-width / 2f + 0.3f, 0, depth / 2f + 0.08f), h);
        KDownpipeLocal(g, new Vector3(width / 2f - 0.3f, 0, -depth / 2f - 0.08f), damaged ? endH : h);
        if (damaged) KAptDamage(g, width, depth, cutX, endH, h, floors, cutFloor, r);
    }

    // 아파트 창 한 짝 (원점 가운데, 앞을 본다): 새시 틀 네 변 + 유리. 18%는 깨져 조각 둘만 남고 뒤에 검은 구멍
    static void KAptWindow(List<CombineInstance> glass, List<CombineInstance> frames, List<CombineInstance> shards, List<CombineInstance> holes, Vector3 at, float ww, float wh, System.Random r)
    {
        if (r.NextDouble() < 0.18)
        {
            holes.Add(new CombineInstance { mesh = BoxMesh(new Vector3(ww, wh, 0.01f), 1f), transform = Matrix4x4.TRS(at, Quaternion.identity, Vector3.one) });
            for (int k = 0; k < 2; k++)
                shards.Add(new CombineInstance { mesh = BoxMesh(new Vector3(KF(r, 0.15f, 0.4f), KF(r, 0.15f, 0.4f), 0.03f), 1f), transform = Matrix4x4.TRS(at + new Vector3(KF(r, -ww * 0.4f, ww * 0.4f), k == 0 ? wh * 0.42f : -wh * 0.42f, 0), Quaternion.Euler(0, 0, KF(r, -30f, 30f)), Vector3.one) });
        }
        else glass.Add(new CombineInstance { mesh = BoxMesh(new Vector3(ww, wh, 0.03f), 1f), transform = Matrix4x4.TRS(at, Quaternion.identity, Vector3.one) });
        KBar(frames, new Vector3(ww + 0.08f, 0.05f, 0.08f), at + Vector3.up * (wh / 2f), Quaternion.identity); // 틀은 뚜껑 없는 막대 (메시 용량)
        KBar(frames, new Vector3(ww + 0.08f, 0.05f, 0.08f), at - Vector3.up * (wh / 2f), Quaternion.identity);
        KBar(frames, new Vector3(0.05f, wh, 0.08f), at - Vector3.right * (ww / 2f), Quaternion.identity);
        KBar(frames, new Vector3(0.05f, wh, 0.08f), at + Vector3.right * (ww / 2f), Quaternion.identity);
    }


    // 파손 동 꾸미기: 날아간 구간(x > cutX, 층 cutFloor 이상)의 가장자리 — 부서진 바닥판 토막·휜 철근·매달린 벽 조각, 남은 토막 옥상의 잔해, 바닥의 콘크리트 덩어리와 더미, 그을음, 큰 연기
    static void KAptDamage(Transform g, float width, float depth, float cutX, float endH, float h, int floors, int cutFloor, System.Random r)
    {
        var dg = Group("Damage", g, Vector3.zero);
        // 부서진 바닥판 토막: 남은 몸체 끝(x = cutX)에서 바깥으로 삐져나온 판 (층마다)
        for (int f = cutFloor; f <= floors; f++)
        {
            float y = f * AptFloorH + 0.12f;
            int pieces = 1 + r.Next(3);
            for (int k = 0; k < pieces; k++)
            {
                float len = KF(r, 0.5f, 2.4f), dz = KF(r, 1.2f, depth * 0.5f), zc = KF(r, -depth / 2f + dz / 2f, depth / 2f - dz / 2f);
                Box("BrokenSlab", dg, new Vector3(cutX + len / 2f, y, zc), new Vector3(len, 0.24f, dz), M["dirty"], 2f, true, new Vector3(0, 0, KF(r, -9f, 3f)));
                for (int q = 0; q < 2; q++)
                    Rebar(dg, new Vector3(cutX + len, y + KF(r, -0.08f, 0.08f), zc + KF(r, -dz * 0.4f, dz * 0.4f)), new Vector3(1f, KF(r, -0.6f, 0.3f), KF(r, -0.4f, 0.4f)), KF(r, 0.4f, 1.1f), r);
            }
            // 매달린 벽 조각 (앞·뒤 면)
            if (r.NextDouble() < 0.6)
            {
                float s = r.NextDouble() < 0.5 ? 1f : -1f;
                Box("HangingWall", dg, new Vector3(cutX + KF(r, 0.3f, 1.1f), y + KF(r, 0.6f, 1.6f), s * (depth / 2f - 0.15f)), new Vector3(KF(r, 0.6f, 1.6f), KF(r, 0.8f, 1.8f), 0.28f), M["aptWall"], 2f, true, new Vector3(KF(r, -8f, 8f), 0, KF(r, -20f, 20f)));
            }
        }
        // 남은 몸체의 드러난 끝면: 그을음
        Stain("Scorch", dg, new Vector3(cutX + 0.03f, endH + KF(r, 3f, 8f), KF(r, -3f, 3f)), KF(r, 3f, 6f), KF(r, 2.5f, 4f), M["soot"], 4700, new Vector3(0, 0, -90f));
        Stain("Scorch", dg, new Vector3(cutX + 0.03f, h - KF(r, 2f, 5f), KF(r, -3f, 3f)), KF(r, 2.5f, 5f), KF(r, 2f, 3.5f), M["soot"], 4701, new Vector3(0, 0, -90f));
        // 토막 옥상: 무너진 잔해 덩어리·철근·그을음·재
        float ex0 = cutX + 0.3f, ex1 = width / 2f - 0.3f;
        for (int i = 0, n = 7 + r.Next(5); i < n; i++)
        {
            var p = new Vector3(KF(r, ex0, ex1), endH + KF(r, 0.15f, 0.5f), KF(r, -depth / 2f + 0.6f, depth / 2f - 0.6f));
            Box("Rubble", dg, p, new Vector3(KF(r, 0.5f, 1.8f), KF(r, 0.3f, 0.9f), KF(r, 0.5f, 1.5f)), r.NextDouble() < 0.5 ? M["dirty"] : M["aptWall"], 1.5f, true, new Vector3(KF(r, -25f, 25f), KF(r, 0, 360), KF(r, -25f, 25f)));
        }
        for (int i = 0; i < 6; i++)
            Rebar(dg, new Vector3(KF(r, ex0, ex1), endH + 0.1f, KF(r, -depth / 2f + 0.5f, depth / 2f - 0.5f)), new Vector3(KF(r, -0.6f, 0.6f), 1f, KF(r, -0.6f, 0.6f)), KF(r, 0.6f, 1.6f), r);
        Stain("Scorch", dg, new Vector3((ex0 + ex1) / 2f, endH + 0.012f, 0), (ex1 - ex0) * 0.5f, depth * 0.45f, M["scorch"], 4702);
        Stain("Ash", dg, new Vector3((ex0 + ex1) / 2f + KF(r, -1f, 1f), endH + 0.02f, KF(r, -1f, 1f)), 1.8f, 1.5f, M["ashPile"], 4703);
        // 바닥: 떨어진 콘크리트 덩어리 (철근 삐져나옴)와 잔해 더미, 그을음
        for (int i = 0, n = 4 + r.Next(3); i < n; i++)
        {
            var p = new Vector3(width / 2f + KF(r, 0.5f, 5f), 0, KF(r, -depth / 2f - 2f, depth / 2f + 2f));
            var size = new Vector3(KF(r, 0.8f, 2.4f), KF(r, 0.4f, 1.2f), KF(r, 0.7f, 1.8f));
            Box("Chunk", dg, p + Vector3.up * (size.y / 2f - 0.05f), size, r.NextDouble() < 0.5 ? M["dirty"] : M["aptWall"], 1.5f, true, new Vector3(KF(r, -12f, 12f), KF(r, 0, 360), KF(r, -12f, 12f)));
            Rebar(dg, p + new Vector3(size.x * 0.4f, size.y - 0.1f, 0), new Vector3(KF(r, -0.5f, 0.5f), 1f, KF(r, -0.5f, 0.5f)), KF(r, 0.5f, 1.2f), r);
        }
        RubblePile(dg, new Vector3(width / 2f + 2.2f, 0, KF(r, -3f, 3f)), 9, 2.6f);
        RubblePile(dg, new Vector3(cutX + 1.5f, 0, depth / 2f + 2.5f), 6, 2f);
        Stain("Scorch", dg, new Vector3(width / 2f + 2f, 0.012f, 0), 5f, 4f, M["scorch"], 4704);
        // 큰 연기 둘 (토막 옥상 위)
        BigSmoke(dg, new Vector3((ex0 + ex1) / 2f, endH + 0.3f, KF(r, -1.5f, 1.5f)), 1.2f);
        BigSmoke(dg, new Vector3(cutX + 0.6f, h - 2f, KF(r, -2f, 2f)), 0.7f);
    }

    static void KDownpipeLocal(Transform g, Vector3 foot, float h)
    {
        var parts = new List<CombineInstance>();
        KRod(parts, foot + Vector3.up * 0.3f, foot + Vector3.up * h, 0.05f);
        KRod(parts, foot + Vector3.up * 0.3f, foot + Vector3.up * 0.05f + (foot.z > 0 ? Vector3.forward : Vector3.back) * 0.35f, 0.05f);
        CombineInto(g, "Downpipe", parts, M["steelGrey"]);
    }

    // 단지 마당: 주차선과 버려진 차, 과속방지턱, 놀이터(그네·미끄럼틀·모래), 경비실, 정문 표지, 둘레 울타리. area = 로컬 사각형(원점 가운데)
    static void BuildAptYard(Transform g, float w, float d, Vector3 gateSide, System.Random r, Transform points)
    {
        // 주차선 (흰 페인트, 바닥 위)
        var lines = new List<CombineInstance>();
        int slots = Mathf.FloorToInt((w - 2f) / 2.5f);
        for (int i = 0; i <= slots; i++)
            lines.Add(new CombineInstance { mesh = BoxMesh(new Vector3(0.12f, 0.006f, 5f), 1.5f), transform = Matrix4x4.TRS(new Vector3(-w / 2f + 1f + i * 2.5f, 0.011f, d / 2f - 3f), Quaternion.identity, Vector3.one) }); // 마당 아스팔트(0.008) 위
        CombineInto(g, "ParkingLines", lines, M["parkPaint"]);
        for (int i = 0, n = 2 + r.Next(3); i < n; i++)
        {
            int slot = r.Next(slots);
            WreckedCarV2(g, new Vector3(-w / 2f + 2.25f + slot * 2.5f, 0, d / 2f - 3f), KF(r, -6f, 6f), false, r.NextDouble() < 0.2, r);
        }
        // 과속방지턱 (노랑·검정)
        foreach (float x in new[] { -w * 0.25f, w * 0.25f })
        {
            Box("Bump", g, new Vector3(x, 0.04f, 0), new Vector3(0.6f, 0.08f, 3.2f), M["blastStripe"], 1f, true, new Vector3(0, 0, 0));
        }
        // 놀이터: 모래 바닥, 그네, 미끄럼틀
        var play = Group("Playground", g, new Vector3(-w / 2f + 5f, 0, -d / 2f + 4f));
        Box("Sand", play, new Vector3(0, 0.03f, 0), new Vector3(7f, 0.06f, 6f), M["ashPile"], 2f, false);
        var sw = new List<CombineInstance>();
        foreach (float sx in new[] { -1.5f, 1.5f })
        {
            KRod(sw, new Vector3(sx, 0, -1f), new Vector3(sx * 0.7f, 2.4f, -1f), 0.04f);
            KRod(sw, new Vector3(sx, 0, 1f), new Vector3(sx * 0.7f, 2.4f, 1f), 0.04f);
        }
        KRod(sw, new Vector3(-1.05f, 2.4f, -1f), new Vector3(1.05f, 2.4f, -1f), 0.04f);
        KRod(sw, new Vector3(-1.05f, 2.4f, 1f), new Vector3(1.05f, 2.4f, 1f), 0.04f);
        foreach (float sx in new[] { -0.5f, 0.5f })
        {
            KRod(sw, new Vector3(sx, 2.4f, -1f), new Vector3(sx, 0.6f, -1.3f), 0.01f);
            KRod(sw, new Vector3(sx, 2.4f, 1f), new Vector3(sx, 0.6f, 0.7f), 0.01f);
        }
        KPart(sw, new Vector3(0.45f, 0.04f, 0.2f), 1f, new Vector3(-0.5f, 0.6f, -1.3f), Quaternion.identity);
        KPart(sw, new Vector3(0.45f, 0.04f, 0.2f), 1f, new Vector3(0.5f, 0.6f, 0.7f), Quaternion.identity);
        CombineInto(play, "Swing", sw, M["aptRail"]);
        ColliderBox("SwingCollider", play, new Vector3(0, 1.2f, 0), new Vector3(3.2f, 2.4f, 2.4f), Vector3.zero);
        var slide = Group("Slide", play, new Vector3(2.5f, 0, 1.5f), KF(r, 0, 360));
        Box("Platform", slide, new Vector3(0, 1.4f, 0), new Vector3(1.0f, 0.08f, 1.0f), M["paintBlue"], 1f);
        foreach (float px in new[] { -0.45f, 0.45f }) foreach (float pz in new[] { -0.45f, 0.45f }) Box("Leg", slide, new Vector3(px, 0.7f, pz), new Vector3(0.06f, 1.4f, 0.06f), M["aptRail"], 1f, false);
        Box("Chute", slide, new Vector3(0, 0.8f, 1.5f), new Vector3(0.8f, 0.05f, 2.6f), M["paintRed"], 1f, true, new Vector3(-33f, 0, 0));
        Box("Ladder", slide, new Vector3(0, 0.75f, -0.7f), new Vector3(0.5f, 1.4f, 0.05f), M["aptRail"], 1f, true, new Vector3(20f, 0, 0));
        // 경비실: 정문 옆 작은 부스 (ext = 정문 방향의 반폭)
        float ext = Mathf.Abs(gateSide.x) > 0.5f ? w / 2f : d / 2f;
        var booth = Group("GuardBooth", g, gateSide * (ext - 3f) + Vector3.Cross(gateSide, Vector3.up) * 3.5f, Mathf.Atan2(gateSide.x, gateSide.z) * Mathf.Rad2Deg);
        Box("Body", booth, new Vector3(0, 1.3f, 0), new Vector3(2.4f, 2.6f, 2.0f), M["aptWall"], 2f);
        Box("Roof", booth, new Vector3(0, 2.68f, 0), new Vector3(2.8f, 0.16f, 2.4f), M["dirty"], 2f, false);
        Box("Window", booth, new Vector3(0, 1.5f, 1.01f), new Vector3(1.6f, 0.9f, 0.02f), M["glass"], 1f, false);
        Box("Window", booth, new Vector3(1.21f, 1.5f, 0), new Vector3(0.02f, 0.9f, 1.2f), M["glass"], 1f, false);
        Box("Door", booth, new Vector3(-1.21f, 1.0f, 0.2f), new Vector3(0.02f, 2.0f, 0.8f), M["metal"], 1f, false);
        // 정문 표지: 돌판 기둥 + 차단기
        var gate = Group("Gate", g, gateSide * (ext - 1f), Mathf.Atan2(gateSide.x, gateSide.z) * Mathf.Rad2Deg);
        Box("Pillar", gate, new Vector3(-4.0f, 1.3f, 0), new Vector3(0.8f, 2.6f, 0.8f), M["granite"], 1f);
        Box("Pillar", gate, new Vector3(4.0f, 1.3f, 0), new Vector3(0.8f, 2.6f, 0.8f), M["granite"], 1f);
        Box("Beam", gate, new Vector3(0, 2.9f, 0), new Vector3(8.8f, 0.6f, 0.5f), M["granite"], 1f, false);
        var face = Prim(PrimitiveType.Quad, "GateSign", gate, new Vector3(0, 2.9f, 0.26f), new Vector3(8.0f, 0.5f, 1f), M["aptGate"], new Vector3(0, 180f, 0));
        face.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        var bar = new List<CombineInstance>();
        KRod(bar, new Vector3(3.4f, 1.0f, 0.5f), new Vector3(-0.6f, 1.3f, 0.5f), 0.04f);
        KPart(bar, new Vector3(0.3f, 1.0f, 0.3f), 1f, new Vector3(3.4f, 0.5f, 0.5f), Quaternion.identity);
        CombineInto(gate, "Barrier", bar, M["blastStripe"]);
        // 둘레 울타리 (정문 쪽 가운데는 비운다)
        var fence = new List<CombineInstance>();
        var fenceRot = Quaternion.identity;
        for (int side = 0; side < 4; side++)
        {
            float len = side < 2 ? w : d;
            var outN = KSideOut(side);
            bool gateHere = Vector3.Dot(outN, gateSide) > 0.9f;
            if (Vector3.Dot(outN, -gateSide) > 0.9f) continue; // 동 앞(정문 반대쪽)은 울타리 없이 마당과 이어진다
            for (float a = -len / 2f + 1f; a < len / 2f - 1f; a += 2f)
            {
                if (gateHere && Mathf.Abs(a + 1f) < 5.5f) continue;
                var c = KSidePoint(side, a + 1f, (side < 2 ? d : w) / 2f - 0.2f);
                KFenceSegment(fence, c, Quaternion.Euler(0, KSideYaw(side), 0));
                ColliderBox("YardFence", g, c + Vector3.up * 0.5f, new Vector3(2.0f, 1.0f, 0.1f), new Vector3(0, KSideYaw(side), 0));
            }
        }
        CombineInto(g, "YardFence", fence, M["fenceGreen"]);
        // 마당 아이템 자리 (돌만)
        for (int i = 0; i < 4; i++) OutdoorItemPoint(points, g.TransformPoint(new Vector3(KF(r, -w / 2f + 2f, w / 2f - 2f), 0.1f, KF(r, -d / 2f + 2f, d / 2f - 2f))));
    }
}
