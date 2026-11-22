using System.Collections.Generic;
using UnityEngine;

// 일반 좀비(감염자)의 새 몸: 하나로 이어진 피부 메시(뼈 53개, 거리별 LOD 3단계)를 얹고,
// 게임 관절(Body·Head·Jaw·팔·다리)은 감염자 전용 자리에 보이지 않게 둔다. 좀비 코드·래그돌은 지금처럼 관절을 움직이고,
// SkinnedZombieDriver가 매 프레임 그 움직임을 척추·목·쇄골·손가락 뼈에 나눠 준다.
// 모델: ModelWork/Enemies/zombie_common (AI 원본을 잘라 다듬은 것, export/manifest.json). 파일이 없으면 예전처럼 관(ProcMesh)으로 짓는다.
public static partial class CharacterModels
{
    public static bool UseSkinnedCommonZombie = true;
    const string SkinnedDir = "Enemies/ZombieCommon/";
    static GameObject skinnedPrefab;
    static Material skinnedBody, skinnedEye;
    static bool skinnedLoaded;

    static bool SkinnedCommonZombie(Builder b)
    {
        if (!UseSkinnedCommonZombie) return false;
        if (!skinnedLoaded)
        {
            skinnedLoaded = true;
            skinnedPrefab = Resources.Load<GameObject>(SkinnedDir + "zombie_common");
            skinnedBody = Resources.Load<Material>(SkinnedDir + "M_ZombieCommon_Body");
            skinnedEye = Resources.Load<Material>(SkinnedDir + "M_ZombieCommon_Eye");
            if (skinnedEye != null) skinnedEye.EnableKeyword("_EMISSION"); // 빌드·다시 들여오기에서 꺼지는 일이 있다
        }
        if (skinnedPrefab == null || skinnedBody == null) return false;

        var rig = b.Rig;
        var skin = Object.Instantiate(skinnedPrefab, rig.Model, false);
        skin.name = "Skin";
        var anim = skin.GetComponent<Animator>();
        if (anim != null) Object.Destroy(anim);

        var bones = new Dictionary<string, Transform>();
        foreach (var t in skin.GetComponentsInChildren<Transform>(true)) bones[t.name] = t;
        var mats2 = new[] { skinnedBody, skinnedEye != null ? skinnedEye : skinnedBody };
        foreach (var r in skin.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            r.sharedMaterials = r.sharedMesh != null && r.sharedMesh.subMeshCount > 1 ? mats2 : new[] { skinnedBody };
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }

        // 관절은 뼈의 쉬는 자리에 (Model 기준). 손·발 끝까지 맞으므로 래그돌 캡슐·공격 판정이 새 몸과 맞는다
        Vector3 P(string n) => rig.Model.InverseTransformPoint(bones[n].position);
        var hips = b.Joint("Hips", rig.Model, P("Hips"));
        rig.Body = b.Joint("Body", rig.Model, P("Spine"));
        rig.Head = b.Joint("Head", rig.Body, P("Head") - P("Spine"));
        rig.Jaw = b.Joint("Jaw", rig.Head, P("Jaw") - P("Head"));
        foreach (var s in new[] { "L", "R" })
        {
            var arm = b.Joint("Arm" + s, rig.Body, P("Arm" + s) - P("Spine"));
            var fore = b.Joint("Fore" + s, arm, P("Fore" + s) - P("Arm" + s));
            var hand = b.Joint("Hand" + s, fore, P("Hand" + s) - P("Fore" + s));
            var leg = b.Joint("Leg" + s, rig.Model, P("Leg" + s));
            var shin = b.Joint("Shin" + s, leg, P("Shin" + s) - P("Leg" + s));
            if (s == "L") { rig.ArmL = arm; rig.ForeL = fore; rig.HandL = hand; rig.LegL = leg; rig.ShinL = shin; }
            else { rig.ArmR = arm; rig.ForeR = fore; rig.HandR = hand; rig.LegR = leg; rig.ShinR = shin; }
        }
        skin.AddComponent<SkinnedZombieDriver>().Init(rig, hips, bones);
        return true;
    }
}
