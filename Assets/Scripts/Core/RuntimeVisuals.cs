using System.Collections.Generic;
using UnityEngine;

// 실행 중에 기본 도형으로 간단한 모양(아이템, 무기)을 만드는 도우미
public static class RuntimeVisuals
{
    static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
    static Shader lit;

    // 색·매끈함·금속성이 같은 재질은 하나를 함께 쓴다.
    // metallic이 높을수록 주변을 비추는 쇠처럼 보인다 (어두운 방에서는 너무 높이면 검게 보이므로 큰 면은 0.6 아래로)
    public static Material Mat(Color color, float smoothness = 0.25f, float metallic = 0f)
    {
        string key = color.r + "," + color.g + "," + color.b + "/" + smoothness + "/" + metallic;
        if (materials.TryGetValue(key, out var m) && m != null) return m;
        if (lit == null) lit = Shader.Find("Universal Render Pipeline/Lit");
        m = new Material(lit) { name = "Runtime " + key };
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Smoothness", smoothness);
        m.SetFloat("_Metallic", metallic);
        materials[key] = m;
        return m;
    }

    public static GameObject Part(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 scale, Color color, float smoothness = 0.25f, Vector3? euler = null, float metallic = 0f)
    {
        var go = GameObject.CreatePrimitive(type);
        // 편집기(미리보기·검사 도구)에서 부를 때는 Destroy를 쓸 수 없다
        if (Application.isPlaying) Object.Destroy(go.GetComponent<Collider>());
        else Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = scale;
        if (euler.HasValue) go.transform.localEulerAngles = euler.Value;
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = Mat(color, smoothness, metallic);
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        return go;
    }
}
