using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 트럭 정비소의 고장 난 픽업트럭(수리 목표, TruckSite)을 Tripo 트럭 품질 4단계로 (2026-11-23, 사용자 "트럭 도시에 넣기").
// 코드 트럭을 예전처럼 다 만든 뒤(난수 그대로) 몸통 겉모습만 지우고 Tripo 트럭 LOD 묶음을 놓는다. 콜라이더·TruckSite·받침목·차축·브레이크 드럼·
// 프레임·바퀴(수리 부품)·연료통 깔판은 그대로. 수리 부품 중 몸통에 붙는 것(엔진 칸 부품·배터리·배선·내비·덧댄 철판·전조등·짐칸 상자)은
// Tripo 몸통 표면을 향해 빛을 쏘아(임시 메시 콜라이더) 그 자리로 옮긴다.
// 크기: 바퀴 자리(펜더 아치) 두 개의 가운데 사이를 코드 바퀴 간격 3.5 m에 맞춘다. Tripo 트럭은 길이 1 기준 아치 가운데 z -0.262 / +0.317, 아치 위 y -0.099,
// 폭 0.412 (판 q2 옆모습에서 잼, Docs/맵모델링_미리보기/12_트럭_도시넣기). 배율 3.5 / 0.579 = 6.045, 폭은 코드 트럭 폭 2.0 m에 맞춰 4.85 (모델이 넓어서 바퀴가 아치 안쪽에 박혔다)
public static partial class GameSceneBuilder
{
    const float TruckScale = 6.045f, TruckWidthScale = 4.85f;
    static readonly Vector3 TruckModelPos = new Vector3(0f, 0.099f * TruckScale + 0.95f, -0.0275f * TruckScale); // 아치 위를 0.95 m(바퀴 위 11 cm)에, 아치 가운데를 z 0에

    // 지울 겉모습: 트럭(t) 바로 아래의 이 이름들, 그리고 경첩 아래 보닛·뒷문 (짐칸 깔판의 Slat 등 다른 그룹 아래는 건드리지 않는다)
    static readonly HashSet<string> TruckBodyParts = new HashSet<string>
    {
        "CabLower", "CabUpper", "Windshield", "RearWindow", "SideWindow", "DoorHandle", "Mirror", "Fender", "Apron", "Headlight",
        "Grille", "Slat", "Bumper", "BayFloor", "HoodProp", "BedFloor", "BedSide", "Taillight", "BedFront", "RearBumper", "Rust", "Hole",
    };

    static void TripoTruck(Transform t, List<GameObject> plates, Transform bay, List<GameObject> batteries, List<GameObject> wires, Transform navi, List<GameObject> headlights, GameObject beam)
    {
        if (!kTripoCity || t == null) return;
        // 1) 예전 몸통 겉모습 지우기 (콜라이더는 남긴다)
        var kill = new List<Transform>();
        foreach (Transform c in t) if (TruckBodyParts.Contains(c.name)) kill.Add(c);
        var hood = t.Find("HoodHinge/Hood"); if (hood != null) kill.Add(hood);
        var tail = t.Find("TailgateHinge/Tailgate"); if (tail != null) kill.Add(tail);
        foreach (var k in kill)
        {
            var r = k.GetComponent<MeshRenderer>(); if (r != null) Object.DestroyImmediate(r);
            var f = k.GetComponent<MeshFilter>(); if (f != null) Object.DestroyImmediate(f);
        }

        // 2) Tripo 트럭 LOD 묶음
        var holder = new GameObject("Tripo_pickup_truck_01").transform;
        holder.SetParent(t, false);
        var model = TripoTierModel(holder, "pickup_truck_01", "pickup_truck_01", 0f);
        if (model == null) return;
        model.transform.localPosition = TruckModelPos;
        model.transform.localScale = new Vector3(TruckWidthScale, TruckScale, TruckScale);
        foreach (var tr in holder.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(tr.gameObject, StaticEditorFlags.OccludeeStatic);

        // 3) 몸통에 붙는 부품을 새 표면으로: 네 판 모두에 임시 메시 콜라이더를 달고 빛을 쏴 가장 먼저 맞는 면(= 가장 바깥/가장 위)을 쓴다.
        //    한 판(매우 높음)에만 맞추면 중간 품질(가까이서도 중간 판)에서 오른쪽 전조등이 몸통에 묻히고 배터리·철판이 5 cm 떴다 (11-23 검수)
        var cols = new List<MeshCollider>();
        foreach (Transform lod in model.transform)
        {
            var lf = lod.GetComponent<MeshFilter>(); if (lf == null || lf.sharedMesh == null) continue;
            var c = lod.gameObject.AddComponent<MeshCollider>(); c.sharedMesh = lf.sharedMesh; cols.Add(c);
        }
        Physics.SyncTransforms();
        Vector3 lastN = Vector3.zero;
        System.Func<Vector3, Vector3, float?> Hit = (localFrom, localDir) =>
        {
            var ray = new Ray(t.TransformPoint(localFrom), t.TransformDirection(localDir));
            float best = float.MaxValue; bool any = false;
            foreach (var c in cols)
            {
                RaycastHit h;
                if (c.Raycast(ray, out h, 10f) && h.distance < best) { best = h.distance; any = true; lastN = t.InverseTransformDirection(h.normal); }
            }
            if (!any) return null;
            return best / t.TransformDirection(localDir).magnitude;
        };
        var log = new List<string>();
        // 엔진 칸: 칸 가운데 바닥에 엔진 블록 바닥(그룹 기준 -0.27)이 앉게. 배선은 같은 높이만큼 함께 옮긴다
        float bayDy = 0f;
        if (bay != null)
        {
            var from = new Vector3(bay.localPosition.x, 1.5f, bay.localPosition.z);
            var d = Hit(from, Vector3.down);
            if (d.HasValue) { float floor = from.y - d.Value; bayDy = floor + 0.27f - bay.localPosition.y; bay.localPosition += Vector3.up * bayDy; log.Add($"엔진 칸 바닥 {floor:F2} m (+{bayDy:F2})"); }
        }
        foreach (var w in wires) if (w != null) w.transform.localPosition += Vector3.up * bayDy;
        // 배터리·내비: 그 자리 아래 표면 위에
        foreach (var bt in batteries)
        {
            if (bt == null) continue;
            var p = bt.transform.localPosition; var d = Hit(new Vector3(p.x, 1.5f, p.z), Vector3.down);
            if (d.HasValue) bt.transform.localPosition = new Vector3(p.x, 1.5f - d.Value, p.z);
        }
        if (navi != null)
        {
            var p = navi.localPosition; var d = Hit(new Vector3(p.x, 2.1f, p.z), Vector3.down);
            if (d.HasValue) { navi.localPosition = new Vector3(p.x, 2.1f - d.Value, p.z); log.Add($"내비 {navi.localPosition.y:F2} m"); }
        }
        // 짐칸 상자: 짐칸 바닥 위에
        var crate = t.Find("Crate");
        if (crate != null)
        {
            var p = crate.localPosition; var d = Hit(new Vector3(p.x, 2.2f, p.z), Vector3.down);
            if (d.HasValue) { crate.localPosition = new Vector3(p.x, 2.2f - d.Value + 0.2f, p.z); log.Add($"짐칸 바닥 {2.2f - d.Value:F2} m"); }
        }
        // 덧댄 철판 8장 (다 고치면 보인다): 네 모서리가 모두 몸통에 닿는 높이까지 올리고(아치에 걸리지 않게), 옆면 바깥에 그 면 기울기대로 붙인다
        int platesOn = 0, platesUp = 0;
        foreach (var pl in plates)
        {
            if (pl == null) continue;
            var p = pl.transform.localPosition; float side = Mathf.Sign(p.x);
            var pmf = pl.GetComponent<MeshFilter>(); var sz = pmf != null && pmf.sharedMesh != null ? pmf.sharedMesh.bounds.size : new Vector3(0.02f, 0.4f, 0.5f); // Box는 크기를 메시에 넣는다
            float hh = sz.y * 0.5f, hw = sz.z * 0.5f;
            float y = p.y; bool ok = false;
            for (int k = 0; k <= 8 && !ok; k++, y += 0.05f)
            {
                // 네 모서리가 모두 가운데와 8 cm 안쪽 깊이에서 옆면에 맞아야 한다 (아치 구멍으로 들어가 안쪽 벽에 맞는 것은 안 됨)
                var dc = Hit(new Vector3(side * 2.5f, y, p.z), new Vector3(-side, 0, 0));
                ok = dc.HasValue;
                foreach (var cy in new[] { -hh, hh }) foreach (var cz in new[] { -hw, hw })
                {
                    var dk = Hit(new Vector3(side * 2.5f, y + cy, p.z + cz), new Vector3(-side, 0, 0));
                    if (!dk.HasValue || !dc.HasValue || Mathf.Abs(dk.Value - dc.Value) > 0.08f) ok = false;
                }
                if (ok) break;
            }
            if (!ok) y = p.y; else if (y > p.y + 0.001f) platesUp++;
            var d = Hit(new Vector3(side * 2.5f, y, p.z), new Vector3(-side, 0, 0));
            if (!d.HasValue) continue;
            var n = lastN.normalized;
            pl.transform.localPosition = new Vector3(side * (2.5f - d.Value), y, p.z) + n * 0.012f;
            pl.transform.localRotation = Quaternion.FromToRotation(new Vector3(side, 0, 0), n) * pl.transform.localRotation;
            platesOn++;
        }
        log.Add($"철판 {platesOn}/{plates.Count}장 옆면에 (아치를 피해 올린 것 {platesUp})");
        // 전조등(다 고치면 켜짐): 렌즈 가운데(앞모습에서 잰 x ±0.68, y 1.19 둘레에서 가장 깊이 들어간 곳)를 찾아, 테두리 안에 들어가는 작은 판으로 렌즈 앞에.
        // 예전 크기(0.21 × 0.16)는 9 cm 들어간 둥근 테두리에 가장자리가 잘려 흰 조각처럼만 보였다 (11-23 검수)
        float frontZ = 0f; int hl = 0;
        foreach (var h in headlights)
        {
            if (h == null) continue;
            float side = Mathf.Sign(h.transform.localPosition.x);
            float bestZ = float.MaxValue; Vector2 c = new Vector2(side * 0.68f, 1.19f); Vector2 sum = Vector2.zero; int ns = 0;
            for (float x = 0.5f; x <= 0.86f; x += 0.02f)
                for (float yy = 1.0f; yy <= 1.26f; yy += 0.02f)
                {
                    var d = Hit(new Vector3(side * x, yy, 4.5f), Vector3.back); if (!d.HasValue) continue;
                    float z = 4.5f - d.Value;
                    if (z < 2.45f) continue; // 앞면에서 25 cm보다 깊으면 렌즈가 아니라 보닛 아래 엔진 칸이다
                    if (z < bestZ - 0.005f) { bestZ = z; sum = new Vector2(side * x, yy); ns = 1; }
                    else if (Mathf.Abs(z - bestZ) <= 0.005f) { sum += new Vector2(side * x, yy); ns++; }
                }
            if (ns > 0) c = sum / ns;
            var dz = Hit(new Vector3(c.x, c.y, 4.5f), Vector3.back);
            if (!dz.HasValue) continue;
            h.transform.localPosition = new Vector3(c.x, c.y, 4.5f - dz.Value + 0.008f);
            var hmf = h.GetComponent<MeshFilter>(); var hs = hmf != null && hmf.sharedMesh != null ? hmf.sharedMesh.bounds.size : new Vector3(0.21f, 0.16f, 0.03f);
            h.transform.localScale = new Vector3(0.13f / hs.x, 0.1f / hs.y, 0.012f / hs.z); // 0.13 × 0.1 × 0.012 m
            frontZ = Mathf.Max(frontZ, h.transform.localPosition.z); hl++;
            log.Add($"전조등 렌즈 ({c.x:F2}, {c.y:F2})");
        }
        if (beam != null && hl > 0) beam.transform.localPosition = new Vector3(0f, 1.19f, frontZ + 0.15f);
        log.Add($"전조등 {hl}/{headlights.Count}개 앞면에");
        foreach (var c in cols) Object.DestroyImmediate(c);
        Debug.Log("[GameSceneBuilder] City: Tripo 트럭 — " + string.Join(", ", log));
    }

    // 길 찾기 지도를 굽기 직전(배치가 다 끝난 뒤): Tripo 트럭의 짐칸이 코드 트럭보다 뒤로 약 0.45 m 길어 그 자리에 상자 콜라이더를 더한다
    // (코드 콜라이더는 그대로: 수리 창을 여는 대상·길 찾기 지도 모양이 예전과 같다)
    static int TripoTruckColliders(Transform root)
    {
        int n = 0;
        foreach (var holder in root.GetComponentsInChildren<Transform>(true))
        {
            if (holder.name != "Tripo_pickup_truck_01") continue;
            var rs = holder.GetComponentsInChildren<MeshRenderer>(true);
            if (rs.Length == 0) continue;
            var truck = holder.parent;
            var b = TripoLocalBounds(truck, new Renderer[] { rs[0] });
            const float codeRear = -2.81f; // 코드 트럭 뒤 범퍼 끝
            if (b.min.z >= codeRear - 0.05f) continue;
            var box = holder.gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 1.05f, (b.min.z + codeRear) * 0.5f);
            box.size = new Vector3(Mathf.Min(b.size.x, 2.0f), 0.9f, codeRear - b.min.z);
            n++;
        }
        return n;
    }
}
