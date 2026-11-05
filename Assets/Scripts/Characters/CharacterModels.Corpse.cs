using UnityEngine;

// 도시 건물 벽에 기대 앉아 죽은 사람 (DeadBody가 짓고 자세를 잡는다).
// 좀비가 아닌 사람 몸: 핏기 없는 잿빛 피부, 피 묻은 옷. 얼굴은 컷씬 주인공과 같은 한 덩어리 매끄러운 머리(Face.cs)를 FaceStyle로 변주한다:
// 감거나 반쯤 뜬 눈, 벌어진 입, 꺼진 볼, 부은 얼굴, 마른 피·멍·베인 상처, 수염. variant마다 옷·머리·얼굴이 다르다
public static partial class CharacterModels
{
    public const int CorpseVariants = 5;

    static void Corpse(Builder b, int variant)
    {
        int v = ((variant % CorpseVariants) + CorpseVariants) % CorpseVariants;
        int seed = 300 + v * 7;
        Material skin, shirt, sleeve, pants, shoes, hair;
        float sleeveTo = -0.6f;
        bool longHair = false, boots = false, baggy = true;
        var face = new FaceStyle();
        switch (v)
        {
            case 1:
                // 갈색 가죽 재킷, 진청 바지, 긴 머리, 부츠. 창백한 피부, 반쯤 뜬 흐린 눈, 푸르스름한 입술, 관자놀이의 멍
                skin = ProcTex.Get(ProcTex.Kind.Skin, Hex("c4b3a6"), seed, 1, 0.4f, Hex("968778"));
                shirt = sleeve = ProcTex.Get(ProcTex.Kind.Leather, Hex("5b4631"), seed, 2, 0.5f, Hex("3d2e20"));
                pants = ProcTex.Get(ProcTex.Kind.Denim, Hex("2f3d55"), seed + 1, 1, 0.5f, Hex("1f2a3c"));
                shoes = ProcTex.Get(ProcTex.Kind.Leather, Hex("2a211b"), seed + 2, 0, 0.5f);
                hair = ProcTex.Get(ProcTex.Kind.Hair, Hex("3a2a1e"), seed + 3);
                longHair = true;
                boots = true;
                baggy = false;
                face.EyeOpenL = face.EyeOpenR = 0.45f;
                face.EyeWhite = Hex("d9d5cf");
                face.Iris = Hex("6d6b66");
                face.LipUpper = Hex("6e5c62");
                face.LipLower = Hex("7a6468");
                face.CheekSunk = 0.25f;
                face.Mark(0.054f, 0.152f, 0.015f, 0.011f, -20f, 1);
                break;
            case 2:
                // 국방색 파카, 회색 바지, 희끗한 머리와 수염. 잿빛 피부, 감은 눈, 턱이 처져 벌어진 입, 이마의 상처와 흘러내린 피
                skin = ProcTex.Get(ProcTex.Kind.Skin, Hex("a99d92"), seed, 1, 0.45f, Hex("7d7267"));
                shirt = sleeve = ProcTex.Get(ProcTex.Kind.Cloth, Hex("4e5a3a"), seed, 2, 0.65f, Hex("353f27"));
                pants = ProcTex.Get(ProcTex.Kind.Cloth, Hex("5a5752"), seed + 1, 1, 0.55f, Hex("3d3b37"));
                shoes = ProcTex.Get(ProcTex.Kind.Leather, Hex("3b2b1f"), seed + 2, 0, 0.5f);
                hair = ProcTex.Get(ProcTex.Kind.Hair, Hex("8f8a82"), seed + 3);
                boots = true;
                face.EyeOpenL = face.EyeOpenR = 0f;
                face.MouthGap = 0.7f;
                face.Stubble = true;
                face.CheekSunk = 0.55f;
                face.LipUpper = Hex("7a5e5a");
                face.LipLower = Hex("856862");
                face.Mark(-0.026f, 0.192f, 0.0035f, 0.017f, 25f, 2);
                face.Mark(-0.038f, 0.162f, 0.0025f, 0.022f, 18f, 0);
                break;
            case 3:
                // 붉은 체크 셔츠(단색으로 어림), 검은 바지. 한쪽 눈만 반쯤 뜬 채, 볼의 베인 상처, 입꼬리에서 턱으로 흐른 피
                skin = ProcTex.Get(ProcTex.Kind.Skin, Hex("b3a090"), seed, 2, 0.45f, Hex("857566"));
                shirt = sleeve = ProcTex.Get(ProcTex.Kind.Cloth, Hex("7a2f2a"), seed, 3, 0.5f, Hex("4c1c19"));
                pants = ProcTex.Get(ProcTex.Kind.Cloth, Hex("24242a"), seed + 1, 1, 0.5f, Hex("151518"));
                shoes = ProcTex.Get(ProcTex.Kind.Leather, Hex("1d1a17"), seed + 2, 0, 0.4f);
                hair = ProcTex.Get(ProcTex.Kind.Hair, Hex("1e1816"), seed + 3);
                sleeveTo = -0.3f;
                face.EyeOpenL = 0f;
                face.EyeOpenR = 0.5f;
                face.EyeWhite = Hex("dcd6cc");
                face.Iris = Hex("4a4a46");
                face.MouthGap = 0.3f;
                face.CheekSunk = 0.3f;
                face.LipUpper = Hex("7d5a56");
                face.LipLower = Hex("8a6560");
                face.Mark(0.05f, 0.112f, 0.003f, 0.021f, 35f, 2);
                face.Mark(0.021f, 0.048f, 0.003f, 0.017f, -12f, 0);
                break;
            case 4:
                // 푸른 작업복 셔츠, 카키 바지, 민머리. 얻어맞아 부은 얼굴: 보랏빛 멍, 부은 눈두덩, 터진 입술
                skin = ProcTex.Get(ProcTex.Kind.Skin, Hex("a89b92"), seed, 1, 0.45f, Hex("6f5f6a"));
                shirt = sleeve = ProcTex.Get(ProcTex.Kind.Cloth, Hex("3d5370"), seed, 2, 0.6f, Hex("27374b"));
                pants = ProcTex.Get(ProcTex.Kind.Cloth, Hex("8a7a58"), seed + 1, 2, 0.6f, Hex("5e5339"));
                shoes = ProcTex.Get(ProcTex.Kind.Leather, Hex("4a3a2a"), seed + 2, 0, 0.5f);
                hair = null;
                face.EyeOpenL = face.EyeOpenR = 0f;
                face.Swollen = 0.8f;
                face.MouthGap = 0.2f;
                face.LipUpper = Hex("6f4e4e");
                face.LipLower = Hex("7e5a58");
                face.Mark(-0.031f, 0.148f, 0.023f, 0.017f, 0f, 1);
                face.Mark(0.042f, 0.062f, 0.014f, 0.01f, 15f, 1);
                face.Mark(0.007f, 0.062f, 0.0025f, 0.0065f, 80f, 2);
                break;
            default:
                // 회색 후드티, 청바지, 짧은 검은 머리. 핏기 없는 얼굴, 감은 눈, 조금 벌어진 입, 코에서 흘러 마른 피
                skin = ProcTex.Get(ProcTex.Kind.Skin, Hex("b9aa9b"), seed, 1, 0.45f, Hex("8a7d70"));
                shirt = sleeve = ProcTex.Get(ProcTex.Kind.Cloth, Hex("5a5c60"), seed, 2, 0.55f, Hex("3b3d40"));
                pants = ProcTex.Get(ProcTex.Kind.Denim, Hex("3a4a66"), seed + 1, 1, 0.45f, Hex("283449"));
                shoes = ProcTex.Get(ProcTex.Kind.Leather, Hex("6a6560"), seed + 2, 0, 0.5f, Hex("4b4744"));
                hair = ProcTex.Get(ProcTex.Kind.Hair, Hex("1e1816"), seed + 3);
                face.EyeOpenL = face.EyeOpenR = 0f;
                face.MouthGap = 0.35f;
                face.CheekSunk = 0.3f;
                face.LipUpper = Hex("7f5d58");
                face.LipLower = Hex("8a6660");
                face.Mark(0.0085f, 0.081f, 0.0025f, 0.013f, 6f, 0);
                break;
        }
        var h = new Human
        {
            Skin = skin,
            Shirt = shirt,
            Sleeve = sleeve,
            SleeveTo = sleeveTo,
            Pants = pants,
            Shoes = shoes,
            Hair = hair,
            Zombie = false,
            LongHair = longHair,
            Boots = boots,
            PantsBaggy = baggy,
            Belt = v != 2,
            Bulk = v == 1 ? 0.94f : v == 2 ? 1.08f : 1f,
            HeadSize = v == 1 ? 0.96f : 1f,
            SmoothFace = true,
            Face = face,
            NeckDrop = 0.02f,
        };
        h.Skin.SetFloat("_BumpScale", 0.3f); // 시체 피부는 거칠게
        Humanoid(b, h);
        var rig = b.Rig;
        // 후드티: 뒤로 늘어진 모자
        if (v == 0)
            b.Part(rig.Body, Tube(b.Key + "hood", 0.44f, 0.11f, 0.08f, -0.06f, 0.5f, 0.125f, 0.095f, -0.07f, 0.56f, 0.1f, 0.085f, -0.08f), shirt, Vector3.zero);
    }
}
