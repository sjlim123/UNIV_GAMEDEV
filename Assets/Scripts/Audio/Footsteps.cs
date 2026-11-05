using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// 플레이어 발소리 (실제 녹음, Resources/Audio/Footsteps/<바닥>/*.ogg). 발밑 바닥에 따라 고른다:
//  stone    방공호·복도·터널 콘크리트, 도시 보도 — Fantozzi's Footsteps (OpenGameArt, CC0)
//  boots    도시 아스팔트 (군화) — swuing footstep-concrete (OpenGameArt 묶음, CC-BY 3.0)
//  interior 건물 안 바닥 — Kenney Impact Sounds footstep_concrete (CC0)
//  gravel   잔해·자갈 — Ali_6868 Gravel Footsteps (CC0)
//  sand     흙·재 — Fantozzi (CC0)
//  water    웅덩이 — EminYILDIRIM Water Footsteps (CC-BY 3.0)
// 같은 소리가 연달아 나지 않게 고르고, 클립이 없으면 예전 합성 발소리로 돌아간다. 출처·라이선스는 Credits/외부_에셋_목록.json
public static class Footsteps
{
    public enum Surface { Stone, Asphalt, Interior, Gravel, Sand, Water }

    static readonly string[] Folders = { "stone", "boots", "interior", "gravel", "sand", "water" };
    static readonly Dictionary<Surface, AudioClip[]> clips = new Dictionary<Surface, AudioClip[]>();
    static readonly Dictionary<Surface, int> last = new Dictionary<Surface, int>();
    static readonly RaycastHit[] hits = new RaycastHit[16];

    static AudioClip[] Load(Surface s)
    {
        if (!clips.TryGetValue(s, out var arr))
        {
            arr = Resources.LoadAll<AudioClip>("Audio/Footsteps/" + Folders[(int)s]);
            clips[s] = arr;
        }
        return arr;
    }

    // 바닥에 맞는 걸음 소리 하나 (바로 앞 것과는 다르게). 클립이 없으면 합성 발소리
    public static AudioClip Clip(Surface s)
    {
        var arr = Load(s);
        if (arr == null || arr.Length == 0) return s == Surface.Water ? ProceduralAudio.Splash() : ProceduralAudio.Footstep();
        int prev = last.TryGetValue(s, out var p) ? p : -1;
        int i = Random.Range(0, arr.Length);
        if (arr.Length > 1 && i == prev) i = (i + 1 + Random.Range(0, arr.Length - 1)) % arr.Length;
        last[s] = i;
        return arr[i];
    }

    // 발밑 바닥. 웅덩이 → 물. 도시 씬이 아니면(방공호·터널) 돌. 도시에서는 발밑 물체의 이름·재질로:
    // Ground(아스팔트) → 군화, Sidewalk·콘크리트 바닥 → 돌, 건물 Floor → 건물 안, 잔해·자갈 조각 → 자갈, 흙·재·모래 → 모래
    public static Surface SurfaceUnder(Vector3 pos, Transform self = null)
    {
        if (PuddleFx.Inside(pos)) return Surface.Water;
        if (SceneManager.GetActiveScene().name != "City") return Surface.Stone;
        int n = Physics.RaycastNonAlloc(pos + Vector3.up * 0.3f, Vector3.down, hits, 1.3f, ~0, QueryTriggerInteraction.Ignore);
        Collider ground = null;
        float best = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            var h = hits[i];
            if (self != null && h.collider.transform.IsChildOf(self)) continue;
            if (h.distance < best) { best = h.distance; ground = h.collider; }
        }
        if (ground == null) return Surface.Asphalt;
        string name = ground.name;
        var r = ground.GetComponent<Renderer>();
        string mat = r != null && r.sharedMaterial != null ? r.sharedMaterial.name : "";
        if (name.StartsWith("Debris") || name.StartsWith("Rubble") || name == "Chunk" || name == "Slab" || name.StartsWith("Gravel") || mat.StartsWith("M_AshPile") || mat.StartsWith("M_Scorch") || mat.StartsWith("M_Soot"))
            return Surface.Gravel;
        if (name.StartsWith("Dirt") || name.StartsWith("Soil") || name.StartsWith("Sand") || name.StartsWith("Mud") || name.StartsWith("AshGround"))
            return Surface.Sand;
        if (mat.StartsWith("M_WetConcrete")) return Surface.Stone;
        if (name == "Floor" || mat.StartsWith("M_WoodPlanks")) return Surface.Interior;
        if (name == "Sidewalk" || mat.StartsWith("M_ConcreteFloor") || mat.StartsWith("M_DirtyConcrete")) return Surface.Stone;
        return Surface.Asphalt;
    }
}
