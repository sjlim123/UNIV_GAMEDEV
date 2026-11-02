using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// GameSceneBuilder의 메인 메뉴 배경 씬 부분
public static partial class GameSceneBuilder
{
    // ================= 메인 메뉴 배경 =================

    static void BuildMenuScene(GameObject zombiePrefab)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        BeginMeshes(GenDir + "/MenuMeshes.asset");
        rng = new System.Random(5);
        var root = new GameObject("MenuBackdrop").transform;

        Box("Ground", root, new Vector3(0, -0.1f, 25), new Vector3(70, 0.2f, 100), M["asphalt"], 5f);
        Box("SidewalkL", root, new Vector3(-10, 0.075f, 25), new Vector3(6, 0.15f, 100), M["floor"], 3f);
        Box("SidewalkR", root, new Vector3(10, 0.075f, 25), new Vector3(6, 0.15f, 100), M["floor"], 3f);
        var lines = new List<CombineInstance>();
        var dash = BoxMesh(new Vector3(0.15f, 0.02f, 2.4f), 1f);
        for (float z = -5; z < 70; z += 6)
            lines.Add(new CombineInstance { mesh = dash, transform = Matrix4x4.TRS(new Vector3(0, 0.005f, z), Quaternion.identity, Vector3.one) });
        CombineInto(root, "RoadLines", lines, M["line"]);

        // 길 양옆 건물
        foreach (var side in new[] { -1f, 1f })
            for (float z = -12; z < 75;)
            {
                float d = R(8f, 13f), w = R(8f, 12f), h = R(9f, 24f);
                float x0 = side < 0 ? -13 - w : 13;
                var g = Group("Building", root, new Vector3(0, 0.15f, 0));
                Box("Mass", g, new Vector3(x0 + w / 2, h / 2, z + d / 2), new Vector3(w, h, d), Pick(M["wall"], M["brick"], M["cracked"]), 3f);
                FacadeWindows(g, x0, z, w, d, 3.5f, Mathf.FloorToInt((h - 3.5f) / 3.2f));
                Box("Shutter", g, new Vector3(side < 0 ? x0 + w + 0.06f : x0 - 0.06f, 1.4f, z + d / 2), new Vector3(0.1f, 2.6f, Mathf.Min(5, d - 2)), M["metal"], 1.5f);
                z += d + R(0.5f, 3f);
            }

        WreckedCar(root, new Vector3(-3f, 0, 13), 18, false);
        WreckedCar(root, new Vector3(3.5f, 0, 26), -70, true);
        WreckedCar(root, new Vector3(-2f, 0, 42), 160, false);
        RubblePile(root, new Vector3(4.5f, 0, 9), 10, 1.4f);
        RubblePile(root, new Vector3(-5.5f, 0, 31), 12, 1.6f);
        for (int k = 0; k < 3; k++)
            Box("Barrier", root, new Vector3(5f + k * 2.1f, 0.45f, 18), new Vector3(2f, 0.9f, 0.6f), M["dirty"], 2f, true, new Vector3(0, R(-10, 10), k == 1 ? 80 : 0));
        FireBarrel(root, new Vector3(3.2f, 0, 7.5f));
        LampPost(root, new Vector3(-8.5f, 0.15f, 20), true);
        LampPost(root, new Vector3(8.5f, 0.15f, 38));
        DeadTree(root, new Vector3(9f, 0.15f, 12));
        DeadTree(root, new Vector3(-9f, 0.15f, 34));

        // 안개 속에 서 있는 좀비 실루엣
        PoseZombie(zombiePrefab, new Vector3(1.4f, 0, 30), 190);
        PoseZombie(zombiePrefab, new Vector3(-2.6f, 0, 46), 170);

        var camGo = new GameObject("MenuCamera");
        camGo.tag = "MainCamera";
        camGo.transform.position = new Vector3(-3.2f, 1.7f, -3f);
        camGo.transform.rotation = Quaternion.Euler(3, -6, 0);
        var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 55f;
        cam.farClipPlane = 120f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        var data = cam.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = true;
        data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        camGo.AddComponent<AudioListener>();
        AddAshFall(camGo.transform);
        root.gameObject.AddComponent<MainMenuBackground>().cameraTransform = camGo.transform;

        var moon = new GameObject("Moonlight").AddComponent<Light>();
        moon.type = LightType.Directional;
        moon.color = new Color(0.55f, 0.62f, 0.75f);
        moon.intensity = 0.55f;
        moon.shadows = LightShadows.Soft;
        moon.transform.rotation = Quaternion.Euler(30, -140, 0);
        RenderSettings.sun = moon;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.15f, 0.16f, 0.18f);
        RenderSettings.skybox = null;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.035f;
        RenderSettings.fogColor = new Color(0.11f, 0.12f, 0.12f);
        cam.backgroundColor = RenderSettings.fogColor;
        AddVolume(MakeProfile("MenuProfile", 1.1f, -40f, new Color(0.92f, 1f, 0.95f), 0.5f, 12f));

        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(scene, SceneDir + "/MainMenu.unity");
    }

    static void PoseZombie(GameObject prefab, Vector3 pos, float yaw)
    {
        var z = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        PrefabUtility.UnpackPrefabInstance(z, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        z.transform.position = pos;
        z.transform.rotation = Quaternion.Euler(0, yaw, 0);
        Object.DestroyImmediate(z.GetComponent<Zombie>());
        Object.DestroyImmediate(z.GetComponent<NavMeshAgent>());
        var t = z.transform;
        t.Find("Body").localRotation = Quaternion.Euler(14, 0, 4);
        t.Find("Body/ArmL").localRotation = Quaternion.Euler(-70, 0, -6);
        t.Find("Body/ArmR").localRotation = Quaternion.Euler(-55, 0, 8);
        t.Find("Body/Head").localRotation = Quaternion.Euler(8, 15, 24);
        t.Find("LegL").localRotation = Quaternion.Euler(12, 0, 0);
        t.Find("LegR").localRotation = Quaternion.Euler(-8, 0, 0);
    }
}
