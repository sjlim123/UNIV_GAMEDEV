using System.Collections.Generic;
using UnityEngine;

// 일반 좀비(감염자)의 새 몸: 하나로 이어진 피부 메시(뼈 53개, 거리별 LOD 3단계)를 얹고,
// 게임 관절(Body·Head·Jaw·팔·다리)은 감염자 전용 자리에 보이지 않게 둔다. 좀비 코드·래그돌은 지금처럼 관절을 움직이고,
// SkinnedZombieDriver가 매 프레임 그 움직임을 척추·목·쇄골·손가락 뼈에 나눠 준다.
// 모델: ModelWork/Enemies/zombie_common (AI 원본을 잘라 다듬은 것, export/manifest.json). 파일이 없으면 예전처럼 관(ProcMesh)으로 짓는다.
public static partial class CharacterModels
{
    // ---- Tripo 감염자 (2026-11-26, 사용자 "힉스필드에서 Tripo로", "5번 8번 둘 다 — 시각적 다양성") ----
    // 사실적으로 썩은 감염자 두 가지 (a 단정한 머리·가슴 피, b 헝클어진 머리·피 묻은 입). 뼈 67개(Tripo 사람형 리깅), 품질 4단계 LOD.
    // 플레이어 모델 몸(CharacterModels.Player.cs)과 같은 규칙: 게임 관절은 쉬는 회전 = 팔다리가 -y로 곧게, 모델 뼈 자리에 세우고
    // SkinnedPlayerDriver가 매 프레임 관절 → 뼈로 옮긴다. 키는 눈 높이로 맞춘다. 이 모델에는 아래턱 뼈가 없어 Jaw 관절은 움직여도 보이지 않는다.
    // 모델: Resources/Enemies/ZombieCommon/zombie_common_a·b.prefab (편집기 '감염자 Tripo 모델: 가중치·품질 4단계·프리팹'). 없으면 아래 예전 피부 메시
    public static bool UseTripoZombie = true;
    const float ZombieEyeH = 1.62f;
    static readonly string[] TripoZombieRes = { "zombie_common_a", "zombie_common_b" };
    static readonly GameObject[] tripoZombiePrefab = new GameObject[2];
    static bool tripoZombieLoaded;

    static bool TripoCommonZombie(Builder b, int variant)
    {
        if (!UseTripoZombie) return false;
        if (!tripoZombieLoaded)
        {
            tripoZombieLoaded = true;
            for (int i = 0; i < 2; i++) tripoZombiePrefab[i] = Resources.Load<GameObject>(SkinnedDir + TripoZombieRes[i]);
        }
        int k = Mathf.Abs(variant) % 2;
        var prefab = tripoZombiePrefab[k] != null ? tripoZombiePrefab[k] : tripoZombiePrefab[1 - k];
        return prefab != null && TripoSkinnedBody(b, prefab, 0.7f) != null;
    }

    // ---- 가죽 감염자 = 감염된 군인 (2026-11-27, 사용자 "갑바 정도의 군복", 그림 5·7 두 가지) ----
    // 장비 6개(방탄조끼·전투모·어깨 보호대 둘·무릎 보호대 둘)는 프리팹에서 뼈 아래에 붙어 있다 (편집기 '가죽 감염자 Tripo 모델: 몸 a·b + 장비 맞춰 붙이기').
    // 이름(ChestArmor·HelmetArmor·ShoulderArmor·KneeArmor)으로 찾아 갑옷 조각 목록에 넣으면 Zombie.LoseArmor가 맞을 때마다 하나씩 떨어뜨린다
    public static bool UseTripoLeather = true;
    static readonly string[] TripoLeatherRes = { "Enemies/ZombieLeather/zombie_leather_a", "Enemies/ZombieLeather/zombie_leather_b" };
    static readonly GameObject[] tripoLeatherPrefab = new GameObject[2];
    static bool tripoLeatherLoaded;
    static readonly HashSet<string> ArmorNames = new HashSet<string> { "ChestArmor", "HelmetArmor", "ShoulderArmor", "KneeArmor" };

    static bool TripoLeatherZombie(Builder b)
    {
        if (!UseTripoLeather) return false;
        if (!tripoLeatherLoaded)
        {
            tripoLeatherLoaded = true;
            for (int i = 0; i < 2; i++) tripoLeatherPrefab[i] = Resources.Load<GameObject>(TripoLeatherRes[i]);
        }
        int k = Random.Range(0, 2);
        var prefab = tripoLeatherPrefab[k] != null ? tripoLeatherPrefab[k] : tripoLeatherPrefab[1 - k];
        if (prefab == null) return false;
        var skin = TripoSkinnedBody(b, prefab, 0.7f);
        if (skin == null) return false;
        foreach (var t in skin.GetComponentsInChildren<Transform>(true))
            if (ArmorNames.Contains(t.name)) b.Rig.Armor.Add(t);
        return true;
    }

    // Tripo 스킨 몸 하나를 얹고 게임 관절을 세운다. 실패하면 null
    static GameObject TripoSkinnedBody(Builder b, GameObject prefab, float curl)
    {
        var rig = b.Rig;
        var skin = Object.Instantiate(prefab, rig.Model, false);
        skin.name = "Skin";
        skin.transform.localPosition = Vector3.zero; skin.transform.localRotation = Quaternion.identity; skin.transform.localScale = Vector3.one;
        var anim = skin.GetComponent<Animator>(); if (anim != null) Object.Destroy(anim);
        var bones = new Dictionary<string, Transform>();
        foreach (var t in skin.GetComponentsInChildren<Transform>(true)) bones[t.name] = t;
        string[] need = { "Hips", "Spine", "Neck", "Head", "Left_Eye", "Right_Eye", "Left_UpperArm", "Left_LowerArm", "Left_Hand", "Right_UpperArm", "Right_LowerArm", "Right_Hand",
            "Left_UpperLeg", "Left_LowerLeg", "Left_Foot", "Right_UpperLeg", "Right_LowerLeg", "Right_Foot", "Left_MiddleProximal", "Right_MiddleProximal" };
        foreach (var n in need)
            if (!bones.ContainsKey(n)) { Object.Destroy(skin); return null; }
        foreach (var r in skin.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

        // 키: 눈 높이를 맞춘다 (Tripo 모델은 키 약 1 단위로 나온다)
        Vector3 P(string n) => rig.Model.InverseTransformPoint(bones[n].position);
        float eye = (P("Left_Eye").y + P("Right_Eye").y) * 0.5f;
        skin.transform.localScale = Vector3.one * (ZombieEyeH / Mathf.Max(0.1f, eye));

        // 관절: 쉬는 회전 = 팔다리가 아래(-y)로 곧게. 자식 관절은 부모 바로 아래 뼈 길이만큼 (SkinnedPlayer와 같다)
        float upper = (P("Left_LowerArm") - P("Left_UpperArm")).magnitude, fore = (P("Left_Hand") - P("Left_LowerArm")).magnitude;
        float thigh = (P("Left_LowerLeg") - P("Left_UpperLeg")).magnitude, shin = (P("Left_Foot") - P("Left_LowerLeg")).magnitude;
        var hips = b.Joint("Hips", rig.Model, P("Hips"));
        rig.Body = b.Joint("Body", rig.Model, P("Spine"));
        rig.Head = b.Joint("Head", rig.Body, P("Head") - P("Spine"));
        rig.Jaw = b.Joint("Jaw", rig.Head, Vector3.zero);
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
        rig.CurlL = rig.CurlR = curl; // 할퀴려는 갈고리 손 (1 = 느슨한 주먹)
        skin.AddComponent<SkinnedPlayerDriver>().Init(rig, hips, bones);
        return skin;
    }

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
