using System.Collections.Generic;
using UnityEngine;

// 좀비 종류별 겉모습. 같은 좀비 프리팹의 색·크기를 바꾸고 기본 도형 부품을 붙여 만든다.
// 프리팹 부품 이름(Torso, Rag, Neck, Skull, Jaw, EyeL, EyeR, Arm, Hand, Leg)은 GameSceneBuilder.Prefabs 참고
public static class ZombieLooks
{
    static readonly string[] SkinParts = { "Neck", "Skull", "Jaw", "Arm", "Hand" };

    static readonly Dictionary<(Material, Color), Material> tinted = new Dictionary<(Material, Color), Material>();
    static readonly Dictionary<(Color, Color), Material> glows = new Dictionary<(Color, Color), Material>();
    static readonly Dictionary<PrimitiveType, Mesh> meshes = new Dictionary<PrimitiveType, Mesh>();
    static Material glowSource; // 발광이 켜진 좀비 눈 재질을 복사해 쓴다 (빌드에서 발광 셰이더가 빠지지 않도록)

    // 원거리 좀비 독액 재질
    public static Material Spit => Glow(new Color(0.55f, 1f, 0.3f), new Color(1.6f, 3.6f, 0.5f));

    // 겉모습을 바꾼다. 약탈자면 야구방망이 Transform을 돌려준다
    public static Transform Apply(Zombie z, ZombieKind kind)
    {
        var map = Collect(z.transform);
        if (kind == ZombieKind.Normal) return null;
        Material cloth = First(map, "Torso"), skin = First(map, "Skull");
        switch (kind)
        {
            case ZombieKind.Leather: Leather(z, map, cloth); break;
            case ZombieKind.Ranged: Ranged(z, map); break;
            case ZombieKind.Heavy: Heavy(z, map, cloth, skin); break;
            case ZombieKind.Raider: return Raider(z, map, cloth);
        }
        return null;
    }

    // 가죽 좀비: 덩치가 크고 갈색 가죽 갑옷을 둘렀다
    static void Leather(Zombie z, Dictionary<string, List<Renderer>> map, Material cloth)
    {
        var dark = new Color(0.17f, 0.1f, 0.06f);
        Recolor(map, "Torso", new Color(0.36f, 0.22f, 0.12f));
        Recolor(map, "Rag", dark);
        Recolor(map, "Leg", new Color(0.22f, 0.15f, 0.1f));
        foreach (var n in SkinParts) Recolor(map, n, new Color(0.42f, 0.47f, 0.36f));
        Resize(map, "Torso", new Vector3(1.18f, 1f, 1.3f));
        Resize(map, "Arm", new Vector3(1.25f, 1f, 1.25f));
        Resize(map, "Leg", new Vector3(1.15f, 1f, 1.15f));

        var plate = Tint(cloth, new Color(0.45f, 0.28f, 0.15f));
        var strap = Tint(cloth, dark);
        Part(PrimitiveType.Cube, "ChestPlate", z.body, new Vector3(0, 0.4f, 0.18f), new Vector3(0.5f, 0.34f, 0.05f), plate, new Vector3(-4, 0, 0));
        Part(PrimitiveType.Cube, "BellyPlate", z.body, new Vector3(0, 0.15f, 0.175f), new Vector3(0.44f, 0.16f, 0.05f), plate, new Vector3(4, 0, 2));
        Part(PrimitiveType.Cube, "BackPlate", z.body, new Vector3(0, 0.36f, -0.18f), new Vector3(0.5f, 0.46f, 0.05f), plate);
        Part(PrimitiveType.Cube, "Strap", z.body, new Vector3(0, 0.32f, 0.21f), new Vector3(0.06f, 0.72f, 0.02f), strap, new Vector3(0, 0, 35));
        Part(PrimitiveType.Cube, "Belt", z.body, new Vector3(0, 0.03f, 0), new Vector3(0.58f, 0.08f, 0.38f), strap);
        Part(PrimitiveType.Cube, "PadL", z.armL, Vector3.zero, new Vector3(0.22f, 0.1f, 0.22f), plate, new Vector3(0, 0, -15));
        Part(PrimitiveType.Cube, "PadR", z.armR, Vector3.zero, new Vector3(0.22f, 0.1f, 0.22f), plate, new Vector3(0, 0, 15));
        Part(PrimitiveType.Cube, "BracerL", z.armL, new Vector3(0, -0.47f, 0), new Vector3(0.17f, 0.18f, 0.17f), strap);
        Part(PrimitiveType.Cube, "BracerR", z.armR, new Vector3(0, -0.47f, 0), new Vector3(0.17f, 0.18f, 0.17f), strap);
        Part(PrimitiveType.Cube, "WrapL", z.legL, new Vector3(0, -0.28f, 0), new Vector3(0.21f, 0.24f, 0.23f), plate);
        Part(PrimitiveType.Cube, "WrapR", z.legR, new Vector3(0, -0.28f, 0), new Vector3(0.21f, 0.24f, 0.23f), plate);
        Part(PrimitiveType.Sphere, "Cap", z.head, new Vector3(0, 0.2f, 0), new Vector3(0.26f, 0.19f, 0.28f), plate);
    }

    // 원거리 좀비: 깡마르고 병든 초록빛, 몸 곳곳에 빛나는 고름 주머니
    static void Ranged(Zombie z, Dictionary<string, List<Renderer>> map)
    {
        foreach (var n in SkinParts) Recolor(map, n, new Color(0.42f, 0.58f, 0.28f));
        Recolor(map, "Torso", new Color(0.36f, 0.37f, 0.29f));
        Recolor(map, "Rag", new Color(0.3f, 0.32f, 0.24f));
        Resize(map, "Torso", new Vector3(0.78f, 1.04f, 0.8f));
        Resize(map, "Arm", new Vector3(0.72f, 1f, 0.72f));
        Resize(map, "Leg", new Vector3(0.75f, 1f, 0.75f));
        var eye = Glow(new Color(0.6f, 1f, 0.4f), new Color(0.9f, 2.4f, 0.4f));
        SetMaterial(map, "EyeL", eye);
        SetMaterial(map, "EyeR", eye);

        var pus = Glow(new Color(0.5f, 1f, 0.3f), new Color(1.2f, 3f, 0.4f));
        Pustule(z.body, new Vector3(0.11f, 0.45f, 0.1f), 0.075f, pus);
        Pustule(z.body, new Vector3(-0.09f, 0.26f, 0.1f), 0.09f, pus);
        Pustule(z.body, new Vector3(0.04f, 0.12f, 0.095f), 0.06f, pus);
        Pustule(z.body, new Vector3(-0.15f, 0.52f, 0.02f), 0.065f, pus);
        Pustule(z.body, new Vector3(0.06f, 0.3f, -0.1f), 0.07f, pus);
        Pustule(z.head, new Vector3(0.08f, 0.21f, 0.07f), 0.05f, pus);
        Pustule(z.head, new Vector3(-0.1f, 0.08f, 0.05f), 0.045f, pus);
        Pustule(z.armR, new Vector3(0.05f, -0.22f, 0.04f), 0.055f, pus);
        Pustule(z.armL, new Vector3(-0.04f, -0.42f, 0.04f), 0.045f, pus);
        // 목에 부풀어 오른 독주머니
        Part(PrimitiveType.Sphere, "ThroatSac", z.body, new Vector3(0, 0.64f, 0.06f), new Vector3(0.12f, 0.1f, 0.1f), pus, null, false);

        // 몸에서 새어 나오는 희미한 초록 빛
        if (z.body != null)
        {
            var go = new GameObject("PustuleGlow");
            go.transform.SetParent(z.body, false);
            go.transform.localPosition = new Vector3(0, 0.4f, 0.3f);
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(0.5f, 1f, 0.3f);
            l.range = 2.5f;
            l.intensity = 1.5f;
            l.shadows = LightShadows.None;
        }
    }

    // 거대 좀비: 퉁퉁 부은 몸, 커다란 손, 붉은 눈
    static void Heavy(Zombie z, Dictionary<string, List<Renderer>> map, Material cloth, Material skin)
    {
        var bloated = new Color(0.55f, 0.55f, 0.47f);
        foreach (var n in SkinParts) Recolor(map, n, bloated);
        Recolor(map, "Torso", new Color(0.22f, 0.2f, 0.19f));
        Recolor(map, "Rag", new Color(0.2f, 0.18f, 0.17f));
        Recolor(map, "Leg", new Color(0.13f, 0.14f, 0.16f));
        Resize(map, "Torso", new Vector3(1.35f, 1.06f, 1.45f));
        Resize(map, "Arm", new Vector3(1.45f, 1f, 1.45f));
        Resize(map, "Hand", new Vector3(1.5f, 1.3f, 1.6f));
        Resize(map, "Leg", new Vector3(1.4f, 1f, 1.4f));
        if (z.head != null) z.head.localScale = Vector3.one * 0.85f;

        var gut = Tint(skin, new Color(0.58f, 0.56f, 0.47f));
        var scar = RuntimeVisuals.Mat(new Color(0.18f, 0.08f, 0.07f), 0.3f);
        Part(PrimitiveType.Sphere, "Gut", z.body, new Vector3(0, 0.2f, 0.11f), new Vector3(0.52f, 0.46f, 0.4f), gut);
        Part(PrimitiveType.Cube, "Scar1", z.body, new Vector3(0.03f, 0.24f, 0.3f), new Vector3(0.12f, 0.018f, 0.02f), scar, new Vector3(0, 0, 25), false);
        Part(PrimitiveType.Cube, "Scar2", z.body, new Vector3(-0.06f, 0.14f, 0.3f), new Vector3(0.1f, 0.018f, 0.02f), scar, new Vector3(0, 0, -15), false);
        Part(PrimitiveType.Cube, "Hump", z.body, new Vector3(0.05f, 0.55f, -0.16f), new Vector3(0.42f, 0.25f, 0.2f), gut, new Vector3(20, 10, 0));
        Part(PrimitiveType.Cube, "Shreds", z.body, new Vector3(0, 0.5f, 0.16f), new Vector3(0.5f, 0.2f, 0.08f), Tint(cloth, new Color(0.2f, 0.18f, 0.17f)), new Vector3(-10, 0, 6));
        var eye = Glow(new Color(0.9f, 0.35f, 0.25f), new Color(1.6f, 0.25f, 0.12f));
        SetMaterial(map, "EyeL", eye);
        SetMaterial(map, "EyeR", eye);
    }

    // 약탈자: 어두운 옷, 후드·방독면·고글, 오른손에 못 박힌 야구방망이
    static Transform Raider(Zombie z, Dictionary<string, List<Renderer>> map, Material cloth)
    {
        var jacket = new Color(0.12f, 0.12f, 0.13f);
        var human = new Color(0.6f, 0.46f, 0.38f);
        Recolor(map, "Torso", jacket);
        Recolor(map, "Arm", jacket);
        Recolor(map, "Leg", new Color(0.09f, 0.09f, 0.1f));
        Recolor(map, "Hand", new Color(0.1f, 0.09f, 0.08f)); // 장갑
        Recolor(map, "Skull", human);
        Recolor(map, "Neck", human);
        Hide(map, "Rag");
        Hide(map, "Jaw");
        Hide(map, "EyeL");
        Hide(map, "EyeR");
        Resize(map, "Torso", new Vector3(1.05f, 1f, 1.1f));

        var dark = RuntimeVisuals.Mat(new Color(0.07f, 0.07f, 0.08f), 0.2f);
        var lens = RuntimeVisuals.Mat(new Color(0.03f, 0.03f, 0.03f), 0.95f);
        var metal = RuntimeVisuals.Mat(new Color(0.35f, 0.36f, 0.36f), 0.6f);
        var mask = RuntimeVisuals.Mat(new Color(0.18f, 0.2f, 0.17f), 0.3f);
        var hood = Tint(cloth, new Color(0.1f, 0.1f, 0.11f));
        Part(PrimitiveType.Sphere, "Hood", z.head, new Vector3(0, 0.16f, -0.035f), new Vector3(0.27f, 0.31f, 0.27f), hood);
        Part(PrimitiveType.Cube, "Mask", z.head, new Vector3(0, 0.05f, 0.105f), new Vector3(0.19f, 0.12f, 0.07f), mask);
        Part(PrimitiveType.Cylinder, "Filter", z.head, new Vector3(0, 0.03f, 0.15f), new Vector3(0.07f, 0.025f, 0.07f), metal, new Vector3(90, 0, 0), false);
        Part(PrimitiveType.Cylinder, "GoggleL", z.head, new Vector3(-0.05f, 0.16f, 0.128f), new Vector3(0.055f, 0.012f, 0.055f), lens, new Vector3(90, 0, 0), false);
        Part(PrimitiveType.Cylinder, "GoggleR", z.head, new Vector3(0.05f, 0.16f, 0.128f), new Vector3(0.055f, 0.012f, 0.055f), lens, new Vector3(90, 0, 0), false);
        Part(PrimitiveType.Cube, "GoggleStrap", z.head, new Vector3(0, 0.16f, 0.01f), new Vector3(0.245f, 0.03f, 0.25f), dark, null, false);
        Part(PrimitiveType.Cube, "Vest", z.body, new Vector3(0, 0.36f, 0), new Vector3(0.5f, 0.42f, 0.3f), Tint(cloth, new Color(0.2f, 0.21f, 0.17f)));
        Part(PrimitiveType.Cube, "Pack", z.body, new Vector3(0, 0.38f, -0.2f), new Vector3(0.34f, 0.4f, 0.16f), Tint(cloth, new Color(0.22f, 0.22f, 0.16f)));

        if (z.armR == null) return null;
        // 손잡이 끝을 쥐고, 방망이는 팔 아래·앞쪽으로 뻗는다 (팔을 뒤로 젖히면 머리 뒤로 넘어간다)
        var bat = new GameObject("Bat").transform;
        bat.SetParent(z.armR, false);
        bat.localPosition = new Vector3(0, -0.68f, 0.03f);
        bat.localRotation = Quaternion.Euler(150, 0, 0);
        var wood = RuntimeVisuals.Mat(new Color(0.45f, 0.3f, 0.17f), 0.35f);
        Part(PrimitiveType.Sphere, "Knob", bat, new Vector3(0, -0.06f, 0), new Vector3(0.05f, 0.03f, 0.05f), wood, null, false);
        Part(PrimitiveType.Cylinder, "Handle", bat, new Vector3(0, 0.18f, 0), new Vector3(0.035f, 0.24f, 0.035f), wood);
        Part(PrimitiveType.Cylinder, "Barrel", bat, new Vector3(0, 0.6f, 0), new Vector3(0.065f, 0.22f, 0.065f), wood);
        Part(PrimitiveType.Cube, "Nail1", bat, new Vector3(0.04f, 0.66f, 0), new Vector3(0.05f, 0.008f, 0.008f), metal, null, false);
        Part(PrimitiveType.Cube, "Nail2", bat, new Vector3(0, 0.56f, 0.04f), new Vector3(0.008f, 0.008f, 0.05f), metal, null, false);
        Part(PrimitiveType.Cube, "Nail3", bat, new Vector3(-0.04f, 0.74f, 0), new Vector3(0.05f, 0.008f, 0.008f), metal, null, false);
        return bat;
    }

    // ---------- 도우미 ----------

    static Dictionary<string, List<Renderer>> Collect(Transform root)
    {
        var map = new Dictionary<string, List<Renderer>>();
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            if (!map.TryGetValue(r.name, out var list)) map[r.name] = list = new List<Renderer>();
            list.Add(r);
            if (glowSource == null && r.name == "EyeL") glowSource = r.sharedMaterial;
        }
        return map;
    }

    static Material First(Dictionary<string, List<Renderer>> map, string name) =>
        map.TryGetValue(name, out var list) && list.Count > 0 ? list[0].sharedMaterial : null;

    static void Recolor(Dictionary<string, List<Renderer>> map, string name, Color color)
    {
        if (!map.TryGetValue(name, out var list)) return;
        foreach (var r in list) r.sharedMaterial = Tint(r.sharedMaterial, color);
    }

    static void SetMaterial(Dictionary<string, List<Renderer>> map, string name, Material m)
    {
        if (!map.TryGetValue(name, out var list)) return;
        foreach (var r in list) r.sharedMaterial = m;
    }

    static void Resize(Dictionary<string, List<Renderer>> map, string name, Vector3 mul)
    {
        if (!map.TryGetValue(name, out var list)) return;
        foreach (var r in list) r.transform.localScale = Vector3.Scale(r.transform.localScale, mul);
    }

    static void Hide(Dictionary<string, List<Renderer>> map, string name)
    {
        if (!map.TryGetValue(name, out var list)) return;
        foreach (var r in list) r.enabled = false;
    }

    static void Pustule(Transform parent, Vector3 pos, float size, Material m) =>
        Part(PrimitiveType.Sphere, "Pustule", parent, pos, Vector3.one * size, m, null, false);

    // 원래 재질(텍스처 유지)에 색만 바꾼 복사본. 같은 조합은 공유한다
    static Material Tint(Material src, Color color)
    {
        if (src == null) return RuntimeVisuals.Mat(color);
        var key = (src, color);
        if (tinted.TryGetValue(key, out var m) && m != null) return m;
        m = new Material(src) { name = src.name + "_Tint" };
        m.SetColor("_BaseColor", color);
        if (m.HasProperty("_Color")) m.SetColor("_Color", color);
        tinted[key] = m;
        return m;
    }

    static Material Glow(Color baseColor, Color emission)
    {
        var key = (baseColor, emission);
        if (glows.TryGetValue(key, out var m) && m != null) return m;
        m = glowSource != null ? new Material(glowSource) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.name = "ZombieGlow";
        m.EnableKeyword("_EMISSION");
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        m.SetColor("_BaseColor", baseColor);
        m.SetColor("_EmissionColor", emission);
        glows[key] = m;
        return m;
    }

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
