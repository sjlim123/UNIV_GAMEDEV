using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// GameSceneBuilder의 플레이어·좀비·들개 프리팹 부분
public static partial class GameSceneBuilder
{
    // ================= 프리팹 =================

    static GameObject BuildPlayerPrefab()
    {
        var root = new GameObject("Player");
        root.tag = "Player";
        var cc = root.AddComponent<CharacterController>();
        cc.height = 1.8f;
        cc.radius = 0.35f;
        cc.center = new Vector3(0, 0.9f, 0);
        cc.stepOffset = 0.4f;
        cc.slopeLimit = 50;
        cc.skinWidth = 0.04f;

        var camRoot = Group("CameraRoot", root.transform, new Vector3(0, 1.65f, 0));
        var cam = camRoot.gameObject.AddComponent<Camera>();
        camRoot.gameObject.tag = "MainCamera";
        cam.nearClipPlane = 0.03f;
        cam.farClipPlane = 100f; // 안개 너머는 그리지 않는다 (최적화)
        cam.fieldOfView = 70f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        var camData = cam.GetUniversalAdditionalCameraData();
        camData.renderPostProcessing = true;
        camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        camRoot.gameObject.AddComponent<AudioListener>();

        var flash = Group("Flashlight", camRoot, new Vector3(0.22f, -0.2f, 0.1f));
        flash.localRotation = Quaternion.Euler(1.5f, -2f, 0);
        var spot = flash.gameObject.AddComponent<Light>();
        spot.type = LightType.Spot;
        spot.range = 32f;
        spot.spotAngle = 60f;
        spot.innerSpotAngle = 22f;
        spot.color = new Color(1f, 0.93f, 0.8f);
        spot.intensity = 40f;
        spot.shadows = LightShadows.Soft;
        spot.shadowStrength = 0.95f;

        root.AddComponent<PlayerController>().cameraRoot = camRoot;
        root.AddComponent<PlayerInteractor>().cam = cam;
        var fl = root.AddComponent<PlayerFlashlight>();
        fl.spot = spot;
        fl.baseIntensity = 40f;
        var combat = root.AddComponent<PlayerCombat>();
        combat.cam = cam;
        root.AddComponent<PlayerHotbar>();
        root.AddComponent<PlayerHands>();
        root.AddComponent<NavMeshModifier>().ignoreFromBuild = true;

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabDir + "/Player.prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }

    static GameObject BuildZombiePrefab()
    {
        var root = new GameObject("Zombie");
        var agent = root.AddComponent<NavMeshAgent>();
        agent.radius = 0.35f;
        agent.height = 1.8f;
        agent.speed = 0.7f;
        agent.angularSpeed = 240f;
        agent.acceleration = 10f;
        agent.stoppingDistance = 1.1f;
        var col = root.AddComponent<CapsuleCollider>();
        col.height = 1.8f;
        col.radius = 0.33f;
        col.center = new Vector3(0, 0.9f, 0);
        var z = root.AddComponent<Zombie>();

        var t = root.transform;
        z.legL = Group("LegL", t, new Vector3(-0.12f, 0.92f, 0));
        Prim(PrimitiveType.Cube, "Leg", z.legL, new Vector3(0, -0.46f, 0), new Vector3(0.16f, 0.92f, 0.18f), M["zPants"]);
        z.legR = Group("LegR", t, new Vector3(0.12f, 0.92f, 0));
        Prim(PrimitiveType.Cube, "Leg", z.legR, new Vector3(0, -0.46f, 0), new Vector3(0.16f, 0.92f, 0.18f), M["zPants"]);

        z.body = Group("Body", t, new Vector3(0, 0.92f, 0));
        Prim(PrimitiveType.Cube, "Torso", z.body, new Vector3(0, 0.33f, 0), new Vector3(0.46f, 0.64f, 0.26f), M["zCloth"]);
        Prim(PrimitiveType.Cube, "Rag", z.body, new Vector3(0.08f, 0.02f, 0.12f), new Vector3(0.22f, 0.18f, 0.03f), M["zCloth"], new Vector3(0, 0, 18));
        Prim(PrimitiveType.Cube, "Neck", z.body, new Vector3(0, 0.68f, 0), new Vector3(0.12f, 0.1f, 0.12f), M["zSkin"]);

        z.head = Group("Head", z.body, new Vector3(0, 0.72f, 0));
        Prim(PrimitiveType.Sphere, "Skull", z.head, new Vector3(0, 0.13f, 0.02f), new Vector3(0.23f, 0.28f, 0.25f), M["zSkin"]);
        Prim(PrimitiveType.Cube, "Jaw", z.head, new Vector3(0, 0.0f, 0.07f), new Vector3(0.14f, 0.06f, 0.11f), M["zSkin"], new Vector3(18, 0, 0));
        Prim(PrimitiveType.Sphere, "EyeL", z.head, new Vector3(-0.05f, 0.16f, 0.12f), new Vector3(0.035f, 0.025f, 0.02f), M["zEye"]);
        Prim(PrimitiveType.Sphere, "EyeR", z.head, new Vector3(0.05f, 0.16f, 0.12f), new Vector3(0.035f, 0.025f, 0.02f), M["zEye"]);

        z.armL = Group("ArmL", z.body, new Vector3(-0.3f, 0.6f, 0));
        Prim(PrimitiveType.Cube, "Arm", z.armL, new Vector3(0, -0.3f, 0), new Vector3(0.12f, 0.62f, 0.12f), M["zSkin"]);
        Prim(PrimitiveType.Cube, "Hand", z.armL, new Vector3(0, -0.66f, 0), new Vector3(0.1f, 0.13f, 0.06f), M["zSkin"]);
        z.armR = Group("ArmR", z.body, new Vector3(0.3f, 0.6f, 0));
        Prim(PrimitiveType.Cube, "Arm", z.armR, new Vector3(0, -0.3f, 0), new Vector3(0.12f, 0.62f, 0.12f), M["zSkin"]);
        Prim(PrimitiveType.Cube, "Hand", z.armR, new Vector3(0, -0.66f, 0), new Vector3(0.1f, 0.13f, 0.06f), M["zSkin"]);

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabDir + "/Zombie.prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }

    static GameObject BuildDogPrefab()
    {
        var root = new GameObject("Dog");
        var agent = root.AddComponent<NavMeshAgent>();
        agent.radius = 0.3f;
        agent.height = 0.8f;
        agent.speed = 1.2f;
        agent.angularSpeed = 400f;
        agent.acceleration = 14f;
        var col = root.AddComponent<CapsuleCollider>();
        col.direction = 2;
        col.height = 1.0f;
        col.radius = 0.25f;
        col.center = new Vector3(0, 0.5f, 0);
        var a = root.AddComponent<Animal>();

        var t = root.transform;
        Prim(PrimitiveType.Cube, "Body", t, new Vector3(0, 0.55f, 0), new Vector3(0.3f, 0.3f, 0.8f), M["fur"]);
        Prim(PrimitiveType.Cube, "Head", t, new Vector3(0, 0.72f, 0.5f), new Vector3(0.22f, 0.22f, 0.28f), M["fur"]);
        Prim(PrimitiveType.Cube, "Snout", t, new Vector3(0, 0.66f, 0.7f), new Vector3(0.12f, 0.1f, 0.16f), M["fur"]);
        Prim(PrimitiveType.Cube, "EarL", t, new Vector3(-0.07f, 0.88f, 0.45f), new Vector3(0.05f, 0.1f, 0.04f), M["fur"], new Vector3(0, 0, -12));
        Prim(PrimitiveType.Cube, "EarR", t, new Vector3(0.07f, 0.88f, 0.45f), new Vector3(0.05f, 0.1f, 0.04f), M["fur"], new Vector3(0, 0, 12));
        Prim(PrimitiveType.Cube, "Tail", t, new Vector3(0, 0.66f, -0.5f), new Vector3(0.05f, 0.05f, 0.3f), M["fur"], new Vector3(-30, 0, 0));
        a.legs = new Transform[4];
        var legPos = new[] { new Vector3(-0.1f, 0.42f, 0.3f), new Vector3(0.1f, 0.42f, 0.3f), new Vector3(-0.1f, 0.42f, -0.3f), new Vector3(0.1f, 0.42f, -0.3f) };
        for (int i = 0; i < 4; i++)
        {
            a.legs[i] = Group("Leg" + i, t, legPos[i]);
            Prim(PrimitiveType.Cube, "Leg", a.legs[i], new Vector3(0, -0.21f, 0), new Vector3(0.08f, 0.42f, 0.08f), M["fur"]);
        }

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabDir + "/Dog.prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }

    static GameObject PlacePlayer(GameObject prefab, Vector3 pos, float yaw, Color background)
    {
        var p = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        p.transform.position = pos;
        p.transform.rotation = Quaternion.Euler(0, yaw, 0);
        var cam = p.GetComponentInChildren<Camera>();
        cam.backgroundColor = background;
        return p;
    }
}
