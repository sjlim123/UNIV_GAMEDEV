using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// 방공호 작은 소품을 Tripo 모델(품질 4단계 LOD)로 (2026-11-27, 사용자 "방공호 소품 진행" → "5종" → "탁자 위 라디오 빼고 진행" → 모델 4종 "통과, 넣기 진행").
// 계획·조사·사진: Docs/맵모델링_미리보기/20_방공호_작은소품/계획.md
// 새 모델 4종: 천장 전등(전구 5개의 소켓·줄 자리), 벽 비상등 몸통(붉은 등 3개), 선반 위 보급 상자(선반 둘의 장식 상자), 발전기실 벽 배전함.
// 크레딧 0 재사용: 거실 물통 둘 → 드럼통(steel_drum_01), 옛 통로 끝의 잔해 덩어리 셋·판 하나 → 도시 잔해 모델(도시와 같은 규칙).
// 원칙은 방공호 가구·도시 소품과 같다: 방공호를 예전처럼 다 만든 뒤(난수는 이미 다 뽑았다) 길 찾기 지도를 굽기 직전에 겉모습만 바꾼다.
// 콜라이더·이름·불빛(Light)은 그대로라 길 찾기 지도(ShelterNavMesh)·좀비 시야·방 밝기가 바뀌지 않는다.
// 남기는 코드 겉모습: 전구 유리(Glass: ShelterLights가 찾아 깜박이게 한다), 붉게 빛나는 등(이름·재질 그대로, 모양만 둥근 유리로), 선반 깡통.
// 탁자 위 라디오는 바꾸지 않는다 (사용자: 나머지가 다 끝난 뒤 상의)
public static partial class GameSceneBuilder
{
    const string TpCeilingLamp = "shelter_ceiling_lamp_01", TpWallLamp = "shelter_wall_lamp_01", TpCarton = "shelter_carton_01", TpFuseBox = "shelter_fuse_box_01";
    // 천장 전등 크기: 갓 지름 약 27 cm. 소켓 아래 끝을 전구 유리 꼭대기에 맞추면 천장 받침판(두께 3 cm)이 천장 속으로 들어가 이음 고리만 천장에 보인다
    // (거실·발전기실: 전구가 천장 아래 25 cm. 복도는 20 cm라 봉도 5 cm 더 들어간다)
    const float TpCeilingLampScale = 0.30f;
    // 벽 비상등 크기: 받침판 30 × 20 cm, 벽에서 15 cm 나온다 (모델 원본 1 × 0.67 × 0.51)
    const float TpWallLampScale = 0.30f;
    // 배전함 높이 (모델 원본 높이 1): 예전 상자(0.28)보다 크게, 아래 전선관 이음이 올라오는 전선관(ConduitUp, 윗끝 1.41 m)과 겹치게
    const float TpFuseBoxScale = 0.42f;

    // BuildShelterSceneCore가 길 찾기 지도를 굽기 직전에 한 번 부른다. wallT = 벽 두께 (벽면 자리 계산)
    static string TripoShelterProps(Transform root, float wallT)
    {
        if (!kTripoShelter) return "꺼짐";
        var log = new List<string>();
        try { TpNewModels(root, wallT, log); }
        catch (System.Exception e) { Debug.LogError("[GameSceneBuilder] Shelter: Tripo 작은 소품(새 모델)을 넣다 멈춤 — 남은 소품은 코드 모습 그대로: " + e); log.Add("새 모델 오류"); }
        try { TpReuseModels(root, log); }
        catch (System.Exception e) { Debug.LogError("[GameSceneBuilder] Shelter: Tripo 작은 소품(재사용 모델)을 넣다 멈춤 — 남은 소품은 코드 모습 그대로: " + e); log.Add("재사용 모델 오류"); }
        return string.Join(", ", log);
    }

    // 새 모델 4종: 천장 전등·벽 비상등·보급 상자·배전함
    static void TpNewModels(Transform root, float wallT, List<string> log)
    {

        // 천장 전등: 전구 묶음마다. 녹·얼룩이 똑같아 보이지 않게 전등마다 돌려 놓는다
        var bulbs = new List<Transform>();
        foreach (Transform c in root) if (c.name == "Bulb") bulbs.Add(c);
        int lamps = 0;
        for (int i = 0; i < bulbs.Count; i++) if (TpCeilingLampSwap(bulbs[i], i * 73f)) lamps++;
        log.Add($"천장 전등 {lamps}/{bulbs.Count}");

        // 벽 비상등: 남쪽 벽 비상등(천장 배관 바로 아래: 예전 자리는 배관에 가려 붉은 빛만 보였다), 출입 철문 위 출구등, 방폭문 옆 경고등(세로로)
        int wall = 0;
        float half = 0.335f * TpWallLampScale; // 받침판 짧은 쪽의 반
        if (TpWallLampSwap(root.Find("EmergencyLamp"), new Vector3(0f, 2.6f, -4f + wallT * 0.5f), Vector3.forward, Vector3.right)) wall++;
        var exit = root.Find("Exit");
        // 문틀 윗면(2.37 m)과 복도 천장(2.6 m) 사이. 벽면 = 복도 북쪽 벽 안쪽 면 (z 12 − 두께/2)
        if (exit != null && TpWallLampSwap(exit.Find("ExitLamp"), new Vector3(0f, 2.385f + half, 12f - wallT * 0.5f - exit.localPosition.z), Vector3.back, Vector3.right)) wall++;
        var blast = root.Find("BlastDoor");
        // 방폭문 틀 기둥(바깥 끝 −0.96) 옆 벽. 틀의 −Z가 복도 쪽
        if (blast != null && TpWallLampSwap(blast.Find("WarnLamp"), new Vector3(-1.14f, 2.05f, -wallT * 0.5f), Vector3.back, Vector3.up)) wall++;
        log.Add($"벽 비상등 {wall}/3");

        // 선반 위 보급 상자: 선반의 장식 상자(Box). TripoShelterShelf가 이미 모델 판자 위로 올려 두었다. 깡통(Can)은 그대로
        int cartons = 0, boxes = 0;
        foreach (Transform shelf in root)
        {
            if (shelf.name != "Shelf") continue;
            var list = new List<Transform>();
            foreach (Transform c in shelf) if (c.name == "Box") list.Add(c);
            foreach (var c in list)
            {
                var r = c.GetComponent<MeshRenderer>(); if (r == null) continue;
                var holder = TripoSwapRenderers(c, TpCarton, new List<Renderer> { r }, TripoFit.Box, TripoAlign.Bottom, 0f, null, Vector3.zero);
                if (holder == null) continue;
                if (cartons % 2 == 1) holder.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // 하나 걸러 뒤로 돌려 놓는다 (상자 가운데가 축이라 자리는 그대로)
                cartons++;
            }
            boxes += list.Count;
        }
        log.Add($"보급 상자 {cartons}/{boxes}");

        // 발전기실 배전함: 발전기 묶음의 WallBox (콜라이더 없음, 스크립트가 쓰지 않는 장식). 뒷면·바닥은 예전 상자 자리, 문이 방 쪽(+Z)을 본다
        var wallBox = root.Find("Generator/WallBox");
        var wr = wallBox != null ? wallBox.GetComponent<MeshRenderer>() : null;
        var fuseHolder = wr != null ? TripoSwapRenderers(wallBox, TpFuseBox, new List<Renderer> { wr }, TripoFit.Height, TripoAlign.BackBottom, TpFuseBoxScale, null, Vector3.zero) : null;
        bool fuse = fuseHolder != null;
        // 모델 아래 전선관 이음은 둘이고(가운데가 모델 x ±0.117) 그 사이는 비어 있다: 가운데에 놓으면 올라오는 전선관(x 0.5)이 빈 틈으로 들어간다 (11-27 검토).
        // 오른쪽 이음이 전선관 위에 오게 옆으로 옮긴다 (뒷면은 벽에 그대로)
        var fuseModel = fuse ? fuseHolder.transform.Find("Model") : null;
        if (fuseModel != null) fuseModel.localPosition += new Vector3(-0.117f * TpFuseBoxScale, 0f, 0f);
        log.Add("배전함 " + (fuse ? 1 : 0) + "/1");
    }

    // 재사용 모델 (크레딧 0): 재질은 도시가 쓰는 것을 불러 쓰기만 한다 (값을 다시 쓰지 않아 도시 재질 파일이 바뀌지 않는다)
    static void TpReuseModels(Transform root, List<string> log)
    {
        bool keepWas = kKeepMaterials;
        kKeepMaterials = true;
        try
        {
            // 물통 둘 → 드럼통. 둘이 똑같아 보이지 않게 하나는 돌려 놓는다 (받침은 통 가운데에 있어 자리는 그대로)
            int drums = 0;
            foreach (var name in new[] { "WaterBarrel1", "WaterBarrel2" })
            {
                var b = root.Find(name); if (b == null) continue;
                // 크기: 높이로 맞추면(0.9 m) 몸통 지름이 0.66 m라 캡슐 콜라이더(지름 0.55)보다 5 cm씩 넓다 → 0.8배 (높이 0.8, 몸통 지름 0.58 m)
                var holder = TripoSwap(b, "steel_drum_01", null, TripoFit.Height, TripoAlign.Bottom, 0.8f);
                if (holder == null) continue;
                if (drums == 1) holder.transform.Rotate(0f, 140f, 0f, Space.Self);
                drums++;
            }
            log.Add($"물통 {drums}/2");

            // 옛 통로 끝의 잔해 (콜라이더 없는 장식): 덩어리는 도시와 같은 규칙으로 고르고, 판은 콘크리트 판 모델
            // 덩어리는 도시의 규칙(TcRubbleId)을 그대로 부르지 않는다: 그 함수는 도시에서만 만드는 재질 이름(aptWall)을 찾아서, 방공호만 생성하면 없는 이름으로 멈춘다 (11-27 검토).
            // 방공호 잔해 재질은 둘뿐이다: 금 간 콘크리트 → 철근 콘크리트 덩어리(납작하면 판 조각), 더러운 콘크리트 → 칠한 벽 콘크리트 덩어리
            int chunks = 0, slabs = 0;
            M.TryGetValue("cracked", out var mCracked); M.TryGetValue("dirty", out var mDirty);
            var tunnel = root.Find("OldTunnel");
            if (tunnel != null)
            {
                var deb = new List<Transform>();
                foreach (Transform c in tunnel) if (c.name == "Chunk" || c.name == "Slab") deb.Add(c);
                foreach (var c in deb)
                {
                    var r = c.GetComponent<MeshRenderer>(); var mf = c.GetComponent<MeshFilter>();
                    if (r == null || mf == null || mf.sharedMesh == null || mf.sharedMesh.vertexCount != 24) continue;
                    var size = mf.sharedMesh.bounds.size;
                    if (c.name == "Slab") { if (TcSlabSwap(c, r, size) > 0) slabs++; continue; }
                    var mat = r.sharedMaterial; float flat = size.y / Mathf.Max(1e-3f, Mathf.Min(size.x, size.z));
                    string id = mat == null ? null : mat == mCracked ? (flat < 0.35f ? "city_rubble_slab_01" : "city_rubble_concrete_01")
                        : mat == mDirty ? (flat < 0.3f ? "city_rubble_slab_01" : "city_rubble_wall_01") : null;
                    if (id != null && TcBoxSwap(c, r, id, size) != null) chunks++;
                }
            }
            log.Add($"옛 통로 잔해 덩어리 {chunks}/3 · 판 {slabs}/1");
        }
        finally { kKeepMaterials = keepWas; }
    }

    // 천장 전등: 전구 묶음(Bulb)의 소켓·줄 자리에 갓 달린 천장 전등을 단다. 전구 유리(Glass)와 불빛(Light)은 손대지 않는다.
    // 모델 소켓 아래 끝의 가운데를 유리 꼭대기에 맞춘다 (유리가 소켓에 4 mm 물린다). yaw = 전등을 돌려 놓는 각도 (소켓이 축이라 유리 자리는 그대로)
    static bool TpCeilingLampSwap(Transform bulb, float yaw)
    {
        var glass = bulb.Find("Glass"); if (glass == null) return false;
        var holder = new GameObject("Tripo_" + TpCeilingLamp).transform;
        holder.SetParent(bulb, false);
        holder.localRotation = Quaternion.Euler(0f, yaw, 0f);
        var go = TripoTierModel(holder, TpCeilingLamp, TpCeilingLamp, 0f);
        Vector3 socket = Vector3.zero;
        if (go != null) go.transform.localScale = Vector3.one * TpCeilingLampScale;
        if (go == null || !TpLampSocket(holder, go.transform.Find("LOD0_q3"), out socket))
        {
            Debug.LogWarning("[GameSceneBuilder] Shelter: 천장 전등 모델을 놓지 못해 코드 소켓·줄 그대로 (" + TpCeilingLamp + ")");
            Object.DestroyImmediate(holder.gameObject);
            return false;
        }
        var gl = holder.InverseTransformPoint(glass.position); // 유리 가운데 (받침 기준)
        float gr = glass.lossyScale.y * 0.5f;
        go.transform.localPosition += new Vector3(gl.x - socket.x, gl.y + gr - 0.004f - socket.y, gl.z - socket.z);
        // 그림자는 끈다: 불빛이 전구 바로 아래(갓 테두리 높이)에 있어 갓이 그림자를 드리우면 천장과 벽 위쪽이 어두워져 방 밝기가 달라진다 (전구 유리도 꺼져 있다)
        foreach (var mr in go.GetComponentsInChildren<MeshRenderer>()) mr.shadowCastingMode = ShadowCastingMode.Off;
        var lg = go.GetComponent<LODGroup>(); if (lg != null) lg.RecalculateBounds();
        TcStrip(TsRenderers(bulb, "Cord", "Socket"));
        foreach (var t in holder.GetComponentsInChildren<Transform>())
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.OccludeeStatic);
        return true;
    }

    // 전등 갓 안의 소켓 아래 끝 가운데 (받침 기준 좌표). 갓 한가운데 둘레(갓 반지름의 30 % 안)에서 위로 쏘아 맞은 자리 가운데 가장 낮은 것들 = 소켓 테두리 아랫면
    // (소켓 속은 비어 있어 더 높이 맞고, 갓 안쪽 면도 소켓보다 높다)
    static bool TpLampSocket(Transform holder, Transform lod0, out Vector3 socket)
    {
        socket = Vector3.zero;
        var mf = lod0 != null ? lod0.GetComponent<MeshFilter>() : null;
        if (mf == null || mf.sharedMesh == null) return false;
        var nb = TripoLocalBounds(holder, new Renderer[] { lod0.GetComponent<MeshRenderer>() });
        var mc = lod0.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = mf.sharedMesh;
        Physics.SyncTransforms();
        float rad = Mathf.Max(nb.extents.x, nb.extents.z) * 0.3f; const int N = 24;
        var hits = new List<Vector3>(); float low = float.MaxValue;
        for (int i = -N; i <= N; i++)
            for (int j = -N; j <= N; j++)
            {
                var o = new Vector3(nb.center.x + rad * i / N, nb.min.y - 0.05f, nb.center.z + rad * j / N);
                if (!mc.Raycast(new Ray(holder.TransformPoint(o), holder.up), out var hit, nb.size.y + 0.1f)) continue;
                var p = holder.InverseTransformPoint(hit.point); hits.Add(p); low = Mathf.Min(low, p.y);
            }
        Object.DestroyImmediate(mc);
        if (hits.Count == 0) return false;
        float lim = low + nb.size.y * 0.06f; int n = 0;
        foreach (var p in hits) if (p.y <= lim) { socket += p; n++; }
        socket /= n;
        return true;
    }

    // 벽 비상등: 철망 달린 주물 등 몸통을 벽에 붙이고, 붉게 빛나던 상자(lamp: 이름·재질 그대로)를 그 안의 둥근 유리로 바꾼다 (상자 메시 → 구 메시, 납작하게 늘인다).
    // pos = 벽면 위 받침판 가운데, outward = 벽에서 나오는 방향, along = 등의 긴 쪽 방향 (모두 lamp의 부모 기준, 둘은 서로 직각). 불빛(Light)은 손대지 않는다
    static bool TpWallLampSwap(Transform lamp, Vector3 pos, Vector3 outward, Vector3 along)
    {
        var mf = lamp != null ? lamp.GetComponent<MeshFilter>() : null;
        var mr = lamp != null ? lamp.GetComponent<MeshRenderer>() : null;
        if (mf == null || mr == null) return false;
        var holder = new GameObject("Tripo_" + TpWallLamp).transform;
        holder.SetParent(lamp.parent, false);
        holder.localPosition = pos;
        holder.localRotation = Quaternion.LookRotation(Vector3.Cross(along, outward), outward); // 모델: 긴 쪽이 X, 철망 쪽이 +Y, 받침판이 바닥
        var go = TripoTierModel(holder, TpWallLamp, TpWallLamp, 0f);
        if (go == null)
        {
            Debug.LogWarning("[GameSceneBuilder] Shelter: 벽 비상등 모델을 놓지 못해 코드 상자 그대로 (" + lamp.name + ")");
            Object.DestroyImmediate(holder.gameObject);
            return false;
        }
        float s = TpWallLampScale;
        go.transform.localScale = Vector3.one * s;
        var nb = TripoLocalBounds(holder, new Renderer[] { go.transform.Find("LOD0_q3").GetComponent<MeshRenderer>() });
        go.transform.localPosition -= new Vector3(nb.center.x, nb.min.y, nb.center.z); // 받침판 밑면 가운데가 벽면의 pos에
        var lg = go.GetComponent<LODGroup>(); if (lg != null) lg.RecalculateBounds();
        // 유리: 몸통 안쪽(모델 원본 치수: 바닥 0.06, 테두리 0.29, 안 너비 ±0.41 × ±0.25, 철망 꼭대기 0.51)에 들어가는 납작한 타원체.
        // 가운데 높이 0.20, 반지름 0.36 × 0.22(바깥쪽) × 0.21 → 꼭대기 0.42가 철망 살 아래에 온다 (11-27 치수 재기)
        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        mf.sharedMesh = sphere.GetComponent<MeshFilter>().sharedMesh;
        Object.DestroyImmediate(sphere);
        lamp.SetPositionAndRotation(holder.TransformPoint(new Vector3(0f, 0.20f * s, 0f)), holder.rotation);
        lamp.localScale = new Vector3(0.72f, 0.44f, 0.42f) * s;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        foreach (var t in holder.GetComponentsInChildren<Transform>())
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.OccludeeStatic);
        return true;
    }
}
