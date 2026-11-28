using System.Collections.Generic;
using UnityEngine;

// 좀비 종류별 겉모습: CharacterModels로 몸을 짓고(디자인 시트), 원거리 좀비는 새총, 약탈자는 못 박힌 야구방망이를 쥐여 준다.
// 새총 고무줄(Slingshot)과 새총 돌 재질도 여기 있다
public static class ZombieLooks
{
    static readonly Dictionary<PrimitiveType, Mesh> meshes = new Dictionary<PrimitiveType, Mesh>();

    // 원거리 좀비가 새총으로 쏘는 돌멩이 재질
    public static Material Stone => RuntimeVisuals.Mat(new Color(0.42f, 0.41f, 0.38f), 0.15f);

    // 몸을 새로 짓는다. 원거리 좀비면 slingshot에 새총, 약탈자면 weapon에 야구방망이를 돌려준다
    public static CharacterRig Apply(Zombie z, ZombieKind kind, out Slingshot slingshot, out Transform weapon)
    {
        slingshot = null;
        weapon = null;
        FigureKind figure;
        int variant = 0;
        switch (kind)
        {
            case ZombieKind.Leather: figure = FigureKind.Leather; break;
            case ZombieKind.Ranged: figure = FigureKind.Slingshot; break;
            case ZombieKind.Heavy: figure = FigureKind.Giant; break;
            case ZombieKind.Raider: figure = FigureKind.Raider; break;
            default: figure = FigureKind.Zombie; variant = Random.Range(0, 4); break;
        }
        var rig = CharacterModels.Build(z.transform, figure, variant);
        if (kind == ZombieKind.Ranged) slingshot = BuildSlingshot(rig.ForeL, rig.GripL);
        if (kind == ZombieKind.Raider) weapon = BuildBat(rig.ForeR, rig.GripR);
        return rig;
    }

    // Tripo 새총 Y자 틀 (2026-11-27, 사용자 "새총 4번": 나무 가지 + 검은 테이프 손잡이 + 갈래 끝 고무 캡). 없으면 예전 원기둥 새총
    public static bool UseTripoSlingshot = true;
    static GameObject tripoSlingshot;
    static bool tripoSlingshotLoaded;

    // 왼손에 쥔 새총: Y자 나무 손잡이 + 고무줄 두 가닥 + 가죽 주머니
    static Slingshot BuildSlingshot(Transform fore, Vector3 grip)
    {
        if (fore == null) return null;
        // 주먹을 관통해 쥔다 (아래팔 기준 주먹 가운데). 팔을 앞으로 뻗으면 갈래가 위를, 주머니가 몸 쪽을 향한다
        var fork = new GameObject("Slingshot").transform;
        fork.SetParent(fore, false);
        fork.localPosition = grip;
        fork.localRotation = Quaternion.Euler(90, 0, 0);

        // 호박색 라텍스 고무 튜브 (Tripo 새총 끝 고무 캡 색), 닳은 갈색 가죽, 묶은 끈
        var rubber = RuntimeVisuals.Mat(new Color(0.46f, 0.26f, 0.14f), 0.66f);
        var leather = ProcTex.Get(ProcTex.Kind.Leather, new Color(0.30f, 0.19f, 0.11f), 37, 0, 0.55f, new Color(0.17f, 0.10f, 0.06f));
        var cord = RuntimeVisuals.Mat(new Color(0.13f, 0.10f, 0.08f), 0.2f);
        Vector3 tipL = Slingshot.TipL, tipR = Slingshot.TipR;
        if (UseTripoSlingshot && !tripoSlingshotLoaded) { tripoSlingshotLoaded = true; tripoSlingshot = Resources.Load<GameObject>("Enemies/ZombieRanged/slingshot"); }
        if (UseTripoSlingshot && tripoSlingshot != null)
        {
            // 모델 원점 = 테이프 손잡이 가운데, y = 갈래 쪽. 고무줄은 두 고무 캡 가운데에 건다
            var model = Object.Instantiate(tripoSlingshot, fork, false);
            model.name = "Frame";
            var l = model.transform.Find("TipL"); var r = model.transform.Find("TipR");
            if (l != null) tipL = l.localPosition;
            if (r != null) tipR = r.localPosition;
        }
        else
        {
            var wood = RuntimeVisuals.Mat(new Color(0.43f, 0.29f, 0.16f), 0.3f);
            Part(PrimitiveType.Cylinder, "Grip", fork, new Vector3(0, 0.015f, 0), new Vector3(0.032f, 0.075f, 0.032f), wood);
            Part(PrimitiveType.Cylinder, "ProngL", fork, new Vector3(-0.03f, 0.155f, 0), new Vector3(0.028f, 0.072f, 0.028f), wood, new Vector3(0, 0, 24.8f));
            Part(PrimitiveType.Cylinder, "ProngR", fork, new Vector3(0.03f, 0.155f, 0), new Vector3(0.028f, 0.072f, 0.028f), wood, new Vector3(0, 0, -24.8f));
            // 갈래 끝에 감은 고무 매듭
            Part(PrimitiveType.Sphere, "WrapL", fork, Slingshot.TipL, Vector3.one * 0.032f, rubber, null, false);
            Part(PrimitiveType.Sphere, "WrapR", fork, Slingshot.TipR, Vector3.one * 0.032f, rubber, null, false);
        }

        // 고무 튜브 두 가닥 (길이·방향은 Slingshot.Pose가 매 프레임 정한다)
        var bandL = MeshPart(TubeMesh(), "BandL", fork, Vector3.zero, Vector3.one * 0.01f, rubber, false);
        var bandR = MeshPart(TubeMesh(), "BandR", fork, Vector3.zero, Vector3.one * 0.01f, rubber, false);
        // 고무 튜브를 주머니 구멍에 끼워 감아 묶은 끈 (Pose가 튜브 끝에 붙인다)
        var tieL = MeshPart(TubeMesh(), "TieL", fork, Vector3.zero, Vector3.one * 0.01f, cord, false);
        var tieR = MeshPart(TubeMesh(), "TieR", fork, Vector3.zero, Vector3.one * 0.01f, cord, false);
        // 돌을 뒤에서 감싸는 휜 가죽 주머니와 그 안의 울퉁불퉁한 돌 (주머니 +z가 갈래 쪽)
        var pouch = new GameObject("Pouch").transform;
        pouch.SetParent(fork, false);
        MeshPart(PouchMesh(), "PouchLeather", pouch, Vector3.zero, Vector3.one, leather, true);
        var stone = MeshPart(PebbleMesh(), "PouchStone", pouch, new Vector3(0, 0, PouchStoneZ), new Vector3(0.044f, 0.036f, 0.038f), Stone, false);

        var sling = new Slingshot(fork, pouch, bandL, bandR, stone, tipL, tipR);
        sling.SetPouch(pouchEnd, tieL, tieR);
        sling.Relax();
        return sling;
    }

    // ---- 새총 고무줄·주머니·돌 메시 (2026-11-28, 사용자: 상자·막대 고무줄과 주머니가 장난감 같아 Tripo 틀과 어긋난다) ----
    const float PouchR = 0.024f;       // 주머니가 돌을 감싸는 휨 반지름
    const float PouchStoneZ = 0.017f;  // 돌 가운데 (주머니 기준, 갈래 쪽)
    static Mesh tubeMesh, pouchMesh, pebbleMesh;
    static Vector3 pouchEnd;           // 주머니 +x 끝 (오른 고무줄을 묶는 곳). 왼쪽은 x를 뒤집는다

    // 고무 튜브·끈: 길이 z -0.5~0.5, 지름 1 (localScale = 굵기, 굵기, 길이)
    static Mesh TubeMesh()
    {
        if (tubeMesh != null) return tubeMesh;
        const int sides = 10;
        var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
        for (int r = 0; r < 2; r++)
            for (int s = 0; s <= sides; s++)
            {
                float a = s * Mathf.PI * 2f / sides;
                var d = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                v.Add(d * 0.5f + new Vector3(0, 0, r == 0 ? -0.5f : 0.5f)); n.Add(d); uv.Add(new Vector2((float)s / sides, r));
            }
        for (int s = 0; s < sides; s++) Quad(tri, v, s, s + 1, sides + 1 + s, sides + 2 + s, v[s] + v[s + 1]);
        // 양 끝 뚜껑
        for (int e = 0; e < 2; e++)
        {
            float z = e == 0 ? -0.5f : 0.5f; var nz = new Vector3(0, 0, e == 0 ? -1f : 1f);
            int c = v.Count; v.Add(new Vector3(0, 0, z)); n.Add(nz); uv.Add(new Vector2(0.5f, e));
            for (int s = 0; s <= sides; s++)
            {
                float a = s * Mathf.PI * 2f / sides;
                v.Add(new Vector3(Mathf.Cos(a) * 0.5f, Mathf.Sin(a) * 0.5f, z)); n.Add(nz); uv.Add(new Vector2((float)s / sides, e));
            }
            for (int s = 0; s < sides; s++) Tri(tri, v, c, c + 1 + s, c + 2 + s, nz);
        }
        tubeMesh = new Mesh { name = "SlingTube" };
        tubeMesh.SetVertices(v); tubeMesh.SetNormals(n); tubeMesh.SetUVs(0, uv); tubeMesh.SetTriangles(tri, 0);
        tubeMesh.RecalculateBounds();
        return tubeMesh;
    }

    // 가죽 주머니: 돌 뒤를 감싸는 휜 띠 (가운데 넓고 끝으로 갈수록 좁아짐, 가로로도 살짝 오목) + 양 끝 곧은 꼬리.
    // 두께가 있는 닫힌 판 (안·밖·네 옆면)
    static Mesh PouchMesh()
    {
        if (pouchMesh != null) return pouchMesh;
        const float th = 0.0028f, tail = 0.011f, thetaMax = 66f * Mathf.Deg2Rad;
        const int arcN = 16, tailN = 3, across = 4;
        var center = new Vector3(0, 0, PouchStoneZ);
        // 가운데 선 (점, 안쪽 법선, 진행 방향, 가운데부터 잰 길이 비율)
        var P = new List<Vector3>(); var Nin = new List<Vector3>(); var T = new List<Vector3>(); var F = new List<float>(); var S = new List<float>();
        float arcHalf = PouchR * thetaMax, half = arcHalf + tail;
        System.Action<float> add = (sArc) =>
        {
            // sArc: 가운데부터 잰 길이 (-half ~ half)
            float a = Mathf.Abs(sArc), sg = Mathf.Sign(sArc);
            float th0 = Mathf.Min(a, arcHalf) / PouchR * sg;
            var p = center + PouchR * new Vector3(Mathf.Sin(th0), 0, -Mathf.Cos(th0));
            var t = new Vector3(Mathf.Cos(th0), 0, Mathf.Sin(th0));
            var ni = new Vector3(-Mathf.Sin(th0), 0, Mathf.Cos(th0));
            if (a > arcHalf) p += t * (a - arcHalf) * sg;
            P.Add(p); Nin.Add(ni); T.Add(t); F.Add(a / half); S.Add(sArc);
        };
        for (int i = tailN; i > 0; i--) add(-arcHalf - tail * i / tailN);
        for (int i = 0; i <= arcN; i++) add(-arcHalf + 2f * arcHalf * i / arcN);
        for (int i = 1; i <= tailN; i++) add(arcHalf + tail * i / tailN);
        int L = P.Count;
        // 너비: 가운데 2.7 cm → 끝 1.4 cm, 가운데쯤은 가장자리가 돌 쪽으로 0.4 cm 오목
        System.Func<int, float, Vector3> at = (i, y) =>
        {
            float w = Mathf.Lerp(0.027f, 0.014f, Mathf.Pow(F[i], 1.6f));
            float yy = y * w * 0.5f;
            float cup = 0.004f * (1f - Mathf.Clamp01(F[i] * 1.3f)) * y * y;
            return P[i] + Vector3.up * yy + Nin[i] * cup;
        };
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
        // 안·밖 면
        for (int side = 0; side < 2; side++)
        {
            float off = side == 0 ? th * 0.5f : -th * 0.5f;
            int b = v.Count;
            for (int i = 0; i < L; i++)
                for (int j = 0; j <= across; j++)
                {
                    float y = -1f + 2f * j / across;
                    v.Add(at(i, y) + Nin[i] * off);
                    uv.Add(new Vector2(S[i] * 3f, y * 0.04f));
                }
            for (int i = 0; i < L - 1; i++)
                for (int j = 0; j < across; j++)
                {
                    int a0 = b + i * (across + 1) + j, a1 = a0 + 1, b0 = a0 + across + 1, b1 = b0 + 1;
                    Quad(tri, v, a0, a1, b0, b1, side == 0 ? Nin[i] : -Nin[i]);
                }
        }
        // 긴 옆면 두 개 (위·아래 가장자리)
        for (int e = 0; e < 2; e++)
        {
            float y = e == 0 ? -1f : 1f; int b = v.Count;
            for (int i = 0; i < L; i++)
            {
                v.Add(at(i, y) + Nin[i] * th * 0.5f); uv.Add(new Vector2(S[i] * 3f, 0));
                v.Add(at(i, y) - Nin[i] * th * 0.5f); uv.Add(new Vector2(S[i] * 3f, 0.01f));
            }
            for (int i = 0; i < L - 1; i++) Quad(tri, v, b + i * 2, b + i * 2 + 1, b + i * 2 + 2, b + i * 2 + 3, Vector3.up * y);
        }
        // 양 끝 옆면
        for (int e = 0; e < 2; e++)
        {
            int i = e == 0 ? 0 : L - 1; int b = v.Count;
            for (int j = 0; j <= across; j++)
            {
                float y = -1f + 2f * j / across;
                v.Add(at(i, y) + Nin[i] * th * 0.5f); uv.Add(new Vector2(0, y * 0.04f));
                v.Add(at(i, y) - Nin[i] * th * 0.5f); uv.Add(new Vector2(0.01f, y * 0.04f));
            }
            for (int j = 0; j < across; j++) Quad(tri, v, b + j * 2, b + j * 2 + 1, b + j * 2 + 2, b + j * 2 + 3, T[i] * (e == 0 ? -1f : 1f));
        }
        pouchMesh = new Mesh { name = "SlingPouch" };
        pouchMesh.SetVertices(v); pouchMesh.SetUVs(0, uv); pouchMesh.SetTriangles(tri, 0);
        pouchMesh.RecalculateNormals(); pouchMesh.RecalculateTangents(); pouchMesh.RecalculateBounds();
        // 고무줄을 묶는 곳: +x 꼬리 끝에서 0.4 cm 안쪽
        pouchEnd = P[L - 1] - T[L - 1] * 0.004f;
        return pouchMesh;
    }

    // 강가 자갈처럼 둥글지만 울퉁불퉁한 돌 (지름 1)
    static Mesh PebbleMesh()
    {
        if (pebbleMesh != null) return pebbleMesh;
        var src = MeshOf(PrimitiveType.Sphere);
        var v = src.vertices;
        for (int i = 0; i < v.Length; i++)
        {
            var d = v[i].normalized;
            float k = Mathf.PerlinNoise(d.x * 1.6f + 3.1f, d.y * 1.6f + 7.3f) + Mathf.PerlinNoise(d.z * 1.6f + 1.9f, d.x * 1.6f + 5.2f) - 1f;
            float k2 = Mathf.PerlinNoise(d.y * 4.1f + 11.7f, d.z * 4.1f + 2.3f) - 0.5f;
            v[i] = d * 0.5f * (1f + 0.16f * k + 0.04f * k2);
        }
        pebbleMesh = new Mesh { name = "SlingPebble" };
        pebbleMesh.vertices = v; pebbleMesh.uv = src.uv; pebbleMesh.triangles = src.triangles;
        pebbleMesh.RecalculateNormals();
        // 구의 이음선(같은 자리 두 꼭짓점)에서 법선이 갈라지지 않게 같은 자리끼리 평균
        var nrm = pebbleMesh.normals; var sum = new Dictionary<Vector3Int, Vector3>();
        System.Func<Vector3, Vector3Int> key = p => new Vector3Int(Mathf.RoundToInt(p.x * 1e4f), Mathf.RoundToInt(p.y * 1e4f), Mathf.RoundToInt(p.z * 1e4f));
        for (int i = 0; i < v.Length; i++) { var kk = key(v[i]); sum[kk] = (sum.TryGetValue(kk, out var s) ? s : Vector3.zero) + nrm[i]; }
        for (int i = 0; i < v.Length; i++) nrm[i] = sum[key(v[i])].normalized;
        pebbleMesh.normals = nrm; pebbleMesh.RecalculateBounds();
        return pebbleMesh;
    }

    // 삼각형 하나: 앞면(시계 방향)이 want 쪽을 보게 감는다
    static void Tri(List<int> tri, List<Vector3> v, int a, int b, int c, Vector3 want)
    {
        if (Vector3.Dot(Vector3.Cross(v[b] - v[a], v[c] - v[a]), want) < 0f) { int t = b; b = c; c = t; }
        tri.Add(a); tri.Add(b); tri.Add(c);
    }

    // 사각형 a0-a1 / b0-b1 (a0·b0이 한 변): 두 삼각형, 앞면이 want 쪽
    static void Quad(List<int> tri, List<Vector3> v, int a0, int a1, int b0, int b1, Vector3 want)
    {
        Tri(tri, v, a0, a1, b0, want);
        Tri(tri, v, a1, b1, b0, want);
    }

    // 콜라이더 없는 메시 부품
    static Transform MeshPart(Mesh mesh, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, bool shadows)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
        return go.transform;
    }

    // 원거리 좀비의 새총. 고무줄 두 가닥을 Y자 끝과 가죽 주머니 사이에 매 프레임 늘여 놓는다.
    // Zombie가 Pose로 주머니를 오른손 쪽으로 당기면 고무줄이 늘어나고, 놓으면 튕겨 돌아간다
    public class Slingshot
    {
        // Y자 기준 좌표 (y = 갈래 쪽, z = 겨누는 쪽)
        public static readonly Vector3 TipL = new Vector3(-0.06f, 0.22f, 0f), TipR = new Vector3(0.06f, 0.22f, 0f);
        static readonly Vector3 RestPouch = new Vector3(0f, 0.19f, -0.04f);
        const float BandWidth = 0.013f;

        public readonly Transform Fork;
        readonly Transform pouch, bandL, bandR, stone;
        readonly Vector3 tipL, tipR, restPouch; // 이 새총의 갈래 끝 (Tripo 틀이면 고무 캡 가운데)

        public Slingshot(Transform fork, Transform pouch, Transform bandL, Transform bandR, Transform stone)
            : this(fork, pouch, bandL, bandR, stone, TipL, TipR) { }

        public Slingshot(Transform fork, Transform pouch, Transform bandL, Transform bandR, Transform stone, Vector3 tipL, Vector3 tipR)
        {
            Fork = fork;
            this.pouch = pouch;
            this.bandL = bandL;
            this.bandR = bandR;
            this.stone = stone;
            this.tipL = tipL;
            this.tipR = tipR;
            // 쉬는 주머니: 갈래 끝 높이에서 조금 아래, 몸 쪽으로 (기본 새총과 같은 비율)
            float y = (tipL.y + tipR.y) * 0.5f;
            restPouch = new Vector3((tipL.x + tipR.x) * 0.5f, y + (RestPouch.y - TipL.y), (tipL.z + tipR.z) * 0.5f + RestPouch.z);
        }

        Vector3 pouchEnd;          // 주머니 기준 +x 끝 (오른 고무줄 묶는 곳). 0이면 주머니 가운데
        Transform tieL, tieR;      // 고무줄 끝을 주머니에 묶은 끈

        public void SetPouch(Vector3 end, Transform tieL, Transform tieR)
        {
            pouchEnd = end;
            this.tieL = tieL;
            this.tieR = tieR;
        }

        // 돌이 날아가기 시작하는 곳 (두 갈래 끝 사이)
        public Vector3 Muzzle => Fork.TransformPoint((tipL + tipR) * 0.5f);

        // 당기지 않았을 때 주머니가 있는 곳 (오른손이 주머니를 집으러 가는 곳)
        public Vector3 Rest => Fork.TransformPoint(restPouch);

        // 고무줄이 느슨하게 Y자에 붙어 있는 상태 (돌 없음)
        public void Relax() => Pose(Vector3.zero, 0f, false);

        // draw 0 = 주머니가 Y자 바로 뒤, 1 = 주머니가 pullTo(오른손)까지 당겨짐. loaded면 주머니에 돌이 보인다.
        // 음수면 놓은 고무줄이 튕겨 Y자 앞으로 넘어간 상태 (-0.5까지)
        public void Pose(Vector3 pullTo, float draw, bool loaded)
        {
            if (Fork == null || pouch == null) return;
            Vector3 wl = Fork.TransformPoint(tipL), wr = Fork.TransformPoint(tipR);
            Vector3 p = Vector3.LerpUnclamped(Fork.TransformPoint(restPouch), pullTo, Mathf.Clamp(draw, -0.5f, 1f));
            pouch.position = p;
            Vector3 face = (wl + wr) * 0.5f - p;
            if (face.sqrMagnitude > 1e-6f) pouch.rotation = Quaternion.LookRotation(face, Fork.up);
            // 고무줄은 주머니 양 끝(꼬리 구멍)에 묶여 있다
            Vector3 el = pouch.TransformPoint(new Vector3(-pouchEnd.x, pouchEnd.y, pouchEnd.z));
            Vector3 er = pouch.TransformPoint(pouchEnd);
            Stretch(bandL, tieL, wl, el);
            Stretch(bandR, tieR, wr, er);
            if (stone != null && stone.gameObject.activeSelf != loaded) stone.gameObject.SetActive(loaded);
        }

        void Stretch(Transform band, Transform tie, Vector3 from, Vector3 to)
        {
            if (band == null) return;
            Vector3 d = to - from;
            float len = d.magnitude;
            band.position = (from + to) * 0.5f;
            if (len > 1e-4f) band.rotation = Quaternion.LookRotation(d, Fork.up);
            float s = Mathf.Max(1e-4f, Fork.lossyScale.x); // 좀비 크기 (부모 배율은 모두 균일)
            float local = len / s;
            float w = BandWidth * Mathf.Lerp(1f, 0.6f, Mathf.Clamp01(local / 0.7f)); // 늘어날수록 가늘어진다
            band.localScale = new Vector3(w, w, Mathf.Max(0.001f, local));
            if (tie == null) return;
            // 주머니 쪽 끝을 감아 묶은 끈 (튜브보다 조금 굵고 짧다)
            tie.rotation = band.rotation;
            tie.position = to - (len > 1e-4f ? d / len : Vector3.zero) * 0.006f * s;
            tie.localScale = new Vector3(BandWidth * 1.25f, BandWidth * 1.25f, 0.009f);
        }
    }

    // 약탈자의 못 박힌 야구방망이 (#B58E58, 검은 손잡이 테이프). 손잡이 끝을 쥐고, 팔을 뒤로 젖히면 머리 뒤로 넘어간다
    // Tripo 못 박힌 방망이 (2026-11-28, 사용자 "방망이 3": 연갈색 나무 + 검은 테이프 손잡이 + 녹슨 못 + 핏자국). 없으면 아래 예전 원기둥 방망이
    public static bool UseTripoBat = true;
    static GameObject tripoBat;
    static bool tripoBatLoaded;

    static Transform BuildBat(Transform fore, Vector3 grip)
    {
        if (fore == null) return null;
        var bat = new GameObject("Bat").transform;
        bat.SetParent(fore, false);
        if (UseTripoBat && !tripoBatLoaded) { tripoBatLoaded = true; tripoBat = Resources.Load<GameObject>("Enemies/Raider/nailbat"); }
        if (UseTripoBat && tripoBat != null)
        {
            // 주먹 구멍을 지나도록 손목을 꺾어 쥔다: 아래팔과 45도 (쉴 때는 앞아래로 늘어뜨리고, 젖히면 머리 뒤로 넘어간다. 플레이에서 115도와 비교해 고름)
            bat.localPosition = grip;
            bat.localRotation = Quaternion.Euler(135, 0, 0);
            var model = Object.Instantiate(tripoBat, bat, false);
            model.name = "NailBat";
            return bat;
        }
        bat.localPosition = new Vector3(0, -(CharacterModels.HandReach - 0.30f) - 0.01f, 0.03f);
        bat.localRotation = Quaternion.Euler(150, 0, 0);
        var wood = ProcTex.Get(ProcTex.Kind.Wood, new Color(0.71f, 0.56f, 0.35f), 91);
        var tape = ProcTex.Plain(new Color(0.08f, 0.08f, 0.08f), 0.3f);
        var metal = ProcTex.Plain(new Color(0.55f, 0.55f, 0.53f), 0.6f, 0.7f);
        Part(PrimitiveType.Sphere, "Knob", bat, new Vector3(0, -0.06f, 0), new Vector3(0.05f, 0.03f, 0.05f), tape, null, false);
        Part(PrimitiveType.Cylinder, "Grip", bat, new Vector3(0, 0.04f, 0), new Vector3(0.038f, 0.1f, 0.038f), tape);
        Part(PrimitiveType.Cylinder, "Handle", bat, new Vector3(0, 0.22f, 0), new Vector3(0.035f, 0.14f, 0.035f), wood);
        Part(PrimitiveType.Cylinder, "Taper", bat, new Vector3(0, 0.4f, 0), new Vector3(0.05f, 0.08f, 0.05f), wood);
        Part(PrimitiveType.Cylinder, "Barrel", bat, new Vector3(0, 0.62f, 0), new Vector3(0.068f, 0.18f, 0.068f), wood);
        Part(PrimitiveType.Sphere, "Cap", bat, new Vector3(0, 0.8f, 0), new Vector3(0.068f, 0.02f, 0.068f), wood, null, false);
        // 사방으로 박은 못
        for (int i = 0; i < 6; i++)
        {
            float a = i * 60f + (i % 2) * 25f;
            var dir = Quaternion.Euler(0, a, 0) * Vector3.right;
            Part(PrimitiveType.Cylinder, "Nail", bat, new Vector3(0, 0.52f + i * 0.045f, 0) + dir * 0.045f, new Vector3(0.006f, 0.03f, 0.006f), metal, new Vector3(0, a, 90), false);
        }
        return bat;
    }

    // ---------- 도우미 ----------

    // 콜라이더 없는 부품 (CreatePrimitive는 콜라이더가 프레임 끝까지 남아서 메시만 빌려 쓴다)
    static Transform Part(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, Vector3? euler = null, bool shadows = true)
    {
        if (parent == null) return null;
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(euler ?? Vector3.zero);
        go.transform.localScale = scale;
        go.AddComponent<MeshFilter>().sharedMesh = MeshOf(type);
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
        return go.transform;
    }

    static Mesh MeshOf(PrimitiveType type)
    {
        if (meshes.TryGetValue(type, out var m) && m != null) return m;
        var tmp = GameObject.CreatePrimitive(type);
        tmp.SetActive(false);
        m = tmp.GetComponent<MeshFilter>().sharedMesh;
        Object.Destroy(tmp);
        meshes[type] = m;
        return m;
    }
}
