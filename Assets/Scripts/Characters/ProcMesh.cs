using System.Collections.Generic;
using UnityEngine;

// 실행 중에 사람·동물 몸 부품 메시를 만든다.
// 단면 고리를 아래에서 위로 이어 붙인 매끈한 관 모양 하나로 팔·다리·몸통·머리·옷·가죽판을 모두 만든다.
// 같은 모양은 이름(key)으로 한 번만 만들어 함께 쓴다
public static class ProcMesh
{
    static readonly Dictionary<string, Mesh> cache = new Dictionary<string, Mesh>();
    static readonly Dictionary<string, Mesh> baked = new Dictionary<string, Mesh>();

    // rings: 고리마다 (높이 y, 가로 반지름 rx, 앞뒤 반지름 rz, 앞쪽 치우침 z) 4개씩.
    // round: 2면 타원 단면, 클수록 모서리가 각진 둥근 사각형 (가죽판·신발 밑창 등)
    public static Mesh Tube(string key, float round, int radial, params float[] rings)
    {
        if (cache.TryGetValue(key, out var m) && m != null) return m;
        int n = rings.Length / 4;
        // 아래(y가 작은 쪽)부터 위로 정렬한다
        var order = new List<int>(n);
        for (int i = 0; i < n; i++) order.Add(i);
        order.Sort((a, b) => rings[a * 4].CompareTo(rings[b * 4]));

        int cols = radial + 1;
        var verts = new Vector3[n * cols + 2];
        var uvs = new Vector2[verts.Length];
        float maxR = 0;
        for (int i = 0; i < n; i++) maxR = Mathf.Max(maxR, (rings[order[i] * 4 + 1] + rings[order[i] * 4 + 2]) * 0.5f);
        float around = 2 * Mathf.PI * maxR; // 텍스처 가로 길이(m)
        float e = 2f / Mathf.Max(0.5f, round);

        for (int i = 0; i < n; i++)
        {
            int o = order[i] * 4;
            float y = rings[o], rx = rings[o + 1], rz = rings[o + 2], zo = rings[o + 3];
            for (int j = 0; j < cols; j++)
            {
                float a = 2 * Mathf.PI * j / radial;
                float c = Mathf.Cos(a), s = Mathf.Sin(a);
                float x = rx * Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), e);
                float z = rz * Mathf.Sign(s) * Mathf.Pow(Mathf.Abs(s), e) + zo;
                verts[i * cols + j] = new Vector3(x, y, z);
                uvs[i * cols + j] = new Vector2((float)j / radial * around, y);
            }
        }
        int bottom = n * cols, top = n * cols + 1;
        verts[bottom] = new Vector3(0, rings[order[0] * 4], rings[order[0] * 4 + 3]);
        verts[top] = new Vector3(0, rings[order[n - 1] * 4], rings[order[n - 1] * 4 + 3]);
        uvs[bottom] = new Vector2(around * 0.5f, verts[bottom].y);
        uvs[top] = new Vector2(around * 0.5f, verts[top].y);

        var tris = new List<int>((n - 1) * radial * 6 + radial * 6);
        for (int i = 0; i < n - 1; i++)
            for (int j = 0; j < radial; j++)
            {
                int a = i * cols + j, b = a + 1, c = a + cols, d = c + 1;
                tris.Add(a); tris.Add(c); tris.Add(b);
                tris.Add(b); tris.Add(c); tris.Add(d);
            }
        for (int j = 0; j < radial; j++)
        {
            tris.Add(bottom); tris.Add(j); tris.Add(j + 1);
            int t = (n - 1) * cols + j;
            tris.Add(top); tris.Add(t + 1); tris.Add(t);
        }

        m = new Mesh { name = key };
        m.vertices = verts;
        m.uv = uvs;
        m.SetTriangles(tris, 0);
        m.RecalculateNormals();
        // 바깥을 보게 감는다 (첫 고리의 한 점에서 법선이 축 바깥을 향하는지 본다)
        int probe = Mathf.Min(n - 1, n / 2) * cols + radial / 4;
        var nrm = m.normals;
        var outward = verts[probe] - new Vector3(0, verts[probe].y, rings[order[Mathf.Min(n - 1, n / 2)] * 4 + 3]);
        if (Vector3.Dot(nrm[probe], outward) < 0)
        {
            for (int t = 0; t < tris.Count; t += 3) { int k = tris[t + 1]; tris[t + 1] = tris[t + 2]; tris[t + 2] = k; }
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            nrm = m.normals;
        }
        // 이음매(첫 줄과 마지막 줄은 같은 자리)의 법선을 맞춰 줄무늬가 생기지 않게 한다
        for (int i = 0; i < n; i++)
        {
            int a = i * cols, b = a + radial;
            var avg = (nrm[a] + nrm[b]).normalized;
            nrm[a] = nrm[b] = avg;
        }
        m.normals = nrm;
        m.RecalculateTangents();
        m.RecalculateBounds();
        cache[key] = m;
        return m;
    }

    // 타원체 (눈·코·귀·근육 덩이·돌기 등). rings가 많을수록 둥글다
    public static Mesh Ellipsoid(string key, float rx, float ry, float rz, int radial = 12, int rings = 8)
    {
        if (cache.TryGetValue(key, out var m) && m != null) return m;
        var p = new float[(rings + 1) * 4];
        for (int i = 0; i <= rings; i++)
        {
            float a = Mathf.PI * i / rings - Mathf.PI / 2;
            float k = Mathf.Max(0.02f, Mathf.Cos(a));
            p[i * 4] = Mathf.Sin(a) * ry;
            p[i * 4 + 1] = rx * k;
            p[i * 4 + 2] = rz * k;
        }
        return Tube(key, 2, radial, p);
    }

    // 원뿔 (뼈 돌기·발톱·이빨·귀). 밑면 반지름 r, 높이 h, 위로 뾰족
    public static Mesh Cone(string key, float r, float h, int radial = 6) =>
        Tube(key, 2, radial, 0, r, r, 0, h * 0.5f, r * 0.55f, r * 0.55f, 0, h, r * 0.04f, r * 0.04f, 0);

    // 둥근 상자 (가죽판·띠·버클·주머니). 크기는 전체 폭·높이·두께
    public static Mesh Box(string key, float w, float h, float d, float round = 6) =>
        Tube(key, round, 16, -h / 2, w / 2 * 0.92f, d / 2 * 0.92f, 0, -h / 2 + h * 0.06f, w / 2, d / 2, 0, h / 2 - h * 0.06f, w / 2, d / 2, 0, h / 2, w / 2 * 0.92f, d / 2 * 0.92f, 0);

    // 한 관절(joint)에 붙은 부품 중 재질이 같은 것을 한 메시로 합친다 (그리기 호출 수를 줄인다).
    // cacheKey가 같으면 같은 결과 메시를 함께 쓴다. 떼어 낼 부품(keep)은 합치지 않는다
    public static void Merge(Transform joint, string cacheKey, ICollection<Transform> keep)
    {
        var groups = new Dictionary<Material, List<MeshFilter>>();
        foreach (Transform child in joint)
        {
            if (keep != null && keep.Contains(child)) continue;
            var mf = child.GetComponent<MeshFilter>();
            var mr = child.GetComponent<MeshRenderer>();
            if (mf == null || mr == null || mf.sharedMesh == null || child.childCount > 0) continue;
            if (!groups.TryGetValue(mr.sharedMaterial, out var list)) groups[mr.sharedMaterial] = list = new List<MeshFilter>();
            list.Add(mf);
        }
        foreach (var kv in groups)
        {
            if (kv.Value.Count < 2) continue;
            string key = cacheKey + "/" + joint.name + "/" + kv.Key.name + "/" + kv.Value.Count;
            if (!baked.TryGetValue(key, out var mesh) || mesh == null)
            {
                var combine = new CombineInstance[kv.Value.Count];
                for (int i = 0; i < combine.Length; i++)
                {
                    var t = kv.Value[i].transform;
                    combine[i].mesh = kv.Value[i].sharedMesh;
                    combine[i].transform = Matrix4x4.TRS(t.localPosition, t.localRotation, t.localScale);
                }
                mesh = new Mesh { name = key };
                mesh.CombineMeshes(combine, true, true);
                mesh.RecalculateBounds();
                baked[key] = mesh;
            }
            var go = new GameObject(kv.Key.name);
            go.transform.SetParent(joint, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = kv.Key;
            foreach (var mf in kv.Value)
            {
                mf.gameObject.SetActive(false);
                Object.Destroy(mf.gameObject);
            }
        }
    }
}
