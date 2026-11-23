using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 방공호 가구 6종의 겉모습을 Tripo 모델(품질 4단계 LOD)로 (2026-11-23, 사용자 "1차 6개로" → 품질 4단계 통과 → "방공호에 넣기").
// 침대·창고 몸통·선반·탁자·의자·제작대. 계획과 조사: Docs/맵모델링_미리보기/13_방공호_가구/계획.md
// 원칙 (도시 소품과 같다): 코드 가구를 예전처럼 다 만든 뒤 겉모습 렌더러만 지운다. 콜라이더·상호작용 컴포넌트·이름·자리는 그대로라
// 길 찾기 지도(ShelterNavMesh)·좀비 시야·[F] 대상이 바뀌지 않는다. 방공호 난수(씨앗 7, 선반 장식만 뽑는다)도 그대로.
// 남기는 코드 겉모습: 창고 문짝(경첩이 돈다), 제작대 망치(휘두른다), 선반 장식 깡통·상자(난수 자리, 모델 판자 위로 높이만 옮긴다),
// 탁자 위 지도·컵·라디오(라디오는 빛나는 다이얼·바늘·스피커가 있다). 발전기·화로·라디오는 바꾸지 않는다
public static partial class GameSceneBuilder
{
    // 방공호 씬을 만드는 동안만 켠다: 선반(BuildShelf)은 도시 건물 안에서도 쓰여, 도시 선반은 예전 모습 그대로 둔다
    static bool kTripoShelter;

    // 방공호 씬 만들기 (게임 씬 전체 생성·방공호 씬만 생성 둘 다 이것을 부른다). 본체는 GameSceneBuilder.Shelter.cs의 BuildShelterSceneCore
    static void BuildShelterScene(GameObject playerPrefab, GameObject zombiePrefab)
    {
        kTripoShelter = true;
        try { BuildShelterSceneCore(playerPrefab, zombiePrefab); }
        finally { kTripoShelter = false; }
    }

    // target 아래 이름이 names 중 하나인 물체의 렌더러 (자식 포함)
    static List<Renderer> TsRenderers(Transform target, params string[] names)
    {
        var set = new HashSet<string>(names); var l = new List<Renderer>();
        foreach (var t in target.GetComponentsInChildren<Transform>(true))
            if (set.Contains(t.name)) l.AddRange(t.GetComponentsInChildren<Renderer>());
        return l;
    }

    // 침대: 틀·다리·머리판·매트리스·베개·담요 전부. 길이(2 m)로 맞춘다 (머리판 = +Z, 모델도 머리판 +Z로 돌려 두었다)
    static void TripoShelterBed(Transform g)
    {
        if (!kTripoShelter) return;
        TripoSwapRenderers(g, "shelter_bed_01", TsRenderers(g, "Frame", "Leg", "HeadBoard", "Mattress", "Pillow", "Blanket"), TripoFit.Length, TripoAlign.Bottom, 0f, null, Vector3.zero);
    }

    // 제작대: 망치(HammerHandle·HammerHead)만 빼고 전부. 가로·깊이는 예전 크기, 높이는 모델 상판이 예전 상판 높이(0.94 m)에 오게
    // (실행 중 나사·걸레·쇳가루·망치가 0.94에 놓인다, Workbench.cs). 공구판(Pegboard) 물체는 남는다: 실행 중 그 자리에 콜라이더를 단다
    static void TripoShelterWorkbench(Transform g)
    {
        if (!kTripoShelter) return;
        if (AssetDatabase.LoadAssetAtPath<GameObject>($"{TripoModelDir}/{WbId}/{LpTierDir[3]}/{WbId}_q3.fbx") == null)
        {
            Debug.LogWarning("[GameSceneBuilder] Tripo 제작대 원본이 없어 코드 제작대 그대로: " + WbId);
            return;
        }
        var src = WbSource(); var d = WbMeasure(src); Object.DestroyImmediate(src.tex);
        float h = d.topY - d.all.min.y;
        var holder = TripoSwapRenderers(g, "shelter_workbench_01", TsRenderers(g, "Top", "Leg", "Shelf", "Pegboard", "Vise", "Wrench", "Saw", "ToolBox", "Scrap"),
            TripoFit.Box, TripoAlign.Bottom, 0f, null, new Vector3(0f, h > 1e-3f ? 0.94f / h : 0f, 0f));
        if (holder != null) TsFitWorkbenchTop(g, holder.transform.Find("Model"));
    }

    // 넣은 뒤 상판을 직접 재서 맞춘다: 윗면 0.94 m (실행 중 너트·나사·걸레가 0.94에 놓인다), 앞 모서리 z -0.4 · 뒤(공구판 뒷면) +0.4 (코드 콜라이더 2.2×1×0.8과 같게).
    // 축마다 맞추기(Box)는 앞으로 튀어나온 바이스까지 깊이에 넣어 상판 앞 모서리가 -0.27에 와서 앞쪽 너트가 허공에 떴고, 윗면은 0.93이었다 (11-23 확인)
    static void TsFitWorkbenchTop(Transform g, Transform model)
    {
        var lod = model != null ? model.Find("LOD0_q3") : null; if (lod == null) return;
        var r = lod.GetComponent<MeshRenderer>();
        var mc = lod.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = lod.GetComponent<MeshFilter>().sharedMesh;
        System.Func<float, float, float> Down = (x, z) =>
        {
            Physics.SyncTransforms(); RaycastHit hit;
            return mc.Raycast(new Ray(g.TransformPoint(new Vector3(x, 2f, z)), -g.up), out hit, 3f) ? g.InverseTransformPoint(hit.point).y : -1f;
        };
        for (int pass = 0; pass < 2; pass++)
        {
            // 높이: 바닥은 그대로 0, 상판 가운데 윗면을 0.94로
            var b = TripoLocalBounds(g, new[] { r });
            float top = Down(0.1f, 0f);
            if (top > 0.3f)
            {
                var s = model.localScale; var p = model.localPosition;
                float k = (0.94f - b.min.y) / (top - b.min.y);
                float m0 = (b.min.y - p.y) / s.y; s.y *= k; p.y = b.min.y - s.y * m0;
                model.localScale = s; model.localPosition = p;
            }
            // 깊이: 상판 앞 모서리 (가운데에서 앞으로 1 cm씩 쏴 상판에 맞는 마지막 자리) ~ 뒤 끝
            b = TripoLocalBounds(g, new[] { r });
            float front = 0f;
            for (float z = 0f; z > -0.7f; z -= 0.01f) { float y = Down(0.1f, z); if (y < 0.85f) break; front = z; }
            float back = b.max.z;
            if (back - front > 0.2f)
            {
                var s = model.localScale; var p = model.localPosition;
                float mf = (front - p.z) / s.z; s.z *= 0.8f / (back - front); p.z = -0.4f - s.z * mf;
                model.localScale = s; model.localPosition = p;
            }
        }
        Debug.Log($"[GameSceneBuilder] Shelter: Tripo 제작대 상판 윗면 {Down(0.1f, 0f):F3} m, 앞 모서리 둘레 {Down(0.1f, -0.39f):F3}");
        Object.DestroyImmediate(mc);
    }

    // 창고(양문 수납장): 몸통·선반 세 칸·보관품. 문짝 둘(DoorL·DoorR, 경첩 축이 돈다)은 코드로 남기고 문짝 판 색을 모델(붉게 녹슨 칠)에 맞춘다
    static void TripoShelterStorage(Transform g)
    {
        if (!kTripoShelter) return;
        var old = new List<Renderer>();
        var doorL = g.Find("DoorL"); var doorR = g.Find("DoorR");
        foreach (var r in g.GetComponentsInChildren<Renderer>())
            if ((doorL == null || !r.transform.IsChildOf(doorL)) && (doorR == null || !r.transform.IsChildOf(doorR))) old.Add(r);
        TripoSwapRenderers(g, "shelter_cabinet_01", old, TripoFit.Box, TripoAlign.Bottom, 0f, null, Vector3.zero);
        if (!M.ContainsKey("cabinetDoor")) M["cabinetDoor"] = TexMat("M_ShelterCabinetDoor", "rusty_metal_02", new Color(0.7f, 0.36f, 0.28f), 0.3f, 0.35f); // 0.42·0.2·0.16은 어두운 방에서 밋밋한 검붉은 덩어리였다 (11-23 미리보기)
        // 문짝 판과 아래 띠만 (자식인 통풍 살·손잡이·리벳은 어두운 색 그대로)
        foreach (var t in g.GetComponentsInChildren<Transform>())
            if (t.name == "Leaf" || t.name == "Kick") { var r = t.GetComponent<Renderer>(); if (r != null) r.sharedMaterial = M["cabinetDoor"]; }
    }

    // 선반 (방공호 둘): 옆판·판자 넷·뒤판. 장식(깡통·상자)은 난수로 놓인 코드 물체 그대로 두고, 높이만 바로 아래 모델 판자 위로 옮긴다
    // (모델 판자는 다섯 장이라 코드 판자 0.1·0.6·1.1·1.6 m와 높이가 다르다). 판자 콜라이더는 그대로
    static void TripoShelterShelf(Transform g)
    {
        if (!kTripoShelter) return;
        var holder = TripoSwapRenderers(g, "shelter_shelf_01", TsRenderers(g, "SideL", "SideR", "Board", "Back"), TripoFit.Box, TripoAlign.Bottom, 0f, null, Vector3.zero);
        if (holder == null) return;
        var tops = TsBoardTops(g, holder.transform.Find("Model/LOD0_q3"));
        // 판자 콜라이더(코드 판자 넷, 윗면 0.115·0.615·1.115·1.615 m)를 가장 가까운 모델 판자 윗면으로 (판자마다 다른 판자, 두께 3 cm 그대로).
        // 보이는 판자와 보이지 않는 판자가 따로 있으면 떨어진 물건이 허공에 걸린다 (11-23 검토)
        var used = new HashSet<int>();
        foreach (Transform c in g)
        {
            if (c.name != "Board" || c.GetComponent<Collider>() == null) continue;
            float top = c.localPosition.y + 0.015f; int bi = -1;
            for (int i = 0; i < tops.Count; i++) if (!used.Contains(i) && (bi < 0 || Mathf.Abs(tops[i] - top) < Mathf.Abs(tops[bi] - top))) bi = i;
            if (bi < 0) continue;
            used.Add(bi);
            c.localPosition = new Vector3(c.localPosition.x, tops[bi] - 0.015f, c.localPosition.z);
        }
        int moved = 0;
        foreach (Transform c in g)
        {
            if (c.name != "Can" && c.name != "Box") continue;
            var r = c.GetComponent<Renderer>(); if (r == null) continue;
            var b = TripoLocalBounds(g, new[] { r });
            float best = float.MinValue;
            foreach (var y in tops) if (y <= b.min.y + 0.12f && y > best) best = y;
            if (best == float.MinValue) continue;
            c.localPosition += Vector3.up * (best + 0.003f - b.min.y); moved++;
        }
        Debug.Log($"[GameSceneBuilder] Shelter: Tripo 선반 판자 높이 {string.Join(", ", tops.ConvertAll(y => y.ToString("F2")))} — 장식 {moved}개를 판자 위로");
    }

    // 렌더러 메시에서 위를 보는 넓은 면이 모인 높이 (판자 윗면들, target 기준). 1 cm 칸마다 넓이를 모아 가장 넓은 칸의 15 %를 넘는 봉우리
    static List<float> TsBoardTops(Transform target, Transform lod)
    {
        var res = new List<float>();
        if (lod == null) return res;
        var mf = lod.GetComponent<MeshFilter>(); if (mf == null) return res;
        var mesh = mf.sharedMesh; var v = mesh.vertices; var t = mesh.triangles;
        var area = new Dictionary<int, float>(); float maxA = 0f;
        for (int k = 0; k < t.Length; k += 3)
        {
            Vector3 a = target.InverseTransformPoint(lod.TransformPoint(v[t[k]])), b = target.InverseTransformPoint(lod.TransformPoint(v[t[k + 1]])), c = target.InverseTransformPoint(lod.TransformPoint(v[t[k + 2]]));
            var n = Vector3.Cross(b - a, c - a); float ar = n.magnitude * 0.5f; if (ar < 1e-10f || n.y / (ar * 2f) < 0.9f) continue;
            int bin = Mathf.FloorToInt((a.y + b.y + c.y) / 3f * 100f);
            area.TryGetValue(bin, out float s); s += ar; area[bin] = s; maxA = Mathf.Max(maxA, s);
        }
        foreach (var kv in area)
        {
            if (kv.Value < maxA * 0.15f) continue;
            float l = 0f, r2 = 0f; area.TryGetValue(kv.Key - 1, out l); area.TryGetValue(kv.Key + 1, out r2);
            if (kv.Value >= l && kv.Value >= r2) res.Add((kv.Key + 0.5f) / 100f);
        }
        res.Sort();
        return res;
    }

    // 탁자: 상판·다리 넷. 지도·컵·라디오·의자는 그대로 (라디오가 0.79 m 상판 위에 앉는다: 축마다 맞추면 모델 상판도 0.79)
    // 의자: 의자 묶음(Chair) 전부를 높이로 맞춘다
    static void TripoShelterTable(Transform g)
    {
        if (!kTripoShelter) return;
        var old = new List<Renderer>();
        foreach (Transform c in g) if (c.name == "Top" || c.name == "Leg") old.AddRange(c.GetComponents<Renderer>());
        TripoSwapRenderers(g, "shelter_table_01", old, TripoFit.Box, TripoAlign.Bottom, 0f, null, Vector3.zero);
        var chair = g.Find("Chair");
        if (chair != null) TripoSwapRenderers(chair, "shelter_chair_01", new List<Renderer>(chair.GetComponentsInChildren<Renderer>()), TripoFit.Height, TripoAlign.Bottom, 0f, null, Vector3.zero);
    }
}
