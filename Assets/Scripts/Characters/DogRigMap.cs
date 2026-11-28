using System.Collections.Generic;
using UnityEngine;

// Tripo 'Other' 자동 리깅 들개의 뼈 찾기 (2026-11-28, 사용자 "유일 동물이다보니 리깅에 매우 체계적으로").
// Tripo가 붙인 뼈 이름은 믿지 않는다: 개 b의 앞오른다리가 'tripo::Spine_4…', 개 a의 꼬리 끝이 '1_Right_Limb_0', 뒷오른다리가 '1_Left_Limb…'로 나왔다.
// 그래서 생김새로 찾는다 (Blender 점검 스크립트 ModelWork/Enemies/dog/tripo/scripts/dog_rig.py와 같은 규칙, 두 개 모두 맞게 나옴):
//   다리   = 끝뼈가 바닥 가까이(키 20% 아래) 있는 사슬 4개. 사슬 = 끝뼈에서 올라가다 부모 뼈의 자식이 둘 이상이 되기 직전까지.
//            앞·뒤·왼·오는 발끝 위치로 가른다 (사슬 꼭대기는 몸 가운데에서 시작할 때가 있다).
//   흔드는 뼈(게임 Leg) = 뒷다리는 사슬 첫 뼈(엉덩이), 앞다리는 뒷다리 꼭대기 높이에 가장 가까운 관절 (개 a 앞다리는 등 위 어깨뼈부터 시작한다)
//   무릎(게임 Knee) = 다리 꼭대기 높이의 앞 18%(손목)·뒤 33%(발목)에 가장 가까운 관절
//   머리   = 가장 높은 끝뼈(귀)의 사슬이 갈라져 나온 뼈, 목 = 앞다리가 갈라진 등뼈의 자식 중 머리로 가는 뼈
//   꼬리   = 다리·머리가 아닌 끝뼈 중 가장 뒤의 것의 사슬. 흔드는 뼈는 둘째 마디 (첫 마디는 엉덩이 살을 잡고 있다)
//   몸     = 뿌리뼈. 그 밖의 뼈(가슴·배·목 아래에 매달린 짧은 뼈들)는 몸에 붙어 따라간다. 턱뼈는 없다.
public class DogRigMap
{
    public Transform Root, Neck, Head, TailDrive;
    public readonly List<Transform> Tail = new List<Transform>();
    public readonly Transform[] Pivot = new Transform[4], Knee = new Transform[4]; // 앞왼·앞오·뒤왼·뒤오 (Animal.legs 순서)
    public readonly List<Transform>[] Chains = new List<Transform>[4];
    public readonly HashSet<Transform> HeadBranch = new HashSet<Transform>();      // 목 밑동 아래 전부
    public Vector3 Fwd, Up;
    public float H, Ground;
    public string Error;

    public static DogRigMap Find(Transform[] bones, Vector3 up)
    {
        var m = new DogRigMap { Up = up.normalized };
        var set = new HashSet<Transform>(bones);
        Transform ParentBone(Transform t) { var p = t.parent; while (p != null && !set.Contains(p)) p = p.parent; return p; }
        var kids = new Dictionary<Transform, List<Transform>>();
        foreach (var b in bones) kids[b] = new List<Transform>();
        foreach (var b in bones) { var p = ParentBone(b); if (p != null) kids[p].Add(b); }
        float Ht(Transform t) => Vector3.Dot(t.position, m.Up);
        bool IsAnc(Transform a, Transform b) { while (b != null) { if (b == a) return true; b = ParentBone(b); } return false; }
        List<Transform> ChainUp(Transform leaf)
        {
            var c = new List<Transform> { leaf };
            var b = leaf;
            while (true) { var p = ParentBone(b); if (p == null || kids[p].Count >= 2) break; b = p; c.Add(b); }
            c.Reverse();
            return c;
        }

        m.Ground = float.MaxValue; float top = float.MinValue;
        foreach (var b in bones) { m.Ground = Mathf.Min(m.Ground, Ht(b)); top = Mathf.Max(top, Ht(b)); }
        m.H = top - m.Ground;
        var leaves = new List<Transform>();
        foreach (var b in bones) if (kids[b].Count == 0) leaves.Add(b);

        // 다리 사슬
        var legs = new List<List<Transform>>();
        foreach (var lf in leaves) if (Ht(lf) - m.Ground < 0.2f * m.H) legs.Add(ChainUp(lf));
        if (legs.Count != 4) { m.Error = "바닥에 닿는 사슬 " + legs.Count + "개"; return m; }
        var legBones = new HashSet<Transform>(); foreach (var c in legs) foreach (var b in c) legBones.Add(b);

        // 머리: 가장 높은 (다리 아닌) 끝뼈 = 귀. 귀 사슬 꼭대기의 부모
        Transform ear = null;
        foreach (var lf in leaves) if (!legBones.Contains(lf) && (ear == null || Ht(lf) > Ht(ear))) ear = lf;
        var earTop = ChainUp(ear)[0];
        m.Head = ParentBone(earTop);
        if (m.Head == null) { m.Error = "머리 없음"; return m; }

        // 앞쪽 = 머리 쪽 (수평)
        var mid = Vector3.zero; foreach (var c in legs) mid += c[0].position; mid /= 4f;
        m.Fwd = Vector3.ProjectOnPlane(m.Head.position - mid, m.Up).normalized;
        var right = Vector3.Cross(m.Up, m.Fwd);
        var mid2 = Vector3.zero; foreach (var c in legs) mid2 += c[c.Count - 1].position; mid2 /= 4f;
        foreach (var c in legs)
        {
            var p = c[c.Count - 1].position - mid2;
            int i = (Vector3.Dot(p, m.Fwd) > 0 ? 0 : 2) + (Vector3.Dot(p, right) > 0 ? 1 : 0);
            if (m.Chains[i] != null) { m.Error = "앞뒤좌우가 겹친다"; return m; }
            m.Chains[i] = c;
        }

        // 목 밑동
        Transform frontParent = null;
        foreach (var c in legs)
        {
            var p = ParentBone(c[0]);
            if (p != null && IsAnc(p, m.Head) && (frontParent == null || IsAnc(frontParent, p))) frontParent = p;
        }
        m.Neck = null;
        if (frontParent != null) foreach (var k in kids[frontParent]) if (IsAnc(k, m.Head)) { m.Neck = k; break; }
        if (m.Neck == null || m.Neck == m.Head) m.Neck = ParentBone(m.Head);
        void Sub(Transform b) { m.HeadBranch.Add(b); foreach (var k in kids[b]) Sub(k); }
        Sub(m.Neck);

        // 꼬리
        Transform tailLeaf = null;
        foreach (var lf in leaves)
            if (!legBones.Contains(lf) && !m.HeadBranch.Contains(lf) && (tailLeaf == null || Vector3.Dot(lf.position, m.Fwd) < Vector3.Dot(tailLeaf.position, m.Fwd))) tailLeaf = lf;
        if (tailLeaf != null) m.Tail.AddRange(ChainUp(tailLeaf));
        m.TailDrive = m.Tail.Count > 1 ? m.Tail[1] : m.Tail.Count > 0 ? m.Tail[0] : null;

        // 흔드는 뼈·무릎
        float hindTop = (Ht(m.Chains[2][0]) + Ht(m.Chains[3][0])) * 0.5f;
        for (int i = 0; i < 4; i++)
        {
            var c = m.Chains[i];
            if (i >= 2) m.Pivot[i] = c[0];
            else
            {
                Transform best = c[0];
                for (int j = 0; j < c.Count - 1; j++) if (Mathf.Abs(Ht(c[j]) - hindTop) < Mathf.Abs(Ht(best) - hindTop)) best = c[j];
                m.Pivot[i] = best;
            }
            float want = m.Ground + (Ht(c[0]) - m.Ground) * (i < 2 ? 0.18f : 0.33f);
            Transform kn = c[Mathf.Min(1, c.Count - 1)];
            for (int j = 1; j < c.Count; j++) if (Mathf.Abs(Ht(c[j]) - want) < Mathf.Abs(Ht(kn) - want)) kn = c[j];
            m.Knee[i] = kn;
        }
        m.Root = bones[0]; foreach (var b in bones) if (ParentBone(b) == null) { m.Root = b; break; }
        return m;
    }

    public string Describe()
    {
        if (Error != null) return "실패: " + Error;
        string N(Transform t) => t != null ? t.name : "-";
        var s = new System.Text.StringBuilder();
        s.Append("몸 ").Append(N(Root)).Append(", 목 ").Append(N(Neck)).Append(", 머리 ").Append(N(Head)).Append(", 꼬리 ").Append(N(TailDrive)).Append('(').Append(Tail.Count).Append("마디)");
        string[] nm = { "앞왼", "앞오", "뒤왼", "뒤오" };
        for (int i = 0; i < 4; i++) s.Append(", ").Append(nm[i]).Append(' ').Append(N(Pivot[i])).Append('/').Append(N(Knee[i]));
        return s.ToString();
    }
}
