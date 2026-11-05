using UnityEngine;

// 발광 재질 보강: 재질 파일에는 _EMISSION 키워드가 있어도 에디터가 재질을 다시 불러온 뒤 메모리에서 키워드가 빠지는 일이 있어
// (발전기 철창 안 불빛·화로 불·전구가 그냥 어두운 판으로 보인다), 장치가 시작할 때 발광색이 있는 재질의 키워드를 켠다.
// 빌드에서는 파일대로 불러오므로 아무것도 바꾸지 않는다
public static class EmissionFix
{
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    // root 아래 모든 렌더러의 공유 재질 가운데 발광색이 검정이 아닌데 키워드가 꺼진 것을 켠다
    public static void Ensure(Transform root)
    {
        if (root == null) return;
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            var m = r.sharedMaterial;
            if (m == null || !m.HasProperty(EmissionId) || m.IsKeywordEnabled("_EMISSION")) continue;
            var c = m.GetColor(EmissionId);
            if (c.r + c.g + c.b > 0.001f) m.EnableKeyword("_EMISSION");
        }
    }
}
