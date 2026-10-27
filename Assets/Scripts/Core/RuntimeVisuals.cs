using System.Collections.Generic;
using UnityEngine;

// 실행 중에 기본 도형으로 간단한 모양(아이템, 무기)을 만드는 도우미
public static class RuntimeVisuals
{
    static readonly Dictionary<Color, Material> materials = new Dictionary<Color, Material>();
    static Shader lit;

    public static Material Mat(Color color, float smoothness = 0.25f)
    {
        var key = new Color(color.r, color.g, color.b, smoothness);
        if (materials.TryGetValue(key, out var m) && m != null) return m;
        if (lit == null) lit = Shader.Find("Universal Render Pipeline/Lit");
        m = new Material(lit);
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Smoothness", smoothness);
        materials[key] = m;
        return m;
    }

    public static GameObject Part(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 scale, Color color, float smoothness = 0.25f, Vector3? euler = null)
    {
        var go = GameObject.CreatePrimitive(type);
        Object.Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = scale;
        if (euler.HasValue) go.transform.localEulerAngles = euler.Value;
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = Mat(color, smoothness);
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        return go;
    }
}
