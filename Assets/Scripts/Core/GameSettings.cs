using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// 설정 메뉴 값 (PlayerPrefs에 저장). 저사양 PC용 품질 조절도 여기서 한다.
public static class GameSettings
{
    public static readonly string[] QualityNames = { "낮음", "중간", "높음" };
    public static readonly int[] FpsOptions = { 30, 60, 120, 144, 0 };
    // 조준점: 모양·색·크기
    public static readonly string[] CrosshairStyleNames = { "점", "십자", "십자 + 점", "원", "원 + 점", "없음" };
    public static readonly string[] CrosshairColorNames = { "흰색", "초록", "노랑", "빨강", "하늘색" };
    public static readonly Color[] CrosshairColors =
    {
        new Color(1f, 1f, 1f), new Color(0.45f, 1f, 0.45f), new Color(1f, 0.88f, 0.3f), new Color(1f, 0.35f, 0.3f), new Color(0.45f, 0.85f, 1f),
    };
    public static readonly string[] CrosshairSizeNames = { "작게", "보통", "크게" };

    public static int Quality;        // 0 낮음, 1 중간, 2 높음
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
    static float originalRenderScale = -1f, originalShadowDistance;

    public static void Load()
    {
        Quality = PlayerPrefs.GetInt("set_quality", 2);
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

    // 화면 모드, FPS 제한, 음량, 렌더 해상도처럼 씬과 상관없는 설정
    public static void ApplyGlobal()
    {
        QualitySettings.vSyncCount = VSync ? 1 : 0;
        int fps = FpsOptions[Mathf.Clamp(FpsIndex, 0, FpsOptions.Length - 1)];
        Application.targetFrameRate = fps == 0 ? -1 : fps;
        if (!Application.isEditor)
            Screen.fullScreenMode = Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        // 전체 음량을 100%로 둬도 실제 출력은 81% (처음 시작한 사람이 놀라지 않게 10%씩 두 번 낮춤, 사용자 요청)
        AudioListener.volume = Volume * 0.81f;

        if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
        {
            if (originalRenderScale < 0)
            {
                originalRenderScale = urp.renderScale;
                originalShadowDistance = urp.shadowDistance;
            }
            urp.renderScale = RenderScale;
            urp.shadowDistance = Quality == 0 ? 15f : Quality == 1 ? 25f : 40f;
        }
    }

    // 에디터에서 플레이를 끝낼 때 URP 설정 파일을 원래대로 돌린다
    public static void RestoreAssets()
    {
        if (originalRenderScale < 0) return;
        if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
        {
            urp.renderScale = originalRenderScale;
            urp.shadowDistance = originalShadowDistance;
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
            if (profile.TryGet<Bloom>(out var bloom)) bloom.active = Quality >= 1;
            if (profile.TryGet<FilmGrain>(out var grain)) grain.active = Quality >= 1;
            if (profile.TryGet<ChromaticAberration>(out var chroma)) chroma.active = Quality >= 2;
        }

        // 낮음: 그림자 없음 / 중간: 점광원(전구·불) 그림자만 끔 / 높음: 전부
        foreach (var kv in lightShadows)
        {
            if (kv.Key == null) continue;
            bool keep = Quality == 2 || (Quality == 1 && kv.Key.type != LightType.Point);
            kv.Key.shadows = keep ? kv.Value : LightShadows.None;
        }
        float particleScale = Quality == 0 ? 0.3f : Quality == 1 ? 0.6f : 1f;
        foreach (var kv in particleRates)
        {
            if (kv.Key == null) continue;
            var em = kv.Key.emission;
            em.rateOverTimeMultiplier = kv.Value * particleScale;
        }

        var cam = Camera.main;
        if (cam != null)
        {
            cam.fieldOfView = Fov;
            var data = cam.GetUniversalAdditionalCameraData();
            data.antialiasing = Quality == 0 ? AntialiasingMode.None : Quality == 1 ? AntialiasingMode.FastApproximateAntialiasing : AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        }
    }
}
