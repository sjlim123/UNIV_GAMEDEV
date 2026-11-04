using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// 아이템 아이콘. ItemDef.Icon 그림(Resources/ItemIcons/{이름}.png)이 있으면 그것을 쓰고,
// 없으면 아이템 모양을 작은 카메라로 한 번 찍어 쓴다 (아이콘 그림 에셋이 없어도 된다)
// 그림을 바꾸려면 같은 이름의 PNG만 갈아 끼우면 된다 (투명 배경 정사각형 권장)
public static class ItemIcons
{
    const int Size = 128;
    const string Folder = "ItemIcons/";
    static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    public static Sprite Get(string id)
    {
        if (cache.TryGetValue(id, out var s) && s != null) return s;
        s = Load2D(id);
        if (s == null) s = Render(id);
        cache[id] = s;
        return s;
    }

    static readonly Dictionary<string, Sprite> outlined = new Dictionary<string, Sprite>();

    // 윤곽선을 두른 아이콘: 바탕 없이 화면에 띄워도 눈에 띄게 그림의 투명한 가장자리를 2px 두께의 color로 두른다 (HUD 손전등·배터리)
    public static Sprite Outlined(string id, Color color)
    {
        if (outlined.TryGetValue(id, out var cached) && cached != null) return cached;
        var src = Get(id).texture;
        int w = src.width, h = src.height;
        // 읽기 설정과 상관없이 픽셀을 얻으려고 렌더 텍스처를 거친다
        var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
        var prev = RenderTexture.active;
        Graphics.Blit(src, rt);
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "IconOutline_" + id, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        var px = tex.GetPixels32();
        var result = (Color32[])px.Clone();
        var line = (Color32)color;
        int radius = Mathf.Max(2, w / 48);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (px[y * w + x].a >= 40) continue;
                bool near = false;
                for (int dy = -radius; dy <= radius && !near; dy++)
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        if (dx * dx + dy * dy > radius * radius + 1) continue;
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                        if (px[ny * w + nx].a >= 100) { near = true; break; }
                    }
                if (near) result[y * w + x] = line;
            }
        tex.SetPixels32(result);
        tex.Apply();
        var sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect);
        outlined[id] = sprite;
        return sprite;
    }

    // 2D 아이콘 그림. 텍스처 가져오기 설정(Sprite 여부)과 상관없이 코드에서 스프라이트를 만든다
    static Sprite Load2D(string id)
    {
        string icon = ItemDatabase.Get(id).Icon;
        if (string.IsNullOrEmpty(icon)) return null;
        var tex = Resources.Load<Texture2D>(Folder + icon);
        if (tex == null) return null;
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect);
    }

    // 납작한 아이템(합금판)은 윗면이 잘 보이게 카메라 쪽으로 조금 세워서 찍는다
    static float IconTilt(string id) => id == "alloy_plate" ? -30f : 0f;

    static Sprite Render(string id)
    {
        var stage = new GameObject("IconStage");
        stage.transform.position = new Vector3(0, -500, 0);
        var item = new GameObject("Item");
        item.transform.SetParent(stage.transform, false);
        // 투명한 부품(페트병 껍질)은 아이콘에서 더 진하게 그리도록 알린다
        ItemVisuals.IconMode = true;
        try { ItemVisuals.Build(id, item.transform); }
        finally { ItemVisuals.IconMode = false; }
        item.transform.localRotation = Quaternion.Euler(IconTilt(id), 35, 0);

        var bounds = new Bounds(stage.transform.position, Vector3.zero);
        bool first = true;
        foreach (var r in item.GetComponentsInChildren<Renderer>())
        {
            if (first) { bounds = r.bounds; first = false; }
            else bounds.Encapsulate(r.bounds);
        }

        var camGo = new GameObject("IconCamera");
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z) * 1.25f + 0.01f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0, 0, 0, 0);
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = 10f;
        cam.enabled = false;
        cam.GetUniversalAdditionalCameraData().renderPostProcessing = false;
        var dir = Quaternion.Euler(28, 0, 0) * Vector3.forward;
        camGo.transform.position = bounds.center - dir * 3f;
        camGo.transform.rotation = Quaternion.LookRotation(dir);

        var key = new GameObject("IconKeyLight").AddComponent<Light>();
        key.type = LightType.Directional;
        key.intensity = 2.2f;
        key.transform.rotation = Quaternion.Euler(40, -30, 0);
        var fill = new GameObject("IconFillLight").AddComponent<Light>();
        fill.type = LightType.Directional;
        fill.intensity = 0.9f;
        fill.color = new Color(0.8f, 0.85f, 1f);
        fill.transform.rotation = Quaternion.Euler(10, 150, 0);

        // 아이콘을 찍는 동안만 안개와 어두운 환경광을 끈다
        bool fog = RenderSettings.fog;
        var ambient = RenderSettings.ambientLight;
        RenderSettings.fog = false;
        RenderSettings.ambientLight = new Color(0.45f, 0.45f, 0.45f);

        var rt = RenderTexture.GetTemporary(Size, Size, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
#if UNITY_EDITOR
        // 편집기는 처음 쓰는 셰이더 변형을 첫 렌더에서 건너뛰고 컴파일만 한다 (페트병의 투명 플라스틱이 빠진 채 찍혔다).
        // 비동기 컴파일이 켜져 있으면 하늘색 임시 셰이더로 그리므로 끄고, 두 번 찍어 두 번째 결과를 쓴다 (빌드에서는 미리 컴파일되어 있어 한 번이면 된다)
        bool asyncWas = UnityEditor.ShaderUtil.allowAsyncCompilation;
        UnityEditor.ShaderUtil.allowAsyncCompilation = false;
        cam.Render();
        cam.Render();
        UnityEditor.ShaderUtil.allowAsyncCompilation = asyncWas;
#else
        cam.Render();
#endif
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { name = "Icon_" + id };
        tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;
        cam.targetTexture = null;
        RenderTexture.ReleaseTemporary(rt);

        RenderSettings.fog = fog;
        RenderSettings.ambientLight = ambient;
        Object.DestroyImmediate(stage);
        Object.DestroyImmediate(camGo);
        Object.DestroyImmediate(key.gameObject);
        Object.DestroyImmediate(fill.gameObject);

        return Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f));
    }
}
