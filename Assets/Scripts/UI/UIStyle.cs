using UnityEngine;

// UI용 둥근 사각형(채우기)과 테두리 스프라이트를 코드로 만든다. 9-slice로 늘려 쓴다.
public static class UIStyle
{
    static Sprite fill, frame;

    static Sprite fade;

    public static Sprite Fill => fill != null ? fill : fill = Make(false);
    public static Sprite Frame => frame != null ? frame : frame = Make(true);

    // 왼쪽은 진하고 오른쪽으로 갈수록 투명해지는 그라데이션 (메뉴 배경 가림막)
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

    static Sprite Make(bool outline)
    {
        const int size = 32;
        const float radius = 7f, width = 1.6f;
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
