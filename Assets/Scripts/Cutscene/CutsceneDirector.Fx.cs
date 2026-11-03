using System.Collections.Generic;
using UnityEngine;

// 컷씬 효과: 바람에 휘날리는 커튼(정점 애니메이션), 충격파에 터지는 창유리(조각 입자 + 틀에 남는 조각),
// 창으로 밀려드는 먼지와 천장에서 떨어지는 가루, 흔들리는 천장 등, 떨어지는 소품(물리), 퍼져 나가는 충격파 구,
// 거리의 재·불티·땅 연기·하늘을 덮는 연기, 먼 불길의 연기 기둥, 방공호 경광등
public partial class CutsceneDirector
{
    const int CurtainCols = 9, CurtainRows = 22;
    const float CurtainWidth = 0.78f, CurtainHeight = 2.28f;

    ParticleSystem shards, windowDust, ceilingDust, ash, embers, groundSmoke, skySmoke;
    readonly List<ParticleSystem> fireSmokes = new List<ParticleSystem>();
    Mesh curtainMeshL, curtainMeshR;
    Vector3[] curtainBase, curtainWork;
    Transform curtainL, curtainR, shockSphere;
    Material shockMat;
    readonly List<GameObject> remnants = new List<GameObject>();
    float gust, lampTime = -1f, shockRadius = -1f, ceilingDustLeft;
    bool propsLoose;
    Vector3[] propHome;
    Quaternion[] propHomeRot;

    void BuildFx()
    {
        // 커튼: 창 양옆에 걷어 둔 두 폭 (정점을 매 프레임 움직인다)
        if (curtainRod != null)
        {
            var cloth = new Material(ProcTex.Get(ProcTex.Kind.Cloth, new Color(0.9f, 0.86f, 0.76f), 97, 0, 0.08f, new Color(0.7f, 0.65f, 0.55f)));
            cloth.SetFloat("_Cull", 0); // 양면
            curtainMeshL = CurtainMesh();
            curtainMeshR = CurtainMesh();
            curtainBase = curtainMeshL.vertices;
            curtainWork = new Vector3[curtainBase.Length];
            // 봉 가운데 기준: 창(폭 3m) 양 끝을 조금 덮으며 바깥쪽으로 걷혀 있다
            curtainL = CurtainObject("CurtainL", curtainMeshL, cloth, new Vector3(-2.05f, 0, -0.06f));
            curtainR = CurtainObject("CurtainR", curtainMeshR, cloth, new Vector3(1.28f, 0, -0.06f));
        }
        // 유리 조각 입자 (창 안쪽으로 튄다)
        if (windowCenter != null)
        {
            shards = MakeParticles("GlassShards", glassShard, 400);
            shards.transform.position = windowCenter.position;
            shards.transform.rotation = Quaternion.Euler(0, 180, 0); // 상자의 +z(뿜는 방향)가 방 안쪽(-z)을 보게
            var main = shards.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.8f, 3.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3.5f, 9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.16f);
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            main.gravityModifier = 1f;
            main.startColor = new Color(0.85f, 0.93f, 1f, 0.75f);
            var shape = shards.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(2.9f, 1.8f, 0.02f);
            shape.randomDirectionAmount = 0.35f;
            var rot = shards.rotationOverLifetime;
            rot.enabled = true;
            rot.separateAxes = true;
            rot.x = new ParticleSystem.MinMaxCurve(-8f, 8f);
            rot.y = new ParticleSystem.MinMaxCurve(-8f, 8f);
            rot.z = new ParticleSystem.MinMaxCurve(-8f, 8f);
            var col = shards.collision;
            col.enabled = true;
            col.type = ParticleSystemCollisionType.World;
            col.mode = ParticleSystemCollisionMode.Collision3D;
            col.bounce = 0.15f;
            col.dampen = 0.5f;
            col.lifetimeLoss = 0.15f;
            col.quality = ParticleSystemCollisionQuality.Medium;
            var cl = shards.colorOverLifetime;
            cl.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 0.75f), new GradientAlphaKey(0, 1) });
            cl.color = g;
            var r = shards.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Mesh;
            r.SetMeshes(new[] { ShardMesh(0), ShardMesh(1), ShardMesh(2) });
            r.alignment = ParticleSystemRenderSpace.World;

            // 창으로 밀려드는 먼지
            windowDust = MakeParticles("WindowDust", dustParticle, 120);
            windowDust.transform.position = windowCenter.position;
            windowDust.transform.rotation = Quaternion.Euler(0, 180, 0);
            var dm = windowDust.main;
            dm.startLifetime = new ParticleSystem.MinMaxCurve(1.8f, 3.5f);
            dm.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 5.5f);
            dm.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.6f);
            dm.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            dm.startColor = new Color(0.75f, 0.6f, 0.45f, 0.35f);
            var ds = windowDust.shape;
            ds.enabled = true;
            ds.shapeType = ParticleSystemShapeType.Box;
            ds.scale = new Vector3(2.8f, 1.7f, 0.1f);
            ds.randomDirectionAmount = 0.4f;
            SoftFade(windowDust, 0.15f, 0.5f);
            var dsz = windowDust.sizeOverLifetime;
            dsz.enabled = true;
            dsz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.5f, 1, 1.8f));
            var dlim = windowDust.limitVelocityOverLifetime;
            dlim.enabled = true;
            dlim.dampen = 0.35f;
        }
        // 천장에서 떨어지는 회반죽 가루
        if (home != null)
        {
            ceilingDust = MakeParticles("CeilingDust", dustParticle, 600);
            ceilingDust.transform.SetParent(home, false);
            ceilingDust.transform.localPosition = new Vector3(0, 2.62f, -0.1f);
            ceilingDust.transform.localRotation = Quaternion.Euler(90, 0, 0);
            var cm = ceilingDust.main;
            cm.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 2.6f);
            cm.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.3f);
            cm.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.07f);
            cm.gravityModifier = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
            cm.startColor = new Color(0.85f, 0.82f, 0.76f, 0.7f);
            var cs = ceilingDust.shape;
            cs.enabled = true;
            cs.shapeType = ParticleSystemShapeType.Box;
            cs.scale = new Vector3(7.2f, 6.2f, 0.05f);
            var cem = ceilingDust.emission;
            cem.rateOverTime = 220f;
            SoftFade(ceilingDust, 0.05f, 0.6f);
        }
        // 충격파 구: 폭심에서 퍼져 나오는 반투명 막
        if (shockRing != null)
        {
            shockSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
            shockSphere.name = "Shockwave";
            Destroy(shockSphere.GetComponent<Collider>());
            shockSphere.SetParent(transform, false);
            shockMat = new Material(shockRing);
            var sr = shockSphere.GetComponent<MeshRenderer>();
            sr.sharedMaterial = shockMat;
            sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            shockSphere.gameObject.SetActive(false);
        }
        // 거리: 재·불티·땅 연기·하늘 연기·먼 불길
        if (street != null)
        {
            ash = MakeParticles("Ash", ashParticle, 4000);
            ash.transform.rotation = Quaternion.Euler(90, 0, 0);
            var am = ash.main;
            am.startLifetime = new ParticleSystem.MinMaxCurve(10f, 15f);
            am.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
            am.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.065f);
            am.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            am.startColor = new ParticleSystem.MinMaxGradient(new Color(0.55f, 0.52f, 0.5f, 0.95f), new Color(0.3f, 0.28f, 0.27f, 0.95f));
            am.prewarm = true;
            am.loop = true;
            var aem = ash.emission;
            aem.rateOverTime = 230f;
            var ashape = ash.shape;
            ashape.enabled = true;
            ashape.shapeType = ParticleSystemShapeType.Box;
            ashape.scale = new Vector3(36f, 36f, 0.5f);
            var avel = ash.velocityOverLifetime;
            avel.enabled = true;
            avel.space = ParticleSystemSimulationSpace.World;
            avel.x = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
            avel.y = new ParticleSystem.MinMaxCurve(0f, 0f);
            avel.z = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
            var anoise = ash.noise;
            anoise.enabled = true;
            anoise.strength = 0.45f;
            anoise.frequency = 0.35f;
            anoise.scrollSpeed = 0.3f;
            var arot = ash.rotationOverLifetime;
            arot.enabled = true;
            arot.z = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);
            SoftFade(ash, 0.05f, 0.9f);

            embers = MakeParticles("Embers", emberParticle, 300);
            embers.transform.rotation = Quaternion.Euler(-90, 0, 0);
            var em = embers.main;
            em.startLifetime = new ParticleSystem.MinMaxCurve(3f, 6f);
            em.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.2f);
            em.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.045f);
            em.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.5f, 0.15f, 1f), new Color(1f, 0.3f, 0.08f, 1f));
            em.prewarm = true;
            em.loop = true;
            var eem = embers.emission;
            eem.rateOverTime = 14f;
            var eshape = embers.shape;
            eshape.enabled = true;
            eshape.shapeType = ParticleSystemShapeType.Box;
            eshape.scale = new Vector3(30f, 30f, 0.5f);
            var evel = embers.velocityOverLifetime;
            evel.enabled = true;
            evel.space = ParticleSystemSimulationSpace.World;
            evel.x = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
            evel.y = new ParticleSystem.MinMaxCurve(0.1f, 0.4f);
            evel.z = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);
            var enoise = embers.noise;
            enoise.enabled = true;
            enoise.strength = 0.8f;
            enoise.frequency = 0.5f;
            SoftFade(embers, 0.1f, 0.7f);

            groundSmoke = MakeParticles("GroundSmoke", smokeParticle, 160);
            var gm = groundSmoke.main;
            gm.startLifetime = new ParticleSystem.MinMaxCurve(12f, 20f);
            gm.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
            gm.startSize = new ParticleSystem.MinMaxCurve(5f, 9f);
            gm.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            // 밝은 먼지 안개 (어두우면 카메라 둘레를 검게 덮어 버린다)
            gm.startColor = new Color(0.62f, 0.5f, 0.42f, 0.08f);
            gm.prewarm = true;
            gm.loop = true;
            var gem = groundSmoke.emission;
            gem.rateOverTime = 3f;
            var gshape = groundSmoke.shape;
            gshape.enabled = true;
            gshape.shapeType = ParticleSystemShapeType.Box;
            gshape.scale = new Vector3(44f, 2.2f, 44f);
            var gvel = groundSmoke.velocityOverLifetime;
            gvel.enabled = true;
            gvel.space = ParticleSystemSimulationSpace.World;
            gvel.x = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
            gvel.y = new ParticleSystem.MinMaxCurve(0f, 0.05f);
            gvel.z = new ParticleSystem.MinMaxCurve(-0.1f, 0.1f);
            var grot = groundSmoke.rotationOverLifetime;
            grot.enabled = true;
            grot.z = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);
            SoftFade(groundSmoke, 0.2f, 0.75f);
            groundSmoke.GetComponent<ParticleSystemRenderer>().maxParticleSize = 1.5f;

            // 하늘을 덮어 가는 검은 연기 (거리 위 높은 곳)
            skySmoke = MakeParticles("SkySmoke", smokeParticle, 120);
            skySmoke.transform.SetParent(street, false);
            skySmoke.transform.localPosition = new Vector3(0, 75f, 60f);
            var sm = skySmoke.main;
            sm.startLifetime = new ParticleSystem.MinMaxCurve(50f, 70f);
            sm.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
            sm.startSize = new ParticleSystem.MinMaxCurve(70f, 120f);
            sm.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            sm.startColor = new ParticleSystem.MinMaxGradient(new Color(0.12f, 0.08f, 0.07f, 0.6f), new Color(0.2f, 0.12f, 0.09f, 0.5f));
            sm.prewarm = true;
            sm.loop = true;
            var sem = skySmoke.emission;
            sem.rateOverTime = 1.6f;
            var sshape = skySmoke.shape;
            sshape.enabled = true;
            sshape.shapeType = ParticleSystemShapeType.Box;
            sshape.scale = new Vector3(420f, 14f, 380f);
            var svel = skySmoke.velocityOverLifetime;
            svel.enabled = true;
            svel.space = ParticleSystemSimulationSpace.World;
            svel.x = new ParticleSystem.MinMaxCurve(1.5f, 3f);
            svel.y = new ParticleSystem.MinMaxCurve(0f, 0.2f);
            svel.z = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);
            var srot = skySmoke.rotationOverLifetime;
            srot.enabled = true;
            srot.z = new ParticleSystem.MinMaxCurve(-0.03f, 0.03f);
            SoftFade(skySmoke, 0.1f, 0.85f);
            skySmoke.GetComponent<ParticleSystemRenderer>().maxParticleSize = 3f;

            // 먼 불길의 연기 기둥 (불빛 자리마다)
            if (fireLights != null)
                foreach (var fl in fireLights)
                {
                    if (fl == null) continue;
                    var ps = MakeParticles("FireSmoke", smokeParticle, 200);
                    ps.transform.SetParent(fl.transform, false);
                    ps.transform.localPosition = new Vector3(0, 1f, 0);
                    ps.transform.localRotation = Quaternion.Euler(-90, 0, 0);
                    var fm = ps.main;
                    fm.startLifetime = new ParticleSystem.MinMaxCurve(14f, 22f);
                    fm.startSpeed = new ParticleSystem.MinMaxCurve(2f, 3.5f);
                    fm.startSize = new ParticleSystem.MinMaxCurve(4f, 8f);
                    fm.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
                    fm.startColor = new ParticleSystem.MinMaxGradient(new Color(0.16f, 0.11f, 0.09f, 0.6f), new Color(0.3f, 0.18f, 0.12f, 0.5f));
                    fm.prewarm = true;
                    fm.loop = true;
                    var fem = ps.emission;
                    fem.rateOverTime = 6f;
                    var fs = ps.shape;
                    fs.enabled = true;
                    fs.shapeType = ParticleSystemShapeType.Cone;
                    fs.angle = 8f;
                    fs.radius = 1.5f;
                    var fvel = ps.velocityOverLifetime;
                    fvel.enabled = true;
                    fvel.space = ParticleSystemSimulationSpace.World;
                    fvel.x = new ParticleSystem.MinMaxCurve(1f, 2f);
                    fvel.y = new ParticleSystem.MinMaxCurve(0f, 0f);
                    fvel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
                    var fsz = ps.sizeOverLifetime;
                    fsz.enabled = true;
                    fsz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.5f, 1, 2.5f));
                    SoftFade(ps, 0.1f, 0.7f);
                    ps.GetComponent<ParticleSystemRenderer>().maxParticleSize = 2f;
                    fireSmokes.Add(ps);
                }
        }
        if (looseProps != null)
        {
            propHome = new Vector3[looseProps.Length];
            propHomeRot = new Quaternion[looseProps.Length];
            for (int i = 0; i < looseProps.Length; i++)
                if (looseProps[i] != null) { propHome[i] = looseProps[i].position; propHomeRot[i] = looseProps[i].rotation; }
        }
    }

    static ParticleSystem MakeParticles(string name, Material mat, int max)
    {
        var go = new GameObject(name);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = max;
        var em = ps.emission;
        em.rateOverTime = 0f;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return ps;
    }

    // 입자가 서서히 나타났다 사라진다
    static void SoftFade(ParticleSystem ps, float inAt, float outAt)
    {
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, inAt), new GradientAlphaKey(1f, outAt), new GradientAlphaKey(0f, 1f) });
        col.color = g;
    }

    // 유리 조각: 들쭉날쭉한 삼각형 (양면)
    static Mesh ShardMesh(int v)
    {
        var rng = new System.Random(11 + v);
        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        var p = new[] { new Vector3(R(-0.5f, -0.2f), R(-0.5f, -0.1f), 0), new Vector3(R(0.2f, 0.5f), R(-0.4f, 0f), 0), new Vector3(R(-0.2f, 0.3f), R(0.25f, 0.5f), 0) };
        var m = new Mesh { name = "Shard" + v };
        m.vertices = new[] { p[0], p[1], p[2], p[0], p[2], p[1] };
        m.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.forward, Vector3.forward, Vector3.forward };
        m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 1), new Vector2(0, 0), new Vector2(0.5f, 1), new Vector2(1, 0) };
        m.triangles = new[] { 0, 1, 2, 3, 4, 5 };
        m.RecalculateBounds();
        return m;
    }

    // 커튼 천: 위(봉)에서 아래로 늘어진 격자. x 0~폭, y 0~-높이, 주름은 z로
    Mesh CurtainMesh()
    {
        var verts = new Vector3[(CurtainCols + 1) * (CurtainRows + 1)];
        var uvs = new Vector2[verts.Length];
        var tris = new List<int>();
        for (int r = 0; r <= CurtainRows; r++)
            for (int c = 0; c <= CurtainCols; c++)
            {
                float x = c / (float)CurtainCols * CurtainWidth, y = -r / (float)CurtainRows * CurtainHeight;
                verts[r * (CurtainCols + 1) + c] = new Vector3(x, y, 0.035f * Mathf.Sin(x * 25f + y * 0.6f));
                uvs[r * (CurtainCols + 1) + c] = new Vector2(x * 2f, -y * 2f);
            }
        for (int r = 0; r < CurtainRows; r++)
            for (int c = 0; c < CurtainCols; c++)
            {
                int a = r * (CurtainCols + 1) + c, b = a + 1, d = a + CurtainCols + 1, e = d + 1;
                tris.Add(a); tris.Add(b); tris.Add(d);
                tris.Add(b); tris.Add(e); tris.Add(d);
            }
        var m = new Mesh { name = "Curtain" };
        m.MarkDynamic();
        m.vertices = verts;
        m.uv = uvs;
        m.SetTriangles(tris, 0);
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    Transform CurtainObject(string name, Mesh mesh, Material mat, Vector3 localPos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(curtainRod, false);
        go.transform.localPosition = localPos;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        return go.transform;
    }

    // 장면이 시작될 때 효과를 그 장면에 맞게 되돌린다 (2번 장면 전이면 창이 멀쩡하다)
    void ResetFx(int shot)
    {
        gust = 0;
        lampTime = -1f;
        shockRadius = -1f;
        ceilingDustLeft = 0;
        if (windowPanes != null) foreach (var p in windowPanes) if (p != null) p.enabled = true;
        foreach (var r in remnants) if (r != null) Destroy(r);
        remnants.Clear();
        if (shards != null) shards.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (windowDust != null) windowDust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (ceilingDust != null) ceilingDust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (shockSphere != null) shockSphere.gameObject.SetActive(false);
        if (ceilingLamp != null) ceilingLamp.localRotation = Quaternion.identity;
        if (looseProps != null && propsLoose)
            for (int i = 0; i < looseProps.Length; i++)
            {
                var p = looseProps[i];
                if (p == null) continue;
                var rb = p.GetComponent<Rigidbody>();
                if (rb != null) Destroy(rb);
                p.SetPositionAndRotation(propHome[i], propHomeRot[i]);
            }
        propsLoose = false;
        bool streetFx = shot == 3;
        foreach (var ps in new[] { ash, embers, groundSmoke, skySmoke })
        {
            if (ps == null) continue;
            if (streetFx) { if (!ps.isPlaying) ps.Play(); }
            else ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        foreach (var ps in fireSmokes)
        {
            if (streetFx) { if (!ps.isPlaying) ps.Play(); }
            else ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    // 섬광과 함께 폭심에서 충격파 구가 퍼져 나온다
    void StartShockwave(Vector3 groundZero)
    {
        if (shockSphere == null) return;
        shockSphere.position = groundZero + Vector3.up * 10f;
        shockSphere.localScale = Vector3.zero;
        shockSphere.gameObject.SetActive(true);
        shockRadius = 0;
    }

    // 충격파가 집에 닿는다: 유리가 터지고 커튼이 휘날리고 먼지가 쏟아지고 물건이 떨어진다
    void Shock()
    {
        gust = 1.5f;
        lampTime = 0;
        ceilingDustLeft = 2.6f;
        if (shards != null) { shards.Play(); shards.Emit(260); }
        if (windowDust != null) { windowDust.Play(); windowDust.Emit(70); }
        if (ceilingDust != null) ceilingDust.Play();
        // 창틀에 남는 들쭉날쭉한 유리 조각들 (유리판을 끄기 전에 그 크기를 읽는다)
        if (windowPanes != null && glassShard != null)
        {
            var rng = new System.Random(5);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            foreach (var pane in windowPanes)
            {
                if (pane == null) continue;
                var b = pane.bounds;
                for (int i = 0; i < 9; i++)
                {
                    // 가장자리 네 변 중 하나에 붙는다
                    int side = rng.Next(4);
                    float u = R(0.05f, 0.95f);
                    Vector3 pos = side == 0 ? new Vector3(Mathf.Lerp(b.min.x, b.max.x, u), b.min.y, b.center.z)
                        : side == 1 ? new Vector3(Mathf.Lerp(b.min.x, b.max.x, u), b.max.y, b.center.z)
                        : side == 2 ? new Vector3(b.min.x, Mathf.Lerp(b.min.y, b.max.y, u), b.center.z)
                        : new Vector3(b.max.x, Mathf.Lerp(b.min.y, b.max.y, u), b.center.z);
                    var go = new GameObject("GlassRemnant");
                    go.transform.SetParent(home, true);
                    go.transform.position = pos;
                    float roll = side == 0 ? R(60f, 120f) : side == 1 ? R(240f, 300f) : side == 2 ? R(-30f, 30f) : R(150f, 210f);
                    go.transform.rotation = Quaternion.Euler(0, 0, roll);
                    float sc = R(0.12f, 0.3f);
                    go.transform.localScale = new Vector3(sc, sc * R(1f, 1.8f), 1f);
                    go.AddComponent<MeshFilter>().sharedMesh = ShardMesh(i % 3);
                    var mr = go.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = pane.sharedMaterial;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    remnants.Add(go);
                }
            }
        }
        if (windowPanes != null) foreach (var p in windowPanes) if (p != null) p.enabled = false;
        // 소품이 떨어진다
        if (looseProps != null && !propsLoose)
        {
            propsLoose = true;
            var push = -(windowCenter.position - home.position).normalized;
            push.y = 0;
            foreach (var p in looseProps)
            {
                if (p == null) continue;
                if (p.GetComponent<Collider>() == null) p.gameObject.AddComponent<BoxCollider>();
                var rb = p.gameObject.AddComponent<Rigidbody>();
                rb.mass = 0.4f;
                rb.AddForce(push * UnityEngine.Random.Range(1.2f, 2.6f) + Vector3.up * UnityEngine.Random.Range(0.4f, 1.2f) + UnityEngine.Random.insideUnitSphere * 0.5f, ForceMode.Impulse);
                rb.AddTorque(UnityEngine.Random.insideUnitSphere * 0.4f, ForceMode.Impulse);
            }
        }
    }

    void UpdateFx(float dt)
    {
        float t = Time.time;
        // 커튼
        if (curtainBase != null)
        {
            bool broken = shocked;
            if (broken) gust = Mathf.Max(0.12f, gust - dt * 0.42f);
            float g = gust * (0.8f + 0.2f * Mathf.Sin(t * 6.5f));
            UpdateCurtain(curtainMeshL, g, t, 0f);
            UpdateCurtain(curtainMeshR, g, t, 1.7f);
        }
        // 천장 등이 흔들린다
        if (ceilingLamp != null && lampTime >= 0)
        {
            lampTime += dt;
            float a = 24f * Mathf.Exp(-lampTime * 0.4f);
            ceilingLamp.localRotation = Quaternion.Euler(Mathf.Sin(lampTime * 4.6f) * a, 0, Mathf.Cos(lampTime * 4.1f) * a * 0.55f);
        }
        // 천장 가루는 잠깐만
        if (ceilingDustLeft > 0)
        {
            ceilingDustLeft -= dt;
            if (ceilingDustLeft <= 0 && ceilingDust != null) ceilingDust.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
        // 충격파 구
        if (shockRadius >= 0 && shockSphere != null)
        {
            shockRadius += ShockSpeed * dt;
            shockSphere.localScale = Vector3.one * (shockRadius * 2f);
            float a = 0.28f * Mathf.Clamp01(1 - shockRadius / 900f);
            shockMat.SetColor("_BaseColor", new Color(1f, 0.9f, 0.8f, a));
            if (shockRadius > 900f) { shockSphere.gameObject.SetActive(false); shockRadius = -1f; }
        }
        // 거리: 재·연기는 카메라를 따라다니고, 경광등은 돌고, 먼 불빛과 비상등은 깜빡인다
        if (Shot == 3)
        {
            var p = cam.transform.position;
            if (ash != null) ash.transform.position = new Vector3(p.x, p.y + 12f, p.z);
            if (embers != null) embers.transform.position = new Vector3(p.x, p.y - 1f, p.z);
            if (groundSmoke != null) groundSmoke.transform.position = new Vector3(p.x, street.position.y + 1.1f, p.z);
            if (beaconHead != null) beaconHead.Rotate(0, 420f * dt, 0, Space.Self);
            if (beaconLight != null) beaconLight.intensity = 16f;
            if (distantLights != null)
                for (int i = 0; i < distantLights.Length; i++)
                    if (distantLights[i] != null) distantLights[i].intensity = Mathf.Sin(t * 5f + i * 2.1f) > 0.2f ? 9f : 0f;
            if (fireLights != null)
                for (int i = 0; i < fireLights.Length; i++)
                    if (fireLights[i] != null) fireLights[i].intensity = 10f + 7f * Mathf.PerlinNoise(t * 9f, i * 3.3f);
        }
    }

    // 커튼 정점: 아래로 갈수록 크게, 안쪽(-z)으로 밀리며 들려 올라간다. 평소엔 아주 약한 바람
    void UpdateCurtain(Mesh mesh, float g, float t, float phase)
    {
        for (int i = 0; i < curtainBase.Length; i++)
        {
            var b = curtainBase[i];
            float f = Mathf.Pow(Mathf.Clamp01(-b.y / CurtainHeight), 1.35f);
            float breeze = 0.012f * Mathf.Sin(t * 1.3f + b.x * 4f + phase) * f;
            float dz = -g * (0.75f * f + 0.3f * f * Mathf.Sin(b.x * 7f + t * 13f + b.y * 5f + phase));
            float dx = g * 0.1f * f * Mathf.Sin(t * 9f + b.x * 3f + phase);
            float dy = g * 0.4f * f * f;
            curtainWork[i] = new Vector3(b.x + dx, b.y + dy, b.z + breeze + dz);
        }
        mesh.vertices = curtainWork;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }
}
