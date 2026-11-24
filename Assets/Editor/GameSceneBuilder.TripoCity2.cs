using System.Collections.Generic;
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
            if (isBox && (t.name == "Chunk" || t.name == "Rubble" || t.name == "RoofChunk"))
            {
                int h = Mathf.Abs(Mathf.RoundToInt(t.position.x * 7.31f) * 31 + Mathf.RoundToInt(t.position.z * 5.17f) * 17 + Mathf.RoundToInt(t.position.y * 3.3f)) % 2;
                float flat = size.y / Mathf.Max(1e-3f, Mathf.Min(size.x, size.z));
                if (mat == M["brick"]) id = "city_rubble_brick_01";
                else if (mat == M["asphalt"]) id = "city_rubble_asphalt_01";
                else if (mat == M["cracked"]) id = flat < 0.35f ? "city_rubble_slab_01" : "city_rubble_concrete_01";
                else if (mat == M["aptWall"]) id = flat < 0.3f ? "city_rubble_slab_01" : "city_rubble_wall_01";
                else if (mat == M["dirty"]) id = flat < 0.3f ? "city_rubble_slab_01" : h == 0 ? "city_rubble_wall_01" : "city_rubble_concrete_01";
            }
            else if (isBox && t.name == "Crate") id = "city_crate_01";
            // 쓰러진 방호벽(Z 80°)은 예전 상자 그대로 둔다: 모델을 상자 축에 맞추면 길쭉한 면을 세워 땅에 반쯤 묻힌 모습이 된다 (11-24 검토)
            else if (isBox && t.name == "Barrier" && Vector3.Dot(t.up, Vector3.up) > 0.9f && Mathf.Abs(size.x - 2f) < 0.05f && Mathf.Abs(size.y - 0.9f) < 0.05f) id = "city_barrier_01";
            else if (isBox && t.name.StartsWith("Sandbag") && t.name.Length == 8) id = "city_sandbag_01";
            if (id != null)
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
                    add(id);
                    if (id == "city_rubble_concrete_01") TcFillRebarBody(holder.transform);
                }
                else if (target != t) Object.DestroyImmediate(target.gameObject);
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
}
