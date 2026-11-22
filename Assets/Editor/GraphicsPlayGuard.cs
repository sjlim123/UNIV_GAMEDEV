using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// 에디터 전용 (2026-11-22, 그래픽 세부 옵션 검수): 플레이 중 GameSettings가 바꾸는 URP 설정 파일·렌더러 기능·품질 설정 값을
// 플레이를 시작하기 직전에 SessionState(도메인 다시 읽기에도 남는다)에 적어 두고, 편집 모드로 돌아오면 그대로 되돌린다.
// GameSettings.RestoreAssets도 되돌리지만, 플레이 중 스크립트가 다시 컴파일되면(도메인 다시 읽기) 그 쪽 기억이 지워져 아무것도 하지 않는다.
[InitializeOnLoad]
static class GraphicsPlayGuard
{
    const string Key = "BunkerLab.GraphicsPlayGuard";

    [System.Serializable]
    class Snapshot
    {
        public float renderScale, shadowDistance, lodBias;
        public int msaa, upscale, cascades, shadowRes, textureLimit, aniso, maxLod, vSync;
        public bool opaque, ssao, hasSsao, hasHighRes;
        public int highResMode, highResBias;
    }

    static GraphicsPlayGuard()
    {
        EditorApplication.playModeStateChanged += OnPlayMode;
    }

    static void OnPlayMode(PlayModeStateChange s)
    {
        if (s == PlayModeStateChange.ExitingEditMode) Capture();
        else if (s == PlayModeStateChange.EnteredEditMode) Restore();
    }

    static ScriptableRendererFeature Ssao(UniversalRenderPipelineAsset urp)
    {
        foreach (var rd in urp.rendererDataList)
        {
            if (rd == null) continue;
            foreach (var f in rd.rendererFeatures)
                if (f != null && f.GetType().Name == "ScreenSpaceAmbientOcclusion") return f;
        }
        return null;
    }

    static void Capture()
    {
        var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urp == null) { SessionState.EraseString(Key); return; }
        var snap = new Snapshot
        {
            renderScale = urp.renderScale, shadowDistance = urp.shadowDistance, msaa = urp.msaaSampleCount, upscale = (int)urp.upscalingFilter,
            cascades = urp.shadowCascadeCount, shadowRes = urp.mainLightShadowmapResolution, opaque = urp.supportsCameraOpaqueTexture,
            textureLimit = QualitySettings.globalTextureMipmapLimit, aniso = (int)QualitySettings.anisotropicFiltering,
            lodBias = QualitySettings.lodBias, maxLod = QualitySettings.maximumLODLevel, vSync = QualitySettings.vSyncCount,
        };
        var ssao = Ssao(urp);
        snap.hasSsao = ssao != null;
        snap.ssao = ssao != null && ssao.isActive;
        snap.hasHighRes = TextureMipmapLimitGroups.HasGroup("HighRes");
        if (snap.hasHighRes)
        {
            var hr = QualitySettings.GetTextureMipmapLimitSettings("HighRes");
            snap.highResMode = (int)hr.limitBiasMode; snap.highResBias = hr.limitBias;
        }
        SessionState.SetString(Key, JsonUtility.ToJson(snap));
    }

    static void Restore()
    {
        string json = SessionState.GetString(Key, "");
        if (string.IsNullOrEmpty(json)) return;
        SessionState.EraseString(Key);
        var snap = JsonUtility.FromJson<Snapshot>(json);
        var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urp != null)
        {
            if (urp.renderScale != snap.renderScale) urp.renderScale = snap.renderScale;
            if (urp.shadowDistance != snap.shadowDistance) urp.shadowDistance = snap.shadowDistance;
            if (urp.msaaSampleCount != snap.msaa) urp.msaaSampleCount = snap.msaa;
            if ((int)urp.upscalingFilter != snap.upscale) urp.upscalingFilter = (UpscalingFilterSelection)snap.upscale;
            if (urp.shadowCascadeCount != snap.cascades) urp.shadowCascadeCount = snap.cascades;
            if (urp.mainLightShadowmapResolution != snap.shadowRes) urp.mainLightShadowmapResolution = snap.shadowRes;
            if (urp.supportsCameraOpaqueTexture != snap.opaque) urp.supportsCameraOpaqueTexture = snap.opaque;
            var ssao = Ssao(urp);
            if (snap.hasSsao && ssao != null && ssao.isActive != snap.ssao) ssao.SetActive(snap.ssao);
        }
        if (QualitySettings.globalTextureMipmapLimit != snap.textureLimit) QualitySettings.globalTextureMipmapLimit = snap.textureLimit;
        if ((int)QualitySettings.anisotropicFiltering != snap.aniso) QualitySettings.anisotropicFiltering = (AnisotropicFiltering)snap.aniso;
        if (QualitySettings.lodBias != snap.lodBias) QualitySettings.lodBias = snap.lodBias;
        if (QualitySettings.maximumLODLevel != snap.maxLod) QualitySettings.maximumLODLevel = snap.maxLod;
        if (QualitySettings.vSyncCount != snap.vSync) QualitySettings.vSyncCount = snap.vSync;
        if (snap.hasHighRes && TextureMipmapLimitGroups.HasGroup("HighRes"))
            QualitySettings.SetTextureMipmapLimitSettings("HighRes", new TextureMipmapLimitSettings { limitBiasMode = (TextureMipmapLimitBiasMode)snap.highResMode, limitBias = snap.highResBias });
        Texture.SetGlobalAnisotropicFilteringLimits(9, 16);
    }
}
