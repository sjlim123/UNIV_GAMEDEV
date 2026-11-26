using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 도시 남은 소품 1차 (2026-11-24, 사용자 "1차: 재사용+상위 4종"). 계획: Docs/맵모델링_미리보기/17_도시_남은소품/계획.md
// 원칙은 도시 소품·방공호 가구와 같다: 코드가 난수를 다 뽑아 예전처럼 만든 뒤 겉모습 렌더러만 Tripo 모델(품질 4단계 LOD)로 바꾼다.
// 콜라이더·이름·자리는 그대로라 아이템 자리·좀비 길찾기·NavMesh가 바뀌지 않는다. 도시를 만드는 동안(kTripoCity)에만 바꾼다.
// 크레딧 0 재사용: 선반은 TripoShelterShelf(GameSceneBuilder.TripoShelter.cs)가 도시에서도 돈다. 2층 방 책상·의자는 아래
public static partial class GameSceneBuilder
{
    // 2층 방 책상(상판 1.4×0.8, 윗면 0.785 m, 위에 아이템 자리 +0.85)과 의자: 방공호 탁자·의자 모델.
    // 탁자는 축마다 예전 크기에 맞춰 윗면 높이가 그대로, 의자는 높이로 맞춘다 (방공호와 같은 방법)
    static void TripoCityDeskChair(Transform desk, Transform chair)
    {
        if (!kTripoCity) return;
        if (desk != null)
        {
            var old = new List<Renderer>();
            foreach (Transform c in desk) if (c.name == "Top" || c.name == "Leg") old.AddRange(c.GetComponents<Renderer>());
            TripoSwapRenderers(desk, "shelter_table_01", old, TripoFit.Box, TripoAlign.Bottom, 0f, null, Vector3.zero);
        }
        if (chair != null)
            TripoSwapRenderers(chair, "shelter_chair_01", new List<Renderer>(chair.GetComponentsInChildren<Renderer>()), TripoFit.Height, TripoAlign.Bottom, 0f, null, Vector3.zero);
    }

    // 선반 아이템 자리 (11-24 검토): Tripo 선반은 판자 다섯 장이라 판자 사이가 0.36 m뿐이다. 예전 자리(0.65·1.15·0.15 m)에 생긴 연료통(0.5 m)이
    // 위 판자를 뚫고 나와서, 도시 Tripo 선반이면 아이템을 위가 트인 맨 위 판자(1.605 m)에 놓고 그 자리 둘레 장식(깡통·상자)을 치운다 (난수는 쓰지 않는다)
    static Vector3 ShelfItemPoint(Transform shelf, Vector3 local)
    {
        if (kTripoCity && shelf.Find("Tripo_shelter_shelf_01") != null)
        {
            local.y = 1.65f;
            var gone = new List<GameObject>();
            foreach (Transform c in shelf)
                if ((c.name == "Can" || c.name == "Box") && c.localPosition.y > 1.5f && Mathf.Abs(c.localPosition.x - local.x) < 0.22f) gone.Add(c.gameObject);
            foreach (var go in gone) Object.DestroyImmediate(go);
        }
        return shelf.position + shelf.rotation * local;
    }

    // ---------- 새 모델 8종 (11-24 웹 스튜디오, 사용자 "통과, 도시에 넣기") ----------
    // 도시를 다 만든 뒤, 길 찾기 지도를 굽기 직전에 이름으로 한 번 훑어 바꾼다 (난수를 쓰지 않고 콜라이더는 그대로라 배치·길 찾기가 그대로).
    // 지도는 콜라이더로 굽는다 (NavMeshCollectGeometry.PhysicsColliders): 겉모습을 바꿔도 길이 바뀌지 않는다.
    //  - 잔해 덩어리 Chunk·Rubble·RoofChunk (Box 상자): 예전 재질로 고른다. 벽돌 → 벽돌 덩어리, 아스팔트 → 아스팔트 조각,
    //    금 간 콘크리트 → 납작하면 판 조각 아니면 철근 콘크리트, 더러운 콘크리트·아파트 벽 → 칠한 벽 콘크리트나 철근 콘크리트 (자리로 정한다)
    //  - 나무 상자 Crate, 도로 방호벽 Barrier(2×0.9×0.6 상자만, 쓰러진 것은 쓰러진 채), 해치 모래주머니 상자 Sandbag1~4
    //  - 눕힌 캡슐 모래주머니 Sandbag (KSandbag): 캡슐이 Z로 90° 누워 있어 받침을 수평(긴 쪽 = 캡슐 축)으로 따로 둔다
    static string TripoCityRound1(Transform root)
    {
        if (!kTripoCity) return "꺼짐";
        var cnt = new Dictionary<string, int>();
        System.Action<string> add = k => { cnt.TryGetValue(k, out int n); cnt[k] = n + 1; };
        var all = root.GetComponentsInChildren<MeshRenderer>(true);
        foreach (var r in all)
        {
            if (r == null || !r.enabled) continue;
            var t = r.transform; var mf = t.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            string id = null;
            var mat = r.sharedMaterial;
            bool isBox = mf.sharedMesh.vertexCount == 24 && (t.lossyScale - Vector3.one).sqrMagnitude < 1e-4f; // Box(): 크기가 메시에 들어간 상자
            Vector3 size = mf.sharedMesh.bounds.size;
            if (isBox && (t.name == "Chunk" || t.name == "Rubble" || t.name == "RoofChunk")) id = TcRubbleId(t, mat, size);
            else if (isBox && t.name == "Crate") id = "city_crate_01";
            // 쓰러진 방호벽(Z 80°)은 예전 상자 그대로 둔다: 모델을 상자 축에 맞추면 길쭉한 면을 세워 땅에 반쯤 묻힌 모습이 된다 (11-24 검토)
            else if (isBox && t.name == "Barrier" && Vector3.Dot(t.up, Vector3.up) > 0.9f && Mathf.Abs(size.x - 2f) < 0.05f && Mathf.Abs(size.y - 0.9f) < 0.05f) id = "city_barrier_01";
            else if (isBox && t.name.StartsWith("Sandbag") && t.name.Length == 8) id = "city_sandbag_01";
            if (id != null)
            {
                if (TcBoxSwap(t, r, id, size) != null) add(id);
                continue;
            }
            // 눕힌 캡슐 모래주머니: 긴 축(캡슐의 위)을 수평 받침의 X로
            if (t.name == "Sandbag" && mf.sharedMesh.name.StartsWith("Capsule"))
            {
                var axis = Vector3.ProjectOnPlane(t.up, Vector3.up);
                if (axis.sqrMagnitude < 1e-4f) continue;
                var fit = new GameObject("SandbagFit").transform;
                fit.SetParent(t.parent, false);
                fit.SetPositionAndRotation(t.position, Quaternion.FromToRotation(Vector3.right, axis.normalized));
                if (TripoSwapRenderers(fit, "city_sandbag_01", new List<Renderer> { r }, TripoFit.Box, TripoAlign.Bottom, 0f, null, Vector3.zero) != null) add("city_sandbag_01(캡슐)");
                else Object.DestroyImmediate(fit.gameObject);
            }
        }
        var parts = new List<string>();
        foreach (var kv in cnt) parts.Add(kv.Key + " " + kv.Value);
        return string.Join(", ", parts);
    }

    // 잔해 상자 하나에 놓을 모델을 예전 재질로 고른다 (1차의 규칙. 102동 TripoAptPass도 같은 규칙을 쓴다). 잔해 재질이 아니면 null
    static string TcRubbleId(Transform t, Material mat, Vector3 size)
    {
        int h = Mathf.Abs(Mathf.RoundToInt(t.position.x * 7.31f) * 31 + Mathf.RoundToInt(t.position.z * 5.17f) * 17 + Mathf.RoundToInt(t.position.y * 3.3f)) % 2;
        float flat = size.y / Mathf.Max(1e-3f, Mathf.Min(size.x, size.z));
        if (mat == M["brick"]) return "city_rubble_brick_01";
        if (mat == M["asphalt"]) return "city_rubble_asphalt_01";
        if (mat == M["cracked"]) return flat < 0.35f ? "city_rubble_slab_01" : "city_rubble_concrete_01";
        if (mat == M["aptWall"]) return flat < 0.3f ? "city_rubble_slab_01" : "city_rubble_wall_01";
        if (mat == M["dirty"]) return flat < 0.3f ? "city_rubble_slab_01" : h == 0 ? "city_rubble_wall_01" : "city_rubble_concrete_01";
        return null;
    }

    // 상자 하나(t의 렌더러 r, 메시 크기 size)의 겉모습을 모델로 바꾼다 (1차의 본체: 잔해·나무 상자·방호벽·모래주머니). 받침을 돌려준다 (못 바꾸면 null)
    static GameObject TcBoxSwap(Transform t, MeshRenderer r, string id, Vector3 size)
    {
        bool rubble = id.StartsWith("city_rubble_");
        // 잔해가 앞뒤(z)로 길쭉하면 모델을 90° 돌려 긴 쪽(x)을 맞춘다 (축마다 늘이면 덩어리 모델이 4배까지 찌그러졌다, 11-24 검토)
        Transform target = t;
        if (rubble && size.z > size.x * 1.3f)
        {
            target = new GameObject("ChunkFit").transform;
            target.SetParent(t, false);
            target.localRotation = Quaternion.Euler(0f, 90f, 0f);
        }
        var holder = TripoSwapRenderers(target, id, new List<Renderer> { r }, TripoFit.Box, TripoAlign.Bottom, 0f, null, Vector3.zero);
        if (holder != null)
        {
            if (id == "city_rubble_concrete_01") TcFillRebarBody(holder.transform);
        }
        else if (target != t) Object.DestroyImmediate(target.gameObject);
        return holder;
    }

    // 철근 콘크리트 덩어리: 몸통(모델 x -0.25~0.49, 전체 -0.48~0.49)이 예전 상자를 채우고 철근은 -x 쪽 밖으로 삐져나오게 (11-24 검토:
    // 철근까지 상자에 맞추면 철근 가닥만 보이는 곳에 상자 콜라이더가 남아 보이지 않는 벽이 된다). 몸통 +x 끝은 그대로 둔다
    static void TcFillRebarBody(Transform holder)
    {
        var model = holder.Find("Model"); if (model == null) return;
        var rs = model.GetComponentsInChildren<MeshRenderer>();
        var b0 = TripoLocalBounds(holder, rs);
        var s = model.localScale; s.x *= 0.972f / 0.74f; model.localScale = s;
        var b1 = TripoLocalBounds(holder, rs);
        model.localPosition += new Vector3(b0.max.x - b1.max.x, 0f, 0f);
        var lg = model.GetComponent<LODGroup>(); if (lg != null) lg.RecalculateBounds();
    }

    // ---------- 2차 7종 (11-26 웹 스튜디오 H3.5, 사용자 "통과, 도시에 넣기") ----------
    // 계획: Docs/맵모델링_미리보기/18_도시_남은소품_2차/계획.md. 1차와 같은 때(길 찾기 지도를 굽기 직전)에 이름으로 훑어 겉모습만 바꾼다.
    //  - 매트리스 Mattress(Pad + Pillow): 모델 베개가 -Z 쪽이라 받침을 180° 돌린다 (코드 베개는 +Z)
    //  - 계산대 Counter(Top + Body), 쓰레기통 Bins의 BinA(초록)·BinB(파랑) + 뚜껑 Lid
    //  - 옥상 물탱크: 가게 옥상 Tank/Shell(쓰러진 것은 쓰러진 채), 아파트 옥상의 합친 Tank. 노랑(예전 줄무늬 재질)·파랑(예전 파란 재질). 쇠 받침대 TankStand는 코드 그대로
    //  - 부서진 콘크리트 판: 폭격 건물의 떨어진 천장 FallenSlab, 아파트 파손부의 BrokenSlab·HangingWall. 모델(얇은 쪽 Y, 긴 쪽 Z)을 상자의 얇은 쪽·긴 쪽에 맞춰 돌린다.
    //    길쭉한 상자(긴 쪽이 짧은 쪽의 2.8배 넘음)는 모델을 긴 쪽으로 여러 장 이어 놓는다 (한 장을 늘이면 6배까지 찌그러진다). 모델 양 끝 6 %는 철근 가닥뿐이라
    //    긴 쪽을 1/0.88배 더 늘려 콘크리트 몸통이 콜라이더를 채우게 한다 (11-26 검토).
    //    걸어 오르는 무너진 지붕 판(FallenRoof/Slab)과 무너진 건물의 큰 FallenSlab(3 m 넘음)은 코드 그대로: 테두리가 들쭉날쭉한 모델로 바꾸면 발밑 콜라이더와 어긋난다
    //  - 볼라드: 블록마다 한 메시로 합친 Bollards·BollardBands를 지우고 BollardCollider 자리마다 모델 하나 (높이는 예전 0.915 m)
    //  - 그대로 두는 것: 넘어진 탁자 TableTipped(비스듬히 세운 판자 콜라이더와 탁자 모델이 맞지 않는다), 인도의 부서진 의자 BrokenChair
    //    (콜라이더 없는 납작한 잔해라, 멀쩡한 의자 모델로 바꾸면 뚫고 지나가는 가구가 된다. 11-26 검토)
    //  - 불탄 나무 밑동은 가로수를 만들 때 바꾼다 (KoreaStreet.cs StreetTree)
    static string TripoCityRound2(Transform root)
    {
        if (!kTripoCity) return "꺼짐";
        var cnt = new Dictionary<string, int>();
        System.Action<string> add = k => { cnt.TryGetValue(k, out int n); cnt[k] = n + 1; };

        // 볼라드: 콜라이더 물체 이름으로
        var byParent = new Dictionary<Transform, int>();
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t == null) continue;
            if (t.name == "BollardCollider" && t.parent != null)
            {
                var par = t.parent;
                var basePos = par.InverseTransformPoint(t.position) - Vector3.up * 0.45f; // 콜라이더 상자(0.9) 가운데 → 인도 윗면
                float yaw = Mathf.Abs(Mathf.RoundToInt(t.position.x * 13.7f) * 31 + Mathf.RoundToInt(t.position.z * 9.1f) * 17) % 360; // 자리로 정한 방향 (난수를 쓰지 않는다)
                if (TcPlace(par, "city_bollard_01", null, basePos, Quaternion.Euler(0f, yaw, 0f), 0.915f) != null)
                {
                    add("city_bollard_01");
                    byParent.TryGetValue(par, out int n); byParent[par] = n + 1;
                }
            }
        }
        foreach (var kv in byParent) // 모델을 세운 블록만 합친 메시를 지운다
            foreach (var nm in new[] { "Bollards", "BollardBands" })
            {
                var c = kv.Key.Find(nm);
                if (c != null) TcStrip(new List<Renderer>(c.GetComponents<MeshRenderer>()));
            }

        foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (r == null || !r.enabled) continue;
            var t = r.transform; var mf = t.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null || t.parent == null) continue;
            bool isBox = mf.sharedMesh.vertexCount == 24 && (t.lossyScale - Vector3.one).sqrMagnitude < 1e-4f;
            Vector3 size = mf.sharedMesh.bounds.size;
            string n = t.name, pn = t.parent.name;
            bool blue = r.sharedMaterial == M["paintBlue"];
            if (isBox && n == "Pad" && pn == "Mattress")
            {
                var old = new List<Renderer> { r };
                var pil = t.parent.Find("Pillow"); var pr = pil != null ? pil.GetComponent<MeshRenderer>() : null;
                if (pr != null) old.Add(pr);
                var fit = new GameObject("MattressFit").transform;
                fit.SetParent(t.parent, false);
                fit.localRotation = Quaternion.Euler(0f, 180f, 0f);
                if (TripoSwapRenderers(fit, "city_mattress_01", old, TripoFit.Box, TripoAlign.Bottom, 0f, null, Vector3.zero) != null) add("city_mattress_01");
                else Object.DestroyImmediate(fit.gameObject);
            }
            else if (isBox && n == "Body" && pn == "Counter")
            {
                var old = new List<Renderer> { r };
                var top = t.parent.Find("Top"); var tr = top != null ? top.GetComponent<MeshRenderer>() : null;
                if (tr != null) old.Add(tr);
                if (TripoSwapRenderers(t.parent, "city_counter_01", old, TripoFit.Box, TripoAlign.Bottom, 0f, null, Vector3.zero) != null) add("city_counter_01");
            }
            else if (isBox && (n == "BinA" || n == "BinB") && pn == "Bins")
            {
                // 뚜껑(같은 x의 Lid)은 모델에 들어 있다: 통 상자에 맞추고 뚜껑 렌더러는 지운다
                var lids = new List<Renderer>();
                foreach (Transform c in t.parent)
                    if (c.name == "Lid" && Mathf.Abs(c.localPosition.x - t.localPosition.x) < 0.05f) lids.AddRange(c.GetComponents<MeshRenderer>());
                if (TripoSwapRenderers(t, "city_bin_01", new List<Renderer> { r }, TripoFit.Box, TripoAlign.Bottom, 0f, blue ? "city_bin_01_blue" : null, Vector3.zero) != null)
                {
                    TcStrip(lids);
                    add(blue ? "city_bin_01(파랑)" : "city_bin_01");
                }
            }
            else if (!isBox && ((n == "Shell" && pn == "Tank") || (n == "Tank" && size.y > 1.8f && size.y < 2.2f && size.x > 1.4f && size.x < 1.9f)))
            {
                var target = n == "Shell" ? t.parent : t; // 가게 옥상: 쓰러진 탱크는 Tank째 돌아가 있다. 아파트: 합친 메시 하나(다리 토막 포함)
                if (TcTankSwap(target, r, blue) != null) add(blue ? "city_water_tank_01(파랑)" : "city_water_tank_01");
            }
            else if (isBox && (n == "BrokenSlab" || n == "HangingWall" || (n == "FallenSlab" && Mathf.Max(size.x, size.z) < 3f)))
            {
                int pieces = TcSlabSwap(t, r, size);
                if (pieces > 0)
                {
                    add("city_slab_01(" + n + ")"); if (pieces > 1) add("city_slab_01(이어 놓은 장)");
                }
            }
        }
        var parts = new List<string>();
        foreach (var kv in cnt) parts.Add(kv.Key + " " + kv.Value);
        return string.Join(", ", parts);
    }

    // 물탱크 하나(target 아래 렌더러 r)를 모델로 바꾼다 (2차의 본체. 102동 꼭대기 물탱크도 쓴다). 받침을 돌려준다 (못 바꾸면 null)
    static GameObject TcTankSwap(Transform target, MeshRenderer r, bool blue)
    {
        var ob = TripoLocalBounds(target, new Renderer[] { r }); // 받침은 target과 같은 자리·방향이라 같은 좌표다
        var h = TripoSwapRenderers(target, "city_water_tank_01", new List<Renderer> { r }, TripoFit.Box, TripoAlign.Bottom, 0f, blue ? "city_water_tank_01_blue" : null, Vector3.zero);
        if (h != null) TcTankRound(h.transform, ob);
        return h;
    }

    // 콘크리트 판 상자 하나(t의 렌더러 r, 메시 크기 size)를 모델로 바꾼다 (2차의 본체. 102동 잘린 면의 판·매달린 벽도 쓴다).
    // 놓으려던 장 수를 돌려준다 (0 = 못 바꿈, 2 이상 = 긴 쪽으로 이어 놓음)
    static int TcSlabSwap(Transform t, MeshRenderer r, Vector3 size)
    {
        int thin = size.x <= size.y && size.x <= size.z ? 0 : size.y <= size.z ? 1 : 2;
        int lng = -1;
        for (int a = 0; a < 3; a++) if (a != thin && (lng < 0 || size[a] > size[lng])) lng = a;
        int mid = 3 - thin - lng;
        var fwd = Vector3.zero; fwd[lng] = 1f;
        var up = Vector3.zero; up[thin] = 1f;
        var fit = new GameObject("SlabFit").transform;
        fit.SetParent(t, false);
        fit.localRotation = Quaternion.LookRotation(fwd, up);
        var lb = TripoLocalBounds(fit, new Renderer[] { r }); // 받침 기준 예전 상자: x 짧은 쪽, y 얇은 쪽, z 긴 쪽
        float ratio = size[lng] / Mathf.Max(0.01f, size[mid]);
        int pieces = ratio > 2.8f ? Mathf.Max(2, Mathf.RoundToInt(ratio / 1.73f)) : 1; // 모델은 긴 쪽이 짧은 쪽의 1.73배
        float seg = lb.size.z / pieces;
        int made = 0;
        for (int k = 0; k < pieces; k++)
        {
            var box = new Bounds(new Vector3(lb.center.x, lb.center.y, lb.min.z + seg * (k + 0.5f)), new Vector3(lb.size.x, lb.size.y, seg));
            if (TcBoxFit(fit, "city_slab_01", null, box, k % 2 == 1 ? 180f : 0f, new Vector3(1f, 1f, 1f / 0.88f)) != null) made++;
        }
        if (made > 0)
        {
            TcStrip(new List<Renderer> { r });
            return pieces;
        }
        Object.DestroyImmediate(fit.gameObject);
        return 0;
    }

    // 품질 4단계 모델을 frame(크기 1, 똑바로 선 묶음) 아래에 놓는다: 모델 자체 높이를 height로, frame 기준 상자의 바닥 가운데를 localPos에.
    // localRot으로 돌릴 수 있다 (돌린 뒤의 가장 낮은 곳이 localPos 높이에 닿는다). 난수를 쓰지 않는다. 볼라드에 쓴다
    static Transform TcPlace(Transform frame, string id, string matId, Vector3 localPos, Quaternion localRot, float height)
    {
        var holder = new GameObject("Tripo_" + id).transform;
        holder.SetParent(frame, false);
        holder.localPosition = localPos;
        holder.localRotation = localRot;
        var go = TripoTierModel(holder, id, matId ?? id, TripoSpecs[id].roll);
        if (go == null) { Object.DestroyImmediate(holder.gameObject); return null; }
        var rs = go.transform.Find("LOD0_q3").GetComponentsInChildren<MeshRenderer>();
        var nb = TripoLocalBounds(holder, rs);
        go.transform.localScale = Vector3.one * (nb.size.y > 1e-4f ? height / nb.size.y : 1f);
        var fb = TripoLocalBounds(frame, rs);
        holder.localPosition += localPos - new Vector3(fb.center.x, fb.min.y, fb.center.z);
        var lg = go.GetComponent<LODGroup>(); if (lg != null) lg.RecalculateBounds();
        foreach (var c in holder.GetComponentsInChildren<Transform>())
            GameObjectUtility.SetStaticEditorFlags(c.gameObject, StaticEditorFlags.OccludeeStatic);
        return holder;
    }

    // 품질 4단계 모델을 frame 기준 상자 box에 축마다 맞춰 놓는다 (가운데 맞춤). yaw: 모델을 위 축으로 돌려 놓는 각도 (0 또는 180: 이어 놓은 장이 똑같아 보이지 않게).
    // over: 축마다 더 곱하는 배율 (1보다 크면 모델이 상자 밖으로 그만큼 나온다). 난수를 쓰지 않는다
    static Transform TcBoxFit(Transform frame, string id, string matId, Bounds box, float yaw, Vector3 over)
    {
        var holder = new GameObject("Tripo_" + id).transform;
        holder.SetParent(frame, false);
        holder.localPosition = box.center;
        holder.localRotation = Quaternion.Euler(0f, yaw, 0f);
        var go = TripoTierModel(holder, id, matId ?? id, TripoSpecs[id].roll);
        if (go == null) { Object.DestroyImmediate(holder.gameObject); return null; }
        var rs = go.transform.Find("LOD0_q3").GetComponentsInChildren<MeshRenderer>();
        var nb = TripoLocalBounds(holder, rs);
        var sv = Vector3.one;
        for (int a = 0; a < 3; a++) sv[a] = (nb.size[a] > 1e-4f ? box.size[a] / nb.size[a] : 1f) * over[a];
        go.transform.localScale = sv;
        nb = TripoLocalBounds(holder, rs);
        go.transform.localPosition -= nb.center;
        var lg = go.GetComponent<LODGroup>(); if (lg != null) lg.RecalculateBounds();
        foreach (var c in holder.GetComponentsInChildren<Transform>())
            GameObjectUtility.SetStaticEditorFlags(c.gameObject, StaticEditorFlags.OccludeeStatic);
        return holder;
    }

    // 물탱크: 모델 밑 배수 꼭지가 +z로 나와 있어(전체 z 0.858, 몸통 지름 0.797) 상자에 맞추면 몸통이 7 % 눌린 타원이 되고 쓰러진 탱크는 바닥에서 12 cm 뜬다 (11-26 검토).
    // z 배율을 x와 같게 해 몸통을 둥글게 하고, 몸통 뒷면(-z)을 예전 원통 뒷면에 맞춘다: 꼭지만 예전 원통 밖으로 나온다. ob = 받침 기준 예전 겉모습 상자
    static void TcTankRound(Transform holder, Bounds ob)
    {
        var model = holder.Find("Model"); if (model == null) return;
        var lod0 = model.Find("LOD0_q3"); if (lod0 == null) return;
        var rs = lod0.GetComponentsInChildren<MeshRenderer>();
        var sc = model.localScale; sc.z = sc.x; model.localScale = sc;
        var nb = TripoLocalBounds(holder, rs);
        model.localPosition += new Vector3(0f, 0f, ob.min.z - nb.min.z);
        var lg = model.GetComponent<LODGroup>(); if (lg != null) lg.RecalculateBounds();
    }

    // 예전 겉모습 지우기: 렌더러와 메시만 (물체·콜라이더는 남긴다. TripoSwapRenderers와 같은 방식)
    static void TcStrip(List<Renderer> old)
    {
        foreach (var r in old)
        {
            if (r == null) continue;
            var mf = r.GetComponent<MeshFilter>();
            Object.DestroyImmediate(r);
            if (mf != null) Object.DestroyImmediate(mf);
        }
    }
}
