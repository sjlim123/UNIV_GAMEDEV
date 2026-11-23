using System.Collections.Generic;
using UnityEngine;

// 방공호 2차: 화로·발전기 몸통을 Tripo 모델(품질 4단계 LOD)로 (2026-11-23, 사용자 "방공호 2차: 발전기·화로", "275 한도 승인").
// 계획·사진: Docs/맵모델링_미리보기/13_방공호_가구/계획.md (2차)
// 원칙 (1차 가구와 같다): 코드 화로·발전기를 예전처럼 다 만든 뒤 몸통 렌더러만 지운다. 콜라이더·Furnace·Generator 컴포넌트·소리는 그대로.
// 빛나거나 움직이는 것(불·숯·불빛·불티, 연소실 빛·배기관 빛·계기 바늘·표시등·연기·굴뚝)은 코드로 남기고 모델의 그 자리로 옮긴다.
// 자리 수치: 매우 높음(q3) 메시를 '앞 = 코드 축'(180° 돌림)으로 놓고 빛을 쏴 잰 값 (모델 단위, 11-23). 게임 자리 = 모델 단위 × 배율 + 모델 자리
public static partial class GameSceneBuilder
{
    // ---------- 화로 ----------
    // 배율 1.4: 벽돌 몸통 폭 0.718 → 1.0 m (코드 몸통 1.0 m), 받침 1.10 m, 윗판 1.07 m, 몸통 깊이 0.68 m
    // 모델 자리: 화구 가운데(모델 x 0.075)를 x 0으로, 몸통 앞면(모델 z -0.14)을 코드 앞면 z -0.35로
    const float FnScale = 1.4f;
    static readonly Vector3 FnModelPos = new Vector3(-0.105f, 0f, -0.154f);
    static Vector3 FnAt(float x, float y, float z) => new Vector3(x, y, z) * FnScale + FnModelPos;

    static void TripoShelterFurnace(Transform g)
    {
        if (!kTripoShelter) return;
        var holder = TripoSwapRenderers(g, "shelter_furnace_01",
            TsRenderers(g, "Plinth", "Core", "FrontL", "FrontR", "FrontLow", "FrontHigh", "Band", "TopPlate",
                "MouthT", "MouthB", "MouthL", "MouthR", "MouthDoor", "Hinge", "Log", "IronPlate"),
            TripoFit.Height, TripoAlign.None, FnScale, null, Vector3.zero);
        if (holder == null) return;
        holder.transform.Find("Model").localPosition = FnModelPos;

        // 화구: 모델 단위 x -0.085..0.235 · y 0.32..0.50 · 앞 z -0.14 · 안쪽 벽 z 0.033 (깊이 0.25 m)
        var mouthC = FnAt(0.075f, 0.41f, 0f);
        float mouthW = 0.32f * FnScale, mouthH = 0.18f * FnScale, floorY = 0.32f * FnScale, backZ = FnAt(0, 0, 0.033f).z;
        var fire = g.Find("FireGlow");     // 원래 0.5 × 0.34 (크기는 메시에 들어 있다)
        if (fire != null) { fire.localPosition = new Vector3(mouthC.x, mouthC.y, backZ - 0.01f); fire.localScale = new Vector3((mouthW - 0.02f) / 0.5f, (mouthH - 0.01f) / 0.34f, 1f); }
        // 숯: 바닥 판(원래 0.46 × 0.06 × 0.16, 윗면 0.44)을 얇게 화구 바닥 위로, 숯 덩이 셋은 0.65배로 줄여 판 위에.
        // 그대로 두면 0.45 × 0.25 m 화구 안에서 큰 빨간 상자처럼 보였다 (11-23 넣기 사진)
        const float bedH = 0.035f, coalK = 0.65f;
        float bedTop = floorY + bedH;
        foreach (Transform c in g)
        {
            if (c.name == "CoalBed") { c.localPosition = new Vector3(mouthC.x, floorY + bedH * 0.5f, -0.2f); c.localScale = new Vector3((mouthW - 0.03f) / 0.46f, bedH / 0.06f, 1f); }
            else if (c.name == "Coal")
            {
                var p = c.localPosition;
                c.localPosition = new Vector3(mouthC.x + p.x * 0.85f, bedTop + (p.y - 0.44f) * coalK, -0.2f + (p.z + 0.24f) * 0.8f);
                c.localScale = Vector3.one * coalK;
            }
        }
        var embers = g.Find("Embers"); if (embers != null) embers.localPosition = new Vector3(mouthC.x, mouthC.y + 0.04f, -0.4f);
        var mouth = g.Find("Mouth"); if (mouth != null) mouth.localPosition = new Vector3(mouthC.x, mouthC.y, -0.38f);
        var light = g.Find("FireLight"); if (light != null) light.localPosition = new Vector3(mouthC.x, light.localPosition.y, light.localPosition.z);

        // 연통: 모델 윗판의 짧은 관(모델 단위 가운데 x 0.07 · z 0.233, 위 0.817) 위에서 천장(3.0 m)까지
        var stub = FnAt(0.07f, 0.817f, 0.233f);
        foreach (Transform c in g)
        {
            if (c.name == "FlueCollar") c.localPosition = new Vector3(stub.x, stub.y, stub.z);
            else if (c.name == "Flue") { c.localPosition = new Vector3(stub.x, (stub.y + 3f) * 0.5f, stub.z); c.localScale = new Vector3(c.localScale.x, (3f - stub.y) * 0.5f, c.localScale.z); }
            else if (c.name == "FlueRing" || c.name == "CeilingFlange") c.localPosition = new Vector3(stub.x, c.localPosition.y, stub.z);
        }

        // 장작 콜라이더: 모델 장작 더미(모델 단위 x -0.49..-0.295 · z -0.09..0.15 · 위 0.175) 자리로
        var wood = g.Find("Firewood");
        if (wood != null)
        {
            var w0 = FnAt(-0.49f, 0f, -0.09f); var w1 = FnAt(-0.295f, 0.175f, 0.15f);
            wood.localPosition = new Vector3((w0.x + w1.x) * 0.5f, 0f, (w0.z + w1.z) * 0.5f);
            var col = wood.GetComponent<BoxCollider>();
            if (col != null) { col.center = new Vector3(0f, w1.y * 0.5f, 0f); col.size = new Vector3(w1.x - w0.x, w1.y, w1.z - w0.z); }
        }
        // 문 콜라이더: 모델의 열린 문 판(모델 단위 경첩 쪽 (0.240, -0.194) → 끝 (0.483, -0.410), 높이 0.311..0.525, 매우 높음 꼭짓점의 주축으로 잰 값)에 맞춘다.
        // 코드 문 자리 그대로면 모델 문 끝보다 8 cm 앞까지 보이지 않는 판이 있었다 (11-23 코드 검토)
        var doorT = g.Find("MouthDoor");
        var doorCol = doorT != null ? doorT.GetComponent<BoxCollider>() : null;
        if (doorCol != null)
        {
            var d0 = FnAt(0.240f, 0.311f, -0.194f); var d1 = FnAt(0.483f, 0.525f, -0.410f);
            var dxz = new Vector3(d1.x - d0.x, 0f, d1.z - d0.z);
            doorT.localPosition = (d0 + d1) * 0.5f;
            doorT.localRotation = Quaternion.FromToRotation(Vector3.right, dxz.normalized);
            doorT.localScale = Vector3.one;
            doorCol.center = Vector3.zero;
            doorCol.size = new Vector3(dxz.magnitude, d1.y - d0.y, 0.04f);
        }
        Debug.Log($"[GameSceneBuilder] Shelter: Tripo 화로 — 화구 가운데 {mouthC:F2}, 폭 {mouthW:F2} · 높이 {mouthH:F2} · 안쪽 벽 z {backZ:F2}, 연통 {stub:F2}");
    }

    // ---------- 발전기 ----------
    // 배율 1.7: 모델 길이 0.98 → 1.67 m (코드 팬 덮개 끝 -0.91 ~ 발전부 끝 0.75), 높이 1.17 m(배기관 끝), 깊이 0.71 m
    const float GnScale = 1.7f;
    static readonly Vector3 GnModelPos = new Vector3(-0.08f, 0f, 0f);
    static Vector3 GnAt(float x, float y, float z) => new Vector3(x, y, z) * GnScale + GnModelPos;

    static void TripoShelterGenerator(Transform g)
    {
        if (!kTripoShelter) return;
        var vibe = g.Find("Vibe"); if (vibe == null) return;
        // 바꿀 것: 스키드·엔진·발전부·연료통·팬 덮개·라디에이터·팬 날개(모델 라디에이터 판 뒤라 보이지 않는다: 팬 물체는 남겨 Generator가 돌린다)·
        // 머플러·계기판 몸통과 단추·고무 전선. 남길 것: 연소실 빛(Firebox)·배기관 빛(Runner)·계기·바늘·표시등·불티·배기관·주름 이음새·전선관·벽 배전함·굴뚝
        var holder = TripoSwapRenderers(vibe, "shelter_generator_01",
            TsRenderers(vibe, "Rail", "Cross", "Foot", "Tray", "Block", "BlockL", "BlockR", "BlockLow", "BlockHigh", "Bar", "BarTop", "BarBottom",
                "Head", "ValveCover", "Bolt", "OilCap", "AirFilter", "FilterCap", "Intake", "Flywheel", "Alternator", "Rib", "AltCap", "Vent", "Cradle", "JBox",
                "Tank", "Strap", "FillerNeck", "FillerCap", "SightGlass", "TankLeg", "ShroudT", "ShroudB", "ShroudS", "Radiator", "Hub", "Blade", "Slat", "WarnPlate",
                "ExhaustElbow", "Muffler", "PanelPost", "Panel", "StopButton", "KeySwitch", "Key", "Breaker", "WarnLabel", "Cable"),
            TripoFit.Height, TripoAlign.None, GnScale, null, Vector3.zero);
        if (holder == null) return;
        holder.transform.Find("Model").localPosition = GnModelPos;

        // 연소실 창: 엔진 앞(+Z) 아래쪽의 네모 창 (모델 단위 안쪽 x -0.11..-0.03 · y 0.18..0.22, 테 앞면 z 0.120, 안쪽 벽 z 0.076 → 14 × 8.5 cm, 깊이 7.7 cm).
        // 안쪽 벽에 벌건 판, 그 앞에 달아오르는 관 셋(예전 철창 안 모습을 작게). 처음엔 배기 다기관 뒤 '틈'에 넣었는데 그 틈은 헤드 위 빈 공간이라 판이 엔진 위로 떠 보였다 (11-23 사진)
        var gc = GnAt(-0.07f, 0.20f, 0.079f);
        float winW = 0.08f * GnScale, winH = 0.05f * GnScale;
        var glow = vibe.Find("Firebox"); // 원래 0.3 × 0.19
        if (glow != null) { glow.localPosition = gc; glow.localScale = new Vector3((winW + 0.01f) / 0.3f, (winH + 0.01f) / 0.19f, 1f); }
        int ri = 0;
        foreach (Transform c in vibe)
        {
            if (c.name != "Runner") continue;
            c.localPosition = gc + new Vector3((ri - 1) * 0.035f, 0f, 0.025f); // 빛 판 바로 앞, 창 테보다 안쪽
            c.localScale = new Vector3(0.016f, winH * 0.42f, 0.016f);
            ri++;
        }
        var embers = vibe.Find("Embers");
        if (embers != null)
        {
            embers.localPosition = GnAt(-0.07f, 0.20f, 0.125f); // 창 앞
            var sh = embers.GetComponent<ParticleSystem>().shape; sh.scale = new Vector3(winW, winH, 0.02f);
        }

        // 계기: 오른쪽 끝 계기함 앞면 (모델 단위 x 0.40..0.47 · y 0.385..0.57 · 앞면 z 0.20). 폭 12 cm라 계기 둘·표시등을 위아래로
        var face = GnAt(0.435f, 0f, 0.2f);
        float[] gy = { 0.9f, 0.8f };
        int gi = 0, ni = 0;
        foreach (Transform c in vibe)
        {
            if (c.name == "GaugeRim") { c.localPosition = new Vector3(face.x, gy[Mathf.Min(gi, 1)], face.z + 0.003f); c.localScale = new Vector3(0.09f, 0.003f, 0.09f); }
            else if (c.name == "Gauge") { c.localPosition = new Vector3(face.x, gy[Mathf.Min(gi, 1)], face.z + 0.007f); c.localScale = new Vector3(0.08f, 0.003f, 0.08f); gi++; }
            else if (c.name == "Needle") { c.localPosition = new Vector3(face.x, gy[Mathf.Min(ni, 1)], face.z + 0.012f); c.localScale = new Vector3(0.85f, 0.85f, 1f); ni++; }
            else if (c.name == "RunLamp") c.localPosition = new Vector3(face.x, 0.715f, face.z + 0.006f);
        }

        // 배기: 모델 머플러 위 짧은 관(모델 단위 x 0.16 · z 0.13, 위 0.689)에서 관·주름 이음새·굴뚝을 그 자리로 (굴뚝은 천장까지)
        var ex = GnAt(0.16f, 0.689f, 0.13f);
        foreach (Transform c in vibe)
        {
            if (c.name == "ExhaustPipe") { c.localPosition = new Vector3(ex.x, (ex.y + 1.83f) * 0.5f, ex.z); c.localScale = new Vector3(c.localScale.x, (1.83f - ex.y) * 0.5f, c.localScale.z); }
            else if (c.name == "Bellows") c.localPosition = new Vector3(ex.x, c.localPosition.y, ex.z);
        }
        var chimney = g.Find("Chimney"); if (chimney != null) chimney.localPosition = new Vector3(ex.x, 0f, ex.z);

        // 연료 주입구 (11-23 사용자 "발전기에 연료를 넣을 구멍"): 모델 연료통 윗면(모델 단위 높이 0.535)의 작은 뚜껑 자리(가운데 x 0.26 · z -0.01, 지름 약 0.05, 높이 0.549)에
        // 녹슨 주입관을 세워 뚜껑을 덮고, 관 안은 검게(열린 구멍), 빨간 뚜껑은 옆에 벗겨 두고 가는 줄로 잇는다. 연료를 붓는 소리·빛은 이 구멍(Filler)에서 난다
        {
            var top = GnAt(0.26f, 0.535f, -0.01f);          // 연료통 윗면, 주입구 가운데
            const float neckD = 0.10f, neckH = 0.042f, holeD = 0.07f, capD = 0.10f, capH = 0.024f;
            Prim(PrimitiveType.Cylinder, "FillerNeck", vibe, top + Vector3.up * (neckH * 0.5f), new Vector3(neckD, neckH * 0.5f, neckD), M["metal"]);
            Prim(PrimitiveType.Cylinder, "FillerHole", vibe, top + Vector3.up * (neckH + 0.0008f), new Vector3(holeD, 0.0008f, holeD), M["tire"]);
            var capPos = top + new Vector3(0.10f, capH * 0.5f + 0.004f, -0.045f); // 연료통 윗면 평평한 곳 안 (모델 x 0.17..0.35)
            Prim(PrimitiveType.Cylinder, "FillerCapOff", vibe, capPos, new Vector3(capD, capH * 0.5f, capD), M["paintRed"], new Vector3(0f, 20f, 7f));
            // 뚜껑과 관을 잇는 가는 줄
            var a = top + new Vector3(0.045f, neckH * 0.6f, -0.012f); var b = capPos + new Vector3(-0.04f, 0.004f, 0.012f);
            Prim(PrimitiveType.Cylinder, "FillerTether", vibe, (a + b) * 0.5f, new Vector3(0.005f, (b - a).magnitude * 0.5f, 0.005f), M["tire"],
                Quaternion.FromToRotation(Vector3.up, (b - a).normalized).eulerAngles);
            var filler = new GameObject("Filler").transform;
            filler.SetParent(vibe, false);
            filler.localPosition = top + Vector3.up * neckH;
            var genC = g.GetComponent<Generator>(); if (genC != null) genC.filler = filler;
        }

        // 몸 콜라이더를 모델 크기로 (예전 1.9 × 1.3 × 1.0은 모델보다 앞뒤로 15 cm씩 넓어 보이지 않는 벽이 된다)
        var box = g.GetComponent<BoxCollider>();
        if (box != null) { var a = GnAt(-0.49f, 0f, -0.208f); var b = GnAt(0.49f, 0.62f, 0.208f); box.center = (a + b) * 0.5f; box.size = b - a; }
        Debug.Log($"[GameSceneBuilder] Shelter: Tripo 발전기 — 연소실 빛 {gc:F2}, 계기함 앞면 {face:F2}, 배기관 {ex:F2}, 콜라이더 {(box != null ? box.size.ToString("F2") : "-")}");
    }
}
