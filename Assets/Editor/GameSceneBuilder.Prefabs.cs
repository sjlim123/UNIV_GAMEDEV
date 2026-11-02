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
        spot.intensity = 48f;
        spot.shadows = LightShadows.Soft;
        spot.shadowStrength = 0.95f;
        // 손을 따라 흔들리는 빛이라 그림자 자국(얼룩 줄무늬)이 지지직 떨려 보이지 않게
        // 그림자 해상도를 높이고 바이어스를 조금 키운다
        var spotData = spot.GetUniversalAdditionalLightData();
        spotData.usePipelineSettings = false;
        var spotSo = new SerializedObject(spotData); // 해상도 단계는 스크립트에서 읽기 전용이라 직렬화 값으로 바꾼다
        SetProp(spotSo, "m_AdditionalLightsShadowResolutionTier", UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierHigh);
        spotSo.ApplyModifiedPropertiesWithoutUndo();
        spot.shadowBias = 0.08f;
        spot.shadowNormalBias = 0.6f;

        root.AddComponent<PlayerController>().cameraRoot = camRoot;
        root.AddComponent<PlayerInteractor>().cam = cam;
        var fl = root.AddComponent<PlayerFlashlight>();
        fl.spot = spot;
        fl.baseIntensity = 48f;
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
        // 몸은 실행할 때 종류에 맞게 짓는다 (CharacterModels, 디자인 시트). 발광 눈 재질 원본만 넘겨 둔다
        z.eyeTemplate = M["zEye"];

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
        // 몸은 실행할 때 짓는다 (CharacterModels.Dog, 디자인 시트). 발광 눈 재질 원본만 넘겨 둔다
        a.eyeTemplate = M["zEye"];

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
