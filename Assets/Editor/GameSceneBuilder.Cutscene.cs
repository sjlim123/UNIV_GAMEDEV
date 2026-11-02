using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// 메뉴 Tools > 핵폭발 생존 > 컷씬 이미지 생성
// 새 게임 도입 컷씬 사진 3장(Assets/Resources/Cutscene/cut1~3.png, 1920x1080)을 임시 씬에 만든 모형을 찍어서 만든다.
//  1: 맑은 오후, 창가 거실에서 소파에 앉아 TV를 보는 주인공 (뒷모습)
//  2: TV의 핵폭발 속보와 창밖 섬광에 놀라 일어선 주인공 (입을 막은 채 굳어 있다)
//  3: 붉게 물든 하늘과 버섯구름을 뒤로하고 방공호 철문으로 뛰어드는 주인공
// 임시 씬은 저장하지 않고, 다 찍으면 MainMenu 씬을 다시 연다.
public static partial class GameSceneBuilder
{
    const string CutDir = "Assets/Resources/Cutscene";
    const int CutW = 1920, CutH = 1080, CutSuper = 2;                    // 2배 크기로 찍은 뒤 줄여서 계단 현상을 없앤다
    static readonly Vector3 CutShelterOrigin = new Vector3(0, 0, -300);  // 방공호 장면은 거실과 멀리 떨어뜨려 만든다

    // 몸 치수 (넓적다리·정강이·위팔·아래팔 길이)
    const float CutThigh = 0.43f, CutShin = 0.43f, CutUpperArm = 0.29f, CutForearm = 0.26f;

    // TV·표지판에 그리는 글자
    const string CutTxtBreaking = "속보";
    const string CutTxtHeadline = "서울 상공 핵폭발 발생";
    const string CutTxtTicker = "정부, 전 국민 대피령 발령  ·  즉시 가까운 대피소로 대피하십시오  ·  창문에서 멀리 떨어지십시오";
    const string CutTxtNewsTag = "뉴스 특보";
    const string CutTxtLive = "LIVE";
    const string CutTxtShow = "주말 나들이  ·  남해 바다 특집";
    const string CutTxtSign = "민방위 대피소";
    const string CutTxtSignSub = "CIVIL DEFENSE SHELTER";
    static readonly int[] CutTextSizes = { 26, 30, 46, 56, 64 };

    // 관절로 자세를 잡는 사람 모형
    class CutRig
    {
        public Transform Root, Hips, Spine, Head, ArmL, ArmR, ForeL, ForeR, LegL, LegR, ShinL, ShinR;
    }

    static readonly List<Object> cutTemp = new List<Object>(); // 다 찍고 지울 임시 재질·텍스처
    static Font cutFont;
    static Texture2D cutAtlas;
    static bool cutAtlasRed;

    // 장면마다 바꾸는 것들
    static Transform cutHome, cutShelter;
    static CutRig cutSitting, cutStanding, cutRunner;
    static GameObject cutDroppedRemote;
    static Light cutSun, cutTvLight, cutWindowLight, cutBackLight;
    static Material cutSkyMat, cutTvMat, cutCityNear, cutCityMid, cutCityFar;
    static Texture2D cutSkyClear, cutSkyFlash, cutTvShow, cutTvNews;
    static VolumeProfile cutProfile;

    [MenuItem("Tools/핵폭발 생존/컷씬 이미지 생성")]
    public static void BuildCutsceneImages()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[GameSceneBuilder] 플레이 모드에서는 컷씬 이미지를 만들 수 없습니다.");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EnsureFolder("Assets", "Generated");
        EnsureFolder("Assets", "Resources");
        EnsureFolder("Assets/Resources", "Cutscene");
        ConfigurePipeline();
        CreateMaterials();

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        BeginMeshes(GenDir + "/CutsceneMeshes.asset");
        rng = new System.Random(31);
        cutTemp.Clear();
        bool ok = false;
        try
        {
            EditorUtility.DisplayProgressBar("컷씬 이미지 생성", "그림과 모형 만드는 중...", 0.1f);
            CutPrepareFont();
            var cam = CutBuildWorld();
            for (int shot = 0; shot < 3; shot++)
            {
                EditorUtility.DisplayProgressBar("컷씬 이미지 생성", $"{shot + 1}번 사진 찍는 중...", 0.3f + shot * 0.22f);
                CutSetupShot(shot, cam);
                // 에디터의 첫 렌더에는 방금 만든 메시가 빠질 수 있어 한 번 미리 찍어 둔다
                if (shot == 0) CutRender(cam, $"{CutDir}/cut{shot + 1}.png");
                CutRender(cam, $"{CutDir}/cut{shot + 1}.png");
            }
            ok = true;
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            string menu = SceneDir + "/MainMenu.unity";
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(menu) != null) EditorSceneManager.OpenScene(menu);
            else EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            foreach (var o in cutTemp)
                if (o != null) Object.DestroyImmediate(o);
            cutTemp.Clear();
            cutFont = null;
            cutAtlas = null;
            cutHome = cutShelter = null;
            cutSitting = cutStanding = cutRunner = null;
            cutDroppedRemote = null;
            cutSun = cutTvLight = cutWindowLight = cutBackLight = null;
            cutSkyMat = cutTvMat = cutCityNear = cutCityMid = cutCityFar = null;
            cutSkyClear = cutSkyFlash = cutTvShow = cutTvNews = null;
            cutProfile = null;
            AssetDatabase.DeleteAsset(GenDir + "/CutsceneMeshes.asset");
        }
        if (!ok) return;
        CutImportImages();
        Debug.Log("[GameSceneBuilder] 컷씬 이미지 3장 생성 완료: " + CutDir);
    }

    // 게임에서 Resources.Load로 쓰기 좋게: 밉맵 없음, Clamp, 고품질 압축, 크기 그대로(NPOT)
    static void CutImportImages()
    {
        AssetDatabase.Refresh();
        for (int i = 1; i <= 3; i++)
        {
            string path = $"{CutDir}/cut{i}.png";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            if (!(AssetImporter.GetAtPath(path) is TextureImporter imp)) continue;
            imp.textureType = TextureImporterType.Default;
            imp.mipmapEnabled = false;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.filterMode = FilterMode.Bilinear;
            imp.npotScale = TextureImporterNPOTScale.None;
            imp.sRGBTexture = true;
            imp.alphaSource = TextureImporterAlphaSource.None;
            imp.maxTextureSize = 2048;
            imp.textureCompression = TextureImporterCompression.CompressedHQ;
            imp.SaveAndReimport();
        }
    }

    // ================= 장면 만들기 =================

    static Camera CutBuildWorld()
    {
        // 그림 (하늘·TV 화면·건물 벽)
        cutSkyClear = CutSkyTexture(0);
        cutSkyFlash = CutSkyTexture(1);
        cutTvShow = CutTvShowTexture();
        cutTvNews = CutTvNewsTexture();
        var facade = CutFacadeTexture();
        cutSkyMat = CutUnlit(cutSkyClear, Color.white);
        cutTvMat = CutUnlit(cutTvShow, Color.white);
        cutCityNear = CutUnlit(facade, Color.white);
        cutCityMid = CutUnlit(facade, Color.white);
        cutCityFar = CutUnlit(facade, Color.white);

        // 평범한 옷차림 (밝은 티셔츠, 청바지, 흰 운동화)
        var shirt = CutLit(new Color(0.82f, 0.81f, 0.78f), 0.1f);
        var jeans = CutLit(new Color(0.2f, 0.27f, 0.4f), 0.15f);
        var hair = CutLit(new Color(0.06f, 0.05f, 0.045f), 0.35f);
        var shoes = CutLit(new Color(0.86f, 0.85f, 0.83f), 0.2f);
        var bag = CutLit(new Color(0.2f, 0.25f, 0.18f), 0.1f);
        var skin = M["skin"];

        // ----- 거실 (1·2번) -----
        cutHome = new GameObject("Home").transform;
        CutBuildApartment(cutHome);
        CutCity(cutHome, new[] { 45f, 75f, 110f, 150f, 195f }, -70f, 240f, 1.4f, -2.5f, -5.5f, 0.5f, 3f);
        var sky = Prim(PrimitiveType.Quad, "Sky", cutHome, new Vector3(80, 10, 220), new Vector3(240, 120, 1), cutSkyMat);
        sky.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

        // 1번: 소파에 기대 앉아 왼팔은 등받이에 걸치고, 오른손엔 리모컨
        cutSitting = CutPerson(cutHome, "Sitting", new Vector3(-0.72f, 0, -0.62f), -6, shirt, jeans, skin, hair, shoes);
        CutPose(cutSitting, 0.56f, new Vector3(-16, 0, 0), new Vector3(11, -4, 0),
            new Vector3(-0.16f, 0.04f, 0.6f), new Vector3(-0.25f, 1f, 1f),
            new Vector3(0.14f, 0.04f, 0.66f), new Vector3(0.25f, 1f, 1f),
            new Vector3(-0.78f, 1.0f, -0.24f), new Vector3(-0.5f, 1.4f, 0.1f),
            new Vector3(0.2f, 0.68f, 0.24f), new Vector3(0.45f, 0.75f, -0.2f));
        Prim(PrimitiveType.Cube, "Remote", cutSitting.ForeR, new Vector3(0, -CutForearm - 0.05f, 0.05f), new Vector3(0.045f, 0.028f, 0.17f), M["tire"]);

        // 2번: TV 쪽을 보며 굳어 선 채 왼손으로 입을 막는다. 리모컨은 바닥에 떨어져 있다
        var standPos = new Vector3(0.75f, 0, 1.25f);
        float standYaw = Mathf.Atan2(-1.6f - standPos.x, 3.1f - standPos.z) * Mathf.Rad2Deg;
        cutStanding = CutPerson(cutHome, "Standing", standPos, standYaw, shirt, jeans, skin, hair, shoes);
        CutPose(cutStanding, 0.92f, new Vector3(-8, 0, 0), new Vector3(14, 0, 0),
            new Vector3(-0.13f, 0.04f, 0.06f), new Vector3(-0.13f, 0.5f, 1f),
            new Vector3(0.15f, 0.04f, -0.2f), new Vector3(0.15f, 0.5f, 1f),
            new Vector3(-0.06f, 1.5f, 0.1f), new Vector3(-0.3f, 1.1f, 0.25f),
            new Vector3(0.33f, 0.9f, 0.02f), new Vector3(0.4f, 1.2f, -0.3f));
        cutDroppedRemote = CutBox("DroppedRemote", cutHome, standPos + new Vector3(0.42f, 0.014f, -0.05f), new Vector3(0.045f, 0.028f, 0.17f), M["tire"], new Vector3(0, 35, 0));

        // TV 화면 빛 (사람에게 그림자를 드리운다), 창가 반사광, 카메라 쪽 보조광
        var tvGo = new GameObject("TvGlow");
        tvGo.transform.SetParent(cutHome, false);
        tvGo.transform.localPosition = new Vector3(-1.6f, 1.3f, 2.95f);
        tvGo.transform.localRotation = Quaternion.Euler(0, 180, 0);
        cutTvLight = tvGo.AddComponent<Light>();
        cutTvLight.type = LightType.Spot;
        cutTvLight.spotAngle = 140f;
        cutTvLight.innerSpotAngle = 70f;
        cutTvLight.range = 9f;
        cutTvLight.shadows = LightShadows.Soft;
        cutWindowLight = PointLight("WindowBounce", cutHome, new Vector3(1.7f, 1.5f, 2.5f), Color.white, 1f, 8f);
        cutBackLight = PointLight("BackFill", cutHome, new Vector3(0.6f, 2.2f, -2.2f), Color.white, 1f, 6f);

        // ----- 방공호 입구 (3번) -----
        cutShelter = new GameObject("ShelterEntrance").transform;
        cutShelter.position = CutShelterOrigin;
        CutBuildShelter(cutShelter);
        var runPos = new Vector3(-0.5f, 0, 4.6f);
        var door = new Vector3(-3.8f, 0, 7.0f);
        float runYaw = Mathf.Atan2(door.x - runPos.x, door.z - runPos.z) * Mathf.Rad2Deg;
        cutRunner = CutPerson(cutShelter, "Runner", runPos, runYaw, shirt, jeans, skin, hair, shoes);
        Prim(PrimitiveType.Cube, "Backpack", cutRunner.Spine, new Vector3(0, 0.27f, -0.18f), new Vector3(0.3f, 0.38f, 0.14f), bag);
        // 몸을 앞으로 숙이고, 왼다리를 앞으로 차올리며 오른팔을 앞으로 흔든다
        CutPose(cutRunner, 0.9f, new Vector3(14, -6, 0), new Vector3(-10, 4, 0),
            new Vector3(-0.1f, 0.38f, 0.4f), new Vector3(-0.1f, 0.9f, 1.3f),
            new Vector3(0.12f, 0.3f, -0.52f), new Vector3(0.12f, 0.5f, 0.6f),
            new Vector3(-0.27f, 1.02f, -0.22f), new Vector3(-0.35f, 1.3f, -0.5f),
            new Vector3(0.14f, 1.22f, 0.42f), new Vector3(0.35f, 0.9f, 0.1f));

        // ----- 공용: 해(섬광), 후처리, 카메라 -----
        cutSun = new GameObject("Sun").AddComponent<Light>();
        cutSun.type = LightType.Directional;
        cutSun.shadows = LightShadows.Soft;
        cutSun.shadowStrength = 1f;
        RenderSettings.sun = cutSun;
        RenderSettings.skybox = null;
        RenderSettings.fog = false;
        RenderSettings.reflectionIntensity = 0.2f;

        cutProfile = CutMakeProfile();
        var volume = new GameObject("PostProcessVolume").AddComponent<Volume>();
        volume.isGlobal = true;
        volume.sharedProfile = cutProfile;

        var cam = new GameObject("CutsceneCamera").AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 600f;
        cam.allowHDR = true;
        cam.allowMSAA = false;
        var data = cam.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = true;
        data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        data.antialiasingQuality = AntialiasingQuality.High;
        data.renderShadows = true;
        data.dithering = true; // 하늘 그라데이션 띠 줄이기
        return cam;
    }

    static void CutSetupShot(int shot, Camera cam)
    {
        bool home = shot < 2;
        cutHome.gameObject.SetActive(home);
        cutShelter.gameObject.SetActive(!home);
        cutSitting.Root.gameObject.SetActive(shot == 0);
        cutStanding.Root.gameObject.SetActive(shot == 1);
        cutDroppedRemote.SetActive(shot == 1);

        if (shot == 0)
        {
            // 맑은 오후: 낮게 들어오는 따뜻한 햇빛, 밝은 하늘색 그늘
            cutSkyMat.SetTexture("_BaseMap", cutSkyClear);
            cutSkyMat.SetColor("_BaseColor", new Color(1.1f, 1.1f, 1.1f));
            cutTvMat.SetTexture("_BaseMap", cutTvShow);
            cutTvMat.SetColor("_BaseColor", new Color(1.05f, 1.05f, 1.05f));
            CutCityTint(new Color(0.8f, 0.83f, 0.88f), new Color(0.88f, 0.91f, 0.96f), new Color(0.95f, 0.97f, 1f));
            CutSunLight(new Vector3(18, 205, 0), new Color(1f, 0.9f, 0.76f), 2.4f);
            CutSetLight(cutTvLight, new Color(0.85f, 0.9f, 1f), 0.8f);
            CutSetLight(cutWindowLight, new Color(1f, 0.9f, 0.76f), 2.5f);
            CutSetLight(cutBackLight, new Color(1f, 0.93f, 0.85f), 1.4f);
            CutAmbient(new Color(0.4f, 0.42f, 0.46f));
            CutGrade(0.35f, 6f, 8f, new Color(1f, 0.98f, 0.94f), 0.5f, 0.3f, 0.15f, 9f);
            cam.backgroundColor = new Color(0.75f, 0.84f, 0.93f);
            CutCamera(cam, new Vector3(0.75f, 1.55f, -2.9f), new Vector3(-0.3f, 1.0f, 3.0f), 50f, 0f);
        }
        else if (shot == 1)
        {
            // 속보: 창밖 지평선에서 섬광과 버섯구름, 방 안은 TV의 차가운 빛과 창으로 들어오는 주황빛
            cutSkyMat.SetTexture("_BaseMap", cutSkyFlash);
            cutSkyMat.SetColor("_BaseColor", new Color(1.3f, 1.3f, 1.3f));
            cutTvMat.SetTexture("_BaseMap", cutTvNews);
            cutTvMat.SetColor("_BaseColor", new Color(1.35f, 1.35f, 1.35f));
            CutCityTint(new Color(0.16f, 0.11f, 0.12f), new Color(0.3f, 0.19f, 0.17f), new Color(0.52f, 0.32f, 0.24f));
            CutSunLight(new Vector3(9, 209, 0), new Color(1f, 0.6f, 0.3f), 3.2f);
            CutSetLight(cutTvLight, new Color(0.72f, 0.84f, 1f), 6f);
            CutSetLight(cutWindowLight, new Color(1f, 0.55f, 0.25f), 3f);
            CutSetLight(cutBackLight, new Color(0.5f, 0.6f, 0.9f), 0.3f);
            CutAmbient(new Color(0.1f, 0.11f, 0.15f));
            CutGrade(0.1f, 16f, -6f, new Color(1f, 0.96f, 0.96f), 1.3f, 0.42f, 0.28f, 7.5f);
            cam.backgroundColor = new Color(0.6f, 0.35f, 0.25f);
            CutCamera(cam, new Vector3(0.35f, 1.2f, -1.6f), new Vector3(0.7f, 1.4f, 3.0f), 48f, 0f);
        }
        else
        {
            // 방공호: 버섯구름 쪽에서 낮게 비치는 주황빛, 철문 안쪽과 경광등의 붉은 빛
            CutCityTint(new Color(0.1f, 0.06f, 0.06f), new Color(0.2f, 0.1f, 0.08f), new Color(0.38f, 0.18f, 0.12f));
            CutSunLight(new Vector3(12, 197, 0), new Color(1f, 0.5f, 0.24f), 2.2f);
            CutAmbient(new Color(0.13f, 0.065f, 0.06f));
            CutGrade(0.35f, 20f, -4f, new Color(1f, 0.93f, 0.9f), 1.2f, 0.5f, 0.35f, 30f);
            cam.backgroundColor = new Color(0.3f, 0.1f, 0.06f);
            var o = CutShelterOrigin;
            CutCamera(cam, o + new Vector3(0, 1.0f, -3f), o + new Vector3(-0.6f, 2.0f, 10f), 42f, -3f);
        }
    }

    // ---------- 거실 ----------

    static void CutBuildApartment(Transform root)
    {
        var wallpaper = CutLit(new Color(0.86f, 0.83f, 0.77f), 0.1f);
        var ceiling = CutLit(new Color(0.9f, 0.89f, 0.86f), 0.05f);
        var floor = CutClone(M["wood"], new Color(0.95f, 0.78f, 0.6f), 0.45f);
        var trim = CutLit(new Color(0.94f, 0.93f, 0.91f), 0.35f);
        var sofa = CutLit(new Color(0.24f, 0.34f, 0.37f), 0.12f);
        var pillow = CutLit(new Color(0.8f, 0.62f, 0.36f), 0.1f);
        var darkWood = CutClone(M["wood"], new Color(0.45f, 0.32f, 0.24f), 0.4f);
        var rug = CutLit(new Color(0.72f, 0.56f, 0.44f), 0.05f);
        var curtain = CutLit(new Color(0.93f, 0.89f, 0.8f), 0.05f);
        var leaf = CutLit(new Color(0.2f, 0.4f, 0.18f), 0.25f);
        var pot = CutLit(new Color(0.74f, 0.44f, 0.3f), 0.2f);
        var fruit = CutLit(new Color(0.95f, 0.5f, 0.12f), 0.4f);
        var black = M["tire"];
        var books = new[]
        {
            CutLit(new Color(0.55f, 0.16f, 0.12f)), CutLit(new Color(0.16f, 0.3f, 0.45f)),
            CutLit(new Color(0.85f, 0.78f, 0.6f)), CutLit(new Color(0.25f, 0.4f, 0.25f)),
        };

        // 방: 가로 7.6m(x), 세로 6.6m(z), 높이 2.7m. 북쪽 벽(z=3.2)에 TV와 큰 창
        const float W = 3.8f, S = -3.4f, N = 3.2f, H = 2.7f, T = 0.2f;
        float midZ = (S + N) / 2, depth = N - S;
        CutBox("Floor", root, new Vector3(0, -0.05f, midZ), new Vector3(2 * W + 2 * T, 0.1f, depth + 2 * T), floor, null, 1.6f);
        CutBox("Ceiling", root, new Vector3(0, H + 0.05f, midZ), new Vector3(2 * W + 2 * T, 0.1f, depth + 2 * T), ceiling);
        Wall(root, "WallN", new Vector3(-W - T, 0, N + T / 2), 0, 2 * W + 2 * T, H, T, wallpaper, new[] { new Op(W + T + 1.7f, 3.0f, 0.55f, 2.4f) });
        CutBox("WallS", root, new Vector3(0, H / 2, S - T / 2), new Vector3(2 * W + 2 * T, H, T), wallpaper);
        CutBox("WallW", root, new Vector3(-W - T / 2, H / 2, midZ), new Vector3(T, H, depth), wallpaper);
        CutBox("WallE", root, new Vector3(W + T / 2, H / 2, midZ), new Vector3(T, H, depth), wallpaper);
        CutBox("Baseboard", root, new Vector3(-1.9f, 0.04f, N - 0.01f), new Vector3(4.2f, 0.08f, 0.02f), trim);
        CutBox("BaseboardW", root, new Vector3(-W + 0.01f, 0.04f, midZ), new Vector3(0.02f, 0.08f, depth), trim);

        // 큰 창 (x 0.2~3.2, 높이 0.55~2.4), 창틀과 가운데 문살, 양옆으로 걷은 커튼
        const float wx = 1.7f;
        CutBox("Sill", root, new Vector3(wx, 0.535f, N + 0.05f), new Vector3(3.2f, 0.04f, 0.36f), trim);
        CutBox("FrameTop", root, new Vector3(wx, 2.375f, N + 0.1f), new Vector3(3.0f, 0.05f, 0.1f), trim);
        CutBox("FrameL", root, new Vector3(0.23f, 1.47f, N + 0.1f), new Vector3(0.06f, 1.85f, 0.1f), trim);
        CutBox("FrameR", root, new Vector3(3.17f, 1.47f, N + 0.1f), new Vector3(0.06f, 1.85f, 0.1f), trim);
        CutBox("Mullion", root, new Vector3(wx, 1.47f, N + 0.1f), new Vector3(0.06f, 1.85f, 0.08f), trim);
        foreach (var cx in new[] { -0.02f, 3.42f })
            for (int k = 0; k < 5; k++)
                CutBox("Curtain", root, new Vector3(cx - 0.24f + k * 0.12f, 1.3f, N - 0.08f - (k % 2) * 0.05f), new Vector3(0.14f, 2.5f, 0.05f), curtain);
        Prim(PrimitiveType.Cylinder, "CurtainRod", root, new Vector3(wx, 2.58f, N - 0.1f), new Vector3(0.025f, 2.1f, 0.025f), trim, new Vector3(0, 0, 90));

        // 벽걸이 TV와 TV장
        const float tx = -1.6f;
        CutBox("TvCabinet", root, new Vector3(tx, 0.24f, N - 0.25f), new Vector3(2.3f, 0.48f, 0.46f), darkWood, null, 1.2f);
        CutBox("CabinetTop", root, new Vector3(tx, 0.495f, N - 0.25f), new Vector3(2.34f, 0.03f, 0.48f), trim);
        CutBox("TvBody", root, new Vector3(tx, 1.3f, N - 0.06f), new Vector3(1.78f, 1.03f, 0.06f), black);
        var screen = Prim(PrimitiveType.Quad, "TvScreen", root, new Vector3(tx, 1.3f, N - 0.095f), new Vector3(1.7f, 0.956f, 1f), cutTvMat);
        screen.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        CutBox("Soundbar", root, new Vector3(tx, 0.55f, N - 0.2f), new Vector3(0.9f, 0.07f, 0.1f), black);
        Prim(PrimitiveType.Cylinder, "SmallPot", root, new Vector3(tx + 0.85f, 0.58f, N - 0.25f), new Vector3(0.14f, 0.07f, 0.14f), pot);
        Prim(PrimitiveType.Sphere, "SmallPlant", root, new Vector3(tx + 0.85f, 0.72f, N - 0.25f), new Vector3(0.24f, 0.22f, 0.24f), leaf);
        CutBox("PhotoFrame", root, new Vector3(tx - 0.85f, 0.6f, N - 0.2f), new Vector3(0.2f, 0.16f, 0.02f), darkWood, new Vector3(-10, 8, 0));

        // 벽시계, 서쪽 벽 책장, 구석의 스탠드 에어컨
        Prim(PrimitiveType.Cylinder, "Clock", root, new Vector3(-3.0f, 2.0f, N - 0.02f), new Vector3(0.36f, 0.015f, 0.36f), trim, new Vector3(90, 0, 0));
        CutBox("ClockHandH", root, new Vector3(-3.0f, 2.04f, N - 0.04f), new Vector3(0.012f, 0.1f, 0.005f), black, new Vector3(0, 0, -30));
        CutBox("ClockHandM", root, new Vector3(-2.95f, 1.99f, N - 0.04f), new Vector3(0.12f, 0.01f, 0.005f), black, new Vector3(0, 0, -20));
        var shelf = Group("Bookshelf", root, new Vector3(-W + 0.18f, 0, 1.9f));
        CutBox("Back", shelf, new Vector3(-0.15f, 0.95f, 0), new Vector3(0.03f, 1.9f, 1.0f), darkWood);
        CutBox("SideA", shelf, new Vector3(0, 0.95f, -0.49f), new Vector3(0.34f, 1.9f, 0.03f), darkWood);
        CutBox("SideB", shelf, new Vector3(0, 0.95f, 0.49f), new Vector3(0.34f, 1.9f, 0.03f), darkWood);
        foreach (var y in new[] { 0.05f, 0.5f, 0.95f, 1.4f, 1.88f })
            CutBox("Board", shelf, new Vector3(0, y, 0), new Vector3(0.34f, 0.03f, 0.98f), darkWood);
        foreach (var y in new[] { 0.05f, 0.5f, 0.95f, 1.4f })
            for (float z = -0.45f; z < 0.4f;)
            {
                float thick = R(0.03f, 0.07f), tall = R(0.22f, 0.36f);
                if (Chance(0.12)) { z += 0.08f; continue; }
                CutBox("Book", shelf, new Vector3(-0.02f, y + 0.015f + tall / 2, z + thick / 2), new Vector3(0.24f, tall, thick), Pick(books));
                z += thick + 0.004f;
            }
        CutBox("AirCon", root, new Vector3(-3.45f, 0.875f, N - 0.27f), new Vector3(0.42f, 1.75f, 0.34f), trim);
        CutBox("AirConGrille", root, new Vector3(-3.45f, 1.4f, N - 0.445f), new Vector3(0.32f, 0.3f, 0.01f), black);

        // 소파 (북쪽 TV를 향한다)
        const float sx = -1.25f, sz = -0.5f;
        CutBox("SofaBase", root, new Vector3(sx, 0.2f, sz), new Vector3(2.3f, 0.3f, 0.95f), sofa);
        CutBox("SeatL", root, new Vector3(sx - 0.55f, 0.43f, sz + 0.05f), new Vector3(1.08f, 0.16f, 0.84f), sofa);
        CutBox("SeatR", root, new Vector3(sx + 0.55f, 0.43f, sz + 0.05f), new Vector3(1.08f, 0.16f, 0.84f), sofa);
        CutBox("SofaBack", root, new Vector3(sx, 0.68f, sz - 0.38f), new Vector3(2.3f, 0.66f, 0.2f), sofa, new Vector3(-8, 0, 0));
        CutBox("ArmL", root, new Vector3(sx - 1.07f, 0.47f, sz), new Vector3(0.18f, 0.54f, 0.95f), sofa);
        CutBox("ArmR", root, new Vector3(sx + 1.07f, 0.47f, sz), new Vector3(0.18f, 0.54f, 0.95f), sofa);
        foreach (var lx in new[] { sx - 1.05f, sx + 1.05f })
            foreach (var lz in new[] { sz - 0.4f, sz + 0.4f })
                CutBox("SofaLeg", root, new Vector3(lx, 0.025f, lz), new Vector3(0.05f, 0.05f, 0.05f), darkWood);
        CutBox("Pillow", root, new Vector3(sx - 0.8f, 0.68f, sz - 0.22f), new Vector3(0.42f, 0.38f, 0.12f), pillow, new Vector3(-15, 10, 4));
        CutBox("Blanket", root, new Vector3(sx - 1.07f, 0.72f, sz + 0.05f), new Vector3(0.24f, 0.06f, 0.7f), rug, new Vector3(0, 0, 6));

        // 거실 탁자·러그·스탠드 조명·화분
        CutBox("Rug", root, new Vector3(sx, 0.005f, 0.25f), new Vector3(3.0f, 0.01f, 2.3f), rug);
        CutBox("TableTop", root, new Vector3(sx, 0.4f, 0.62f), new Vector3(1.1f, 0.04f, 0.55f), darkWood);
        foreach (var lx in new[] { sx - 0.5f, sx + 0.5f })
            foreach (var lz in new[] { 0.4f, 0.84f })
                CutBox("TableLeg", root, new Vector3(lx, 0.19f, lz), new Vector3(0.04f, 0.38f, 0.04f), darkWood);
        Prim(PrimitiveType.Cylinder, "Mug", root, new Vector3(sx + 0.3f, 0.47f, 0.6f), new Vector3(0.08f, 0.05f, 0.08f), trim);
        CutBox("Magazine", root, new Vector3(sx - 0.25f, 0.425f, 0.66f), new Vector3(0.28f, 0.01f, 0.21f), books[1], new Vector3(0, 12, 0));
        for (int i = 0; i < 3; i++)
            Prim(PrimitiveType.Sphere, "Fruit", root, new Vector3(sx + 0.05f + i * 0.07f, 0.46f, 0.52f + (i % 2) * 0.05f), Vector3.one * 0.075f, fruit);
        Prim(PrimitiveType.Cylinder, "LampBase", root, new Vector3(-2.85f, 0.02f, -0.95f), new Vector3(0.3f, 0.02f, 0.3f), black);
        Prim(PrimitiveType.Cylinder, "LampPole", root, new Vector3(-2.85f, 0.78f, -0.95f), new Vector3(0.03f, 0.76f, 0.03f), black);
        Prim(PrimitiveType.Cylinder, "LampShade", root, new Vector3(-2.85f, 1.6f, -0.95f), new Vector3(0.36f, 0.14f, 0.36f), curtain);
        Prim(PrimitiveType.Cylinder, "Pot", root, new Vector3(3.35f, 0.22f, 2.55f), new Vector3(0.42f, 0.22f, 0.42f), pot);
        for (int i = 0; i < 6; i++)
            Prim(PrimitiveType.Sphere, "Leaves", root, new Vector3(3.35f + R(-0.2f, 0.2f), R(0.65f, 1.3f), 2.55f + R(-0.18f, 0.18f)), new Vector3(R(0.3f, 0.45f), R(0.35f, 0.5f), R(0.3f, 0.45f)), leaf);
        Prim(PrimitiveType.Cylinder, "CeilingLamp", root, new Vector3(-0.4f, 2.68f, 0), new Vector3(0.5f, 0.02f, 0.5f), trim);
    }

    // ---------- 방공호 입구 ----------

    static void CutBuildShelter(Transform root)
    {
        var hazard = CutLitTex(CutStripesTexture(), Color.white, 0.3f);
        var sandbag = CutClone(M["cloth"], new Color(0.55f, 0.5f, 0.38f), 0.05f);
        var sign = CutUnlit(CutSignTexture(), new Color(1.15f, 1.15f, 1.15f));
        var black = M["tire"];

        // 땅과 입구 앞 콘크리트 바닥
        CutBox("Ground", root, new Vector3(0, -0.1f, 30), new Vector3(260, 0.2f, 240), M["asphalt"], null, 4f);
        CutBox("Plaza", root, new Vector3(-3.8f, 0.03f, 5.2f), new Vector3(8f, 0.06f, 4f), M["floor"], null, 2.5f);

        // 두꺼운 콘크리트 방공호 (앞벽 z 7.0~7.8, 문 구멍 x -4.8~-2.8, 높이 2.4)
        Wall(root, "BunkerFront", new Vector3(-6.8f, 0, 7.4f), 0, 6f, 3.2f, 0.8f, M["wall"], new[] { new Op(3f, 2f, 0, 2.4f) });
        CutBox("MassL", root, new Vector3(-5.8f, 1.6f, 9.6f), new Vector3(2f, 3.2f, 3.6f), M["cracked"], null, 3f);
        CutBox("MassR", root, new Vector3(-1.8f, 1.6f, 9.6f), new Vector3(2f, 3.2f, 3.6f), M["cracked"], null, 3f);
        CutBox("BackWall", root, new Vector3(-3.8f, 1.6f, 11.2f), new Vector3(2f, 3.2f, 0.4f), M["wall"], null, 2.5f);
        CutBox("InnerRoof", root, new Vector3(-3.8f, 2.8f, 9.4f), new Vector3(2f, 0.8f, 3.2f), M["wall"], null, 2.5f);
        CutBox("InnerFloor", root, new Vector3(-3.8f, 0.01f, 9.4f), new Vector3(2f, 0.02f, 3.2f), M["floor"], null, 2.5f);
        CutBox("Roof", root, new Vector3(-3.8f, 3.35f, 9.2f), new Vector3(6.6f, 0.3f, 4.8f), M["dirty"], null, 2.5f);
        Prim(PrimitiveType.Cylinder, "InnerPipe", root, new Vector3(-4.65f, 2.2f, 9.4f), new Vector3(0.08f, 1.6f, 0.08f), M["metal"], new Vector3(90, 0, 0));

        // 노랑·검정 줄무늬 철문틀
        CutBox("FrameL", root, new Vector3(-4.86f, 1.23f, 6.96f), new Vector3(0.14f, 2.46f, 0.1f), hazard, null, 0.5f);
        CutBox("FrameR", root, new Vector3(-2.74f, 1.23f, 6.96f), new Vector3(0.14f, 2.46f, 0.1f), hazard, null, 0.5f);
        CutBox("FrameT", root, new Vector3(-3.8f, 2.47f, 6.96f), new Vector3(2.26f, 0.14f, 0.1f), hazard, null, 0.5f);

        // 바깥으로 활짝 열린 두꺼운 방폭문 (안쪽 면의 핸들이 카메라 쪽을 본다)
        var hinge = Group("BlastDoor", root, new Vector3(-4.86f, 0, 6.86f), 100);
        CutBox("Rim", hinge, new Vector3(1.03f, 1.22f, 0), new Vector3(2.12f, 2.5f, 0.16f), hazard, null, 0.5f);
        CutBox("Slab", hinge, new Vector3(1.03f, 1.22f, 0), new Vector3(2.06f, 2.44f, 0.26f), M["metal"], null, 1.2f);
        foreach (var y in new[] { 0.5f, 1.95f })
            CutBox("Bar", hinge, new Vector3(1.03f, y, 0.16f), new Vector3(1.7f, 0.09f, 0.07f), M["metal"]);
        Prim(PrimitiveType.Cylinder, "Wheel", hinge, new Vector3(1.03f, 1.22f, 0.17f), new Vector3(0.62f, 0.02f, 0.62f), M["metal"], new Vector3(90, 0, 0));
        Prim(PrimitiveType.Cylinder, "Hub", hinge, new Vector3(1.03f, 1.22f, 0.21f), new Vector3(0.12f, 0.04f, 0.12f), M["metal"], new Vector3(90, 0, 0));
        for (int k = 0; k < 3; k++)
            CutBox("Spoke", hinge, new Vector3(1.03f, 1.22f, 0.2f), new Vector3(0.6f, 0.04f, 0.04f), M["metal"], new Vector3(0, 0, k * 60));
        foreach (var y in new[] { 0.45f, 2.0f })
            Prim(PrimitiveType.Cylinder, "Hinge", hinge, new Vector3(0, y, 0), new Vector3(0.12f, 0.16f, 0.12f), M["metal"]);

        // 문 위 '민방위 대피소' 표지판, 경광등, 문 안쪽 붉은 비상등
        var signQuad = Prim(PrimitiveType.Quad, "Sign", root, new Vector3(-3.8f, 2.88f, 6.97f), new Vector3(1.5f, 0.47f, 1f), sign);
        signQuad.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        CutBox("Beacon", root, new Vector3(-2.35f, 2.85f, 6.9f), new Vector3(0.2f, 0.2f, 0.16f), M["redLamp"]);
        PointLight("BeaconLight", root, new Vector3(-2.35f, 2.7f, 6.5f), new Color(1f, 0.12f, 0.07f), 6f, 10f);
        CutBox("InsideLamp", root, new Vector3(-3.8f, 2.37f, 8.8f), new Vector3(0.3f, 0.06f, 0.12f), M["redLamp"]);
        PointLight("InsideRed", root, new Vector3(-3.8f, 2.1f, 8.8f), new Color(1f, 0.1f, 0.06f), 10f, 7f, true);

        // 입구 양옆 모래주머니
        foreach (var x0 in new[] { -6.7f, -2.5f })
            for (int row = 0; row < 2; row++)
                for (int k = 0; k < 4 - row; k++)
                    Prim(PrimitiveType.Sphere, "Sandbag", root, new Vector3(x0 + k * 0.55f + row * 0.27f, 0.13f + row * 0.21f, 6.55f), new Vector3(0.56f, 0.24f, 0.36f), sandbag, new Vector3(0, R(-8f, 8f), 0));

        // 흩어진 잔해 (주인공이 뛰는 길은 비운다), 가로등, 세워 둔 차
        for (int i = 0; i < 14; i++)
        {
            var p = new Vector3(R(-2f, 5f), 0, R(0.5f, 9f));
            if (p.x > -1.6f && p.x < 0.6f && p.z > 3.2f && p.z < 7f) continue;
            var s = new Vector3(R(0.12f, 0.45f), R(0.08f, 0.22f), R(0.12f, 0.4f));
            p.y = s.y / 2;
            CutBox("Rubble", root, p, s, Pick(M["cracked"], M["dirty"]), new Vector3(R(-12f, 12f), R(0f, 360f), R(-12f, 12f)));
        }
        Prim(PrimitiveType.Cylinder, "LampPost", root, new Vector3(8.5f, 2.6f, 12f), new Vector3(0.12f, 2.6f, 0.12f), black);
        CutBox("LampArm", root, new Vector3(8.0f, 5.1f, 12f), new Vector3(1.1f, 0.08f, 0.1f), black);
        CutBox("LampHead", root, new Vector3(7.5f, 5.0f, 12f), new Vector3(0.45f, 0.12f, 0.22f), black);
        var car = Group("Car", root, new Vector3(5.5f, 0, 14f), 80);
        CutBox("Body", car, new Vector3(0, 0.55f, 0), new Vector3(1.8f, 0.7f, 4.2f), M["paintBlue"]);
        CutBox("Cabin", car, new Vector3(0, 1.12f, -0.2f), new Vector3(1.6f, 0.5f, 2.1f), M["glass"]);
        foreach (var wx in new[] { -0.85f, 0.85f })
            foreach (var wz in new[] { -1.3f, 1.3f })
                Prim(PrimitiveType.Cylinder, "Wheel", car, new Vector3(wx, 0.32f, wz), new Vector3(0.62f, 0.12f, 0.62f), M["tire"], new Vector3(0, 0, 90));

        // 먼 도시 실루엣과 하늘 (버섯구름은 오른쪽 뒤)
        CutCity(root, new[] { 60f, 78f, 100f, 125f }, -150f, 150f, 1.0f, -3f, 0.6f, 4.2f, 6.5f);
        var sky = Prim(PrimitiveType.Quad, "Sky", root, new Vector3(0, 35, 150), new Vector3(260, 130, 1), CutUnlit(CutSkyTexture(2), new Color(1.12f, 1.12f, 1.12f)));
        sky.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
    }

    // 먼 도시: 줄마다 건물 상자를 늘어놓는다. 눈높이에서 본 각도(도)로 지붕 높이를 정해 스카이라인이 고르게 보이게 한다
    static void CutCity(Transform parent, float[] rows, float xMin, float xMax, float eyeY, float eyeZ, float minAngle, float maxAngle, float towerAngle)
    {
        const float bottom = -60f;
        for (int r = 0; r < rows.Length; r++)
        {
            var mat = r < 2 ? cutCityNear : r < 4 ? cutCityMid : cutCityFar;
            for (float x = xMin; x < xMax;)
            {
                float w = R(7f, 18f), d = R(8f, 14f), z = rows[r] + R(-4f, 4f);
                float ang = Chance(0.12) ? towerAngle * R(0.8f, 1.1f) : R(minAngle, maxAngle);
                float top = eyeY + Mathf.Tan(ang * Mathf.Deg2Rad) * (z - eyeZ);
                var b = Box("Block", parent, new Vector3(x + w / 2, (top + bottom) / 2, z), new Vector3(w, top - bottom, d), mat, 6f, false, null, false);
                b.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                x += w + R(1f, 6f);
            }
        }
    }

    // ---------- 사람 ----------

    static CutRig CutPerson(Transform parent, string name, Vector3 pos, float yaw, Material shirt, Material pants, Material skin, Material hair, Material shoes)
    {
        var r = new CutRig();
        r.Root = Group(name, parent, pos, yaw);
        r.Hips = Group("Hips", r.Root, new Vector3(0, 0.94f, 0));
        Prim(PrimitiveType.Cube, "Pelvis", r.Hips, new Vector3(0, -0.03f, 0), new Vector3(0.34f, 0.2f, 0.21f), pants);
        r.LegL = CutLeg(r.Hips, -1, pants, shoes, out r.ShinL);
        r.LegR = CutLeg(r.Hips, 1, pants, shoes, out r.ShinR);

        r.Spine = Group("Spine", r.Hips, new Vector3(0, 0.06f, 0));
        Prim(PrimitiveType.Cube, "Torso", r.Spine, new Vector3(0, 0.22f, 0), new Vector3(0.36f, 0.46f, 0.2f), shirt);
        Prim(PrimitiveType.Cube, "Chest", r.Spine, new Vector3(0, 0.4f, 0.005f), new Vector3(0.42f, 0.17f, 0.22f), shirt);
        Prim(PrimitiveType.Cylinder, "Neck", r.Spine, new Vector3(0, 0.54f, 0), new Vector3(0.1f, 0.06f, 0.1f), skin);

        r.Head = Group("Head", r.Spine, new Vector3(0, 0.58f, 0));
        Prim(PrimitiveType.Sphere, "Skull", r.Head, new Vector3(0, 0.11f, 0.01f), new Vector3(0.19f, 0.23f, 0.21f), skin);
        Prim(PrimitiveType.Sphere, "Hair", r.Head, new Vector3(0, 0.155f, -0.03f), new Vector3(0.205f, 0.19f, 0.21f), hair);
        Prim(PrimitiveType.Cube, "Nose", r.Head, new Vector3(0, 0.1f, 0.115f), new Vector3(0.03f, 0.05f, 0.04f), skin);
        Prim(PrimitiveType.Sphere, "EarL", r.Head, new Vector3(-0.095f, 0.1f, 0), new Vector3(0.03f, 0.06f, 0.045f), skin);
        Prim(PrimitiveType.Sphere, "EarR", r.Head, new Vector3(0.095f, 0.1f, 0), new Vector3(0.03f, 0.06f, 0.045f), skin);

        r.ArmL = CutArm(r.Spine, -1, shirt, skin, out r.ForeL);
        r.ArmR = CutArm(r.Spine, 1, shirt, skin, out r.ForeR);
        return r;
    }

    static Transform CutLeg(Transform hips, float side, Material pants, Material shoes, out Transform shin)
    {
        var leg = Group(side < 0 ? "LegL" : "LegR", hips, new Vector3(0.1f * side, -0.04f, 0));
        Prim(PrimitiveType.Cube, "Thigh", leg, new Vector3(0, -CutThigh / 2, 0), new Vector3(0.16f, CutThigh + 0.04f, 0.17f), pants);
        shin = Group("Shin", leg, new Vector3(0, -CutThigh, 0));
        Prim(PrimitiveType.Cube, "Calf", shin, new Vector3(0, -CutShin / 2, 0), new Vector3(0.13f, CutShin + 0.02f, 0.14f), pants);
        Prim(PrimitiveType.Cube, "Shoe", shin, new Vector3(0, -CutShin, 0.05f), new Vector3(0.12f, 0.08f, 0.27f), shoes);
        return leg;
    }

    static Transform CutArm(Transform spine, float side, Material shirt, Material skin, out Transform fore)
    {
        var arm = Group(side < 0 ? "ArmL" : "ArmR", spine, new Vector3(0.25f * side, 0.46f, 0));
        Prim(PrimitiveType.Cube, "Sleeve", arm, new Vector3(0, -0.07f, 0), new Vector3(0.13f, 0.17f, 0.13f), shirt);
        Prim(PrimitiveType.Cube, "Upper", arm, new Vector3(0, -CutUpperArm / 2, 0), new Vector3(0.095f, CutUpperArm, 0.095f), skin);
        fore = Group("Fore", arm, new Vector3(0, -CutUpperArm, 0));
        Prim(PrimitiveType.Cube, "Forearm", fore, new Vector3(0, -CutForearm / 2, 0), new Vector3(0.085f, CutForearm + 0.02f, 0.085f), skin);
        Prim(PrimitiveType.Cube, "Hand", fore, new Vector3(0, -CutForearm - 0.05f, 0), new Vector3(0.08f, 0.11f, 0.04f), skin);
        return arm;
    }

    // 자세 잡기. 발·손 위치와 무릎·팔꿈치가 향할 곳은 사람 기준 좌표(발밑이 원점, +z가 앞)로 준다
    static void CutPose(CutRig r, float hipsY, Vector3 spine, Vector3 head,
        Vector3 footL, Vector3 kneeL, Vector3 footR, Vector3 kneeR,
        Vector3 handL, Vector3 elbowL, Vector3 handR, Vector3 elbowR)
    {
        r.Hips.localPosition = new Vector3(0, hipsY, 0);
        r.Spine.localRotation = Quaternion.Euler(spine);
        r.Head.localRotation = Quaternion.Euler(head);
        var fwd = r.Root.forward;
        Vector3 P(Vector3 local) => r.Root.TransformPoint(local);
        CutReach(r.LegL, r.ShinL, CutThigh, CutShin, P(footL), P(kneeL), fwd);
        CutReach(r.LegR, r.ShinR, CutThigh, CutShin, P(footR), P(kneeR), fwd);
        CutReach(r.ArmL, r.ForeL, CutUpperArm, CutForearm, P(handL), P(elbowL), fwd);
        CutReach(r.ArmR, r.ForeR, CutUpperArm, CutForearm, P(handR), P(elbowR), fwd);
    }

    // 두 마디 팔다리를 목표 지점에 닿게 굽힌다 (코사인 법칙). pole: 팔꿈치·무릎이 향할 쪽 (월드 좌표)
    static void CutReach(Transform upper, Transform lower, float a, float b, Vector3 target, Vector3 pole, Vector3 facing)
    {
        Vector3 s = upper.position;
        Vector3 to = target - s;
        float d = Mathf.Clamp(to.magnitude, 0.05f, a + b - 0.002f);
        Vector3 u = to.normalized;
        Vector3 v = Vector3.ProjectOnPlane(pole - s, u);
        if (v.sqrMagnitude < 1e-6f) v = Vector3.ProjectOnPlane(facing, u);
        v.Normalize();
        float cos = Mathf.Clamp((a * a + d * d - b * b) / (2 * a * d), -1f, 1f);
        Vector3 joint = s + a * (cos * u + Mathf.Sqrt(1 - cos * cos) * v);
        CutAim(upper, joint - s, facing);
        CutAim(lower, s + u * d - joint, facing);
    }

    // 마디의 아래쪽(-y)이 dir을 향하게 돌린다. 앞(+z)은 몸이 보는 쪽에 가깝게 둔다
    static void CutAim(Transform joint, Vector3 dir, Vector3 facing)
    {
        Vector3 down = dir.normalized;
        Vector3 fwd = Vector3.ProjectOnPlane(facing, down);
        if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.ProjectOnPlane(Vector3.up, down);
        joint.rotation = Quaternion.LookRotation(fwd.normalized, -down);
    }

    // ================= 조명·후처리·촬영 =================

    static void CutSunLight(Vector3 euler, Color color, float intensity)
    {
        cutSun.transform.rotation = Quaternion.Euler(euler);
        cutSun.color = color;
        cutSun.intensity = intensity;
    }

    static void CutSetLight(Light l, Color color, float intensity)
    {
        l.color = color;
        l.intensity = intensity;
    }

    static void CutCityTint(Color near, Color mid, Color far)
    {
        cutCityNear.SetColor("_BaseColor", near);
        cutCityMid.SetColor("_BaseColor", mid);
        cutCityFar.SetColor("_BaseColor", far);
    }

    // 고른 환경광. 굽지 않은 임시 씬이라 환경광 프로브도 직접 채운다
    static void CutAmbient(Color c)
    {
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = c;
        var sh = new SphericalHarmonicsL2();
        sh.AddAmbientLight(QualitySettings.activeColorSpace == ColorSpace.Linear ? c.linear : c);
        RenderSettings.ambientProbe = sh;
    }

    static void CutCamera(Camera cam, Vector3 pos, Vector3 target, float fov, float roll)
    {
        cam.transform.position = pos;
        cam.transform.rotation = Quaternion.LookRotation(target - pos) * Quaternion.Euler(0, 0, roll);
        cam.fieldOfView = fov;
    }

    // 메모리에만 있는 후처리 프로필 (에셋을 남기지 않는다). 값은 장면마다 CutGrade로 바꾼다
    static VolumeProfile CutMakeProfile()
    {
        var p = ScriptableObject.CreateInstance<VolumeProfile>();
        cutTemp.Add(p);

        T Add<T>() where T : VolumeComponent
        {
            var c = p.Add<T>(true);
            cutTemp.Add(c);
            return c;
        }

        Add<Tonemapping>().mode.value = TonemappingMode.ACES;
        Add<ColorAdjustments>();
        var bl = Add<Bloom>();
        bl.threshold.value = 0.95f;
        bl.scatter.value = 0.7f;
        bl.highQualityFiltering.value = true;
        var vg = Add<Vignette>();
        vg.smoothness.value = 0.5f;
        vg.color.value = Color.black;
        vg.center.value = new Vector2(0.5f, 0.5f);
        var fg = Add<FilmGrain>();
        fg.type.value = FilmGrainLookup.Medium3;
        fg.response.value = 0.8f;
        Add<ChromaticAberration>().intensity.value = 0.08f;
        var dof = Add<DepthOfField>();
        dof.mode.value = DepthOfFieldMode.Gaussian;
        dof.gaussianMaxRadius.value = 1f;
        dof.highQualitySampling.value = true;
        return p;
    }

    static void CutGrade(float exposure, float contrast, float saturation, Color filter, float bloom, float vignette, float grain, float blurStart)
    {
        if (cutProfile.TryGet(out ColorAdjustments ca))
        {
            ca.postExposure.value = exposure;
            ca.contrast.value = contrast;
            ca.saturation.value = saturation;
            ca.colorFilter.value = filter;
        }
        if (cutProfile.TryGet(out Bloom bl)) bl.intensity.value = bloom;
        if (cutProfile.TryGet(out Vignette vg)) vg.intensity.value = vignette;
        if (cutProfile.TryGet(out FilmGrain fg)) fg.intensity.value = grain;
        if (cutProfile.TryGet(out DepthOfField dof))
        {
            dof.gaussianStart.value = blurStart;   // 이보다 먼 곳(창밖 도시 등)은 살짝 흐리게
            dof.gaussianEnd.value = blurStart * 6f;
        }
    }

    // 2배 크기 RenderTexture에 찍고 1920x1080으로 줄여 PNG로 저장한다 (sRGB 그대로)
    static void CutRender(Camera cam, string path)
    {
        int w = CutW * CutSuper, h = CutH * CutSuper;
        var big = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { name = "CutsceneBig", filterMode = FilterMode.Bilinear };
        var small = new RenderTexture(CutW, CutH, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { name = "CutsceneSmall", filterMode = FilterMode.Bilinear };
        big.Create();
        small.Create();
        var prevActive = RenderTexture.active;
        try
        {
            cam.targetTexture = big;
            cam.aspect = (float)CutW / CutH;
            cam.Render();
            Graphics.Blit(big, small);
            RenderTexture.active = small;
            var tex = new Texture2D(CutW, CutH, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, CutW, CutH), 0, 0);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
        finally
        {
            cam.targetTexture = null;
            RenderTexture.active = prevActive;
            big.Release();
            small.Release();
            Object.DestroyImmediate(big);
            Object.DestroyImmediate(small);
        }
    }

    // ================= 임시 재질·도형 =================

    static GameObject CutBox(string name, Transform parent, Vector3 pos, Vector3 size, Material mat, Vector3? euler = null, float tile = 1f)
        => Box(name, parent, pos, size, mat, tile, false, euler, false);

    static Material CutLit(Color color, float smoothness = 0.2f)
    {
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "CutLit" };
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Smoothness", smoothness);
        m.SetFloat("_Metallic", 0f);
        cutTemp.Add(m);
        return m;
    }

    static Material CutLitTex(Texture tex, Color color, float smoothness)
    {
        var m = CutLit(color, smoothness);
        m.SetTexture("_BaseMap", tex);
        return m;
    }

    // 기존 재질(텍스처 포함)을 복사해 색만 바꾼다. 원본 에셋은 건드리지 않는다
    static Material CutClone(Material src, Color tint, float smoothness)
    {
        var m = new Material(src) { name = src.name + "_Cutscene" };
        m.SetColor("_BaseColor", tint);
        m.SetColor("_Color", tint);
        m.SetFloat("_Smoothness", smoothness);
        cutTemp.Add(m);
        return m;
    }

    // 빛을 받지 않는 재질 (하늘·TV 화면·먼 건물·표지판). 색을 1보다 크게 주면 블룸으로 번진다
    static Material CutUnlit(Texture tex, Color color)
    {
        var m = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "CutUnlit" };
        m.SetTexture("_BaseMap", tex);
        m.SetColor("_BaseColor", color);
        cutTemp.Add(m);
        return m;
    }

    // ================= 코드로 그리는 그림 =================

    static Texture2D CutTex(int w, int h, Color[] px, bool mips, bool repeat)
    {
        var t = new Texture2D(w, h, TextureFormat.RGBA32, mips)
        {
            name = "CutsceneTex",
            wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp,
            filterMode = mips ? FilterMode.Trilinear : FilterMode.Bilinear,
            anisoLevel = 4,
        };
        t.SetPixels(px);
        t.Apply(mips);
        cutTemp.Add(t);
        return t;
    }

    static float CutSq(float v) => v * v;

    static float CutFbm(float x, float y)
    {
        float sum = 0, amp = 0.5f, norm = 0;
        for (int o = 0; o < 4; o++)
        {
            sum += Mathf.PerlinNoise(x + o * 17.3f, y + o * 31.7f) * amp;
            norm += amp;
            amp *= 0.5f;
            x *= 2.03f;
            y *= 2.03f;
        }
        return sum / norm;
    }

    // 하늘 판 그림. kind 0: 맑은 하늘, 1: 섬광과 버섯구름(2번), 2: 붉은 하늘(3번)
    // 판 위의 점(u,v)을 월드 좌표(x,y)로 바꿔 그린다: 거실 창밖 판 x -40~200, y -50~70 / 방공호 판 x -130~130, y -30~100
    static Texture2D CutSkyTexture(int kind)
    {
        const int w = 2048, h = 1024;
        float x0 = kind < 2 ? -40f : -130f, xs = kind < 2 ? 240f : 260f;
        float y0 = kind < 2 ? -50f : -30f, ys = kind < 2 ? 120f : 130f;
        var px = new Color[w * h];
        for (int j = 0; j < h; j++)
        {
            float y = y0 + ys * (j + 0.5f) / h;
            for (int i = 0; i < w; i++)
            {
                float x = x0 + xs * (i + 0.5f) / w;
                var c = kind == 0 ? CutClearSky(x, y) : kind == 1 ? CutFlashSky(x, y) : CutRedSky(x, y);
                c.a = 1;
                px[j * w + i] = c;
            }
        }
        return CutTex(w, h, px, false, false);
    }

    static Color CutClearSky(float x, float y)
    {
        Color c = y >= 0
            ? Color.Lerp(new Color(0.8f, 0.88f, 0.95f), new Color(0.24f, 0.5f, 0.88f), Mathf.Pow(Mathf.Clamp01(y / 70f), 0.55f))
            : Color.Lerp(new Color(0.74f, 0.8f, 0.86f), new Color(0.58f, 0.64f, 0.7f), Mathf.Clamp01(-y / 30f));
        // 해가 있는 오른쪽 위가 밝게 번진다
        c += new Color(1f, 0.95f, 0.85f) * (0.3f * Mathf.Exp(-2f * (CutSq((x - 160f) / 90f) + CutSq((y - 60f) / 50f))));
        // 지평선 가까이 낮게 뜬 작은 구름 몇 점 (유난히 맑은 날)
        if (y > 3f && y < 32f)
        {
            float band = Mathf.Exp(-CutSq((y - 14f) / 8f));
            float n = CutFbm(x * 0.05f + 100f, y * 0.12f + 100f);
            float cl = Mathf.Clamp01((n * band - 0.56f) * 7f);
            if (cl > 0)
            {
                var cloud = Color.Lerp(new Color(0.78f, 0.8f, 0.86f), new Color(1f, 0.99f, 0.97f), Mathf.Clamp01((y - 8f) / 14f));
                c = Color.Lerp(c, cloud, cl * 0.9f);
            }
        }
        return c;
    }

    static Color CutFlashSky(float x, float y)
    {
        Color c = y >= 0
            ? Color.Lerp(new Color(0.98f, 0.66f, 0.4f), new Color(0.2f, 0.32f, 0.6f), Mathf.Pow(Mathf.Clamp01(y / 70f), 0.5f))
            : Color.Lerp(new Color(0.75f, 0.42f, 0.26f), new Color(0.4f, 0.2f, 0.14f), Mathf.Clamp01(-y / 30f));
        // 폭발 쪽 지평선이 하얗게 타오른다
        c += new Color(1f, 0.72f, 0.42f) * (0.85f * Mathf.Exp(-(CutSq((x - 90f) / 75f) + CutSq((y - 5f) / 30f))));
        CutMushroom(ref c, x, y, 90f, -1f, 17f, 1f);
        return c;
    }

    static Color CutRedSky(float x, float y)
    {
        Color c;
        if (y >= 0)
        {
            c = Color.Lerp(new Color(1f, 0.52f, 0.22f), new Color(0.15f, 0.06f, 0.06f), Mathf.Pow(Mathf.Clamp01(y / 90f), 0.6f));
            // 연기가 하늘을 덮어 간다
            float smoke = CutFbm(x * 0.011f + 140f, y * 0.028f + 60f);
            c = Color.Lerp(c, new Color(0.1f, 0.05f, 0.045f), Mathf.Clamp01((smoke - 0.46f) * 2.4f) * Mathf.Clamp01(y / 35f));
        }
        else c = new Color(0.4f, 0.16f, 0.09f);
        c += new Color(1f, 0.62f, 0.3f) * (0.7f * Mathf.Exp(-(CutSq((x - 45f) / 70f) + CutSq((y - 4f) / 24f))));
        CutMushroom(ref c, x, y, 45f, -1f, 22f, 0.75f);
        return c;
    }

    // 버섯구름 (cx: 가운데, baseY: 밑동 높이, scale: 갓 반지름, bright: 1이면 막 터져 하얗게 빛나는 구름)
    static void CutMushroom(ref Color c, float x, float y, float cx, float baseY, float scale, float bright)
    {
        float px = (x - cx) / scale, py = (y - baseY) / scale;
        if (px < -2.3f || px > 2.3f || py < -0.6f || py > 2.7f) return;
        float n = CutFbm(px * 2.6f + 31.3f, py * 2.6f + 15.1f) - 0.5f;
        float n2 = CutFbm(px * 7f + 52.2f, py * 7f + 19.9f) - 0.5f;
        // 갓(납작한 타원), 기둥(밑동과 갓 쪽이 넓다), 바닥에 퍼진 먼지
        float cap = CutSq(px) + CutSq((py - 1.62f) / (py < 1.62f ? 0.32f : 0.5f));
        float stemW = 0.16f + 0.22f * Mathf.Exp(-Mathf.Max(0f, py) * 4f) + 0.12f * Mathf.Clamp01((py - 1.15f) * 2.5f);
        float stem = py > -0.1f && py < 1.7f ? CutSq(px / stemW) : 9f;
        float skirt = CutSq(px / 1.7f) + CutSq(py / 0.2f);
        float d = Mathf.Min(cap, Mathf.Min(stem, skirt));
        float dens = Mathf.Clamp01((1f - d + n * 0.9f + n2 * 0.25f) * 4f);
        if (dens <= 0) return;
        // 속은 뜨겁게 빛나고 가장자리는 연기
        float core = Mathf.Max(Mathf.Clamp01(1f - cap * 1.3f), Mathf.Clamp01(1f - stem * 1.6f) * 0.8f);
        float heat = Mathf.Clamp01(core * 1.2f + n * 0.7f + 0.1f) * bright;
        Color smoke = new Color(0.3f, 0.14f, 0.09f), fire = new Color(1f, 0.5f, 0.16f), white = new Color(1f, 0.95f, 0.8f);
        Color col = heat < 0.5f ? Color.Lerp(smoke, fire, heat * 2f) : Color.Lerp(fire, white, (heat - 0.5f) * 2f);
        c = Color.Lerp(c, col, dens);
    }

    // TV 속 도시 실루엣 높이 (가로 위치마다 건물 하나)
    static float CutSkyline(float x)
    {
        float r = Mathf.PerlinNoise(Mathf.Floor(x / 3.2f) * 0.73f + 11.1f, 4.4f);
        return 1f + r * r * 16f;
    }

    // 1번 TV: 평범한 여행 프로그램 (바다·섬·해, 아래 자막 띠)
    static Texture2D CutTvShowTexture()
    {
        const int w = 1024, h = 576;
        var px = new Color[w * h];
        for (int j = 0; j < h; j++)
            for (int i = 0; i < w; i++)
            {
                float u = (i + 0.5f) / w, v = (j + 0.5f) / h;
                var c = Color.Lerp(new Color(0.82f, 0.91f, 0.98f), new Color(0.35f, 0.62f, 0.95f), Mathf.Clamp01((v - 0.45f) / 0.55f));
                float sd = Mathf.Sqrt(CutSq((u - 0.78f) * 1.78f) + CutSq(v - 0.78f));
                c = Color.Lerp(c, new Color(1f, 0.96f, 0.75f), Mathf.Clamp01((0.07f - sd) * 200f));
                c += new Color(1f, 0.9f, 0.6f) * (0.25f * Mathf.Exp(-sd * sd * 60f));
                if (v < 0.45f)
                {
                    c = Color.Lerp(new Color(0.1f, 0.38f, 0.62f), new Color(0.3f, 0.6f, 0.8f), v / 0.45f);
                    if (Mathf.PerlinNoise(u * 120f + 5f, v * 30f + 5f) > 0.72f) c = Color.Lerp(c, Color.white, 0.6f);
                }
                float island = 0.44f + 0.17f * Mathf.Exp(-CutSq((u - 0.2f) / 0.17f)) + 0.06f * Mathf.Exp(-CutSq((u - 0.63f) / 0.07f)) + 0.01f * Mathf.Sin(u * 60f);
                if (v < island && island > 0.455f && v > 0.43f)
                    c = Color.Lerp(new Color(0.16f, 0.4f, 0.2f), new Color(0.32f, 0.6f, 0.3f), Mathf.Clamp01((v - 0.43f) / 0.2f));
                c.a = 1;
                px[j * w + i] = c;
            }
        CutFill(px, w, h, 0.04f, 0.08f, 0.6f, 0.17f, new Color(1f, 1f, 1f, 0.85f));
        CutFill(px, w, h, 0.04f, 0.08f, 0.055f, 0.17f, new Color(1f, 0.55f, 0.15f, 1f));
        CutFill(px, w, h, 0.035f, 0.87f, 0.075f, 0.945f, new Color(1f, 1f, 1f, 0.8f));
        CutText(px, w, h, CutTxtShow, 30, 0.075f * w, 0.125f * h, new Color(0.15f, 0.15f, 0.18f), 0f);
        CutText(px, w, h, "14:47", 30, 0.965f * w, 0.9075f * h, Color.white, 1f);
        return CutTex(w, h, px, true, false);
    }

    // 2번 TV: 핵폭발 속보 (현장 화면 + 빨간 속보 띠 + 흐름 자막)
    static Texture2D CutTvNewsTexture()
    {
        const int w = 1024, h = 576;
        var px = new Color[w * h];
        for (int j = 0; j < h; j++)
            for (int i = 0; i < w; i++)
            {
                // 화면을 가로 0~160, 지평선 y=0(화면 아래에서 34%)인 풍경으로 본다
                float x = (i + 0.5f) / w * 160f, y = ((j + 0.5f) / h - 0.34f) * 100f;
                var c = Color.Lerp(new Color(1f, 0.58f, 0.28f), new Color(0.28f, 0.2f, 0.28f), Mathf.Pow(Mathf.Clamp01(y / 66f), 0.7f));
                c += new Color(1f, 0.8f, 0.5f) * (0.8f * Mathf.Exp(-(CutSq((x - 80f) / 40f) + CutSq((y - 4f) / 18f))));
                CutMushroom(ref c, x, y, 80f, 0f, 18f, 1f);
                if (y < CutSkyline(x))
                    c = Color.Lerp(new Color(0.1f, 0.06f, 0.06f), new Color(0.22f, 0.11f, 0.08f), Mathf.Clamp01((y + 34f) / 40f));
                c.a = 1;
                px[j * w + i] = c;
            }
        CutFill(px, w, h, 0f, 0.115f, 1f, 0.255f, new Color(0.78f, 0.05f, 0.06f, 0.96f));
        CutFill(px, w, h, 0f, 0.115f, 0.2f, 0.255f, new Color(0.42f, 0.02f, 0.03f, 1f));
        CutFill(px, w, h, 0f, 0.255f, 1f, 0.263f, new Color(1f, 0.84f, 0.3f, 1f));
        CutFill(px, w, h, 0f, 0.02f, 1f, 0.115f, new Color(0.95f, 0.95f, 0.93f, 1f));
        CutFill(px, w, h, 0f, 0.02f, 0.13f, 0.115f, new Color(0.08f, 0.13f, 0.32f, 1f));
        CutFill(px, w, h, 0.035f, 0.87f, 0.135f, 0.945f, new Color(0.86f, 0.05f, 0.05f, 1f));
        CutText(px, w, h, CutTxtBreaking, 56, 0.1f * w, 0.185f * h, Color.white, 0.5f);
        CutText(px, w, h, CutTxtHeadline, 46, 0.225f * w, 0.185f * h, Color.white, 0f);
        CutText(px, w, h, CutTxtNewsTag, 26, 0.065f * w, 0.0675f * h, Color.white, 0.5f);
        CutText(px, w, h, CutTxtTicker, 26, 0.145f * w, 0.0675f * h, new Color(0.1f, 0.1f, 0.12f), 0f);
        CutText(px, w, h, CutTxtLive, 30, 0.085f * w, 0.9075f * h, Color.white, 0.5f);
        CutText(px, w, h, "14:52", 30, 0.965f * w, 0.9075f * h, Color.white, 1f);
        return CutTex(w, h, px, true, false);
    }

    // 초록 바탕 흰 글씨 대피소 표지판
    static Texture2D CutSignTexture()
    {
        const int w = 512, h = 160;
        var px = new Color[w * h];
        CutFill(px, w, h, 0f, 0f, 1f, 1f, Color.white);
        CutFill(px, w, h, 0.015f, 0.05f, 0.985f, 0.95f, new Color(0.04f, 0.36f, 0.2f, 1f));
        CutText(px, w, h, CutTxtSign, 64, w * 0.5f, h * 0.6f, Color.white, 0.5f);
        CutText(px, w, h, CutTxtSignSub, 26, w * 0.5f, h * 0.22f, new Color(0.85f, 1f, 0.9f), 0.5f);
        return CutTex(w, h, px, true, false);
    }

    // 노랑·검정 사선 줄무늬 (반복)
    static Texture2D CutStripesTexture()
    {
        const int s = 64;
        var px = new Color[s * s];
        for (int j = 0; j < s; j++)
            for (int i = 0; i < s; i++)
            {
                bool yellow = (i + j) / 16 % 2 == 0;
                float grime = 0.8f + 0.2f * Mathf.PerlinNoise(i * 0.2f + 3f, j * 0.2f + 7f);
                var c = (yellow ? new Color(0.95f, 0.72f, 0.08f) : new Color(0.07f, 0.07f, 0.07f)) * grime;
                c.a = 1;
                px[j * s + i] = c;
            }
        return CutTex(s, s, px, true, true);
    }

    // 먼 건물 벽 (한 칸 = 6m: 2층 x 창 3개, 반복)
    static Texture2D CutFacadeTexture()
    {
        const int s = 64;
        var px = new Color[s * s];
        for (int j = 0; j < s; j++)
            for (int i = 0; i < s; i++)
            {
                float fu = i / (float)s * 3f % 1f, fv = j / (float)s * 2f % 1f;
                bool window = fu > 0.18f && fu < 0.82f && fv > 0.3f && fv < 0.78f;
                px[j * s + i] = window ? new Color(0.42f, 0.5f, 0.6f) : new Color(0.82f, 0.84f, 0.87f);
            }
        return CutTex(s, s, px, true, true);
    }

    // 정규화 좌표(0~1) 사각형을 반투명하게 칠한다
    static void CutFill(Color[] px, int w, int h, float u0, float v0, float u1, float v1, Color c)
    {
        int i0 = Mathf.Clamp(Mathf.RoundToInt(u0 * w), 0, w), i1 = Mathf.Clamp(Mathf.RoundToInt(u1 * w), 0, w);
        int j0 = Mathf.Clamp(Mathf.RoundToInt(v0 * h), 0, h), j1 = Mathf.Clamp(Mathf.RoundToInt(v1 * h), 0, h);
        var solid = new Color(c.r, c.g, c.b, 1f);
        for (int j = j0; j < j1; j++)
            for (int i = i0; i < i1; i++)
                px[j * w + i] = Color.Lerp(px[j * w + i], solid, c.a);
    }

    // ---------- 글자 ----------

    // 쓸 글자를 모두 폰트 글자판에 올린 뒤, GPU에만 있는 글자판을 읽을 수 있게 복사해 둔다
    static void CutPrepareFont()
    {
        cutFont = Resources.Load<Font>("Fonts/Pretendard-Bold");
        cutAtlas = null;
        if (cutFont == null)
        {
            Debug.LogWarning("[GameSceneBuilder] 폰트(Resources/Fonts/Pretendard-Bold)가 없어 TV·표지판 글자를 생략합니다.");
            return;
        }
        string all = CutTxtBreaking + CutTxtHeadline + CutTxtTicker + CutTxtNewsTag + CutTxtLive + CutTxtShow + CutTxtSign + CutTxtSignSub + "0123456789:";
        for (int pass = 0; pass < 2; pass++) // 글자판이 다시 만들어져도 모든 크기가 남도록 두 번
            foreach (int size in CutTextSizes)
                cutFont.RequestCharactersInTexture(all, size, FontStyle.Normal);
        var src = cutFont.material != null ? cutFont.material.mainTexture : null;
        if (src == null)
        {
            cutFont = null;
            return;
        }

        var rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        var prev = RenderTexture.active;
        Graphics.Blit(src, rt);
        RenderTexture.active = rt;
        cutAtlas = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false, true) { name = "CutsceneFontAtlas" };
        cutAtlas.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
        cutAtlas.Apply();
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        cutTemp.Add(cutAtlas);

        // 글자 모양이 알파에 담기는 것이 보통이지만, 혹시 빨강 채널에 담겼으면 그쪽을 쓴다
        var all32 = cutAtlas.GetPixels32();
        double sumA = 0, sumR = 0;
        foreach (var p in all32) { sumA += p.a; sumR += p.r; }
        cutAtlasRed = sumA / all32.Length > 150 && sumR < sumA;
    }

    // 글자를 텍스처 픽셀에 직접 그린다. (x, cy): 기준점(픽셀), align 0 왼쪽 · 0.5 가운데 · 1 오른쪽, 세로는 글자 높이의 가운데
    static void CutText(Color[] px, int w, int h, string text, int size, float x, float cy, Color color, float align)
    {
        if (cutFont == null || cutAtlas == null) return;
        float width = 0, top = float.MinValue, bottom = float.MaxValue;
        foreach (char ch in text)
        {
            if (!cutFont.GetCharacterInfo(ch, out CharacterInfo ci, size, FontStyle.Normal)) continue;
            width += ci.advance;
            if (ci.maxY > ci.minY)
            {
                top = Mathf.Max(top, ci.maxY);
                bottom = Mathf.Min(bottom, ci.minY);
            }
        }
        if (top < bottom) return;

        float pen = Mathf.Round(x - width * align), baseline = Mathf.Round(cy - (top + bottom) / 2f);
        foreach (char ch in text)
        {
            if (!cutFont.GetCharacterInfo(ch, out CharacterInfo ci, size, FontStyle.Normal)) continue;
            float gx0 = pen + ci.minX, gx1 = pen + ci.maxX, gy0 = baseline + ci.minY, gy1 = baseline + ci.maxY;
            if (gx1 > gx0 && gy1 > gy0)
            {
                int jMax = Mathf.Min(h, Mathf.CeilToInt(gy1)), iMax = Mathf.Min(w, Mathf.CeilToInt(gx1));
                for (int j = Mathf.Max(0, Mathf.FloorToInt(gy0)); j < jMax; j++)
                    for (int i = Mathf.Max(0, Mathf.FloorToInt(gx0)); i < iMax; i++)
                    {
                        float s = (i + 0.5f - gx0) / (gx1 - gx0), t = (j + 0.5f - gy0) / (gy1 - gy0);
                        if (s < 0 || s > 1 || t < 0 || t > 1) continue;
                        var uv = Vector2.Lerp(Vector2.Lerp(ci.uvBottomLeft, ci.uvBottomRight, s), Vector2.Lerp(ci.uvTopLeft, ci.uvTopRight, s), t);
                        var g = cutAtlas.GetPixelBilinear(uv.x, uv.y);
                        float a = (cutAtlasRed ? g.r : g.a) * color.a;
                        if (a <= 0) continue;
                        int k = j * w + i;
                        px[k] = Color.Lerp(px[k], new Color(color.r, color.g, color.b, 1f), a);
                    }
            }
            pen += ci.advance;
        }
    }
}
