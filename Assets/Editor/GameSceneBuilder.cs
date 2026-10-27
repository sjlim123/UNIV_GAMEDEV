using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// 메뉴 Tools > 핵폭발 생존 > 게임 씬 전체 생성
// 방공호(Shelter)·폐허 도시(City) 씬, 플레이어/좀비/들개 프리팹, 머티리얼, 포스트 프로세싱을 한 번에 만든다.
// 다시 실행하면 두 씬을 처음부터 새로 만든다 (씬에서 손으로 고친 내용은 사라짐).
public static class GameSceneBuilder
{
    const string TexDir = "Assets/ThirdParty/PolyHaven";
    const string MatDir = "Assets/Materials/Game";
    const string GenDir = "Assets/Generated";
    const string PrefabDir = "Assets/Prefabs";
    const string SceneDir = "Assets/Scenes";
    const string SettingsDir = "Assets/Settings";

    static Dictionary<string, Mesh> meshCache;
    static Object meshContainer;
    static System.Random rng;
    static readonly Dictionary<string, Material> M = new Dictionary<string, Material>();

    [MenuItem("Tools/핵폭발 생존/게임 씬 전체 생성")]
    public static void BuildAll()
    {
        EnsureFolder("Assets", "Materials");
        EnsureFolder("Assets/Materials", "Game");
        EnsureFolder("Assets", "Generated");
        EnsureFolder("Assets", "Prefabs");
        EnsureFolder("Assets", "Scenes");
        EnsureFolder("Assets", "Resources");

        CreateBalanceAsset();
        ConfigureTextures();
        ConfigurePipeline();
        CreateMaterials();
        var player = BuildPlayerPrefab();
        var zombie = BuildZombiePrefab();
        var dog = BuildDogPrefab();
        BuildShelterScene(player);
        BuildCityScene(player, zombie, dog);
        BuildMenuScene(zombie);

        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(SceneDir + "/MainMenu.unity", true),
            new EditorBuildSettingsScene(SceneDir + "/Shelter.unity", true),
            new EditorBuildSettingsScene(SceneDir + "/City.unity", true),
            new EditorBuildSettingsScene(SceneDir + "/SampleScene.unity", false),
        };
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene(SceneDir + "/MainMenu.unity");
        Debug.Log("[GameSceneBuilder] MainMenu / Shelter / City 씬 생성 완료");
    }

    // 밸런스 에셋은 팀이 고친 값을 지키기 위해 없을 때만 만든다
    static void CreateBalanceAsset()
    {
        const string path = "Assets/Resources/GameBalance.asset";
        if (AssetDatabase.LoadAssetAtPath<GameBalance>(path) == null)
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<GameBalance>(), path);
    }

    // ================= 공통 설정 =================

    static void EnsureFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
    }

    static void ConfigureTextures()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { TexDir }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = path.Contains("_nor_") ? TextureImporterType.NormalMap : TextureImporterType.Default;
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.anisoLevel = 4;
            imp.maxTextureSize = 1024;
            imp.SaveAndReimport();
        }
    }

    // 손전등 그림자 등을 위해 URP 설정을 조정한다
    static void ConfigurePipeline()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset", new[] { "Assets" }))
        {
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset == null) continue;
            var so = new SerializedObject(asset);
            SetProp(so, "m_AdditionalLightsRenderingMode", 1);
            SetProp(so, "m_AdditionalLightShadowsSupported", true);
            SetProp(so, "m_AdditionalLightsPerObjectLimit", 8);
            SetProp(so, "m_SoftShadowsSupported", true);
            SetProp(so, "m_ShadowDistance", 40f);
            SetProp(so, "m_AdditionalLightsShadowmapResolution", 4096);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        // Forward+ : 한 물체에 닿는 조명 개수 제한이 없다 (모닥불·가로등이 많은 도시용)
        foreach (var guid in AssetDatabase.FindAssets("t:UniversalRendererData", new[] { "Assets" }))
        {
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(AssetDatabase.GUIDToAssetPath(guid));
            if (data == null) continue;
            var so = new SerializedObject(data);
            SetProp(so, "m_RenderingMode", 2);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    static void SetProp(SerializedObject so, string name, object value)
    {
        var p = so.FindProperty(name);
        if (p == null) return;
        if (value is bool b) p.boolValue = b;
        else if (value is int i) p.intValue = i;
        else if (value is float f) p.floatValue = f;
    }

    static void CreateMaterials()
    {
        M["wall"] = TexMat("M_ConcreteWall", "concrete_wall_008", new Color(0.72f, 0.71f, 0.69f));
        M["cracked"] = TexMat("M_CrackedWall", "cracked_concrete_wall", new Color(0.68f, 0.68f, 0.66f));
        M["dirty"] = TexMat("M_DirtyConcrete", "dirty_concrete", new Color(0.66f, 0.66f, 0.64f));
        M["asphalt"] = TexMat("M_Asphalt", "asphalt_02", new Color(0.68f, 0.68f, 0.68f), 0.2f);
        M["metal"] = TexMat("M_RustyMetal", "rusty_metal_02", new Color(0.8f, 0.76f, 0.72f), 0.35f, 0.4f);
        M["brick"] = TexMat("M_Brick", "brick_wall_10", new Color(0.62f, 0.56f, 0.52f));
        M["floor"] = TexMat("M_ConcreteFloor", "concrete_floor_worn_001", new Color(0.68f, 0.68f, 0.66f), 0.2f);
        M["wood"] = TexMat("M_WoodPlanks", "wood_planks_dirt", new Color(0.66f, 0.6f, 0.55f));
        M["glass"] = TexMat("M_DarkWindow", null, new Color(0.02f, 0.025f, 0.03f), 0.85f);
        M["line"] = TexMat("M_RoadLine", null, new Color(0.5f, 0.46f, 0.33f), 0.1f);
        M["tire"] = TexMat("M_Tire", null, new Color(0.04f, 0.04f, 0.04f), 0.3f);
        M["paintRed"] = TexMat("M_PaintRed", "rusty_metal_02", new Color(0.5f, 0.12f, 0.08f), 0.3f, 0.3f);
        M["paintBlue"] = TexMat("M_PaintBlue", "rusty_metal_02", new Color(0.2f, 0.32f, 0.5f), 0.3f, 0.3f);
        M["cloth"] = TexMat("M_Cloth", "dirty_concrete", new Color(0.62f, 0.58f, 0.5f), 0.05f);
        M["blanket"] = TexMat("M_Blanket", "dirty_concrete", new Color(0.3f, 0.32f, 0.26f), 0.05f);
        M["zSkin"] = TexMat("M_ZombieSkin", "dirty_concrete", new Color(0.5f, 0.56f, 0.44f), 0.4f);
        M["zCloth"] = TexMat("M_ZombieShirt", "cracked_concrete_wall", new Color(0.28f, 0.24f, 0.22f), 0.1f);
        M["zPants"] = TexMat("M_ZombiePants", "dirty_concrete", new Color(0.17f, 0.19f, 0.24f), 0.1f);
        M["zEye"] = Emissive("M_ZombieEye", new Color(0.8f, 0.78f, 0.6f), new Color(0.5f, 0.45f, 0.3f));
        M["fur"] = TexMat("M_DogFur", "dirty_concrete", new Color(0.33f, 0.26f, 0.19f), 0.15f);
        M["coat"] = TexMat("M_TraderCoat", "cracked_concrete_wall", new Color(0.3f, 0.31f, 0.22f), 0.1f);
        M["skin"] = TexMat("M_Skin", null, new Color(0.62f, 0.48f, 0.4f), 0.3f);
        M["bulb"] = Emissive("M_Bulb", new Color(1f, 0.85f, 0.6f), new Color(2.5f, 1.8f, 1.0f));
        M["redLamp"] = Emissive("M_RedLamp", new Color(1f, 0.15f, 0.1f), new Color(3f, 0.3f, 0.15f));
        M["lantern"] = Emissive("M_Lantern", new Color(1f, 0.7f, 0.4f), new Color(3f, 1.6f, 0.6f));
        M["fire"] = ParticleMat("M_FireParticle", new Color(1f, 0.55f, 0.2f, 1f), true);
        M["ash"] = ParticleMat("M_AshParticle", new Color(0.55f, 0.55f, 0.53f, 0.8f), false);
    }

    static Material LoadOrCreate(string name, Shader shader)
    {
        string path = $"{MatDir}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(shader);
            AssetDatabase.CreateAsset(m, path);
        }
        m.shader = shader;
        return m;
    }

    static Material TexMat(string name, string tex, Color tint, float smoothness = 0.15f, float metallic = 0f)
    {
        var m = LoadOrCreate(name, Shader.Find("Universal Render Pipeline/Lit"));
        if (tex != null)
        {
            var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/{tex}_diff_1k.jpg");
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/{tex}_nor_gl_1k.jpg");
            m.SetTexture("_BaseMap", albedo);
            m.SetTexture("_MainTex", albedo);
            if (normal != null)
            {
                m.SetTexture("_BumpMap", normal);
                m.SetFloat("_BumpScale", 1f);
                m.EnableKeyword("_NORMALMAP");
            }
        }
        m.SetColor("_BaseColor", tint);
        m.SetColor("_Color", tint);
        m.SetFloat("_Smoothness", smoothness);
        m.SetFloat("_Metallic", metallic);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material Emissive(string name, Color color, Color emission)
    {
        var m = TexMat(name, null, color, 0.5f);
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", emission);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material ParticleMat(string name, Color color, bool additive)
    {
        var src = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat");
        var m = LoadOrCreate(name, Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        if (src != null) m.CopyPropertiesFromMaterial(src);
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Surface", 1);
        m.SetFloat("_Blend", additive ? 2 : 0);
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(m);
        return m;
    }

    static VolumeProfile MakeProfile(string name, float exposure, float saturation, Color filter, float grain, float contrast)
    {
        string path = $"{SettingsDir}/{name}.asset";
        AssetDatabase.DeleteAsset(path);
        var p = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(p, path);

        T Add<T>() where T : VolumeComponent
        {
            var c = p.Add<T>(true);
            c.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(c, p);
            return c;
        }

        Add<Tonemapping>().mode.Override(TonemappingMode.ACES);
        var ca = Add<ColorAdjustments>();
        ca.postExposure.Override(exposure);
        ca.contrast.Override(contrast);
        ca.saturation.Override(saturation);
        ca.colorFilter.Override(filter);
        var vg = Add<Vignette>();
        vg.intensity.Override(0.45f);
        vg.smoothness.Override(0.55f);
        var fg = Add<FilmGrain>();
        fg.type.Override(FilmGrainLookup.Medium3);
        fg.intensity.Override(grain);
        fg.response.Override(0.75f);
        var bl = Add<Bloom>();
        bl.intensity.Override(0.9f);
        bl.threshold.Override(0.85f);
        bl.scatter.Override(0.7f);
        Add<ChromaticAberration>().intensity.Override(0.18f);
        EditorUtility.SetDirty(p);
        return p;
    }

    static void AddVolume(VolumeProfile profile)
    {
        var go = new GameObject("PostProcessVolume");
        var v = go.AddComponent<Volume>();
        v.isGlobal = true;
        v.sharedProfile = profile;
    }

    // ================= 메시·도형 도우미 =================

    static void BeginMeshes(string path)
    {
        AssetDatabase.DeleteAsset(path);
        var holder = new Mesh { name = "MeshHolder" };
        AssetDatabase.CreateAsset(holder, path);
        meshContainer = holder;
        meshCache = new Dictionary<string, Mesh>();
    }

    // 크기에 맞춰 UV를 미터 단위로 만든 상자 메시 (텍스처가 늘어나지 않는다)
    static Mesh BoxMesh(Vector3 s, float tile)
    {
        string key = $"box_{s.x:0.00}_{s.y:0.00}_{s.z:0.00}_{tile:0.0}";
        if (meshCache.TryGetValue(key, out var cached)) return cached;

        var h = s * 0.5f;
        var verts = new List<Vector3>();
        var norms = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();

        void Face(Vector3 n, Vector3 u, Vector3 v, float uLen, float vLen)
        {
            Vector3 c = Vector3.Scale(n, h), du = u * uLen * 0.5f, dv = v * vLen * 0.5f;
            int i = verts.Count;
            verts.Add(c - du - dv); verts.Add(c + du - dv); verts.Add(c + du + dv); verts.Add(c - du + dv);
            for (int k = 0; k < 4; k++) norms.Add(n);
            uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(uLen / tile, 0)); uvs.Add(new Vector2(uLen / tile, vLen / tile)); uvs.Add(new Vector2(0, vLen / tile));
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
        }

        Face(Vector3.right, Vector3.back, Vector3.up, s.z, s.y);
        Face(Vector3.left, Vector3.forward, Vector3.up, s.z, s.y);
        Face(Vector3.up, Vector3.right, Vector3.back, s.x, s.z);
        Face(Vector3.down, Vector3.right, Vector3.forward, s.x, s.z);
        Face(Vector3.forward, Vector3.right, Vector3.up, s.x, s.y);
        Face(Vector3.back, Vector3.left, Vector3.up, s.x, s.y);

        var m = new Mesh { name = key };
        m.SetVertices(verts);
        m.SetNormals(norms);
        m.SetUVs(0, uvs);
        m.SetTriangles(tris, 0);
        m.RecalculateTangents();
        m.RecalculateBounds();
        AssetDatabase.AddObjectToAsset(m, meshContainer);
        meshCache[key] = m;
        return m;
    }

    static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 size, Material mat, float tile = 2.5f, bool collider = true, Vector3? euler = null, bool isStatic = true)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(euler ?? Vector3.zero);
        go.AddComponent<MeshFilter>().sharedMesh = BoxMesh(size, tile);
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        if (collider) go.AddComponent<BoxCollider>().size = size;
        if (isStatic)
        {
            // 큰 벽·건물은 뒤를 가리는 물체(Occluder)로 표시해 안 보이는 것을 그리지 않게 한다
            var flags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic;
            if (Mathf.Max(size.x, size.z) >= 2.5f && size.y >= 2.5f) flags |= StaticEditorFlags.OccluderStatic;
            GameObjectUtility.SetStaticEditorFlags(go, flags);
        }
        return go;
    }

    static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, Vector3? euler = null, bool collider = false)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.transform.localRotation = Quaternion.Euler(euler ?? Vector3.zero);
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }

    static Transform Group(string name, Transform parent, Vector3 pos, float yaw = 0)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.localPosition = pos;
        t.localRotation = Quaternion.Euler(0, yaw, 0);
        return t;
    }

    static Light PointLight(string name, Transform parent, Vector3 pos, Color color, float intensity, float range, bool shadows = false)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.intensity = intensity;
        l.range = range;
        l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
        return l;
    }

    static FlickerLight Flicker(Light l, float amount, float speed, bool outages)
    {
        var f = l.gameObject.AddComponent<FlickerLight>();
        f.baseIntensity = l.intensity;
        f.flickerAmount = amount;
        f.speed = speed;
        f.outages = outages;
        return f;
    }

    static float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
    static bool Chance(double p) => rng.NextDouble() < p;
    static T Pick<T>(params T[] items) => items[rng.Next(items.Length)];

    static void Spawn<T>(Transform parent, Vector3 worldPos) where T : Component
    {
        var go = new GameObject(typeof(T).Name);
        go.transform.SetParent(parent, true);
        go.transform.position = worldPos;
        go.AddComponent<T>();
    }

    // ---------- 벽 (문·창문 구멍 포함) ----------

    struct Op
    {
        public float c, w, bottom, top;
        public Op(float c, float w, float bottom, float top) { this.c = c; this.w = w; this.bottom = bottom; this.top = top; }
    }

    static Op DoorOp(float c, float w = 1.4f, float h = 2.3f) => new Op(c, w, 0, h);
    static Op WinOp(float c, float w = 1.3f) => new Op(c, w, 1.0f, 2.2f);
    static readonly Op[] Solid = new Op[0];

    // 로컬 X 방향으로 length 만큼 뻗은 벽. 구멍(op.c는 벽 시작점 기준)을 피해 조각으로 만든다
    static Transform Wall(Transform parent, string name, Vector3 start, float yaw, float length, float height, float thick, Material mat, Op[] ops)
    {
        var w = Group(name, parent, start, yaw);
        var sorted = new List<Op>(ops);
        sorted.Sort((a, b) => a.c.CompareTo(b.c));
        float x = 0;
        foreach (var o in sorted)
        {
            float o0 = o.c - o.w / 2, o1 = o.c + o.w / 2;
            if (o0 > x + 0.01f) Box("Seg", w, new Vector3((x + o0) / 2, height / 2, 0), new Vector3(o0 - x, height, thick), mat);
            if (o.bottom > 0.01f) Box("Below", w, new Vector3(o.c, o.bottom / 2, 0), new Vector3(o.w, o.bottom, thick), mat);
            if (o.top < height - 0.01f) Box("Above", w, new Vector3(o.c, (o.top + height) / 2, 0), new Vector3(o.w, height - o.top, thick), mat);
            x = o1;
        }
        if (length > x + 0.01f) Box("Seg", w, new Vector3((x + length) / 2, height / 2, 0), new Vector3(length - x, height, thick), mat);
        return w;
    }

    static Op[] Shift(Op[] ops, float delta)
    {
        var r = new Op[ops.Length];
        for (int i = 0; i < ops.Length; i++) r[i] = new Op(ops[i].c + delta, ops[i].w, ops[i].bottom, ops[i].top);
        return r;
    }

    // 직사각형 방의 네 벽. 구멍 위치는 각 변의 시작(x0 또는 z0) 기준. null이면 그 벽은 만들지 않는다.
    // 반환: [남, 북, 서, 동] 벽 Transform
    static Transform[] RoomWalls(Transform parent, float x0, float z0, float w, float d, float h, float t, Material mat, Op[] south, Op[] north, Op[] west, Op[] east)
    {
        var r = new Transform[4];
        if (south != null) r[0] = Wall(parent, "WallS", new Vector3(x0 - t / 2, 0, z0), 0, w + t, h, t, mat, Shift(south, t / 2));
        if (north != null) r[1] = Wall(parent, "WallN", new Vector3(x0 - t / 2, 0, z0 + d), 0, w + t, h, t, mat, Shift(north, t / 2));
        if (west != null) r[2] = Wall(parent, "WallW", new Vector3(x0, 0, z0 + t / 2), -90, d - t, h, t, mat, Shift(west, -t / 2));
        if (east != null) r[3] = Wall(parent, "WallE", new Vector3(x0 + w, 0, z0 + t / 2), -90, d - t, h, t, mat, Shift(east, -t / 2));
        return r;
    }

    static float ShiftFor(int side, float t) => side < 2 ? t / 2 : -t / 2;

    // 경첩 문 (wall: 벽 Transform, c: 벽 로컬 기준 구멍 중심)
    static void AddDoor(Transform wall, float c, float width, float height, Material mat)
    {
        var pivot = Group("Door", wall, new Vector3(c - width / 2 + 0.03f, 0, 0));
        pivot.gameObject.AddComponent<Door>();
        pivot.gameObject.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
        var leaf = Box("Leaf", pivot, new Vector3(width / 2 - 0.03f, height / 2, 0), new Vector3(width - 0.08f, height - 0.04f, 0.06f), mat, 1.2f, true, null, false);
        Box("Handle", leaf.transform, new Vector3(width / 2 - 0.2f, -0.1f, 0.06f), new Vector3(0.12f, 0.03f, 0.04f), M["metal"], 1f, false, null, false);
        var obstacle = leaf.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.size = new Vector3(width - 0.08f, height, 0.3f);
        obstacle.carving = true;
    }

    static void CombineInto(Transform parent, string name, List<CombineInstance> parts, Material mat)
    {
        if (parts.Count == 0) return;
        var mesh = new Mesh { name = name + "_" + meshCache.Count, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.CombineMeshes(parts.ToArray(), true, true);
        mesh.RecalculateBounds();
        AssetDatabase.AddObjectToAsset(mesh, meshContainer);
        meshCache[mesh.name] = mesh;
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
    }

    // ================= 프리팹 =================

    static GameObject BuildPlayerPrefab()
    {
        var root = new GameObject("Player");
        root.tag = "Player";
        var cc = root.AddComponent<CharacterController>();
        cc.height = 1.8f;
        cc.radius = 0.35f;
        cc.center = new Vector3(0, 0.9f, 0);
        cc.stepOffset = 0.4f;
        cc.slopeLimit = 50;
        cc.skinWidth = 0.04f;

        var camRoot = Group("CameraRoot", root.transform, new Vector3(0, 1.65f, 0));
        var cam = camRoot.gameObject.AddComponent<Camera>();
        camRoot.gameObject.tag = "MainCamera";
        cam.nearClipPlane = 0.03f;
        cam.farClipPlane = 100f; // 안개 너머는 그리지 않는다 (최적화)
        cam.fieldOfView = 70f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        var camData = cam.GetUniversalAdditionalCameraData();
        camData.renderPostProcessing = true;
        camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        camRoot.gameObject.AddComponent<AudioListener>();

        var flash = Group("Flashlight", camRoot, new Vector3(0.22f, -0.2f, 0.1f));
        flash.localRotation = Quaternion.Euler(1.5f, -2f, 0);
        var spot = flash.gameObject.AddComponent<Light>();
        spot.type = LightType.Spot;
        spot.range = 32f;
        spot.spotAngle = 60f;
        spot.innerSpotAngle = 22f;
        spot.color = new Color(1f, 0.93f, 0.8f);
        spot.intensity = 40f;
        spot.shadows = LightShadows.Soft;
        spot.shadowStrength = 0.95f;

        root.AddComponent<PlayerController>().cameraRoot = camRoot;
        root.AddComponent<PlayerInteractor>().cam = cam;
        var fl = root.AddComponent<PlayerFlashlight>();
        fl.spot = spot;
        fl.baseIntensity = 40f;
        var combat = root.AddComponent<PlayerCombat>();
        combat.cam = cam;
        root.AddComponent<PlayerHotbar>();
        root.AddComponent<PlayerHands>();
        root.AddComponent<NavMeshModifier>().ignoreFromBuild = true;

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabDir + "/Player.prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }

    static GameObject BuildZombiePrefab()
    {
        var root = new GameObject("Zombie");
        var agent = root.AddComponent<NavMeshAgent>();
        agent.radius = 0.35f;
        agent.height = 1.8f;
        agent.speed = 0.7f;
        agent.angularSpeed = 240f;
        agent.acceleration = 10f;
        agent.stoppingDistance = 1.1f;
        var col = root.AddComponent<CapsuleCollider>();
        col.height = 1.8f;
        col.radius = 0.33f;
        col.center = new Vector3(0, 0.9f, 0);
        var z = root.AddComponent<Zombie>();

        var t = root.transform;
        z.legL = Group("LegL", t, new Vector3(-0.12f, 0.92f, 0));
        Prim(PrimitiveType.Cube, "Leg", z.legL, new Vector3(0, -0.46f, 0), new Vector3(0.16f, 0.92f, 0.18f), M["zPants"]);
        z.legR = Group("LegR", t, new Vector3(0.12f, 0.92f, 0));
        Prim(PrimitiveType.Cube, "Leg", z.legR, new Vector3(0, -0.46f, 0), new Vector3(0.16f, 0.92f, 0.18f), M["zPants"]);

        z.body = Group("Body", t, new Vector3(0, 0.92f, 0));
        Prim(PrimitiveType.Cube, "Torso", z.body, new Vector3(0, 0.33f, 0), new Vector3(0.46f, 0.64f, 0.26f), M["zCloth"]);
        Prim(PrimitiveType.Cube, "Rag", z.body, new Vector3(0.08f, 0.02f, 0.12f), new Vector3(0.22f, 0.18f, 0.03f), M["zCloth"], new Vector3(0, 0, 18));
        Prim(PrimitiveType.Cube, "Neck", z.body, new Vector3(0, 0.68f, 0), new Vector3(0.12f, 0.1f, 0.12f), M["zSkin"]);

        z.head = Group("Head", z.body, new Vector3(0, 0.72f, 0));
        Prim(PrimitiveType.Sphere, "Skull", z.head, new Vector3(0, 0.13f, 0.02f), new Vector3(0.23f, 0.28f, 0.25f), M["zSkin"]);
        Prim(PrimitiveType.Cube, "Jaw", z.head, new Vector3(0, 0.0f, 0.07f), new Vector3(0.14f, 0.06f, 0.11f), M["zSkin"], new Vector3(18, 0, 0));
        Prim(PrimitiveType.Sphere, "EyeL", z.head, new Vector3(-0.05f, 0.16f, 0.12f), new Vector3(0.035f, 0.025f, 0.02f), M["zEye"]);
        Prim(PrimitiveType.Sphere, "EyeR", z.head, new Vector3(0.05f, 0.16f, 0.12f), new Vector3(0.035f, 0.025f, 0.02f), M["zEye"]);

        z.armL = Group("ArmL", z.body, new Vector3(-0.3f, 0.6f, 0));
        Prim(PrimitiveType.Cube, "Arm", z.armL, new Vector3(0, -0.3f, 0), new Vector3(0.12f, 0.62f, 0.12f), M["zSkin"]);
        Prim(PrimitiveType.Cube, "Hand", z.armL, new Vector3(0, -0.66f, 0), new Vector3(0.1f, 0.13f, 0.06f), M["zSkin"]);
        z.armR = Group("ArmR", z.body, new Vector3(0.3f, 0.6f, 0));
        Prim(PrimitiveType.Cube, "Arm", z.armR, new Vector3(0, -0.3f, 0), new Vector3(0.12f, 0.62f, 0.12f), M["zSkin"]);
        Prim(PrimitiveType.Cube, "Hand", z.armR, new Vector3(0, -0.66f, 0), new Vector3(0.1f, 0.13f, 0.06f), M["zSkin"]);

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabDir + "/Zombie.prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }

    static GameObject BuildDogPrefab()
    {
        var root = new GameObject("Dog");
        var agent = root.AddComponent<NavMeshAgent>();
        agent.radius = 0.3f;
        agent.height = 0.8f;
        agent.speed = 1.2f;
        agent.angularSpeed = 400f;
        agent.acceleration = 14f;
        var col = root.AddComponent<CapsuleCollider>();
        col.direction = 2;
        col.height = 1.0f;
        col.radius = 0.25f;
        col.center = new Vector3(0, 0.5f, 0);
        var a = root.AddComponent<Animal>();

        var t = root.transform;
        Prim(PrimitiveType.Cube, "Body", t, new Vector3(0, 0.55f, 0), new Vector3(0.3f, 0.3f, 0.8f), M["fur"]);
        Prim(PrimitiveType.Cube, "Head", t, new Vector3(0, 0.72f, 0.5f), new Vector3(0.22f, 0.22f, 0.28f), M["fur"]);
        Prim(PrimitiveType.Cube, "Snout", t, new Vector3(0, 0.66f, 0.7f), new Vector3(0.12f, 0.1f, 0.16f), M["fur"]);
        Prim(PrimitiveType.Cube, "EarL", t, new Vector3(-0.07f, 0.88f, 0.45f), new Vector3(0.05f, 0.1f, 0.04f), M["fur"], new Vector3(0, 0, -12));
        Prim(PrimitiveType.Cube, "EarR", t, new Vector3(0.07f, 0.88f, 0.45f), new Vector3(0.05f, 0.1f, 0.04f), M["fur"], new Vector3(0, 0, 12));
        Prim(PrimitiveType.Cube, "Tail", t, new Vector3(0, 0.66f, -0.5f), new Vector3(0.05f, 0.05f, 0.3f), M["fur"], new Vector3(-30, 0, 0));
        a.legs = new Transform[4];
        var legPos = new[] { new Vector3(-0.1f, 0.42f, 0.3f), new Vector3(0.1f, 0.42f, 0.3f), new Vector3(-0.1f, 0.42f, -0.3f), new Vector3(0.1f, 0.42f, -0.3f) };
        for (int i = 0; i < 4; i++)
        {
            a.legs[i] = Group("Leg" + i, t, legPos[i]);
            Prim(PrimitiveType.Cube, "Leg", a.legs[i], new Vector3(0, -0.21f, 0), new Vector3(0.08f, 0.42f, 0.08f), M["fur"]);
        }

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabDir + "/Dog.prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }

    static GameObject PlacePlayer(GameObject prefab, Vector3 pos, float yaw, Color background)
    {
        var p = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        p.transform.position = pos;
        p.transform.rotation = Quaternion.Euler(0, yaw, 0);
        var cam = p.GetComponentInChildren<Camera>();
        cam.backgroundColor = background;
        return p;
    }

    // ================= 방공호 =================

    static void BuildShelterScene(GameObject playerPrefab)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        BeginMeshes(GenDir + "/ShelterMeshes.asset");
        rng = new System.Random(7);
        var root = new GameObject("Shelter").transform;
        const float t = 0.3f;

        // 바닥·천장
        Box("Floor", root, new Vector3(0, -0.1f, 0), new Vector3(10.3f, 0.2f, 8.3f), M["floor"], 2.5f);
        Box("Ceiling", root, new Vector3(0, 3.1f, 0), new Vector3(10.3f, 0.2f, 8.3f), M["dirty"], 2.5f);
        Box("GenFloor", root, new Vector3(7.65f, -0.1f, 0), new Vector3(5f, 0.2f, 5.3f), M["floor"], 2.5f);
        Box("GenCeiling", root, new Vector3(7.65f, 3.1f, 0), new Vector3(5f, 0.2f, 5.3f), M["dirty"], 2.5f);
        Box("CorridorFloor", root, new Vector3(0, -0.1f, 8.15f), new Vector3(2.3f, 0.2f, 8f), M["floor"], 2.5f);
        Box("CorridorCeiling", root, new Vector3(0, 2.7f, 8.15f), new Vector3(2.3f, 0.2f, 8f), M["dirty"], 2.5f);

        // 벽: 거실(가운데), 발전기실(동쪽), 복도(북쪽)
        RoomWalls(root, -5, -4, 10, 8, 3, t, M["wall"], Solid, new[] { DoorOp(5, 2.0f, 2.5f) }, Solid, new[] { DoorOp(4, 1.3f, 2.2f) });
        RoomWalls(root, 5, -2.5f, 5, 5, 3, t, M["wall"], Solid, Solid, null, Solid);
        RoomWalls(root, -1, 4, 2, 8, 2.6f, t, M["cracked"], null, Solid, Solid, Solid);

        // 천장 배관
        Prim(PrimitiveType.Cylinder, "Pipe1", root, new Vector3(0, 2.82f, -3.6f), new Vector3(0.12f, 5.1f, 0.12f), M["metal"], new Vector3(0, 0, 90));
        Prim(PrimitiveType.Cylinder, "Pipe2", root, new Vector3(0, 2.9f, -3.3f), new Vector3(0.07f, 5.1f, 0.07f), M["metal"], new Vector3(0, 0, 90));
        Prim(PrimitiveType.Cylinder, "Pipe3", root, new Vector3(0.75f, 2.45f, 8f), new Vector3(0.08f, 4f, 0.08f), M["metal"], new Vector3(90, 0, 0));
        Prim(PrimitiveType.Cylinder, "Pipe4", root, new Vector3(4.8f, 2.85f, 0), new Vector3(0.06f, 3.9f, 0.06f), M["metal"], new Vector3(90, 0, 0));

        // 조명
        var lights = new List<Light>();
        lights.Add(Bulb(root, new Vector3(-2.5f, 2.75f, 0.5f), 14f, 10f, true));
        lights.Add(Bulb(root, new Vector3(2.8f, 2.75f, 2.8f), 9f, 8f, false));
        lights.Add(Bulb(root, new Vector3(0, 2.4f, 8.2f), 5f, 7f, false));
        lights.Add(Bulb(root, new Vector3(7.5f, 2.75f, 0), 8f, 8f, false));
        lights.Add(Bulb(root, new Vector3(2.2f, 2.75f, -2.4f), 9f, 9f, false));
        var emergency = PointLight("EmergencyLight", root, new Vector3(0, 2.85f, -3.7f), new Color(1f, 0.12f, 0.08f), 10f, 14f);
        Box("EmergencyLamp", root, new Vector3(0, 2.95f, -3.82f), new Vector3(0.25f, 0.1f, 0.12f), M["redLamp"], 1f, false);
        var shelterLights = root.gameObject.AddComponent<ShelterLights>();
        shelterLights.lights = lights.ToArray();
        shelterLights.emergencyLights = new[] { emergency };

        BuildBed(root, new Vector3(-4.3f, 0, 2.3f));
        BuildWorkbench(root, new Vector3(3.2f, 0, 3.45f));
        BuildStorage(root, new Vector3(-3.4f, 0, -3.55f));
        BuildShelf(root, new Vector3(-1.2f, 0, -3.62f), 0, true);
        BuildShelf(root, new Vector3(3.6f, 0, -3.62f), 0, true);
        BuildTable(root, new Vector3(0.6f, 0, -0.4f));
        BuildGenerator(root, new Vector3(8.9f, 0, 0));

        // 물통
        Prim(PrimitiveType.Cylinder, "WaterBarrel1", root, new Vector3(-4.4f, 0.45f, -1.3f), new Vector3(0.55f, 0.45f, 0.55f), M["paintBlue"], null, true);
        Prim(PrimitiveType.Cylinder, "WaterBarrel2", root, new Vector3(-4.35f, 0.45f, -0.6f), new Vector3(0.55f, 0.45f, 0.55f), M["paintBlue"], null, true);
        Box("Crate", root, new Vector3(4.2f, 0.3f, -1.6f), new Vector3(0.8f, 0.6f, 0.6f), M["wood"], 1f, true, new Vector3(0, 12, 0));
        Box("Crate2", root, new Vector3(4.3f, 0.82f, -1.6f), new Vector3(0.55f, 0.44f, 0.5f), M["wood"], 1f, true, new Vector3(0, -8, 0));

        // 출입구 (복도 끝 철문)
        var exit = Group("Exit", root, new Vector3(0, 0, 11.82f));
        var door = Box("SteelDoor", exit, new Vector3(0, 1.1f, 0), new Vector3(1.2f, 2.2f, 0.12f), M["metal"], 1.2f);
        door.AddComponent<ShelterExit>();
        Prim(PrimitiveType.Cylinder, "Wheel", exit, new Vector3(0, 1.15f, -0.1f), new Vector3(0.45f, 0.02f, 0.45f), M["metal"], new Vector3(90, 0, 0));
        Box("FrameL", exit, new Vector3(-0.68f, 1.15f, -0.02f), new Vector3(0.14f, 2.3f, 0.18f), M["metal"], 1f, false);
        Box("FrameR", exit, new Vector3(0.68f, 1.15f, -0.02f), new Vector3(0.14f, 2.3f, 0.18f), M["metal"], 1f, false);
        Box("FrameT", exit, new Vector3(0, 2.3f, -0.02f), new Vector3(1.5f, 0.14f, 0.18f), M["metal"], 1f, false);
        Box("ExitLamp", exit, new Vector3(0, 2.45f, -0.08f), new Vector3(0.2f, 0.1f, 0.1f), M["redLamp"], 1f, false);
        PointLight("ExitLight", exit, new Vector3(0, 2.4f, -0.4f), new Color(1f, 0.15f, 0.1f), 3f, 5f);

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.12f, 0.105f, 0.09f);
        RenderSettings.skybox = null;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.018f;
        RenderSettings.fogColor = new Color(0.05f, 0.045f, 0.04f);
        RenderSettings.reflectionIntensity = 0.3f;

        AddVolume(MakeProfile("ShelterProfile", 0.85f, -20f, new Color(1f, 0.95f, 0.88f), 0.35f, 14f));
        PlacePlayer(playerPrefab, new Vector3(0, 0.05f, -2f), 0, RenderSettings.fogColor);

        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(scene, SceneDir + "/Shelter.unity");
    }

    static Light Bulb(Transform parent, Vector3 pos, float intensity, float range, bool shadows)
    {
        var g = Group("Bulb", parent, pos);
        Prim(PrimitiveType.Sphere, "Glass", g, Vector3.zero, Vector3.one * 0.11f, M["bulb"]).GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        Prim(PrimitiveType.Cylinder, "Cord", g, new Vector3(0, 0.15f, 0), new Vector3(0.01f, 0.13f, 0.01f), M["tire"]);
        Prim(PrimitiveType.Cylinder, "Socket", g, new Vector3(0, 0.07f, 0), new Vector3(0.04f, 0.03f, 0.04f), M["metal"]);
        return PointLight("Light", g, new Vector3(0, -0.08f, 0), new Color(1f, 0.72f, 0.45f), intensity, range, shadows);
    }

    static void BuildBed(Transform parent, Vector3 pos)
    {
        var g = Group("Bed", parent, pos);
        Box("Frame", g, new Vector3(0, 0.25f, 0), new Vector3(0.95f, 0.08f, 2f), M["metal"], 1f, false);
        foreach (var x in new[] { -0.44f, 0.44f })
            foreach (var z in new[] { -0.96f, 0.96f })
                Box("Leg", g, new Vector3(x, 0.12f, z), new Vector3(0.05f, 0.25f, 0.05f), M["metal"], 1f, false);
        Box("HeadBoard", g, new Vector3(0, 0.5f, 0.98f), new Vector3(0.95f, 0.5f, 0.04f), M["metal"], 1f, false);
        Box("Mattress", g, new Vector3(0, 0.37f, 0), new Vector3(0.9f, 0.16f, 1.92f), M["cloth"], 1f, false);
        Box("Pillow", g, new Vector3(0, 0.5f, 0.72f), new Vector3(0.6f, 0.1f, 0.34f), M["cloth"], 1f, false, new Vector3(-6, 0, 0));
        Box("Blanket", g, new Vector3(0.02f, 0.47f, -0.35f), new Vector3(0.94f, 0.05f, 1.15f), M["blanket"], 1f, false, new Vector3(0, 0, -2));
        var col = g.gameObject.AddComponent<BoxCollider>();
        col.center = new Vector3(0, 0.35f, 0);
        col.size = new Vector3(1f, 0.7f, 2.05f);
        g.gameObject.AddComponent<Bed>();
    }

    static void BuildWorkbench(Transform parent, Vector3 pos)
    {
        var g = Group("Workbench", parent, pos);
        Box("Top", g, new Vector3(0, 0.9f, 0), new Vector3(2.2f, 0.08f, 0.8f), M["wood"], 1.2f, false);
        foreach (var x in new[] { -1.02f, 1.02f })
            foreach (var z in new[] { -0.34f, 0.34f })
                Box("Leg", g, new Vector3(x, 0.43f, z), new Vector3(0.06f, 0.86f, 0.06f), M["metal"], 1f, false);
        Box("Shelf", g, new Vector3(0, 0.25f, 0), new Vector3(2.1f, 0.04f, 0.7f), M["wood"], 1.2f, false);
        Box("Pegboard", g, new Vector3(0, 1.65f, 0.36f), new Vector3(2.2f, 1.1f, 0.03f), M["wood"], 1.2f, false);
        Box("Vise", g, new Vector3(0.85f, 1.0f, -0.2f), new Vector3(0.18f, 0.12f, 0.22f), M["metal"], 1f, false);
        Prim(PrimitiveType.Cylinder, "HammerHandle", g, new Vector3(-0.4f, 0.96f, -0.1f), new Vector3(0.03f, 0.15f, 0.03f), M["wood"], new Vector3(90, 30, 0));
        Box("HammerHead", g, new Vector3(-0.33f, 0.96f, 0.03f), new Vector3(0.12f, 0.05f, 0.05f), M["metal"], 1f, false, new Vector3(0, 30, 0));
        Box("Wrench", g, new Vector3(0.2f, 1.7f, 0.33f), new Vector3(0.04f, 0.3f, 0.02f), M["metal"], 1f, false, new Vector3(0, 0, 10));
        Box("Saw", g, new Vector3(-0.5f, 1.75f, 0.33f), new Vector3(0.5f, 0.12f, 0.01f), M["metal"], 1f, false);
        Box("ToolBox", g, new Vector3(0.1f, 0.38f, 0), new Vector3(0.55f, 0.22f, 0.3f), M["paintRed"], 1f, false);
        Box("Scrap", g, new Vector3(-0.7f, 0.96f, 0.1f), new Vector3(0.3f, 0.02f, 0.25f), M["metal"], 1f, false, new Vector3(0, 20, 0));
        var col = g.gameObject.AddComponent<BoxCollider>();
        col.center = new Vector3(0, 0.5f, 0);
        col.size = new Vector3(2.2f, 1f, 0.8f);
        g.gameObject.AddComponent<Workbench>();
    }

    static void BuildStorage(Transform parent, Vector3 pos)
    {
        var g = Group("Storage", parent, pos);
        foreach (var x in new[] { -0.42f, 0.42f })
        {
            Box("Locker", g, new Vector3(x, 0.95f, 0), new Vector3(0.8f, 1.9f, 0.5f), M["metal"], 1.2f, false);
            Box("Seam", g, new Vector3(x, 0.95f, 0.255f), new Vector3(0.02f, 1.8f, 0.01f), M["tire"], 1f, false);
            for (int i = 0; i < 4; i++)
                Box("Vent", g, new Vector3(x - 0.2f, 1.6f - i * 0.06f, 0.255f), new Vector3(0.22f, 0.02f, 0.01f), M["tire"], 1f, false);
            Box("Handle", g, new Vector3(x + 0.06f, 1.0f, 0.27f), new Vector3(0.03f, 0.15f, 0.03f), M["metal"], 1f, false);
        }
        var col = g.gameObject.AddComponent<BoxCollider>();
        col.center = new Vector3(0, 0.95f, 0);
        col.size = new Vector3(1.7f, 1.9f, 0.55f);
        g.gameObject.AddComponent<StorageBox>();
    }

    // 선반 (장식용 깡통·물병 포함). 도시 건물에서도 쓴다
    static Transform BuildShelf(Transform parent, Vector3 pos, float yaw, bool stocked)
    {
        var g = Group("Shelf", parent, pos, yaw);
        Box("SideL", g, new Vector3(-0.78f, 0.9f, 0), new Vector3(0.04f, 1.8f, 0.45f), M["metal"], 1f, false);
        Box("SideR", g, new Vector3(0.78f, 0.9f, 0), new Vector3(0.04f, 1.8f, 0.45f), M["metal"], 1f, false);
        foreach (var y in new[] { 0.1f, 0.6f, 1.1f, 1.6f })
            Box("Board", g, new Vector3(0, y, 0), new Vector3(1.6f, 0.03f, 0.45f), M["wood"], 1f, false);
        if (stocked)
            foreach (var y in new[] { 0.6f, 1.1f, 1.6f })
                for (int i = 0; i < 5; i++)
                {
                    if (Chance(0.35)) continue;
                    float x = -0.6f + i * 0.3f + R(-0.05f, 0.05f);
                    if (Chance(0.5)) Prim(PrimitiveType.Cylinder, "Can", g, new Vector3(x, y + 0.08f, R(-0.1f, 0.1f)), new Vector3(0.09f, 0.065f, 0.09f), M["metal"]);
                    else Box("Box", g, new Vector3(x, y + 0.12f, R(-0.1f, 0.1f)), new Vector3(0.22f, 0.2f, 0.18f), M["cloth"], 1f, false, new Vector3(0, R(-20, 20), 0));
                }
        var col = g.gameObject.AddComponent<BoxCollider>();
        col.center = new Vector3(0, 0.9f, 0);
        col.size = new Vector3(1.6f, 1.8f, 0.45f);
        GameObjectUtility.SetStaticEditorFlags(g.gameObject, StaticEditorFlags.BatchingStatic);
        return g;
    }

    static void BuildTable(Transform parent, Vector3 pos)
    {
        var g = Group("Table", parent, pos);
        Box("Top", g, new Vector3(0, 0.76f, 0), new Vector3(1.6f, 0.06f, 0.9f), M["wood"], 1.2f, true);
        foreach (var x in new[] { -0.72f, 0.72f })
            foreach (var z in new[] { -0.38f, 0.38f })
                Box("Leg", g, new Vector3(x, 0.37f, z), new Vector3(0.06f, 0.73f, 0.06f), M["wood"], 1f, true);
        Box("Radio", g, new Vector3(-0.45f, 0.88f, 0.15f), new Vector3(0.34f, 0.18f, 0.16f), M["tire"], 1f, false, new Vector3(0, 10, 0));
        Prim(PrimitiveType.Cylinder, "Antenna", g, new Vector3(-0.35f, 1.1f, 0.18f), new Vector3(0.008f, 0.22f, 0.008f), M["metal"], new Vector3(0, 0, -20));
        Box("Map", g, new Vector3(0.25f, 0.795f, -0.05f), new Vector3(0.6f, 0.005f, 0.45f), M["cloth"], 1f, false, new Vector3(0, -8, 0));
        Prim(PrimitiveType.Cylinder, "Mug", g, new Vector3(0.6f, 0.84f, 0.25f), new Vector3(0.08f, 0.05f, 0.08f), M["metal"]);
        var chair = Group("Chair", g, new Vector3(0.2f, 0, -0.85f), 10);
        Box("Seat", chair, new Vector3(0, 0.45f, 0), new Vector3(0.45f, 0.05f, 0.45f), M["wood"], 1f, true);
        Box("Back", chair, new Vector3(0, 0.75f, -0.21f), new Vector3(0.45f, 0.55f, 0.04f), M["wood"], 1f, false);
        foreach (var x in new[] { -0.2f, 0.2f })
            foreach (var z in new[] { -0.2f, 0.2f })
                Box("Leg", chair, new Vector3(x, 0.22f, z), new Vector3(0.04f, 0.45f, 0.04f), M["wood"], 1f, false);
    }

    static void BuildGenerator(Transform parent, Vector3 pos)
    {
        var g = Group("Generator", parent, pos, -90);
        Box("Base", g, new Vector3(0, 0.06f, 0), new Vector3(1.5f, 0.12f, 0.9f), M["metal"], 1f, false);
        Box("Body", g, new Vector3(-0.15f, 0.55f, 0), new Vector3(1.0f, 0.8f, 0.8f), M["metal"], 1f, false);
        Prim(PrimitiveType.Cylinder, "Engine", g, new Vector3(0.5f, 0.5f, 0), new Vector3(0.45f, 0.3f, 0.45f), M["metal"], new Vector3(0, 0, 90));
        Box("Tank", g, new Vector3(-0.15f, 1.08f, 0), new Vector3(0.8f, 0.25f, 0.55f), M["paintRed"], 1f, false);
        Box("Panel", g, new Vector3(-0.15f, 0.6f, 0.41f), new Vector3(0.5f, 0.35f, 0.03f), M["tire"], 1f, false);
        Box("Gauge", g, new Vector3(-0.25f, 0.68f, 0.43f), new Vector3(0.08f, 0.08f, 0.01f), M["bulb"], 1f, false);
        Prim(PrimitiveType.Cylinder, "Exhaust", g, new Vector3(0.45f, 1.85f, -0.25f), new Vector3(0.08f, 1.15f, 0.08f), M["metal"]);
        var col = g.gameObject.AddComponent<BoxCollider>();
        col.center = new Vector3(0, 0.6f, 0);
        col.size = new Vector3(1.5f, 1.2f, 0.9f);
        var gen = g.gameObject.AddComponent<Generator>();
        var hum = g.gameObject.AddComponent<AudioSource>();
        hum.loop = true;
        hum.playOnAwake = false;
        hum.spatialBlend = 1;
        hum.minDistance = 1.5f;
        hum.maxDistance = 16f;
        hum.rolloffMode = AudioRolloffMode.Linear;
        hum.volume = 0.3f;
        gen.hum = hum;
    }

    // ================= 메인 메뉴 배경 =================

    static void BuildMenuScene(GameObject zombiePrefab)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        BeginMeshes(GenDir + "/MenuMeshes.asset");
        rng = new System.Random(5);
        var root = new GameObject("MenuBackdrop").transform;

        Box("Ground", root, new Vector3(0, -0.1f, 25), new Vector3(70, 0.2f, 100), M["asphalt"], 5f);
        Box("SidewalkL", root, new Vector3(-10, 0.075f, 25), new Vector3(6, 0.15f, 100), M["floor"], 3f);
        Box("SidewalkR", root, new Vector3(10, 0.075f, 25), new Vector3(6, 0.15f, 100), M["floor"], 3f);
        var lines = new List<CombineInstance>();
        var dash = BoxMesh(new Vector3(0.15f, 0.02f, 2.4f), 1f);
        for (float z = -5; z < 70; z += 6)
            lines.Add(new CombineInstance { mesh = dash, transform = Matrix4x4.TRS(new Vector3(0, 0.005f, z), Quaternion.identity, Vector3.one) });
        CombineInto(root, "RoadLines", lines, M["line"]);

        // 길 양옆 건물
        foreach (var side in new[] { -1f, 1f })
            for (float z = -12; z < 75;)
            {
                float d = R(8f, 13f), w = R(8f, 12f), h = R(9f, 24f);
                float x0 = side < 0 ? -13 - w : 13;
                var g = Group("Building", root, new Vector3(0, 0.15f, 0));
                Box("Mass", g, new Vector3(x0 + w / 2, h / 2, z + d / 2), new Vector3(w, h, d), Pick(M["wall"], M["brick"], M["cracked"]), 3f);
                FacadeWindows(g, x0, z, w, d, 3.5f, Mathf.FloorToInt((h - 3.5f) / 3.2f));
                Box("Shutter", g, new Vector3(side < 0 ? x0 + w + 0.06f : x0 - 0.06f, 1.4f, z + d / 2), new Vector3(0.1f, 2.6f, Mathf.Min(5, d - 2)), M["metal"], 1.5f);
                z += d + R(0.5f, 3f);
            }

        WreckedCar(root, new Vector3(-3f, 0, 13), 18, false);
        WreckedCar(root, new Vector3(3.5f, 0, 26), -70, true);
        WreckedCar(root, new Vector3(-2f, 0, 42), 160, false);
        RubblePile(root, new Vector3(4.5f, 0, 9), 10, 1.4f);
        RubblePile(root, new Vector3(-5.5f, 0, 31), 12, 1.6f);
        for (int k = 0; k < 3; k++)
            Box("Barrier", root, new Vector3(5f + k * 2.1f, 0.45f, 18), new Vector3(2f, 0.9f, 0.6f), M["dirty"], 2f, true, new Vector3(0, R(-10, 10), k == 1 ? 80 : 0));
        FireBarrel(root, new Vector3(3.2f, 0, 7.5f));
        LampPost(root, new Vector3(-8.5f, 0.15f, 20), true);
        LampPost(root, new Vector3(8.5f, 0.15f, 38));
        DeadTree(root, new Vector3(9f, 0.15f, 12));
        DeadTree(root, new Vector3(-9f, 0.15f, 34));

        // 안개 속에 서 있는 좀비 실루엣
        PoseZombie(zombiePrefab, new Vector3(1.4f, 0, 30), 190);
        PoseZombie(zombiePrefab, new Vector3(-2.6f, 0, 46), 170);

        var camGo = new GameObject("MenuCamera");
        camGo.tag = "MainCamera";
        camGo.transform.position = new Vector3(-3.2f, 1.7f, -3f);
        camGo.transform.rotation = Quaternion.Euler(3, -6, 0);
        var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 55f;
        cam.farClipPlane = 120f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        var data = cam.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = true;
        data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        camGo.AddComponent<AudioListener>();
        AddAshFall(camGo.transform);
        root.gameObject.AddComponent<MainMenuBackground>().cameraTransform = camGo.transform;

        var moon = new GameObject("Moonlight").AddComponent<Light>();
        moon.type = LightType.Directional;
        moon.color = new Color(0.55f, 0.62f, 0.75f);
        moon.intensity = 0.55f;
        moon.shadows = LightShadows.Soft;
        moon.transform.rotation = Quaternion.Euler(30, -140, 0);
        RenderSettings.sun = moon;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.15f, 0.16f, 0.18f);
        RenderSettings.skybox = null;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.035f;
        RenderSettings.fogColor = new Color(0.11f, 0.12f, 0.12f);
        cam.backgroundColor = RenderSettings.fogColor;
        AddVolume(MakeProfile("MenuProfile", 1.1f, -40f, new Color(0.92f, 1f, 0.95f), 0.5f, 12f));

        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(scene, SceneDir + "/MainMenu.unity");
    }

    static void PoseZombie(GameObject prefab, Vector3 pos, float yaw)
    {
        var z = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        PrefabUtility.UnpackPrefabInstance(z, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        z.transform.position = pos;
        z.transform.rotation = Quaternion.Euler(0, yaw, 0);
        Object.DestroyImmediate(z.GetComponent<Zombie>());
        Object.DestroyImmediate(z.GetComponent<NavMeshAgent>());
        var t = z.transform;
        t.Find("Body").localRotation = Quaternion.Euler(14, 0, 4);
        t.Find("Body/ArmL").localRotation = Quaternion.Euler(-70, 0, -6);
        t.Find("Body/ArmR").localRotation = Quaternion.Euler(-55, 0, 8);
        t.Find("Body/Head").localRotation = Quaternion.Euler(8, 15, 24);
        t.Find("LegL").localRotation = Quaternion.Euler(12, 0, 0);
        t.Find("LegR").localRotation = Quaternion.Euler(-8, 0, 0);
    }

    // ================= 폐허 도시 =================

    static readonly float[] RoadCenters = { -69f, -23f, 23f, 69f };
    static readonly Vector3 SpawnPos = new Vector3(-23f, 0.05f, -22.5f);

    static void BuildCityScene(GameObject playerPrefab, GameObject zombiePrefab, GameObject dogPrefab)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        BeginMeshes(GenDir + "/CityMeshes.asset");
        rng = new System.Random(31);

        var root = new GameObject("City").transform;
        var geo = Group("Geometry", root, Vector3.zero);
        var points = Group("SpawnPoints", root, Vector3.zero);

        for (int gx = 0; gx < 8; gx++)
            for (int gz = 0; gz < 8; gz++)
                Box("Ground", geo, new Vector3(-85 + 10.625f + gx * 21.25f, -0.1f, -85 + 10.625f + gz * 21.25f), new Vector3(21.25f, 0.2f, 21.25f), M["asphalt"], 5f);
        BuildRoadLines(geo);

        foreach (var bx in new[] { -46f, 0f, 46f })
            foreach (var bz in new[] { -46f, 0f, 46f })
                BuildBlock(geo, points, new Vector3(bx, 0, bz));

        BuildStreetProps(geo, points);
        BuildHatch(geo, new Vector3(-23f, 0, -26.5f));
        BuildBoundary(geo);

        // 도로 위 좀비 등장 지점
        for (int i = 0; i < 18; i++)
        {
            var p = RandomRoadPoint();
            if (Vector3.Distance(p, SpawnPos) < 30) { i--; continue; }
            Spawn<ZombieSpawnPoint>(points, p);
        }

        // 하늘·안개·달빛
        var moon = new GameObject("Moonlight").AddComponent<Light>();
        moon.type = LightType.Directional;
        moon.color = new Color(0.55f, 0.62f, 0.75f);
        moon.intensity = 0.9f;
        moon.shadows = LightShadows.Soft;
        moon.shadowStrength = 0.7f;
        moon.transform.rotation = Quaternion.Euler(38, -35, 0);
        RenderSettings.sun = moon;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.27f, 0.29f, 0.31f);
        RenderSettings.skybox = null;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.03f;
        RenderSettings.fogColor = new Color(0.16f, 0.17f, 0.17f);
        RenderSettings.reflectionIntensity = 0.2f;

        AddVolume(MakeProfile("CityProfile", 1.4f, -35f, new Color(0.92f, 1f, 0.95f), 0.4f, 10f));

        var player = PlacePlayer(playerPrefab, SpawnPos, 0, RenderSettings.fogColor);
        AddAshFall(player.transform);

        var director = new GameObject("CityDirector").AddComponent<CityDirector>();
        director.zombiePrefab = zombiePrefab;
        director.dogPrefab = dogPrefab;

        // 내비메시 굽기 (좀비·들개가 다닐 길)
        var surface = geo.gameObject.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Children;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.BuildNavMesh();
        string navPath = GenDir + "/CityNavMesh.asset";
        AssetDatabase.DeleteAsset(navPath);
        AssetDatabase.CreateAsset(surface.navMeshData, navPath);

        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(scene, SceneDir + "/City.unity");

        // 오클루전 컬링: 건물에 가려 안 보이는 것은 그리지 않는다 (저사양 PC 최적화)
        StaticOcclusionCulling.Compute();
        EditorSceneManager.SaveScene(scene, SceneDir + "/City.unity");
    }

    static Vector3 RandomRoadPoint()
    {
        float rc = Pick(RoadCenters) + R(-4f, 4f);
        float along = R(-74f, 74f);
        return Chance(0.5) ? new Vector3(rc, 0, along) : new Vector3(along, 0, rc);
    }

    static void BuildRoadLines(Transform geo)
    {
        var parts = new List<CombineInstance>();
        var dash = BoxMesh(new Vector3(0.15f, 0.02f, 2.4f), 1f);
        foreach (var rc in RoadCenters)
            for (float s = -76; s <= 76; s += 6)
            {
                bool nearCross = false;
                foreach (var other in RoadCenters) if (Mathf.Abs(s - other) < 7) nearCross = true;
                if (nearCross || Chance(0.2)) continue; // 지워진 차선
                parts.Add(new CombineInstance { mesh = dash, transform = Matrix4x4.TRS(new Vector3(rc, 0.005f, s), Quaternion.identity, Vector3.one) });
                parts.Add(new CombineInstance { mesh = dash, transform = Matrix4x4.TRS(new Vector3(s, 0.005f, rc), Quaternion.Euler(0, 90, 0), Vector3.one) });
            }
        CombineInto(geo, "RoadLines", parts, M["line"]);
    }

    static void BuildBlock(Transform geo, Transform points, Vector3 c)
    {
        var b = Group($"Block_{c.x}_{c.z}", geo, c);
        Box("Sidewalk", b, new Vector3(0, 0.075f, 0), new Vector3(34, 0.15f, 34), M["floor"], 3f);

        for (int ix = 0; ix < 2; ix++)
            for (int iz = 0; iz < 2; iz++)
            {
                bool trader = c.x == 0 && c.z == -46 && ix == 0 && iz == 1;
                bool nearSpawn = c.x == -46 && c.z == -46 && ix == 1 && iz == 1;
                double r = rng.NextDouble();
                if (trader || nearSpawn || r < 0.5) EnterableBuilding(b, points, ix, iz, trader);
                else if (r < 0.75) TallBuilding(b, points, ix, iz);
                else if (r < 0.9) CollapsedBuilding(b, points, ix, iz);
                else EmptyLot(b, points, ix, iz);
            }

        // 모퉁이 가로등
        foreach (var x in new[] { -16.3f, 16.3f })
            foreach (var z in new[] { -16.3f, 16.3f })
                if (Chance(0.8)) LampPost(b, new Vector3(x, 0.15f, z));

        // 인도 위 잔해, 말라 죽은 나무
        for (int i = 0; i < 3; i++)
        {
            var side = rng.Next(4);
            float along = R(-15, 15);
            var p = side == 0 ? new Vector3(along, 0.15f, -16f) : side == 1 ? new Vector3(along, 0.15f, 16f) : side == 2 ? new Vector3(-16f, 0.15f, along) : new Vector3(16f, 0.15f, along);
            if (Chance(0.5)) DeadTree(b, p);
            else RubblePile(b, p, 4, 0.8f);
        }
    }

    // 블록 안 13x13m 구역의 바깥 모서리(도로 쪽)에 붙도록 건물 크기와 위치를 정한다
    static void LotRect(int ix, int iz, out float x0, out float z0, out float w, out float d)
    {
        w = R(9.5f, 13f);
        d = R(9.5f, 13f);
        x0 = ix == 0 ? -14.5f : 14.5f - w;
        z0 = iz == 0 ? -14.5f : 14.5f - d;
    }

    static void EnterableBuilding(Transform block, Transform points, int ix, int iz, bool trader)
    {
        LotRect(ix, iz, out float x0, out float z0, out float w, out float d);
        var g = Group("Building", block, new Vector3(0, 0.15f, 0));
        const float h = 3.6f, t = 0.3f;
        var mat = Pick(M["wall"], M["cracked"], M["brick"]);

        // 바깥쪽 두 면: 하나는 문, 하나는 창문. 0=남 1=북 2=서 3=동
        int xSide = ix == 0 ? 2 : 3, zSide = iz == 0 ? 0 : 1;
        int doorSide = Chance(0.5) ? xSide : zSide;
        int winSide = doorSide == xSide ? zSide : xSide;
        float len(int side) => side < 2 ? w : d;
        var ops = new Op[4][];
        for (int s = 0; s < 4; s++) ops[s] = Solid;
        float doorC = R(2f, len(doorSide) - 2f);
        ops[doorSide] = new[] { DoorOp(doorC) };
        ops[winSide] = new[] { WinOp(len(winSide) * 0.3f), WinOp(len(winSide) * 0.7f) };
        int backSide = doorSide == xSide ? (zSide == 0 ? 1 : 0) : (xSide == 2 ? 3 : 2);
        if (Chance(0.5)) ops[backSide] = new[] { WinOp(len(backSide) * 0.5f) };

        var walls = RoomWalls(g, x0, z0, w, d, h, t, mat, ops[0], ops[1], ops[2], ops[3]);
        Box("Floor", g, new Vector3(x0 + w / 2, 0.02f, z0 + d / 2), new Vector3(w - t, 0.04f, d - t), M["floor"], 2.5f);
        Box("Roof", g, new Vector3(x0 + w / 2, h + 0.15f, z0 + d / 2), new Vector3(w + t, 0.3f, d + t), M["dirty"], 3f);

        // 위층 (들어갈 수 없는 겉모습)
        int floors = rng.Next(1, 5);
        float upperH = floors * 3.2f;
        Box("Upper", g, new Vector3(x0 + w / 2, h + 0.3f + upperH / 2, z0 + d / 2), new Vector3(w + t, upperH, d + t), mat, 3f);
        FacadeWindows(g, x0 - t / 2, z0 - t / 2, w + t, d + t, h + 0.3f, floors);

        if (!trader && Chance(0.7)) AddDoor(walls[doorSide], doorC + ShiftFor(doorSide, t), 1.4f, 2.3f, Pick(M["wood"], M["metal"]));

        // 내부: 선반, 탁자/계산대, 상자, 잔해
        Vector3 center = new Vector3(x0 + w / 2, 0, z0 + d / 2);
        Vector3 Inside(float u, float v) => new Vector3(x0 + 0.8f + u * (w - 1.6f), 0.04f, z0 + 0.8f + v * (d - 1.6f));
        float backYaw = backSide == 0 ? 0 : backSide == 1 ? 180 : backSide == 2 ? 90 : -90;
        Vector3 backPos = backSide == 0 ? Inside(0.5f, 0) : backSide == 1 ? Inside(0.5f, 1) : backSide == 2 ? Inside(0, 0.5f) : Inside(1, 0.5f);
        var shelf = BuildShelf(g, backPos, backYaw, true);
        var shelfWorld = shelf.position;
        Spawn<ItemSpawnPoint>(points, shelfWorld + shelf.rotation * new Vector3(-0.4f, 0.65f, 0));
        Spawn<ItemSpawnPoint>(points, shelfWorld + shelf.rotation * new Vector3(0.4f, 1.15f, 0));
        if (Chance(0.6))
        {
            var shelf2 = BuildShelf(g, Vector3.Lerp(backPos, center, 0.45f) + new Vector3(R(-1, 1), 0, R(-1, 1)), backYaw + (Chance(0.3) ? R(-25, 25) : 0), Chance(0.5));
            Spawn<ItemSpawnPoint>(points, shelf2.position + shelf2.rotation * new Vector3(0, 0.15f, 0));
        }

        var tablePos = g.TransformPoint(Inside(R(0.2f, 0.8f), R(0.2f, 0.8f)));
        var tableLocal = g.InverseTransformPoint(tablePos);
        if (Chance(0.6))
        {
            var tg = Group("Counter", g, tableLocal, R(0, 360));
            Box("Top", tg, new Vector3(0, 0.95f, 0), new Vector3(2.2f, 0.06f, 0.7f), M["wood"], 1.2f);
            Box("Body", tg, new Vector3(0, 0.46f, 0), new Vector3(2.1f, 0.92f, 0.6f), M["wood"], 1.2f);
            Spawn<ItemSpawnPoint>(points, tablePos + Vector3.up * 1.0f);
        }
        else
        {
            var tg = Group("TableTipped", g, tableLocal, R(0, 360));
            Box("Top", tg, new Vector3(0, 0.45f, 0), new Vector3(1.4f, 0.05f, 0.8f), M["wood"], 1.2f, true, new Vector3(0, 0, 75));
            Spawn<ItemSpawnPoint>(points, tablePos + new Vector3(0.8f, 0.1f, 0.5f));
        }
        for (int i = 0, n = rng.Next(1, 4); i < n; i++)
        {
            var p = Inside(R(0, 1), R(0, 1));
            Box("Crate", g, p + Vector3.up * 0.3f, new Vector3(0.7f, 0.6f, 0.6f), M["wood"], 1f, true, new Vector3(0, R(0, 90), 0));
            if (Chance(0.5)) Spawn<ItemSpawnPoint>(points, g.TransformPoint(p) + Vector3.up * 0.65f);
        }
        for (int i = 0; i < 4; i++)
            Box("Debris", g, Inside(R(0, 1), R(0, 1)) + Vector3.up * 0.05f, new Vector3(R(0.1f, 0.5f), 0.06f, R(0.1f, 0.4f)), Pick(M["cracked"], M["wood"], M["metal"]), 1f, false, new Vector3(R(-10, 10), R(0, 180), 0));

        if (Chance(0.3) && !trader) Spawn<ZombieSpawnPoint>(points, g.TransformPoint(center));
        if (Chance(0.25) && !trader)
        {
            var l = PointLight("BrokenLight", g, center + Vector3.up * (h - 0.3f), new Color(0.75f, 0.85f, 0.8f), 5f, 8f);
            Flicker(l, 0.4f, 12f, true);
            Box("Tube", g, center + Vector3.up * (h - 0.1f), new Vector3(1.2f, 0.05f, 0.1f), M["bulb"], 1f, false);
        }
        if (trader) BuildTrader(g, Inside(0.5f, 0.5f));
    }

    static void FacadeWindows(Transform g, float x0, float z0, float w, float d, float baseY, int floors)
    {
        var parts = new List<CombineInstance>();
        var win = BoxMesh(new Vector3(1.1f, 1.5f, 0.06f), 1f);
        for (int f = 0; f < floors; f++)
        {
            float y = baseY + f * 3.2f + 1.7f;
            for (int side = 0; side < 4; side++)
            {
                float len = side < 2 ? w : d;
                int cols = Mathf.Max(1, Mathf.FloorToInt(len / 3f));
                for (int i = 0; i < cols; i++)
                {
                    if (Chance(0.08)) continue;
                    float u = (i + 0.5f) * len / cols;
                    Vector3 p; Quaternion q;
                    if (side == 0) { p = new Vector3(x0 + u, y, z0 - 0.02f); q = Quaternion.identity; }
                    else if (side == 1) { p = new Vector3(x0 + u, y, z0 + d + 0.02f); q = Quaternion.identity; }
                    else if (side == 2) { p = new Vector3(x0 - 0.02f, y, z0 + u); q = Quaternion.Euler(0, 90, 0); }
                    else { p = new Vector3(x0 + w + 0.02f, y, z0 + u); q = Quaternion.Euler(0, 90, 0); }
                    parts.Add(new CombineInstance { mesh = win, transform = Matrix4x4.TRS(p, q, Vector3.one) });
                }
            }
        }
        CombineInto(g, "Windows", parts, M["glass"]);
    }

    static void TallBuilding(Transform block, Transform points, int ix, int iz)
    {
        LotRect(ix, iz, out float x0, out float z0, out float w, out float d);
        var g = Group("Tower", block, new Vector3(0, 0.15f, 0));
        float h = R(10f, 24f);
        var mat = Pick(M["wall"], M["brick"], M["cracked"]);
        Box("Mass", g, new Vector3(x0 + w / 2, h / 2, z0 + d / 2), new Vector3(w, h, d), mat, 3f);
        int floors = Mathf.FloorToInt((h - 3.5f) / 3.2f);
        FacadeWindows(g, x0, z0, w, d, 3.5f, floors);
        // 1층 녹슨 셔터
        int xSide = ix == 0 ? 2 : 3, zSide = iz == 0 ? 0 : 1;
        float sz = zSide == 0 ? z0 - 0.06f : z0 + d + 0.06f;
        Box("ShutterZ", g, new Vector3(x0 + w / 2, 1.4f, sz), new Vector3(Mathf.Min(6, w - 2), 2.6f, 0.1f), M["metal"], 1.5f);
        float sx = xSide == 2 ? x0 - 0.06f : x0 + w + 0.06f;
        Box("ShutterX", g, new Vector3(sx, 1.4f, z0 + d / 2), new Vector3(0.1f, 2.6f, Mathf.Min(6, d - 2)), M["metal"], 1.5f);
        RubblePile(g, new Vector3(sx + (xSide == 2 ? -1.2f : 1.2f), 0, z0 + R(1, d - 1)), 6, 1.0f);
        if (Chance(0.5)) Spawn<ItemSpawnPoint>(points, g.TransformPoint(new Vector3(sx + (xSide == 2 ? -0.8f : 0.8f), 0.1f, z0 + d * 0.3f)));
    }

    static void CollapsedBuilding(Transform block, Transform points, int ix, int iz)
    {
        LotRect(ix, iz, out float x0, out float z0, out float w, out float d);
        var g = Group("Ruin", block, new Vector3(0, 0.15f, 0));
        var mat = Pick(M["cracked"], M["brick"]);
        Box("Floor", g, new Vector3(x0 + w / 2, 0.02f, z0 + d / 2), new Vector3(w, 0.04f, d), M["floor"], 2.5f);
        // 무너져 높이가 들쭉날쭉한 벽
        for (int side = 0; side < 4; side++)
        {
            float len = side < 2 ? w : d;
            for (float s = 0; s < len - 0.1f;)
            {
                float seg = Mathf.Min(R(1.0f, 2.2f), len - s);
                float hh = Chance(0.18) ? 0 : R(0.4f, 3.8f);
                if (hh > 0)
                {
                    Vector3 p = side == 0 ? new Vector3(x0 + s + seg / 2, hh / 2, z0) : side == 1 ? new Vector3(x0 + s + seg / 2, hh / 2, z0 + d)
                              : side == 2 ? new Vector3(x0, hh / 2, z0 + s + seg / 2) : new Vector3(x0 + w, hh / 2, z0 + s + seg / 2);
                    var size = side < 2 ? new Vector3(seg, hh, 0.3f) : new Vector3(0.3f, hh, seg);
                    Box("Wall", g, p, size, mat, 2.5f, true, new Vector3(0, 0, R(-2, 2)));
                }
                s += seg;
            }
        }
        Vector3 c = new Vector3(x0 + w / 2, 0, z0 + d / 2);
        RubblePile(g, c + new Vector3(R(-2, 2), 0, R(-2, 2)), 14, 1.6f);
        Box("FallenSlab", g, c + new Vector3(R(-2, 2), 1.0f, R(-2, 2)), new Vector3(w * 0.55f, 0.25f, d * 0.35f), M["dirty"], 3f, true, new Vector3(R(18, 32), R(0, 360), 0));
        Box("Beam", g, c + new Vector3(R(-3, 3), 0.8f, R(-3, 3)), new Vector3(0.25f, 0.25f, 4.5f), M["metal"], 1.5f, true, new Vector3(R(10, 25), R(0, 360), 0));
        for (int i = 0; i < 3; i++)
            Spawn<ItemSpawnPoint>(points, g.TransformPoint(c + new Vector3(R(-w / 2 + 1, w / 2 - 1), 0.5f, R(-d / 2 + 1, d / 2 - 1))));
        if (Chance(0.4)) Spawn<ZombieSpawnPoint>(points, g.TransformPoint(c + new Vector3(2.5f, 0, -2.5f)));
        if (Chance(0.35)) FireBarrel(g, c + new Vector3(R(-3, 3), 0, R(-3, 3)));
    }

    static void EmptyLot(Transform block, Transform points, int ix, int iz)
    {
        LotRect(ix, iz, out float x0, out float z0, out float w, out float d);
        var g = Group("Lot", block, new Vector3(0, 0.15f, 0));
        Vector3 c = new Vector3(x0 + w / 2, 0, z0 + d / 2);
        RubblePile(g, c + new Vector3(-2, 0, 1), 10, 1.3f);
        WreckedCar(g, c + new Vector3(2, 0, -1.5f), R(0, 360), Chance(0.3));
        // 쓰러진 철조망 울타리
        for (int i = 0; i < 6; i++)
            Box("FencePost", g, new Vector3(x0 + 1 + i * (w - 2) / 5f, 0.9f, z0 + 0.5f), new Vector3(0.06f, 1.8f, 0.06f), M["metal"], 1f, true, new Vector3(R(-12, 12), 0, R(-12, 12)));
        Spawn<ItemSpawnPoint>(points, g.TransformPoint(c + new Vector3(-2, 0.4f, 2.5f)));
        Spawn<ZombieSpawnPoint>(points, g.TransformPoint(c));
    }

    static void RubblePile(Transform parent, Vector3 center, int count, float spread)
    {
        var g = Group("Rubble", parent, center);
        for (int i = 0; i < count; i++)
        {
            float s = R(0.25f, 1.1f) * spread;
            var size = new Vector3(s * R(0.6f, 1.4f), s * R(0.3f, 0.7f), s * R(0.6f, 1.4f));
            var p = new Vector3(R(-spread, spread), size.y * R(0.1f, 0.4f), R(-spread, spread));
            Box("Chunk", g, p, size, Pick(M["cracked"], M["dirty"], M["brick"]), 1.5f, true, new Vector3(R(-25, 25), R(0, 360), R(-25, 25)));
        }
        if (Chance(0.4))
            Prim(PrimitiveType.Cylinder, "Rebar", g, new Vector3(0, 0.4f, 0), new Vector3(0.03f, 0.8f, 0.03f), M["metal"], new Vector3(R(20, 60), R(0, 360), 0));
    }

    static void DeadTree(Transform parent, Vector3 pos)
    {
        var g = Group("DeadTree", parent, pos, R(0, 360));
        float lean = R(-8, 8);
        Prim(PrimitiveType.Cylinder, "Trunk", g, new Vector3(0, 1.6f, 0), new Vector3(0.22f, 1.6f, 0.22f), M["wood"], new Vector3(lean, 0, 0), true);
        for (int i = 0; i < 4; i++)
            Prim(PrimitiveType.Cylinder, "Branch", g, new Vector3(R(-0.3f, 0.3f), R(2.2f, 3.1f), R(-0.3f, 0.3f)), new Vector3(0.07f, R(0.5f, 0.9f), 0.07f), M["wood"], new Vector3(R(-60, 60), R(0, 360), R(-60, 60)));
    }

    static void LampPost(Transform parent, Vector3 pos, bool forceLit = false)
    {
        var g = Group("LampPost", parent, pos, R(0, 360));
        bool bent = !forceLit && Chance(0.2);
        var tilt = bent ? new Vector3(R(8, 20), 0, 0) : Vector3.zero;
        var pole = Group("Pole", g, Vector3.zero);
        pole.localRotation = Quaternion.Euler(tilt);
        Prim(PrimitiveType.Cylinder, "Pole", pole, new Vector3(0, 3f, 0), new Vector3(0.14f, 3f, 0.14f), M["metal"], null, true);
        Box("Arm", pole, new Vector3(0, 5.9f, 0.6f), new Vector3(0.08f, 0.08f, 1.3f), M["metal"], 1f, false);
        Box("Head", pole, new Vector3(0, 5.8f, 1.2f), new Vector3(0.3f, 0.12f, 0.5f), M["metal"], 1f, false);
        if (!bent && (forceLit || Chance(0.3)))
        {
            Box("Glow", pole, new Vector3(0, 5.73f, 1.2f), new Vector3(0.24f, 0.02f, 0.4f), M["lantern"], 1f, false);
            var l = PointLight("StreetLight", pole, new Vector3(0, 5.4f, 1.2f), new Color(1f, 0.6f, 0.3f), 35f, 18f);
            Flicker(l, 0.15f, 20f, true);
        }
    }

    static void FireBarrel(Transform parent, Vector3 pos)
    {
        var g = Group("FireBarrel", parent, pos);
        Prim(PrimitiveType.Cylinder, "Barrel", g, new Vector3(0, 0.45f, 0), new Vector3(0.6f, 0.45f, 0.6f), M["metal"], null, true);
        var l = PointLight("Fire", g, new Vector3(0, 1.3f, 0), new Color(1f, 0.5f, 0.18f), 16f, 14f);
        Flicker(l, 0.35f, 9f, false);

        var psGo = new GameObject("Flames");
        psGo.transform.SetParent(g, false);
        psGo.transform.localPosition = new Vector3(0, 0.85f, 0);
        psGo.transform.localRotation = Quaternion.Euler(-90, 0, 0);
        var ps = psGo.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.0f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
        main.startColor = new Color(1f, 0.6f, 0.25f, 0.8f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 120;
        var emission = ps.emission;
        emission.rateOverTime = 45;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 8;
        shape.radius = 0.22f;
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(new Color(1f, 0.8f, 0.4f), 0), new GradientColorKey(new Color(1f, 0.35f, 0.1f), 0.5f), new GradientColorKey(new Color(0.3f, 0.1f, 0.05f), 1) },
            new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(0.9f, 0.15f), new GradientAlphaKey(0, 1) });
        col.color = grad;
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0.2f));
        psGo.GetComponent<ParticleSystemRenderer>().sharedMaterial = M["fire"];
    }

    static void WreckedCar(Transform parent, Vector3 pos, float yaw, bool flipped)
    {
        var g = Group("WreckedCar", parent, pos, yaw);
        var body = Group("Body", g, Vector3.zero);
        if (flipped) body.localRotation = Quaternion.Euler(0, 0, 180);
        float yBase = flipped ? 0.95f : 0.35f;
        var paint = Pick(M["metal"], M["paintRed"], M["paintBlue"]);
        Box("Chassis", body, new Vector3(0, flipped ? -0.6f : 0.65f, 0), new Vector3(1.8f, 0.8f, 4.3f), paint, 1.5f);
        Box("Cabin", body, new Vector3(0, flipped ? -1.3f : 1.35f, -0.25f), new Vector3(1.65f, 0.65f, 2.2f), paint, 1.5f);
        Box("Windshield", body, new Vector3(0, flipped ? -1.3f : 1.35f, 0.86f), new Vector3(1.55f, 0.55f, 0.02f), M["glass"], 1f, false);
        foreach (var x in new[] { -0.85f, 0.85f })
            foreach (var z in new[] { -1.4f, 1.4f })
                if (!Chance(0.15))
                    Prim(PrimitiveType.Cylinder, "Wheel", body, new Vector3(x, flipped ? -0.05f : 0.35f, z), new Vector3(0.65f, 0.12f, 0.65f), M["tire"], new Vector3(0, 0, 90));
        if (flipped) g.localPosition += Vector3.up * yBase;
    }

    static void BuildStreetProps(Transform geo, Transform points)
    {
        var props = Group("StreetProps", geo, Vector3.zero);
        for (int i = 0; i < 16; i++)
        {
            float rc = Pick(RoadCenters);
            float along = R(-72f, 72f);
            bool alongZ = Chance(0.5);
            var p = alongZ ? new Vector3(rc + R(-3.5f, 3.5f), 0, along) : new Vector3(along, 0, rc + R(-3.5f, 3.5f));
            if (Vector3.Distance(p, SpawnPos) < 9) continue;
            float yaw = (alongZ ? 0 : 90) + R(-25, 25) + (Chance(0.2) ? 90 : 0);
            WreckedCar(props, p, yaw, Chance(0.15));
            if (Chance(0.45)) Spawn<ItemSpawnPoint>(points, p + Quaternion.Euler(0, yaw, 0) * new Vector3(1.4f, 0.2f, R(-1, 1)));
        }
        for (int i = 0; i < 9; i++)
        {
            var p = RandomRoadPoint();
            if (Vector3.Distance(p, SpawnPos) < 8) continue;
            RubblePile(props, p, rng.Next(6, 12), R(1f, 1.8f));
            if (Chance(0.3)) Spawn<ItemSpawnPoint>(points, p + new Vector3(1.8f, 0.3f, 0));
        }
        for (int i = 0; i < 6; i++)
        {
            var p = RandomRoadPoint();
            float yaw = R(0, 360);
            for (int k = 0; k < 3; k++)
                Box("Barrier", props, p + Quaternion.Euler(0, yaw, 0) * new Vector3(k * 2.1f, 0.45f, 0), new Vector3(2f, 0.9f, 0.6f), M["dirty"], 2f, true, new Vector3(0, yaw + R(-8, 8), Chance(0.2) ? 80 : 0));
        }
        FireBarrel(props, new Vector3(-19.5f, 0, -19.5f)); // 출발 지점 근처
        FireBarrel(props, new Vector3(1.5f, 0, -21f));
        FireBarrel(props, new Vector3(24f, 0, 26f));
        FireBarrel(props, new Vector3(-26f, 0, 40f));
    }

    static void BuildHatch(Transform geo, Vector3 pos)
    {
        var g = Group("ShelterHatch", geo, pos);
        Box("Bunker", g, new Vector3(0, 1.3f, 0), new Vector3(3.2f, 2.6f, 3f), M["dirty"], 2f);
        Box("Roof", g, new Vector3(0, 2.7f, 0), new Vector3(3.5f, 0.2f, 3.3f), M["cracked"], 2f);
        Prim(PrimitiveType.Cylinder, "Vent", g, new Vector3(0.9f, 3.2f, -0.6f), new Vector3(0.25f, 0.5f, 0.25f), M["metal"], null, true);
        var door = Box("HatchDoor", g, new Vector3(0, 1.05f, 1.53f), new Vector3(1.1f, 2.1f, 0.08f), M["metal"], 1.2f);
        door.AddComponent<CityExit>();
        Prim(PrimitiveType.Cylinder, "Wheel", g, new Vector3(0, 1.1f, 1.6f), new Vector3(0.4f, 0.02f, 0.4f), M["metal"], new Vector3(90, 0, 0));
        Box("Lamp", g, new Vector3(0, 2.3f, 1.55f), new Vector3(0.22f, 0.12f, 0.1f), M["redLamp"], 1f, false);
        var l = PointLight("BeaconLight", g, new Vector3(0, 2.25f, 1.9f), new Color(1f, 0.12f, 0.08f), 8f, 10f);
        Flicker(l, 0.1f, 3f, false);
        RubblePile(g, new Vector3(-2.3f, 0, 0.3f), 5, 0.8f);
        Box("Sandbag1", g, new Vector3(1.9f, 0.25f, 1.2f), new Vector3(0.9f, 0.5f, 0.45f), M["cloth"], 1f, true, new Vector3(0, 15, 0));
        Box("Sandbag2", g, new Vector3(1.85f, 0.7f, 1.15f), new Vector3(0.8f, 0.4f, 0.42f), M["cloth"], 1f, true, new Vector3(0, -10, 0));
    }

    static void BuildTrader(Transform parent, Vector3 pos)
    {
        var g = Group("Trader", parent, pos, R(0, 360));
        Prim(PrimitiveType.Capsule, "Coat", g, new Vector3(0, 0.85f, 0), new Vector3(0.6f, 0.82f, 0.5f), M["coat"]);
        var head = Group("Head", g, new Vector3(0, 1.72f, 0));
        Prim(PrimitiveType.Sphere, "Face", head, Vector3.zero, new Vector3(0.24f, 0.27f, 0.25f), M["skin"]);
        Prim(PrimitiveType.Cylinder, "HatBrim", head, new Vector3(0, 0.12f, 0), new Vector3(0.42f, 0.012f, 0.42f), M["coat"]);
        Prim(PrimitiveType.Cylinder, "HatCrown", head, new Vector3(0, 0.19f, 0), new Vector3(0.25f, 0.07f, 0.25f), M["coat"]);
        Box("Scarf", head, new Vector3(0, -0.08f, 0.08f), new Vector3(0.24f, 0.1f, 0.12f), M["paintRed"], 1f, false);
        Box("Pack", g, new Vector3(0, 1.1f, -0.33f), new Vector3(0.5f, 0.7f, 0.3f), M["blanket"], 1f, false);
        var col = g.gameObject.AddComponent<CapsuleCollider>();
        col.height = 1.9f;
        col.radius = 0.35f;
        col.center = new Vector3(0, 0.95f, 0);
        g.gameObject.AddComponent<Trader>().head = head;

        var crate = Box("Crate", g, new Vector3(0.9f, 0.3f, 0.3f), new Vector3(0.7f, 0.6f, 0.6f), M["wood"], 1f, true);
        crate.transform.localRotation = Quaternion.Euler(0, 15, 0);
        Prim(PrimitiveType.Cylinder, "Lantern", g, new Vector3(0.9f, 0.73f, 0.3f), new Vector3(0.12f, 0.12f, 0.12f), M["lantern"]);
        var l = PointLight("LanternLight", g, new Vector3(0.9f, 0.95f, 0.3f), new Color(1f, 0.65f, 0.35f), 6f, 9f, true);
        Flicker(l, 0.12f, 6f, false);
        Box("Goods1", g, new Vector3(-0.8f, 0.2f, 0.4f), new Vector3(0.5f, 0.4f, 0.4f), M["wood"], 1f, true, new Vector3(0, -20, 0));
        Prim(PrimitiveType.Cylinder, "Goods2", g, new Vector3(-0.8f, 0.55f, 0.4f), new Vector3(0.12f, 0.12f, 0.12f), M["metal"]);
    }

    static void BuildBoundary(Transform geo)
    {
        var g = Group("Boundary", geo, Vector3.zero);
        for (int side = 0; side < 4; side++)
        {
            float yaw = side * 90;
            var rot = Quaternion.Euler(0, yaw, 0);
            Box("WallOfRubble", g, rot * new Vector3(0, 5, 80), new Vector3(170, 10, 3), M["cracked"], 4f, true, new Vector3(0, yaw, 0));
            Box("Invisible", g, rot * new Vector3(0, 15, 78.2f), new Vector3(170, 30, 0.5f), M["cracked"], 4f, true, new Vector3(0, yaw, 0)).GetComponent<MeshRenderer>().enabled = false;
            for (int i = 0; i < 10; i++)
                RubblePile(g, rot * new Vector3(R(-75, 75), 0, 77.5f), 6, 2f);
        }
    }

    static void AddAshFall(Transform player)
    {
        var go = new GameObject("AshFall");
        go.transform.SetParent(player, false);
        go.transform.localPosition = new Vector3(0, 7, 0);
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = 9f;
        main.startSpeed = 0.2f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.07f);
        main.startColor = new Color(0.6f, 0.6f, 0.58f, 0.75f);
        main.gravityModifier = 0.03f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 2500;
        main.prewarm = true;
        var emission = ps.emission;
        emission.rateOverTime = 220;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(32, 1, 32);
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.35f;
        noise.frequency = 0.3f;
        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = M["ash"];
    }
}
