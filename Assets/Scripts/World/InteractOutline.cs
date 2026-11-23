using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// 화면 가운데로 보고 있는 상호작용 물체(제작대·화로·아이템 등)의 윤곽선을 은은하게 빛낸다.
// 물체(Interactable)에 딸린 모든 메시에 살짝 부풀린 껍데기(셰이더 BunkerLab/InteractOutline)를 붙여 두고,
// 볼 때만 켠다. 제작대처럼 탁자·공구가 붙은 물체는 어디를 보든 전체가 함께 빛난다.
// PlayerInteractor가 매 프레임 Show(지금 보는 것)를 부른다
public class InteractOutline : MonoBehaviour
{
    const float Width = 0.01f;
    static readonly Color Glow = new Color(1f, 0.78f, 0.42f);

    static Material material;
    static InteractOutline shown;
    static readonly int CenterId = Shader.PropertyToID("_Center");
    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int WidthId = Shader.PropertyToID("_Width");

    readonly List<GameObject> shells = new List<GameObject>();
    int lodKey;
    float fade;

    // target이 null이면 윤곽선을 끈다
    public static void Show(Interactable target)
    {
        InteractOutline next = null;
        if (target != null)
        {
            next = target.GetComponent<InteractOutline>();
            if (next == null) next = target.gameObject.AddComponent<InteractOutline>();
        }
        if (next == shown) return;
        if (shown != null) shown.SetOn(false);
        shown = next;
        if (shown != null) shown.SetOn(true);
    }

    void SetOn(bool on)
    {
        // 품질별 판(LOD 묶음)을 든 물체: 설정에서 세부 묘사 품질을 바꿔 첫 칸의 판이 달라졌으면 껍데기를 새로 붙인다
        if (on && shells.Count > 0 && LodKey(null) != lodKey)
        {
            foreach (var s in shells) if (s != null) Destroy(s);
            shells.Clear();
        }
        if (on && shells.Count == 0) Build();
        foreach (var s in shells)
            if (s != null) s.SetActive(on);
        fade = 0;
        enabled = on;
    }

    // 물체의 메시마다 같은 모양의 껍데기를 자식으로 붙인다 (정적 배칭으로 합쳐진 메시는 건너뛴다)
    void Build()
    {
        var mat = Material;
        if (mat == null) return;
        var block = new MaterialPropertyBlock();
        var farTiers = new HashSet<Renderer>();
        lodKey = LodKey(farTiers);
        foreach (var mf in GetComponentsInChildren<MeshFilter>())
        {
            var mesh = mf.sharedMesh;
            var r = mf.GetComponent<MeshRenderer>();
            if (mesh == null || r == null || !r.enabled || mesh.name.StartsWith("Combined Mesh") || farTiers.Contains(r)) continue;
            var go = new GameObject("Outline");
            go.transform.SetParent(mf.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var shell = go.AddComponent<MeshRenderer>();
            shell.sharedMaterials = Repeat(mat, mesh.subMeshCount);
            shell.shadowCastingMode = ShadowCastingMode.Off;
            shell.receiveShadows = false;
            shell.lightProbeUsage = LightProbeUsage.Off;
            shell.reflectionProbeUsage = ReflectionProbeUsage.Off;
            block.SetVector(CenterId, mesh.bounds.center);
            shell.SetPropertyBlock(block);
            go.SetActive(false);
            shells.Add(go);
        }
    }

    // 품질별 판(LOD 묶음, 방공호 가구 Tripo 모델 2026-11-23)은 첫 칸(LOD 0)의 판에만 껍데기를 붙인다.
    // 상호작용 거리(2.6 m)에서는 늘 첫 칸이 보이는데, 다른 칸의 판(켜져 있지만 LOD가 그리지 않음)에도 붙이면 껍데기는 LOD 밖이라 늘 그려져
    // 판 수만큼 겹친 윤곽선이 된다. far에 첫 칸이 아닌 판을 모으고, 첫 칸 판들로 만든 값(바뀌었는지 보는 용도)을 돌려준다
    int LodKey(HashSet<Renderer> far)
    {
        int key = 17;
        foreach (var lg in GetComponentsInChildren<LODGroup>())
        {
            var lods = lg.GetLODs();
            for (int l = 0; l < lods.Length; l++)
                foreach (var r in lods[l].renderers)
                {
                    if (r == null) continue;
                    if (l == 0) key = key * 31 + r.GetInstanceID();
                    else if (far != null) far.Add(r);
                }
        }
        if (far != null)
            foreach (var lg in GetComponentsInChildren<LODGroup>())
                foreach (var r in lg.GetLODs()[0].renderers) far.Remove(r); // 첫 칸과 다른 칸에 같은 판이 함께 들어 있으면(낮음 품질) 그 판은 남긴다
        return key;
    }

    static Material[] Repeat(Material m, int n)
    {
        var list = new Material[Mathf.Max(1, n)];
        for (int i = 0; i < list.Length; i++) list[i] = m;
        return list;
    }

    static Material Material
    {
        get
        {
            if (material == null)
            {
                var shader = Resources.Load<Shader>("Shaders/InteractOutline");
                if (shader == null) return null;
                material = new Material(shader) { name = "InteractOutline (runtime)" };
                material.SetFloat(WidthId, Width);
            }
            return material;
        }
    }

    // 켜질 때 0.15초 동안 밝아지고, 켜져 있는 동안 천천히 숨 쉬듯 밝기가 오르내린다 (켜진 것은 하나뿐이라 공용 재질을 바꾼다)
    void Update()
    {
        fade = Mathf.Min(1f, fade + Time.unscaledDeltaTime / 0.15f);
        float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 3.2f);
        var c = Glow * (1.4f * pulse);
        c.a = 0.85f * fade;
        if (material != null) material.SetColor(ColorId, c);
    }

    void OnDestroy()
    {
        if (shown == this) shown = null;
    }
}
