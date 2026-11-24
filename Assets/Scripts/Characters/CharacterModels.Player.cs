using System.Collections.Generic;
using UnityEngine;

// 플레이어 모델 몸 (2026-11-24, 플레이어 모델 업그레이드 3단계 "컷씬"): 탐험 차림 시민(Civilian variant 1)을
// Tripo 플레이어 모델(후드·방독면·작업 장갑, 뼈 67개)로 짓는다. 쓰는 곳: 인트로 컷씬 장면 3, 방공호 해치 귀환 연출 (CutsceneActor).
// 집 차림(variant 0)은 얼굴 클로즈업·입 가리기가 있어 예전 관 몸 그대로 둔다 (사용자 11-24 "탐험 차림 장면만").
// 게임 관절(Body·Head·팔·다리)은 CutsceneActor의 규칙대로 쉬는 회전 = 팔다리가 -y로 곧게 내려간 모습으로 모델 뼈 자리에 세우고,
// SkinnedPlayerDriver가 매 프레임 관절 → 뼈로 옮긴다. 키는 눈이 1인칭 카메라 높이(1.65m + 발밑 틈)에 오게 맞춘다.
// 모델: Resources/Player/player_body.prefab (편집기 '플레이어 모델: 컷씬 몸 프리팹 만들기'). 없으면 예전 관 몸.
public static partial class CharacterModels
{
    public static bool UseSkinnedPlayer = true; // 끄면 예전 관 몸 (비교·문제 확인용)
    const float PlayerEyeH = 1.69f; // 서 있는 1인칭 눈 높이 1.65 + 캐릭터 컨트롤러 피부 0.04 (그림자 몸과 같다)
    static readonly GameObject[] playerBodyPrefab = new GameObject[2];
    static readonly bool[] playerBodyLoaded = new bool[2];
    // variant 0: 얼굴 있는 집 차림 (인트로 전체, 2026-11-24 사용자 "집 차림 하나로 도입 전부"), 1: 방독면 탐험 차림 (해치 귀환)
    static readonly string[] PlayerBodyRes = { "Player/player_civilian_body", "Player/player_body" };

    static bool SkinnedPlayer(Builder b, int variant)
    {
        if (!UseSkinnedPlayer || variant < 0 || variant > 1) return false;
        if (!playerBodyLoaded[variant]) { playerBodyLoaded[variant] = true; playerBodyPrefab[variant] = Resources.Load<GameObject>(PlayerBodyRes[variant]); }
        if (playerBodyPrefab[variant] == null) return false;
        var rig = b.Rig;
        var skin = Object.Instantiate(playerBodyPrefab[variant], rig.Model, false);
        skin.name = "Skin";
        skin.transform.localPosition = Vector3.zero; skin.transform.localRotation = Quaternion.identity; skin.transform.localScale = Vector3.one;
        var bones = new Dictionary<string, Transform>();
        foreach (var t in skin.GetComponentsInChildren<Transform>(true)) bones[t.name] = t;
        string[] need = { "Hips", "Spine", "Neck", "Head", "Left_Eye", "Right_Eye", "Left_UpperArm", "Left_LowerArm", "Left_Hand", "Right_UpperArm", "Right_LowerArm", "Right_Hand",
            "Left_UpperLeg", "Left_LowerLeg", "Left_Foot", "Right_UpperLeg", "Right_LowerLeg", "Right_Foot", "Left_MiddleProximal", "Right_MiddleProximal" };
        foreach (var n in need)
            if (!bones.ContainsKey(n)) { Object.Destroy(skin); return false; }

        // 키: 눈 높이를 맞춘다
        Vector3 P(string n) => rig.Model.InverseTransformPoint(bones[n].position);
        float eye = (P("Left_Eye").y + P("Right_Eye").y) * 0.5f;
        skin.transform.localScale = Vector3.one * (PlayerEyeH / Mathf.Max(0.1f, eye));

        // 판: 세부 묘사 품질 (컷씬은 중간 이상 판: 가까이 찍히는 장면이 있다. 얼굴 모델은 0.4m 얼굴 클로즈업이 있어 높음 이상)
        int tier = Mathf.Clamp(GameSettings.DetailQ, variant == 0 ? 2 : 1, 3);
        SkinnedMeshRenderer shown = null;
        foreach (var r in skin.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            bool on = r.name == "Body_q" + tier;
            r.enabled = on;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            if (on) shown = r;
        }
        if (shown == null) { Object.Destroy(skin); return false; }

        // 관절: 쉬는 회전(단위 회전) = 팔다리가 아래(-y)로 곧게. 자식 관절은 부모 바로 아래 뼈 길이만큼
        float upper = (P("Left_LowerArm") - P("Left_UpperArm")).magnitude, fore = (P("Left_Hand") - P("Left_LowerArm")).magnitude;
        float thigh = (P("Left_LowerLeg") - P("Left_UpperLeg")).magnitude, shin = (P("Left_Foot") - P("Left_LowerLeg")).magnitude;
        var hips = b.Joint("Hips", rig.Model, P("Hips"));
        rig.Body = b.Joint("Body", rig.Model, P("Spine"));
        rig.Head = b.Joint("Head", rig.Body, P("Head") - P("Spine"));
        foreach (var s in new[] { "L", "R" })
        {
            string pre = s == "L" ? "Left_" : "Right_";
            var arm = b.Joint("Arm" + s, rig.Body, P(pre + "UpperArm") - P("Spine"));
            var fo = b.Joint("Fore" + s, arm, new Vector3(0, -upper, 0));
            var hand = b.Joint("Hand" + s, fo, new Vector3(0, -fore, 0));
            var leg = b.Joint("Leg" + s, rig.Model, P(pre + "UpperLeg"));
            var sh = b.Joint("Shin" + s, leg, new Vector3(0, -thigh, 0));
            if (s == "L") { rig.ArmL = arm; rig.ForeL = fo; rig.HandL = hand; rig.LegL = leg; rig.ShinL = sh; }
            else { rig.ArmR = arm; rig.ForeR = fo; rig.HandR = hand; rig.LegR = leg; rig.ShinR = sh; }
        }
        rig.ThighLen = thigh; rig.ShinLen = shin; rig.UpperArmLen = upper; rig.ForearmLen = fore;
        rig.BodyY = P("Spine").y;
        rig.Shoulder = P("Right_UpperArm") - P("Spine");
        rig.AnkleH = P("Left_Foot").y;
        rig.MaxArmStretch = 1.2f;
        rig.PalmDepth = (P("Left_MiddleProximal") - P("Left_Hand")).magnitude * 0.58f; // 손바닥 가운데 = 손목에서 가운데 손가락 뿌리까지의 6할쯤
        // 겉면은 늘 q1 판에서 잰다 (q2 메시는 리깅 FBX에서 복사해 빌드에서 읽을 수 없다; 판마다 바인드 자세는 같다)
        SkinnedMeshRenderer probe = null;
        foreach (var r in skin.GetComponentsInChildren<SkinnedMeshRenderer>(true)) if (r.name == "Body_q1") probe = r;
        rig.TorsoSurface = PlayerTorsoSurface(probe != null && probe.sharedMesh != null && probe.sharedMesh.isReadable ? probe : shown, rig.Model, P("Spine"));
        // 입 (얼굴 모델만): 코끝(눈 아래 2~7cm에서 가장 앞)을 찾고, 그 아래 2~4.5cm에서 가장 앞으로 나온 곳 (입술 겉면).
        // 손으로 입을 가릴 때 손바닥이 여기로 간다 (눈 아래 범위만으로 찾으면 코끝을 잡아 손이 눈·코를 가렸다)
        if (variant == 0 && probe != null && probe.sharedMesh != null && probe.sharedMesh.isReadable)
        {
            Vector3 eyeMid = (P("Left_Eye") + P("Right_Eye")) * 0.5f;
            var mv = probe.sharedMesh.vertices;
            var mm = rig.Model.worldToLocalMatrix * probe.transform.localToWorldMatrix;
            var pts = new List<Vector3>();
            foreach (var v0 in mv)
            {
                var q = mm.MultiplyPoint3x4(v0);
                if (Mathf.Abs(q.x - eyeMid.x) <= 0.012f && q.y < eyeMid.y && q.y > eyeMid.y - 0.14f && q.z > eyeMid.z - 0.05f) pts.Add(q);
            }
            Vector3 nose = eyeMid + new Vector3(0, -0.045f, 0.03f), mouth = eyeMid + new Vector3(0, -0.08f, 0.02f);
            float nz = float.MinValue, mz = float.MinValue;
            foreach (var q in pts) if (q.y < eyeMid.y - 0.02f && q.y > eyeMid.y - 0.07f && q.z > nz) { nz = q.z; nose = q; }
            foreach (var q in pts) if (q.y < nose.y - 0.02f && q.y > nose.y - 0.045f && q.z > mz) { mz = q.z; mouth = q; }
            rig.Mouth = b.Joint("Mouth", rig.Head, mouth - P("Head"));
        }

        skin.AddComponent<SkinnedPlayerDriver>().Init(rig, hips, bones);
        return true;
    }

    // 몸통 겉면: 바인드 자세 메시에서 (높이, 가로) 칸마다 가장 앞·뒤 꼭짓점 z를 재 둔다 (몸통 관절 기준). 가방·어깨끈을 몸에 붙일 때 쓴다
    static System.Func<float, float, bool, float> PlayerTorsoSurface(SkinnedMeshRenderer smr, Transform model, Vector3 origin)
    {
        const float cell = 0.04f;
        var front = new Dictionary<long, float>();
        var back = new Dictionary<long, float>();
        if (!smr.sharedMesh.isReadable) return null; // 읽을 수 없으면 관 몸통 겉면으로 (CutsceneActor.SurfZ)
        var v = smr.sharedMesh.vertices;
        var m = model.worldToLocalMatrix * smr.transform.localToWorldMatrix;
        for (int i = 0; i < v.Length; i++)
        {
            var p = m.MultiplyPoint3x4(v[i]) - origin;
            if (p.y < -0.25f || p.y > 0.75f || Mathf.Abs(p.x) > 0.3f) continue;
            long key = Key(Mathf.RoundToInt(p.y / cell), Mathf.RoundToInt(p.x / cell));
            front[key] = front.TryGetValue(key, out var f) ? Mathf.Max(f, p.z) : p.z;
            back[key] = back.TryGetValue(key, out var k) ? Mathf.Min(k, p.z) : p.z;
        }
        return (y, x, isFront) =>
        {
            var d = isFront ? front : back;
            int iy = Mathf.RoundToInt(y / cell), ix = Mathf.RoundToInt(x / cell);
            for (int r = 0; r <= 2; r++)
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                        if (d.TryGetValue(Key(iy + dy, ix + dx), out var z)) return z;
            return isFront ? 0.12f : -0.12f;
        };
    }

    static long Key(int a, int b) => ((long)a << 32) ^ (uint)b;
}
