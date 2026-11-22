using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

// 설정 > 그래픽 > '선명도 향상' (2026-11-22). 렌더러 설정 파일을 건드리지 않고, 켜져 있는 동안만 화면에 그리는 게임 카메라마다
// 날카롭게 하기 패스(Resources/Shaders/Sharpen.shader)를 끼워 넣는다 (URP '스크립트로 패스 넣기' 방식, 렌더 그래프).
// 후처리 앞(BeforeRenderingPostProcessing)에서 한다: 필름 그레인·디더링·FXAA·업스케일이 그 뒤에 오므로 그레인 노이즈를 키우지 않는다 (11-22 검수)
public static class SharpenPass
{
    class Pass : ScriptableRenderPass
    {
        public Material mat;

        public Pass()
        {
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            requiresIntermediateTexture = true;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (mat == null) return;
            var res = frameData.Get<UniversalResourceData>();
            if (res.isActiveTargetBackBuffer) return;
            var src = res.activeColorTexture;
            var desc = renderGraph.GetTextureDesc(src);
            desc.name = "SharpenColor";
            desc.clearBuffer = false;
            var dst = renderGraph.CreateTexture(desc);
            renderGraph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(src, dst, mat, 0), "Sharpen");
            res.cameraColor = dst;
        }
    }

    static Pass pass;
    static Material mat;
    static bool hooked;
    static float amount = 0.5f;

    // on: 켤지, strength: 0~1 세기
    public static void Set(bool on, float strength = 0.5f)
    {
        amount = Mathf.Clamp01(strength);
        if (on && mat == null)
        {
            var shader = Resources.Load<Shader>("Shaders/Sharpen");
            if (shader == null || !shader.isSupported)
            {
                Debug.LogWarning("[SharpenPass] BunkerLab/Sharpen 셰이더를 쓸 수 없어 선명도 향상을 끈다.");
                on = false;
            }
            else
            {
                mat = new Material(shader) { name = "Sharpen", hideFlags = HideFlags.HideAndDontSave };
                pass = new Pass { mat = mat };
            }
        }
        if (mat != null) mat.SetFloat("_Amount", amount);
        if (on && !hooked) { RenderPipelineManager.beginCameraRendering += OnBeginCamera; hooked = true; }
        else if (!on && hooked) { RenderPipelineManager.beginCameraRendering -= OnBeginCamera; hooked = false; }
    }

    // 끄고 재질까지 지운다 (에디터에서 플레이를 끝낼 때, GameSettings.RestoreAssets)
    public static void Release()
    {
        Set(false);
        if (mat != null)
        {
            if (Application.isPlaying) Object.Destroy(mat); else Object.DestroyImmediate(mat);
        }
        mat = null;
        pass = null;
    }

    static void OnBeginCamera(ScriptableRenderContext context, Camera cam)
    {
        // 화면에 그리는 게임 카메라만: 아이템 아이콘·흐림 캡처처럼 렌더 텍스처에 그리는 카메라는 뺀다
        if (cam == null || cam.cameraType != CameraType.Game || cam.targetTexture != null || pass == null) return;
        var data = cam.GetUniversalAdditionalCameraData();
        if (data == null || data.renderType != CameraRenderType.Base || data.scriptableRenderer == null) return;
        data.scriptableRenderer.EnqueuePass(pass);
    }
}
