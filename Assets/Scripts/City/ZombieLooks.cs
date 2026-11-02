using System.Collections.Generic;
using UnityEngine;

// 좀비 종류별 겉모습: CharacterModels로 몸을 짓고(디자인 시트), 원거리 좀비는 새총, 약탈자는 못 박힌 야구방망이를 쥐여 준다.
// 새총 고무줄(Slingshot)과 새총 돌 재질도 여기 있다
public static class ZombieLooks
{
    static readonly Dictionary<PrimitiveType, Mesh> meshes = new Dictionary<PrimitiveType, Mesh>();

    // 원거리 좀비가 새총으로 쏘는 돌멩이 재질
    public static Material Stone => RuntimeVisuals.Mat(new Color(0.42f, 0.41f, 0.38f), 0.15f);

    // 몸을 새로 짓는다. 원거리 좀비면 slingshot에 새총, 약탈자면 weapon에 야구방망이를 돌려준다
    public static CharacterRig Apply(Zombie z, ZombieKind kind, out Slingshot slingshot, out Transform weapon)
    {
        slingshot = null;
        weapon = null;
        FigureKind figure;
        int variant = 0;
        switch (kind)
        {
            case ZombieKind.Leather: figure = FigureKind.Leather; break;
            case ZombieKind.Ranged: figure = FigureKind.Slingshot; break;
            case ZombieKind.Heavy: figure = FigureKind.Giant; break;
            case ZombieKind.Raider: figure = FigureKind.Raider; break;
            default: figure = FigureKind.Zombie; variant = Random.Range(0, 4); break;
        }
        var rig = CharacterModels.Build(z.transform, figure, variant);
        if (kind == ZombieKind.Ranged) slingshot = BuildSlingshot(rig.ForeL);
        if (kind == ZombieKind.Raider) weapon = BuildBat(rig.ForeR);
        return rig;
    }

    // 왼손에 쥔 새총: Y자 나무 손잡이 + 고무줄 두 가닥 + 가죽 주머니
    static Slingshot BuildSlingshot(Transform fore)
    {
        if (fore == null) return null;
        // 주먹을 관통해 쥔다 (아래팔 기준 손 가운데). 팔을 앞으로 뻗으면 갈래가 위를, 주머니가 몸 쪽을 향한다
        var fork = new GameObject("Slingshot").transform;
        fork.SetParent(fore, false);
        fork.localPosition = new Vector3(0, -(CharacterModels.HandReach - 0.30f), 0);
        fork.localRotation = Quaternion.Euler(90, 0, 0);

        var wood = RuntimeVisuals.Mat(new Color(0.43f, 0.29f, 0.16f), 0.3f);
        var rubber = RuntimeVisuals.Mat(new Color(0.58f, 0.32f, 0.12f), 0.55f);
        var leather = RuntimeVisuals.Mat(new Color(0.22f, 0.13f, 0.07f), 0.25f);
        Part(PrimitiveType.Cylinder, "Grip", fork, new Vector3(0, 0.015f, 0), new Vector3(0.032f, 0.075f, 0.032f), wood);
        Part(PrimitiveType.Cylinder, "ProngL", fork, new Vector3(-0.03f, 0.155f, 0), new Vector3(0.028f, 0.072f, 0.028f), wood, new Vector3(0, 0, 24.8f));
        Part(PrimitiveType.Cylinder, "ProngR", fork, new Vector3(0.03f, 0.155f, 0), new Vector3(0.028f, 0.072f, 0.028f), wood, new Vector3(0, 0, -24.8f));
        // 갈래 끝에 감은 고무 매듭
        Part(PrimitiveType.Sphere, "WrapL", fork, Slingshot.TipL, Vector3.one * 0.032f, rubber, null, false);
        Part(PrimitiveType.Sphere, "WrapR", fork, Slingshot.TipR, Vector3.one * 0.032f, rubber, null, false);

        // 고무줄 (길이·방향은 Slingshot.Pose가 매 프레임 정한다)
        var bandL = Part(PrimitiveType.Cube, "BandL", fork, Vector3.zero, Vector3.one * 0.01f, rubber, null, false);
        var bandR = Part(PrimitiveType.Cube, "BandR", fork, Vector3.zero, Vector3.one * 0.01f, rubber, null, false);
        // 가죽 주머니와 그 안의 돌 (주머니 +z가 갈래 쪽)
        var pouch = new GameObject("Pouch").transform;
        pouch.SetParent(fork, false);
        Part(PrimitiveType.Cube, "PouchLeather", pouch, Vector3.zero, new Vector3(0.05f, 0.034f, 0.016f), leather, null, false);
        var stone = Part(PrimitiveType.Sphere, "PouchStone", pouch, new Vector3(0, 0, 0.018f), new Vector3(0.05f, 0.042f, 0.046f), Stone, null, false);

        var sling = new Slingshot(fork, pouch, bandL, bandR, stone);
        sling.Relax();
        return sling;
    }

    // 원거리 좀비의 새총. 고무줄 두 가닥을 Y자 끝과 가죽 주머니 사이에 매 프레임 늘여 놓는다.
    // Zombie가 Pose로 주머니를 오른손 쪽으로 당기면 고무줄이 늘어나고, 놓으면 튕겨 돌아간다
    public class Slingshot
    {
        // Y자 기준 좌표 (y = 갈래 쪽, z = 겨누는 쪽)
        public static readonly Vector3 TipL = new Vector3(-0.06f, 0.22f, 0f), TipR = new Vector3(0.06f, 0.22f, 0f);
        static readonly Vector3 RestPouch = new Vector3(0f, 0.19f, -0.04f);
        const float BandWidth = 0.013f;

        public readonly Transform Fork;
        readonly Transform pouch, bandL, bandR, stone;

        public Slingshot(Transform fork, Transform pouch, Transform bandL, Transform bandR, Transform stone)
        {
            Fork = fork;
            this.pouch = pouch;
            this.bandL = bandL;
            this.bandR = bandR;
            this.stone = stone;
        }

        // 돌이 날아가기 시작하는 곳 (두 갈래 끝 사이)
        public Vector3 Muzzle => Fork.TransformPoint((TipL + TipR) * 0.5f);

        // 당기지 않았을 때 주머니가 있는 곳 (오른손이 주머니를 집으러 가는 곳)
        public Vector3 Rest => Fork.TransformPoint(RestPouch);

        // 고무줄이 느슨하게 Y자에 붙어 있는 상태 (돌 없음)
        public void Relax() => Pose(Vector3.zero, 0f, false);

        // draw 0 = 주머니가 Y자 바로 뒤, 1 = 주머니가 pullTo(오른손)까지 당겨짐. loaded면 주머니에 돌이 보인다.
        // 음수면 놓은 고무줄이 튕겨 Y자 앞으로 넘어간 상태 (-0.5까지)
        public void Pose(Vector3 pullTo, float draw, bool loaded)
        {
            if (Fork == null || pouch == null) return;
            Vector3 tipL = Fork.TransformPoint(TipL), tipR = Fork.TransformPoint(TipR);
            Vector3 p = Vector3.LerpUnclamped(Fork.TransformPoint(RestPouch), pullTo, Mathf.Clamp(draw, -0.5f, 1f));
            pouch.position = p;
            Vector3 face = (tipL + tipR) * 0.5f - p;
            if (face.sqrMagnitude > 1e-6f) pouch.rotation = Quaternion.LookRotation(face, Fork.up);
            Stretch(bandL, tipL, p);
            Stretch(bandR, tipR, p);
            if (stone != null && stone.gameObject.activeSelf != loaded) stone.gameObject.SetActive(loaded);
        }

        void Stretch(Transform band, Vector3 from, Vector3 to)
        {
            if (band == null) return;
            Vector3 d = to - from;
            float len = d.magnitude;
            band.position = (from + to) * 0.5f;
            if (len > 1e-4f) band.rotation = Quaternion.LookRotation(d, Fork.up);
            float s = Mathf.Max(1e-4f, Fork.lossyScale.x); // 좀비 크기 (부모 배율은 모두 균일)
            float local = len / s;
            float w = BandWidth * Mathf.Lerp(1f, 0.6f, Mathf.Clamp01(local / 0.7f)); // 늘어날수록 가늘어진다
            band.localScale = new Vector3(w, w, Mathf.Max(0.001f, local));
        }
    }

    // 약탈자의 못 박힌 야구방망이 (#B58E58, 검은 손잡이 테이프). 손잡이 끝을 쥐고, 팔을 뒤로 젖히면 머리 뒤로 넘어간다
    static Transform BuildBat(Transform fore)
    {
        if (fore == null) return null;
        var bat = new GameObject("Bat").transform;
        bat.SetParent(fore, false);
        bat.localPosition = new Vector3(0, -(CharacterModels.HandReach - 0.30f) - 0.01f, 0.03f);
        bat.localRotation = Quaternion.Euler(150, 0, 0);
        var wood = ProcTex.Get(ProcTex.Kind.Wood, new Color(0.71f, 0.56f, 0.35f), 91);
        var tape = ProcTex.Plain(new Color(0.08f, 0.08f, 0.08f), 0.3f);
        var metal = ProcTex.Plain(new Color(0.55f, 0.55f, 0.53f), 0.6f, 0.7f);
        Part(PrimitiveType.Sphere, "Knob", bat, new Vector3(0, -0.06f, 0), new Vector3(0.05f, 0.03f, 0.05f), tape, null, false);
        Part(PrimitiveType.Cylinder, "Grip", bat, new Vector3(0, 0.04f, 0), new Vector3(0.038f, 0.1f, 0.038f), tape);
        Part(PrimitiveType.Cylinder, "Handle", bat, new Vector3(0, 0.22f, 0), new Vector3(0.035f, 0.14f, 0.035f), wood);
        Part(PrimitiveType.Cylinder, "Taper", bat, new Vector3(0, 0.4f, 0), new Vector3(0.05f, 0.08f, 0.05f), wood);
        Part(PrimitiveType.Cylinder, "Barrel", bat, new Vector3(0, 0.62f, 0), new Vector3(0.068f, 0.18f, 0.068f), wood);
        Part(PrimitiveType.Sphere, "Cap", bat, new Vector3(0, 0.8f, 0), new Vector3(0.068f, 0.02f, 0.068f), wood, null, false);
        // 사방으로 박은 못
        for (int i = 0; i < 6; i++)
        {
            float a = i * 60f + (i % 2) * 25f;
            var dir = Quaternion.Euler(0, a, 0) * Vector3.right;
            Part(PrimitiveType.Cylinder, "Nail", bat, new Vector3(0, 0.52f + i * 0.045f, 0) + dir * 0.045f, new Vector3(0.006f, 0.03f, 0.006f), metal, new Vector3(0, a, 90), false);
        }
        return bat;
    }

    // ---------- 도우미 ----------

    // 콜라이더 없는 부품 (CreatePrimitive는 콜라이더가 프레임 끝까지 남아서 메시만 빌려 쓴다)
    static Transform Part(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, Vector3? euler = null, bool shadows = true)
    {
        if (parent == null) return null;
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(euler ?? Vector3.zero);
        go.transform.localScale = scale;
        go.AddComponent<MeshFilter>().sharedMesh = MeshOf(type);
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
        return go.transform;
    }

    static Mesh MeshOf(PrimitiveType type)
    {
        if (meshes.TryGetValue(type, out var m) && m != null) return m;
        var tmp = GameObject.CreatePrimitive(type);
        tmp.SetActive(false);
        m = tmp.GetComponent<MeshFilter>().sharedMesh;
        Object.Destroy(tmp);
        meshes[type] = m;
        return m;
    }
}
