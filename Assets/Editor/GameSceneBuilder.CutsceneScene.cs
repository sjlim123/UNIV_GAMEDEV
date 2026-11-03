using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// 메뉴 Tools > 핵폭발 생존 > 컷씬 씬 생성
// 새 게임 도입 컷씬을 실시간으로 재생하는 Cutscene 씬을 짓는다:
//  - 거실 (창·커튼 봉·TV·소파·가구·소품), 창밖 도시와 땅, 하늘(절차적 스카이박스)
//  - 방공호 앞 거리 (메인 화면과 같은 터널 입구, 콘크리트 벙커, 표지판·경광등, 버려진 차·잔해, 먼 불길)
//  - 햇빛·섬광·TV·창가·보조 조명, 카메라, 후처리 볼륨(피사계 심도·블룸·비네트·그레인·모션 블러)
//  - 모든 참조를 채운 CutsceneDirector. 사람·버섯구름·유리 조각·커튼·먼지·재는 실행 중에 CutsceneDirector가 만든다
// TV 화면·표지판 그림은 컷씬 이미지 생성기(GameSceneBuilder.Cutscene)와 같은 코드로 그려 PNG로 저장한다.
// 재질은 Assets/Materials/Cutscene, 그림은 Assets/Generated/Cutscene에 남긴다 (다시 실행하면 덮어쓴다)
public static partial class GameSceneBuilder
{
    const string CsMatDir = "Assets/Materials/Cutscene";
    const string CsTexDir = "Assets/Generated/Cutscene";
    static readonly Vector3 CsStreetOrigin = new Vector3(0, 0, -300); // 거리는 거실과 멀리 떨어뜨려 짓는다
    const float CsCityGround = -20f;                                  // 거실은 6층쯤: 창밖 도시의 땅은 20m 아래
    static readonly Dictionary<string, Material> CM = new Dictionary<string, Material>();

    [MenuItem("Tools/핵폭발 생존/컷씬 씬 생성")]
    public static void BuildCutsceneSceneMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[GameSceneBuilder] 플레이 모드에서는 컷씬 씬을 만들 수 없습니다.");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EnsureFolder("Assets", "Materials");
        EnsureFolder("Assets/Materials", "Game");
        EnsureFolder("Assets", "Generated");
        EnsureFolder("Assets", "Scenes");
        EnsureFolder("Assets", "Settings");
        ConfigurePipeline();
        CreateMaterials();
        BuildCutsceneScene();
        AddCutsceneToBuildList();
        AssetDatabase.SaveAssets();
        string menu = SceneDir + "/MainMenu.unity";
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(menu) != null) EditorSceneManager.OpenScene(menu);
        Debug.Log("[GameSceneBuilder] Cutscene 씬 생성 완료: " + SceneDir + "/Cutscene.unity");
    }

    // 빌드 씬 목록에 Cutscene을 MainMenu 바로 뒤에 넣는다 (이미 있으면 그대로)
    static void AddCutsceneToBuildList()
    {
        var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        string path = SceneDir + "/Cutscene.unity";
        if (list.Exists(s => s.path == path)) return;
        int at = list.FindIndex(s => s.path.EndsWith("MainMenu.unity"));
        list.Insert(at < 0 ? 0 : at + 1, new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = list.ToArray();
    }

    static void BuildCutsceneScene()
    {
        EnsureFolder("Assets/Materials", "Cutscene");
        EnsureFolder("Assets/Generated", "Cutscene");
        if (!M.ContainsKey("blastStripe")) M["blastStripe"] = TexMat("M_BlastDoorStripe", "rusty_metal_02", new Color(0.72f, 0.55f, 0.1f), 0.3f, 0.3f);

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        BeginMeshes(GenDir + "/CutsceneSceneMeshes.asset");
        rng = new System.Random(31);
        CM.Clear();
        cutTemp.Clear();
        CutPrepareFont();
        // 에디터가 글꼴 글자판을 아직 GPU에 올리지 않았으면(방금 컴파일한 직후 등) 글자가 비어 나온다. 그러면 한 번 더 시도한다
        if (!CsFontReady())
        {
            cutFont = null;
            cutAtlas = null;
            CutPrepareFont();
            if (!CsFontReady()) Debug.LogWarning("[GameSceneBuilder] 글꼴 글자판이 비어 있어 TV·표지판 글자가 빠질 수 있습니다. 게임을 한 번 플레이한 뒤 '컷씬 씬 생성'을 다시 실행하세요.");
        }
        CsCreateMaterials();
        foreach (var o in cutTemp)
            if (o != null) Object.DestroyImmediate(o);
        cutTemp.Clear();
        cutFont = null;
        cutAtlas = null;

        var rootGo = new GameObject("CutsceneWorld");
        var d = rootGo.AddComponent<CutsceneDirector>();
        d.home = Group("Home", rootGo.transform, Vector3.zero);
        CsBuildApartment(d.home, d);
        CsBuildOutside(d.home, d);
        d.street = Group("Street", rootGo.transform, CsStreetOrigin);
        CsBuildStreet(d.street, d);
        CsBuildLightsAndCamera(rootGo.transform, d);

        d.tvShow = CM["tvShow"];
        d.tvNews = CM["tvNews"];
        d.tvStatic = CM["tvStatic"];
        d.glowTemplate = M["redLamp"];
        d.glassShard = CM["glassShard"];
        d.shockRing = CM["shock"];
        d.skyMaterial = CM["sky"];
        d.smokeParticle = CM["pSmoke"];
        d.ashParticle = CM["pAsh"];
        d.emberParticle = CM["pEmber"];
        d.dustParticle = CM["pDust"];

        Debug.Log($"[GameSceneBuilder] Cutscene: 같은 면에 겹친 상자 {FixCoplanarFaces()}곳을 고침");
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(scene, SceneDir + "/Cutscene.unity");
    }

    // ================= 재질·그림 =================

    // 글꼴 글자판에 글자가 실제로 그려져 있는지 (알파·빨강 채널에 뭔가 있는지)
    static bool CsFontReady()
    {
        if (cutFont == null || cutAtlas == null) return false;
        var px = cutAtlas.GetPixels32();
        long sum = 0;
        for (int i = 0; i < px.Length; i += 7) sum += px[i].a + px[i].r;
        return sum > px.Length / 7 * 2;
    }

    static void CsCreateMaterials()
    {
        var soft = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/Effects/T_SoftCircle.png");
        // 코드로 그린 그림을 먼저 전부 그린 뒤에 PNG로 들여온다 (들여오기 도중 글꼴 글자판이 다시 만들어져 뒤의 글자가 빠지지 않게)
        var texShow = CutTvShowTexture();
        var texNews = CutTvNewsTexture();
        var texSign = CutSignTexture();
        var texStatic = CsStaticTexture();
        var texFacade = CsFacadeTexture();
        var texWallpaper = CsWallpaperTexture();
        var texFabric = CsFabricTexture();
        var tvShow = CsPng("tv_show", texShow, false);
        var tvNews = CsPng("tv_news", texNews, false);
        var tvStatic = CsPng("tv_static", texStatic, true);
        var sign = CsPng("shelter_sign", texSign, false);
        var facade = CsPng("facade", texFacade, true);
        var wallpaper = CsPng("wallpaper", texWallpaper, true);
        var fabric = CsPng("fabric", texFabric, true);

        CM["tvShow"] = CsUnlit("TvShow", tvShow, Color.white);
        CM["tvNews"] = CsUnlit("TvNews", tvNews, Color.white);
        CM["tvStatic"] = CsUnlit("TvStatic", tvStatic, Color.white);
        CM["sign"] = CsUnlit("ShelterSign", sign, new Color(1.15f, 1.15f, 1.15f));
        CM["facade"] = CsLit("Facade", Color.white, 0.25f, facade);
        CM["wallpaper"] = CsLit("Wallpaper", Color.white, 0.08f, wallpaper);
        CM["ceiling"] = CsLit("Ceiling", new Color(0.9f, 0.89f, 0.86f), 0.05f);
        CM["trim"] = CsLit("Trim", new Color(0.94f, 0.93f, 0.91f), 0.35f);
        CM["floor"] = CsPoly("WoodFloor", "wood_planks_dirt", new Color(0.95f, 0.78f, 0.6f), 0.45f);
        CM["darkWood"] = CsPoly("DarkWood", "wood_planks_dirt", new Color(0.45f, 0.32f, 0.24f), 0.4f);
        CM["sofa"] = CsLit("Sofa", new Color(0.24f, 0.34f, 0.37f), 0.1f, fabric);
        CM["pillow"] = CsLit("Pillow", new Color(0.8f, 0.62f, 0.36f), 0.08f, fabric);
        CM["rug"] = CsLit("Rug", new Color(0.72f, 0.56f, 0.44f), 0.03f, fabric);
        CM["leaf"] = CsLit("Leaf", new Color(0.2f, 0.4f, 0.18f), 0.25f);
        CM["pot"] = CsLit("Pot", new Color(0.74f, 0.44f, 0.3f), 0.2f);
        CM["fruit"] = CsLit("Fruit", new Color(0.95f, 0.5f, 0.12f), 0.4f);
        CM["black"] = CsLit("Black", new Color(0.04f, 0.04f, 0.04f), 0.3f);
        CM["mug"] = CsLit("Mug", new Color(0.92f, 0.92f, 0.9f), 0.6f);
        CM["frame"] = CsLit("Frame", new Color(0.2f, 0.16f, 0.13f), 0.3f);
        CM["door"] = CsPoly("Door", "wood_planks_dirt", new Color(0.8f, 0.72f, 0.62f), 0.3f);
        CM["ground"] = CsPoly("CityGround", "asphalt_02", new Color(0.6f, 0.6f, 0.62f), 0.15f);
        CM["book0"] = CsLit("Book0", new Color(0.55f, 0.16f, 0.12f));
        CM["book1"] = CsLit("Book1", new Color(0.16f, 0.3f, 0.45f));
        CM["book2"] = CsLit("Book2", new Color(0.85f, 0.78f, 0.6f));
        CM["book3"] = CsLit("Book3", new Color(0.25f, 0.4f, 0.25f));
        CM["glass"] = CsGlass("WindowGlass", new Color(0.85f, 0.92f, 1f, 0.14f), 0.96f);
        CM["glassShard"] = CsParticleLit("GlassShard", new Color(0.85f, 0.93f, 1f, 0.75f), 0.95f);
        CM["shock"] = CsUnlitTransparent("Shockwave", new Color(1f, 0.9f, 0.8f, 0.25f), true);
        CM["sky"] = CsSky();
        CM["pSmoke"] = CsParticle("PSmoke", Color.white, false, soft);
        CM["pAsh"] = CsParticle("PAsh", Color.white, false, soft);
        CM["pEmber"] = CsParticle("PEmber", Color.white, true, soft);
        CM["pDust"] = CsParticle("PDust", Color.white, false, soft);
    }

    static Material CsLit(string name, Color color, float smooth = 0.15f, Texture2D albedo = null, Texture2D normal = null, float metallic = 0f)
    {
        var m = LoadOrCreate("M_Cut_" + name, Shader.Find("Universal Render Pipeline/Lit"), CsMatDir);
        m.SetTexture("_BaseMap", albedo);
        m.SetTexture("_MainTex", albedo);
        if (normal != null)
        {
            m.SetTexture("_BumpMap", normal);
            m.SetFloat("_BumpScale", 1f);
            m.EnableKeyword("_NORMALMAP");
        }
        else
        {
            m.SetTexture("_BumpMap", null);
            m.DisableKeyword("_NORMALMAP");
        }
        m.SetColor("_BaseColor", color);
        m.SetColor("_Color", color);
        m.SetFloat("_Smoothness", smooth);
        m.SetFloat("_Metallic", metallic);
        EditorUtility.SetDirty(m);
        return m;
    }

    // Poly Haven 텍스처(색·노멀)를 입힌 재질
    static Material CsPoly(string name, string tex, Color tint, float smooth)
    {
        var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/{tex}_diff_1k.jpg");
        var normal = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/{tex}_nor_gl_1k.jpg");
        return CsLit(name, tint, smooth, albedo, normal);
    }

    static Material CsUnlit(string name, Texture2D tex, Color color)
    {
        var m = LoadOrCreate("M_Cut_" + name, Shader.Find("Universal Render Pipeline/Unlit"), CsMatDir);
        m.SetTexture("_BaseMap", tex);
        m.SetTexture("_MainTex", tex);
        m.SetColor("_BaseColor", color);
        m.SetColor("_Color", color);
        m.SetFloat("_Surface", 0);
        m.SetFloat("_ZWrite", 1);
        m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = -1;
        EditorUtility.SetDirty(m);
        return m;
    }

    static void CsTransparent(Material m, bool additive)
    {
        m.SetFloat("_Surface", 1);
        m.SetFloat("_Blend", additive ? 2 : 0);
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)RenderQueue.Transparent;
    }

    // 반투명 유리 (빛을 받는다)
    static Material CsGlass(string name, Color color, float smooth)
    {
        var m = CsLit(name, color, smooth, null, null, 0.05f);
        CsTransparent(m, false);
        EditorUtility.SetDirty(m);
        return m;
    }

    // 빛을 받는 입자 (유리 조각)
    static Material CsParticleLit(string name, Color color, float smooth)
    {
        var m = LoadOrCreate("M_Cut_" + name, Shader.Find("Universal Render Pipeline/Particles/Lit"), CsMatDir);
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Smoothness", smooth);
        m.SetFloat("_Metallic", 0.05f);
        CsTransparent(m, false);
        EditorUtility.SetDirty(m);
        return m;
    }

    // 빛을 받지 않는 반투명 (충격파 구). 양면
    static Material CsUnlitTransparent(string name, Color color, bool additive)
    {
        var m = LoadOrCreate("M_Cut_" + name, Shader.Find("Universal Render Pipeline/Unlit"), CsMatDir);
        m.SetTexture("_BaseMap", null);
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Cull", 0);
        CsTransparent(m, additive);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material CsParticle(string name, Color color, bool additive, Texture2D tex)
    {
        var m = ParticleMat("M_Cut_" + name, color, additive, CsMatDir);
        m.SetTexture("_BaseMap", tex);
        m.SetTexture("_MainTex", tex);
        EditorUtility.SetDirty(m);
        return m;
    }

    // 절차적 하늘 (해·대기 산란). 색·두께·노출은 실행 중 CutsceneDirector가 장면마다 바꾼다
    static Material CsSky()
    {
        var m = LoadOrCreate("M_Cut_Sky", Shader.Find("Skybox/Procedural"), CsMatDir);
        m.SetFloat("_SunSize", 0.04f);
        m.SetFloat("_SunSizeConvergence", 5f);
        m.SetFloat("_AtmosphereThickness", 0.95f);
        m.SetColor("_SkyTint", new Color(0.5f, 0.55f, 0.62f));
        m.SetColor("_GroundColor", new Color(0.4f, 0.37f, 0.34f));
        m.SetFloat("_Exposure", 1.3f);
        EditorUtility.SetDirty(m);
        return m;
    }

    // 그림을 PNG로 저장하고 들여온다
    static Texture2D CsPng(string name, Texture2D tex, bool repeat)
    {
        string path = $"{CsTexDir}/{name}.png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        if (AssetImporter.GetAtPath(path) is TextureImporter imp)
        {
            imp.textureType = TextureImporterType.Default;
            imp.mipmapEnabled = true;
            imp.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            imp.filterMode = FilterMode.Trilinear;
            imp.anisoLevel = 4;
            imp.sRGBTexture = true;
            imp.alphaSource = TextureImporterAlphaSource.None;
            imp.maxTextureSize = 2048;
            imp.textureCompression = TextureImporterCompression.CompressedHQ;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // TV 잡음
    static Texture2D CsStaticTexture()
    {
        const int s = 256;
        var px = new Color[s * s];
        var r = new System.Random(3);
        for (int i = 0; i < px.Length; i++)
        {
            float v = (float)r.NextDouble();
            v = v * v * 1.3f;
            px[i] = new Color(v, v, v, 1);
        }
        return CutTex(s, s, px, true, true);
    }

    // 먼 건물 벽 (한 칸 = 12m: 4층 x 창 6개). 창마다 밝기가 다르고 군데군데 블라인드·때
    static Texture2D CsFacadeTexture()
    {
        const int s = 512;
        var px = new Color[s * s];
        var concrete = new Color(0.72f, 0.7f, 0.66f);
        for (int j = 0; j < s; j++)
            for (int i = 0; i < s; i++)
            {
                float u = i / (float)s, v = j / (float)s;
                float fu = u * 6f % 1f, fv = v * 4f % 1f;
                int wi = (int)(u * 6f), wj = (int)(v * 4f);
                float grime = 0.85f + 0.15f * Mathf.PerlinNoise(i * 0.05f + 3f, j * 0.05f + 7f);
                float streak = 1f - 0.12f * Mathf.Clamp01((Mathf.PerlinNoise(i * 0.12f, j * 0.01f) - 0.55f) * 4f);
                var c = concrete * grime * streak;
                // 층 사이 띠
                if (fv > 0.9f) c *= 0.82f;
                bool window = fu > 0.2f && fu < 0.8f && fv > 0.25f && fv < 0.8f;
                if (window)
                {
                    float seed = Mathf.PerlinNoise(wi * 1.37f + 0.5f, wj * 2.11f + 0.2f);
                    var glass = Color.Lerp(new Color(0.3f, 0.38f, 0.48f), new Color(0.55f, 0.6f, 0.66f), seed);
                    if (seed > 0.72f) glass = Color.Lerp(glass, new Color(0.8f, 0.78f, 0.72f), 0.7f); // 블라인드
                    // 위쪽은 하늘이 비쳐 밝다
                    glass = Color.Lerp(glass, new Color(0.72f, 0.8f, 0.9f), Mathf.Clamp01((fv - 0.55f) * 1.5f) * 0.35f);
                    bool frame = fu < 0.24f || fu > 0.76f || fv < 0.29f || fv > 0.76f || Mathf.Abs(fu - 0.5f) < 0.015f;
                    c = frame ? concrete * 0.55f : glass;
                }
                c.a = 1;
                px[j * s + i] = c;
            }
        return CutTex(s, s, px, true, true);
    }

    // 벽지: 옅은 베이지에 고운 결과 희미한 세로 줄무늬 (한 칸 = 2.5m)
    static Texture2D CsWallpaperTexture()
    {
        const int s = 256;
        var px = new Color[s * s];
        var baseC = new Color(0.86f, 0.83f, 0.77f);
        for (int j = 0; j < s; j++)
            for (int i = 0; i < s; i++)
            {
                float n = Mathf.PerlinNoise(i * 0.3f + 11f, j * 0.3f + 5f) - 0.5f;
                float stripe = ((i / 16) % 2 == 0) ? 0.012f : -0.012f;
                float grime = -0.05f * Mathf.Clamp01((Mathf.PerlinNoise(i * 0.02f, j * 0.02f) - 0.6f) * 3f);
                var c = baseC + Color.white * (n * 0.035f + stripe + grime);
                c.a = 1;
                px[j * s + i] = c;
            }
        return CutTex(s, s, px, true, true);
    }

    // 천: 촘촘한 직조 결 (한 칸 = 0.4m)
    static Texture2D CsFabricTexture()
    {
        const int s = 256;
        var px = new Color[s * s];
        for (int j = 0; j < s; j++)
            for (int i = 0; i < s; i++)
            {
                float wx = Mathf.Sin(i * Mathf.PI * 2 / 6f), wy = Mathf.Sin(j * Mathf.PI * 2 / 6f);
                float weave = 0.9f + 0.1f * (wx * wy);
                float n = 0.95f + 0.1f * Mathf.PerlinNoise(i * 0.08f + 2f, j * 0.08f + 9f);
                float v = weave * n;
                px[j * s + i] = new Color(v, v, v, 1);
            }
        return CutTex(s, s, px, true, true);
    }

    // ================= 거실 =================

    static GameObject CsBox(string name, Transform parent, Vector3 pos, Vector3 size, Material mat, Vector3? euler = null, float tile = 1f, bool isStatic = true, bool collider = true)
        => Box(name, parent, pos, size, mat, tile, collider, euler, isStatic);

    static void CsBuildApartment(Transform root, CutsceneDirector d)
    {
        var wallpaper = CM["wallpaper"];
        var trim = CM["trim"];
        var darkWood = CM["darkWood"];
        var black = CM["black"];
        var books = new[] { CM["book0"], CM["book1"], CM["book2"], CM["book3"] };
        var loose = new List<Transform>();

        // 방: 가로 7.6m(x), 세로 6.6m(z), 높이 2.7m. 북쪽 벽(z=3.2)에 TV와 큰 창
        const float W = 3.8f, S = -3.4f, N = 3.2f, H = 2.7f, T = 0.2f;
        float midZ = (S + N) / 2, depth = N - S;
        CsBox("Floor", root, new Vector3(0, -0.05f, midZ), new Vector3(2 * W + 2 * T, 0.1f, depth + 2 * T), CM["floor"], null, 1.6f);
        CsBox("Ceiling", root, new Vector3(0, H + 0.05f, midZ), new Vector3(2 * W + 2 * T, 0.1f, depth + 2 * T), CM["ceiling"]);
        Wall(root, "WallN", new Vector3(-W - T, 0, N + T / 2), 0, 2 * W + 2 * T, H, T, wallpaper, new[] { new Op(W + T + 1.7f, 3.0f, 0.55f, 2.4f) });
        Wall(root, "WallS", new Vector3(-W - T, 0, S - T / 2), 0, 2 * W + 2 * T, H, T, wallpaper, new[] { new Op(W + T + 2.2f, 0.95f, 0f, 2.1f) });
        CsBox("WallW", root, new Vector3(-W - T / 2, H / 2, midZ), new Vector3(T, H, depth), wallpaper);
        CsBox("WallE", root, new Vector3(W + T / 2, H / 2, midZ), new Vector3(T, H, depth), wallpaper);
        foreach (var (x, z, bw, bd) in new[] { (-1.9f, N - 0.01f, 4.2f, 0.02f), (-W + 0.01f, midZ, 0.02f, depth), (W - 0.01f, midZ, 0.02f, depth), (-1.7f, S + 0.01f, 4.0f, 0.02f) })
            CsBox("Baseboard", root, new Vector3(x, 0.04f, z), new Vector3(bw, 0.08f, bd), trim, null, 1f, true, false);
        CsBox("CeilingMolding", root, new Vector3(0, H - 0.03f, N - 0.03f), new Vector3(2 * W, 0.06f, 0.06f), trim, null, 1f, true, false);

        // 남쪽 벽의 현관문 (문틀·문짝·손잡이), 옆에 옷걸이
        var doorPivot = Group("FrontDoor", root, new Vector3(2.2f, 0, S - 0.02f));
        CsBox("Leaf", doorPivot, new Vector3(0, 1.05f, 0), new Vector3(0.9f, 2.1f, 0.05f), CM["door"], null, 1.2f);
        foreach (var y in new[] { 1.45f, 0.65f })
            CsBox("Panel", doorPivot, new Vector3(0, y, 0.03f), new Vector3(0.62f, 0.62f, 0.012f), CM["door"], null, 1f, true, false);
        Prim(PrimitiveType.Sphere, "Knob", doorPivot, new Vector3(0.36f, 1.0f, 0.05f), new Vector3(0.06f, 0.06f, 0.05f), CM["mug"]);
        foreach (var x in new[] { 1.65f, 2.75f })
            CsBox("DoorCasing", root, new Vector3(x, 1.08f, S + 0.0f), new Vector3(0.09f, 2.16f, 0.04f), trim, null, 1f, true, false);
        CsBox("DoorCasingTop", root, new Vector3(2.2f, 2.14f, S + 0.0f), new Vector3(1.19f, 0.09f, 0.04f), trim, null, 1f, true, false);
        CsBox("CoatRack", root, new Vector3(3.3f, 1.7f, S + 0.03f), new Vector3(0.5f, 0.06f, 0.04f), darkWood, null, 1f, true, false);
        for (int i = 0; i < 3; i++) Prim(PrimitiveType.Sphere, "Hook", root, new Vector3(3.12f + i * 0.18f, 1.66f, S + 0.07f), new Vector3(0.025f, 0.025f, 0.04f), CM["mug"]);
        CsBox("Coat", root, new Vector3(3.3f, 1.25f, S + 0.1f), new Vector3(0.36f, 0.8f, 0.1f), CM["sofa"], new Vector3(0, 0, 3), 1f, true, false);
        CsBox("WallPicture", root, new Vector3(0.4f, 1.75f, S + 0.03f), new Vector3(0.7f, 0.5f, 0.03f), CM["frame"], null, 1f, true, false);
        CsBox("WallPictureInner", root, new Vector3(0.4f, 1.75f, S + 0.046f), new Vector3(0.62f, 0.42f, 0.004f), CM["pillow"], null, 1f, true, false);

        // 큰 창 (x 0.2~3.2, 높이 0.55~2.4): 창틀, 가운데 문살, 유리판 두 장, 커튼 봉 (커튼 천은 실행 중에 만든다)
        const float wx = 1.7f;
        CsBox("Sill", root, new Vector3(wx, 0.535f, N + 0.05f), new Vector3(3.2f, 0.04f, 0.36f), trim);
        CsBox("FrameTop", root, new Vector3(wx, 2.375f, N + 0.1f), new Vector3(3.0f, 0.05f, 0.1f), trim, null, 1f, true, false);
        CsBox("FrameL", root, new Vector3(0.23f, 1.47f, N + 0.1f), new Vector3(0.06f, 1.85f, 0.1f), trim, null, 1f, true, false);
        CsBox("FrameR", root, new Vector3(3.17f, 1.47f, N + 0.1f), new Vector3(0.06f, 1.85f, 0.1f), trim, null, 1f, true, false);
        CsBox("Mullion", root, new Vector3(wx, 1.47f, N + 0.1f), new Vector3(0.06f, 1.85f, 0.08f), trim, null, 1f, true, false);
        CsBox("Transom", root, new Vector3(wx, 1.95f, N + 0.1f), new Vector3(2.94f, 0.04f, 0.07f), trim, null, 1f, true, false);
        var panes = new List<Renderer>();
        foreach (var (px, pw) in new[] { (0.965f, 1.41f), (2.435f, 1.41f) })
        {
            var pane = CsBox("GlassPane", root, new Vector3(px, 1.475f, N + 0.1f), new Vector3(pw, 1.8f, 0.012f), CM["glass"], null, 1f, false, false);
            var pr = pane.GetComponent<MeshRenderer>();
            pr.shadowCastingMode = ShadowCastingMode.Off;
            panes.Add(pr);
        }
        d.windowPanes = panes.ToArray();
        d.windowCenter = Group("WindowCenter", root, new Vector3(wx, 1.47f, N));
        d.curtainRod = Group("CurtainRod", root, new Vector3(wx, 2.58f, N - 0.1f));
        Prim(PrimitiveType.Cylinder, "Rod", d.curtainRod, Vector3.zero, new Vector3(0.025f, 2.1f, 0.025f), trim, new Vector3(0, 0, 90));
        foreach (var x in new[] { -2.05f, 2.05f })
            Prim(PrimitiveType.Sphere, "RodEnd", d.curtainRod, new Vector3(x, 0, 0), new Vector3(0.05f, 0.05f, 0.05f), darkWood);
        foreach (var x in new[] { -1.4f, 0f, 1.4f })
            CsBox("RodBracket", d.curtainRod, new Vector3(x, -0.02f, 0.06f), new Vector3(0.03f, 0.06f, 0.1f), trim, null, 1f, true, false);

        // 벽걸이 TV와 TV장
        const float tx = -1.6f;
        CsBox("TvCabinet", root, new Vector3(tx, 0.24f, N - 0.25f), new Vector3(2.3f, 0.48f, 0.46f), darkWood, null, 1.2f);
        CsBox("CabinetTop", root, new Vector3(tx, 0.495f, N - 0.25f), new Vector3(2.34f, 0.03f, 0.48f), trim);
        CsBox("CabinetDrawer", root, new Vector3(tx - 0.58f, 0.24f, N - 0.49f), new Vector3(1.05f, 0.36f, 0.02f), CM["frame"], null, 1f, true, false);
        CsBox("CabinetDrawer", root, new Vector3(tx + 0.58f, 0.24f, N - 0.49f), new Vector3(1.05f, 0.36f, 0.02f), CM["frame"], null, 1f, true, false);
        CsBox("TvBody", root, new Vector3(tx, 1.3f, N - 0.06f), new Vector3(1.78f, 1.03f, 0.06f), black, null, 1f, true, false);
        var screen = Prim(PrimitiveType.Quad, "TvScreen", root, new Vector3(tx, 1.3f, N - 0.095f), new Vector3(1.7f, 0.956f, 1f), CM["tvShow"]);
        screen.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        d.tvScreen = screen.transform;
        d.tvScreenRenderer = screen.GetComponent<MeshRenderer>();
        CsBox("Soundbar", root, new Vector3(tx, 0.55f, N - 0.2f), new Vector3(0.9f, 0.07f, 0.1f), black, null, 1f, true, false);
        Prim(PrimitiveType.Cylinder, "SmallPot", root, new Vector3(tx + 0.85f, 0.58f, N - 0.25f), new Vector3(0.14f, 0.07f, 0.14f), CM["pot"]);
        Prim(PrimitiveType.Sphere, "SmallPlant", root, new Vector3(tx + 0.85f, 0.72f, N - 0.25f), new Vector3(0.24f, 0.22f, 0.24f), CM["leaf"]);
        var photo = CsBox("PhotoFrame", root, new Vector3(tx - 0.85f, 0.6f, N - 0.2f), new Vector3(0.2f, 0.16f, 0.02f), darkWood, new Vector3(-10, 8, 0), 1f, false);
        loose.Add(photo.transform);

        // 벽시계, 서쪽 벽 책장, 스탠드 에어컨, 액자
        Prim(PrimitiveType.Cylinder, "Clock", root, new Vector3(-3.0f, 2.0f, N - 0.02f), new Vector3(0.36f, 0.015f, 0.36f), trim, new Vector3(90, 0, 0));
        CsBox("ClockHandH", root, new Vector3(-3.0f, 2.04f, N - 0.04f), new Vector3(0.012f, 0.1f, 0.005f), black, new Vector3(0, 0, -30), 1f, true, false);
        CsBox("ClockHandM", root, new Vector3(-2.95f, 1.99f, N - 0.04f), new Vector3(0.12f, 0.01f, 0.005f), black, new Vector3(0, 0, -20), 1f, true, false);
        var shelf = Group("Bookshelf", root, new Vector3(-W + 0.18f, 0, 1.9f));
        CsBox("Back", shelf, new Vector3(-0.15f, 0.95f, 0), new Vector3(0.03f, 1.9f, 1.0f), darkWood);
        CsBox("SideA", shelf, new Vector3(0, 0.95f, -0.49f), new Vector3(0.34f, 1.9f, 0.03f), darkWood);
        CsBox("SideB", shelf, new Vector3(0, 0.95f, 0.49f), new Vector3(0.34f, 1.9f, 0.03f), darkWood);
        foreach (var y in new[] { 0.05f, 0.5f, 0.95f, 1.4f, 1.88f })
            CsBox("Board", shelf, new Vector3(0, y, 0), new Vector3(0.34f, 0.03f, 0.98f), darkWood);
        foreach (var y in new[] { 0.05f, 0.5f, 0.95f, 1.4f })
            for (float z = -0.45f; z < 0.4f;)
            {
                float thick = R(0.03f, 0.07f), tall = R(0.22f, 0.36f);
                if (Chance(0.12)) { z += 0.08f; continue; }
                CsBox("Book", shelf, new Vector3(-0.02f, y + 0.015f + tall / 2, z + thick / 2), new Vector3(0.24f, tall, thick), Pick(books), null, 1f, true, false);
                z += thick + 0.004f;
            }
        // 책장 위에 얹은 책 두 권과 화병 (충격파에 떨어진다)
        var topBook = CsBox("LooseBook", shelf, new Vector3(0, 1.92f, -0.2f), new Vector3(0.2f, 0.035f, 0.27f), books[1], new Vector3(0, 8, 0), 1f, false);
        var topBook2 = CsBox("LooseBook", shelf, new Vector3(0, 1.955f, -0.18f), new Vector3(0.18f, 0.03f, 0.24f), books[3], new Vector3(0, -6, 0), 1f, false);
        var vase = Prim(PrimitiveType.Cylinder, "Vase", shelf, new Vector3(0, 2.02f, 0.25f), new Vector3(0.1f, 0.12f, 0.1f), CM["mug"]);
        loose.Add(topBook.transform); loose.Add(topBook2.transform); loose.Add(vase.transform);
        CsBox("AirCon", root, new Vector3(-3.45f, 0.875f, N - 0.27f), new Vector3(0.42f, 1.75f, 0.34f), trim);
        CsBox("AirConGrille", root, new Vector3(-3.45f, 1.4f, N - 0.445f), new Vector3(0.32f, 0.3f, 0.01f), black, null, 1f, true, false);
        CsBox("WestPicture", root, new Vector3(-W + 0.03f, 1.7f, -1.2f), new Vector3(0.03f, 0.6f, 0.8f), CM["frame"], null, 1f, true, false);
        CsBox("WestPictureInner", root, new Vector3(-W + 0.046f, 1.7f, -1.2f), new Vector3(0.004f, 0.5f, 0.7f), CM["leaf"], null, 1f, true, false);

        // 소파 (북쪽 TV를 향한다)와 앉는 자리
        const float sx = -1.25f, sz = -0.5f;
        var sofa = CM["sofa"];
        CsBox("SofaBase", root, new Vector3(sx, 0.2f, sz), new Vector3(2.3f, 0.3f, 0.95f), sofa, null, 0.4f);
        CsBox("SeatL", root, new Vector3(sx - 0.55f, 0.43f, sz + 0.05f), new Vector3(1.08f, 0.16f, 0.84f), sofa, null, 0.4f);
        CsBox("SeatR", root, new Vector3(sx + 0.55f, 0.43f, sz + 0.05f), new Vector3(1.08f, 0.16f, 0.84f), sofa, null, 0.4f);
        CsBox("SofaBack", root, new Vector3(sx, 0.68f, sz - 0.38f), new Vector3(2.3f, 0.66f, 0.2f), sofa, new Vector3(-8, 0, 0), 0.4f);
        CsBox("BackCushionL", root, new Vector3(sx - 0.55f, 0.72f, sz - 0.24f), new Vector3(1.0f, 0.5f, 0.14f), sofa, new Vector3(-8, 0, 0), 0.4f, true, false);
        CsBox("BackCushionR", root, new Vector3(sx + 0.55f, 0.72f, sz - 0.24f), new Vector3(1.0f, 0.5f, 0.14f), sofa, new Vector3(-8, 0, 0), 0.4f, true, false);
        CsBox("ArmL", root, new Vector3(sx - 1.07f, 0.47f, sz), new Vector3(0.18f, 0.54f, 0.95f), sofa, null, 0.4f);
        CsBox("ArmR", root, new Vector3(sx + 1.07f, 0.47f, sz), new Vector3(0.18f, 0.54f, 0.95f), sofa, null, 0.4f);
        foreach (var lx in new[] { sx - 1.05f, sx + 1.05f })
            foreach (var lz in new[] { sz - 0.4f, sz + 0.4f })
                CsBox("SofaLeg", root, new Vector3(lx, 0.025f, lz), new Vector3(0.05f, 0.05f, 0.05f), darkWood, null, 1f, true, false);
        CsBox("Pillow", root, new Vector3(sx - 0.8f, 0.68f, sz - 0.2f), new Vector3(0.42f, 0.38f, 0.12f), CM["pillow"], new Vector3(-15, 10, 4), 0.4f, true, false);
        CsBox("Blanket", root, new Vector3(sx - 1.07f, 0.72f, sz + 0.05f), new Vector3(0.24f, 0.06f, 0.7f), CM["rug"], new Vector3(0, 0, 6), 0.4f, true, false);
        d.sofaSpot = Group("SofaSpot", root, new Vector3(sx + 0.53f, 0.0f, sz - 0.1f), -8f);
        d.standSpot = Group("StandSpot", root, new Vector3(-0.55f, 0, 0.15f), -19.6f);

        // 거실 탁자·러그·소품 (컵·잡지·과일은 충격파에 떨어진다), 리모컨 (손에 쥔다)
        CsBox("Rug", root, new Vector3(sx, 0.005f, 0.25f), new Vector3(3.0f, 0.01f, 2.3f), CM["rug"], null, 0.4f, true, false);
        CsBox("TableTop", root, new Vector3(sx, 0.4f, 0.62f), new Vector3(1.1f, 0.04f, 0.55f), darkWood);
        foreach (var lx in new[] { sx - 0.5f, sx + 0.5f })
            foreach (var lz in new[] { 0.4f, 0.84f })
                CsBox("TableLeg", root, new Vector3(lx, 0.19f, lz), new Vector3(0.04f, 0.38f, 0.04f), darkWood, null, 1f, true, false);
        var mug = Prim(PrimitiveType.Cylinder, "Mug", root, new Vector3(sx + 0.3f, 0.47f, 0.6f), new Vector3(0.08f, 0.05f, 0.08f), CM["mug"]);
        var magazine = CsBox("Magazine", root, new Vector3(sx - 0.25f, 0.425f, 0.66f), new Vector3(0.28f, 0.01f, 0.21f), books[1], new Vector3(0, 12, 0), 1f, false);
        loose.Add(mug.transform); loose.Add(magazine.transform);
        for (int i = 0; i < 3; i++)
        {
            var fruit = Prim(PrimitiveType.Sphere, "Fruit", root, new Vector3(sx + 0.05f + i * 0.07f, 0.46f, 0.52f + (i % 2) * 0.05f), Vector3.one * 0.075f, CM["fruit"]);
            loose.Add(fruit.transform);
        }
        var remote = CsBox("Remote", root, new Vector3(sx + 0.5f, 0.1f, 0.2f), new Vector3(0.045f, 0.028f, 0.17f), black, null, 1f, false);
        d.remote = remote.transform;
        Prim(PrimitiveType.Cylinder, "LampBase", root, new Vector3(-2.85f, 0.02f, -0.95f), new Vector3(0.3f, 0.02f, 0.3f), black);
        Prim(PrimitiveType.Cylinder, "LampPole", root, new Vector3(-2.85f, 0.78f, -0.95f), new Vector3(0.03f, 0.76f, 0.03f), black);
        Prim(PrimitiveType.Cylinder, "LampShade", root, new Vector3(-2.85f, 1.6f, -0.95f), new Vector3(0.36f, 0.14f, 0.36f), CM["pillow"]);
        Prim(PrimitiveType.Cylinder, "Pot", root, new Vector3(3.35f, 0.22f, 2.55f), new Vector3(0.42f, 0.22f, 0.42f), CM["pot"]);
        for (int i = 0; i < 6; i++)
            Prim(PrimitiveType.Sphere, "Leaves", root, new Vector3(3.35f + R(-0.2f, 0.2f), R(0.65f, 1.3f), 2.55f + R(-0.18f, 0.18f)), new Vector3(R(0.3f, 0.45f), R(0.35f, 0.5f), R(0.3f, 0.45f)), CM["leaf"]);

        // 천장 등: 천장에 매달린 전등 (충격파에 흔들린다). 회전축은 천장의 고정점
        d.ceilingLamp = Group("CeilingLamp", root, new Vector3(-0.4f, H, 0));
        Prim(PrimitiveType.Cylinder, "Mount", d.ceilingLamp, new Vector3(0, -0.01f, 0), new Vector3(0.12f, 0.01f, 0.12f), trim);
        Prim(PrimitiveType.Cylinder, "Cord", d.ceilingLamp, new Vector3(0, -0.25f, 0), new Vector3(0.008f, 0.25f, 0.008f), black);
        Prim(PrimitiveType.Cylinder, "Shade", d.ceilingLamp, new Vector3(0, -0.56f, 0), new Vector3(0.42f, 0.09f, 0.42f), CM["pillow"]);
        Prim(PrimitiveType.Sphere, "Bulb", d.ceilingLamp, new Vector3(0, -0.6f, 0), new Vector3(0.07f, 0.07f, 0.07f), CM["mug"]);

        d.looseProps = loose.ToArray();

        // 빛: TV 화면 빛(그림자), 창가 반사광, 카메라 쪽 보조광
        var tvGo = new GameObject("TvGlow");
        tvGo.transform.SetParent(root, false);
        tvGo.transform.localPosition = new Vector3(tx, 1.3f, N - 0.2f);
        tvGo.transform.localRotation = Quaternion.Euler(0, 180, 0);
        d.tvLight = tvGo.AddComponent<Light>();
        d.tvLight.type = LightType.Spot;
        d.tvLight.spotAngle = 140f;
        d.tvLight.innerSpotAngle = 70f;
        d.tvLight.range = 9f;
        d.tvLight.color = new Color(0.85f, 0.9f, 1f);
        d.tvLight.intensity = 1.3f;
        d.tvLight.shadows = LightShadows.Soft;
        d.windowLight = PointLight("WindowBounce", root, new Vector3(1.7f, 1.6f, 2.4f), new Color(1f, 0.9f, 0.76f), 2.2f, 8f);
        d.roomFill = PointLight("RoomFill", root, new Vector3(0.6f, 2.2f, -2.2f), new Color(1f, 0.94f, 0.86f), 1.1f, 7f);
    }

    // ================= 창밖 =================

    static void CsBuildOutside(Transform root, CutsceneDirector d)
    {
        var outside = Group("Outside", root, Vector3.zero);
        // 땅 (20m 아래), 길 건너 건물들. 눈높이(1.4m, z -2.5)에서 본 각도로 지붕 높이를 정해 스카이라인이 고르게 보이게 한다
        var ground = CsBox("CityGround", outside, new Vector3(80, CsCityGround - 0.1f, 300), new Vector3(700, 0.2f, 700), CM["ground"], null, 8f, true, false);
        ground.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        float[] rows = { 42f, 68f, 100f, 140f, 190f, 250f, 330f };
        const float eyeY = 1.4f, eyeZ = -2.5f;
        for (int r = 0; r < rows.Length; r++)
        {
            float minAngle = r < 2 ? -2.5f : r < 4 ? -1.5f : -0.5f, maxAngle = r < 2 ? 5.5f : r < 4 ? 4f : 3f;
            for (float x = -140f; x < 320f;)
            {
                float w = R(8f, 22f), dd = R(9f, 16f), z = rows[r] + R(-5f, 5f);
                float ang = Chance(0.1) ? R(7f, 10f) : R(minAngle, maxAngle);
                float top = eyeY + Mathf.Tan(ang * Mathf.Deg2Rad) * (z - eyeZ);
                var b = CsBox("Block", outside, new Vector3(x + w / 2, (top + CsCityGround) / 2, z), new Vector3(w, top - CsCityGround, dd), CM["facade"], null, 12f, true, false);
                b.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                // 옥상 구조물
                if (Chance(0.5)) CsBox("Rooftop", outside, new Vector3(x + w / 2 + R(-w * 0.3f, w * 0.3f), top + 1.2f, z), new Vector3(R(2f, 4f), 2.4f, R(2f, 4f)), M["dirty"], null, 2f, true, false);
                x += w + R(2f, 8f);
            }
        }
        // 폭심 (거실 창에서 북동쪽 약 390m): 버섯구름 발치
        d.cloudHome = Group("CloudHome", outside, new Vector3(240f, CsCityGround, 300f));
    }

    // ================= 방공호 앞 거리 =================

    static void CsBuildStreet(Transform root, CutsceneDirector d)
    {
        // 메인 화면과 같은 터널 입구 (빗물 줄기만 뺀다)
        BuildMenuTunnel(root);
        var drip = root.Find("ShelterEntrance/Drip");
        if (drip != null) Object.DestroyImmediate(drip.gameObject);

        // 땅·인도·차선
        CsBox("Ground", root, new Vector3(0, -0.1f, 40), new Vector3(220, 0.2f, 260), M["asphalt"], null, 6f);
        CsBox("SidewalkL", root, new Vector3(-10, 0.075f, 48), new Vector3(6, 0.15f, 110), M["floor"], null, 3f);
        CsBox("SidewalkR", root, new Vector3(10, 0.075f, 48), new Vector3(6, 0.15f, 110), M["floor"], null, 3f);
        CsBox("Plaza", root, new Vector3(0, 0.03f, 3.5f), new Vector3(14f, 0.06f, 7f), M["floor"], null, 2.5f);
        var lines = new List<CombineInstance>();
        var dash = BoxMesh(new Vector3(0.15f, 0.02f, 2.4f), 1f);
        for (float z = 8; z < 100; z += 6)
            lines.Add(new CombineInstance { mesh = dash, transform = Matrix4x4.TRS(new Vector3(0, 0.005f, z), Quaternion.identity, Vector3.one) });
        CombineInto(root, "RoadLines", lines, M["line"]);

        // 터널을 감싼 두꺼운 콘크리트 벙커 (앞면 z=0.3, 안쪽으로 14m)
        CsBox("BunkerL", root, new Vector3(-5.1f, 2.8f, -6.75f), new Vector3(4.5f, 5.6f, 14.1f), M["dirty"], null, 3f);
        CsBox("BunkerR", root, new Vector3(5.1f, 2.8f, -6.75f), new Vector3(4.5f, 5.6f, 14.1f), M["dirty"], null, 3f);
        CsBox("BunkerTop", root, new Vector3(0, 4.78f, -6.75f), new Vector3(5.8f, 1.65f, 14.1f), M["dirty"], null, 3f);
        CsBox("BunkerLedge", root, new Vector3(0, 5.72f, -6.75f), new Vector3(15f, 0.25f, 14.4f), M["cracked"], null, 3f);
        // 표지판 '민방위 대피소'와 경광등 (머리가 돌며 붉은 빛을 뿌린다)
        var sign = Prim(PrimitiveType.Quad, "ShelterSign", root, new Vector3(0, 4.85f, 0.32f), new Vector3(2.4f, 0.75f, 1f), CM["sign"], new Vector3(0, 180, 0));
        sign.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        CsBox("SignArrow", root, new Vector3(-7.5f, 2.6f, 0.33f), new Vector3(1.6f, 0.5f, 0.02f), CM["sign"], null, 1f, true, false);
        CsBox("BeaconMount", root, new Vector3(3.4f, 4.3f, 0.45f), new Vector3(0.24f, 0.12f, 0.3f), M["metal"], null, 1f, true, false);
        CsBox("BeaconLamp", root, new Vector3(3.4f, 4.5f, 0.45f), new Vector3(0.2f, 0.26f, 0.2f), M["redLamp"], null, 1f, true, false);
        d.beaconHead = Group("BeaconHead", root, new Vector3(3.4f, 4.5f, 0.45f));
        var beaconGo = new GameObject("BeaconLight");
        beaconGo.transform.SetParent(d.beaconHead, false);
        beaconGo.transform.localRotation = Quaternion.Euler(12, 0, 0);
        d.beaconLight = beaconGo.AddComponent<Light>();
        d.beaconLight.type = LightType.Spot;
        d.beaconLight.spotAngle = 55f;
        d.beaconLight.innerSpotAngle = 20f;
        d.beaconLight.range = 38f;
        d.beaconLight.color = new Color(1f, 0.12f, 0.07f);
        d.beaconLight.intensity = 16f;
        d.beaconLight.shadows = LightShadows.None;
        PointLight("BeaconGlow", root, new Vector3(3.4f, 4.5f, 0.9f), new Color(1f, 0.15f, 0.08f), 4f, 6f);

        // 길 양옆 건물 (메인 화면과 같은 식)
        foreach (var side in new[] { -1f, 1f })
            for (float z = 2; z < 110;)
            {
                float dd = R(8f, 13f), w = R(8f, 12f), h = R(9f, 26f);
                float x0 = side < 0 ? -13 - w : 13;
                var g = Group("Building", root, new Vector3(0, 0.15f, 0));
                CsBox("Mass", g, new Vector3(x0 + w / 2, h / 2, z + dd / 2), new Vector3(w, h, dd), Pick(M["wall"], M["brick"], M["cracked"]), null, 3f);
                FacadeWindows(g, x0, z, w, dd, 3.5f, Mathf.FloorToInt((h - 3.5f) / 3.2f));
                if (Chance(0.6)) CsBox("Shutter", g, new Vector3(side < 0 ? x0 + w + 0.06f : x0 - 0.06f, 1.4f, z + dd / 2), new Vector3(0.1f, 2.6f, Mathf.Min(5, dd - 2)), M["metal"], null, 1.5f);
                z += dd + R(0.5f, 3f);
            }
        // 먼 끝을 막는 큰 건물들
        for (float x = -60; x < 60; x += 18)
            CsBox("FarBlock", root, new Vector3(x + 9, 14, 130 + R(0, 10)), new Vector3(16, R(20, 36), 16), Pick(M["wall"], M["cracked"]), null, 3f, true, false);

        // 버려진 차·잔해·가로등·죽은 나무
        WreckedCar(root, new Vector3(-3.6f, 0, 18f), 14, false);
        WreckedCar(root, new Vector3(4.4f, 0, 36f), -62, true);
        WreckedCar(root, new Vector3(-6.5f, 0, 56f), 28, true);
        WreckedCar(root, new Vector3(6.8f, 0, 80f), 170, false);
        RubblePile(root, new Vector3(6.5f, 0, 10f), 9, 1.4f);
        RubblePile(root, new Vector3(-7f, 0, 28f), 10, 1.5f);
        RubblePile(root, new Vector3(2f, 0, 66f), 8, 1.6f);
        for (int k = 0; k < 3; k++)
            CsBox("Barrier", root, new Vector3(-6f + k * 2.1f, 0.45f, 14f), new Vector3(2f, 0.9f, 0.6f), M["dirty"], new Vector3(0, R(-10, 10), k == 1 ? 80 : 0), 2f);
        LampPost(root, new Vector3(-8.5f, 0.15f, 12));
        LampPost(root, new Vector3(8.5f, 0.15f, 30));
        LampPost(root, new Vector3(-8.5f, 0.15f, 52));
        DeadTree(root, new Vector3(9f, 0.15f, 6));
        DeadTree(root, new Vector3(-9f, 0.15f, 40));
        // 길에 흩어진 종이·유리 (작은 판들)
        for (int i = 0; i < 26; i++)
            CsBox("Litter", root, new Vector3(R(-9f, 9f), 0.006f, R(2f, 60f)), new Vector3(R(0.15f, 0.3f), 0.004f, R(0.2f, 0.32f)), Pick(CM["book2"], CM["mug"], CM["trim"]), new Vector3(0, R(0, 360), 0), 1f, true, false);

        // 불타는 차 세 대의 불빛 (연기·불꽃은 실행 중에), 먼 비상등 둘 (파랑·빨강 깜빡임)
        var fires = new List<Light>();
        foreach (var (x, z) in new[] { (-6.5f, 56f), (6.8f, 80f), (-5f, 104f) })
        {
            var l = PointLight("FireLight", root, new Vector3(x, 1.4f, z), new Color(1f, 0.5f, 0.15f), 10f, 30f);
            fires.Add(l);
        }
        d.fireLights = fires.ToArray();
        d.distantLights = new[]
        {
            PointLight("DistantBlue", root, new Vector3(-9f, 2.5f, 72f), new Color(0.3f, 0.5f, 1f), 0f, 40f),
            PointLight("DistantRed", root, new Vector3(9f, 2.5f, 96f), new Color(1f, 0.15f, 0.1f), 0f, 40f),
        };

        // 달리는 길: 거리 저쪽 → 문 앞 → 터널 안. 버섯구름은 멀리 뒤쪽 하늘에
        d.runStart = Group("RunStart", root, new Vector3(9f, 0, 22f));
        d.runDoor = Group("RunDoor", root, new Vector3(0.45f, 0, 1.4f));
        d.runInside = Group("RunInside", root, new Vector3(0.3f, 0, -7.5f));
        d.cloudStreet = Group("CloudStreet", root, new Vector3(120f, 0, 480f));
    }

    // ================= 빛·카메라·후처리 =================

    static void CsBuildLightsAndCamera(Transform root, CutsceneDirector d)
    {
        d.sun = new GameObject("Sun").AddComponent<Light>();
        d.sun.transform.SetParent(root, false);
        d.sun.type = LightType.Directional;
        d.sun.color = new Color(1f, 0.9f, 0.76f);
        d.sun.intensity = 2.4f;
        d.sun.shadows = LightShadows.Soft;
        d.sun.shadowStrength = 1f;
        d.sun.transform.rotation = Quaternion.Euler(26, 200, 0);
        RenderSettings.sun = d.sun;

        d.flashLight = new GameObject("BlastLight").AddComponent<Light>();
        d.flashLight.transform.SetParent(root, false);
        d.flashLight.type = LightType.Directional;
        d.flashLight.color = new Color(1f, 0.95f, 0.85f);
        d.flashLight.intensity = 0f;
        d.flashLight.shadows = LightShadows.None;

        RenderSettings.skybox = CM["sky"];
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.55f, 0.62f, 0.72f);
        RenderSettings.ambientEquatorColor = new Color(0.5f, 0.48f, 0.45f);
        RenderSettings.ambientGroundColor = new Color(0.3f, 0.27f, 0.24f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.0022f;
        RenderSettings.fogColor = new Color(0.72f, 0.78f, 0.88f);
        RenderSettings.reflectionIntensity = 0.3f;

        var camGo = new GameObject("CutsceneCamera");
        camGo.tag = "MainCamera";
        camGo.transform.SetParent(root, false);
        camGo.transform.position = new Vector3(2.0f, 1.45f, -3.0f);
        camGo.transform.rotation = Quaternion.LookRotation(new Vector3(-0.7f, 1.15f, 2.0f) - camGo.transform.position);
        d.cam = camGo.AddComponent<Camera>();
        d.cam.clearFlags = CameraClearFlags.Skybox;
        d.cam.nearClipPlane = 0.05f;
        d.cam.farClipPlane = 1500f;
        d.cam.fieldOfView = 44f;
        d.cam.allowHDR = true;
        d.cam.allowMSAA = false;
        var data = d.cam.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = true;
        data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        data.antialiasingQuality = AntialiasingQuality.High;
        data.renderShadows = true;
        data.dithering = true;
        camGo.AddComponent<AudioListener>();

        var volumeGo = new GameObject("PostProcessVolume");
        volumeGo.transform.SetParent(root, false);
        d.volume = volumeGo.AddComponent<Volume>();
        d.volume.isGlobal = true;
        d.volume.sharedProfile = CsMakeProfile();
    }

    static VolumeProfile CsMakeProfile()
    {
        string path = $"{SettingsDir}/CutsceneProfile.asset";
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
        ca.postExposure.Override(0f);
        ca.contrast.Override(10f);
        ca.saturation.Override(0f);
        ca.colorFilter.Override(Color.white);
        var bl = Add<Bloom>();
        bl.intensity.Override(0.9f);
        bl.threshold.Override(0.9f);
        bl.scatter.Override(0.65f);
        bl.highQualityFiltering.Override(true);
        var vg = Add<Vignette>();
        vg.intensity.Override(0.35f);
        vg.smoothness.Override(0.5f);
        var fg = Add<FilmGrain>();
        fg.type.Override(FilmGrainLookup.Medium3);
        fg.intensity.Override(0.3f);
        fg.response.Override(0.8f);
        Add<ChromaticAberration>().intensity.Override(0.1f);
        var dof = Add<DepthOfField>();
        dof.mode.Override(DepthOfFieldMode.Bokeh);
        dof.focusDistance.Override(3f);
        dof.aperture.Override(5.6f);
        dof.focalLength.Override(50f);
        dof.bladeCount.Override(6);
        var mb = Add<MotionBlur>();
        mb.intensity.Override(0.25f);
        mb.quality.Override(MotionBlurQuality.Medium);
        EditorUtility.SetDirty(p);
        return p;
    }
}
