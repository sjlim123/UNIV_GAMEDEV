using System.Collections.Generic;
using UnityEngine;

// 적·NPC 겉모습 (Character_Design 디자인 시트를 따른다: 일반·가죽갑옷·새총·거대 좀비, 약탈자, 들개, 잡상인)
// Civilian: 컷씬의 주인공 (평범한 옷차림의 시민)
// Corpse: 도시 건물 벽에 기대 앉아 죽은 사람 (DeadBody)
public enum FigureKind { Zombie, Leather, Slingshot, Giant, Raider, Dog, Merchant, Civilian, Corpse }

// 만든 몸의 관절. 사람은 몸통·머리·턱·어깨·팔꿈치·손목·엉덩이·무릎, 들개는 목·머리·턱·다리 4개(위·아래)·꼬리
public class CharacterRig
{
    public Transform Model, Body, Head, Jaw, ArmL, ArmR, ForeL, ForeR, HandL, HandR, LegL, LegR, ShinL, ShinR;
    public Transform Neck, Tail;
    public Transform[] Legs, Knees; // 들개: 앞왼·앞오·뒤왼·뒤오
    public Transform Lantern;       // 잡상인 등불 (불빛을 단다)
    public Transform Mouth;         // 입 가운데 (매끄러운 얼굴에만 있다. 컷씬 배우가 손으로 입을 가릴 때 쓴다)
    // 맞으면 하나씩 떨어져 나가는 가죽 갑옷 (가슴·어깨·무릎·모자)
    public readonly List<Transform> Armor = new List<Transform>();
    public Renderer[] Renderers;
}

// 실행 중에 매끈한 관(ProcMesh)과 무늬 재질(ProcTex)로 사람·들개 몸을 짓는다.
// 크기는 Unity 1 = 1m (사람 1.75m, 들개 어깨 0.6m·몸길이 1.1m)
public static partial class CharacterModels
{
    // 사람 뼈대 길이 (m). 컷씬 배우(CutsceneActor)가 관절을 IK로 굽힐 때도 쓴다
    public const float HipY = 0.92f, Thigh = 0.44f, Shin = 0.40f, UpperArm = 0.30f, Forearm = 0.27f;
    // 손 가운데 (어깨 기준 아래로). 좀비 코드가 손 위치를 이 값으로 잡는다
    public const float HandReach = 0.66f;

    public static CharacterRig Build(Transform owner, FigureKind kind, int variant = 0)
    {
        var old = owner.Find("Model");
        if (old != null)
        {
            old.gameObject.SetActive(false);
            Object.Destroy(old.gameObject);
        }
        var b = new Builder(owner, kind, variant);
        switch (kind)
        {
            case FigureKind.Leather: LeatherZombie(b); break;
            case FigureKind.Slingshot: SlingshotZombie(b); break;
            case FigureKind.Giant: GiantZombie(b); break;
            case FigureKind.Raider: Raider(b); break;
            case FigureKind.Dog: Dog(b); break;
            case FigureKind.Merchant: Merchant(b); break;
            case FigureKind.Civilian: Civilian(b, variant); break;
            case FigureKind.Corpse: Corpse(b, variant); break;
            default: CommonZombie(b, variant); break;
        }
        b.Finish();
        return b.Rig;
    }

    // ================= 부품을 붙이는 도구 =================

    class Builder
    {
        public readonly CharacterRig Rig = new CharacterRig();
        public readonly string Key;
        readonly List<Transform> joints = new List<Transform>();

        public Builder(Transform owner, FigureKind kind, int variant)
        {
            Key = kind + "_" + variant;
            Rig.Model = Joint("Model", owner, Vector3.zero);
        }

        public Transform Joint(string name, Transform parent, Vector3 pos, Vector3? euler = null)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            t.localRotation = Quaternion.Euler(euler ?? Vector3.zero);
            joints.Add(t);
            return t;
        }

        public Transform Part(Transform parent, Mesh mesh, Material mat, Vector3 pos, Vector3? euler = null, Vector3? scale = null, bool shadow = true)
        {
            var go = new GameObject(mesh.name);
            var t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            t.localRotation = Quaternion.Euler(euler ?? Vector3.zero);
            t.localScale = scale ?? Vector3.one;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = shadow ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            return t;
        }

        // 떨어질 수 있는 갑옷 조각: 자기 관절을 따로 갖는다 (합치지 않는다)
        public Transform Armor(string name, Transform parent, Vector3 pos)
        {
            var t = Joint(name, parent, pos);
            Rig.Armor.Add(t);
            return t;
        }

        // 관절마다 같은 재질 부품을 합쳐 그리기 호출을 줄이고, 렌더러 목록을 모은다
        public void Finish()
        {
            foreach (var j in joints)
                if (j != null && !Rig.Armor.Contains(j)) ProcMesh.Merge(j, Key, null);
            foreach (var a in Rig.Armor) ProcMesh.Merge(a, Key, null);
            Rig.Renderers = Rig.Model.GetComponentsInChildren<Renderer>(false);
        }
    }

    static Mesh Tube(string key, params float[] rings) => ProcMesh.Tube(key, 2, 14, rings);
    static Mesh Ell(string key, float rx, float ry, float rz) => ProcMesh.Ellipsoid(key, rx, ry, rz);

    // 고리 배열의 반지름을 배율만큼 키운다 (옷을 몸보다 살짝 크게)
    static float[] Grow(float[] rings, float sx, float sz, float add = 0f)
    {
        var r = (float[])rings.Clone();
        for (int i = 0; i < r.Length; i += 4)
        {
            r[i + 1] = r[i + 1] * sx + add;
            r[i + 2] = r[i + 2] * sz + add;
            r[i + 3] *= sz;
        }
        return r;
    }

    // 고리 배열 중 y가 from~to인 것만 (옷자락·소매 길이)
    static float[] Slice(float[] rings, float from, float to)
    {
        var list = new List<float>();
        for (int i = 0; i < rings.Length; i += 4)
            if (rings[i] >= from - 1e-4f && rings[i] <= to + 1e-4f)
                for (int k = 0; k < 4; k++) list.Add(rings[i + k]);
        return list.ToArray();
    }

    static Color Hex(string hex) => ColorUtility.TryParseHtmlString("#" + hex, out var c) ? c : Color.magenta;

    // 기본 체격 몸통의 겉면: 높이 y(몸통 관절 기준 0~0.55), 가로 x에서 앞(front) 또는 뒤 표면의 z. 배낭끈처럼 몸에 붙는 부품을 놓을 때 쓴다
    public static float TorsoSurfaceZ(float y, float x, bool front)
    {
        var r = TorsoRings;
        int n = r.Length / 4, i = 0;
        while (i < n - 2 && r[(i + 1) * 4] < y) i++;
        float y0 = r[i * 4], y1 = r[(i + 1) * 4];
        float t = Mathf.Clamp01((y - y0) / Mathf.Max(1e-4f, y1 - y0));
        float rx = Mathf.Lerp(r[i * 4 + 1], r[(i + 1) * 4 + 1], t);
        float rz = Mathf.Lerp(r[i * 4 + 2], r[(i + 1) * 4 + 2], t);
        float zo = Mathf.Lerp(r[i * 4 + 3], r[(i + 1) * 4 + 3], t);
        float q = Mathf.Clamp01(Mathf.Abs(x) / Mathf.Max(1e-4f, rx));
        float h = rz * Mathf.Sqrt(1 - q * q);
        return zo + (front ? h : -h);
    }

    // ================= 사람 기본 몸 =================

    // 몸 모양과 재질. 재질이 null인 부위는 맨살
    class Human
    {
        public Material Skin, Shirt, Pants, Shoes, Hair, Sleeve, Eyes;
        public float Bulk = 1f, Limb = 1f, HeadSize = 1f, SleeveTo = -0.18f; // SleeveTo: 소매 끝 (위팔 기준 y, 아래팔까지면 -0.6)
        public bool Zombie = true, LongHair, Boots, Belt = true, PantsBaggy = true;
        public bool SmoothFace;  // 한 덩어리로 이어진 매끄러운 얼굴 (CharacterModels.Face)
        public FaceStyle Face;   // 매끄러운 얼굴의 생김새·표정 변주 (시체). null이면 주인공 얼굴
        public float Hunch;     // 몸통이 앞으로 굽은 정도 (m)
        public float NeckDrop;  // 머리를 어깨 쪽으로 내려 목을 짧게 보이게 하는 정도 (m)
        public bool LiveFingers; // 손가락 마디를 합치지 않고 따로 둔다 (컷씬 배우: 입을 가릴 때 더 감긴다)
        public float Shoulders = 1f;
    }

    // 몸통 단면 (몸통 관절 기준: 엉덩이 0 → 목 0.55)
    static readonly float[] TorsoRings =
    {
        0.00f, 0.152f, 0.100f, 0.000f,
        0.08f, 0.148f, 0.098f, 0.004f,
        0.17f, 0.150f, 0.100f, 0.010f,
        0.27f, 0.163f, 0.108f, 0.016f,
        0.36f, 0.178f, 0.112f, 0.016f,
        0.43f, 0.185f, 0.106f, 0.006f,
        0.48f, 0.170f, 0.092f, -0.004f,
        0.52f, 0.120f, 0.075f, -0.010f,
        0.55f, 0.060f, 0.055f, -0.008f,
    };

    static readonly float[] UpperArmRings =
    {
        0.07f, 0.016f, 0.017f, 0f,
        0.055f, 0.036f, 0.038f, 0f,
        0.03f, 0.044f, 0.046f, 0f,
        0.00f, 0.056f, 0.054f, 0f,
        -0.05f, 0.054f, 0.052f, 0.003f,
        -0.15f, 0.046f, 0.048f, 0.003f,
        -0.25f, 0.040f, 0.042f, 0f,
        -0.31f, 0.037f, 0.039f, -0.004f,
    };

    static readonly float[] ForearmRings =
    {
        0.02f, 0.037f, 0.039f, -0.004f,
        -0.04f, 0.042f, 0.040f, 0f,
        -0.12f, 0.036f, 0.032f, 0.002f,
        -0.22f, 0.027f, 0.022f, 0f,
        -0.27f, 0.025f, 0.019f, 0f,
    };

    static readonly float[] ThighRings =
    {
        0.04f, 0.078f, 0.082f, 0f,
        -0.04f, 0.082f, 0.088f, 0.008f,
        -0.16f, 0.072f, 0.078f, 0.008f,
        -0.30f, 0.060f, 0.064f, 0.004f,
        -0.42f, 0.052f, 0.056f, 0.006f,
        -0.46f, 0.050f, 0.054f, 0.008f,
    };

    static readonly float[] ShinRings =
    {
        0.03f, 0.050f, 0.054f, 0.008f,
        -0.05f, 0.048f, 0.056f, -0.006f,
        -0.13f, 0.048f, 0.058f, -0.014f,
        -0.25f, 0.038f, 0.042f, -0.006f,
        -0.36f, 0.031f, 0.034f, 0f,
        -0.40f, 0.030f, 0.032f, 0f,
    };

    // 머리 윗부분 (머리 관절 기준: 턱끝 0 → 정수리 0.25). 아래턱은 따로 움직인다
    static readonly float[] SkullRings =
    {
        0.065f, 0.068f, 0.088f, 0.014f,
        0.09f, 0.071f, 0.094f, 0.012f,
        0.12f, 0.075f, 0.098f, 0.008f,
        0.15f, 0.078f, 0.100f, 0.003f,
        0.18f, 0.077f, 0.098f, -0.004f,
        0.21f, 0.068f, 0.088f, -0.010f,
        0.235f, 0.045f, 0.060f, -0.014f,
        0.25f, 0.012f, 0.016f, -0.014f,
    };

    static readonly float[] JawRings =
    {
        0.000f, 0.036f, 0.040f, 0.048f,
        0.018f, 0.052f, 0.058f, 0.038f,
        0.045f, 0.064f, 0.080f, 0.022f,
        0.075f, 0.068f, 0.088f, 0.016f,
    };

    // 사람 몸 전체. 관절을 Rig에 채운다
    static void Humanoid(Builder b, Human h)
    {
        var rig = b.Rig;
        string k = b.Key;
        float bulk = h.Bulk, limb = h.Limb;

        // --- 골반 (엉덩이 관절 높이, 몸통이 숙여도 다리와 붙어 있게 따로 둔다). 아래는 관절에서 7.5cm까지만, 허벅지 사이에 끼게 좁다:
        //     예전(15cm·폭 25cm)에는 달릴 때 다리가 앞뒤로 벌어지면 가랑이 덩어리가 다리 사이로 튀어나와 몸통이 길어 보였다 ---
        var hips = b.Joint("Hips", rig.Model, new Vector3(0, HipY, 0));
        var pelvis = Tube(k + "pelvis", -0.075f, 0.104f * bulk, 0.076f * bulk, 0f, -0.035f, 0.15f * bulk, 0.1f * bulk, 0.004f, 0.01f, 0.163f * bulk, 0.108f * bulk, 0.005f, 0.06f, 0.15f * bulk, 0.1f * bulk, 0.004f);
        b.Part(hips, pelvis, h.Pants, Vector3.zero);
        if (h.Belt)
        {
            // 허리띠는 골반보다 6mm 이상 크게 (붙어 있으면 멀리서 두 면이 겹쳐 깜빡인다)
            b.Part(hips, Tube(k + "belt", 0.0f, 0.168f * bulk, 0.113f * bulk, 0.006f, 0.04f, 0.164f * bulk, 0.11f * bulk, 0.006f), ProcTex.Get(ProcTex.Kind.Leather, Hex("2e231b"), 3), Vector3.zero);
            b.Part(hips, ProcMesh.Box("buckle", 0.05f, 0.04f, 0.012f), ProcTex.Plain(Hex("8c8576"), 0.6f, 0.6f), new Vector3(0, 0.02f, 0.118f * bulk));
        }

        // --- 다리 ---
        float legX = 0.095f * bulk;
        var pants = h.PantsBaggy ? Grow(ThighRings, 1.1f * limb, 1.1f * limb) : Grow(ThighRings, limb, limb);
        var pantsShin = h.PantsBaggy ? Grow(ShinRings, 1.14f * limb, 1.12f * limb) : Grow(ShinRings, limb, limb);
        for (int s = -1; s <= 1; s += 2)
        {
            var leg = b.Joint(s < 0 ? "LegL" : "LegR", rig.Model, new Vector3(s * legX, HipY, 0));
            b.Part(leg, Tube(k + "thigh", pants), h.Pants, Vector3.zero);
            var knee = b.Joint(s < 0 ? "ShinL" : "ShinR", leg, new Vector3(0, -Thigh, 0));
            b.Part(knee, Tube(k + "shin", pantsShin), h.Pants, Vector3.zero);
            // 바짓단: 발목에서 살짝 넓어진다
            b.Part(knee, Tube(k + "hem", -0.40f, 0.043f * limb, 0.047f * limb, 0.004f, -0.34f, 0.04f * limb, 0.043f * limb, 0.002f), h.Pants, Vector3.zero);
            Shoe(b, knee, h);
            if (s < 0) { rig.LegL = leg; rig.ShinL = knee; }
            else { rig.LegR = leg; rig.ShinR = knee; }
        }

        // --- 몸통 ---
        rig.Body = b.Joint("Body", rig.Model, new Vector3(0, HipY, 0));
        var torso = Grow(TorsoRings, bulk * h.Shoulders, Mathf.Pow(bulk, 0.7f));
        for (int i = 0; i < torso.Length; i += 4) torso[i + 3] += h.Hunch * Mathf.Pow(torso[i] / 0.55f, 2) * 0.5f;
        b.Part(rig.Body, Tube(k + "torso", torso), h.Shirt, Vector3.zero);
        // 목 (등이 굽을수록 머리가 어깨 사이로 내려앉고 앞으로 나온다)
        float neckZ = h.Hunch * 0.5f, headY = 0.60f - h.Hunch * 0.6f - h.NeckDrop, neckW = Mathf.Lerp(1f, bulk, 0.6f);
        b.Part(rig.Body, Tube(k + "neck", headY - 0.10f, 0.052f * neckW, 0.054f * neckW, neckZ, headY - 0.02f, 0.046f * neckW, 0.049f * neckW, neckZ + 0.008f, headY + 0.04f, 0.048f * neckW, 0.05f * neckW, neckZ + 0.014f), h.Skin, Vector3.zero);
        // 옷깃
        b.Part(rig.Body, Tube(k + "collar", 0.50f, 0.075f * bulk, 0.07f, neckZ - 0.004f, 0.53f, 0.068f * bulk, 0.064f, neckZ), h.Shirt, Vector3.zero);

        // --- 머리 ---
        rig.Head = b.Joint("Head", rig.Body, new Vector3(0, headY, 0.012f + neckZ + h.Hunch * 0.4f));
        rig.Head.localScale = Vector3.one * h.HeadSize;
        if (h.SmoothFace) SmoothHead(b, h);
        else Head(b, h);

        // --- 팔 ---
        float shoulderX = 0.176f * bulk * h.Shoulders + (bulk > 1.3f ? 0.04f : 0f);
        for (int s = -1; s <= 1; s += 2)
        {
            var arm = b.Joint(s < 0 ? "ArmL" : "ArmR", rig.Body, new Vector3(s * shoulderX, 0.455f, -0.005f + h.Hunch * 0.35f));
            b.Part(arm, Tube(k + "upperarm", Grow(UpperArmRings, limb, limb)), h.Skin, Vector3.zero);
            // 어깨 근육 (소매가 있으면 소매색)
            b.Part(arm, Ell(k + "delt", 0.05f * limb, 0.062f, 0.054f * limb), h.Sleeve ?? h.Skin, new Vector3(-s * 0.004f, -0.018f, 0));
            if (h.Sleeve != null)
                b.Part(arm, Tube(k + "sleeve", Slice(Grow(UpperArmRings, limb * 1.22f, limb * 1.22f), Mathf.Max(h.SleeveTo, -0.31f), 0.08f)), h.Sleeve, Vector3.zero);

            var fore = b.Joint(s < 0 ? "ForeL" : "ForeR", arm, new Vector3(0, -UpperArm, 0));
            b.Part(fore, Tube(k + "forearm", Grow(ForearmRings, limb, limb)), h.SleeveTo < -0.31f && h.Sleeve != null ? h.Sleeve : h.Skin, Vector3.zero);
            if (h.SleeveTo < -0.31f && h.Sleeve != null)
                b.Part(fore, Tube(k + "cuff", -0.27f, 0.034f * limb, 0.032f * limb, 0, -0.21f, 0.036f * limb, 0.034f * limb, 0), h.Sleeve, Vector3.zero);

            var hand = b.Joint(s < 0 ? "HandL" : "HandR", fore, new Vector3(0, -Forearm, 0));
            Hand(b, hand, s, h);
            if (s < 0) { rig.ArmL = arm; rig.ForeL = fore; rig.HandL = hand; }
            else { rig.ArmR = arm; rig.ForeR = fore; rig.HandR = hand; }
        }
    }

    static void Shoe(Builder b, Transform knee, Human h)
    {
        string k = b.Key;
        // 발 앞쪽(+z)으로 긴 관: 부품을 x축으로 90도 돌려 세운다 (고리 z치우침 = 아래쪽)
        var shoe = ProcMesh.Tube(k + "shoe", 3f, 14,
            -0.073f, 0.014f, 0.013f, 0.038f, // 뒤꿈치를 둥글게 닫는다 (납작한 마감면이 다리 사이로 보이지 않게)
            -0.065f, 0.032f, 0.030f, 0.036f,
            -0.05f, 0.042f, 0.042f, 0.036f,
            0.00f, 0.046f, 0.045f, 0.032f,
            0.07f, 0.047f, 0.035f, 0.044f,
            0.14f, 0.044f, 0.026f, 0.054f,
            0.18f, 0.036f, 0.020f, 0.058f,
            0.195f, 0.020f, 0.012f, 0.060f);
        b.Part(knee, shoe, h.Shoes, new Vector3(0, -Shin, 0), new Vector3(90, 0, 0));
        // 밑창
        var sole = ProcMesh.Tube(k + "sole", 4f, 14, -0.07f, 0.04f, 0.008f, 0.075f, 0.19f, 0.045f, 0.008f, 0.07f, 0.205f, 0.025f, 0.006f, 0.068f);
        b.Part(knee, sole, ProcTex.Plain(Hex("1d1a17"), 0.2f), new Vector3(0, -Shin, 0), new Vector3(90, 0, 0));
        if (h.Boots)
            b.Part(knee, Tube(k + "bootshaft", -0.38f, 0.046f, 0.05f, 0f, -0.24f, 0.044f, 0.048f, -0.004f, -0.22f, 0.046f, 0.05f, -0.004f), h.Shoes, Vector3.zero);
    }

    static void Hand(Builder b, Transform hand, int side, Human h)
    {
        string k = b.Key;
        var skin = h.Skin;
        float s = h.Limb;
        // 손바닥은 허벅지 쪽(안쪽)을 보고, 엄지는 앞(+z)
        b.Part(hand, Tube(k + "palm", 0.01f, 0.017f * s, 0.024f * s, 0f, -0.03f, 0.016f * s, 0.037f * s, 0f, -0.08f, 0.015f * s, 0.040f * s, 0f, -0.10f, 0.013f * s, 0.036f * s, 0f), skin, Vector3.zero);
        // 손가락은 두 마디: 손등 관절(curl)과 가운데 관절(bend)에서 감겨 힘을 뺀 손이 둥글게 보인다 (좀비는 갈고리처럼 더 감긴다).
        // 살아 움직이는 손(h.LiveFingers: 컷씬 배우)만 마디를 부모-자식으로 엮어 따로 돌릴 수 있게 두고(합쳐지지 않는다),
        // 나머지는 같은 자리를 손 관절 바로 아래 두어 Finish()에서 한 메시로 합쳐진다
        float curl = (h.Zombie ? 26f : 20f) * -side;
        float bend = (h.Zombie ? 42f : 30f) * -side;
        float[] z = { 0.027f, 0.009f, -0.009f, -0.027f }, len = { 0.075f, 0.082f, 0.077f, 0.062f };
        var nail = h.Zombie ? ProcTex.Plain(Hex("5a5232"), 0.4f) : null; // 좀비 손톱은 길고 누렇다
        Transform Seg(Transform parent, Mesh m, Material mat, Vector3 pos, Quaternion rot)
        {
            if (h.LiveFingers || parent == hand) return b.Part(parent, m, mat, pos, rot.eulerAngles);
            return b.Part(hand, m, mat, parent.localPosition + parent.localRotation * pos, (parent.localRotation * rot).eulerAngles);
        }
        for (int i = 0; i < 4; i++)
        {
            float L = len[i] * (h.Zombie ? 1.08f : 1f), L1 = L * 0.55f, L2 = L * 0.5f;
            var p = Seg(hand, Tube(k + "finger" + i, 0f, 0.0088f * s, 0.0085f * s, 0f, -L1 * 0.5f, 0.0084f * s, 0.008f * s, 0f, -L1 - 0.003f, 0.0076f * s, 0.0073f * s, 0f), skin,
                new Vector3(0, -0.095f, z[i] * s), Quaternion.Euler(0, 0, curl + (i - 1.5f) * 3f));
            var d = Seg(p, Tube(k + "finger" + i + "b", 0.003f, 0.0078f * s, 0.0075f * s, 0f, -L2 * 0.5f, 0.0072f * s, 0.0068f * s, 0f, -L2, 0.0062f * s, 0.0058f * s, 0f, -L2 - 0.006f, 0.002f, 0.002f, 0f), skin,
                new Vector3(0, -L1, 0), Quaternion.Euler(0, 0, bend));
            if (nail != null) Seg(d, ProcMesh.Cone("nail", 0.006f, 0.016f, 4), nail, new Vector3(-side * 0.004f, -L2, 0), Quaternion.Euler(180, 0, 0));
        }
        var t = Seg(hand, Tube(k + "thumb", 0f, 0.011f * s, 0.0105f * s, 0f, -0.02f, 0.0098f * s, 0.0095f * s, 0f, -0.036f, 0.009f * s, 0.0088f * s, 0f), skin,
            new Vector3(-side * 0.008f, -0.03f, 0.03f * s), Quaternion.Euler(-38, 0, curl * 0.6f));
        Seg(t, Tube(k + "thumbb", 0.003f, 0.0088f * s, 0.0086f * s, 0f, -0.016f, 0.008f * s, 0.0078f * s, 0f, -0.026f, 0.006f * s, 0.006f * s, 0f, -0.031f, 0.002f, 0.002f, 0f), skin,
            new Vector3(0, -0.033f, 0), Quaternion.Euler(0, 0, bend * 0.5f));
    }

    static void Head(Builder b, Human h)
    {
        var head = b.Rig.Head;
        string k = b.Key;
        var skin = h.Skin;

        b.Part(head, Tube(k + "skull", SkullRings), skin, Vector3.zero);
        // 아래턱 (입을 벌릴 수 있게 관절에 단다)
        var jaw = b.Joint("Jaw", head, new Vector3(0, 0.085f, 0.0f));
        b.Rig.Jaw = jaw;
        b.Part(jaw, Tube(k + "jaw", JawRings), skin, new Vector3(0, -0.085f, 0));
        // 입속 (턱을 벌리면 보인다)
        b.Part(head, Ell("mouthin", 0.05f, 0.012f, 0.042f), ProcTex.Plain(Hex("2a0806"), 0.6f), new Vector3(0, 0.06f, 0.05f), null, null, false);

        // 코·광대·눈두덩
        b.Part(head, Tube(k + "nose", 0.095f, 0.017f, 0.014f, 0.106f, 0.105f, 0.015f, 0.016f, 0.111f, 0.125f, 0.009f, 0.012f, 0.107f, 0.148f, 0.008f, 0.008f, 0.100f), skin, Vector3.zero);
        b.Part(head, Ell("brow", 0.066f, 0.013f, 0.022f), skin, new Vector3(0, 0.163f, 0.093f));
        b.Part(head, Ell("cheek", 0.03f, 0.022f, 0.02f), skin, new Vector3(-0.045f, 0.118f, 0.082f));
        b.Part(head, Ell("cheek", 0.03f, 0.022f, 0.02f), skin, new Vector3(0.045f, 0.118f, 0.082f));
        // 귀
        b.Part(head, Ell("ear", 0.012f, 0.03f, 0.02f), skin, new Vector3(-0.077f, 0.13f, -0.004f), new Vector3(0, -12, 0));
        b.Part(head, Ell("ear", 0.012f, 0.03f, 0.02f), skin, new Vector3(0.077f, 0.13f, -0.004f), new Vector3(0, 12, 0));

        // 눈: 움푹 꺼진 어두운 눈자위 + 눈알 (좀비는 희멀겋게 빛난다)
        var socket = ProcTex.Plain(h.Zombie ? Hex("2b2a1c") : Hex("4a3426"), 0.3f);
        var eye = h.Eyes ?? ProcTex.Plain(Hex("d8d2c4"), 0.85f);
        for (int s = -1; s <= 1; s += 2)
        {
            b.Part(head, Ell("socket", 0.02f, 0.014f, 0.01f), socket, new Vector3(s * 0.032f, 0.143f, 0.088f), null, null, false);
            b.Part(head, Ell("eye", 0.0125f, 0.0105f, 0.009f), eye, new Vector3(s * 0.032f, 0.142f, 0.092f), null, null, false);
            if (!h.Zombie) b.Part(head, Ell("iris", 0.005f, 0.005f, 0.003f), ProcTex.Plain(Hex("2c2118"), 0.9f), new Vector3(s * 0.032f, 0.142f, 0.1005f), null, null, false);
        }

        if (h.Zombie)
        {
            // 벌어진 입: 누런 윗니·아랫니
            var tooth = ProcTex.Plain(Hex("b9a77a"), 0.5f);
            for (int i = -2; i <= 2; i++)
            {
                b.Part(head, ProcMesh.Box("tooth", 0.008f, 0.012f, 0.006f), tooth, new Vector3(i * 0.0095f, 0.061f, 0.094f - Mathf.Abs(i) * 0.004f), new Vector3(0, i * 10, 0), null, false);
                b.Part(jaw, ProcMesh.Box("tooth", 0.008f, 0.01f, 0.006f), tooth, new Vector3(i * 0.0092f, -0.012f, 0.092f - Mathf.Abs(i) * 0.004f), new Vector3(0, i * 10, 0), null, false);
            }
            // 찢어진 뺨 상처
            b.Part(head, Ell("wound", 0.016f, 0.01f, 0.006f), ProcTex.Plain(Hex("5c1813"), 0.65f), new Vector3(0.058f, 0.1f, 0.074f), new Vector3(0, 35, -25), null, false);
        }

        if (h.Hair != null)
        {
            // 이마선 뒤로 덮는 머리카락
            var hair = Grow(Slice(SkullRings, 0.12f, 0.25f), 1.1f, 1.07f, 0.003f);
            // 이마 쪽만 뒤로 물려 이마선을 만든다 (정수리는 덮는다)
            for (int i = 0; i < hair.Length; i += 4) hair[i + 3] -= 0.018f * Mathf.Clamp01((0.2f - hair[i]) / 0.08f);
            b.Part(head, Tube(k + "hair", hair), h.Hair, Vector3.zero);
            // 귀 옆 구레나룻과 뒤통수
            b.Part(head, Ell("hairback", 0.072f, 0.06f, 0.05f), h.Hair, new Vector3(0, 0.13f, -0.05f));
            if (h.LongHair)
            {
                // 어깨까지 늘어진 긴 머리
                b.Part(head, Tube(k + "longhair", -0.10f, 0.09f, 0.05f, -0.065f, -0.02f, 0.085f, 0.06f, -0.06f, 0.08f, 0.082f, 0.07f, -0.05f, 0.16f, 0.08f, 0.08f, -0.04f), h.Hair, Vector3.zero);
                b.Part(head, Tube(k + "bangs", 0.10f, 0.03f, 0.02f, 0.085f, 0.2f, 0.06f, 0.03f, 0.08f), h.Hair, new Vector3(-0.035f, 0, 0), new Vector3(0, 0, 18));
            }
        }
    }

    // 옷자락 끝의 찢어진 조각들: 몸통 둘레(rx, rz)를 따라 아래로 늘어진다
    static void Tatters(Builder b, Transform parent, Material mat, float y, float rx, float rz, int count, int seed, float length = 0.09f)
    {
        var rng = new System.Random(seed);
        for (int i = 0; i < count; i++)
        {
            float a = (float)(rng.NextDouble() * Mathf.PI * 2);
            var n = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            float w = 0.035f + 0.04f * (float)rng.NextDouble(), l = length * (0.6f + 0.8f * (float)rng.NextDouble());
            var pos = new Vector3(n.x * rx, y - l * 0.4f, n.z * rz);
            float yaw = Mathf.Atan2(n.x, n.z) * Mathf.Rad2Deg;
            b.Part(parent, ProcMesh.Box("tatter", 1, 1, 1, 4), mat, pos, new Vector3(8, yaw, (float)(rng.NextDouble() * 24 - 12)), new Vector3(w, l, 0.006f));
        }
    }

    // ================= 01 일반 좀비 =================
    // 피부 #8E9C7C, 셔츠 #BDB39C(찢김·혈흔), 청바지 #51637C, 눈 #E6E0A3 (발광). 변형: 0 기본 · 1 여성형 · 2 작업복 · 3 비만형

    static void CommonZombie(Builder b, int variant)
    {
        int seed = variant * 13 + 1;
        var h = new Human
        {
            Skin = ProcTex.Get(ProcTex.Kind.ZombieSkin, Hex("8E9C7C"), seed, 0, 0.35f, Hex("6E7B5F")),
            Shirt = ProcTex.Get(ProcTex.Kind.Cloth, Hex("BDB39C"), seed, 4, 0.55f, Hex("8c836d")),
            Pants = ProcTex.Get(ProcTex.Kind.Denim, Hex("51637C"), seed, 1, 0.45f, Hex("36435a")),
            Shoes = ProcTex.Get(ProcTex.Kind.Leather, Hex("3a2c22"), seed, 0, 0.6f),
            Hair = ProcTex.Get(ProcTex.Kind.Hair, Hex("2a2420"), seed),
            Eyes = ProcTex.Glow(Hex("E6E0A3"), new Color(1.3f, 1.2f, 0.55f)),
            Sleeve = null,
            Hunch = 0.05f,
        };
        switch (variant)
        {
            case 1: // 여성형: 긴 머리, 좁은 어깨, 긴 블라우스
                h.Shoulders = 0.88f;
                h.Limb = 0.9f;
                h.LongHair = true;
                h.Hair = ProcTex.Get(ProcTex.Kind.Hair, Hex("3b2a1f"), seed);
                h.Shirt = ProcTex.Get(ProcTex.Kind.Cloth, Hex("a9a094"), seed, 4, 0.5f, Hex("7f776c"));
                h.Sleeve = h.Shirt;
                h.SleeveTo = -0.2f;
                break;
            case 2: // 작업복: 칙칙한 녹회색 작업 셔츠, 모자, 긴 소매
                h.Shirt = ProcTex.Get(ProcTex.Kind.Cloth, Hex("5d6b55"), seed, 3, 0.65f, Hex("3f4a3a"));
                h.Pants = ProcTex.Get(ProcTex.Kind.Cloth, Hex("4d4a42"), seed, 1, 0.6f);
                h.Sleeve = h.Shirt;
                h.SleeveTo = -0.6f;
                h.Boots = true;
                break;
            case 3: // 비만형: 느리고 단단해 보이는 큰 몸, 대머리
                h.Bulk = 1.28f;
                h.Limb = 1.18f;
                h.Hair = null;
                h.Shirt = ProcTex.Get(ProcTex.Kind.Cloth, Hex("c8c0aa"), seed, 5, 0.6f, Hex("948b74"));
                break;
            default:
                h.Sleeve = h.Shirt;
                h.SleeveTo = -0.12f;
                break;
        }
        Humanoid(b, h);
        var body = b.Rig.Body;
        float bulk = h.Bulk;

        // 찢어진 셔츠 자락
        Tatters(b, body, h.Shirt, 0.02f, 0.155f * bulk, 0.104f * bulk, 7, seed);
        if (variant == 1) // 블라우스 아랫자락이 허벅지까지
            b.Part(b.Rig.Model.Find("Hips"), Tube(b.Key + "skirt", -0.22f, 0.2f, 0.15f, 0.01f, -0.05f, 0.172f, 0.118f, 0.005f, 0.04f, 0.162f, 0.108f, 0.004f), h.Shirt, Vector3.zero);
        if (variant == 2)
        {
            // 챙 달린 작업 모자
            var cap = ProcTex.Get(ProcTex.Kind.Cloth, Hex("6b5a3e"), seed, 0, 0.7f);
            var capRings = Grow(Slice(SkullRings, 0.16f, 0.25f), 1.1f, 1.08f, 0.004f);
            b.Part(b.Rig.Head, Tube(b.Key + "cap", capRings), cap, Vector3.zero);
            b.Part(b.Rig.Head, ProcMesh.Tube("brim", 3, 12, 0f, 0.07f, 0.06f, 0f, 0.008f, 0.07f, 0.06f, 0f), cap, new Vector3(0, 0.17f, 0.1f), new Vector3(-8, 0, 0));
        }
    }

    // ================= 02 가죽갑옷 좀비 =================
    // 가죽 #7A5236(음영 #5C3D28), 스트랩 #3E2A1C, 리벳 #A8A08E, 피부 #879572, 바지 #4E4A40.
    // 흉갑·어깨·무릎·모자는 맞으면 떨어져 나간다

    static void LeatherZombie(Builder b)
    {
        var leather = ProcTex.Get(ProcTex.Kind.Leather, Hex("7A5236"), 4, 0, 0.35f, Hex("5C3D28"));
        var strap = ProcTex.Get(ProcTex.Kind.Leather, Hex("3E2A1C"), 5, 0, 0.3f);
        var rivet = ProcTex.Plain(Hex("A8A08E"), 0.55f, 0.6f);
        var h = new Human
        {
            Skin = ProcTex.Get(ProcTex.Kind.ZombieSkin, Hex("879572"), 21, 0, 0.35f, Hex("66734f")),
            Shirt = ProcTex.Get(ProcTex.Kind.Cloth, Hex("4a453b"), 21, 2, 0.6f),
            Pants = ProcTex.Get(ProcTex.Kind.Cloth, Hex("4E4A40"), 22, 1, 0.55f, Hex("37342c")),
            Shoes = ProcTex.Get(ProcTex.Kind.Leather, Hex("3a2a1d"), 23, 0, 0.6f),
            Eyes = ProcTex.Glow(Hex("E6E0A3"), new Color(1.3f, 1.2f, 0.55f)),
            Sleeve = ProcTex.Get(ProcTex.Kind.Cloth, Hex("4a453b"), 21, 0, 0.6f),
            SleeveTo = -0.22f,
            Boots = true,
            Bulk = 1.08f,
            Limb = 1.06f,
            Hunch = 0.03f,
        };
        Humanoid(b, h);
        var rig = b.Rig;
        float bulk = h.Bulk;

        // 흉갑 (앞뒤를 감싼 두꺼운 가죽 + 대각선 스트랩 + 리벳)
        var chest = b.Armor("ChestArmor", rig.Body, Vector3.zero);
        var shell = Grow(Slice(Grow(TorsoRings, bulk, Mathf.Pow(bulk, 0.7f)), 0.08f, 0.48f), 1.09f, 1.14f, 0.004f);
        b.Part(chest, ProcMesh.Tube(b.Key + "cuirass", 3.2f, 16, shell), leather, Vector3.zero);
        b.Part(chest, ProcMesh.Box("strapdiag", 0.05f, 0.4f, 0.012f), strap, new Vector3(0, 0.27f, 0.128f * bulk), new Vector3(-6, 0, 38));
        b.Part(chest, ProcMesh.Box("strapdiag", 0.05f, 0.4f, 0.012f), strap, new Vector3(0, 0.27f, -0.124f * bulk), new Vector3(6, 0, 38));
        b.Part(chest, ProcMesh.Box("chestband", 0.26f, 0.035f, 0.01f), strap, new Vector3(0, 0.12f, 0.124f * bulk), new Vector3(4, 0, 0));
        for (int i = 0; i < 6; i++)
        {
            float x = (i % 2 == 0 ? -1 : 1) * 0.11f * bulk, y = 0.18f + (i / 2) * 0.1f;
            b.Part(chest, Ell("rivet", 0.007f, 0.007f, 0.004f), rivet, new Vector3(x, y, 0.13f * bulk - (i / 2) * 0.004f), null, null, false);
        }
        // 다른 색 가죽으로 기운 자리
        b.Part(chest, ProcMesh.Box("patch", 0.06f, 0.07f, 0.008f), ProcTex.Get(ProcTex.Kind.Leather, Hex("8a6a48"), 6), new Vector3(0.07f, 0.36f, 0.128f * bulk), new Vector3(-8, 10, 8));

        // 허리띠 아래로 늘어진 가죽 자락 (골반에 붙는다)
        var hips = rig.Model.Find("Hips");
        b.Part(hips, ProcMesh.Box("tasset", 0.11f, 0.16f, 0.012f), leather, new Vector3(-0.07f, -0.08f, 0.115f), new Vector3(6, -8, 0));
        b.Part(hips, ProcMesh.Box("tasset", 0.11f, 0.16f, 0.012f), leather, new Vector3(0.07f, -0.08f, 0.115f), new Vector3(6, 8, 0));

        // 어깨받이 (팔을 따라 움직인다)
        foreach (var arm in new[] { rig.ArmL, rig.ArmR })
        {
            float side = arm == rig.ArmL ? -1 : 1;
            var pad = b.Armor("ShoulderArmor", arm, Vector3.zero);
            b.Part(pad, ProcMesh.Tube(b.Key + "pauldron", 2.6f, 14, -0.1f, 0.075f, 0.078f, 0f, -0.02f, 0.08f, 0.082f, 0f, 0.035f, 0.06f, 0.062f, 0f, 0.06f, 0.02f, 0.02f, 0f), leather, new Vector3(side * 0.012f, 0, 0), new Vector3(0, 0, side * -12));
            b.Part(pad, Ell("rivet", 0.007f, 0.007f, 0.005f), rivet, new Vector3(side * 0.05f, 0.02f, 0.045f), null, null, false);
            b.Part(pad, Ell("rivet", 0.007f, 0.007f, 0.005f), rivet, new Vector3(side * 0.05f, 0.02f, -0.045f), null, null, false);
        }
        // 팔 보호대 (아래팔을 감은 가죽 + 두 줄 끈)
        foreach (var fore in new[] { rig.ForeL, rig.ForeR })
        {
            b.Part(fore, ProcMesh.Tube(b.Key + "bracer", 2.4f, 14, -0.24f, 0.034f, 0.031f, 0f, -0.04f, 0.048f, 0.046f, 0f), leather, Vector3.zero);
            b.Part(fore, Tube(b.Key + "bracerband", -0.09f, 0.047f, 0.045f, 0f, -0.07f, 0.047f, 0.045f, 0f), strap, Vector3.zero);
            b.Part(fore, Tube(b.Key + "bracerband2", -0.2f, 0.039f, 0.036f, 0f, -0.18f, 0.04f, 0.037f, 0f), strap, Vector3.zero);
        }
        // 무릎 보호대
        foreach (var knee in new[] { rig.ShinL, rig.ShinR })
        {
            var kp = b.Armor("KneeArmor", knee, Vector3.zero);
            b.Part(kp, ProcMesh.Tube(b.Key + "kneepad", 2.8f, 12, -0.08f, 0.05f, 0.02f, 0f, 0.04f, 0.055f, 0.022f, 0f, 0.07f, 0.045f, 0.018f, 0f), leather, new Vector3(0, 0, 0.062f), new Vector3(-6, 0, 0));
            b.Part(kp, Ell("rivet", 0.007f, 0.007f, 0.004f), rivet, new Vector3(0, 0.01f, 0.084f), null, null, false);
        }
        // 가죽 모자 (귀덮개)
        var cap = b.Armor("HelmetArmor", rig.Head, Vector3.zero);
        b.Part(cap, Tube(b.Key + "helmet", Grow(Slice(SkullRings, 0.13f, 0.25f), 1.12f, 1.1f, 0.004f)), leather, new Vector3(0, 0.004f, -0.004f));
        b.Part(cap, ProcMesh.Box("earflap", 0.012f, 0.07f, 0.05f), leather, new Vector3(-0.086f, 0.11f, -0.005f), new Vector3(0, 0, -6));
        b.Part(cap, ProcMesh.Box("earflap", 0.012f, 0.07f, 0.05f), leather, new Vector3(0.086f, 0.11f, -0.005f), new Vector3(0, 0, 6));
        b.Part(cap, Tube(b.Key + "helmetband", 0.155f, 0.088f, 0.11f, -0.004f, 0.172f, 0.086f, 0.108f, -0.006f), strap, Vector3.zero);
        b.Part(cap, Ell("rivet", 0.008f, 0.008f, 0.005f), rivet, new Vector3(0, 0.205f, 0.096f), null, null, false);
    }

    // ================= 03 새총 좀비 =================
    // 후드 #A88A3A(음영 #85692A), 카고 바지 #75704F, 피부 #8E9C7C. 깡마른 몸, 허리에 돌 주머니

    static void SlingshotZombie(Builder b)
    {
        var hoodie = ProcTex.Get(ProcTex.Kind.Cloth, Hex("A88A3A"), 31, 3, 0.55f, Hex("85692A"));
        var h = new Human
        {
            Skin = ProcTex.Get(ProcTex.Kind.ZombieSkin, Hex("8E9C7C"), 31, 0, 0.35f, Hex("6E7B5F")),
            Shirt = hoodie,
            Sleeve = hoodie,
            SleeveTo = -0.6f,
            Pants = ProcTex.Get(ProcTex.Kind.Cloth, Hex("75704F"), 32, 1, 0.5f, Hex("57533a")),
            Shoes = ProcTex.Get(ProcTex.Kind.Leather, Hex("4a463f"), 33, 0, 0.6f),
            Eyes = ProcTex.Glow(Hex("E6E0A3"), new Color(1.3f, 1.2f, 0.55f)),
            Bulk = 0.86f,
            Limb = 0.84f,
            Hunch = 0.06f,
        };
        Humanoid(b, h);
        var rig = b.Rig;

        // 후드: 머리를 뒤에서 감싸고 얼굴만 내놓는다
        var hood = Grow(SkullRings, 1.2f, 1.14f, 0.006f);
        for (int i = 0; i < hood.Length; i += 4) hood[i + 3] -= 0.045f;
        var hoodFull = new List<float>(hood);
        hoodFull.InsertRange(0, new[] { -0.02f, 0.09f, 0.085f, -0.05f, 0.03f, 0.092f, 0.1f, -0.045f });
        b.Part(rig.Head, Tube(b.Key + "hood", hoodFull.ToArray()), hoodie, Vector3.zero);
        // 후드 뾰족한 뒷부분
        b.Part(rig.Head, ProcMesh.Cone("hoodtip", 0.05f, 0.08f), hoodie, new Vector3(0, 0.2f, -0.09f), new Vector3(-125, 0, 0));
        // 후드 끈
        var cord = ProcTex.Plain(Hex("d8cfb4"), 0.3f);
        b.Part(rig.Body, Tube("cord", -0.14f, 0.004f, 0.004f, 0f, 0f, 0.004f, 0.004f, 0f), cord, new Vector3(-0.03f, 0.5f, 0.1f), new Vector3(-8, 0, 3));
        b.Part(rig.Body, Tube("cord", -0.14f, 0.004f, 0.004f, 0f, 0f, 0.004f, 0.004f, 0f), cord, new Vector3(0.03f, 0.5f, 0.1f), new Vector3(-8, 0, -3));
        // 앞주머니
        b.Part(rig.Body, ProcMesh.Box("pocket", 0.2f, 0.1f, 0.016f), ProcTex.Get(ProcTex.Kind.Cloth, Hex("97792f"), 35), new Vector3(0, 0.1f, 0.09f), new Vector3(-6, 0, 0));
        Tatters(b, rig.Body, hoodie, 0.02f, 0.135f, 0.09f, 5, 37, 0.07f);
        // 허리의 돌 주머니
        var hips = rig.Model.Find("Hips");
        var bag = ProcTex.Get(ProcTex.Kind.Leather, Hex("6b4a2e"), 36);
        b.Part(hips, ProcMesh.Tube(b.Key + "stonebag", 2.4f, 12, -0.08f, 0.04f, 0.035f, 0f, -0.02f, 0.06f, 0.05f, 0f, 0.03f, 0.05f, 0.042f, 0f, 0.05f, 0.03f, 0.03f, 0f), bag, new Vector3(0.16f, -0.04f, 0.04f), new Vector3(0, 0, -6));
        b.Part(hips, Ell("bagstone", 0.022f, 0.02f, 0.022f), ProcTex.Get(ProcTex.Kind.Metal, Hex("9A958C"), 38), new Vector3(0.16f, 0.02f, 0.045f));
        // 카고 바지 무릎 덧댐
        b.Part(rig.ShinR, ProcMesh.Box("kneepatch", 0.06f, 0.06f, 0.01f), ProcTex.Get(ProcTex.Kind.Cloth, Hex("5e5a3f"), 39), new Vector3(0, 0, 0.062f));
    }

    // ================= 06 거대 좀비 =================
    // 피부 #7C8A6C(음영 #5D6A51), 변이 오른팔 #9A4A42, 뼈 돌출 #D8CFB8, 등의 약점 혹 #C27A5A, 바지 #4A4F5A

    static void GiantZombie(Builder b)
    {
        var skin = ProcTex.Get(ProcTex.Kind.ZombieSkin, Hex("7C8A6C"), 61, 0, 0.3f, Hex("5D6A51"));
        var mutant = ProcTex.Get(ProcTex.Kind.ZombieSkin, Hex("9A4A42"), 62, 2, 0.1f, Hex("5e2522"));
        var bone = ProcTex.Plain(Hex("D8CFB8"), 0.45f);
        var h = new Human
        {
            Skin = skin,
            Shirt = skin,
            Pants = ProcTex.Get(ProcTex.Kind.Cloth, Hex("4A4F5A"), 63, 1, 0.6f, Hex("33373f")),
            Shoes = skin,
            Eyes = ProcTex.Glow(Hex("e8d27a"), new Color(1.6f, 1.2f, 0.35f)),
            Bulk = 1.55f,
            Limb = 1.55f,
            HeadSize = 0.82f,
            Hunch = 0.16f,
            Belt = true,
        };
        Humanoid(b, h);
        var rig = b.Rig;
        var hips = rig.Model.Find("Hips");

        // 발: 신발 대신 맨발(발가락이 갈라진 두꺼운 발)
        // 배와 가슴 근육 덩이
        b.Part(rig.Body, Ell("gut", 0.2f, 0.16f, 0.13f), skin, new Vector3(0, 0.13f, 0.1f));
        b.Part(rig.Body, Ell("pec", 0.12f, 0.08f, 0.07f), skin, new Vector3(-0.1f, 0.38f, 0.12f), new Vector3(-10, 0, 8));
        b.Part(rig.Body, Ell("pec", 0.12f, 0.08f, 0.07f), skin, new Vector3(0.1f, 0.38f, 0.12f), new Vector3(-10, 0, -8));
        b.Part(rig.Body, Ell("trap", 0.2f, 0.08f, 0.1f), skin, new Vector3(0, 0.5f, 0.02f));
        // 가슴을 가로지른 꿰맨 흉터
        var stitch = ProcTex.Plain(Hex("2a1a14"), 0.3f);
        b.Part(rig.Body, ProcMesh.Box("scar", 0.3f, 0.012f, 0.01f), ProcTex.Plain(Hex("6b3a32"), 0.5f), new Vector3(-0.03f, 0.3f, 0.17f), new Vector3(-12, 0, 10));
        for (int i = 0; i < 6; i++)
            b.Part(rig.Body, ProcMesh.Box("stitch", 0.008f, 0.04f, 0.008f), stitch, new Vector3(-0.15f + i * 0.05f, 0.278f + i * 0.0088f, 0.174f), new Vector3(-12, 0, 10), null, false);
        // 등의 혹 (약점, 밝은 색)
        var hump = ProcTex.Get(ProcTex.Kind.ZombieSkin, Hex("C27A5A"), 64, 0, 0.05f, Hex("9a5a40"));
        b.Part(rig.Body, Ell("hump", 0.17f, 0.15f, 0.12f), hump, new Vector3(0.02f, 0.42f, -0.17f), new Vector3(20, 0, 8));
        b.Part(rig.Body, Ell("humpblister", 0.03f, 0.03f, 0.02f), ProcTex.Plain(Hex("e09a74"), 0.7f), new Vector3(0.06f, 0.46f, -0.28f), new Vector3(30, 0, 0));
        b.Part(rig.Body, Ell("humpblister", 0.025f, 0.025f, 0.018f), ProcTex.Plain(Hex("e09a74"), 0.7f), new Vector3(-0.04f, 0.39f, -0.28f), new Vector3(10, 0, 0));
        // 어깨에서 솟은 뼈 돌기
        for (int i = 0; i < 3; i++)
            b.Part(rig.Body, ProcMesh.Cone("bonespike", 0.025f, 0.11f), bone, new Vector3(0.18f + i * 0.03f, 0.55f - i * 0.04f, -0.04f - i * 0.03f), new Vector3(-20 - i * 10, 0, -30));
        b.Part(rig.Body, ProcMesh.Cone("bonespike", 0.02f, 0.08f), bone, new Vector3(-0.2f, 0.53f, -0.06f), new Vector3(-25, 0, 35));

        // 변이 오른팔: 굵고 길며 붉게 부풀었다. 뼈 돌기와 큰 발톱
        rig.ArmR.localScale = new Vector3(1.35f, 1.18f, 1.35f);
        foreach (var r in rig.ArmR.GetComponentsInChildren<MeshRenderer>()) r.sharedMaterial = mutant;
        for (int i = 0; i < 3; i++)
            b.Part(rig.ForeR, ProcMesh.Cone("armspike", 0.016f, 0.07f), bone, new Vector3(0.03f, -0.05f - i * 0.07f, -0.02f), new Vector3(0, 0, -70 - i * 6));
        b.Part(rig.ArmR, ProcMesh.Cone("armspike", 0.018f, 0.08f), bone, new Vector3(0.05f, -0.12f, -0.02f), new Vector3(10, 0, -75));
        for (int i = 0; i < 4; i++)
            b.Part(rig.HandR, ProcMesh.Cone("claw", 0.009f, 0.05f), bone, new Vector3(0, -0.17f, 0.027f - i * 0.018f), new Vector3(180, 0, 25));

        // 정강이에서 찢어진 바지 + 발목 족쇄
        var pants = h.Pants;
        foreach (var knee in new[] { rig.ShinL, rig.ShinR })
        {
            Tatters(b, knee, pants, -0.2f, 0.07f, 0.075f, 5, knee == rig.ShinL ? 65 : 66, 0.07f);
            b.Part(knee, Tube(b.Key + "calfskin", Slice(Grow(ShinRings, 1.5f, 1.5f), -0.40f, -0.2f)), skin, Vector3.zero);
        }
        b.Part(rig.ShinL, Tube(b.Key + "shackle", -0.33f, 0.06f, 0.064f, 0f, -0.3f, 0.06f, 0.064f, 0f), ProcTex.Get(ProcTex.Kind.Metal, Hex("55524c"), 67), Vector3.zero);
        b.Part(rig.ShinL, Tube("chain", -0.06f, 0.008f, 0.008f, 0f, 0f, 0.008f, 0.008f, 0f), ProcTex.Get(ProcTex.Kind.Metal, Hex("55524c"), 67), new Vector3(0.04f, -0.33f, 0.04f), new Vector3(30, 0, 40));
        Tatters(b, hips, pants, -0.12f, 0.24f, 0.17f, 6, 68, 0.08f);
    }

    // ================= 04 약탈자 =================
    // 피부 #C9A27E, 가죽 조끼 #3B3431, 반다나 #8E2B22, 카고 바지 #5E6241, 고글 렌즈 #4B6B78

    static void Raider(Builder b)
    {
        var h = new Human
        {
            Skin = ProcTex.Get(ProcTex.Kind.Skin, Hex("C9A27E"), 41, 0, 0.25f, Hex("a07a58")),
            Shirt = ProcTex.Get(ProcTex.Kind.Cloth, Hex("6f6d68"), 41, 0, 0.5f),
            Sleeve = ProcTex.Get(ProcTex.Kind.Cloth, Hex("6f6d68"), 41, 0, 0.5f),
            SleeveTo = -0.1f,
            Pants = ProcTex.Get(ProcTex.Kind.Cloth, Hex("5E6241"), 42, 0, 0.5f, Hex("444830")),
            Shoes = ProcTex.Get(ProcTex.Kind.Leather, Hex("2c221b"), 43, 0, 0.5f),
            Hair = ProcTex.Get(ProcTex.Kind.Hair, Hex("2b2119"), 41),
            Zombie = false,
            Boots = true,
            Bulk = 1.04f,
        };
        Humanoid(b, h);
        var rig = b.Rig;
        var vest = ProcTex.Get(ProcTex.Kind.Leather, Hex("3B3431"), 44, 0, 0.4f, Hex("26211f"));

        // 앞이 열린 가죽 조끼: 등판 + 양쪽 앞판
        var shell = Grow(Slice(TorsoRings, 0.05f, 0.5f), 1.07f * 1.04f, 1.1f * 1.03f, 0.004f);
        b.Part(rig.Body, ProcMesh.Tube(b.Key + "vest", 2.6f, 16, shell), vest, Vector3.zero);
        b.Part(rig.Body, ProcMesh.Box("shirtfront", 0.09f, 0.42f, 0.01f), h.Shirt, new Vector3(0, 0.27f, 0.122f), new Vector3(-3, 0, 0));
        b.Part(rig.Body, ProcMesh.Box("vestlapel", 0.035f, 0.3f, 0.012f), vest, new Vector3(-0.055f, 0.36f, 0.123f), new Vector3(-6, 0, -8));
        b.Part(rig.Body, ProcMesh.Box("vestlapel", 0.035f, 0.3f, 0.012f), vest, new Vector3(0.055f, 0.36f, 0.123f), new Vector3(-6, 0, 8));
        b.Part(rig.Body, ProcMesh.Box("vestpatch", 0.03f, 0.03f, 0.008f), ProcTex.Plain(Hex("8E2B22"), 0.4f), new Vector3(-0.1f, 0.38f, 0.122f), new Vector3(-6, -12, 0));
        // 등판의 흰 X 표시
        var paint = ProcTex.Plain(Hex("d9d4c7"), 0.2f);
        b.Part(rig.Body, ProcMesh.Box("xmark", 0.03f, 0.2f, 0.006f), paint, new Vector3(0, 0.3f, -0.128f), new Vector3(0, 0, 30), null, false);
        b.Part(rig.Body, ProcMesh.Box("xmark", 0.03f, 0.2f, 0.006f), paint, new Vector3(0, 0.3f, -0.128f), new Vector3(0, 0, -30), null, false);

        // 반다나: 코와 입을 가리고 뒤통수에 매듭
        var bandana = ProcTex.Get(ProcTex.Kind.Cloth, Hex("8E2B22"), 45, 0, 0.3f, Hex("6a1f18"));
        b.Part(rig.Head, Tube(b.Key + "bandana", -0.01f, 0.06f, 0.065f, 0.05f, 0.03f, 0.074f, 0.092f, 0.022f, 0.08f, 0.079f, 0.104f, 0.012f, 0.122f, 0.08f, 0.106f, 0.008f), bandana, Vector3.zero);
        b.Part(rig.Head, ProcMesh.Box("bandanaflap", 0.11f, 0.07f, 0.012f), bandana, new Vector3(0, -0.005f, 0.085f), new Vector3(-28, 0, 45), new Vector3(1, 1, 1));
        b.Part(rig.Head, ProcMesh.Box("knot", 0.04f, 0.03f, 0.025f), bandana, new Vector3(0, 0.11f, -0.1f));
        b.Part(rig.Head, ProcMesh.Box("knottail", 0.025f, 0.08f, 0.006f), bandana, new Vector3(-0.012f, 0.06f, -0.105f), new Vector3(10, 0, -12));
        b.Part(rig.Head, ProcMesh.Box("knottail", 0.025f, 0.07f, 0.006f), bandana, new Vector3(0.014f, 0.065f, -0.105f), new Vector3(10, 0, 14));
        // 이마 위로 올린 고글
        var frame = ProcTex.Plain(Hex("2a2a2a"), 0.4f);
        var lens = ProcTex.Plain(Hex("4B6B78"), 0.95f, 0.3f);
        b.Part(rig.Head, Tube(b.Key + "gogglestrap", 0.18f, 0.083f, 0.105f, -0.004f, 0.2f, 0.08f, 0.102f, -0.006f), frame, Vector3.zero);
        for (int s = -1; s <= 1; s += 2)
        {
            b.Part(rig.Head, Tube("goggle", -0.012f, 0.024f, 0.024f, 0f, 0.012f, 0.022f, 0.022f, 0f), frame, new Vector3(s * 0.033f, 0.192f, 0.094f), new Vector3(-75, 0, 0));
            b.Part(rig.Head, Ell("lens", 0.019f, 0.006f, 0.019f), lens, new Vector3(s * 0.033f, 0.195f, 0.104f), new Vector3(-75, 0, 0), null, false);
        }
        // 눈 밑 흉터
        b.Part(rig.Head, ProcMesh.Box("scarface", 0.004f, 0.03f, 0.004f), ProcTex.Plain(Hex("8a5a4a"), 0.4f), new Vector3(0.045f, 0.13f, 0.091f), new Vector3(0, 30, 20), null, false);

        // 팔에 감은 붕대와 손가락 없는 장갑
        var wrap = ProcTex.Get(ProcTex.Kind.Cloth, Hex("cdbb98"), 46, 0, 0.5f);
        var glove = ProcTex.Get(ProcTex.Kind.Leather, Hex("25201c"), 47);
        foreach (var fore in new[] { rig.ForeL, rig.ForeR })
        {
            b.Part(fore, Tube(b.Key + "wrap", Grow(Slice(ForearmRings, -0.27f, -0.12f), 1.12f, 1.14f)), wrap, Vector3.zero);
            b.Part(fore == rig.ForeL ? rig.HandL : rig.HandR, Tube(b.Key + "glove", 0.012f, 0.02f, 0.028f, 0f, -0.03f, 0.019f, 0.04f, 0f, -0.08f, 0.018f, 0.043f, 0f, -0.098f, 0.016f, 0.04f, 0f), glove, Vector3.zero);
        }
        // 카고 주머니와 무릎 보호대
        var pocket = ProcTex.Get(ProcTex.Kind.Cloth, Hex("50543a"), 48);
        b.Part(rig.LegL, ProcMesh.Box("cargo", 0.02f, 0.12f, 0.1f), pocket, new Vector3(-0.088f, -0.22f, 0.0f));
        b.Part(rig.LegR, ProcMesh.Box("cargo", 0.02f, 0.12f, 0.1f), pocket, new Vector3(0.088f, -0.22f, 0.0f));
        b.Part(rig.ShinL, ProcMesh.Tube(b.Key + "kneeguard", 3f, 12, -0.07f, 0.05f, 0.02f, 0f, 0.05f, 0.055f, 0.022f, 0f), ProcTex.Plain(Hex("1f1f1f"), 0.35f), new Vector3(0, 0, 0.064f), new Vector3(-6, 0, 0));
        b.Part(rig.Model.Find("Hips"), ProcMesh.Box("pouch", 0.08f, 0.08f, 0.05f), ProcTex.Get(ProcTex.Kind.Leather, Hex("3a2f26"), 49), new Vector3(-0.15f, -0.01f, 0.05f), new Vector3(0, -30, 0));
    }

    // ================= NPC 잡상인 =================
    // 코트 #7A6A4E, 스카프 #4E7470, 등짐 #9C8A62, 모자 #4A3B2C, 등불 #B8963E, 피부 #C9A27E

    static void Merchant(Builder b)
    {
        var coat = ProcTex.Get(ProcTex.Kind.Cloth, Hex("7A6A4E"), 71, 0, 0.55f, Hex("5a4d36"));
        var h = new Human
        {
            Skin = ProcTex.Get(ProcTex.Kind.Skin, Hex("C9A27E"), 71, 0, 0.3f, Hex("a07a58")),
            Shirt = coat,
            Sleeve = coat,
            SleeveTo = -0.6f,
            Pants = ProcTex.Get(ProcTex.Kind.Cloth, Hex("4a4438"), 72, 0, 0.5f),
            Shoes = ProcTex.Get(ProcTex.Kind.Leather, Hex("3b2b1f"), 73, 0, 0.5f),
            Hair = ProcTex.Get(ProcTex.Kind.Hair, Hex("8f8a82"), 71),
            Zombie = false,
            Boots = true,
            Bulk = 1.06f,
            Hunch = 0.03f,
        };
        Humanoid(b, h);
        var rig = b.Rig;

        // 무릎까지 오는 긴 코트 자락 (몸통에 붙어 함께 숙인다) + 앞섶 단추
        b.Part(rig.Body, Tube(b.Key + "coatskirt", -0.5f, 0.22f, 0.16f, 0.0f, -0.25f, 0.2f, 0.145f, 0.004f, 0.0f, 0.172f, 0.118f, 0.006f, 0.06f, 0.168f, 0.114f, 0.006f), coat, Vector3.zero);
        b.Part(rig.Body, ProcMesh.Box("placket", 0.03f, 0.9f, 0.01f), ProcTex.Get(ProcTex.Kind.Cloth, Hex("5f513a"), 74), new Vector3(0.02f, 0.0f, 0.13f), new Vector3(2, 0, 0));
        for (int i = 0; i < 4; i++)
            b.Part(rig.Body, Ell("button", 0.008f, 0.008f, 0.004f), ProcTex.Plain(Hex("3a2a1c"), 0.5f), new Vector3(0.02f, 0.4f - i * 0.12f, 0.12f + (i == 0 ? 0.004f : 0)), null, null, false);
        b.Part(rig.Body, ProcMesh.Box("coatbelt", 0.36f, 0.04f, 0.26f, 8), ProcTex.Get(ProcTex.Kind.Leather, Hex("3b2b1f"), 75), new Vector3(0, 0.06f, 0.0f));
        b.Part(rig.Body, ProcMesh.Box("beltpouch", 0.07f, 0.08f, 0.04f), ProcTex.Get(ProcTex.Kind.Leather, Hex("5a3f28"), 76), new Vector3(-0.12f, 0.02f, 0.1f), new Vector3(0, -20, 0));

        // 목에 두른 스카프
        var scarf = ProcTex.Get(ProcTex.Kind.Knit, Hex("4E7470"), 77, 0, 0.3f, Hex("38544f"));
        b.Part(rig.Body, Tube(b.Key + "scarf", 0.46f, 0.105f, 0.09f, 0.006f, 0.52f, 0.09f, 0.08f, 0.01f, 0.6f, 0.07f, 0.068f, 0.014f), scarf, Vector3.zero);
        b.Part(rig.Body, ProcMesh.Box("scarfend", 0.06f, 0.2f, 0.018f), scarf, new Vector3(-0.045f, 0.4f, 0.128f), new Vector3(-10, 0, 8));

        // 회색 수염과 콧수염
        var beard = ProcTex.Get(ProcTex.Kind.Hair, Hex("8f8a82"), 78);
        b.Part(rig.Head, Tube(b.Key + "beard", -0.02f, 0.03f, 0.03f, 0.06f, 0.0f, 0.055f, 0.05f, 0.05f, 0.05f, 0.072f, 0.085f, 0.022f, 0.085f, 0.073f, 0.092f, 0.016f), beard, Vector3.zero);
        b.Part(rig.Head, ProcMesh.Box("mustache", 0.06f, 0.014f, 0.02f), beard, new Vector3(0, 0.092f, 0.098f));

        // 챙 넓은 모자 (붉은 띠)
        var hat = ProcTex.Get(ProcTex.Kind.Leather, Hex("4A3B2C"), 79, 0, 0.4f, Hex("33281d"));
        b.Part(rig.Head, ProcMesh.Tube("hatbrim", 2, 18, -0.006f, 0.17f, 0.18f, 0f, 0.006f, 0.17f, 0.18f, 0f), hat, new Vector3(0, 0.19f, -0.005f), new Vector3(-4, 0, 0));
        b.Part(rig.Head, Tube(b.Key + "hatcrown", 0.19f, 0.088f, 0.106f, -0.008f, 0.26f, 0.08f, 0.096f, -0.01f, 0.29f, 0.07f, 0.084f, -0.012f, 0.3f, 0.02f, 0.03f, -0.012f), hat, Vector3.zero);
        b.Part(rig.Head, Tube(b.Key + "hatband", 0.195f, 0.09f, 0.108f, -0.008f, 0.215f, 0.088f, 0.106f, -0.009f), ProcTex.Plain(Hex("8a3a2c"), 0.3f), Vector3.zero);

        // 큰 등짐: 배낭 + 위에 만 침낭(붉은색) + 매단 냄비 + 옆에 매단 등불
        var pack = ProcTex.Get(ProcTex.Kind.Cloth, Hex("9C8A62"), 80, 0, 0.6f, Hex("75684a"));
        var packJoint = b.Joint("Pack", rig.Body, new Vector3(0, 0.28f, -0.26f));
        b.Part(packJoint, ProcMesh.Box("pack", 0.44f, 0.6f, 0.28f, 5), pack, Vector3.zero);
        b.Part(packJoint, ProcMesh.Box("packflap", 0.42f, 0.18f, 0.06f, 5), ProcTex.Get(ProcTex.Kind.Cloth, Hex("85744f"), 81), new Vector3(0, -0.12f, -0.15f));
        b.Part(packJoint, ProcMesh.Tube("bedroll", 2, 14, -0.27f, 0.08f, 0.08f, 0f, 0.27f, 0.08f, 0.08f, 0f), ProcTex.Get(ProcTex.Kind.Cloth, Hex("8a3a2c"), 82, 0, 0.5f), new Vector3(0, 0.38f, 0.02f), new Vector3(0, 0, 90));
        b.Part(packJoint, ProcMesh.Tube("pot", 2, 14, -0.07f, 0.09f, 0.09f, 0f, 0.07f, 0.1f, 0.1f, 0f), ProcTex.Get(ProcTex.Kind.Metal, Hex("6a6a66"), 83), new Vector3(0, 0.47f, 0.0f));
        var strap = ProcTex.Get(ProcTex.Kind.Leather, Hex("3b2b1f"), 75);
        b.Part(rig.Body, ProcMesh.Box("packstrap", 0.04f, 0.5f, 0.02f), strap, new Vector3(-0.11f, 0.28f, 0.12f), new Vector3(-4, 0, 4));
        b.Part(rig.Body, ProcMesh.Box("packstrap", 0.04f, 0.5f, 0.02f), strap, new Vector3(0.11f, 0.28f, 0.12f), new Vector3(-4, 0, -4));
        var lantern = b.Joint("Lantern", packJoint, new Vector3(0.27f, -0.05f, 0.0f));
        var brass = ProcTex.Get(ProcTex.Kind.Metal, Hex("B8963E"), 84);
        b.Part(lantern, ProcMesh.Tube("lanterncap", 6, 8, 0.07f, 0.05f, 0.05f, 0f, 0.1f, 0.02f, 0.02f, 0f), brass, Vector3.zero);
        b.Part(lantern, ProcMesh.Tube("lanternglass", 4, 8, -0.06f, 0.04f, 0.04f, 0f, 0.07f, 0.04f, 0.04f, 0f), ProcTex.Glow(Hex("ffcf7a"), new Color(2.4f, 1.5f, 0.6f)), Vector3.zero, null, null, false);
        b.Part(lantern, ProcMesh.Tube("lanternbase", 6, 8, -0.09f, 0.05f, 0.05f, 0f, -0.06f, 0.05f, 0.05f, 0f), brass, Vector3.zero);
        b.Part(lantern, Tube("lanternhook", 0.1f, 0.006f, 0.006f, 0f, 0.16f, 0.006f, 0.006f, 0f), brass, Vector3.zero);
        rig.Lantern = lantern;

        // 오른손 지팡이: 팔꿈치를 굽혀 짚는다
        rig.ArmR.localRotation = Quaternion.Euler(-8, 0, 6);
        rig.ForeR.localRotation = Quaternion.Euler(-62, 0, 0);
        var staff = b.Joint("Staff", rig.HandR, new Vector3(-0.005f, -0.07f, 0f));
        b.Part(staff, ProcMesh.Tube("staff", 2, 8, -1.05f, 0.016f, 0.016f, 0f, 0.55f, 0.018f, 0.018f, 0f, 0.6f, 0.022f, 0.022f, 0f), ProcTex.Get(ProcTex.Kind.Wood, Hex("6a4a2e"), 85), Vector3.zero, new Vector3(62, 0, 0));
        rig.ArmL.localRotation = Quaternion.Euler(4, 0, -6);
        rig.ForeL.localRotation = Quaternion.Euler(-20, 0, 0);
    }

    // ================= 컷씬 주인공 (시민) =================
    // 피부 #C9A27E, 밝은 회색 반팔 티셔츠, 청바지, 흰 운동화, 짧은 검은 머리. 상처·피 없음

    static void Civilian(Builder b, int variant)
    {
        if (variant == 1) { Expedition(b); return; }
        int seed = 90 + variant;
        var shirt = ProcTex.Get(ProcTex.Kind.Cloth, Hex("d9d4c9"), seed, 0, 0.06f, Hex("aaa497"));
        var h = new Human
        {
            Skin = ProcTex.Get(ProcTex.Kind.Skin, Hex("C9A27E"), seed, 0, 0.04f, Hex("a67e5d")),
            Shirt = shirt,
            Sleeve = shirt,
            SleeveTo = -0.13f,
            Pants = ProcTex.Get(ProcTex.Kind.Denim, Hex("3f5273"), seed, 0, 0.1f, Hex("2b3950")),
            Shoes = ProcTex.Get(ProcTex.Kind.Leather, Hex("dcd9d3"), seed, 0, 0.12f, Hex("b8b4ac")),
            Hair = ProcTex.Get(ProcTex.Kind.Hair, Hex("1e1816"), seed),
            Zombie = false,
            PantsBaggy = false,
            Belt = true,
            SmoothFace = true,
            LiveFingers = true, // 입을 가릴 때 손가락 마디가 따로 감긴다 (CutsceneActor)
            NeckDrop = 0.035f, // 목이 길어 보인다는 피드백: 머리를 3.5cm 내린다
        };
        h.Skin.SetFloat("_BumpScale", 0.12f);   // 가까이 찍히는 얼굴이라 모공 요철을 약하게
        Humanoid(b, h);
        var rig = b.Rig;
        // 티셔츠 아랫단(허리띠 위로 살짝 늘어진다)과 목둘레 띠
        b.Part(rig.Body, Tube(b.Key + "hem", -0.03f, 0.158f, 0.106f, 0.004f, 0.03f, 0.154f, 0.102f, 0.002f), shirt, Vector3.zero);
        b.Part(rig.Body, Tube(b.Key + "neckband", 0.515f, 0.072f, 0.068f, 0f, 0.535f, 0.07f, 0.066f, 0.002f), ProcTex.Get(ProcTex.Kind.Cloth, Hex("c4bfb3"), seed + 1, 0, 0.06f), Vector3.zero);
        // 손목시계 (눈썹은 얼굴 조각으로 붙는다)
        b.Part(rig.ForeL, Tube(b.Key + "watchband", -0.255f, 0.028f, 0.023f, 0f, -0.235f, 0.029f, 0.024f, 0f), ProcTex.Plain(Hex("2a2522"), 0.3f), Vector3.zero);
        b.Part(rig.ForeL, ProcMesh.Box("watchface", 0.024f, 0.02f, 0.006f), ProcTex.Plain(Hex("8c8f93"), 0.8f, 0.7f), new Vector3(-0.028f, -0.245f, 0f), new Vector3(0, 90, 0), null, false);
    }

    // 같은 사람의 탐험 차림 (방공호로 돌아오는 연출): 1인칭 팔(HandRig)과 똑같은 재질 호출로 만든 어두운 국방색 야상(세운 깃·지퍼·가슴·허리 주머니·접어 올린 소맷단),
    // 낡은 검은 가죽 반장갑(손목 끈·버클·너클 패드, 손가락은 맨살), 짙은 올리브 작업 바지(카고 주머니), 검은 군화
    static void Expedition(Builder b)
    {
        int seed = 90;
        var jacket = ProcTex.Get(ProcTex.Kind.Cloth, new Color(0.16f, 0.19f, 0.14f), 402, 0, 0.5f, new Color(0.1f, 0.12f, 0.09f));
        var cuff = ProcTex.Get(ProcTex.Kind.Cloth, new Color(0.12f, 0.14f, 0.11f), 403, 0, 0.55f);
        var glove = ProcTex.Get(ProcTex.Kind.Leather, new Color(0.17f, 0.14f, 0.12f), 404, 0, 0.6f, new Color(0.09f, 0.075f, 0.065f));
        var pad = ProcTex.Get(ProcTex.Kind.Rubber, new Color(0.2f, 0.19f, 0.18f), 405, 0, 0.5f);
        var strap = ProcTex.Plain(new Color(0.08f, 0.075f, 0.07f), 0.25f);
        var pants = ProcTex.Get(ProcTex.Kind.Cloth, Hex("3b3e35"), seed + 2, 0, 0.55f, Hex("262821"));
        var h = new Human
        {
            Skin = ProcTex.Get(ProcTex.Kind.Skin, Hex("C9A27E"), seed, 0, 0.04f, Hex("a67e5d")),
            Shirt = jacket,
            Sleeve = jacket,
            SleeveTo = -0.6f,
            Pants = pants,
            Shoes = ProcTex.Get(ProcTex.Kind.Leather, Hex("231f1b"), seed + 3, 0, 0.35f, Hex("15120f")),
            Hair = ProcTex.Get(ProcTex.Kind.Hair, Hex("1e1816"), seed),
            Zombie = false,
            PantsBaggy = true,
            Belt = true,
            Boots = true,
            SmoothFace = true,
            NeckDrop = 0.035f,
        };
        h.Skin.SetFloat("_BumpScale", 0.12f);
        Humanoid(b, h);
        var rig = b.Rig;
        string k = b.Key;
        // 야상: 세운 깃, 허리 밑단, 앞 지퍼 선(몸통 겉면을 따라), 가슴 주머니 둘(덮개)과 허리 주머니 둘
        b.Part(rig.Body, Tube(k + "jcollar", 0.49f, 0.082f, 0.074f, -0.002f, 0.545f, 0.078f, 0.07f, 0.004f), jacket, Vector3.zero);
        b.Part(rig.Body, Tube(k + "jhem", -0.035f, 0.16f, 0.106f, 0.004f, 0.03f, 0.155f, 0.103f, 0.002f), jacket, Vector3.zero);
        var zip = new System.Collections.Generic.List<float>();
        foreach (var y in new[] { 0.0f, 0.12f, 0.24f, 0.36f, 0.44f, 0.5f })
        {
            zip.Add(y); zip.Add(0.007f); zip.Add(0.004f); zip.Add(TorsoSurfaceZ(y, 0f, true) + 0.004f);
        }
        b.Part(rig.Body, Tube(k + "jzip", zip.ToArray()), strap, Vector3.zero);
        foreach (int s in new[] { -1, 1 })
        {
            b.Part(rig.Body, ProcMesh.Box("jpocket", 0.1f, 0.11f, 0.012f), jacket, new Vector3(s * 0.078f, 0.335f, TorsoSurfaceZ(0.335f, 0.078f, true) + 0.005f), new Vector3(0, s * -6, 0));
            b.Part(rig.Body, ProcMesh.Box("jflap", 0.104f, 0.03f, 0.016f), jacket, new Vector3(s * 0.078f, 0.395f, TorsoSurfaceZ(0.395f, 0.078f, true) + 0.007f), new Vector3(0, s * -6, 0));
            b.Part(rig.Body, ProcMesh.Box("jpocketlow", 0.12f, 0.12f, 0.012f), jacket, new Vector3(s * 0.1f, 0.12f, TorsoSurfaceZ(0.12f, 0.1f, true) + 0.005f), new Vector3(0, s * -10, 0));
        }
        // 접어 올린 소맷단 (1인칭 소매와 같은 어두운 색)
        foreach (var fore in new[] { rig.ForeL, rig.ForeR })
            b.Part(fore, Tube(k + "rcuff", -0.275f, 0.037f, 0.035f, 0f, -0.262f, 0.039f, 0.037f, 0f, -0.235f, 0.038f, 0.036f, 0f, -0.222f, 0.035f, 0.033f, 0f), cuff, Vector3.zero);
        // 반장갑: 손바닥·손등을 덮는 가죽, 손가락 쪽 꿰맨 단, 손목 끈과 버클, 손등 너클 패드. 손가락은 맨살
        foreach (int s in new[] { -1, 1 })
        {
            var hand = s < 0 ? rig.HandL : rig.HandR;
            b.Part(hand, Tube(k + "glove", 0.012f, 0.02f, 0.028f, 0f, -0.03f, 0.019f, 0.04f, 0f, -0.08f, 0.018f, 0.043f, 0f, -0.098f, 0.016f, 0.04f, 0f), glove, Vector3.zero);
            b.Part(hand, Tube(k + "ghem", -0.1f, 0.0172f, 0.0412f, 0f, -0.093f, 0.0172f, 0.0412f, 0f), strap, Vector3.zero);
            b.Part(hand, Tube(k + "gstrap", 0.004f, 0.0215f, 0.0305f, 0f, -0.014f, 0.0215f, 0.0305f, 0f), strap, Vector3.zero);
            b.Part(hand, ProcMesh.Box("gbuckle", 0.006f, 0.012f, 0.014f), pad, new Vector3(s * 0.023f, -0.005f, -0.004f));
            b.Part(hand, ProcMesh.Box("gpad", 0.008f, 0.018f, 0.05f), pad, new Vector3(s * 0.02f, -0.078f, 0f));
        }
        // 카고 주머니 (허벅지 바깥)
        b.Part(rig.LegL, ProcMesh.Box("cargo", 0.02f, 0.12f, 0.1f), pants, new Vector3(-0.088f, -0.22f, 0.0f));
        b.Part(rig.LegR, ProcMesh.Box("cargo", 0.02f, 0.12f, 0.1f), pants, new Vector3(0.088f, -0.22f, 0.0f));
    }

    // ================= 05 들개 =================
    // 털 #8A7660(음영 #6A5946), 등갈기 #45392E, 배·주둥이 #A8967E, 눈 #C9A642, 목줄 #7E2E24

    static void Dog(Builder b)
    {
        var rig = b.Rig;
        var fur = ProcTex.Get(ProcTex.Kind.Fur, Hex("8A7660"), 51, 0, 0.35f, Hex("6A5946"));
        var dark = ProcTex.Get(ProcTex.Kind.Fur, Hex("45392E"), 52, 0, 0.3f);
        var light = ProcTex.Get(ProcTex.Kind.Fur, Hex("A8967E"), 53, 0, 0.3f, Hex("8c7a62"));
        var nose = ProcTex.Plain(Hex("17120f"), 0.75f);
        var tooth = ProcTex.Plain(Hex("d8cfb0"), 0.5f);
        var gum = ProcTex.Plain(Hex("5a2420"), 0.6f);

        rig.Body = b.Joint("Body", rig.Model, new Vector3(0, 0.56f, 0));
        // 몸통: 앞(+z)으로 긴 관 (부품을 x축 90도로 눕혀 고리 z치우침이 아래쪽이 된다). 깊은 가슴, 홀쭉한 허리
        var torso = ProcMesh.Tube("dogtorso2", 2, 18,
            -0.53f, 0.02f, 0.02f, 0.0f,
            -0.51f, 0.065f, 0.075f, 0.01f,
            -0.47f, 0.1f, 0.112f, 0.01f,
            -0.39f, 0.118f, 0.128f, 0.01f,
            -0.26f, 0.1f, 0.112f, -0.008f,
            -0.12f, 0.098f, 0.118f, -0.004f,
            0.02f, 0.11f, 0.142f, 0.018f,
            0.14f, 0.12f, 0.16f, 0.032f,
            0.25f, 0.122f, 0.16f, 0.03f,
            0.33f, 0.108f, 0.138f, 0.014f,
            0.39f, 0.08f, 0.1f, 0.0f,
            0.42f, 0.035f, 0.045f, 0f);
        b.Part(rig.Body, torso, fur, Vector3.zero, new Vector3(90, 0, 0));
        // 등줄기를 따라 어두운 털 (몸통에 붙은 낮은 띠), 배 쪽 밝은 털
        b.Part(rig.Body, ProcMesh.Tube("dogback2", 2, 12, -0.46f, 0.04f, 0.02f, 0f, -0.1f, 0.05f, 0.022f, 0f, 0.3f, 0.06f, 0.025f, 0f), dark, new Vector3(0, 0.1f, 0), new Vector3(90, 0, 0));
        b.Part(rig.Body, ProcMesh.Tube("dogbelly", 2, 12, 0.0f, 0.06f, 0.03f, 0f, 0.12f, 0.085f, 0.04f, 0f, 0.3f, 0.08f, 0.035f, 0f), light, new Vector3(0, -0.16f, 0f), new Vector3(90, 0, 0));
        // 드러난 갈비뼈 (옆구리의 줄)
        for (int s = -1; s <= 1; s += 2)
            for (int i = 0; i < 3; i++)
                b.Part(rig.Body, ProcMesh.Box("rib", 0.006f, 0.1f, 0.016f), ProcTex.Plain(Hex("5c4b3b"), 0.2f), new Vector3(s * 0.106f, -0.02f, -0.02f + i * 0.055f), new Vector3(-12, 0, s * 6), null, false);
        // 옆구리의 흉터
        b.Part(rig.Body, Ell("dogscar", 0.004f, 0.03f, 0.05f), ProcTex.Plain(Hex("a8706a"), 0.5f), new Vector3(0.1f, 0.02f, -0.18f), new Vector3(0, 0, 0), null, false);

        // 목·머리
        rig.Neck = b.Joint("Neck", rig.Body, new Vector3(0, 0.05f, 0.3f));
        b.Part(rig.Neck, ProcMesh.Tube("dogneck", 2, 14, -0.05f, 0.09f, 0.1f, 0f, 0.1f, 0.078f, 0.085f, 0f, 0.2f, 0.068f, 0.072f, 0f), fur, Vector3.zero, new Vector3(52, 0, 0));
        // 목줄과 인식표
        b.Part(rig.Neck, ProcMesh.Tube("dogcollar", 2, 16, 0.02f, 0.088f, 0.094f, 0f, 0.055f, 0.086f, 0.092f, 0f), ProcTex.Get(ProcTex.Kind.Leather, Hex("7E2E24"), 54, 0, 0.3f), Vector3.zero, new Vector3(52, 0, 0));
        b.Part(rig.Neck, ProcMesh.Box("dogtag", 0.022f, 0.03f, 0.004f), ProcTex.Plain(Hex("a6a39a"), 0.7f, 0.8f), new Vector3(0, -0.06f, 0.07f), new Vector3(20, 0, 0), null, false);
        // 목덜미·어깨의 뻣뻣한 갈기
        for (int i = 0; i < 6; i++)
            b.Part(rig.Neck, ProcMesh.Cone("mane", 0.02f, 0.07f), dark, new Vector3((i % 2 == 0 ? -0.02f : 0.02f), 0.07f + i * 0.02f, -0.07f - i * 0.025f + 0.08f), new Vector3(-55, 0, (i % 2 == 0 ? 10 : -10)));
        for (int i = 0; i < 4; i++)
            b.Part(rig.Body, ProcMesh.Cone("mane", 0.022f, 0.08f), dark, new Vector3((i % 2 == 0 ? -0.025f : 0.025f), 0.14f, 0.2f - i * 0.07f), new Vector3(-50, 0, (i % 2 == 0 ? 8 : -8)));

        rig.Head = b.Joint("Head", rig.Neck, new Vector3(0, 0.15f, 0.12f));
        b.Part(rig.Head, Ell("dogskull", 0.075f, 0.068f, 0.09f), fur, Vector3.zero);
        b.Part(rig.Head, Ell("dogbrow", 0.07f, 0.02f, 0.03f), dark, new Vector3(0, 0.035f, 0.055f), new Vector3(15, 0, 0));
        // 주둥이 (앞으로 긴 관), 코, 윗니
        var snout = ProcMesh.Tube("dogsnout", 2, 12, 0.04f, 0.055f, 0.052f, 0f, 0.12f, 0.042f, 0.04f, 0.012f, 0.2f, 0.032f, 0.03f, 0.018f, 0.225f, 0.02f, 0.02f, 0.02f);
        b.Part(rig.Head, snout, light, new Vector3(0, -0.005f, 0), new Vector3(90, 0, 0));
        b.Part(rig.Head, Ell("dognose", 0.018f, 0.014f, 0.015f), nose, new Vector3(0, -0.02f, 0.225f));
        for (int s = -1; s <= 1; s += 2)
        {
            b.Part(rig.Head, ProcMesh.Cone("fang", 0.006f, 0.028f), tooth, new Vector3(s * 0.024f, -0.04f, 0.17f), new Vector3(180, 0, 0), null, false);
            b.Part(rig.Head, ProcMesh.Box("gum", 0.01f, 0.012f, 0.12f), gum, new Vector3(s * 0.026f, -0.035f, 0.12f), null, null, false);
            for (int i = 0; i < 3; i++)
                b.Part(rig.Head, ProcMesh.Cone("dogtooth", 0.004f, 0.014f), tooth, new Vector3(s * 0.027f, -0.038f, 0.08f + i * 0.025f), new Vector3(180, 0, 0), null, false);
            // 노란 눈 (발광)과 쫑긋 선 귀
            b.Part(rig.Head, Ell("dogeye", 0.013f, 0.008f, 0.008f), ProcTex.Glow(Hex("C9A642"), new Color(1.5f, 1.1f, 0.25f)), new Vector3(s * 0.042f, 0.022f, 0.075f), new Vector3(0, s * 20, s * -10), null, false);
            b.Part(rig.Head, ProcMesh.Cone("dogear", 0.032f, 0.095f), fur, new Vector3(s * 0.045f, 0.05f, -0.02f), new Vector3(-12, 0, s * -18), new Vector3(1, 1, 0.45f));
            b.Part(rig.Head, ProcMesh.Cone("dogearin", 0.02f, 0.07f), ProcTex.Plain(Hex("6a4a40"), 0.3f), new Vector3(s * 0.045f, 0.055f, -0.012f), new Vector3(-12, 0, s * -18), new Vector3(1, 1, 0.3f), false);
        }
        // 아래턱 (물 때 벌린다). 아랫니
        rig.Jaw = b.Joint("Jaw", rig.Head, new Vector3(0, -0.035f, 0.03f), new Vector3(10, 0, 0));
        b.Part(rig.Jaw, ProcMesh.Tube("dogjaw", 2, 10, 0.0f, 0.042f, 0.022f, 0.012f, 0.1f, 0.032f, 0.016f, 0.016f, 0.18f, 0.022f, 0.012f, 0.012f), light, Vector3.zero, new Vector3(90, 0, 0));
        b.Part(rig.Jaw, ProcMesh.Box("tongue", 0.03f, 0.008f, 0.12f), ProcTex.Plain(Hex("8a3a3a"), 0.7f), new Vector3(0, 0.012f, 0.08f), null, null, false);
        for (int s = -1; s <= 1; s += 2)
            b.Part(rig.Jaw, ProcMesh.Cone("fang", 0.005f, 0.022f), tooth, new Vector3(s * 0.02f, 0.012f, 0.15f), Vector3.zero, null, false);

        // 다리: 앞다리는 곧고, 뒷다리는 무릎이 앞·발목이 뒤로 꺾인 개 다리
        rig.Legs = new Transform[4];
        rig.Knees = new Transform[4];
        for (int i = 0; i < 4; i++)
        {
            bool front = i < 2;
            float s = i % 2 == 0 ? -1 : 1;
            var leg = b.Joint("Leg" + i, rig.Model, front ? new Vector3(s * 0.075f, 0.56f, 0.24f) : new Vector3(s * 0.082f, 0.58f, -0.36f));
            Transform knee;
            if (front)
            {
                b.Part(leg, ProcMesh.Tube("dogupperfront2", 2, 12, 0.06f, 0.04f, 0.05f, 0f, 0.02f, 0.058f, 0.075f, 0.005f, -0.08f, 0.048f, 0.062f, 0.012f, -0.18f, 0.032f, 0.04f, 0.006f, -0.25f, 0.026f, 0.03f, 0f), fur, Vector3.zero);
                knee = b.Joint("Knee" + i, leg, new Vector3(0, -0.23f, 0));
                b.Part(knee, ProcMesh.Tube("doglowerfront", 2, 10, 0.01f, 0.027f, 0.031f, 0f, -0.18f, 0.02f, 0.023f, 0f, -0.29f, 0.019f, 0.021f, 0.004f), fur, Vector3.zero);
                b.Part(knee, Ell("dogpaw", 0.03f, 0.02f, 0.04f), dark, new Vector3(0, -0.305f, 0.015f));
            }
            else
            {
                b.Part(leg, ProcMesh.Tube("dogthigh", 2, 12, 0.05f, 0.06f, 0.085f, 0f, -0.06f, 0.055f, 0.075f, 0.01f, -0.25f, 0.03f, 0.036f, 0f), fur, Vector3.zero, new Vector3(-22, 0, 0));
                knee = b.Joint("Knee" + i, leg, new Vector3(0, -0.23f, 0.09f));
                b.Part(knee, ProcMesh.Tube("doghock", 2, 10, 0.01f, 0.028f, 0.03f, 0f, -0.16f, 0.022f, 0.024f, 0f, -0.33f, 0.019f, 0.021f, 0f), fur, Vector3.zero, new Vector3(24, 0, 0));
                b.Part(knee, Ell("dogpaw", 0.03f, 0.02f, 0.042f), dark, new Vector3(0, -0.31f, -0.11f));
            }
            rig.Legs[i] = leg;
            rig.Knees[i] = knee;
        }

        // 꼬리: 엉덩이에서 뒤로 뻗었다가 아래로 처진다
        rig.Tail = b.Joint("Tail", rig.Model, new Vector3(0, 0.6f, -0.5f), new Vector3(-118, 0, 0));
        b.Part(rig.Tail, ProcMesh.Tube("dogtail", 2, 10, 0f, 0.03f, 0.032f, 0f, 0.15f, 0.026f, 0.03f, 0.01f, 0.32f, 0.012f, 0.014f, 0.02f), dark, Vector3.zero);
    }
}
