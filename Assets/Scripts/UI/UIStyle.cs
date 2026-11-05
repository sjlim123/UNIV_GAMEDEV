using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// UI 그림을 전부 코드로 만든다. 사각형(채우기·테두리)은 9-slice로 늘려 쓰고,
// 대피소 서류 스타일 그림(종이·뜯긴 종이·테이프·녹슨 명판·나사·전구 빛·연필선)은 타일(Tiled)로 깔거나 그대로 붙인다
public static class UIStyle
{
    // ---------- 대피소 서류 스타일 ----------
    static Sprite paper, torn, tape, plate, screw, glow, pencil;

    // 바랜 종이 (타일로 깐다. 가장자리 1px는 종이 끝 그늘). 색은 Image.color로 입힌다
    public static Sprite Paper => paper != null ? paper : paper = MakePaper();
    // 가장자리를 손으로 뜯은 종이 (쪽지·꼬리표). 테두리 14px가 뜯긴 모양이고 Tiled로 깔면 긴 쪽지도 끊기지 않는다
    public static Sprite Torn => torn != null ? torn : torn = MakeTorn();
    // 마스킹 테이프 한 조각 (양 끝이 뜯겨 있다). 색을 입혀 베이지·노란 절연테이프로 쓴다
    public static Sprite Tape => tape != null ? tape : tape = MakeTape();
    // 녹슨 강철 명판 (색을 그림에 넣어 두었으니 Image.color는 흰색)
    public static Sprite Plate => plate != null ? plate : plate = MakePlate();
    // 명판 귀퉁이 나사
    public static Sprite Screw => screw != null ? screw : screw = MakeScrew();
    // 가운데가 밝고 바깥으로 사라지는 빛 (계기 전구)
    public static Sprite Glow => glow != null ? glow : glow = MakeGlow();
    // 연필로 그은 밑줄 (가로로 늘려 쓴다)
    public static Sprite Pencil => pencil != null ? pencil : pencil = MakePencil();
    // 유리 위의 긁힘·먼지·얼룩 (흰색, 알파만 있다 — 타일로 깔고 낮은 알파로 입힌다)
    static Sprite grime;
    public static Sprite Grime => grime != null ? grime : grime = MakeGrime();
    // 피격 방향 호: 화면 가장자리의 둥근 띠 중 위쪽 ±45도만 보이는 그림 (맞은 방향으로 돌려 쓴다)
    static Sprite hitArc;
    public static Sprite HitArc => hitArc != null ? hitArc : hitArc = MakeHitArc();

    static Sprite MakeHitArc()
    {
        const int size = 256;
        var tex = NewTex("UIHitArc", size, size, TextureWrapMode.Clamp);
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2 - 1, dy = (y + 0.5f) / size * 2 - 1;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = 0;
                if (r > 0.68f && r <= 1f)
                {
                    float ang = Mathf.Abs(Mathf.Atan2(dx, dy)) * Mathf.Rad2Deg; // 0 = 위
                    float radial = Mathf.Clamp01((r - 0.68f) / 0.32f);
                    float angular = Mathf.Clamp01(1 - ang / 48f);
                    a = radial * radial * angular * angular;
                }
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        return Finish(tex, px, Vector4.zero);
    }

    // 격자 점 해시 (0~1)
    static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            int h = x * 374761393 + y * 668265263 + seed * 1274126177;
            h = (h ^ (h >> 13)) * 1274126177;
            return ((h ^ (h >> 16)) & 0xffff) / 65535f;
        }
    }

    // 값 노이즈: 격자 점의 해시를 부드럽게 잇는다. period가 있으면 그 칸 수마다 되풀이돼 타일로 이어 붙여도 끊기지 않는다
    static float Noise(float x, float y, int seed, int period = 0)
    {
        int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
        float fx = x - ix, fy = y - iy;
        fx = fx * fx * (3 - 2 * fx);
        fy = fy * fy * (3 - 2 * fy);
        int ix1 = ix + 1, iy1 = iy + 1;
        if (period > 0) { ix = Mod(ix, period); iy = Mod(iy, period); ix1 = Mod(ix1, period); iy1 = Mod(iy1, period); }
        float a = Hash(ix, iy, seed), b = Hash(ix1, iy, seed), c = Hash(ix, iy1, seed), d = Hash(ix1, iy1, seed);
        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
    }

    static int Mod(int a, int n) => ((a % n) + n) % n;

    // 타일 크기 size 안에서 cells칸마다 되풀이되는 노이즈
    static float TileNoise(int x, int y, int size, int cells, int seed) => Noise(x * cells / (float)size, y * cells / (float)size, seed, cells);

    // 되풀이되는 1차원 요철 0~1 (뜯긴 가장자리). u가 0→1로 갈 때 한 바퀴
    static float Wave(float u, int seed)
    {
        float t = u * 6.2832f;
        return 0.5f + 0.25f * Mathf.Sin(t * 3 + seed) + 0.15f * Mathf.Sin(t * 7 + seed * 1.7f) + 0.1f * Mathf.Sin(t * 13 + seed * 2.3f);
    }

    static Texture2D NewTex(string name, int w, int h, TextureWrapMode wrap = TextureWrapMode.Repeat)
        => new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = wrap, filterMode = FilterMode.Bilinear, name = name };

    static Sprite Finish(Texture2D tex, Color32[] px, Vector4 border)
    {
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, border);
    }

    // 종이 결: 잔 알갱이 + 넓은 얼룩. 1에 가까운 밝기라 색을 곱하면 그 색의 종이가 된다
    static float Grain(int x, int y, int size, int seed)
        => 1f - 0.06f * TileNoise(x, y, size, size / 3, seed) - 0.05f * TileNoise(x, y, size, 4, seed + 1) - 0.025f * Hash(x, y, seed + 2);

    static Sprite MakePaper()
    {
        const int size = 128;
        var tex = NewTex("UIPaper", size, size);
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float g = Grain(x, y, size, 101);
                int edge = Mathf.Min(Mathf.Min(x, y), Mathf.Min(size - 1 - x, size - 1 - y));
                if (edge == 0) g *= 0.8f; else if (edge == 1) g *= 0.92f;
                byte v = (byte)(Mathf.Clamp01(g) * 255);
                px[y * size + x] = new Color32(v, v, v, 255);
            }
        return Finish(tex, px, new Vector4(8, 8, 8, 8));
    }

    static Sprite MakeTorn()
    {
        const int size = 96, border = 14;
        var tex = NewTex("UITorn", size, size);
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float g = Grain(x, y, size, 111);
                // 네 변에서 안쪽으로 뜯긴 깊이 (3~10px). 변을 따라 되풀이되는 요철이라 타일로 이어도 끊기지 않는다
                float u = x / (float)size, v = y / (float)size;
                float cutB = 3 + 7 * Wave(u, 1), cutT = 3 + 7 * Wave(u, 2), cutL = 3 + 7 * Wave(v, 3), cutR = 3 + 7 * Wave(v, 4);
                float d = Mathf.Min(Mathf.Min(y + 0.5f - cutB, size - y - 0.5f - cutT), Mathf.Min(x + 0.5f - cutL, size - x - 0.5f - cutR));
                float a = Mathf.Clamp01(d + 0.5f);
                // 뜯긴 자리 바로 안쪽은 종이 속살(조금 밝고 뿌옇게), 그 옆은 그늘
                if (d < 1.5f) g = Mathf.Min(1f, g + 0.06f);
                else if (d < 3f) g *= 0.94f;
                byte val = (byte)(Mathf.Clamp01(g) * 255);
                px[y * size + x] = new Color32(val, val, val, (byte)(a * 255));
            }
        return Finish(tex, px, new Vector4(border, border, border, border));
    }

    static Sprite MakeTape()
    {
        const int w = 128, h = 32;
        var tex = NewTex("UITape", w, h, TextureWrapMode.Clamp);
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float v = y / (float)h;
                float cutL = 2 + 6 * Wave(v, 7), cutR = w - 2 - 6 * Wave(v, 8);
                float a = Mathf.Clamp01(Mathf.Min(x + 0.5f - cutL, cutR - x - 0.5f) + 0.5f);
                // 가로 섬유 결 + 넓은 얼룩, 위아래 가장자리는 살짝 어둡게
                float g = 1f - 0.08f * Noise(x * 0.5f, y * 2.5f, 121) - 0.05f * Noise(x * 0.12f, y * 0.3f, 122) - 0.02f * Hash(x, y, 123);
                if (y == 0 || y == h - 1) g *= 0.86f;
                byte val = (byte)(Mathf.Clamp01(g) * 255);
                // 테이프는 살짝 비친다
                px[y * w + x] = new Color32(val, val, val, (byte)(a * 0.9f * 255));
            }
        return Finish(tex, px, Vector4.zero);
    }

    static Sprite MakePlate()
    {
        const int size = 96, border = 10;
        var tex = NewTex("UIPlate", size, size);
        var px = new Color32[size * size];
        var steel = new Color(0.30f, 0.315f, 0.33f);
        var rust = new Color(0.40f, 0.21f, 0.11f);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // 쇠 결 + 가는 긁힘(비스듬한 줄) + 드문드문 녹 얼룩 (큰 얼룩과 잔 얼룩을 겹쳐 격자처럼 보이지 않게)
                float shade = 0.86f + 0.12f * TileNoise(x, y, size, 24, 131) + 0.1f * TileNoise(x, y, size, 6, 132);
                float scratch = Noise(x * 0.9f + y * 0.35f, y * 0.06f, 133);
                if (scratch > 0.8f) shade += 0.12f;
                var c = steel * shade;
                float r = 0.6f * TileNoise(x, y, size, 3, 134) + 0.4f * TileNoise(x, y, size, 11, 135);
                float rustAmt = Mathf.SmoothStep(0f, 1f, (r - 0.6f) / 0.14f) * 0.8f;
                c = Color.Lerp(c, rust * (0.8f + 0.4f * Hash(x, y, 136)), rustAmt);
                // 가장자리 비스듬한 면: 왼쪽·위는 밝고 오른쪽·아래는 어둡다. 안쪽 6px에는 눌린 홈 선
                int l = x, b = y, rgt = size - 1 - x, t = size - 1 - y;
                int edge = Mathf.Min(Mathf.Min(l, b), Mathf.Min(rgt, t));
                if (edge <= 1)
                {
                    bool lit = l <= 1 || t <= 1;
                    c *= lit ? 1.35f : 0.6f;
                }
                else if (edge == 6) c *= 0.72f;
                else if (edge == 7) c *= 1.08f;
                px[y * size + x] = new Color32((byte)(Mathf.Clamp01(c.r) * 255), (byte)(Mathf.Clamp01(c.g) * 255), (byte)(Mathf.Clamp01(c.b) * 255), 255);
            }
        return Finish(tex, px, new Vector4(border, border, border, border));
    }

    static Sprite MakeScrew()
    {
        const int size = 16;
        var tex = NewTex("UIScrew", size, size, TextureWrapMode.Clamp);
        var px = new Color32[size * size];
        float half = size / 2f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - half, dy = y + 0.5f - half;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(half - 1f - r + 0.5f);
                // 머리: 가운데 어둡고 테두리는 밝은 쇠. 가운데 일자 홈
                float g = r > half - 2.6f ? 0.62f : 0.3f + 0.1f * (dy / half);
                if (Mathf.Abs(dy) < 1.1f && Mathf.Abs(dx) < half - 2.8f) g = 0.1f;
                byte val = (byte)(g * 255);
                px[y * size + x] = new Color32(val, val, val, (byte)(a * 255));
            }
        return Finish(tex, px, Vector4.zero);
    }

    static Sprite MakeGlow()
    {
        const int size = 32;
        var tex = NewTex("UIGlow", size, size, TextureWrapMode.Clamp);
        var px = new Color32[size * size];
        float half = size / 2f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - half, dy = y + 0.5f - half;
                float r = Mathf.Sqrt(dx * dx + dy * dy) / (half - 1);
                float a = Mathf.Clamp01(1 - r);
                a = a * a * (3 - 2 * a);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        return Finish(tex, px, Vector4.zero);
    }

    static Sprite MakePencil()
    {
        const int w = 128, h = 6;
        var tex = NewTex("UIPencil", w, h, TextureWrapMode.Clamp);
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                // 살짝 구불거리는 선, 흑연이 고르지 않게 묻는다
                float mid = 3f + 0.8f * Mathf.Sin(x * 0.21f) + 0.4f * Mathf.Sin(x * 0.57f + 1f);
                float a = Mathf.Clamp01(1.4f - Mathf.Abs(y + 0.5f - mid));
                a *= 0.55f + 0.45f * Noise(x * 0.5f, 0, 141);
                px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        return Finish(tex, px, Vector4.zero);
    }

    static Sprite MakeGrime()
    {
        // 타일이 눈에 띄게 되풀이되지 않도록 크게(256px) 만들고, 긁힘은 드문드문 길게, 먼지는 옅게
        const int size = 256;
        var tex = NewTex("UIGrime", size, size);
        var px = new Color32[size * size];
        const int lineCount = 5;
        var lines = new float[lineCount, 4];
        for (int k = 0; k < lineCount; k++)
        {
            float ang = Hash(k, 0, 151) * 3.1416f;
            lines[k, 0] = Hash(k, 1, 152) * size; lines[k, 1] = Hash(k, 2, 153) * size;
            lines[k, 2] = Mathf.Cos(ang); lines[k, 3] = Mathf.Sin(ang);
        }
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float a = 0f;
                // 넓은 얼룩 (아주 옅게)
                a += 0.07f * TileNoise(x, y, size, 3, 154) * TileNoise(x, y, size, 9, 155);
                // 먼지: 군데군데 모여 있는 점
                float cluster = TileNoise(x, y, size, 6, 156);
                float speck = Hash(x, y, 157);
                if (speck > 0.992f && cluster > 0.55f) a += 0.15f + 0.2f * Hash(x, y, 158);
                // 긁힘 (끝은 옅어진다)
                for (int k = 0; k < lineCount; k++)
                {
                    float dx = x + 0.5f - lines[k, 0], dy = y + 0.5f - lines[k, 1];
                    float along = dx * lines[k, 2] + dy * lines[k, 3];
                    float across = Mathf.Abs(-dx * lines[k, 3] + dy * lines[k, 2]);
                    float len = 40 + 60 * Hash(k, 3, 159);
                    if (Mathf.Abs(along) < len && across < 0.8f)
                        a += 0.2f * (1 - across / 0.8f) * (1 - Mathf.Abs(along) / len) * (0.5f + 0.5f * Noise(along * 0.2f, k, 160));
                }
                px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255));
            }
        return Finish(tex, px, Vector4.zero);
    }

    // ---------- 유리 HUD · 먹 번짐 · 붓 결 (2026-11-05) ----------
    static Sprite glassSoft, glassPill, glassLight, inkWash;

    // 가장자리가 밖으로 갈수록 투명해지는 둥근 판 (9-slice, 테두리 40px가 페더). HudGlass 재질과 함께 쓴다
    public static Sprite GlassSoft => glassSoft != null ? glassSoft : glassSoft = MakeGlassSoft();
    // 알약 유리판: A = 모양(가장자리 1.5px 안티앨리어스), R = 비스듬한 면 높이(가장자리 0 → 안쪽 1, 셰이더 굴절용)
    public static Sprite GlassPill => glassPill != null ? glassPill : glassPill = MakeGlassPill();
    // 알약 유리판 위의 빛: 바깥 테두리 반사선(위쪽이 밝다) + 윗면 광택 + 바닥 가장자리 반사 (흰색, 알파만)
    public static Sprite GlassLight => glassLight != null ? glassLight : glassLight = MakeGlassLight();
    // 먹이 번진 자국 (손전등·배터리 뒤 배경). 색은 Image.color로
    public static Sprite InkWash => inkWash != null ? inkWash : inkWash = MakeInkWash();

    static Sprite MakeGlassSoft()
    {
        const int size = 128, border = 40;
        var tex = NewTex("UIGlassSoft", size, size, TextureWrapMode.Clamp);
        var px = new Color32[size * size];
        float half = size / 2f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // 둥근 사각형(반지름 = border)의 가장자리에서 안쪽으로 들어온 거리 (0~border)
                float dx = Mathf.Max(Mathf.Abs(x + 0.5f - half) - (half - border), 0);
                float dy = Mathf.Max(Mathf.Abs(y + 0.5f - half) - (half - border), 0);
                float d = border - Mathf.Sqrt(dx * dx + dy * dy);
                float t = Mathf.Clamp01(d / border);
                float a = t * t * (3 - 2 * t);     // 바깥은 투명, 가운데로 갈수록 진하다
                a = Mathf.Pow(a, 1.35f);           // 바깥쪽을 조금 더 오래 옅게
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        return Finish(tex, px, new Vector4(border, border, border, border));
    }

    static Sprite MakeGlassPill()
    {
        const int size = 64, border = 28;
        const float radius = 24f, bevel = 12f;
        // 높이(R)를 선형으로 읽어야 하므로 선형 텍스처로 만든다
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "UIGlassPill" };
        var px = new Color32[size * size];
        float half = size / 2f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(Mathf.Abs(x + 0.5f - half) - (half - radius), 0);
                float dy = Mathf.Max(Mathf.Abs(y + 0.5f - half) - (half - radius), 0);
                float d = radius - Mathf.Sqrt(dx * dx + dy * dy); // 안쪽이 양수
                float a = Mathf.Clamp01(d + 0.5f);
                float h = Mathf.Sin(Mathf.Clamp01(d / bevel) * Mathf.PI * 0.5f); // 렌즈처럼 가장자리는 가파르고 안쪽은 평평하다
                byte hv = (byte)(h * 255);
                px[y * size + x] = new Color32(hv, hv, hv, (byte)(a * 255));
            }
        return Finish(tex, px, new Vector4(border, border, border, border));
    }

    static Sprite MakeGlassLight()
    {
        const int size = 64, border = 28;
        const float radius = 24f;
        var tex = NewTex("UIGlassLight", size, size, TextureWrapMode.Clamp);
        var px = new Color32[size * size];
        float half = size / 2f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float cx = x + 0.5f - half, cy = y + 0.5f - half;
                float dx = Mathf.Max(Mathf.Abs(cx) - (half - radius), 0);
                float dy = Mathf.Max(Mathf.Abs(cy) - (half - radius), 0);
                float d = radius - Mathf.Sqrt(dx * dx + dy * dy);
                float cover = Mathf.Clamp01(d + 0.5f);
                // 빛은 왼쪽 위에서 온다: 테두리 반사선은 위·왼쪽이 밝고 아래·오른쪽은 흐리다
                float lit = Mathf.Clamp01(0.55f + 0.45f * (cy / half * 0.85f - cx / half * 0.3f));
                float rim = Mathf.Clamp01(1.6f - Mathf.Abs(d - 1.1f)) * (0.35f + 0.65f * lit);
                float inner = Mathf.Clamp01(1 - d / 7f) * 0.22f * lit;
                // 윗면 광택: 위 가장자리에서 아래로 18px 동안 옅어진다
                float sheen = Mathf.Clamp01(1 - (size - 1 - y) / 18f);
                sheen = sheen * sheen * 0.5f;
                // 바닥 가장자리의 가는 반사 (빛이 아랫면에서 꺾여 나온다)
                float bottom = Mathf.Clamp01(1 - Mathf.Abs(y - 2f) / 2f) * 0.22f;
                float a = Mathf.Clamp01(rim * 0.9f + inner + sheen + bottom) * cover;
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        return Finish(tex, px, new Vector4(border, border, border, border));
    }

    static Sprite MakeInkWash()
    {
        const int w = 320, h = 160;
        var tex = NewTex("UIInkWash", w, h, TextureWrapMode.Clamp);
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w * 2 - 1, v = (y + 0.5f) / h * 2 - 1;
                float ang = Mathf.Atan2(v, u);
                // 타원 반지름을 각도에 따라 울퉁불퉁하게 (큰 굴곡 + 잔 굴곡)
                float wob = 0.78f + 0.12f * Mathf.Sin(ang * 3 + 0.7f) + 0.06f * Mathf.Sin(ang * 7 + 2.1f) + 0.04f * Mathf.Sin(ang * 11 + 4f);
                float r = Mathf.Sqrt(u * u + v * v) / wob; // 1 = 자국 가장자리
                // 번진 가장자리: 종이 섬유를 따라 들쭉날쭉
                float fiber = Noise(x * 0.35f, y * 0.35f, 171) * 0.5f + Noise(x * 0.9f, y * 0.9f, 172) * 0.5f;
                float edge = r + (fiber - 0.5f) * 0.22f;
                float a = Mathf.Clamp01((1.08f - edge) / 0.3f); // 안쪽 1 → 가장자리 0
                a = a * a * (3 - 2 * a);
                // 먹의 농담: 가운데는 진하고 군데군데 옅은 얼룩, 가장자리에 살짝 진한 테 (마르면서 생긴 자국)
                float pool = 0.72f + 0.28f * Noise(x * 0.04f, y * 0.04f, 173);
                float ring = Mathf.Clamp01(1 - Mathf.Abs(edge - 0.92f) / 0.12f) * 0.22f;
                a *= Mathf.Clamp01(pool + ring);
                px[y * w + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255));
            }
        return Finish(tex, px, Vector4.zero);
    }

    // ---------- 더러운 강판 메뉴 (시안 B) ----------
    static Sprite wornFrame, dust;

    // 군데군데 칠이 벗겨진 가는 테두리 (9-slice, Tiled로 쓰면 벗겨진 자국이 늘어나지 않는다). 색은 Image.color
    public static Sprite WornFrame => wornFrame != null ? wornFrame : wornFrame = MakeWornFrame();
    // 뿌연 흙먼지 구름 (타일, 흰색 알파). 흙색을 입혀 판 위에 깐다
    public static Sprite Dust => dust != null ? dust : dust = MakeDust();

    static Sprite MakeWornFrame()
    {
        const int size = 48, border = 8;
        var tex = NewTex("UIWornFrame", size, size);
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int edge = Mathf.Min(Mathf.Min(x, y), Mathf.Min(size - 1 - x, size - 1 - y));
                // 바깥 1.5px 선이 노이즈를 따라 군데군데 끊기고 옅어진다 (벗겨진 칠)
                float line = edge == 0 ? 1f : edge == 1 ? 0.5f : 0f;
                float wear = Noise(x * 0.35f, y * 0.35f, 191) * 0.6f + Noise(x * 1.3f, y * 1.3f, 192) * 0.4f;
                float a = line * Mathf.Clamp01((wear - 0.22f) / 0.5f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        return Finish(tex, px, new Vector4(border, border, border, border));
    }

    static Sprite MakeDust()
    {
        const int size = 128;
        var tex = NewTex("UIDust", size, size);
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // 넓은 얼룩 구름 세 옥타브 + 잔 알갱이. 타일로 이어도 끊기지 않는다
                float n = 0.55f * TileNoise(x, y, size, 3, 193) + 0.3f * TileNoise(x, y, size, 7, 194) + 0.15f * TileNoise(x, y, size, 17, 195);
                float a = Mathf.Clamp01((n - 0.38f) / 0.45f);
                a = a * a * (0.9f + 0.1f * Hash(x, y, 196));
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        return Finish(tex, px, Vector4.zero);
    }

    // ---------- 알약(캡슐) ----------
    static Sprite capsule, capsuleFrame;

    // 알약 모양 채우기·테두리 (64×32, 양 끝 반지름 16, 9-slice 가로 테두리 16). pixelsPerUnitMultiplier = 32 / 높이로 쓰면 양 끝이 정확한 반원이 된다
    public static Sprite Capsule => capsule != null ? capsule : capsule = MakeCapsule(false);
    public static Sprite CapsuleFrame => capsuleFrame != null ? capsuleFrame : capsuleFrame = MakeCapsule(true);

    static Sprite MakeCapsule(bool outline)
    {
        const int w = 64, h = 32, border = 16;
        const float radius = 15.5f, width = 1.5f;
        var tex = NewTex(outline ? "UICapsuleFrame" : "UICapsule", w, h, TextureWrapMode.Clamp);
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                // 두 끝 원 중심을 잇는 선분까지의 거리 (안쪽이 음수)
                float dx = Mathf.Max(Mathf.Abs(x + 0.5f - w / 2f) - (w / 2f - 16f), 0);
                float dy = Mathf.Abs(y + 0.5f - h / 2f);
                float d = Mathf.Sqrt(dx * dx + dy * dy) - radius;
                float a = outline ? Mathf.Clamp01(0.5f - d) * Mathf.Clamp01(d + width + 0.5f) : Mathf.Clamp01(0.5f - d);
                px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        return Finish(tex, px, new Vector4(border, 0, border, 0));
    }

    // ---------- 크레파스 선 · 핏자국 (2026-11-05 저녁) ----------
    static Sprite crayon, bloodWash;

    // 크레파스로 한 번 그은 선 (512×28, 선 굵기 7~11px, 가로 9-slice 12px). Tiled로 쓰면 가운데 결이 늘어나지 않고 양 끝은 뭉툭한 크레파스 끝으로 남는다. 색은 Image.color
    // (세로로 늘리면 선이 두 줄로 되풀이되므로 굵기는 여기서 바꾼다. 11-05 저녁 사용자 요청으로 처음의 2배)
    public static Sprite Crayon => crayon != null ? crayon : crayon = MakeCrayon();
    // 붉은 물감이 번진 핏자국 (사망 화면 뒷배경): 큰 자국 + 둘레에 튄 방울 + 아래로 흐른 줄기. 색은 Image.color
    public static Sprite BloodWash => bloodWash != null ? bloodWash : bloodWash = MakeBloodWash();

    static Sprite MakeCrayon()
    {
        const int w = 512, h = 28, border = 12;
        var tex = NewTex("UICrayon", w, h, TextureWrapMode.Clamp);
        var px = new Color32[w * h];
        float mid = h / 2f;
        for (int x = 0; x < w; x++)
        {
            // 손이 흔들려 선의 가운데와 굵기가 조금씩 오르내린다 (가로로 되풀이되는 노이즈라 타일로 이어도 끊기지 않는다)
            float center = mid + (Noise(x * 12f / w, 0.37f, 211, 12) - 0.5f) * 3.5f;
            float half = 4.2f + (Noise(x * 20f / w, 0.61f, 212, 20) - 0.5f) * 2.4f;
            // 양 끝은 뭉툭하게 가늘어진다
            float end = Mathf.Min(x + 1, w - x) / (float)border;
            if (end < 1f) half *= Mathf.Sqrt(Mathf.Clamp01(end));
            for (int y = 0; y < h; y++)
            {
                float d = Mathf.Abs(y + 0.5f - center) - half + (Hash(x, y, 213) - 0.5f) * 1.4f; // 가장자리가 우둘투둘
                float edge = Mathf.Clamp01(0.5f - d);
                // 왁스 알갱이: 종이 결을 따라 군데군데 옅고, 드문드문 빈 자리가 난다
                float grain = 0.55f + 0.45f * Noise(x * 64f / w, y * 0.7f, 214, 64);
                grain *= 0.8f + 0.2f * Hash(x, y, 215);
                if (Noise(x * 40f / w, y * 0.5f, 216, 40) > 0.82f) grain *= 0.35f;
                float a = edge * grain;
                if (d > 0) a = Mathf.Max(a, Mathf.Clamp01(1 - d / 3f) * 0.16f * grain); // 선 바깥으로 살짝 번진 왁스
                px[y * w + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255));
            }
        }
        return Finish(tex, px, new Vector4(border, 0, border, 0));
    }

    static Sprite MakeBloodWash()
    {
        const int size = 256, drops = 16, drips = 3;
        var tex = NewTex("UIBloodWash", size, size, TextureWrapMode.Clamp);
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2 - 1, v = (y + 0.5f) / size * 2 - 1;
                float ang = Mathf.Atan2(v, u);
                // 큰 자국: 각도에 따라 울퉁불퉁한 반지름 (물감이 튄 가장자리)
                float wob = 0.58f + 0.1f * Mathf.Sin(ang * 4 + 1.3f) + 0.07f * Mathf.Sin(ang * 9 + 0.4f) + 0.05f * Mathf.Sin(ang * 15 + 2.2f) + 0.035f * Mathf.Sin(ang * 23 + 5.1f);
                float r = Mathf.Sqrt(u * u + v * v) / wob;
                float fiber = Noise(x * 0.22f, y * 0.22f, 221) * 0.6f + Noise(x * 0.75f, y * 0.75f, 222) * 0.4f;
                float edge = r + (fiber - 0.5f) * 0.3f;
                float a = Mathf.Clamp01((1f - edge) / 0.1f); // 물감이라 먹보다 가장자리가 또렷하다
                // 둘레에 튄 방울들 (자리·크기는 고정 난수)
                for (int k = 0; k < drops; k++)
                {
                    float da = k / (float)drops * 6.2832f + Hash(k, 1, 224) * 0.4f;
                    float dr = 0.5f + 0.42f * Hash(k, 2, 224);
                    float ds = 0.018f + 0.05f * Hash(k, 3, 224) * Hash(k, 4, 224);
                    float dx = u - Mathf.Cos(da) * dr, dy = v - Mathf.Sin(da) * dr;
                    a = Mathf.Max(a, Mathf.Clamp01((ds - Mathf.Sqrt(dx * dx + dy * dy)) / 0.012f));
                }
                // 아래로 흐른 줄기 (자국 아래쪽에서 시작해 가늘어지며 내려가고 끝에 방울이 맺힌다)
                for (int k = 0; k < drips; k++)
                {
                    float sx = -0.32f + 0.62f * Hash(k, 5, 225), len = 0.25f + 0.4f * Hash(k, 6, 225), wd = 0.028f + 0.022f * Hash(k, 7, 225);
                    float top = -0.45f * wob, bottom = top - len;
                    if (v < top && v > bottom)
                    {
                        float t = (top - v) / len;
                        float width = wd * (1f - 0.55f * t) + (Noise(y * 0.3f, k * 7f, 226) - 0.5f) * 0.012f;
                        a = Mathf.Max(a, Mathf.Clamp01((width - Mathf.Abs(u - sx)) / 0.012f));
                    }
                    float ex = u - sx, ey = v - bottom;
                    a = Mathf.Max(a, Mathf.Clamp01((wd * 1.4f - Mathf.Sqrt(ex * ex + ey * ey)) / 0.012f));
                }
                // 농담: 가운데가 진하고 군데군데 옅은 얼룩, 가장자리 안쪽에 조금 더 진한 테
                float pool = 0.78f + 0.22f * Noise(x * 0.05f, y * 0.05f, 223);
                float ring = Mathf.Clamp01(1 - Mathf.Abs(edge - 0.9f) / 0.1f) * 0.22f;
                a *= Mathf.Clamp01(pool + ring);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255));
            }
        return Finish(tex, px, Vector4.zero);
    }

    // ---------- 할 일 판 마름모 표식 (2026-11-05 저녁) ----------
    static Sprite diamond, diamondFilled;

    // 속이 빈 마름모 (할 일) / 테두리 안에 작은 마름모가 찬 것 (끝낸 일). 32×32, 색은 Image.color
    public static Sprite Diamond => diamond != null ? diamond : diamond = MakeDiamond(false);
    public static Sprite DiamondFilled => diamondFilled != null ? diamondFilled : diamondFilled = MakeDiamond(true);

    static Sprite MakeDiamond(bool filled)
    {
        const int size = 32;
        var tex = NewTex(filled ? "UIDiamondFilled" : "UIDiamond", size, size, TextureWrapMode.Clamp);
        var px = new Color32[size * size];
        float half = size / 2f, radius = half - 2.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Abs(x + 0.5f - half) + Mathf.Abs(y + 0.5f - half) - radius; // 마름모 가장자리까지 (안쪽 음수)
                float a = Mathf.Clamp01(1.6f - Mathf.Abs(d));          // 테두리 선 (약 2px)
                if (filled) a = Mathf.Max(a, Mathf.Clamp01(-d - 4.5f)); // 안쪽 작은 마름모
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        return Finish(tex, px, Vector4.zero);
    }

    // ---------- 기본 사각형 ----------

    static Sprite fill, frame;

    static Sprite fade;

    public static Sprite Fill => fill != null ? fill : fill = Make(false);
    public static Sprite Frame => frame != null ? frame : frame = Make(true);

    // 왼쪽은 진하고 오른쪽으로 갈수록 투명해지는 그라데이션 (메뉴 배경 가림막·메뉴 버튼 빛)
    public static Sprite HorizontalFade
    {
        get
        {
            if (fade != null) return fade;
            var tex = new Texture2D(256, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "UIFade" };
            for (int x = 0; x < 256; x++)
            {
                float a = 1f - Mathf.SmoothStep(0f, 1f, x / 255f);
                tex.SetPixel(x, 0, new Color(1, 1, 1, a));
            }
            tex.Apply();
            return fade = Sprite.Create(tex, new Rect(0, 0, 256, 1), new Vector2(0.5f, 0.5f));
        }
    }

    static Sprite ring, dot, vignette;

    // 가운데는 투명하고 가장자리로 갈수록 짙어지는 화면 테두리 (붉은 밤)
    public static Sprite Vignette
    {
        get
        {
            if (vignette != null) return vignette;
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "UIVignette" };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2 - 1, dy = (y + 0.5f) / size * 2 - 1;
                    float d = Mathf.Sqrt(dx * dx * 0.8f + dy * dy);
                    float a = Mathf.Clamp01((d - 0.45f) / 0.75f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * a * 255));
                }
            tex.SetPixels32(px);
            tex.Apply();
            return vignette = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }
    }

    // 원 조준점: 가는 고리 (64px, 가장자리를 부드럽게)
    public static Sprite Ring => ring != null ? ring : ring = Circle("UIRing", 64, 3.2f);
    // 점 조준점: 꽉 찬 원
    public static Sprite Dot => dot != null ? dot : dot = Circle("UIDot", 32, 0f);

    // thickness가 0이면 꽉 찬 원, 아니면 그 두께의 고리
    static Sprite Circle(string name, int size, float thickness)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = name };
        var pixels = new Color32[size * size];
        float half = size / 2f, outer = half - 1f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float r = Mathf.Sqrt((x + 0.5f - half) * (x + 0.5f - half) + (y + 0.5f - half) * (y + 0.5f - half));
                float a = Mathf.Clamp01(outer - r + 0.5f);
                if (thickness > 0) a *= Mathf.Clamp01(r - (outer - thickness) + 0.5f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        tex.SetPixels32(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    static Sprite Make(bool outline)
    {
        // 모서리는 살짝만 둥글게 (어두운 유리판), 테두리는 가는 선
        const int size = 32;
        const float radius = 4f, width = 1.6f;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            name = outline ? "UIFrame" : "UIFill",
        };
        var pixels = new Color32[size * size];
        float half = size / 2f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // 둥근 사각형까지의 거리 (안쪽이 음수)
                float dx = Mathf.Max(Mathf.Abs(x + 0.5f - half) - (half - radius), 0);
                float dy = Mathf.Max(Mathf.Abs(y + 0.5f - half) - (half - radius), 0);
                float d = Mathf.Sqrt(dx * dx + dy * dy) - radius;
                float a = outline
                    ? Mathf.Clamp01(0.5f - d) * Mathf.Clamp01(d + width + 0.5f)
                    : Mathf.Clamp01(0.5f - d);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        tex.SetPixels32(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(10, 10, 10, 10));
    }
}

// 마우스를 올리면 정해 둔 그래픽들의 색(과 위치·크기)을 부드럽게 바꾼다. 유리 카드·메뉴 버튼이 쓴다
// 코드에서 AddComponent로만 붙인다. 마우스 진입 이벤트는 부모에게도 가므로 안쪽 버튼 위에 있어도 카드는 밝게 남는다
public class UIHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    struct Entry
    {
        public Graphic Graphic;
        public Color Normal, Hover;
        public Color Shown; // 마지막으로 칠한 색. 다른 코드가 색을 바꿨는지 알아볼 때 쓴다
    }

    readonly List<Entry> entries = new List<Entry>(2);

    // 누를 수 없는 버튼이면 반응하지 않는다
    public Selectable Gate;
    // 마우스를 올리면 옮길 것 (예: 메뉴 글자를 오른쪽으로 살짝)
    public RectTransform Move;
    public Vector2 MoveFrom, MoveTo;
    // 마우스를 올리면 크기를 바꿀 것 (예: 메뉴 왼쪽 강조 막대)
    public RectTransform Grow;
    public Vector2 GrowFrom, GrowTo;

    float t, target;

    public UIHover Add(Graphic g, Color normal, Color hover)
    {
        var c = Color.Lerp(normal, hover, t);
        g.color = c;
        entries.Add(new Entry { Graphic = g, Normal = normal, Hover = hover, Shown = c });
        return this;
    }

    // 버튼 상태가 바뀌었을 때 색을 다시 정한다 (예: 이어하기 버튼이 켜지고 꺼질 때). 이 그래픽은 다시 이 컴포넌트가 칠한다
    public void SetColors(int index, Color normal, Color hover)
    {
        if (index < 0 || index >= entries.Count) return;
        var e = entries[index];
        e.Normal = normal;
        e.Hover = hover;
        if (e.Graphic != null) e.Shown = e.Graphic.color;
        entries[index] = e;
        Apply();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (Gate == null || Gate.IsInteractable()) target = 1;
    }

    public void OnPointerExit(PointerEventData eventData) => target = 0;

    void OnDisable()
    {
        if (t == 0 && target == 0) return;
        t = target = 0;
        Apply();
    }

    void Update()
    {
        if (Gate != null && !Gate.IsInteractable()) target = 0;
        if (t == target) return;
        t = Mathf.MoveTowards(t, target, Time.unscaledDeltaTime * 10f);
        Apply();
    }

    void Apply()
    {
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            // 다른 코드가 색을 직접 칠하는 그래픽은 건드리지 않는다 (예: 컷씬 '다음' 버튼 테두리의 깜빡임)
            if (e.Graphic == null || e.Graphic.color != e.Shown) continue;
            e.Shown = Color.Lerp(e.Normal, e.Hover, t);
            e.Graphic.color = e.Shown;
            entries[i] = e;
        }
        float k = Mathf.SmoothStep(0, 1, t);
        if (Move != null) Move.anchoredPosition = Vector2.Lerp(MoveFrom, MoveTo, k);
        if (Grow != null) Grow.sizeDelta = Vector2.Lerp(GrowFrom, GrowTo, k);
    }
}

// 버튼을 만질 때의 움직임: 마우스를 올리면 살짝 떠오르고(1.02배), 누르는 동안 눌려 작아졌다가(0.94배)
// 놓으면 스프링처럼 통 튀며 제자리로 돌아온다. 누르는 순간 바탕에 밝은 빛이 번졌다 사라지고 작은 딸깍 소리가 난다.
// UIManager가 버튼·카드를 만들 때 UIPress.Attach로 붙인다 (누를 수 없는 버튼이면 반응하지 않는다).
// 새로 만든 버튼은 첫 프레임 끝에 제 색을 바로 칠한다: 그러지 않으면 기본색(흰색)에서 0.08초 동안 바뀌며
// 목록을 다시 그릴 때마다(제작대에서 아이템을 누를 때 등) 하얗게 번쩍인다
public class UIPress : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
{
    const float Stiffness = 600f, Damping = 24f, FlashTime = 0.28f;

    public Selectable Gate;
    public float HoverScale = 1.02f;

    RectTransform rt;
    Image flash;
    Vector3 baseScale;
    float scale = 1f, velocity, flashT = 1f;
    bool down, over, snapped;

    public static UIPress Attach(Selectable s, float hoverScale = 1.02f)
    {
        var p = s.GetComponent<UIPress>();
        if (p == null) p = s.gameObject.AddComponent<UIPress>();
        p.Gate = s;
        p.HoverScale = hoverScale;
        return p;
    }

    void Awake()
    {
        rt = (RectTransform)transform;
        baseScale = rt.localScale;
        pending.Add(this);
        if (!hooked)
        {
            Canvas.willRenderCanvases += SnapPending;
            hooked = true;
        }
        // 누를 때 번지는 빛 (글자·테두리 위에 얇게 덮는다)
        var go = new GameObject("PressFlash", typeof(RectTransform));
        var frt = (RectTransform)go.transform;
        frt.SetParent(rt, false);
        frt.anchorMin = Vector2.zero;
        frt.anchorMax = Vector2.one;
        frt.offsetMin = frt.offsetMax = Vector2.zero;
        flash = go.AddComponent<Image>();
        flash.sprite = UIStyle.Fill;
        flash.type = Image.Type.Sliced;
        flash.raycastTarget = false;
        flash.color = new Color(1f, 0.86f, 0.55f, 0f);
    }

    bool Live => Gate == null || Gate.IsInteractable();

    public void OnPointerEnter(PointerEventData e) => over = true;

    public void OnPointerExit(PointerEventData e)
    {
        over = false;
        down = false;
    }

    public void OnPointerDown(PointerEventData e)
    {
        if (!Live || e.button != PointerEventData.InputButton.Left) return;
        down = true;
        flashT = 0;
        ProceduralAudio.Play2D(ProceduralAudio.Click(), 0.18f, 1.35f, SoundCategory.UI);
    }

    public void OnPointerUp(PointerEventData e)
    {
        if (!down) return;
        down = false;
        velocity += 2.4f; // 놓는 순간 위로 튕긴다
    }

    // 만들어진 프레임에 화면을 그리기 직전(Canvas.willRenderCanvases) 버튼 바탕색을
    // 지금 상태(누를 수 없음·마우스 위·보통)의 색으로 바로 칠한다. 새로 만든 버튼의 Start·LateUpdate 시점과 상관없이 첫 화면부터 제 색이다
    static readonly System.Collections.Generic.List<UIPress> pending = new System.Collections.Generic.List<UIPress>();
    static bool hooked;

    static void SnapPending()
    {
        for (int i = 0; i < pending.Count; i++)
            if (pending[i] != null) pending[i].Snap();
        pending.Clear();
    }

    void Snap()
    {
        if (snapped) return;
        snapped = true;
        if (Gate == null || Gate.targetGraphic == null || Gate.transition != Selectable.Transition.ColorTint) return;
        var c = Gate.colors;
        bool hovered = false;
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null && Gate.IsInteractable())
        {
            var canvas = GetComponentInParent<Canvas>();
            var cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            hovered = RectTransformUtility.RectangleContainsScreenPoint(rt, mouse.position.ReadValue(), cam);
        }
        var target = !Gate.IsInteractable() ? c.disabledColor : hovered ? c.highlightedColor : c.normalColor;
        Gate.targetGraphic.CrossFadeColor(target * c.colorMultiplier, 0f, true, true);
    }

    void OnDisable()
    {
        down = over = false;
        scale = 1f;
        velocity = 0;
        flashT = 1f;
        if (rt != null) rt.localScale = baseScale;
        if (flash != null) flash.color = new Color(flash.color.r, flash.color.g, flash.color.b, 0f);
    }

    void Update()
    {
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        float target = !Live ? 1f : down ? 0.94f : over ? HoverScale : 1f;
        velocity += ((target - scale) * Stiffness - velocity * Damping) * dt;
        scale += velocity * dt;
        if (Mathf.Abs(scale - target) < 0.0005f && Mathf.Abs(velocity) < 0.001f) { scale = target; velocity = 0; }
        rt.localScale = baseScale * scale;

        if (flashT < 1f)
        {
            flashT = Mathf.Min(1f, flashT + dt / FlashTime);
            float a = (1 - flashT) * (1 - flashT) * 0.32f;
            flash.color = new Color(flash.color.r, flash.color.g, flash.color.b, a);
        }
    }
}

// 창이 열릴 때 살짝 작은 크기에서 커지며 서서히 나타난다 (0.16초, 일시정지 중에도 움직인다)
[RequireComponent(typeof(CanvasGroup))]
public class UIPopIn : MonoBehaviour
{
    const float Duration = 0.16f;
    public float FromScale = 0.96f;

    RectTransform rt;
    CanvasGroup group;
    float t;

    void Awake()
    {
        rt = (RectTransform)transform;
        group = GetComponent<CanvasGroup>();
    }

    void OnEnable()
    {
        t = 0;
        Apply();
    }

    void Update()
    {
        if (t >= 1f) return;
        t = Mathf.Min(1f, t + Time.unscaledDeltaTime / Duration);
        Apply();
    }

    void Apply()
    {
        float k = 1 - (1 - t) * (1 - t) * (1 - t); // 빠르게 시작해 부드럽게 멈춘다
        rt.localScale = Vector3.one * Mathf.Lerp(FromScale, 1f, k);
        group.alpha = k;
    }
}
