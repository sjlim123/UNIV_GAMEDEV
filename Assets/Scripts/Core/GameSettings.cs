using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// 설정 메뉴 값 (PlayerPrefs에 저장). 저사양 PC용 품질 조절도 여기서 한다.
// 그래픽 (2026-11-22 세부 옵션): '그래픽 품질'(Quality)은 아래 세부 항목을 한 번에 맞추는 묶음이다. 세부 항목을 하나라도 따로 바꾸면 Quality = 4(사용자 지정).
//   물질 품질(MaterialQ 0~2) · 텍스처 품질(TextureQ 0~3) · 세부 묘사 품질(DetailQ 0~3) · UI 품질(UiQ 0~2)
//   비네트 · 블룸 · 왜곡(색수차·렌즈 왜곡) · 필름 그레인 · 그림자 · 안티 앨리어싱 · 이방성 필터링 · 선명도 향상 · 선명도 향상(실험: STP)
public static class GameSettings
{
    public static readonly string[] QualityNames = { "낮음", "중간", "높음", "매우 높음", "사용자 지정" };
    public const int CustomQuality = 4;
    public static readonly string[] Level3Names = { "낮음", "중간", "높음" };
    public static readonly string[] Level4Names = { "낮음", "중간", "높음", "매우 높음" };
    public static readonly string[] AntiAliasNames = { "꺼짐", "FXAA", "SMAA", "MSAA 2x", "MSAA 4x", "MSAA 8x" };
    public static readonly string[] AnisoNames = { "꺼짐", "2x", "4x", "8x", "16x" };
    public static readonly int[] FpsOptions = { 30, 60, 120, 144, 0 };
    // 조준점: 모양·색·크기
    public static readonly string[] CrosshairStyleNames = { "점", "십자", "십자 + 점", "원", "원 + 점", "없음" };
    public static readonly string[] CrosshairColorNames = { "흰색", "초록", "노랑", "빨강", "하늘색" };
    public static readonly Color[] CrosshairColors =
    {
        new Color(1f, 1f, 1f), new Color(0.45f, 1f, 0.45f), new Color(1f, 0.88f, 0.3f), new Color(1f, 0.35f, 0.3f), new Color(0.45f, 0.85f, 1f),
    };
    public static readonly string[] CrosshairSizeNames = { "작게", "보통", "크게" };

    public static int Quality;        // 묶음: 0 낮음, 1 중간, 2 높음, 3 매우 높음, 4 사용자 지정
    public static int MaterialQ;      // 0 낮음, 1 중간, 2 높음: 주변 그림자(SSAO, 중간부터)·전구·불 그림자(높음)·부드러운 그림자(낮음은 단단하게)
    public static int TextureQ;       // 0 낮음(1/4), 1 중간(1/2), 2 높음(원본), 3 매우 높음(원본 + 고해상도 묶음)
    public static int DetailQ;        // 0~3: 그림자 거리·그림자 단계(캐스케이드)·해상도·파티클 양·아파트 창틀 카드·거리별 모델 판(LOD 묶음이 생기면)
    public static int UiQ;            // 0 낮음(유리 흐림 끔, 화면 복사 안 함), 1 중간(약한 흐림), 2 높음
    public static bool Vignette, Bloom, Distortion, FilmGrain, Shadows, Sharpen, SharpenExp;
    public static int AntiAlias;      // AntiAliasNames 번호
    public static int Aniso;          // AnisoNames 번호
    public static float RenderScale;  // 0.5 ~ 1.0
    public static float Brightness;   // 0.5 ~ 2.0
    public static float Fov;
    public static int FpsIndex;
    public static bool VSync, ShowFps, Fullscreen, InvertY;
    public static bool Tips;          // 안내(할 일 명판·나침반·대상 불빛·힌트·조작키) 켜기 — 일시정지 메뉴의 Tip 스위치
    public static float Sensitivity;  // 마우스 감도 배율
    public static float Volume;        // 전체 (AudioListener)
    public static float VolumeAmbient, VolumeEffects, VolumeEnemy, VolumeUI; // 종류별 (AudioCategory)
    public static int CrosshairStyle, CrosshairColor, CrosshairSize;

    static Color baseAmbient;
    static float baseExposure;
    static VolumeProfile profile;
    static readonly Dictionary<Light, LightShadows> lightShadows = new Dictionary<Light, LightShadows>();
    static readonly Dictionary<ParticleSystem, float> particleRates = new Dictionary<ParticleSystem, float>();
    // 에디터 플레이가 끝날 때 되돌릴 원래 값 (URP 설정 파일·품질 설정은 플레이 중 바꾸면 파일에 남으므로)
    static bool assetsSaved;
    static float originalRenderScale, originalShadowDistance, originalLodBias;
    static int originalTextureLimit, originalMsaa, originalMaxLod, originalCascades, originalShadowRes;
    static bool originalOpaque, originalSsao;
    static UpscalingFilterSelection originalUpscale;
    static AnisotropicFiltering originalAniso;
    // '매우 높음' 전용 고해상도 텍스처 묶음 (방공호 벽·바닥, 벙커 정면, 제작대 때, Tripo 소품 Q3). 전체 단계에 더하는 값: 매우 높음 0(원본), 그 밖 1(반)
    const string HighResGroup = "HighRes";
    static bool highResSaved;
    static TextureMipmapLimitSettings originalHighRes;

    // 묶음(0~3)이 정하는 세부 값. 예전 4단계 품질을 바탕으로 했다 (낮음: 텍스처 반·그림자·블룸·필름 없음, 중간: FXAA·전구 그림자 끔, 높음·매우 높음: SMAA).
    // 11-22 일부러 바꾼 것 (저사양 최적화와 단계 구분): 낮음은 주변 그림자(SSAO)·핫바 유리 흐림을 끄고, 그림자 단계·해상도를 낮음 1단계 1024 / 중간 2단계,
    // 매우 높음은 그림자 해상도 4096. 이방성 필터링은 4x / 8x / 16x / 16x (예전에는 모든 단계가 강제 켬 + 기본 한계, 최소 9x)
    public static void ApplyPreset(int q)
    {
        q = Mathf.Clamp(q, 0, 3);
        Quality = q;
        MaterialQ = q == 0 ? 0 : q == 1 ? 1 : 2;
        TextureQ = q == 0 ? 1 : q == 3 ? 3 : 2;
        DetailQ = q;
        UiQ = q == 0 ? 0 : 2;  // 낮음만 유리 흐림을 끈다 (화면 복사를 아낀다)
        Vignette = true;
        Bloom = q >= 1;
        FilmGrain = q >= 1;
        Distortion = q >= 2;
        Shadows = q >= 1;
        AntiAlias = q == 0 ? 0 : q == 1 ? 1 : 2;
        Aniso = q == 0 ? 2 : q == 1 ? 3 : 4; // 낮음 4x · 중간 8x · 높음·매우 높음 16x (예전에는 모든 단계가 강제 켬 + 기본 한계(최소 9x))
        Sharpen = false;
        SharpenExp = false;
    }

    // 세부 항목 하나를 바꿨을 때: 묶음 표시를 '사용자 지정'으로 (모든 값이 어느 묶음과 똑같으면 그 묶음 이름으로)
    public static void MarkCustom()
    {
        int keepM = MaterialQ, keepT = TextureQ, keepD = DetailQ, keepU = UiQ, keepAA = AntiAlias, keepAn = Aniso;
        bool keepV = Vignette, keepB = Bloom, keepDi = Distortion, keepG = FilmGrain, keepS = Shadows, keepSh = Sharpen, keepSx = SharpenExp;
        int match = CustomQuality;
        for (int q = 0; q < 4 && match == CustomQuality; q++)
        {
            ApplyPreset(q);
            if (MaterialQ == keepM && TextureQ == keepT && DetailQ == keepD && UiQ == keepU && AntiAlias == keepAA && Aniso == keepAn
                && Vignette == keepV && Bloom == keepB && Distortion == keepDi && FilmGrain == keepG && Shadows == keepS && Sharpen == keepSh && SharpenExp == keepSx)
                match = q;
        }
        MaterialQ = keepM; TextureQ = keepT; DetailQ = keepD; UiQ = keepU; AntiAlias = keepAA; Aniso = keepAn;
        Vignette = keepV; Bloom = keepB; Distortion = keepDi; FilmGrain = keepG; Shadows = keepS; Sharpen = keepSh; SharpenExp = keepSx;
        Quality = match;
    }

    public static void Load()
    {
        Quality = Mathf.Clamp(PlayerPrefs.GetInt("set_quality", 2), 0, CustomQuality);
        // 세부 값이 없는 예전 저장(2026-11-22 전)은 묶음에서 만든다
        ApplyPreset(Quality == CustomQuality ? 2 : Quality);
        if (PlayerPrefs.HasKey("set_detailQ"))
        {
            MaterialQ = Mathf.Clamp(PlayerPrefs.GetInt("set_materialQ", MaterialQ), 0, 2);
            TextureQ = Mathf.Clamp(PlayerPrefs.GetInt("set_textureQ", TextureQ), 0, 3);
            DetailQ = Mathf.Clamp(PlayerPrefs.GetInt("set_detailQ", DetailQ), 0, 3);
            UiQ = Mathf.Clamp(PlayerPrefs.GetInt("set_uiQ", UiQ), 0, 2);
            Vignette = PlayerPrefs.GetInt("set_vignette", Vignette ? 1 : 0) == 1;
            Bloom = PlayerPrefs.GetInt("set_bloom", Bloom ? 1 : 0) == 1;
            Distortion = PlayerPrefs.GetInt("set_distortion", Distortion ? 1 : 0) == 1;
            FilmGrain = PlayerPrefs.GetInt("set_filmGrain", FilmGrain ? 1 : 0) == 1;
            Shadows = PlayerPrefs.GetInt("set_shadows", Shadows ? 1 : 0) == 1;
            Sharpen = PlayerPrefs.GetInt("set_sharpen", Sharpen ? 1 : 0) == 1;
            SharpenExp = PlayerPrefs.GetInt("set_sharpenExp", SharpenExp ? 1 : 0) == 1;
            AntiAlias = Mathf.Clamp(PlayerPrefs.GetInt("set_antiAlias", AntiAlias), 0, AntiAliasNames.Length - 1);
            Aniso = Mathf.Clamp(PlayerPrefs.GetInt("set_aniso", Aniso), 0, AnisoNames.Length - 1);
            // 저장된 세부 값이 지금의 어느 묶음과 같은지 다시 따진다 (묶음 내용이 바뀌었거나 따로 바꾼 값이면 '사용자 지정')
            MarkCustom();
        }
        RenderScale = PlayerPrefs.GetFloat("set_renderScale", 1f);
        Brightness = PlayerPrefs.GetFloat("set_brightness", 1f);
        Fov = PlayerPrefs.GetFloat("set_fov", 70f);
        FpsIndex = PlayerPrefs.GetInt("set_fps", 1);
        VSync = PlayerPrefs.GetInt("set_vsync", 1) == 1;
        ShowFps = PlayerPrefs.GetInt("set_showFps", 0) == 1;
        Fullscreen = PlayerPrefs.GetInt("set_fullscreen", 1) == 1;
        InvertY = PlayerPrefs.GetInt("set_invertY", 0) == 1;
        Tips = PlayerPrefs.GetInt("set_tips", 1) == 1;
        Sensitivity = PlayerPrefs.GetFloat("set_sensitivity", 1f);
        Volume = PlayerPrefs.GetFloat("set_volume", 0.8f);
        VolumeAmbient = PlayerPrefs.GetFloat("set_volAmbient", 1f);
        VolumeEffects = PlayerPrefs.GetFloat("set_volEffects", 1f);
        VolumeEnemy = PlayerPrefs.GetFloat("set_volEnemy", 1f);
        VolumeUI = PlayerPrefs.GetFloat("set_volUI", 1f);
        CrosshairStyle = Mathf.Clamp(PlayerPrefs.GetInt("set_crossStyle", 0), 0, CrosshairStyleNames.Length - 1);
        CrosshairColor = Mathf.Clamp(PlayerPrefs.GetInt("set_crossColor", 0), 0, CrosshairColors.Length - 1);
        CrosshairSize = Mathf.Clamp(PlayerPrefs.GetInt("set_crossSize", 1), 0, CrosshairSizeNames.Length - 1);
    }

    public static void ResetToDefault()
    {
        PlayerPrefs.DeleteAll();
        KeyBindings.Reload();
        Load();
        Save();
    }

    public static void Save()
    {
        PlayerPrefs.SetInt("set_quality", Quality);
        PlayerPrefs.SetInt("set_materialQ", MaterialQ);
        PlayerPrefs.SetInt("set_textureQ", TextureQ);
        PlayerPrefs.SetInt("set_detailQ", DetailQ);
        PlayerPrefs.SetInt("set_uiQ", UiQ);
        PlayerPrefs.SetInt("set_vignette", Vignette ? 1 : 0);
        PlayerPrefs.SetInt("set_bloom", Bloom ? 1 : 0);
        PlayerPrefs.SetInt("set_distortion", Distortion ? 1 : 0);
        PlayerPrefs.SetInt("set_filmGrain", FilmGrain ? 1 : 0);
        PlayerPrefs.SetInt("set_shadows", Shadows ? 1 : 0);
        PlayerPrefs.SetInt("set_sharpen", Sharpen ? 1 : 0);
        PlayerPrefs.SetInt("set_sharpenExp", SharpenExp ? 1 : 0);
        PlayerPrefs.SetInt("set_antiAlias", AntiAlias);
        PlayerPrefs.SetInt("set_aniso", Aniso);
        PlayerPrefs.SetFloat("set_renderScale", RenderScale);
        PlayerPrefs.SetFloat("set_brightness", Brightness);
        PlayerPrefs.SetFloat("set_fov", Fov);
        PlayerPrefs.SetInt("set_fps", FpsIndex);
        PlayerPrefs.SetInt("set_vsync", VSync ? 1 : 0);
        PlayerPrefs.SetInt("set_showFps", ShowFps ? 1 : 0);
        PlayerPrefs.SetInt("set_fullscreen", Fullscreen ? 1 : 0);
        PlayerPrefs.SetInt("set_invertY", InvertY ? 1 : 0);
        PlayerPrefs.SetInt("set_tips", Tips ? 1 : 0);
        PlayerPrefs.SetFloat("set_sensitivity", Sensitivity);
        PlayerPrefs.SetFloat("set_volume", Volume);
        PlayerPrefs.SetFloat("set_volAmbient", VolumeAmbient);
        PlayerPrefs.SetFloat("set_volEffects", VolumeEffects);
        PlayerPrefs.SetFloat("set_volEnemy", VolumeEnemy);
        PlayerPrefs.SetFloat("set_volUI", VolumeUI);
        PlayerPrefs.SetInt("set_crossStyle", CrosshairStyle);
        PlayerPrefs.SetInt("set_crossColor", CrosshairColor);
        PlayerPrefs.SetInt("set_crossSize", CrosshairSize);
        PlayerPrefs.Save();
    }

    // 소리 종류별 음량 (0~1)
    public static float CategoryVolume(SoundCategory c)
    {
        switch (c)
        {
            case SoundCategory.Ambient: return VolumeAmbient;
            case SoundCategory.Enemy: return VolumeEnemy;
            case SoundCategory.UI: return VolumeUI;
            default: return VolumeEffects;
        }
    }

    // 파티클·효과 개수 배율 (세부 묘사 품질). Vfx와 씬 파티클이 같이 쓴다
    public static float ParticleScale => DetailQ == 0 ? 0.3f : DetailQ == 1 ? 0.6f : 1f;

    // 화면 모드, FPS 제한, 음량, 렌더 해상도처럼 씬과 상관없는 설정 (URP 설정 파일·품질 설정·텍스처 단계)
    public static void ApplyGlobal()
    {
        QualitySettings.vSyncCount = VSync ? 1 : 0;
        int fps = FpsOptions[Mathf.Clamp(FpsIndex, 0, FpsOptions.Length - 1)];
        Application.targetFrameRate = fps == 0 ? -1 : fps;
        if (!Application.isEditor)
            Screen.fullScreenMode = Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        // 전체 음량을 100%로 둬도 실제 출력은 81% (처음 시작한 사람이 놀라지 않게 10%씩 두 번 낮춤, 사용자 요청)
        AudioListener.volume = Volume * 0.81f;

        var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        SaveOriginals(urp);

        // 텍스처 품질: 낮음 1/4, 중간 1/2 (예전 '낮음'), 높음 원본, 매우 높음 원본 + 고해상도 묶음.
        // UI 그림(아이템 아이콘, 등급 배경)은 들여오기 설정에서 'Ignore Mipmap Limit'을 켜 두어 늘 원본으로 그린다
        QualitySettings.globalTextureMipmapLimit = TextureQ == 0 ? 2 : TextureQ == 1 ? 1 : 0;
        if (TextureMipmapLimitGroups.HasGroup(HighResGroup))
        {
            if (!highResSaved) { originalHighRes = QualitySettings.GetTextureMipmapLimitSettings(HighResGroup); highResSaved = true; }
            QualitySettings.SetTextureMipmapLimitSettings(HighResGroup, new TextureMipmapLimitSettings { limitBiasMode = TextureMipmapLimitBiasMode.OffsetGlobalLimit, limitBias = TextureQ >= 3 ? 0 : 1 });
        }

        // 이방성 필터링: 끔이면 꺼 두고, 그 밖에는 모든 텍스처에 그 배수를 강제로 (비스듬히 보이는 도로·바닥 무늬가 덜 번진다)
        if (Aniso <= 0) QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
        else
        {
            int lv = 1 << Aniso; // 2·4·8·16
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
            Texture.SetGlobalAnisotropicFilteringLimits(lv, lv);
        }

        // 세부 묘사 품질: 거리별 판 바꾸기(LOD 묶음). 낮을수록 가까이서 아래 판으로 내려가고, 낮음은 가장 좋은 판(LOD0)을 쓰지 않는다 (감염자 모델 LOD 3단계 등 모든 묶음).
        // 도시 Tripo 소품 8종의 '품질별 판'은 묶음마다 QualityLod가 따로 고른다 (ApplyScene). 전체 설정으로 하면 감염자 LOD까지 같이 내려가서 (11-23 검수)
        QualitySettings.lodBias = DetailQ == 0 ? 1f : DetailQ == 1 ? 1.5f : DetailQ == 2 ? 2f : 2.5f;
        QualitySettings.maximumLODLevel = DetailQ == 0 ? 1 : 0;

        if (urp != null)
        {
            urp.renderScale = RenderScale;
            urp.shadowDistance = DetailQ == 0 ? 15f : DetailQ == 1 ? 25f : DetailQ == 2 ? 40f : 60f; // 매우 높음: 그림자를 더 멀리까지
            // 그림자 단계(캐스케이드)·해상도: 낮음 1단계 1024, 중간 2단계 2048, 높음 4단계 2048(원래 값), 매우 높음 4단계 4096
            urp.shadowCascadeCount = DetailQ == 0 ? 1 : DetailQ == 1 ? 2 : 4;
            urp.mainLightShadowmapResolution = DetailQ == 0 ? 1024 : DetailQ == 3 ? 4096 : 2048;
            // UI 품질 낮음: 핫바 유리가 뒤 화면을 비추지 않아 화면 복사(불투명 텍스처)를 만들지 않는다
            urp.supportsCameraOpaqueTexture = UiQ >= 1;
            // 선명도 향상(실험) = STP 업스케일러 (MSAA와 같이 쓸 수 없어 MSAA를 끈다)
            urp.upscalingFilter = SharpenExp ? UpscalingFilterSelection.STP : originalUpscale;
            urp.msaaSampleCount = SharpenExp ? 1 : AntiAlias == 3 ? 2 : AntiAlias == 4 ? 4 : AntiAlias == 5 ? 8 : 1;
            // 물질 품질 중간부터 주변 그림자(SSAO). 낮음은 끈다 (가장 무거운 후처리 중 하나)
            foreach (var rd in urp.rendererDataList)
            {
                if (rd == null) continue;
                foreach (var f in rd.rendererFeatures)
                    if (f != null && f.GetType().Name == "ScreenSpaceAmbientOcclusion") f.SetActive(MaterialQ >= 1);
            }
        }
        SharpenPass.Set(Sharpen, 0.5f);
    }

    static void SaveOriginals(UniversalRenderPipelineAsset urp)
    {
        if (assetsSaved) return;
        assetsSaved = true;
        originalTextureLimit = QualitySettings.globalTextureMipmapLimit;
        originalAniso = QualitySettings.anisotropicFiltering;
        originalLodBias = QualitySettings.lodBias;
        originalMaxLod = QualitySettings.maximumLODLevel;
        if (urp == null) return;
        originalRenderScale = urp.renderScale;
        originalShadowDistance = urp.shadowDistance;
        originalCascades = urp.shadowCascadeCount;
        originalShadowRes = urp.mainLightShadowmapResolution;
        originalOpaque = urp.supportsCameraOpaqueTexture;
        originalUpscale = urp.upscalingFilter;
        originalMsaa = urp.msaaSampleCount;
        originalSsao = true;
        foreach (var rd in urp.rendererDataList)
        {
            if (rd == null) continue;
            foreach (var f in rd.rendererFeatures)
                if (f != null && f.GetType().Name == "ScreenSpaceAmbientOcclusion") originalSsao = f.isActive;
        }
    }

    // 에디터에서 플레이를 끝낼 때 URP 설정 파일과 품질·텍스처 단계를 원래대로 돌린다
    public static void RestoreAssets()
    {
        SharpenPass.Release();
        Texture.SetGlobalAnisotropicFilteringLimits(9, 16); // Unity 기본 한계 (강제 켬일 때 최소 9x, 최대 16x)
        if (!assetsSaved) return;
        QualitySettings.globalTextureMipmapLimit = originalTextureLimit;
        QualitySettings.anisotropicFiltering = originalAniso;
        QualitySettings.lodBias = originalLodBias;
        QualitySettings.maximumLODLevel = originalMaxLod;
        if (highResSaved && TextureMipmapLimitGroups.HasGroup(HighResGroup)) QualitySettings.SetTextureMipmapLimitSettings(HighResGroup, originalHighRes);
        if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
        {
            urp.renderScale = originalRenderScale;
            urp.shadowDistance = originalShadowDistance;
            urp.shadowCascadeCount = originalCascades;
            urp.mainLightShadowmapResolution = originalShadowRes;
            urp.supportsCameraOpaqueTexture = originalOpaque;
            urp.upscalingFilter = originalUpscale;
            urp.msaaSampleCount = originalMsaa;
            foreach (var rd in urp.rendererDataList)
            {
                if (rd == null) continue;
                foreach (var f in rd.rendererFeatures)
                    if (f != null && f.GetType().Name == "ScreenSpaceAmbientOcclusion") f.SetActive(originalSsao);
            }
        }
    }

    // 씬이 열릴 때 그 씬의 원래 밝기·그림자 값을 기억해 둔다
    public static void CaptureScene()
    {
        baseAmbient = RenderSettings.ambientLight;
        var volume = Object.FindAnyObjectByType<Volume>();
        profile = volume != null ? volume.profile : null; // 실행 중 복사본이라 원본 파일은 바뀌지 않는다
        baseExposure = profile != null && profile.TryGet<ColorAdjustments>(out var ca) ? ca.postExposure.value : 0f;

        lightShadows.Clear();
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            lightShadows[l] = l.shadows;
        particleRates.Clear();
        foreach (var ps in Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
            particleRates[ps] = ps.emission.rateOverTimeMultiplier;

        // 씬에 미리 놓인 소리(웅웅거림·라디오 잡음·문 등)는 종류를 따로 정하지 않았으면 환경음으로 친다
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        foreach (var src in Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (src.gameObject.scene == scene && !AudioCategory.IsTagged(src)) AudioCategory.Tag(src, SoundCategory.Ambient);
    }

    public static void ApplyScene()
    {
        RenderSettings.ambientLight = baseAmbient * Brightness;
        if (profile != null)
        {
            if (profile.TryGet<ColorAdjustments>(out var ca)) ca.postExposure.value = baseExposure + Mathf.Log(Brightness, 2f);
            if (profile.TryGet<Bloom>(out var bloom)) bloom.active = Bloom;
            if (profile.TryGet<FilmGrain>(out var grain)) grain.active = FilmGrain;
            if (profile.TryGet<ChromaticAberration>(out var chroma)) chroma.active = Distortion;
            if (profile.TryGet<LensDistortion>(out var lens)) lens.active = Distortion;
            if (profile.TryGet<Vignette>(out var vig)) vig.active = Vignette;
        }

        // 그림자 끔: 전부 없음 / 켬: 물질 품질 낮음·중간은 점광원(전구·불) 그림자를 끄고(예전 '중간'과 같다), 낮음은 부드러운 그림자를 단단한 그림자로
        foreach (var kv in lightShadows)
        {
            if (kv.Key == null) continue;
            bool keep = Shadows && (MaterialQ >= 2 || kv.Key.type != LightType.Point);
            var mode = keep ? kv.Value : LightShadows.None;
            if (mode == LightShadows.Soft && MaterialQ == 0) mode = LightShadows.Hard;
            kv.Key.shadows = mode;
        }
        float particleScale = ParticleScale;
        foreach (var kv in particleRates)
        {
            if (kv.Key == null) continue;
            var em = kv.Key.emission;
            em.rateOverTimeMultiplier = kv.Value * particleScale;
        }
        FacadeDetail.ApplyAll(DetailQ); // 낮음: 도시 아파트 4층부터 창틀·유리·난간을 카드로 (중간 이상은 원래 메시)
        QualityLod.ApplyAll(DetailQ);   // 도시 Tripo 소품: 가까이서 쓰는 가장 좋은 판 = 세부 묘사 품질 (낮음 판 ~ 매우 높음 판)

        var cam = Camera.main;
        if (cam != null)
        {
            cam.fieldOfView = Fov;
            var data = cam.GetUniversalAdditionalCameraData();
            bool msaaChoice = AntiAlias >= 3;
            data.antialiasing = SharpenExp ? AntialiasingMode.TemporalAntiAliasing
                : AntiAlias == 1 ? AntialiasingMode.FastApproximateAntialiasing
                : AntiAlias == 2 || (msaaChoice && !cam.allowMSAA) ? AntialiasingMode.SubpixelMorphologicalAntiAliasing // MSAA를 끈 카메라(컷씬)는 SMAA로
                : AntialiasingMode.None; // MSAA는 URP 설정의 표본 수로 (ApplyGlobal)
        }
    }
}
