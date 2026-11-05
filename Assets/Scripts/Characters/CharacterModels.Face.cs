using System.Collections.Generic;
using UnityEngine;

// 매끄러운 얼굴 (컷씬 주인공 Civilian과 시체 Corpse가 쓴다. 좀비·플레이어 손 등 다른 모델은 그대로 Head()를 쓴다).
// 시체는 FaceStyle로 변주한다: 감거나 반쯤 뜬 눈, 벌어진 입, 꺼진 볼, 부은 눈두덩, 마른 피·멍·베인 상처, 수염 그루터기, 긴 머리
// 머리 전체를 위도·경도 격자 한 장으로 만들고, 이마·관자놀이·눈두덩·코·광대·입술·턱 홈·턱끝을 표면 위의 부드러운
// 융기와 홈(가우시안)으로 새겨 한 덩어리로 잇는다. 눈은 얕은 우물(눈구멍) 안에 박힌 눈알이라 우물 가장자리가 눈꺼풀이 되고,
// 눈썹·입술·머리카락은 같은 표면 함수로 떠낸 얇은 조각이라 얼굴에 정확히 붙는다. 법선은 정점을 공유해 부드럽다.
// 좌표는 머리 관절 기준: 턱끝 아래 0, 정수리 0.25, +z 앞, +x 오른쪽 (m)
public static partial class CharacterModels
{
    // 입 가운데 높이 (머리 관절 기준). 컷씬 배우가 손으로 입을 가릴 때 쓴다
    public const float MouthY = 0.062f;

    // 얼굴 표면: 아래로 갈수록 좁아지는 밑 타원체 + 융기·홈(가우시안) + 눈구멍 우물
    class FaceShape
    {
        public Vector3 Center = new Vector3(0f, 0.128f, -0.004f);
        public float RX = 0.077f, RY = 0.122f, RZFront = 0.096f, RZBack = 0.104f;
        public readonly List<float[]> Bumps = new List<float[]>();   // cx, cy, cz, sx, sy, sz, 높이
        public readonly List<float[]> Wells = new List<float[]>();   // cx, cy, cz, 가로 반지름, 세로 반지름, 기울기(도), 깊이, 테두리 높이

        public static float Smooth01(float t) { t = Mathf.Clamp01(t); return t * t * (3 - 2 * t); }

        float Width(float y) => Mathf.Lerp(0.8f, 1f, Smooth01((y - 0.025f) / 0.115f));
        float Depth(float y) => Mathf.Lerp(0.9f, 1f, Smooth01((y - 0.025f) / 0.115f));

        // 밑 타원체의 점 (경도 az: 0 앞 → +x 쪽으로, 위도 el: -90 아래 ~ 90 위, 라디안)
        public Vector3 Base(float az, float el)
        {
            float sy = Mathf.Sin(el), cy = Mathf.Cos(el);
            float y = Center.y + RY * sy;
            float ca = Mathf.Cos(az), sa = Mathf.Sin(az);
            float rz = ca >= 0 ? RZFront : RZBack;
            return new Vector3(Center.x + RX * Width(y) * cy * sa, y, Center.z + rz * Depth(y) * cy * ca);
        }

        public Vector3 BaseNormal(float az, float el)
        {
            if (Mathf.Abs(el) > 89.5f * Mathf.Deg2Rad) return el > 0 ? Vector3.up : Vector3.down;
            const float h = 0.002f;
            var dAz = Base(az + h, el) - Base(az - h, el);
            var dEl = Base(az, el + h) - Base(az, el - h);
            var n = Vector3.Cross(dEl.normalized, dAz.normalized).normalized;   // 차분이 작아 외적이 1e-5 아래로 떨어지면 normalized가 0이 되므로 접선부터 정규화한다
            if (Vector3.Dot(n, Base(az, el) - Center) < 0) n = -n;
            return n;
        }

        // 표면에서 밀어 올리는(음수면 꺼지는) 거리
        public float Displace(Vector3 p)
        {
            float d = 0;
            foreach (var b in Bumps)
            {
                float dx = (p.x - b[0]) / b[3], dy = (p.y - b[1]) / b[4], dz = (p.z - b[2]) / b[5];
                float q = dx * dx + dy * dy + dz * dz;
                if (q < 16f) d += b[6] * Mathf.Exp(-0.5f * q);
            }
            foreach (var w in Wells)
            {
                float front = Smooth01((p.z - (w[2] - 0.04f)) / 0.025f);
                if (front <= 0) continue;
                float t = w[5] * Mathf.Deg2Rad, ct = Mathf.Cos(t), st = Mathf.Sin(t);
                float dx = p.x - w[0], dy = p.y - w[1];
                float xr = (dx * ct + dy * st) / w[3], yr = (-dx * st + dy * ct) / w[4];
                float rho = Mathf.Sqrt(xr * xr + yr * yr);
                float well = -w[6] * (1 - Smooth01((rho - 0.78f) / 0.3f));
                float e = (rho - 1.22f) / 0.16f;
                float rim = w[7] * Mathf.Exp(-0.5f * e * e);
                d += (well + rim) * front;
            }
            return d;
        }

        public Vector3 Point(float az, float el)
        {
            var p = Base(az, el);
            return p + BaseNormal(az, el) * Displace(p);
        }

        public Vector3 Normal(float az, float el)
        {
            if (Mathf.Abs(el) > 89.5f * Mathf.Deg2Rad) return el > 0 ? Vector3.up : Vector3.down;
            const float h = 0.004f;
            var dAz = Point(az + h, el) - Point(az - h, el);
            var dEl = Point(az, el + h) - Point(az, el - h);
            var n = Vector3.Cross(dEl.normalized, dAz.normalized).normalized;   // 차분이 작아 외적이 1e-5 아래로 떨어지면 normalized가 0이 되므로 접선부터 정규화한다
            if (Vector3.Dot(n, BaseNormal(az, el)) < 0) n = -n;
            return n;
        }

        // 얼굴 앞면의 (x, y)에 해당하는 경도·위도
        public void Locate(float x, float y, out float az, out float el)
        {
            el = Mathf.Asin(Mathf.Clamp((y - Center.y) / RY, -0.999f, 0.999f));
            float r = RX * Width(y) * Mathf.Cos(el);
            az = Mathf.Asin(Mathf.Clamp(x / Mathf.Max(0.0001f, r), -0.999f, 0.999f));
        }

        public Vector3 FrontPoint(float x, float y, float offset = 0f)
        {
            Locate(x, y, out float az, out float el);
            return Point(az, el) + Normal(az, el) * offset;
        }

        public float FrontZ(float x, float y)
        {
            Locate(x, y, out float az, out float el);
            return Base(az, el).z;
        }
    }

    // 얼굴 생김새·표정 변주 (시체용). Human.Face에 넣으면 SmoothHead가 쓴다. 눈: 1 뜸 · 0.5 반쯤 · 0 감음
    class FaceStyle
    {
        public float EyeOpenL = 1f, EyeOpenR = 1f;
        public Color EyeWhite = Hex("e9e3da"), Iris = Hex("3b2a1d");
        public Color LipUpper = Hex("9a5f55"), LipLower = Hex("ab6c60");
        public float MouthGap;      // 벌어진 입 (0~1): 입선이 어두운 틈이 되고 아랫입술이 내려간다 (이가 보인다)
        public float CheekSunk;     // 볼·관자놀이가 꺼진 정도 (0~1)
        public float Swollen;       // 부은 정도 (0~1): 눈두덩·광대가 부풀고 눈이 작아진다
        public bool Stubble;        // 희끗한 수염 그루터기 (턱·볼·윗입술)
        public readonly List<float[]> Marks = new List<float[]>(); // 얼굴 자국: x, y, 가로 반지름, 세로 반지름, 기울기(도), 종류(0 마른 피, 1 멍, 2 베인 상처)

        public FaceStyle Mark(float x, float y, float rx, float ry, float rot, int kind)
        {
            Marks.Add(new[] { x, y, rx, ry, rot, kind });
            return this;
        }
    }

    static readonly Dictionary<string, Mesh> faceMeshes = new Dictionary<string, Mesh>();

    // 편집기 미리보기에서 얼굴을 다시 만들 때 쓴다
    public static void ClearFaceCache() => faceMeshes.Clear();

    // 주인공 얼굴의 생김새
    static FaceShape CivilianFace()
    {
        var f = new FaceShape();
        void B(float x, float y, float sx, float sy, float sz, float amp, float? z = null) => f.Bumps.Add(new[] { x, y, z ?? f.FrontZ(x, y), sx, sy, sz, amp });
        // 이마: 눈썹뼈가 가로로 솟고, 이마는 살짝 둥글고, 관자놀이는 꺼진다
        B(0f, 0.166f, 0.05f, 0.009f, 0.03f, 0.0045f);
        B(0f, 0.2f, 0.04f, 0.03f, 0.04f, 0.002f);
        B(0.07f, 0.17f, 0.014f, 0.022f, 0.025f, -0.0025f, 0.03f);
        B(-0.07f, 0.17f, 0.014f, 0.022f, 0.025f, -0.0025f, 0.03f);
        foreach (var s in new[] { -1f, 1f })
        {
            // 눈두덩(안와)은 넓고 얕게 꺼지고, 윗눈꺼풀 위에 주름, 그 안에 눈구멍 우물 (바깥쪽 눈꼬리가 조금 내려간다)
            B(s * 0.031f, 0.146f, 0.02f, 0.013f, 0.016f, -0.0065f);
            B(s * 0.031f, 0.159f, 0.016f, 0.0025f, 0.01f, -0.0015f);
            f.Wells.Add(new[] { s * 0.031f, 0.146f, f.FrontZ(s * 0.031f, 0.146f), 0.013f, 0.0062f, -s * 6f, 0.0065f, 0.0015f });
            // 광대뼈, 볼의 살, 턱 모서리
            B(s * 0.05f, 0.128f, 0.016f, 0.012f, 0.02f, 0.004f);
            B(s * 0.04f, 0.095f, 0.02f, 0.018f, 0.02f, 0.002f);
            B(s * 0.05f, 0.05f, 0.014f, 0.02f, 0.028f, 0.004f, -0.012f);
            // 콧방울과 콧구멍 아래 그늘
            B(s * 0.0125f, 0.099f, 0.0065f, 0.0055f, 0.009f, 0.0065f);
            B(s * 0.006f, 0.0905f, 0.004f, 0.0025f, 0.006f, -0.003f);
            // 입꼬리
            B(s * 0.024f, 0.062f, 0.004f, 0.004f, 0.006f, -0.0015f);
        }
        // 코: 콧대 → 코끝
        B(0f, 0.156f, 0.009f, 0.012f, 0.012f, 0.003f);
        B(0f, 0.136f, 0.0085f, 0.013f, 0.012f, 0.007f);
        B(0f, 0.118f, 0.009f, 0.011f, 0.012f, 0.011f);
        B(0f, 0.104f, 0.0105f, 0.0085f, 0.013f, 0.0145f);
        // 입: 윗입술·아랫입술·입선, 턱 홈, 턱끝
        B(0f, 0.0675f, 0.021f, 0.0045f, 0.012f, 0.0035f);
        B(0f, 0.0555f, 0.017f, 0.0045f, 0.012f, 0.0045f);
        B(0f, MouthY, 0.02f, 0.0022f, 0.01f, -0.0035f);
        B(0f, 0.04f, 0.014f, 0.004f, 0.01f, -0.002f);
        B(0f, 0.022f, 0.018f, 0.012f, 0.016f, 0.006f);
        return f;
    }

    // 주인공 얼굴에 변주를 얹는다: 눈 우물(감으면 얕고 눈꺼풀이 도톰, 반쯤 뜨면 좁은 틈), 꺼진 볼·관자놀이, 부은 눈두덩·광대, 처진 턱, 베인 홈
    static FaceShape StyledFace(FaceStyle st)
    {
        var f = CivilianFace();
        if (st == null) return f;
        void B(float x, float y, float sx, float sy, float sz, float amp, float? z = null) => f.Bumps.Add(new[] { x, y, z ?? f.FrontZ(x, y), sx, sy, sz, amp });
        foreach (var s in new[] { -1f, 1f })
        {
            float open = s < 0 ? st.EyeOpenL : st.EyeOpenR;
            var w = f.Wells[s < 0 ? 0 : 1];
            w[4] = Mathf.Lerp(0.0028f, 0.0062f, open);   // 세로 반지름: 감을수록 가는 틈
            w[6] = Mathf.Lerp(0.0012f, 0.0065f, open);   // 깊이
            w[7] = Mathf.Lerp(0.0028f, 0.0015f, open);   // 테두리: 감으면 눈꺼풀이 도톰하다
            if (st.Swollen > 0)
            {
                B(s * 0.031f, 0.15f, 0.024f, 0.016f, 0.02f, 0.0065f * st.Swollen); // 부은 눈두덩
                B(s * 0.05f, 0.124f, 0.02f, 0.016f, 0.02f, 0.005f * st.Swollen);   // 부은 광대
                w[4] *= 1 - 0.4f * st.Swollen;
            }
            if (st.CheekSunk > 0)
            {
                B(s * 0.046f, 0.1f, 0.016f, 0.016f, 0.018f, -0.006f * st.CheekSunk);         // 꺼진 볼
                B(s * 0.066f, 0.165f, 0.012f, 0.02f, 0.02f, -0.003f * st.CheekSunk, 0.03f);  // 꺼진 관자놀이
            }
        }
        if (st.MouthGap > 0)
        {
            B(0f, 0.052f - 0.006f * st.MouthGap, 0.016f, 0.005f, 0.01f, -0.0025f * st.MouthGap);                      // 처진 턱: 아랫입술 아래가 패인다
            B(0f, MouthY - 0.0025f * st.MouthGap, 0.017f, 0.0035f + 0.004f * st.MouthGap, 0.01f, -0.0045f * st.MouthGap); // 벌어진 입속이 들어간다
        }
        foreach (var m in st.Marks)
            if ((int)m[5] == 2) B(m[0], m[1], m[2] * 0.8f + 0.001f, m[3] * 0.9f, 0.01f, -0.0032f, f.FrontZ(m[0], m[1]));
        return f;
    }

    // 얼굴 위의 타원 자국 조각 (피·멍·상처·수염): 중심 (cx, cy), 반지름 (rx, ry), 기울기 rot(도)
    static Mesh EllipsePatch(string key, FaceShape f, float cx, float cy, float rx, float ry, float rot, float offset, int rows = 7, int cols = 9)
    {
        var pts = new Vector2[rows, cols];
        float cr = Mathf.Cos(rot * Mathf.Deg2Rad), sr = Mathf.Sin(rot * Mathf.Deg2Rad);
        for (int r = 0; r < rows; r++)
        {
            float v = -1f + 2f * r / (rows - 1);
            float half = Mathf.Sqrt(Mathf.Max(0f, 1f - v * v));
            for (int c = 0; c < cols; c++)
            {
                float u = (-1f + 2f * c / (cols - 1)) * half;
                float x = u * rx, y = v * ry;
                pts[r, c] = new Vector2(cx + x * cr - y * sr, cy + x * sr + y * cr);
            }
        }
        return SurfacePatch(key, f, pts, offset);
    }

    // 머리 전체 메시: 앞쪽 200도·얼굴 띠(-56~42도)를 촘촘하게 나눈 위도·경도 격자
    static Mesh FaceMesh(string key, FaceShape f)
    {
        if (faceMeshes.TryGetValue(key, out var m) && m != null) return m;
        var azs = new List<float>();
        for (int i = 0; i <= 88; i++) azs.Add(Mathf.Lerp(-100f, 100f, i / 88f));
        for (int i = 1; i <= 28; i++) azs.Add(Mathf.Lerp(100f, 260f, i / 28f));
        var els = new List<float>();
        for (int i = 0; i < 8; i++) els.Add(Mathf.Lerp(-90f, -56f, i / 8f));
        for (int i = 0; i <= 70; i++) els.Add(Mathf.Lerp(-56f, 42f, i / 70f));
        for (int i = 1; i <= 14; i++) els.Add(Mathf.Lerp(42f, 90f, i / 14f));
        int cols = azs.Count, rows = els.Count;
        var verts = new Vector3[cols * rows];
        var uvs = new Vector2[verts.Length];
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                float az = azs[c] * Mathf.Deg2Rad, el = els[r] * Mathf.Deg2Rad;
                var p = f.Point(az, el);
                verts[r * cols + c] = p;
                uvs[r * cols + c] = new Vector2(az * 0.08f, p.y);
            }
        m = GridMesh(key, verts, uvs, rows, cols);
        // 바깥을 향하게: 앞 정점(경도 0, 위도 0)의 법선이 +z인지
        if (m.normals[48 * cols + 44].z < 0) FlipWinding(m);
        // 이음매(첫 줄과 마지막 줄은 같은 자리)의 법선을 맞춘다
        var nrm = m.normals;
        for (int r = 0; r < rows; r++)
        {
            int a = r * cols, b = a + cols - 1;
            var avg = (nrm[a] + nrm[b]).normalized;
            nrm[a] = nrm[b] = avg;
        }
        m.normals = nrm;
        m.RecalculateTangents();
        faceMeshes[key] = m;
        return m;
    }

    // 표면 위에 띄운 얇은 조각 (입술·눈썹). pts[r, c]: 앞면 (x, y) 좌표 (r은 아래→위, c는 왼쪽→오른쪽), offset: 표면에서 띄우는 거리
    static Mesh SurfacePatch(string key, FaceShape f, Vector2[,] pts, float offset)
    {
        if (faceMeshes.TryGetValue(key, out var m) && m != null) return m;
        int rows = pts.GetLength(0), cols = pts.GetLength(1);
        var verts = new Vector3[rows * cols];
        var uvs = new Vector2[verts.Length];
        var mid = Vector3.zero;
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                f.Locate(pts[r, c].x, pts[r, c].y, out float az, out float el);
                var p = f.Point(az, el) + f.Normal(az, el) * offset;
                verts[r * cols + c] = p;
                uvs[r * cols + c] = new Vector2(az * 0.08f, p.y);
                if (r == rows / 2 && c == cols / 2) mid = f.Normal(az, el);
            }
        m = GridMesh(key, verts, uvs, rows, cols);
        if (Vector3.Dot(m.normals[(rows / 2) * cols + cols / 2], mid) < 0) FlipWinding(m);
        m.RecalculateTangents();
        faceMeshes[key] = m;
        return m;
    }

    // 짧은 머리카락: 머리선 위를 덮는 두꺼운 껍질. 머리선은 앞이마에서 높고 귀 앞에서 내려오며 뒤통수에서 가장 낮다.
    // 맨 아랫줄은 표면 안쪽으로 넣어 머리와 틈이 없다
    static Mesh HairMesh(string key, FaceShape f)
    {
        if (faceMeshes.TryGetValue(key, out var m) && m != null) return m;
        float[] ctrlAz = { 0f, 35f, 60f, 72f, 85f, 95f, 110f, 140f, 180f }, ctrlEl = { 42f, 36f, 12f, 1f, 7f, 15f, 5f, -8f, -14f };
        // 머리선: 조각마다 부드럽게 잇고(코사인 보간), 잔털처럼 약간 들쭉날쭉하게
        float ElMin(float azDeg)
        {
            float a = Mathf.Abs(azDeg);
            float jitter = 2.5f * (Mathf.PerlinNoise(azDeg * 0.11f + 17.3f, 0.5f) - 0.5f);
            for (int i = 1; i < ctrlAz.Length; i++)
                if (a <= ctrlAz[i])
                {
                    float t = (a - ctrlAz[i - 1]) / (ctrlAz[i] - ctrlAz[i - 1]);
                    return Mathf.Lerp(ctrlEl[i - 1], ctrlEl[i], 0.5f - 0.5f * Mathf.Cos(t * Mathf.PI)) + jitter;
                }
            return ctrlEl[ctrlEl.Length - 1] + jitter;
        }
        const int cols = 73, rows = 24;
        var verts = new Vector3[(rows + 1) * cols];
        var uvs = new Vector2[verts.Length];
        var mid = Vector3.zero;
        for (int c = 0; c < cols; c++)
        {
            float azDeg = -180f + 360f * c / (cols - 1);
            float az = azDeg * Mathf.Deg2Rad, elMin = ElMin(azDeg) * Mathf.Deg2Rad;
            for (int r = 0; r <= rows; r++)
            {
                float el = r == 0 ? elMin : Mathf.Lerp(elMin, 90f * Mathf.Deg2Rad, (r - 1) / (float)(rows - 1));
                float noise = Mathf.PerlinNoise(azDeg * 0.045f + 3.1f, el * 2.3f + 0.7f) - 0.5f;
                float off = r == 0 ? -0.0025f : 0.0065f + 0.005f * FaceShape.Smooth01((el * Mathf.Rad2Deg - 20f) / 50f) + 0.0028f * noise;
                var nrm0 = f.Normal(az, el);
                var p = f.Point(az, el) + nrm0 * off;
                verts[r * cols + c] = p;
                uvs[r * cols + c] = new Vector2(az * 0.08f, p.y);
                if (r == rows / 2 && c == cols / 2) mid = nrm0;
            }
        }
        m = GridMesh(key, verts, uvs, rows + 1, cols);
        if (Vector3.Dot(m.normals[(rows / 2) * cols + cols / 2], mid) < 0) FlipWinding(m);
        var nrm = m.normals;
        for (int r = 0; r <= rows; r++)
        {
            int a = r * cols, b = a + cols - 1;
            var avg = (nrm[a] + nrm[b]).normalized;
            nrm[a] = nrm[b] = avg;
        }
        m.normals = nrm;
        m.RecalculateTangents();
        faceMeshes[key] = m;
        return m;
    }

    static Mesh GridMesh(string key, Vector3[] verts, Vector2[] uvs, int rows, int cols)
    {
        var tris = new List<int>((rows - 1) * (cols - 1) * 6);
        for (int r = 0; r < rows - 1; r++)
            for (int c = 0; c < cols - 1; c++)
            {
                int a = r * cols + c, b = a + 1, d = a + cols, e = d + 1;
                tris.Add(a); tris.Add(d); tris.Add(b);
                tris.Add(b); tris.Add(d); tris.Add(e);
            }
        var m = new Mesh { name = key, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        m.vertices = verts;
        m.uv = uvs;
        m.SetTriangles(tris, 0);
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    static void FlipWinding(Mesh m)
    {
        var tris = m.triangles;
        for (int t = 0; t < tris.Length; t += 3) { int k = tris[t + 1]; tris[t + 1] = tris[t + 2]; tris[t + 2] = k; }
        m.triangles = tris;
        m.RecalculateNormals();
    }

    // 매끄러운 머리: 얼굴 메시 + 입술·눈썹 조각 + 눈알(흰자·홍채·동공) + 귀 + 머리카락. 아래턱은 얼굴과 한 덩어리 (빈 관절만 둔다).
    // h.Face(FaceStyle)가 있으면 그 변주대로: 감은·반쯤 뜬 눈, 벌어진 입, 자국, 수염, 긴 머리
    static void SmoothHead(Builder b, Human h)
    {
        var head = b.Rig.Head;
        string k = b.Key;
        var st = h.Face;
        var f = StyledFace(st);
        float gap = st != null ? Mathf.Clamp01(st.MouthGap) : 0f;
        b.Part(head, FaceMesh(k + "_face", f), h.Skin, Vector3.zero);
        b.Rig.Jaw = b.Joint("Jaw", head, new Vector3(0, 0.085f, 0));
        b.Rig.Mouth = b.Joint("Mouth", head, f.FrontPoint(0f, MouthY));

        // 입술: 가운데가 살짝 파인 윗입술 활(조금 어둡다), 도톰한 아랫입술, 둘 사이의 어두운 입선
        const int lipCols = 28, lipRows = 4;
        var upper = new Vector2[lipRows + 1, lipCols + 1];
        var lower = new Vector2[lipRows + 1, lipCols + 1];
        var line = new Vector2[2, lipCols + 1];
        for (int c = 0; c <= lipCols; c++)
        {
            float u = -0.985f + 1.97f * c / lipCols;
            float x = u * 0.0225f;
            float top = MouthY + 0.0058f * Mathf.Pow(1 - u * u, 0.55f) - 0.0012f * Mathf.Exp(-(u / 0.2f) * (u / 0.2f));
            // 벌어진 입: 아랫입술이 내려가고 입선이 어두운 틈이 된다 (가운데가 가장 넓다)
            float drop = 0.0075f * gap * Mathf.Pow(1 - u * u, 0.5f);
            float bot = MouthY - 0.0068f * Mathf.Pow(1 - u * u, 0.75f) - drop;
            float seam = 0.0005f * Mathf.Pow(1 - u * u, 0.4f);
            for (int r = 0; r <= lipRows; r++)
            {
                upper[r, c] = new Vector2(x, Mathf.Lerp(MouthY + seam, top, r / (float)lipRows));
                lower[r, c] = new Vector2(x, Mathf.Lerp(bot, MouthY - seam - drop, r / (float)lipRows));
            }
            line[0, c] = new Vector2(x, MouthY - seam * 1.3f - drop);
            line[1, c] = new Vector2(x, MouthY + seam * 1.3f);
        }
        var lipU = st != null ? st.LipUpper : Hex("9a5f55");
        var lipD = st != null ? st.LipLower : Hex("ab6c60");
        b.Part(head, SurfacePatch(k + "_lipU", f, upper, 0.0007f), ProcTex.Plain(lipU, 0.42f), Vector3.zero, null, null, false);
        b.Part(head, SurfacePatch(k + "_lipD", f, lower, 0.0007f), ProcTex.Plain(lipD, 0.5f), Vector3.zero, null, null, false);
        b.Part(head, SurfacePatch(k + "_lipLine", f, line, gap > 0 ? 0.0006f : 0.0009f), ProcTex.Plain(gap > 0 ? Hex("1a0907") : Hex("3a1e1a"), 0.3f), Vector3.zero, null, null, false);
        if (gap > 0.25f)
        {
            // 틈 사이로 보이는 윗니: 윗입술 아래에 매달린 누르스름한 띠
            var teeth = new Vector2[2, 13];
            for (int c = 0; c < 13; c++)
            {
                float u = -1f + 2f * c / 12f;
                float x = u * 0.0145f;
                teeth[1, c] = new Vector2(x, MouthY + 0.0004f);
                teeth[0, c] = new Vector2(x, MouthY + 0.0004f - 0.0032f * gap * Mathf.Pow(1 - u * u, 0.3f));
            }
            b.Part(head, SurfacePatch(k + "_teeth", f, teeth, 0.0007f), ProcTex.Plain(Hex("d9cfb6"), 0.45f), Vector3.zero, null, null, false);
        }

        // 눈썹: 안쪽에서 솟았다가 바깥쪽으로 내려가는 활, 양 끝이 가늘다
        var browMat = h.Hair ?? ProcTex.Plain(Hex("2a2220"), 0.3f);
        foreach (var s in new[] { -1f, 1f })
        {
            const int bc = 16, br = 3;
            var brow = new Vector2[br + 1, bc + 1];
            for (int c = 0; c <= bc; c++)
            {
                float u = c / (float)bc;
                float x = s * (0.013f + 0.044f * u);
                float yc = 0.1655f + 0.0075f * Mathf.Sin(Mathf.PI * Mathf.Pow(u, 0.9f)) - 0.0045f * u;
                float taper = u < 0.12f ? u / 0.12f : u > 0.9f ? (1 - u) / 0.1f : 1f;
                float half = (0.0032f + 0.0013f * Mathf.Sin(Mathf.PI * u)) * Mathf.Max(0.15f, taper);
                for (int r = 0; r <= br; r++) brow[r, c] = new Vector2(x, yc + (r / (float)br * 2 - 1) * half);
            }
            b.Part(head, SurfacePatch(k + (s < 0 ? "_browL" : "_browR"), f, brow, 0.0009f), browMat, Vector3.zero, null, null, false);
        }

        // 눈: 우물 바닥보다 조금 솟은 눈알, 그 위에 홍채와 동공. 우물 가장자리가 눈꺼풀이 되어 아몬드 모양으로 보인다.
        // 감은 눈은 눈알 없이 눈꺼풀 사이의 속눈썹 선만, 반쯤 뜬 눈은 좁은 틈 사이로 흐린 눈알이 보인다
        var white = ProcTex.Plain(st != null ? st.EyeWhite : Hex("e9e3da"), st != null ? 0.6f : 0.92f);
        var iris = ProcTex.Plain(st != null ? st.Iris : Hex("3b2a1d"), 0.85f);
        var pupil = ProcTex.Plain(Hex("070504"), 0.9f);
        var lash = ProcTex.Plain(Hex("2a1c18"), 0.3f);
        foreach (var s in new[] { -1f, 1f })
        {
            float open = st == null ? 1f : (s < 0 ? st.EyeOpenL : st.EyeOpenR);
            f.Locate(s * 0.031f, 0.146f, out float az, out float el);
            var floor = f.Point(az, el);
            var n = f.Normal(az, el);
            var center = floor - n * 0.005f;
            // 시선은 정면: 홍채는 표면 법선이 아니라 앞을 본다 (법선대로 두면 양쪽 눈이 바깥을 본다). 시체는 초점 없이 조금 아래를 본다
            var gaze = Vector3.Lerp(n, st != null ? new Vector3(0f, -0.25f, 1f).normalized : Vector3.forward, 0.8f).normalized;
            var look = Quaternion.LookRotation(gaze).eulerAngles;
            if (open > 0.05f)
            {
                b.Part(head, ProcMesh.Ellipsoid("civ_eyeball", 0.0142f, 0.0142f, 0.0142f, 18, 12), white, center, null, null, false);
                b.Part(head, ProcMesh.Ellipsoid("civ_iris", 0.0064f, 0.0064f, 0.0016f, 16, 6), iris, center + gaze * 0.013f, look, null, false);
                b.Part(head, ProcMesh.Ellipsoid("civ_pupil", 0.0028f, 0.0028f, 0.001f, 12, 4), pupil, center + gaze * 0.0141f, look, null, false);
            }
            if (open < 0.95f)
            {
                // 눈꺼풀이 닿는 선: 바깥쪽이 조금 내려간 가는 활 (감았을수록 또렷하다)
                const int lc = 14;
                var lashPts = new Vector2[2, lc + 1];
                for (int c = 0; c <= lc; c++)
                {
                    float u = -1f + 2f * c / lc;
                    float x = s * (0.031f + u * 0.0135f);
                    float y = 0.1455f + 0.0018f * (1 - u * u) - u * 0.0012f;
                    float half = 0.00075f * Mathf.Lerp(1f, 0.6f, open) * Mathf.Pow(1 - u * u * 0.9f, 0.4f);
                    lashPts[0, c] = new Vector2(x, y - half);
                    lashPts[1, c] = new Vector2(x, y + half);
                }
                b.Part(head, SurfacePatch(k + (s < 0 ? "_lashL" : "_lashR"), f, lashPts, 0.0013f), lash, Vector3.zero, null, null, false);
            }
            // 귀: 머리에 반쯤 묻힌 타원 + 안쪽 오목한 부분 (뒤쪽으로 치우친다)
            b.Part(head, ProcMesh.Ellipsoid("civ_ear", 0.011f, 0.029f, 0.018f), h.Skin, new Vector3(s * 0.0735f, 0.138f, -0.009f), new Vector3(0, -s * 14f, -s * 6f));
            b.Part(head, ProcMesh.Ellipsoid("civ_earin", 0.005f, 0.016f, 0.01f), ProcTex.Plain(Hex("9c7257"), 0.35f), new Vector3(s * 0.08f, 0.136f, -0.012f), new Vector3(0, -s * 14f, -s * 6f), null, false);
        }
        if (h.Hair != null)
        {
            b.Part(head, HairMesh(k + "_hair", f), h.Hair, Vector3.zero);
            if (h.LongHair)
            {
                // 어깨까지 늘어진 긴 머리와 앞머리
                b.Part(head, Tube(k + "_longhair", -0.10f, 0.09f, 0.05f, -0.065f, -0.02f, 0.085f, 0.06f, -0.06f, 0.08f, 0.082f, 0.07f, -0.05f, 0.16f, 0.08f, 0.08f, -0.04f), h.Hair, Vector3.zero);
                b.Part(head, Tube(k + "_bangs", 0.10f, 0.03f, 0.02f, 0.085f, 0.2f, 0.06f, 0.03f, 0.08f), h.Hair, new Vector3(-0.035f, 0, 0), new Vector3(0, 0, 18));
            }
        }
        if (st == null) return;
        // 수염 그루터기: 윗입술·턱·양 볼 아래를 덮는 얇은 조각
        if (st.Stubble)
        {
            var stubble = ProcTex.Get(ProcTex.Kind.Hair, Hex("6f6a64"), 311, 0, 0.5f);
            b.Part(head, EllipsePatch(k + "_stubChin", f, 0f, 0.034f, 0.046f, 0.02f, 0f, 0.0006f, 7, 13), stubble, Vector3.zero, null, null, false);
            b.Part(head, EllipsePatch(k + "_stubLip", f, 0f, 0.08f, 0.022f, 0.008f, 0f, 0.0006f, 5, 9), stubble, Vector3.zero, null, null, false);
            foreach (var s in new[] { -1f, 1f })
                b.Part(head, EllipsePatch(k + (s < 0 ? "_stubL" : "_stubR"), f, s * 0.048f, 0.075f, 0.016f, 0.034f, s * 12f, 0.0006f, 9, 7), stubble, Vector3.zero, null, null, false);
        }
        // 자국: 마른 피(검붉고 조금 반질), 멍(보랏빛 회색, 무광), 베인 상처(검은 홈 위의 어두운 띠)
        for (int i = 0; i < st.Marks.Count; i++)
        {
            var m = st.Marks[i];
            int kind = (int)m[5];
            var mat = kind == 0 ? ProcTex.Plain(Hex("4a100c"), 0.55f) : kind == 1 ? ProcTex.Plain(Hex("55414f"), 0.22f) : ProcTex.Plain(Hex("2a0a08"), 0.5f);
            float off = kind == 1 ? 0.0005f : kind == 2 ? 0.0005f : 0.0009f; // 상처는 홈 바닥 바로 위에
            b.Part(head, EllipsePatch(k + "_mark" + i, f, m[0], m[1], m[2], m[3], m[4], off), mat, Vector3.zero, null, null, false);
        }
    }
}
