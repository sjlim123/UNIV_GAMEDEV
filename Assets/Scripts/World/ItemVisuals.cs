using UnityEngine;

// 아이템마다 다른 모양을 기본 도형으로 만든다 (바닥 아이템, 손에 든 물건, 아이콘이 함께 쓴다)
public static class ItemVisuals
{
    public static void Build(string itemId, Transform t)
    {
        switch (itemId)
        {
            case "water":
                RuntimeVisuals.Part(PrimitiveType.Cylinder, t, new Vector3(0, 0.15f, 0), new Vector3(0.09f, 0.15f, 0.09f), new Color(0.35f, 0.5f, 0.65f), 0.8f);
                RuntimeVisuals.Part(PrimitiveType.Cylinder, t, new Vector3(0, 0.32f, 0), new Vector3(0.035f, 0.025f, 0.035f), new Color(0.2f, 0.3f, 0.7f));
                break;
            case "canned_food":
                RuntimeVisuals.Part(PrimitiveType.Cylinder, t, new Vector3(0, 0.06f, 0), new Vector3(0.09f, 0.06f, 0.09f), new Color(0.55f, 0.52f, 0.48f), 0.7f);
                RuntimeVisuals.Part(PrimitiveType.Cylinder, t, new Vector3(0, 0.06f, 0), new Vector3(0.093f, 0.035f, 0.093f), new Color(0.6f, 0.3f, 0.2f), 0.6f);
                break;
            case "bandage":
                RuntimeVisuals.Part(PrimitiveType.Cylinder, t, new Vector3(0, 0.03f, 0), new Vector3(0.1f, 0.03f, 0.1f), new Color(0.85f, 0.83f, 0.78f));
                break;
            case "medkit":
                RuntimeVisuals.Part(PrimitiveType.Cube, t, new Vector3(0, 0.08f, 0), new Vector3(0.32f, 0.16f, 0.22f), new Color(0.8f, 0.8f, 0.76f), 0.4f);
                RuntimeVisuals.Part(PrimitiveType.Cube, t, new Vector3(0, 0.165f, 0), new Vector3(0.18f, 0.01f, 0.05f), new Color(0.7f, 0.1f, 0.1f));
                RuntimeVisuals.Part(PrimitiveType.Cube, t, new Vector3(0, 0.165f, 0), new Vector3(0.05f, 0.01f, 0.14f), new Color(0.7f, 0.1f, 0.1f));
                break;
            case "cloth":
                RuntimeVisuals.Part(PrimitiveType.Cube, t, new Vector3(0, 0.025f, 0), new Vector3(0.4f, 0.05f, 0.3f), new Color(0.55f, 0.48f, 0.38f), 0.05f, new Vector3(0, 0, 3));
                break;
            case "wood":
                RuntimeVisuals.Part(PrimitiveType.Cube, t, new Vector3(0, 0.04f, 0), new Vector3(0.9f, 0.08f, 0.15f), new Color(0.42f, 0.3f, 0.18f), 0.1f);
                RuntimeVisuals.Part(PrimitiveType.Cube, t, new Vector3(0.05f, 0.12f, 0.03f), new Vector3(0.8f, 0.08f, 0.12f), new Color(0.38f, 0.27f, 0.16f), 0.1f, new Vector3(0, 15, 0));
                break;
            case "metal":
                // 고철: 꺾인 철판 브래킷 + 녹슨 파이프 + 너트 (SurvivalInventory 아이콘과 같은 모양)
                var steel = new Color(0.6f, 0.6f, 0.63f);
                var rust = new Color(0.55f, 0.28f, 0.12f);
                RuntimeVisuals.Part(PrimitiveType.Cube, t, new Vector3(-0.05f, 0.0125f, 0.05f), new Vector3(0.3f, 0.025f, 0.09f), steel, 0.45f);
                RuntimeVisuals.Part(PrimitiveType.Cube, t, new Vector3(0.19f, 0.075f, 0.05f), new Vector3(0.22f, 0.025f, 0.09f), steel, 0.45f, new Vector3(0, 0, 35));
                RuntimeVisuals.Part(PrimitiveType.Cube, t, new Vector3(-0.11f, 0.014f, 0.04f), new Vector3(0.05f, 0.026f, 0.04f), rust, 0.1f);
                RuntimeVisuals.Part(PrimitiveType.Cube, t, new Vector3(0.17f, 0.064f, 0.06f), new Vector3(0.04f, 0.026f, 0.035f), rust, 0.1f, new Vector3(0, 0, 35));
                RuntimeVisuals.Part(PrimitiveType.Cylinder, t, new Vector3(0, 0.04f, -0.09f), new Vector3(0.08f, 0.15f, 0.08f), new Color(0.5f, 0.5f, 0.53f), 0.5f, new Vector3(0, 0, 90));
                RuntimeVisuals.Part(PrimitiveType.Cylinder, t, new Vector3(0.06f, 0.04f, -0.09f), new Vector3(0.083f, 0.03f, 0.083f), rust, 0.1f, new Vector3(0, 0, 90));
                RuntimeVisuals.Part(PrimitiveType.Cylinder, t, new Vector3(0.21f, 0.012f, -0.12f), new Vector3(0.05f, 0.012f, 0.05f), new Color(0.3f, 0.3f, 0.32f), 0.5f);
                break;
            case "wire":
                RuntimeVisuals.Part(PrimitiveType.Cylinder, t, new Vector3(0, 0.04f, 0), new Vector3(0.22f, 0.04f, 0.22f), new Color(0.75f, 0.4f, 0.2f), 0.7f);
                break;
            case "battery":
                RuntimeVisuals.Part(PrimitiveType.Cylinder, t, new Vector3(0, 0.035f, 0), new Vector3(0.05f, 0.07f, 0.05f), new Color(0.15f, 0.15f, 0.15f), 0.6f, new Vector3(0, 0, 90));
                RuntimeVisuals.Part(PrimitiveType.Cylinder, t, new Vector3(0.07f, 0.035f, 0), new Vector3(0.05f, 0.012f, 0.05f), new Color(0.8f, 0.6f, 0.1f), 0.6f, new Vector3(0, 0, 90));
                break;
            case "fuel":
                RuntimeVisuals.Part(PrimitiveType.Cube, t, new Vector3(0, 0.22f, 0), new Vector3(0.18f, 0.44f, 0.34f), new Color(0.55f, 0.1f, 0.08f), 0.5f);
                RuntimeVisuals.Part(PrimitiveType.Cylinder, t, new Vector3(0, 0.47f, 0.1f), new Vector3(0.05f, 0.04f, 0.05f), new Color(0.2f, 0.2f, 0.2f));
                break;
            case "flashlight":
                RuntimeVisuals.Part(PrimitiveType.Cylinder, t, new Vector3(0, 0.03f, 0), new Vector3(0.05f, 0.12f, 0.05f), new Color(0.1f, 0.1f, 0.1f), 0.6f, new Vector3(90, 0, 0));
                RuntimeVisuals.Part(PrimitiveType.Cylinder, t, new Vector3(0, 0.03f, 0.13f), new Vector3(0.07f, 0.03f, 0.07f), new Color(0.15f, 0.15f, 0.15f), 0.6f, new Vector3(90, 0, 0));
                break;
            case "club":
                RuntimeVisuals.Part(PrimitiveType.Cylinder, t, new Vector3(0, 0.03f, 0), new Vector3(0.05f, 0.35f, 0.05f), new Color(0.35f, 0.25f, 0.15f), 0.2f, new Vector3(90, 0, 0));
                RuntimeVisuals.Part(PrimitiveType.Cube, t, new Vector3(0, 0.05f, 0.4f), new Vector3(0.11f, 0.11f, 0.16f), new Color(0.3f, 0.3f, 0.32f), 0.5f);
                break;
            case "backpack":
                RuntimeVisuals.Part(PrimitiveType.Cube, t, new Vector3(0, 0.2f, 0), new Vector3(0.35f, 0.4f, 0.2f), new Color(0.28f, 0.3f, 0.2f), 0.1f);
                RuntimeVisuals.Part(PrimitiveType.Cube, t, new Vector3(0, 0.12f, 0.11f), new Vector3(0.25f, 0.15f, 0.06f), new Color(0.24f, 0.26f, 0.17f), 0.1f);
                break;
            case "raw_meat":
                RuntimeVisuals.Part(PrimitiveType.Sphere, t, new Vector3(0, 0.05f, 0), new Vector3(0.25f, 0.1f, 0.18f), new Color(0.45f, 0.08f, 0.07f), 0.7f);
                break;
            default:
                RuntimeVisuals.Part(PrimitiveType.Cube, t, new Vector3(0, 0.1f, 0), Vector3.one * 0.2f, Color.gray);
                break;
        }
    }
}
