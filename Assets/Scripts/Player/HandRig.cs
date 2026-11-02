using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// 1인칭 팔 하나 (ProcMesh로 짓는다): 팔꿈치(Elbow) → 야상 소매 → 손목(Wrist) → 손바닥과 다섯 손가락 관절.
// 손 좌표(Wrist): +z = 손가락 쪽, +y = 엄지 쪽, 손바닥은 -x*Side 쪽 (Side 1 = 오른손, -1 = 왼손, 왼손은 거울 모양).
// 팔뚝은 늘 손 -z 쪽으로 뻗는다. 그래서 손을 어느 쪽으로 돌려도 손목이 꺾여 보이지 않는다 (몸짓 때만 Bend로 조금 꺾는다).
// 손가락은 마디마다 굽힌 각도로 움직이고, Wrap()은 쥔 물건 표면에 닿을 때까지 손가락을 감는다.
public class HandRig
{
    public const float ForearmLength = 0.27f;
    // 자세 배열: 손가락(0 엄지, 1 검지, 2 가운데, 3 약지, 4 새끼)마다 [벌림, 뿌리 굽힘, 가운데 굽힘, 끝 굽힘] (도)
    public const int PoseLength = 20;

    public static readonly Color SkinColor = new Color(0.78f, 0.58f, 0.47f);

    public readonly float Side;
    public readonly Transform Elbow, Wrist;

    readonly Transform[][] joints = new Transform[5][];
    readonly Quaternion[] rootRest = new Quaternion[5];
    readonly float[][] lengths = new float[5][];
    readonly float[] radii = new float[5];

    // 재질은 ProcTex가 이름으로 한 번만 만들어 함께 쓴다 (장면을 옮기며 지워져도 다시 만든다)
    readonly Material skin, nail, jacket, cuff;

    // ---------- 손 모양 (자세) ----------

    // 힘을 뺀 손: 손가락이 느슨하게 굽어 있다 (빈손)
    public static readonly float[] Relaxed =
    {
        14, 14, 20, 16,
        3, 42, 58, 30,
        0, 48, 62, 32,
        -3, 54, 66, 34,
        -6, 60, 70, 36,
    };
    // 주먹: 엄지가 검지·가운데 손가락 위를 덮는다
    public static readonly float[] Fist =
    {
        30, 30, 40, 30,
        0, 88, 100, 62,
        0, 90, 102, 62,
        0, 90, 100, 60,
        0, 88, 96, 58,
    };
    // 활짝 편 손 (물건을 집기 직전)
    public static readonly float[] Open =
    {
        -6, -6, 4, 4,
        6, 6, 10, 6,
        0, 6, 10, 6,
        -5, 8, 12, 6,
        -11, 10, 12, 8,
    };

    public HandRig(Transform parent, string name, float side)
    {
        Side = side;
        Elbow = new GameObject(name).transform;
        Elbow.SetParent(parent, false);
        Wrist = new GameObject("Wrist").transform;
        Wrist.SetParent(Elbow, false);
        Wrist.localPosition = new Vector3(0, 0, ForearmLength);

        skin = ProcTex.Plain(SkinColor, 0.32f);
        nail = ProcTex.Plain(new Color(0.88f, 0.72f, 0.66f), 0.55f);
        // 야상은 따뜻한 불빛 아래서도 맨살로 보이지 않게 어둡고 푸른 국방색
        jacket = ProcTex.Get(ProcTex.Kind.Cloth, new Color(0.16f, 0.19f, 0.14f), 402, 0, 0.5f, new Color(0.1f, 0.12f, 0.09f));
        cuff = ProcTex.Get(ProcTex.Kind.Cloth, new Color(0.12f, 0.14f, 0.11f), 403, 0, 0.55f);
        BuildSleeve();
        BuildHand();
        Apply(Relaxed);
        foreach (var r in Elbow.GetComponentsInChildren<Renderer>())
            r.shadowCastingMode = ShadowCastingMode.Off;
    }

    static readonly Quaternion Along = Quaternion.Euler(90, 0, 0); // 관(+y)을 앞(+z)으로 눕힌다

    // 야상 소매: 팔꿈치 뒤에서 손목 조금 앞까지. 팔뚝 굵기가 줄며 군데군데 주름이 진다
    void BuildSleeve()
    {
        const float L = ForearmLength;
        Part(Elbow, ProcMesh.Tube("fp2_sleeve", 2, 18,
            -0.16f, 0.057f, 0.061f, 0f,
            0f, 0.055f, 0.06f, 0f,
            0.06f, 0.053f, 0.058f, 0.003f,
            0.11f, 0.05f, 0.055f, 0f,
            0.16f, 0.049f, 0.053f, 0.002f,
            L - 0.07f, 0.045f, 0.049f, 0f,
            L - 0.04f, 0.044f, 0.048f, 0f), jacket, Vector3.zero, Along);
        // 접어 올린 소맷단
        Part(Elbow, ProcMesh.Tube("fp2_cuff", 2, 18,
            L - 0.078f, 0.047f, 0.051f, 0f,
            L - 0.062f, 0.05f, 0.054f, 0f,
            L - 0.04f, 0.049f, 0.053f, 0f,
            L - 0.03f, 0.044f, 0.048f, 0f), cuff, Vector3.zero, Along);
    }

    // 손바닥·손목·손날 + 다섯 손가락
    void BuildHand()
    {
        // 손목 (소매 안에서 손바닥까지)
        Part(Wrist, ProcMesh.Tube("fp2_wrist", 2, 16,
            -0.07f, 0.021f, 0.027f, 0f,
            -0.02f, 0.019f, 0.027f, 0f,
            0.004f, 0.019f, 0.029f, 0f), skin, new Vector3(Side * 0.007f, 0, 0), Along);
        // 손등·손바닥 (손목 → 손가락 뿌리)
        Part(Wrist, ProcMesh.Tube("fp2_palm", 3, 18,
            -0.012f, 0.02f, 0.029f, 0f,
            0.02f, 0.022f, 0.036f, 0f,
            0.05f, 0.02f, 0.04f, 0f,
            0.075f, 0.017f, 0.042f, 0f,
            0.093f, 0.011f, 0.036f, 0f), skin, new Vector3(Side * 0.012f, 0, 0), Along);
        // 새끼손가락 쪽 손날의 두툼한 살
        Part(Wrist, ProcMesh.Ellipsoid("fp2_hypothenar", 0.013f, 0.014f, 0.032f), skin, new Vector3(-Side * 0.001f, -0.027f, 0.035f), Quaternion.identity);

        // 손가락 넷: 검지(위)부터 새끼(아래)로. 뿌리 마디 위치, 마디 길이, 굵기
        float[] ys = { 0.027f, 0.009f, -0.009f, -0.027f };
        float[] zs = { 0.087f, 0.091f, 0.088f, 0.081f };
        float[] scale = { 1f, 1.06f, 1f, 0.82f };
        for (int i = 0; i < 4; i++)
        {
            int f = i + 1;
            float k = scale[i];
            radii[f] = 0.0097f * (i == 3 ? 0.87f : i == 1 ? 1.03f : 1f);
            lengths[f] = new[] { 0.041f * k, 0.026f * k, 0.021f * k };
            // 손등 마디 뼈가 도드라진다
            Part(Wrist, ProcMesh.Ellipsoid("fp2_knuckle", 0.009f, 0.0095f, 0.011f), skin, new Vector3(Side * 0.02f, ys[i], zs[i] - 0.004f), Quaternion.identity);
            BuildFinger(f, new Vector3(Side * 0.011f, ys[i], zs[i]), Quaternion.identity);
        }

        // 엄지: 손목 가까운 엄지 쪽에서 앞·엄지 쪽으로 뻗고, 굽히면 손바닥을 가로질러 새끼 쪽으로 간다
        radii[0] = 0.0112f;
        lengths[0] = new[] { 0.036f, 0.031f, 0.026f };
        var dir = new Vector3(-Side * 0.3f, 0.62f, 0.72f).normalized;
        var flex = new Vector3(-Side * 0.55f, -0.8f, 0.1f);
        BuildFinger(0, new Vector3(-Side * 0.004f, 0.022f, 0.012f), Frame(dir, flex));
        // 엄지 뿌리 두툼한 살 (엄지 첫 마디에 붙어 함께 움직인다)
        Part(joints[0][0], ProcMesh.Ellipsoid("fp2_thenar", 0.015f, 0.016f, 0.026f), skin, new Vector3(-Side * 0.004f, -0.004f, 0.012f), Quaternion.identity);
    }

    // 굽힐 방향(flex)으로 마디가 굽는 좌표계: +z = 손가락 방향, 로컬 y축으로 돌면 flex 쪽으로 굽는다
    Quaternion Frame(Vector3 dir, Vector3 flex)
    {
        var x = Vector3.ProjectOnPlane(-Side * flex, dir).normalized;
        return Quaternion.LookRotation(dir, Vector3.Cross(dir, x));
    }

    void BuildFinger(int f, Vector3 root, Quaternion rest)
    {
        joints[f] = new Transform[3];
        rootRest[f] = rest;
        var parent = Wrist;
        var pos = root;
        float r = radii[f];
        for (int j = 0; j < 3; j++)
        {
            var t = new GameObject("Joint").transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            t.localRotation = j == 0 ? rest : Quaternion.identity;
            joints[f][j] = t;
            float len = lengths[f][j];
            float rr = r * (j == 0 ? 1f : j == 1 ? 0.93f : 0.86f) * (f == 0 && j == 0 ? 1.2f : 1f);
            Segment(t, len, rr);
            // 손톱: 끝 마디 등 쪽
            if (j == 2)
                Part(t, ProcMesh.Ellipsoid($"fp2_nail_{rr:0.0000}_{len:0.000}", 0.0022f, rr * 0.72f, len * 0.42f), nail, new Vector3(Side * rr * 0.82f, 0, len * 0.58f), Quaternion.identity);
            parent = t;
            pos = new Vector3(0, 0, len);
        }
    }

    // 손가락 한 마디 (+z로 길이 len, 끝이 둥글다)
    void Segment(Transform joint, float len, float r)
    {
        var mesh = ProcMesh.Tube($"fp2_seg_{len:0.000}_{r:0.0000}", 2, 12,
            -r * 0.55f, r * 0.72f, r * 0.72f, 0f,
            -r * 0.2f, r, r, 0f,
            len * 0.5f, r * 0.96f, r * 0.9f, 0f,
            len, r * 0.92f, r * 0.88f, 0f,
            len + r * 0.6f, r * 0.55f, r * 0.55f, 0f);
        Part(joint, mesh, skin, Vector3.zero, Along);
    }

    static Transform Part(Transform parent, Mesh mesh, Material mat, Vector3 pos, Quaternion rot)
    {
        var go = new GameObject(mesh.name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = rot;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = ShadowCastingMode.Off;
        return go.transform;
    }

    // ---------- 팔 놓기 ----------

    // 손목 자리(손 좌표의 원점)와 손 방향(rotation), 손목 꺾임(bend)으로 팔 전체를 놓는다. 팔꿈치는 손 뒤쪽 팔뚝 길이만큼
    public void Place(Vector3 wrist, Quaternion rotation, Quaternion bend)
    {
        Elbow.localRotation = rotation;
        Elbow.localPosition = wrist - rotation * new Vector3(0, 0, ForearmLength);
        Wrist.localRotation = bend;
    }

    // 손 좌표 (fingers = 손가락 방향, thumb = 엄지 방향)를 부모 좌표 방향으로
    public static Quaternion Look(Vector3 fingers, Vector3 thumb) => Quaternion.LookRotation(fingers, thumb);

    // ---------- 손가락 ----------

    public void Apply(float[] pose)
    {
        for (int f = 0; f < 5; f++)
            ApplyFinger(f, pose[f * 4], pose[f * 4 + 1], pose[f * 4 + 2], pose[f * 4 + 3]);
    }

    void ApplyFinger(int f, float spread, float a, float b, float c)
    {
        var j = joints[f];
        j[0].localRotation = rootRest[f] * Quaternion.AngleAxis(-spread, Vector3.right) * Quaternion.AngleAxis(-Side * a, Vector3.up);
        j[1].localRotation = Quaternion.AngleAxis(-Side * b, Vector3.up);
        j[2].localRotation = Quaternion.AngleAxis(-Side * c, Vector3.up);
    }

    public static void Blend(float[] from, float[] to, float t, float[] into)
    {
        for (int i = 0; i < PoseLength; i++) into[i] = Mathf.LerpUnclamped(from[i], to[i], t);
    }

    // 손가락(mask의 비트, 1 << 손가락)을 start 자세에서 max 쪽으로 함께 굽히다가, 마디가 물건에 닿으면 그 마디까지 멈추고 나머지 끝마디만 더 감는다.
    // 결과 자세를 pose에 쓴다. shapes는 쥔 물건의 모양 (ShapesOf)
    public void Wrap(float[] pose, float[] max, List<Shape> shapes, int mask, int steps = 44)
    {
        for (int f = 0; f < 5; f++)
            if ((mask & (1 << f)) != 0) WrapFinger(f, pose, max, shapes, steps);
        Apply(pose);
    }

    void WrapFinger(int f, float[] pose, float[] max, List<Shape> shapes, int steps)
    {
        int o = f * 4;
        var start = new float[3];
        var cur = new float[3];
        var prev = new float[3];
        var frozen = new bool[3];
        for (int j = 0; j < 3; j++) start[j] = cur[j] = pose[o + 1 + j];
        ApplyFinger(f, pose[o], cur[0], cur[1], cur[2]);
        int hit = FirstHit(f, shapes);
        for (int j = 0; j <= hit; j++) frozen[j] = true;
        for (int s = 1; s <= steps && !(frozen[0] && frozen[1] && frozen[2]); s++)
        {
            float t = (float)s / steps;
            for (int j = 0; j < 3; j++)
            {
                prev[j] = cur[j];
                if (!frozen[j]) cur[j] = Mathf.Lerp(start[j], max[o + 1 + j], t);
            }
            ApplyFinger(f, pose[o], cur[0], cur[1], cur[2]);
            hit = FirstHit(f, shapes);
            if (hit < 0) continue;
            for (int j = 0; j <= hit; j++)
                if (!frozen[j])
                {
                    cur[j] = prev[j];
                    frozen[j] = true;
                }
            ApplyFinger(f, pose[o], cur[0], cur[1], cur[2]);
        }
        for (int j = 0; j < 3; j++) pose[o + 1 + j] = cur[j];
    }

    static readonly float[] Samples = { 0.3f, 0.65f, 1f };

    // 손가락 f에서 물건에 닿은 가장 뿌리 쪽 마디 (없으면 -1)
    int FirstHit(int f, List<Shape> shapes)
    {
        float margin = radii[f] * 0.85f;
        for (int j = 0; j < 3; j++)
        {
            var t = joints[f][j];
            float len = lengths[f][j] + (j == 2 ? radii[f] * 0.35f : 0);
            foreach (var k in Samples)
                if (Hits(shapes, t.TransformPoint(0, 0, len * k), margin)) return j;
        }
        return -1;
    }

    // ---------- 물건 모양 (손가락이 닿는지 재는 데 쓴다) ----------

    public struct Shape
    {
        public Transform T;
        public byte Kind; // 0 상자, 1 원통, 2 구·캡슐
        public Vector3 Center, Extents;
    }

    // 물건을 이루는 기본 도형들 (상자·원통·구). 다른 메시는 경계 상자로 본다
    public static List<Shape> ShapesOf(Transform root)
    {
        var list = new List<Shape>();
        foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
        {
            var m = mf.sharedMesh;
            if (m == null || !mf.GetComponent<Renderer>().enabled) continue;
            var s = new Shape { T = mf.transform };
            switch (m.name)
            {
                case "Cube": s.Kind = 0; s.Extents = Vector3.one * 0.5f; break;
                case "Cylinder": s.Kind = 1; s.Extents = new Vector3(0.5f, 1f, 0.5f); break;
                case "Sphere": s.Kind = 2; s.Extents = Vector3.one * 0.5f; break;
                case "Capsule": s.Kind = 2; s.Extents = new Vector3(0.5f, 1f, 0.5f); break;
                default: s.Kind = 0; s.Center = m.bounds.center; s.Extents = m.bounds.extents; break;
            }
            list.Add(s);
        }
        return list;
    }

    public static bool Hits(List<Shape> shapes, Vector3 world, float margin)
    {
        foreach (var s in shapes)
        {
            var p = s.T.InverseTransformPoint(world) - s.Center;
            var sc = s.T.lossyScale;
            float ex = s.Extents.x + margin / Mathf.Max(1e-4f, Mathf.Abs(sc.x));
            float ey = s.Extents.y + margin / Mathf.Max(1e-4f, Mathf.Abs(sc.y));
            float ez = s.Extents.z + margin / Mathf.Max(1e-4f, Mathf.Abs(sc.z));
            switch (s.Kind)
            {
                case 0:
                    if (Mathf.Abs(p.x) <= ex && Mathf.Abs(p.y) <= ey && Mathf.Abs(p.z) <= ez) return true;
                    break;
                case 1:
                    if (Mathf.Abs(p.y) <= ey && (p.x * p.x) / (ex * ex) + (p.z * p.z) / (ez * ez) <= 1f) return true;
                    break;
                default:
                    if ((p.x * p.x) / (ex * ex) + (p.y * p.y) / (ey * ey) + (p.z * p.z) / (ez * ez) <= 1f) return true;
                    break;
            }
        }
        return false;
    }
}
