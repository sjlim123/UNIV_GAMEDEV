using System.Collections.Generic;
using UnityEngine;

// 사실적인 소품: 통조림·붕대·구급상자·배터리·손전등·연료통.
// 페트병(ItemVisuals.cs)과 같은 방식으로 회전체 메시와 작은 부품으로 모양을 잡고, 라벨·무늬는 작은 그림을 코드로 그려 재질로 입힌다.
// 글씨는 UI 글꼴(Resources/Fonts/Pretendard-Bold)의 글자판을 읽어 그린다 (글꼴이 없으면 글씨만 빠지고 모양은 그대로다)
public static partial class ItemVisuals
{
    static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();
    static readonly Dictionary<string, Material> texMats = new Dictionary<string, Material>();
    static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();

    // ================= 통조림 =================
    // 양철 깡통: 위아래 이음매가 말려 튀어나오고 뚜껑은 이음매 안쪽으로 들어가 있다. 뚜껑에 따개 고리, 몸통에 종이 라벨(빨간 띠·참치 그림·글씨). 바닥 y=0, 높이 0.12
    const float CanLabelR = 0.0453f, CanLabelBottom = 0.0155f, CanLabelTop = 0.1045f;

    static void CannedFood(Transform t)
    {
        var tin = RuntimeVisuals.Mat(new Color(0.78f, 0.78f, 0.8f), 0.7f, 0.8f);
        MeshPart(t, "Body", Lathe("can_body", 36,
            0f, 0f, 0.038f, 0f, 0.0415f, 0.001f, 0.0445f, 0.003f, 0.0455f, 0.005f, 0.0455f, 0.008f, 0.0445f, 0.0095f, 0.0442f, 0.011f,
            0.0442f, 0.110f, 0.0445f, 0.1115f, 0.0455f, 0.113f, 0.0455f, 0.118f, 0.0445f, 0.120f, 0.0425f, 0.120f, 0.0412f, 0.1185f,
            0.0405f, 0.1165f, 0.040f, 0.1155f, 0.040f, 0.1155f, 0f, 0.1155f), tin);
        // 뚜껑의 따는 선(홈)과 따개 고리 (한쪽 끝이 리벳으로 붙고 반대쪽이 살짝 들려 있다)
        MeshPart(t, "Score", Disc("can_score", 0.0305f, 0.032f, 0.1157f, 36, true), RuntimeVisuals.Mat(new Color(0.45f, 0.45f, 0.47f), 0.5f, 0.6f));
        var tab = MeshPart(t, "Tab", ProcMesh.Box("can_tab", 0.013f, 0.0012f, 0.03f, 6), tin);
        tab.transform.localPosition = new Vector3(0, 0.1168f, -0.012f);
        tab.transform.localEulerAngles = new Vector3(-5f, 0, 0);
        RuntimeVisuals.Part(PrimitiveType.Cylinder, t, new Vector3(0, 0.1166f, 0.001f), new Vector3(0.006f, 0.001f, 0.006f), new Color(0.6f, 0.6f, 0.62f), 0.6f, null, 0.7f);
        // 종이 라벨 (둘레에 한 바퀴)
        MeshPart(t, "Label", ProcMesh.Tube("can_label", 2, 36, CanLabelBottom, CanLabelR, CanLabelR, 0, CanLabelTop, CanLabelR, CanLabelR, 0),
            BandMat("can_label", CanLabelTexture, CanLabelR, CanLabelBottom, CanLabelTop, 0.35f));
    }

    // 라벨 그림 (512×160, 가로가 깡통 둘레). 미색 종이, 가운데 빨간 띠에 참치 그림과 '참치' 글씨, 위아래 금색 선과 잔글씨, 바코드.
    // 반 바퀴마다 같은 무늬라 어느 쪽에서 봐도 그림이 보인다
    static Texture2D CanLabelTexture()
    {
        return MakeTexture("can_label", 512, 160, new Color(0.95f, 0.93f, 0.86f), delegate (Bitmap b)
        {
            var red = new Color(0.72f, 0.09f, 0.08f);
            var gold = new Color(0.85f, 0.7f, 0.3f);
            var ink = new Color(0.22f, 0.2f, 0.18f);
            b.Rect(0, 50, 512, 118, red);
            b.Rect(0, 46, 512, 50, gold);
            b.Rect(0, 118, 512, 122, gold);
            bool text = PrepareText("참치통조림·고단백150gBUNKERFOODS ", 54, 18, 15);
            for (int half = 0; half < 2; half++)
            {
                float ox = half * 256;
                Fish(b, ox + 62, 84, 30, Color.white);
                if (text)
                {
                    b.Text("참치", 54, ox + 182, 84, Color.white, 0.5f);
                    b.Text("통조림 · 고단백 150g", 18, ox + 128, 139, ink, 0.5f);
                    b.Text("BUNKER FOODS", 15, ox + 128, 25, ink, 0.5f);
                }
                else
                {
                    b.Rect((int)ox + 150, 72, (int)ox + 214, 96, Color.white); // 글꼴이 없을 때: 글씨 자리의 흰 획
                    b.Rect((int)ox + 70, 134, (int)ox + 186, 144, ink);
                }
            }
            Barcode(b, 440, 8, 60, 30, ink);
            b.Noise(0.03f, 7);
        });
    }

    // 참치 그림: 몸통 타원 + 꼬리 삼각 + 눈 + 지느러미 선
    static void Fish(Bitmap b, float cx, float cy, float size, Color c)
    {
        b.Ellipse(cx, cy, size, size * 0.4f, c);
        b.Tri(new Vector2(cx + size * 0.72f, cy), new Vector2(cx + size * 1.25f, cy + size * 0.42f), new Vector2(cx + size * 1.25f, cy - size * 0.42f), c);
        b.Ellipse(cx - size * 0.55f, cy + size * 0.08f, size * 0.09f, size * 0.09f, new Color(0.3f, 0.05f, 0.05f));
        b.Line(cx - size * 0.15f, cy + size * 0.36f, cx + size * 0.2f, cy + size * 0.05f, 1.5f, new Color(0.88f, 0.55f, 0.55f));
    }

    // 바코드: 무작위 굵기의 검은 줄
    static void Barcode(Bitmap b, int x, int y, int w, int h, Color ink)
    {
        var rnd = new System.Random(x * 31 + y);
        for (int i = 0; i < w; i++)
            if (rnd.Next(100) < 45) b.Rect(x + i, y, x + i + 1, y + h, ink);
    }

    // ================= 붕대 =================
    // 거즈 롤 (축이 세로): 옆면은 거즈 짜임, 위아래 면은 감긴 겹이 나이테처럼 보이고 가운데에 심(구멍)이 있다.
    // 가운데를 두른 빨간 종이 띠(흰 십자·글씨), 옆에서 풀려 나와 비틀리며 바닥에 눕는 끝자락과 쇠 클립. 바닥 y=0, 반지름 0.05, 높이 0.06
    const float RollR = 0.05f, RollH = 0.06f, CoreR = 0.012f;

    static void Bandage(Transform t)
    {
        // 거즈 한 칸이 둘레 방향으로 약 3.9cm (옆면은 둘레를 8칸으로, 끝자락은 길이 m당 같은 비율로)
        float tile = 2 * Mathf.PI * RollR / 8f;
        var gauzeRoll = TexMat("gauze_roll", GauzeTexture, 0.12f, 0f, new Vector2(8f, 1f), Vector2.zero);
        var gauzeTail = TexMat("gauze_tail", GauzeTexture, 0.12f, 0f, new Vector2(1f / tile, 1f), Vector2.zero);
        MeshPart(t, "Side", Lathe("roll_side", 40, 0.0485f, 0.0015f, 0.0497f, 0.0035f, RollR, 0.006f, RollR, RollH - 0.006f, 0.0497f, RollH - 0.0035f, 0.0485f, RollH - 0.0015f), gauzeRoll);
        var layers = TexMat("roll_face", RollFaceTexture, 0.15f, 0f, Vector2.one, Vector2.zero);
        MeshPart(t, "Top", Disc("roll_top", CoreR, 0.0485f, RollH - 0.0015f, 40, true), layers);
        MeshPart(t, "Bottom", Disc("roll_bottom", CoreR, 0.0485f, 0.0015f, 40, false), layers);
        // 심: 안쪽 벽과 바닥 (어두운 미색 종이심)
        MeshPart(t, "Core", Lathe("roll_core", 24, CoreR, RollH - 0.0015f, CoreR, 0.004f, 0f, 0.004f), RuntimeVisuals.Mat(new Color(0.55f, 0.5f, 0.42f), 0.1f));
        // 빨간 종이 띠
        MeshPart(t, "Band", ProcMesh.Tube("roll_band", 2, 40, 0.023f, 0.0506f, 0.0506f, 0, 0.037f, 0.0506f, 0.0506f, 0),
            BandMat("roll_band", BandageBandTexture, 0.0506f, 0.023f, 0.037f, 0.35f));
        BandageTail(t, gauzeTail);
    }

    // 풀린 끝자락: 옆면에서 접선 방향으로 나와 6.5cm 동안 비틀리며 바닥에 눕고, 끝에 쇠 클립이 물려 있다
    static void BandageTail(Transform t, Material gauze)
    {
        const float phi = -0.6f, len = 0.11f, width = 0.056f;
        const int N = 14;
        var radial = new Vector3(Mathf.Cos(phi), 0, Mathf.Sin(phi));
        var tangent = new Vector3(-Mathf.Sin(phi), 0, Mathf.Cos(phi));
        var centers = new Vector3[N];
        var sides = new Vector3[N];
        for (int i = 0; i < N; i++)
        {
            float s = (float)i / (N - 1) * len;
            float k = Mathf.Clamp01(s / 0.065f);
            float e = k * k * (3 - 2 * k);
            var p = radial * RollR + tangent * s + radial * (0.6f * s * s);
            // 세로로 서 있다가(반폭이 위) 눕는다(반폭이 바깥). 아래 가장자리가 바닥을 뚫지 않게 가운데 높이를 맞춘다
            float angle = e * Mathf.PI / 2;
            p.y = 0.0012f + width / 2 * Mathf.Cos(angle);
            centers[i] = p;
            sides[i] = (Vector3.up * Mathf.Cos(angle) + radial * Mathf.Sin(angle)).normalized;
        }
        MeshPart(t, "Tail", Ribbon("roll_tail", centers, sides, width), gauze);
        var dir = (centers[N - 1] - centers[N - 2]).normalized;
        var clip = RuntimeVisuals.Part(PrimitiveType.Cube, t, centers[N - 1] - dir * 0.008f + Vector3.up * 0.0025f, new Vector3(0.012f, 0.003f, 0.018f), new Color(0.72f, 0.72f, 0.75f), 0.6f, null, 0.7f);
        clip.transform.localRotation = Quaternion.LookRotation(dir, Vector3.up);
    }

    // 거즈 짜임 (64×96 한 칸): 흰 바탕에 가로 올과 세로 올이 성글게 엇갈린다
    static Texture2D GauzeTexture()
    {
        return MakeTexture("gauze", 64, 96, new Color(0.93f, 0.92f, 0.89f), delegate (Bitmap b)
        {
            var dark = new Color(0.84f, 0.83f, 0.79f);
            var light = new Color(0.97f, 0.96f, 0.94f);
            for (int y = 0; y < 96; y += 6) { b.Rect(0, y, 64, y + 1, dark); b.Rect(0, y + 3, 64, y + 4, light); }
            for (int x = 0; x < 64; x += 4) b.Rect(x, 0, x + 1, 96, dark, 0.6f);
            b.Noise(0.04f, 3);
        });
    }

    // 롤의 위·아래 면 (256×256, 한 변이 롤 지름): 감긴 겹이 나선처럼 보이는 가는 선, 바깥 가장자리는 조금 어둡다
    static Texture2D RollFaceTexture()
    {
        return MakeTexture("roll_face", 256, 256, new Color(0.94f, 0.93f, 0.9f), delegate (Bitmap b)
        {
            var line = new Color(0.8f, 0.79f, 0.75f);
            for (int y = 0; y < 256; y++)
                for (int x = 0; x < 256; x++)
                {
                    float dx = x + 0.5f - 128f, dy = y + 0.5f - 128f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy) / 128f * 0.0485f; // m
                    float a = Mathf.Atan2(dy, dx) / (2 * Mathf.PI);
                    float f = Mathf.Repeat(r / 0.0025f - a, 1f);
                    if (f < 0.25f) b.Blend(x, y, line, 0.8f);
                    if (r > 0.045f) b.Blend(x, y, new Color(0.75f, 0.73f, 0.68f), (r - 0.045f) / 0.0035f * 0.5f);
                }
            b.Noise(0.03f, 11);
        }, false);
    }

    // 빨간 띠 (512×32, 가로가 롤 둘레): 흰 십자 네 개와 '붕대 · 멸균' 글씨
    static Texture2D BandageBandTexture()
    {
        return MakeTexture("roll_band", 512, 32, new Color(0.72f, 0.09f, 0.08f), delegate (Bitmap b)
        {
            bool text = PrepareText("붕대·멸균BANDAGE ", 15);
            for (int i = 0; i < 4; i++)
            {
                int ox = i * 128;
                b.Rect(ox + 14, 13, ox + 30, 19, Color.white);
                b.Rect(ox + 19, 8, ox + 25, 24, Color.white);
                if (text) b.Text(i % 2 == 0 ? "붕대 · 멸균" : "BANDAGE", 15, ox + 80, 16, Color.white, 0.5f);
                else b.Rect(ox + 44, 13, ox + 116, 19, Color.white);
            }
            b.Noise(0.02f, 5);
        });
    }

    // ================= 구급상자 =================
    // 둥근 미색 플라스틱 상자: 뚜껑 이음선, 뚜껑 위 빨간 십자, 앞면의 쇠 잠금쇠 둘과 '구급상자' 표지. 바닥 y=0
    static void Medkit(Transform t)
    {
        var shell = RuntimeVisuals.Mat(new Color(0.92f, 0.92f, 0.88f), 0.55f);
        var red = RuntimeVisuals.Mat(new Color(0.75f, 0.08f, 0.08f), 0.5f);
        var box = MeshPart(t, "Box", ProcMesh.Box("medkit_box", 0.32f, 0.16f, 0.22f, 7), shell);
        box.transform.localPosition = new Vector3(0, 0.08f, 0);
        var seam = MeshPart(t, "Seam", ProcMesh.Box("medkit_seam", 0.324f, 0.005f, 0.224f, 7), RuntimeVisuals.Mat(new Color(0.3f, 0.3f, 0.28f), 0.3f));
        seam.transform.localPosition = new Vector3(0, 0.112f, 0);
        var crossA = MeshPart(t, "CrossA", ProcMesh.Box("medkit_cross_a", 0.17f, 0.004f, 0.05f, 5), red);
        crossA.transform.localPosition = new Vector3(0, 0.1615f, 0);
        var crossB = MeshPart(t, "CrossB", ProcMesh.Box("medkit_cross_b", 0.05f, 0.004f, 0.17f, 5), red);
        crossB.transform.localPosition = new Vector3(0, 0.1621f, 0);
        foreach (var x in new[] { -0.085f, 0.085f })
        {
            RuntimeVisuals.Part(PrimitiveType.Cube, t, new Vector3(x, 0.106f, -0.1125f), new Vector3(0.03f, 0.036f, 0.005f), new Color(0.75f, 0.75f, 0.78f), 0.7f, null, 0.75f);
            RuntimeVisuals.Part(PrimitiveType.Cube, t, new Vector3(x, 0.121f, -0.1135f), new Vector3(0.034f, 0.008f, 0.004f), new Color(0.5f, 0.5f, 0.52f), 0.5f, null, 0.6f);
        }
        var plate = MeshPart(t, "Plate", Quad("medkit_plate", 0.15f, 0.055f), TexMat("medkit_plate", MedkitPlateTexture, 0.4f, 0f, Vector2.one, Vector2.zero));
        plate.transform.localPosition = new Vector3(0, 0.062f, -0.1115f);
    }

    // 앞면 표지 (256×96): 미색 바탕, 빨간 테두리, 빨간 십자와 '구급상자 / FIRST AID'
    static Texture2D MedkitPlateTexture()
    {
        return MakeTexture("medkit_plate", 256, 96, new Color(0.97f, 0.96f, 0.92f), delegate (Bitmap b)
        {
            var red = new Color(0.75f, 0.08f, 0.08f);
            var ink = new Color(0.25f, 0.24f, 0.22f);
            b.Rect(0, 0, 256, 96, red);
            b.Rect(4, 4, 252, 92, new Color(0.97f, 0.96f, 0.92f));
            b.Rect(22, 40, 66, 56, red);
            b.Rect(36, 26, 52, 70, red);
            bool text = PrepareText("구급상자FIRSTAID ", 34, 14);
            if (text)
            {
                b.Text("구급상자", 34, 160, 56, ink, 0.5f);
                b.Text("FIRST AID", 14, 160, 24, red, 0.5f);
            }
            else b.Rect(90, 44, 230, 66, ink);
        }, false);
    }

    // ================= 배터리 =================
    // D형 건전지를 눕혀 둔다 (−극이 −x, +극이 +x). 검은 아랫부분·초록 윗띠·흰 +/−가 그려진 라벨, 은색 쇠 뚜껑과 볼록한 +극. 바닥에서 반지름만큼 띄운다
    static void Battery(Transform t)
    {
        var cell = new GameObject("Cell").transform;
        cell.SetParent(t, false);
        cell.localPosition = new Vector3(-0.07f, 0.025f, 0);
        cell.localEulerAngles = new Vector3(0, 0, -90);
        var silver = RuntimeVisuals.Mat(new Color(0.8f, 0.8f, 0.82f), 0.75f, 0.85f);
        MeshPart(cell, "Body", Lathe("batt_body", 28, 0f, 0f, 0.021f, 0f, 0.0238f, 0.001f, 0.025f, 0.0025f, 0.025f, 0.105f, 0f, 0.105f), RuntimeVisuals.Mat(new Color(0.1f, 0.1f, 0.11f), 0.5f));
        MeshPart(cell, "Cap", Lathe("batt_cap", 28, 0.025f, 0.105f, 0.025f, 0.1165f, 0.0238f, 0.118f, 0.021f, 0.119f, 0.021f, 0.119f, 0.0075f, 0.119f, 0.0075f, 0.119f, 0.0075f, 0.1235f, 0.0065f, 0.1245f, 0f, 0.1245f), silver);
        MeshPart(cell, "Label", ProcMesh.Tube("batt_label", 2, 28, 0.003f, 0.0252f, 0.0252f, 0, 0.104f, 0.0252f, 0.0252f, 0),
            BandMat("batt_label", BatteryLabelTexture, 0.0252f, 0.003f, 0.104f, 0.45f));
    }

    // 라벨 그림 (256×165, 가로가 둘레): 아래 62%는 검정, 위는 초록, 반 바퀴마다 흰 +와 −, 상표와 전압 글씨
    static Texture2D BatteryLabelTexture()
    {
        return MakeTexture("batt_label", 256, 165, new Color(0.1f, 0.1f, 0.11f), delegate (Bitmap b)
        {
            var green = new Color(0.1f, 0.5f, 0.26f);
            b.Rect(0, 102, 256, 165, green);
            b.Rect(0, 100, 256, 102, new Color(0.75f, 0.75f, 0.72f));
            bool text = PrepareText("BUNKER1.5VALKALINE ", 16, 11);
            for (int half = 0; half < 2; half++)
            {
                int ox = half * 128;
                b.Rect(ox + 54, 130, ox + 74, 134, Color.white);
                b.Rect(ox + 62, 122, ox + 66, 142, Color.white);
                b.Rect(ox + 54, 18, ox + 74, 22, Color.white);
                if (text)
                {
                    b.Text("BUNKER", 16, ox + 64, 78, Color.white, 0.5f);
                    b.Text("1.5V ALKALINE", 11, ox + 64, 50, new Color(0.8f, 0.8f, 0.8f), 0.5f);
                }
                else b.Rect(ox + 34, 72, ox + 94, 84, Color.white);
            }
            b.Noise(0.02f, 13);
        });
    }

    // ================= 손전등 =================
    // 검은 몸통 (+z가 빛 나가는 쪽, 바닥 y=0.028): 홈이 파인 손잡이, 넓어지는 머리, 크롬 반사경과 전구가 비치는 유리 렌즈, 꽁무니 마개, 위쪽 주황 고무 버튼
    static readonly float[] TorchProfile =
    {
        0f, 0f, 0.019f, 0f, 0.0215f, 0.002f, 0.0215f, 0.012f, 0.02f, 0.014f, 0.02f, 0.118f, 0.022f, 0.122f, 0.028f, 0.138f,
        0.028f, 0.192f, 0.029f, 0.194f, 0.029f, 0.2f, 0.0265f, 0.2f, 0.0265f, 0.2f, 0.0265f, 0.195f,
    };

    static void Flashlight(Transform t)
    {
        var torch = new GameObject("Torch").transform;
        torch.SetParent(t, false);
        torch.localPosition = new Vector3(0, 0.028f, -0.084f);
        torch.localEulerAngles = new Vector3(90, 0, 0);
        MeshPart(torch, "Body", Lathe("torch_body", 32, TorchProfile), TexMat("torch_body", TorchBodyTexture, 0.5f, 0.25f, Vector2.one, Vector2.zero));
        MeshPart(torch, "Reflector", Lathe("torch_reflector", 32, 0.0265f, 0.195f, 0.024f, 0.1935f, 0.0085f, 0.166f, 0.006f, 0.163f, 0f, 0.163f), RuntimeVisuals.Mat(new Color(0.9f, 0.9f, 0.92f), 0.92f, 0.9f));
        RuntimeVisuals.Part(PrimitiveType.Sphere, torch, new Vector3(0, 0.169f, 0), Vector3.one * 0.009f, new Color(1f, 0.92f, 0.75f), 0.6f);
        var lens = MeshPart(torch, "Lens", Disc("torch_lens", 0, 0.0262f, 0.199f, 32, true), Plastic(new Color(0.8f, 0.9f, 1f, 0.3f), 0.97f, 3000));
        lens.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        RuntimeVisuals.Part(PrimitiveType.Cube, t, new Vector3(0, 0.0475f, 0.03f), new Vector3(0.012f, 0.006f, 0.018f), new Color(0.85f, 0.4f, 0.1f), 0.3f);
    }

    // 몸통 그림 (256×256, 가로가 둘레·세로가 옆모습 길이): 검은 바탕, 손잡이 자리(y 0.03~0.10)에 세로 홈 열두 줄, 머리와 마개에 가는 띠
    static Texture2D TorchBodyTexture()
    {
        return MakeTexture("torch_body", 256, 256, new Color(0.09f, 0.09f, 0.1f), delegate (Bitmap b)
        {
            int g0 = Mathf.RoundToInt(ProfileV(TorchProfile, 0.03f) * 256), g1 = Mathf.RoundToInt(ProfileV(TorchProfile, 0.10f) * 256);
            var groove = new Color(0.03f, 0.03f, 0.035f);
            var edge = new Color(0.16f, 0.16f, 0.17f);
            for (int i = 0; i < 12; i++)
            {
                int x = i * 256 / 12 + 4;
                b.Rect(x, g0, x + 8, g1, groove);
                b.Rect(x + 8, g0, x + 10, g1, edge);
            }
            b.Rect(0, g0 - 3, 256, g0, edge);
            b.Rect(0, g1, 256, g1 + 3, edge);
            int head = Mathf.RoundToInt(ProfileV(TorchProfile, 0.14f) * 256);
            b.Rect(0, head, 256, head + 2, new Color(0.2f, 0.2f, 0.21f));
            int tail = Mathf.RoundToInt(ProfileV(TorchProfile, 0.012f) * 256);
            b.Rect(0, tail - 2, 256, tail, new Color(0.2f, 0.2f, 0.21f));
            b.Noise(0.05f, 17);
        });
    }

    // ================= 연료통 =================
    // 빨간 쇠 연료통: 모서리가 둥근 몸통(긁힌 페인트), 넓은 양면에 찍힌 X자 보강 돌기와 테두리, 옆면 용접선, 노란 주둥이 마개, 위쪽 손잡이. 바닥 y=0
    static void JerryCan(Transform t)
    {
        var paint = TexMat("jerry_paint", WornPaintTexture, 0.42f, 0.3f, new Vector2(2f, 2f), Vector2.zero);
        var ridgeColor = new Color(0.64f, 0.13f, 0.1f);
        var body = MeshPart(t, "Body", ProcMesh.Box("jerry_body", 0.18f, 0.44f, 0.34f, 9), paint);
        body.transform.localPosition = new Vector3(0, 0.22f, 0);
        foreach (var sx in new[] { -1f, 1f })
        {
            float x = sx * 0.093f;
            foreach (var a in new[] { 37.3f, -37.3f })
                RuntimeVisuals.Part(PrimitiveType.Cube, t, new Vector3(x, 0.22f, 0), new Vector3(0.008f, 0.478f, 0.018f), ridgeColor, 0.4f, new Vector3(a, 0, 0), 0.3f);
            RuntimeVisuals.Part(PrimitiveType.Cube, t, new Vector3(x, 0.03f, 0), new Vector3(0.006f, 0.012f, 0.302f), ridgeColor, 0.4f, null, 0.3f);
            RuntimeVisuals.Part(PrimitiveType.Cube, t, new Vector3(x, 0.41f, 0), new Vector3(0.006f, 0.012f, 0.302f), ridgeColor, 0.4f, null, 0.3f);
            RuntimeVisuals.Part(PrimitiveType.Cube, t, new Vector3(x, 0.22f, -0.145f), new Vector3(0.006f, 0.392f, 0.012f), ridgeColor, 0.4f, null, 0.3f);
            RuntimeVisuals.Part(PrimitiveType.Cube, t, new Vector3(x, 0.22f, 0.145f), new Vector3(0.006f, 0.392f, 0.012f), ridgeColor, 0.4f, null, 0.3f);
        }
        foreach (var sz in new[] { -1f, 1f })
            RuntimeVisuals.Part(PrimitiveType.Cube, t, new Vector3(0, 0.22f, sz * 0.171f), new Vector3(0.004f, 0.4f, 0.004f), new Color(0.25f, 0.08f, 0.06f), 0.3f);
        // 주둥이: 빨간 목 + 노란 마개 + 검은 띠
        var spout = new GameObject("Spout").transform;
        spout.SetParent(t, false);
        spout.localPosition = new Vector3(0, 0, 0.1f);
        MeshPart(spout, "Neck", Lathe("jerry_neck", 20, 0.021f, 0.436f, 0.021f, 0.458f, 0f, 0.458f), RuntimeVisuals.Mat(ridgeColor, 0.4f, 0.3f));
        MeshPart(spout, "Cap", Lathe("jerry_cap", 24, 0f, 0.456f, 0.026f, 0.456f, 0.026f, 0.482f, 0.0245f, 0.486f, 0f, 0.486f), RuntimeVisuals.Mat(new Color(0.88f, 0.7f, 0.16f), 0.45f));
        MeshPart(spout, "CapBand", ProcMesh.Tube("jerry_cap_band", 2, 24, 0.468f, 0.0262f, 0.0262f, 0, 0.474f, 0.0262f, 0.0262f, 0), RuntimeVisuals.Mat(new Color(0.08f, 0.08f, 0.08f), 0.3f));
        // 손잡이 (손에 들 때 손가락을 건다. 위치는 HeldItems의 fuel과 같다)
        RuntimeVisuals.Part(PrimitiveType.Cylinder, t, new Vector3(0, 0.49f, -0.02f), new Vector3(0.02f, 0.07f, 0.02f), ridgeColor, 0.45f, new Vector3(90, 0, 0), 0.3f);
        foreach (var z in new[] { -0.08f, 0.04f })
            RuntimeVisuals.Part(PrimitiveType.Cylinder, t, new Vector3(0, 0.462f, z), new Vector3(0.016f, 0.026f, 0.016f), ridgeColor, 0.45f, null, 0.3f);
    }

    // 긁힌 빨간 페인트 (256×256 반복 칸): 얼룩덜룩한 빨강, 밝은 긁힘 선, 어두운 때 얼룩
    static Texture2D WornPaintTexture()
    {
        return MakeTexture("jerry_paint", 256, 256, new Color(0.6f, 0.1f, 0.08f), delegate (Bitmap b)
        {
            var rnd = new System.Random(21);
            for (int y = 0; y < 256; y++)
                for (int x = 0; x < 256; x++)
                {
                    float n = TileNoise(x, y, 256, 4, 2) * 0.6f + TileNoise(x, y, 256, 16, 9) * 0.4f;
                    b.Blend(x, y, new Color(0.3f, 0.06f, 0.05f), Mathf.Clamp01((n - 0.5f) * 1.6f));
                    b.Blend(x, y, new Color(0.7f, 0.14f, 0.1f), Mathf.Clamp01((0.42f - n) * 1.4f));
                }
            for (int i = 0; i < 26; i++)
            {
                float x0 = 20 + (float)rnd.NextDouble() * 216, y0 = 20 + (float)rnd.NextDouble() * 216;
                float ang = (float)rnd.NextDouble() * Mathf.PI, l = 8 + (float)rnd.NextDouble() * 40;
                b.Line(x0, y0, x0 + Mathf.Cos(ang) * l, y0 + Mathf.Sin(ang) * l, 1f, new Color(0.78f, 0.55f, 0.5f));
            }
            b.Noise(0.04f, 23);
        });
    }

    // ================= 그림 도우미 =================

    // 되풀이되는 값 노이즈 (0~1): 격자 간격 period/cells 픽셀, 가장자리를 넘어도 이어진다
    static float TileNoise(int x, int y, int period, int cells, int seed)
    {
        float fx = (float)x / period * cells, fy = (float)y / period * cells;
        int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
        float tx = fx - x0, ty = fy - y0;
        tx = tx * tx * (3 - 2 * tx);
        ty = ty * ty * (3 - 2 * ty);
        float a = Hash((x0 % cells + cells) % cells, (y0 % cells + cells) % cells, seed);
        float b = Hash(((x0 + 1) % cells + cells) % cells, (y0 % cells + cells) % cells, seed);
        float c = Hash((x0 % cells + cells) % cells, ((y0 + 1) % cells + cells) % cells, seed);
        float d = Hash(((x0 + 1) % cells + cells) % cells, ((y0 + 1) % cells + cells) % cells, seed);
        return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
    }

    static float Hash(int x, int y, int seed)
    {
        uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1274126177);
        h = (h ^ (h >> 13)) * 1274126177u;
        return ((h ^ (h >> 16)) & 0xffffff) / 16777215f;
    }

    // 작은 그림판: 색 배열에 네모·타원·삼각·선·글씨를 그린다 (왼쪽 아래가 (0,0), Texture2D와 같다)
    class Bitmap
    {
        public readonly int W, H;
        public readonly Color[] Px;

        public Bitmap(int w, int h, Color fill)
        {
            W = w; H = h;
            Px = new Color[w * h];
            for (int i = 0; i < Px.Length; i++) Px[i] = fill;
        }

        public void Blend(int x, int y, Color c, float a)
        {
            if (x < 0 || y < 0 || x >= W || y >= H || a <= 0) return;
            int k = y * W + x;
            Px[k] = a >= 1 ? c : Color.Lerp(Px[k], c, a);
        }

        public void Rect(int x0, int y0, int x1, int y1, Color c, float a = 1f)
        {
            for (int y = Mathf.Max(0, y0); y < Mathf.Min(H, y1); y++)
                for (int x = Mathf.Max(0, x0); x < Mathf.Min(W, x1); x++)
                    Blend(x, y, c, a);
        }

        public void Ellipse(float cx, float cy, float rx, float ry, Color c)
        {
            float edge = Mathf.Min(rx, ry);
            for (int y = Mathf.Max(0, Mathf.FloorToInt(cy - ry - 1)); y <= Mathf.Min(H - 1, Mathf.CeilToInt(cy + ry + 1)); y++)
                for (int x = Mathf.Max(0, Mathf.FloorToInt(cx - rx - 1)); x <= Mathf.Min(W - 1, Mathf.CeilToInt(cx + rx + 1)); x++)
                {
                    float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    Blend(x, y, c, Mathf.Clamp01((1 - d) * edge + 0.5f));
                }
        }

        public void Tri(Vector2 a, Vector2 b, Vector2 c, Color col)
        {
            int x0 = Mathf.FloorToInt(Mathf.Min(a.x, b.x, c.x)), x1 = Mathf.CeilToInt(Mathf.Max(a.x, b.x, c.x));
            int y0 = Mathf.FloorToInt(Mathf.Min(a.y, b.y, c.y)), y1 = Mathf.CeilToInt(Mathf.Max(a.y, b.y, c.y));
            for (int y = Mathf.Max(0, y0); y <= Mathf.Min(H - 1, y1); y++)
                for (int x = Mathf.Max(0, x0); x <= Mathf.Min(W - 1, x1); x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    float s1 = Side(a, b, p), s2 = Side(b, c, p), s3 = Side(c, a, p);
                    bool neg = s1 < 0 || s2 < 0 || s3 < 0, pos = s1 > 0 || s2 > 0 || s3 > 0;
                    if (!(neg && pos)) Blend(x, y, col, 1f);
                }
        }

        static float Side(Vector2 a, Vector2 b, Vector2 p) => (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);

        public void Line(float x0, float y0, float x1, float y1, float width, Color c)
        {
            float half = width / 2;
            var a = new Vector2(x0, y0);
            var d = new Vector2(x1 - x0, y1 - y0);
            float len2 = Mathf.Max(1e-6f, d.sqrMagnitude);
            for (int y = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(y0, y1) - half - 1)); y <= Mathf.Min(H - 1, Mathf.CeilToInt(Mathf.Max(y0, y1) + half + 1)); y++)
                for (int x = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(x0, x1) - half - 1)); x <= Mathf.Min(W - 1, Mathf.CeilToInt(Mathf.Max(x0, x1) + half + 1)); x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    float k = Mathf.Clamp01(Vector2.Dot(p - a, d) / len2);
                    float dist = Vector2.Distance(p, a + d * k);
                    Blend(x, y, c, Mathf.Clamp01(half - dist + 0.5f));
                }
        }

        // 밝기를 조금씩 흔들어 종이·천 결처럼 보이게 한다
        public void Noise(float amount, int seed)
        {
            var rnd = new System.Random(seed);
            for (int i = 0; i < Px.Length; i++)
            {
                float k = 1 + ((float)rnd.NextDouble() - 0.5f) * 2 * amount;
                var p = Px[i];
                Px[i] = new Color(Mathf.Clamp01(p.r * k), Mathf.Clamp01(p.g * k), Mathf.Clamp01(p.b * k), p.a);
            }
        }

        // 글씨 (PrepareText로 읽어 둔 글자판을 쓴다). (x, cy): 기준점, align 0 왼쪽 · 0.5 가운데 · 1 오른쪽, 세로는 글자 높이의 가운데
        public void Text(string text, int size, float x, float cy, Color color, float align)
        {
            if (labelFont == null || fontAtlas == null) return;
            float width = 0, top = float.MinValue, bottom = float.MaxValue;
            foreach (char ch in text)
            {
                if (!labelFont.GetCharacterInfo(ch, out CharacterInfo ci, size, FontStyle.Normal)) continue;
                width += ci.advance;
                if (ci.maxY > ci.minY) { top = Mathf.Max(top, ci.maxY); bottom = Mathf.Min(bottom, ci.minY); }
            }
            if (top < bottom) return;
            float pen = Mathf.Round(x - width * align), baseline = Mathf.Round(cy - (top + bottom) / 2f);
            foreach (char ch in text)
            {
                if (!labelFont.GetCharacterInfo(ch, out CharacterInfo ci, size, FontStyle.Normal)) continue;
                float gx0 = pen + ci.minX, gx1 = pen + ci.maxX, gy0 = baseline + ci.minY, gy1 = baseline + ci.maxY;
                if (gx1 > gx0 && gy1 > gy0)
                {
                    int jMax = Mathf.Min(H, Mathf.CeilToInt(gy1)), iMax = Mathf.Min(W, Mathf.CeilToInt(gx1));
                    for (int j = Mathf.Max(0, Mathf.FloorToInt(gy0)); j < jMax; j++)
                        for (int i = Mathf.Max(0, Mathf.FloorToInt(gx0)); i < iMax; i++)
                        {
                            float s = (i + 0.5f - gx0) / (gx1 - gx0), tt = (j + 0.5f - gy0) / (gy1 - gy0);
                            if (s < 0 || s > 1 || tt < 0 || tt > 1) continue;
                            var uv = Vector2.Lerp(Vector2.Lerp(ci.uvBottomLeft, ci.uvBottomRight, s), Vector2.Lerp(ci.uvTopLeft, ci.uvTopRight, s), tt);
                            var g = fontAtlas.GetPixelBilinear(uv.x, uv.y);
                            float a = (fontAtlasRed ? g.r : g.a) * color.a;
                            Blend(i, j, new Color(color.r, color.g, color.b, 1f), a);
                        }
                }
                pen += ci.advance;
            }
        }
    }

    static Font labelFont;
    static Texture2D fontAtlas;
    static bool fontAtlasRed;

    // 글씨를 그리기 전에 쓸 글자와 크기를 알려 글꼴의 글자판을 읽어 둔다. 글꼴이 없으면 false (글씨 없이 그린다)
    static bool PrepareText(string chars, params int[] sizes)
    {
        if (labelFont == null) labelFont = Resources.Load<Font>("Fonts/Pretendard-Bold");
        if (labelFont == null) return false;
        for (int pass = 0; pass < 2; pass++) // 글자판이 다시 만들어져도 모든 크기가 남도록 두 번
            foreach (int size in sizes) labelFont.RequestCharactersInTexture(chars, size, FontStyle.Normal);
        var src = labelFont.material != null ? labelFont.material.mainTexture : null;
        if (src == null) return false;
        ReleaseText();
        var rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        var prev = RenderTexture.active;
        Graphics.Blit(src, rt);
        RenderTexture.active = rt;
        fontAtlas = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false, true) { name = "ItemLabelFontAtlas" };
        fontAtlas.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
        fontAtlas.Apply();
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        // 글자 모양은 보통 알파에 담기지만 빨강 채널에 담겼으면 그쪽을 쓴다
        var all = fontAtlas.GetPixels32();
        double sumA = 0, sumR = 0;
        foreach (var p in all) { sumA += p.a; sumR += p.r; }
        fontAtlasRed = sumA / all.Length > 150 && sumR < sumA;
        return true;
    }

    static void ReleaseText()
    {
        if (fontAtlas == null) return;
        if (Application.isPlaying) Object.Destroy(fontAtlas); else Object.DestroyImmediate(fontAtlas);
        fontAtlas = null;
    }

    static Texture2D MakeTexture(string key, int w, int h, Color fill, System.Action<Bitmap> paint, bool repeat = true)
    {
        if (textures.TryGetValue(key, out var t) && t != null) return t;
        var b = new Bitmap(w, h, fill);
        paint(b);
        ReleaseText();
        t = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = "Item_" + key, wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
        t.SetPixels(b.Px);
        t.Apply(true, true);
        textures[key] = t;
        return t;
    }

    // 그림을 입힌 URP Lit 재질 (key로 한 번만 만든다). scale·offset은 텍스처 좌표 변환
    static Material TexMat(string key, System.Func<Texture2D> texture, float smoothness, float metallic, Vector2 scale, Vector2 offset)
    {
        if (texMats.TryGetValue(key, out var m) && m != null) return m;
        m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Item " + key };
        m.SetTexture("_BaseMap", texture());
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Smoothness", smoothness);
        m.SetFloat("_Metallic", metallic);
        m.SetTextureScale("_BaseMap", scale);
        m.SetTextureOffset("_BaseMap", offset);
        texMats[key] = m;
        return m;
    }

    // 반지름 radius, 높이 bottom~top인 띠(ProcMesh.Tube: u = 둘레 길이 m, v = 높이 m)에 그림 한 장이 한 바퀴 맞게 들어가는 재질
    static Material BandMat(string key, System.Func<Texture2D> texture, float radius, float bottom, float top, float smoothness, float metallic = 0f)
    {
        float around = 2 * Mathf.PI * radius, h = top - bottom;
        return TexMat(key, texture, smoothness, metallic, new Vector2(1f / around, 1f / h), new Vector2(0, -bottom / h));
    }

    // ================= 메시 도우미 =================

    // 회전체: 옆모습 폴리라인 (r, y, r, y, …)을 y축 둘레로 돌린 메시. 같은 점을 잇달아 두 번 넣으면 그 자리가 각진 모서리가 된다.
    // 겉이 앞면이 되는 방향: 아래 가운데(r=0)에서 바깥으로, 겉을 따라 위로, 위에서 안쪽(r=0)으로. 끝이 r>0이면 그쪽은 뚫려 있다.
    // UV: u = 둘레(0~1), v = 폴리라인을 따라 잰 길이(0~1)
    static Mesh Lathe(string key, int radial, params float[] profile)
    {
        if (meshes.TryGetValue(key, out var m) && m != null) return m;
        int n = profile.Length / 2, cols = radial + 1;
        var len = new float[n];
        for (int i = 1; i < n; i++)
            len[i] = len[i - 1] + Vector2.Distance(new Vector2(profile[i * 2], profile[i * 2 + 1]), new Vector2(profile[i * 2 - 2], profile[i * 2 - 1]));
        float total = Mathf.Max(1e-5f, len[n - 1]);
        var verts = new Vector3[n * cols];
        var uvs = new Vector2[n * cols];
        for (int i = 0; i < n; i++)
        {
            float r = profile[i * 2], y = profile[i * 2 + 1];
            for (int j = 0; j < cols; j++)
            {
                float a = 2 * Mathf.PI * j / radial;
                verts[i * cols + j] = new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
                uvs[i * cols + j] = new Vector2((float)j / radial, len[i] / total);
            }
        }
        var tris = new List<int>();
        int first = -1;
        for (int i = 0; i < n - 1; i++)
        {
            if (len[i + 1] - len[i] < 1e-7f) continue; // 모서리용 중복 점 사이는 비워 둔다
            if (first < 0) first = i;
            for (int j = 0; j < radial; j++)
            {
                int a = i * cols + j, b = a + 1, c = a + cols, d = c + 1;
                tris.Add(a); tris.Add(c); tris.Add(b);
                tris.Add(b); tris.Add(c); tris.Add(d);
            }
        }
        m = new Mesh { name = key };
        m.vertices = verts;
        m.uv = uvs;
        if (first >= 0)
        {
            // 앞면 방향 확인: 첫 구간의 진행 방향 (dr, dy)에서 겉쪽 법선은 (dy, -dr). 첫 두 삼각형의 기하 법선과 비교해 반대면 감는 방향을 뒤집는다
            float dr = profile[first * 2 + 2] - profile[first * 2], dy = profile[first * 2 + 3] - profile[first * 2 + 1];
            var expect = new Vector3(dy, -dr, 0);
            int a0 = first * cols, b0 = a0 + 1, c0 = a0 + cols, d0 = c0 + 1;
            var geo = Vector3.Cross(verts[c0] - verts[a0], verts[b0] - verts[a0]) + Vector3.Cross(verts[c0] - verts[b0], verts[d0] - verts[b0]);
            if (Vector3.Dot(geo, expect) < 0)
                for (int k = 0; k < tris.Count; k += 3) { int tmp = tris[k + 1]; tris[k + 1] = tris[k + 2]; tris[k + 2] = tmp; }
        }
        m.SetTriangles(tris, 0);
        m.RecalculateNormals();
        var nrm = m.normals;
        for (int i = 0; i < n; i++)
        {
            int a = i * cols, b = a + radial;
            var avg = (nrm[a] + nrm[b]).normalized;
            nrm[a] = nrm[b] = avg;
        }
        m.normals = nrm;
        m.RecalculateTangents();
        m.RecalculateBounds();
        meshes[key] = m;
        return m;
    }

    // 폴리라인에서 높이 y에 처음 닿는 자리의 v (0~1). 그림에서 몸통의 특정 높이에 무늬를 놓을 때 쓴다
    static float ProfileV(float[] profile, float y)
    {
        int n = profile.Length / 2;
        float total = 0, at = -1;
        for (int i = 1; i < n; i++)
        {
            float y0 = profile[i * 2 - 1], y1 = profile[i * 2 + 1];
            float seg = Vector2.Distance(new Vector2(profile[i * 2], y1), new Vector2(profile[i * 2 - 2], y0));
            if (at < 0 && seg > 0 && ((y0 <= y && y <= y1) || (y1 <= y && y <= y0)))
                at = total + seg * (Mathf.Approximately(y1, y0) ? 0 : (y - y0) / (y1 - y0));
            total += seg;
        }
        return at < 0 ? 1f : at / Mathf.Max(1e-5f, total);
    }

    // 납작한 원판(고리): 높이 y, 안쪽 반지름 rInner(0이면 꽉 찬 원), 바깥 반지름 rOuter. UV는 평면 (가운데 0.5, 한 변이 지름)
    static Mesh Disc(string key, float rInner, float rOuter, float y, int radial, bool faceUp)
    {
        if (meshes.TryGetValue(key, out var m) && m != null) return m;
        int cols = radial + 1;
        bool hole = rInner > 0;
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();
        for (int ring = 0; ring < 2; ring++)
        {
            float r = ring == 0 ? (hole ? rInner : 0) : rOuter;
            for (int j = 0; j < cols; j++)
            {
                float a = 2 * Mathf.PI * j / radial;
                var p = new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
                verts.Add(p);
                uvs.Add(new Vector2(0.5f + p.x / (2 * rOuter), 0.5f + p.z / (2 * rOuter)));
            }
        }
        for (int j = 0; j < radial; j++)
        {
            int a = j, b = j + 1, c = cols + j, d = cols + j + 1;
            if (hole) { tris.Add(a); tris.Add(c); tris.Add(b); }
            tris.Add(b); tris.Add(c); tris.Add(d);
        }
        var geo = Vector3.Cross(verts[tris[1]] - verts[tris[0]], verts[tris[2]] - verts[tris[0]]);
        if (Vector3.Dot(geo, faceUp ? Vector3.up : Vector3.down) < 0)
            for (int k = 0; k < tris.Count; k += 3) { int tmp = tris[k + 1]; tris[k + 1] = tris[k + 2]; tris[k + 2] = tmp; }
        m = new Mesh { name = key };
        m.SetVertices(verts);
        m.SetUVs(0, uvs);
        m.SetTriangles(tris, 0);
        var nrm = new Vector3[verts.Count];
        for (int i = 0; i < nrm.Length; i++) nrm[i] = faceUp ? Vector3.up : Vector3.down;
        m.normals = nrm;
        m.RecalculateTangents();
        m.RecalculateBounds();
        meshes[key] = m;
        return m;
    }

    // 띠: 가운데 점들(centers)을 지나며 sides 방향으로 width만큼 넓은 양면 띠. UV: u = 길이(m), v = 폭(0~1)
    static Mesh Ribbon(string key, Vector3[] centers, Vector3[] sides, float width)
    {
        if (meshes.TryGetValue(key, out var m) && m != null) return m;
        int n = centers.Length;
        var verts = new Vector3[n * 4];
        var uvs = new Vector2[n * 4];
        float s = 0;
        for (int i = 0; i < n; i++)
        {
            if (i > 0) s += Vector3.Distance(centers[i], centers[i - 1]);
            var half = sides[i].normalized * (width / 2);
            verts[i * 2] = verts[n * 2 + i * 2] = centers[i] - half;
            verts[i * 2 + 1] = verts[n * 2 + i * 2 + 1] = centers[i] + half;
            uvs[i * 2] = uvs[n * 2 + i * 2] = new Vector2(s, 0);
            uvs[i * 2 + 1] = uvs[n * 2 + i * 2 + 1] = new Vector2(s, 1);
        }
        var tris = new List<int>();
        for (int i = 0; i < n - 1; i++)
        {
            int a = i * 2, b = a + 1, c = a + 2, d = a + 3;
            tris.Add(a); tris.Add(c); tris.Add(b);
            tris.Add(b); tris.Add(c); tris.Add(d);
            int o = n * 2;
            tris.Add(o + a); tris.Add(o + b); tris.Add(o + c);
            tris.Add(o + b); tris.Add(o + d); tris.Add(o + c);
        }
        m = new Mesh { name = key };
        m.vertices = verts;
        m.uv = uvs;
        m.SetTriangles(tris, 0);
        m.RecalculateNormals();
        m.RecalculateTangents();
        m.RecalculateBounds();
        meshes[key] = m;
        return m;
    }

    // -z를 보는 네모판 (표지·그림판). UV 0~1
    static Mesh Quad(string key, float w, float h)
    {
        if (meshes.TryGetValue(key, out var m) && m != null) return m;
        m = new Mesh { name = key };
        m.vertices = new[] { new Vector3(-w / 2, -h / 2, 0), new Vector3(w / 2, -h / 2, 0), new Vector3(w / 2, h / 2, 0), new Vector3(-w / 2, h / 2, 0) };
        m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
        m.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
        m.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
        m.RecalculateTangents();
        m.RecalculateBounds();
        meshes[key] = m;
        return m;
    }
}
