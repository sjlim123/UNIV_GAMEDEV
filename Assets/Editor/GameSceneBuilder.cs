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
// 파일 나눔: 이 파일(공통·도우미) / .Prefabs / .Shelter / .Menu / .City / .CutsceneScene(도입 컷씬 씬) / .Cutscene(옛 컷씬 사진 생성기)
public static partial class GameSceneBuilder
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
        BuildShelterScene(player, zombie);
        BuildCityScene(player, zombie, dog);
        BuildMenuScene(zombie);
        BuildCutsceneScene();

        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(SceneDir + "/MainMenu.unity", true),
            new EditorBuildSettingsScene(SceneDir + "/Cutscene.unity", true),
            new EditorBuildSettingsScene(SceneDir + "/Shelter.unity", true),
            new EditorBuildSettingsScene(SceneDir + "/City.unity", true),
        };
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene(SceneDir + "/MainMenu.unity");
        Debug.Log("[GameSceneBuilder] MainMenu / Cutscene / Shelter / City 씬 생성 완료");
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
        // 밝은 강철판 (제작대): 녹슨 쇠의 요철(노멀)만 빌리고 색은 밝은 회색으로. 금속성을 낮게 둬야 어두운 방에서도 전구 빛을 받아 보인다
        M["steel"] = TexMat("M_Steel", "rusty_metal_02", new Color(0.45f, 0.45f, 0.44f), 0.5f, 0.3f);
        M["steel"].SetTexture("_BaseMap", null);
        M["steel"].SetTexture("_MainTex", null);
        M["steel"].SetFloat("_BumpScale", 0.6f);
        EditorUtility.SetDirty(M["steel"]);
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
        // 실행 중에 쓰는 효과 입자 재질 (Resources/Effects: 타격 피, 화상 불꽃·연기)
        ParticleMat("M_HitBlood", Color.white, false, EffectDir);
        ParticleMat("M_BurnFire", Color.white, true, EffectDir);
        ParticleMat("M_BurnSmoke", Color.white, false, EffectDir);
        // 메인 화면의 비: 빗줄기·젖은 바닥·물웅덩이
        M["rain"] = ParticleMat("M_RainParticle", new Color(0.72f, 0.78f, 0.9f, 0.6f), true);
        M["wetAsphalt"] = TexMat("M_WetAsphalt", "asphalt_02", new Color(0.42f, 0.44f, 0.48f), 0.62f);
        M["wetConcrete"] = TexMat("M_WetConcrete", "concrete_floor_worn_001", new Color(0.46f, 0.47f, 0.5f), 0.55f);
        M["puddle"] = TexMat("M_Puddle", null, new Color(0.1f, 0.11f, 0.13f), 0.95f);
        M["crossPaint"] = TexMat("M_CrossPaint", "concrete_floor_worn_001", new Color(0.72f, 0.72f, 0.7f), 0.35f);
    }

    // ---------- Poly Haven 소품 (CC0, Assets/ThirdParty/PolyHaven/Models) ----------

    const string PolyModelDir = TexDir + "/Models";

    // 모델의 색·노멀 텍스처로 URP 재질을 만든다 (모델마다 한 벌)
    static Material PolyMat(string id, float smoothness, float metallic)
    {
        var m = LoadOrCreate("M_PH_" + id, Shader.Find("Universal Render Pipeline/Lit"));
        var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>($"{PolyModelDir}/{id}/textures/{id}_diff_1k.jpg");
        var normal = AssetDatabase.LoadAssetAtPath<Texture2D>($"{PolyModelDir}/{id}/textures/{id}_nor_gl_1k.jpg");
        m.SetTexture("_BaseMap", albedo);
        m.SetTexture("_MainTex", albedo);
        if (normal != null)
        {
            m.SetTexture("_BumpMap", normal);
            m.SetFloat("_BumpScale", 1f);
            m.EnableKeyword("_NORMALMAP");
        }
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Smoothness", smoothness);
        m.SetFloat("_Metallic", metallic);
        EditorUtility.SetDirty(m);
        return m;
    }

    // Poly Haven 소품 모델을 놓는다. 모델 원점(바닥 가운데)이 pos에 오고 yaw만큼 돈다.
    // collider면 모델을 감싸는 상자 콜라이더를 단다 (내비메시도 피해 간다)
    static GameObject PolyProp(string id, Transform parent, Vector3 pos, float yaw, float smoothness = 0.3f, float metallic = 0f, bool collider = true, Vector3? tilt = null)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{PolyModelDir}/{id}/{id}_1k.fbx");
        if (model == null)
        {
            Debug.LogWarning("[GameSceneBuilder] Poly Haven 모델이 없음: " + id);
            return null;
        }
        var holder = Group(id, parent, pos, yaw);
        if (tilt.HasValue) holder.localRotation *= Quaternion.Euler(tilt.Value);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(model, holder);
        go.transform.localPosition = Vector3.zero;
        var mat = PolyMat(id, smoothness, metallic);
        var rs = go.GetComponentsInChildren<MeshRenderer>();
        foreach (var r in rs)
        {
            var mats = new Material[r.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = mat;
            r.sharedMaterials = mats;
        }
        if (collider && rs.Length > 0)
        {
            var b = new Bounds(holder.InverseTransformPoint(rs[0].bounds.center), Vector3.zero);
            foreach (var r in rs)
            {
                var rb = r.bounds;
                for (int k = 0; k < 8; k++)
                    b.Encapsulate(holder.InverseTransformPoint(rb.center + Vector3.Scale(rb.extents, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1))));
            }
            var col = holder.gameObject.AddComponent<BoxCollider>();
            col.center = b.center;
            col.size = b.size;
        }
        foreach (var t in holder.GetComponentsInChildren<Transform>())
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
        return holder.gameObject;
    }

    // 비: 영역(center, size) 위에서 빗줄기가 떨어진다 (늘인 빌보드)
    static ParticleSystem AddRain(Transform parent, Vector3 center, Vector3 size, float rate)
    {
        var go = new GameObject("Rain");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = center + Vector3.up * size.y * 0.5f;
        go.transform.localRotation = Quaternion.Euler(90, 0, 0); // 상자 모양의 +z(뿜는 방향)가 아래를 보게
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = size.y / 16f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(15f, 18f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.016f, 0.028f);
        main.startColor = new Color(0.75f, 0.8f, 0.92f, 0.75f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = Mathf.CeilToInt(rate * main.startLifetime.constant * 1.2f) + 100;
        main.prewarm = true;
        var emission = ps.emission;
        emission.rateOverTime = rate;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(size.x, size.z, 0.1f);
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(0.6f, 1.1f); // 바람에 살짝 비스듬히
        vel.y = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Stretch;
        r.velocityScale = 0.045f;
        r.lengthScale = 1f;
        r.sharedMaterial = M["rain"];
        r.shadowCastingMode = ShadowCastingMode.Off;
        return ps;
    }

    const string EffectDir = "Assets/Resources/Effects";

    static Material LoadOrCreate(string name, Shader shader, string dir = null)
    {
        dir = dir ?? MatDir;
        if (!AssetDatabase.IsValidFolder(dir))
        {
            int cut = dir.LastIndexOf('/');
            AssetDatabase.CreateFolder(dir.Substring(0, cut), dir.Substring(cut + 1));
        }
        string path = $"{dir}/{name}.mat";
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

    static Material ParticleMat(string name, Color color, bool additive, string dir = null)
    {
        var src = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat");
        var m = LoadOrCreate(name, Shader.Find("Universal Render Pipeline/Particles/Unlit"), dir);
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

    // 같은 평면에서 같은 방향으로 겹친 상자 면은 화면에서 지지직 깜빡인다 (z-fighting).
    // 씬을 저장하기 전에 축에 나란한 상자끼리 그런 면을 찾아, 한쪽 상자를 그 면 쪽으로 2mm 줄여 다른 상자 안으로 숨긴다.
    // 줄일 상자: 면이 다른 면 안에 다 들어가는 쪽. 둘 다 아니면 옆면(수평 방향)일 때만 면이 작은 쪽을 줄인다
    // (벽 윗면처럼 위·아래 면을 줄이면 천장과 틈이 보일 수 있다). 고친 수를 돌려준다
    static int FixCoplanarFaces()
    {
        const float Eps = 0.002f, Same = 0.0015f;
        var boxes = new List<MeshRenderer>();
        foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null || mf.sharedMesh.vertexCount != 24) continue; // 상자만
            if (AxisAligned(r.transform)) boxes.Add(r);
        }
        boxes.Sort((a, b) => a.bounds.min.x.CompareTo(b.bounds.min.x));
        int fixedCount = 0;
        for (int i = 0; i < boxes.Count; i++)
        {
            for (int j = i + 1; j < boxes.Count && boxes[j].bounds.min.x <= boxes[i].bounds.max.x + Same; j++)
            {
                for (int axis = 0; axis < 3; axis++)
                {
                    foreach (int side in new[] { -1, 1 })
                    {
                        Bounds a = boxes[i].bounds, b = boxes[j].bounds;
                        float pa = side < 0 ? a.min[axis] : a.max[axis];
                        float pb = side < 0 ? b.min[axis] : b.max[axis];
                        if (Mathf.Abs(pa - pb) > Same) continue;
                        int u = (axis + 1) % 3, v = (axis + 2) % 3;
                        float ou = Mathf.Min(a.max[u], b.max[u]) - Mathf.Max(a.min[u], b.min[u]);
                        float ov = Mathf.Min(a.max[v], b.max[v]) - Mathf.Max(a.min[v], b.min[v]);
                        if (ou <= 0.005f || ov <= 0.005f) continue;
                        float areaA = a.size[u] * a.size[v], areaB = b.size[u] * b.size[v];
                        var small = areaA <= areaB ? boxes[i] : boxes[j];
                        bool inside = ou * ov >= Mathf.Min(areaA, areaB) * 0.98f;
                        if (!inside && axis == 1) continue;
                        if (ShrinkFace(small, axis, side, Eps)) fixedCount++;
                    }
                }
            }
        }
        return fixedCount;
    }

    // 상호작용 물체(제작대·화로 등)는 정적 배칭에서 뺀다. 합쳐지면 메시가 사라져 윤곽선(InteractOutline)을 만들 수 없다
    static void UnbatchInteractables()
    {
        foreach (var it in Object.FindObjectsByType<Interactable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            foreach (var t in it.GetComponentsInChildren<Transform>(true))
            {
                var flags = GameObjectUtility.GetStaticEditorFlags(t.gameObject);
                if ((flags & StaticEditorFlags.BatchingStatic) != 0)
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags & ~StaticEditorFlags.BatchingStatic);
            }
    }

    static bool AxisAligned(Transform t)
    {
        var e = t.rotation.eulerAngles;
        for (int k = 0; k < 3; k++)
        {
            float m = Mathf.Repeat(e[k], 90f);
            if (m > 0.5f && m < 89.5f) return false;
        }
        return true;
    }

    // 상자를 월드 축 axis의 side 쪽(-1/+1)으로 eps만큼 줄인다 (반대쪽 면은 그대로)
    static bool ShrinkFace(MeshRenderer r, int axis, int side, float eps)
    {
        var t = r.transform;
        float size = r.bounds.size[axis];
        if (size <= eps * 4) return false;
        var worldAxis = Vector3.zero;
        worldAxis[axis] = 1;
        var local = t.InverseTransformDirection(worldAxis);
        int la = Mathf.Abs(local.x) > 0.5f ? 0 : Mathf.Abs(local.y) > 0.5f ? 1 : 2;
        var s = t.localScale;
        s[la] *= (size - eps) / size;
        t.localScale = s;
        t.position -= worldAxis * (side * eps * 0.5f);
        return true;
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

    static T Spawn<T>(Transform parent, Vector3 worldPos) where T : Component
    {
        var go = new GameObject(typeof(T).Name);
        go.transform.SetParent(parent, true);
        go.transform.position = worldPos;
        return go.AddComponent<T>();
    }

    // 건물 밖 바닥의 아이템 자리 (돌만 나온다)
    static void OutdoorItemPoint(Transform parent, Vector3 worldPos) => Spawn<ItemSpawnPoint>(parent, worldPos).outdoor = true;

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

    // 경첩 문 (wall: 벽 Transform, c: 벽 로컬 기준 구멍 중심, wallThick: 벽 두께).
    // mat이 쇠면 녹슨 철문(리벳·보강 띠·레버 손잡이·작은 창), 아니면 나무 판문(판자 무늬·둥근 손잡이).
    // 벽 양면에 문틀을 두르고, 문짝의 장식은 양면에 붙인다 (문짝 면에서 조금씩 띄워 겹친 면이 생기지 않게)
    static void AddDoor(Transform wall, float c, float width, float height, Material mat, float wallThick = 0.3f)
    {
        bool steel = mat == M["metal"];
        DoorCasing(wall, c, width, height, wallThick, steel ? M["metal"] : M["wood"]);

        var pivot = Group("Door", wall, new Vector3(c - width / 2 + 0.03f, 0, 0));
        pivot.gameObject.AddComponent<Door>();
        pivot.gameObject.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
        float lw = width - 0.08f, lh = height - 0.04f, lt = steel ? 0.07f : 0.06f;
        var leafGo = Box("Leaf", pivot, new Vector3(width / 2 - 0.03f, height / 2, 0), new Vector3(lw, lh, lt), mat, 1.2f, true, null, false);
        var leaf = leafGo.transform;
        foreach (int s in new[] { -1, 1 }) // 문짝 양면
        {
            float z = s * (lt / 2);
            if (steel)
            {
                // 가로 보강 띠 셋 + 띠마다 리벳, 아래 발판
                foreach (float y in new[] { -lh * 0.32f, 0f, lh * 0.32f })
                {
                    Box("Band", leaf, new Vector3(0, y, z + s * 0.006f), new Vector3(lw - 0.06f, 0.07f, 0.012f), M["metal"], 1f, false, null, false);
                    for (int i = 0; i < 5; i++)
                        Prim(PrimitiveType.Sphere, "Rivet", leaf, new Vector3(-lw / 2 + 0.08f + i * (lw - 0.16f) / 4f, y, z + s * 0.013f), new Vector3(0.018f, 0.018f, 0.01f), M["tire"]);
                }
                Box("KickPlate", leaf, new Vector3(0, -lh / 2 + 0.13f, z + s * 0.005f), new Vector3(lw - 0.04f, 0.22f, 0.01f), M["dirty"], 1f, false, null, false);
                // 레버 손잡이와 열쇠 구멍 판
                Box("LockPlate", leaf, new Vector3(lw / 2 - 0.12f, -0.12f, z + s * 0.006f), new Vector3(0.06f, 0.2f, 0.012f), M["tire"], 1f, false, null, false);
                Box("Lever", leaf, new Vector3(lw / 2 - 0.17f, -0.06f, z + s * 0.035f), new Vector3(0.14f, 0.025f, 0.025f), M["line"], 1f, false, null, false);
                Prim(PrimitiveType.Cylinder, "LeverNeck", leaf, new Vector3(lw / 2 - 0.12f, -0.06f, z + s * 0.02f), new Vector3(0.025f, 0.015f, 0.025f), M["line"], new Vector3(90, 0, 0));
            }
            else
            {
                // 판자 무늬 네 칸 (위 두 칸은 길고 아래 두 칸은 짧다) + 둥근 손잡이
                float pw = (lw - 0.24f) / 2f;
                foreach (float x in new[] { -pw / 2 - 0.04f, pw / 2 + 0.04f })
                {
                    Box("Panel", leaf, new Vector3(x, lh * 0.18f, z + s * 0.005f), new Vector3(pw, lh * 0.42f, 0.01f), mat, 1f, false, null, false);
                    Box("Panel", leaf, new Vector3(x, -lh * 0.3f, z + s * 0.005f), new Vector3(pw, lh * 0.26f, 0.01f), mat, 1f, false, null, false);
                }
                Prim(PrimitiveType.Sphere, "Knob", leaf, new Vector3(lw / 2 - 0.1f, -0.1f, z + s * 0.04f), new Vector3(0.06f, 0.06f, 0.05f), M["line"]);
                Prim(PrimitiveType.Cylinder, "KnobRose", leaf, new Vector3(lw / 2 - 0.1f, -0.1f, z + s * 0.006f), new Vector3(0.07f, 0.006f, 0.07f), M["line"], new Vector3(90, 0, 0));
            }
        }
        if (steel)
        {
            // 위쪽 작은 창: 어두운 유리가 문짝을 꿰뚫는다 (양면에서 조금 튀어나와 겹친 면이 없다)
            float wy = lh * 0.33f + 0.12f;
            Box("Window", leaf, new Vector3(0, wy, 0), new Vector3(0.3f, 0.22f, lt + 0.004f), M["glass"], 1f, false, null, false);
            foreach (int s in new[] { -1, 1 }) // 창 테두리 (위·아래·왼쪽·오른쪽 막대)
            {
                float fz = s * (lt / 2 + 0.008f);
                Box("WindowFrame", leaf, new Vector3(0, wy + 0.125f, fz), new Vector3(0.36f, 0.03f, 0.012f), M["tire"], 1f, false, null, false);
                Box("WindowFrame", leaf, new Vector3(0, wy - 0.125f, fz), new Vector3(0.36f, 0.03f, 0.012f), M["tire"], 1f, false, null, false);
                Box("WindowFrame", leaf, new Vector3(-0.165f, wy, fz), new Vector3(0.03f, 0.22f, 0.012f), M["tire"], 1f, false, null, false);
                Box("WindowFrame", leaf, new Vector3(0.165f, wy, fz), new Vector3(0.03f, 0.22f, 0.012f), M["tire"], 1f, false, null, false);
            }
        }
        var obstacle = leafGo.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.size = new Vector3(lw, height, 0.3f);
        obstacle.carving = true;
    }

    // 문틀: 벽 양면에서 구멍 둘레(왼쪽·오른쪽·위)를 감싸는 띠. 벽 면에서 2.4cm 튀어나온다
    // 판자로 막힌 출입구: 아래 gap(m)만 비우고 위를 판자로 막는다. 앉으면(키 1.1m) 지나가고, 서 있거나 좀비(1.8m)는 못 지나간다
    const float CrawlGap = 1.4f;

    static void BoardedDoorway(Transform wall, float c, float width, float height, float gap, System.Random r, float wallThick = 0.3f)
    {
        DoorCasing(wall, c, width, height, wallThick, M["wood"]);
        var g = Group("BoardedDoor", wall, new Vector3(c, 0, 0));
        float f(float a, float b) => a + (float)r.NextDouble() * (b - a);
        // 가로 판자: 맨 아래 판자의 아랫면이 gap 높이
        float y = gap;
        int i = 0;
        while (y < height - 0.05f)
        {
            // 맨 아래 판자는 거의 수평으로 (기울면 한쪽 끝이 틈으로 내려와 앉은 채로도 걸린다)
            float tilt = i == 0 ? f(-1.5f, 1.5f) : f(-5, 5), len = width + f(0.15f, 0.35f);
            if (i == 0) y = gap + 0.085f + len / 2 * Mathf.Sin(Mathf.Abs(tilt) * Mathf.Deg2Rad);
            var plank = Box("Plank" + i, g, new Vector3(f(-0.06f, 0.06f), y, 0), new Vector3(len, 0.17f, 0.045f), M["wood"], 1f, true, new Vector3(0, 0, tilt));
            foreach (int s in new[] { -1, 1 })
                foreach (float x in new[] { -len / 2 + 0.08f, len / 2 - 0.08f })
                    Prim(PrimitiveType.Sphere, "Nail", plank.transform, new Vector3(x, 0.05f, s * 0.026f), new Vector3(0.02f, 0.02f, 0.012f), M["tire"]);
            y += f(0.2f, 0.27f);
            i++;
        }
        // 비스듬히 덧댄 판자 (틈 위쪽만 가로지른다)
        Box("PlankDiag", g, new Vector3(0, (gap + height) / 2 + 0.05f, 0.04f), new Vector3(0.16f, Mathf.Sqrt(width * width + (height - gap) * (height - gap)) * 0.9f, 0.04f), M["wood"], 1f, true,
            new Vector3(0, 0, Mathf.Atan2(width, height - gap) * Mathf.Rad2Deg * (r.NextDouble() < 0.5 ? 1 : -1)));
        // 문턱의 부서진 판자 조각 (지나다닐 수 있게 낮게)
        Box("Splinter", g, new Vector3(f(-0.4f, 0.4f), 0.03f, f(-0.5f, 0.5f)), new Vector3(0.5f, 0.03f, 0.12f), M["wood"], 1f, false, new Vector3(0, f(0, 180), 0));
        // 앞뒤에서 "앉으면 지나갈 수 있다" 알림
        var hint = new GameObject("CrawlHint").AddComponent<CrawlHint>();
        hint.transform.SetParent(g, false);
        hint.halfSize = new Vector3(width / 2 + 0.6f, 1.2f, 1.6f);
    }

    static void DoorCasing(Transform wall, float c, float width, float height, float wallThick, Material mat)
    {
        const float trim = 0.09f, depth = 0.024f;
        foreach (int s in new[] { -1, 1 })
        {
            float z = s * (wallThick / 2 + depth / 2);
            Box("Casing", wall, new Vector3(c - width / 2 - trim / 2, (height + trim) / 2, z), new Vector3(trim, height + trim, depth), mat, 1f, false);
            Box("Casing", wall, new Vector3(c + width / 2 + trim / 2, (height + trim) / 2, z), new Vector3(trim, height + trim, depth), mat, 1f, false);
            Box("Casing", wall, new Vector3(c, height + trim / 2, z), new Vector3(width, trim, depth), mat, 1f, false);
        }
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
}
