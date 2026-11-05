using UnityEngine;

// 방공호 출입구(복도 안쪽 철문)와 도시의 방공호 해치(바깥 문)가 같이 쓰는 격벽식 철문 꾸밈새.
// 참고 사진(잠수함 해치·녹슨 격벽문)의 분위기만 따르고 모양은 우리 직사각형 문대로: 볼트 박힌 두꺼운 테두리, 한 단 올라온 문짝 판 두 겹과 가장자리 볼트 줄,
// 경첩 블록 셋(핀·덮개·문짝 쪽 팔), 테두리를 무는 걸쇠 넷, 붉은 핸드휠(링·살 넷·허브·너트·축)과 굽은 붉은 손잡이, 압력계 둘, 세로 레버.
// 문짝 자식은 비정적(출발·귀환 연출에서 문짝이 경첩으로 돈다). 바퀴는 "Wheel" 그룹(연출이 통째로 돌린다)이고 링 반지름은 0.2 (연출의 손이 잡는 반지름)
public static partial class GameSceneBuilder
{
    static Material DoorMat(string key, string fallback) => M.ContainsKey(key) ? M[key] : M[fallback];

    // 핸드휠: pos가 가운데, 링은 x-y 평면(문짝과 나란히). f: 문의 앞쪽(+1/-1, 로컬 z)
    static Transform HandWheel(Transform parent, Vector3 pos, float f)
    {
        var w = Group("Wheel", parent, pos);
        var red = M["paintRed"];
        var dark = M["tire"];
        const int segs = 16;
        const float R = 0.2f;
        for (int i = 0; i < segs; i++)
        {
            float a0 = i * 360f / segs, a1 = (i + 1) * 360f / segs;
            var p0 = new Vector3(Mathf.Cos(a0 * Mathf.Deg2Rad), Mathf.Sin(a0 * Mathf.Deg2Rad), 0) * R;
            var p1 = new Vector3(Mathf.Cos(a1 * Mathf.Deg2Rad), Mathf.Sin(a1 * Mathf.Deg2Rad), 0) * R;
            float len = Vector3.Distance(p0, p1) + 0.014f;
            float ang = Mathf.Atan2(p1.y - p0.y, p1.x - p0.x) * Mathf.Rad2Deg;
            Prim(PrimitiveType.Cylinder, "Rim", w, (p0 + p1) * 0.5f, new Vector3(0.034f, len / 2, 0.034f), red, new Vector3(0, 0, ang - 90f));
        }
        for (int i = 0; i < 4; i++)
        {
            float a = i * 90f + 45f;
            var dir = new Vector3(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad), 0);
            Prim(PrimitiveType.Cylinder, "Spoke", w, dir * (R * 0.5f), new Vector3(0.022f, R * 0.5f, 0.022f), red, new Vector3(0, 0, a - 90f));
        }
        Prim(PrimitiveType.Cylinder, "Hub", w, new Vector3(0, 0, -f * 0.01f), new Vector3(0.09f, 0.03f, 0.09f), dark, new Vector3(90, 0, 0));
        Prim(PrimitiveType.Cylinder, "HubNut", w, new Vector3(0, 0, f * 0.03f), new Vector3(0.04f, 0.012f, 0.04f), M["metal"], new Vector3(90, 0, 0));
        Prim(PrimitiveType.Cylinder, "Axle", w, new Vector3(0, 0, -f * 0.05f), new Vector3(0.05f, 0.05f, 0.05f), dark, new Vector3(90, 0, 0));
        return w;
    }

    // 격벽식 철문 꾸밈. leaf: 문짝 상자(가운데 원점, 두께 t, 앞면 z = f·t/2, group의 직계 자식), group: 문틀 쪽 부모(경첩 블록·테두리는 여기에).
    // rimBars: 문틀 막대가 따로 없으면(도시 해치) 볼트 박힌 테두리 막대도 세운다
    static void BulkheadDoor(Transform group, Transform leaf, float w, float h, float t, float f, bool rimBars)
    {
        var rust = M["metal"];
        var dark = M["tire"];
        var red = M["paintRed"];
        var plate = DoorMat("doorRust", "metal");
        var gauge = DoorMat("gaugeFace", "metal");
        float zf = f * t / 2;
        // 문짝: 한 단 올라온 판 두 겹 (돔 느낌), 가장자리 볼트 줄
        Box("Panel", leaf, new Vector3(0, 0, zf + f * 0.012f), new Vector3(w - 0.2f, h - 0.2f, 0.024f), plate, 1.2f, false, null, false);
        Box("PanelInner", leaf, new Vector3(0, 0.04f, zf + f * 0.03f), new Vector3(w - 0.46f, h - 0.6f, 0.014f), plate, 1.2f, false, null, false);
        int nx = Mathf.Max(3, Mathf.RoundToInt((w - 0.11f) / 0.15f)), ny = Mathf.Max(4, Mathf.RoundToInt((h - 0.11f) / 0.15f));
        for (int i = 0; i <= nx; i++)
        {
            float x = -w / 2 + 0.055f + i * (w - 0.11f) / nx;
            foreach (float y in new[] { h / 2 - 0.055f, -h / 2 + 0.055f })
                Prim(PrimitiveType.Cylinder, "Bolt", leaf, new Vector3(x, y, zf + f * 0.005f), new Vector3(0.028f, 0.007f, 0.028f), dark, new Vector3(90, 0, 0));
        }
        for (int i = 1; i < ny; i++)
        {
            float y = -h / 2 + 0.055f + i * (h - 0.11f) / ny;
            foreach (float x in new[] { -w / 2 + 0.055f, w / 2 - 0.055f })
                Prim(PrimitiveType.Cylinder, "Bolt", leaf, new Vector3(x, y, zf + f * 0.005f), new Vector3(0.028f, 0.007f, 0.028f), dark, new Vector3(90, 0, 0));
        }
        // 걸쇠(dog) 넷: 자유 가장자리(+x) 둘, 위·아래 하나씩. 핀을 축으로 테두리를 무는 쐐기 팔
        foreach (var d in new[] { new Vector3(w / 2 - 0.03f, h * 0.22f, 0), new Vector3(w / 2 - 0.03f, -h * 0.22f, 0), new Vector3(w * 0.2f, h / 2 - 0.03f, 90), new Vector3(w * 0.2f, -h / 2 + 0.03f, -90) })
        {
            var dog = Group("Dog", leaf, new Vector3(d.x, d.y, zf + f * 0.05f));
            dog.localRotation = Quaternion.Euler(0, 0, d.z);
            Box("DogArm", dog, new Vector3(0.02f, 0, 0), new Vector3(0.2f, 0.045f, 0.04f), rust, 1f, false, new Vector3(0, 0, -14f), false);
            Prim(PrimitiveType.Cylinder, "DogPin", dog, new Vector3(-0.06f, 0, 0), new Vector3(0.05f, 0.03f, 0.05f), dark, new Vector3(90, 0, 0));
        }
        // 세로 레버 (자유 가장자리 쪽): 받침 둘 + 긴 막대 + 손잡이 공
        float lx = w / 2 - 0.22f;
        foreach (float y in new[] { -0.16f, 0.16f })
            Box("LeverMount", leaf, new Vector3(lx, y, zf + f * 0.03f), new Vector3(0.05f, 0.05f, 0.06f), dark, 1f, false, null, false);
        Prim(PrimitiveType.Cylinder, "Lever", leaf, new Vector3(lx, 0.02f, zf + f * 0.075f), new Vector3(0.022f, 0.27f, 0.022f), rust);
        Prim(PrimitiveType.Sphere, "LeverKnob", leaf, new Vector3(lx, 0.3f, zf + f * 0.075f), new Vector3(0.045f, 0.045f, 0.045f), dark);
        // 압력계 둘 (경첩 쪽 위): 테·눈금판·바늘
        float gx = -w / 2 + 0.22f;
        foreach (float gy in new[] { h * 0.3f, h * 0.3f - 0.2f })
        {
            Prim(PrimitiveType.Cylinder, "GaugeRim", leaf, new Vector3(gx, gy, zf + f * 0.02f), new Vector3(0.085f, 0.012f, 0.085f), rust, new Vector3(90, 0, 0));
            Prim(PrimitiveType.Cylinder, "GaugeFace", leaf, new Vector3(gx, gy, zf + f * 0.034f), new Vector3(0.07f, 0.002f, 0.07f), gauge, new Vector3(90, 0, 0));
            Box("GaugeNeedle", leaf, new Vector3(gx, gy + 0.012f, zf + f * 0.038f), new Vector3(0.004f, 0.03f, 0.002f), red, 1f, false, new Vector3(0, 0, gy > h * 0.25f ? -35f : 20f), false);
        }
        // 굽은 붉은 손잡이 (바퀴 아래): 가로 막대 + 다리 둘
        float hy = -h * 0.14f;
        Prim(PrimitiveType.Cylinder, "Grip", leaf, new Vector3(0f, hy, zf + f * 0.09f), new Vector3(0.024f, 0.14f, 0.024f), red, new Vector3(0, 0, 90));
        foreach (float ggx in new[] { -0.14f, 0.14f })
            Prim(PrimitiveType.Cylinder, "GripLeg", leaf, new Vector3(ggx, hy, zf + f * 0.05f), new Vector3(0.024f, 0.045f, 0.024f), red, new Vector3(90, 0, 0));
        // 경첩 셋 (-x 가장자리): 문틀 쪽 블록·핀·덮개(정적) + 문짝 쪽 팔(문짝 자식이라 함께 돈다)
        var leafPos = leaf.localPosition;
        foreach (float dy in new[] { -h * 0.36f, 0f, h * 0.36f })
        {
            var block = new Vector3(leafPos.x - w / 2 - 0.09f, leafPos.y + dy, leafPos.z + f * (t / 2 + 0.02f));
            Box("HingeBlock", group, block, new Vector3(0.14f, 0.18f, 0.14f), rust, 1f, false);
            Prim(PrimitiveType.Cylinder, "HingePin", group, block, new Vector3(0.06f, 0.13f, 0.06f), dark);
            foreach (float cap in new[] { -0.12f, 0.12f })
                Prim(PrimitiveType.Cylinder, "HingeCap", group, block + new Vector3(0, cap, 0), new Vector3(0.075f, 0.012f, 0.075f), dark);
            Box("HingeArm", leaf, new Vector3(-w / 2 + 0.07f, dy, zf + f * 0.03f), new Vector3(0.22f, 0.07f, 0.05f), rust, 1f, false, null, false);
        }
        // 테두리: 볼트 박힌 두꺼운 링 (막대는 rimBars일 때만)
        float rz = leafPos.z + f * (t / 2 + 0.02f);
        if (rimBars)
        {
            Box("RimTop", group, new Vector3(leafPos.x, leafPos.y + h / 2 + 0.07f, rz), new Vector3(w + 0.32f, 0.14f, 0.07f), rust, 1f, false);
            Box("RimBottom", group, new Vector3(leafPos.x, leafPos.y - h / 2 - 0.07f, rz), new Vector3(w + 0.32f, 0.14f, 0.07f), rust, 1f, false);
            Box("RimL", group, new Vector3(leafPos.x - w / 2 - 0.09f, leafPos.y, rz), new Vector3(0.14f, h, 0.07f), rust, 1f, false);
            Box("RimR", group, new Vector3(leafPos.x + w / 2 + 0.09f, leafPos.y, rz), new Vector3(0.14f, h, 0.07f), rust, 1f, false);
        }
        int rx = Mathf.RoundToInt((w + 0.18f) / 0.16f), ry = Mathf.RoundToInt(h / 0.16f);
        for (int i = 0; i <= rx; i++)
        {
            float x = leafPos.x - w / 2 - 0.09f + i * (w + 0.18f) / rx;
            foreach (float y in new[] { leafPos.y + h / 2 + 0.07f, leafPos.y - h / 2 - 0.07f })
                Prim(PrimitiveType.Cylinder, "RimBolt", group, new Vector3(x, y, rz + f * 0.04f), new Vector3(0.034f, 0.01f, 0.034f), dark, new Vector3(90, 0, 0));
        }
        for (int i = 1; i < ry; i++)
        {
            float y = leafPos.y - h / 2 + i * h / ry;
            foreach (float x in new[] { leafPos.x - w / 2 - 0.09f, leafPos.x + w / 2 + 0.09f })
                Prim(PrimitiveType.Cylinder, "RimBolt", group, new Vector3(x, y, rz + f * 0.04f), new Vector3(0.034f, 0.01f, 0.034f), dark, new Vector3(90, 0, 0));
        }
    }
}
