using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// GameSceneBuilder의 삼일아파트 102동 씬 (Apt102) 점검: 생성한 씬이 설계대로이고 끝까지 걸어 다닐 수 있는지 편집 모드에서 잰다.
// 생성기 파일의 상수는 쓰지 않는다. 설계 문서의 좌표와 이름 약속만으로 확인해야 생성기가 틀렸을 때 여기서 드러난다.
// 씬은 읽기만 한다 (물체를 만들거나 고치거나 저장하지 않는다)
public static partial class GameSceneBuilder
{
    [MenuItem("Tools/핵폭발 생존/삼일아파트 102동 점검")]
    static void ProbeApt102Menu()
    {
        Apt102ProbeReport();
    }

    // 편집 모드 점검 전체. 보고서를 글로 돌려주고 Library/Apt102Probe.txt에도 쓴다.
    // 줄마다 "[Apt102Probe] PASS|FAIL|WARN <번호> <내용>", 마지막 줄은 "[Apt102Probe] DONE pass=N fail=N warn=N"
    public static string Apt102ProbeReport() => Apt102Probe.Report();

    // 걷기 시험용: 경사로 발치에서 옥상 계단참까지 내비메시 경로의 꺾이는 점.
    // "status=PathComplete;mode=single;length=107.42;corners=58|x,y,z|x,y,z|…" ('|'로 자른 첫 칸이 머리글, 나머지가 점)
    public static string Apt102ProbeRoute() => Apt102Probe.Route();

    // 예산 확인용: 오브젝트·정점·빛·콜라이더 수를 JSON 한 줄로
    public static string Apt102ProbeCounts() => Apt102Probe.Counts();

    // 점검의 속. 다른 생성기 파일과 이름이 부딪히지 않게 한 클래스 안에 가둔다
    static class Apt102Probe
    {
        // ================= 설계 좌표 (씬 월드 = 탑 로컬. 층 바닥 윗면 높이 = 2.8 × 층, 2층 = 2.8) =================

        const string ScenePath = "Assets/Scenes/Apt102.unity";
        const string MeshPath = "Assets/Generated/Apt102Meshes.asset";
        const string NavPath = "Assets/Generated/Apt102NavMesh.asset";
        const string ProfilePath = "Assets/Settings/Apt102Profile.asset";
        const string OcclusionPath = "Assets/Scenes/Apt102/OcclusionCullingData.asset";
        const string ReportPath = "Library/Apt102Probe.txt";
        const string BuildListPath = "ProjectSettings/EditorBuildSettings.asset";
        static readonly string[] OldScenes = { "Assets/Scenes/MainMenu.unity", "Assets/Scenes/Cutscene.unity", "Assets/Scenes/Shelter.unity", "Assets/Scenes/City.unity" };

        const int AllLayers = ~0;                 // 물리 질의는 모든 레이어를 본다 (플레이어를 막는 것은 다 잡아야 한다)
        const float SnapRadius = 1f, SnapXZ = 0.6f, SnapY = 0.3f; // "그 자리가 내비메시 위"라고 치는 범위 (지킴이가 서는 규칙과 같다)
        const float EyeHeight = 1.65f, Reach = 2.6f;              // 플레이어 눈높이와 상호작용 거리 (PlayerInteractor)
        const int MaxVertices = 150000, MaxLights = 20, MaxShadowPointLights = 2;
        const long MaxFileBytes = 20L * 1024 * 1024, MinMeshBytes = 512L * 1024;

        // Tripo 품질별 모델 (2026-11-26, 102동 소품). 이름 약속: 받침 'Tripo_<id>' 아래 'Model'(LODGroup + QualityLod)과 판 넷 LOD0_q3 … LOD3_q0.
        // 판 메시는 모델 폴더의 품질 폴더(Q{n}_…)에 든 에셋을 그대로 가리키고 (이 씬의 메시 묶음에 들지 않는다), 재질은 M_TR_…_q{n}
        const string TripoHolderPrefix = "Tripo_", TripoModelName = "Model", TripoMeshRoot = "Assets/ThirdParty/Tripo/Models/", TripoMatPrefix = "M_TR_";
        // B3.tripo: 코드로 지은 메시의 한도(MaxVertices)와 따로 센다. 둘 다 어림 한도라 넘으면 경고만 한다 (판마다의 면 수는 사진으로 통과시킨 값이다).
        //  - 낮음 판의 합 (놓인 것 기준): 세부 묘사 품질 '낮음'에서는 거리와 상관없이 이 판만 그린다. 코드로 지은 씬 한도의 두 배를 넘으면
        //    가장 낮은 설정에서도 소품이 건물 전체보다 무거워진다. 계획(약 190곳)의 어림은 11만~14만
        //  - 서로 다른 판 메시의 합: 판 넷은 품질 설정과 상관없이 씬과 함께 모두 메모리에 올라온다 (QualityLod는 렌더러만 끄고 켠다).
        //    정점 하나에 약 48바이트(자리·법선·접선·UV)라 200만이면 약 96MB. 계획(20가지 × 판 넷)의 어림은 90만~130만
        const int MaxTripoLowVertices = 300000, MaxTripoMeshVertices = 2000000;

        struct Pt
        {
            public readonly string name;
            public readonly Vector3 p;
            public Pt(string name, float x, float y, float z) { this.name = name; p = new Vector3(x, y, z); }
        }

        static readonly Vector3 RampFoot = new Vector3(14.7f, 0f, 12.4f); // 경사로 R1 발치 앞 마당
        static readonly Vector3 Pad = new Vector3(7.1f, 22.4f, 3.7f);     // 옥상 계단참

        // B6: 경사로 발치에서 끝까지 이어져야 하는 곳
        static readonly Pt[] ReachTargets =
        {
            new Pt("204호", 10.6f, 2.8f, 0.5f),
            new Pt("B코어 2층 남쪽 참", 6.5f, 2.8f, -3.7f),
            new Pt("B코어 3층 북쪽 참", 6.5f, 5.6f, 3.7f),
            new Pt("B코어 4층 남쪽 참", 6.5f, 8.4f, -3.7f),
            new Pt("403호 남쪽 방", 2.3f, 8.4f, -3.6f),
            new Pt("403호 부엌", 1.0f, 8.4f, -0.9f),
            new Pt("403호 북쪽 방", 2.6f, 8.4f, 3.6f),
            new Pt("402호 북쪽 방", -2.3f, 8.4f, 3.6f),
            new Pt("402호 가운데 방", -2.3f, 8.4f, 0f),
            new Pt("402호 남쪽 방", -2.3f, 8.4f, -3.6f),
            new Pt("A코어 4층 남쪽 참", -6.5f, 8.4f, -3.7f),
            new Pt("A코어 5층 북쪽 참", -6.5f, 11.2f, 3.7f),
            new Pt("A코어 6층 남쪽 참", -6.5f, 14.0f, -3.7f),
            new Pt("6층 홀 남쪽 통로", 0f, 14.0f, -4.1f),
            new Pt("6층 홀 야영지", 2.0f, 14.0f, 1.0f),
            new Pt("B코어 6층 남쪽 참", 6.5f, 14.0f, -3.7f),
            new Pt("604호", 10.6f, 14.0f, -3.6f),
            new Pt("B코어 7층 북쪽 참", 6.5f, 16.8f, 3.7f),
            new Pt("B코어 8층 남쪽 참", 6.5f, 19.6f, -3.7f),
            new Pt("804호", 10.6f, 19.6f, -1.0f),
            new Pt("옥상 계단참", 7.1f, 22.4f, 3.7f),
            new Pt("골목 표지판 앞", -19.8f, 0f, 28.2f),
            new Pt("출발 지점", -14f, 0f, 30f),
        };

        // B6: 올라가는 길의 참 순서 (ReachTargets의 번호). 이웃한 참 사이의 경로 길이로 끊긴 곳·지름길을 잡는다
        static readonly int[] LandingChain = { 1, 2, 3, 10, 11, 12, 15, 17, 18, 20 };
        const float RouteMin = 85f, RouteMax = 130f, LegMin = 5f, LegMax = 40f;

        // B7: 내비메시가 있으면 안 되는 곳 (잘라 낸 방·지붕·경계 밖)
        static readonly Pt[] NoMeshPoints =
        {
            new Pt("303호 (숨는 방)", 2.3f, 5.6f, 2.6f),
            new Pt("304호", 10.6f, 5.6f, 0f),
            new Pt("404호", 10.6f, 8.4f, 1.5f),
            new Pt("504호", 10.6f, 11.2f, 3.5f),
            new Pt("704호", 10.6f, 16.8f, 2.5f),
            new Pt("옥상 탱크 동쪽 좁은 길", 12.2f, 22.4f, 0.3f),
            new Pt("옥상 잔해 더미 위", 7.1f, 24.0f, -3.7f),
            new Pt("남은 몸체 꼭대기", 0f, 39.2f, 0f),
            new Pt("경비실 지붕", -6.6f, 2.76f, 21.6f),
            new Pt("서쪽 경계 밖", -23f, 0f, 10f),
            new Pt("남쪽 경계 밖", 2f, 0f, -13f),
            new Pt("동쪽 경계 밖", 27f, 0f, 10f),
            new Pt("북쪽 경계 밖", 2f, 0f, 37f),
        };

        // B7: 옥상 싸움터 (일부러 떼어 놓은 섬). 서로는 이어지고 본체와는 끊겨 있어야 한다
        static readonly Pt[] ArenaPoints =
        {
            new Pt("옥상 북쪽 (G8b 자리)", 10.6f, 22.4f, 3.6f),
            new Pt("탱크와 중계기함 사이 (G8 자리)", 9.4f, 22.4f, -1.8f),
            new Pt("중계기함 앞", 9.5f, 22.4f, -4.0f),
        };
        // 싸움터 바닥 범위 (섬이 싸움터인지 가릴 때의 마지막 수단)
        const float ArenaX0 = 8.45f, ArenaX1 = 12.75f, ArenaZ0 = -5.25f, ArenaZ1 = 5.25f, RoofY = 22.4f;

        struct Flight
        {
            public readonly string name, surface;
            public readonly Vector3 foot, top;
            public readonly float width;
            public Flight(string name, float fx, float fy, float fz, float tx, float ty, float tz, float width, string surface)
            {
                this.name = name; this.surface = surface; this.width = width;
                foot = new Vector3(fx, fy, fz); top = new Vector3(tx, ty, tz);
            }
        }

        // B10: 계단과 경사로 (발치 가운데 → 꼭대기 가운데, 폭, 밟는 콜라이더 이름)
        static readonly Flight[] Flights =
        {
            new Flight("B1", 7.4f, 2.8f, -2.2f, 7.4f, 5.6f, 2.2f, 1.7f, "Floor"),
            new Flight("B2", 5.6f, 5.6f, 2.2f, 5.6f, 8.4f, -2.2f, 1.7f, "Floor"),
            new Flight("A3", -5.6f, 8.4f, -2.2f, -5.6f, 11.2f, 2.2f, 1.7f, "Floor"),
            new Flight("A4", -7.4f, 11.2f, 2.2f, -7.4f, 14.0f, -2.2f, 1.7f, "Floor"),
            new Flight("B5", 7.4f, 14.0f, -2.2f, 7.4f, 16.8f, 2.2f, 1.7f, "Floor"),
            new Flight("B6", 5.6f, 16.8f, 2.2f, 5.6f, 19.6f, -2.2f, 1.7f, "Floor"),
            new Flight("B7", 7.4f, 19.6f, -2.2f, 7.4f, 22.4f, 2.2f, 1.7f, "Floor"),
            new Flight("R1", 14.7f, 0f, 11.6f, 14.7f, 2.8f, 5.2f, 3.0f, "Slab"),
        };
        const float FlightStep = 0.25f, CapsuleLow = 0.45f, CapsuleHigh = 1.75f, CapsuleRadius = 0.33f;

        // B10: 발소리 재질을 정하는 바닥 이름 (이름 약속)
        struct Surface
        {
            public readonly string name, collider;
            public readonly Vector3 p;
            public Surface(string name, float x, float y, float z, string collider) { this.name = name; this.collider = collider; p = new Vector3(x, y, z); }
        }
        static readonly Surface[] Surfaces =
        {
            new Surface("마당", 0f, 0f, 30f, "Ground"),
            new Surface("경사로 위 턱", 14.6f, 2.8f, 3.6f, "Slab"),
            new Surface("B코어 2층 남쪽 참", 6.5f, 2.8f, -3.7f, "Floor"),
            new Surface("A코어 6층 남쪽 참", -6.5f, 14.0f, -3.7f, "Floor"),
            new Surface("6층 홀 남쪽 통로", 0f, 14.0f, -4.1f, "Floor"),
            new Surface("옥상 계단참", 7.1f, 22.4f, 3.7f, "Rubble_Roof"),
            new Surface("옥상 싸움터", 9.4f, 22.4f, -1.8f, "Rubble_Roof"),
        };

        struct Hole
        {
            public readonly string name;
            public readonly float y, x0, x1, z0, z1; // y = 구멍이 난 바닥판의 윗면
            public Hole(string name, float y, float x0, float x1, float z0, float z1) { this.name = name; this.y = y; this.x0 = x0; this.x1 = x1; this.z0 = z0; this.z1 = z1; }
        }

        // B9: 4호 줄의 바닥 구멍 (굴뚝)
        static readonly Hole[] Holes =
        {
            new Hole("H8", 22.4f, 10.6f, 12.6f, -4.9f, -2.9f),
            new Hole("H7", 19.6f, 9.6f, 11.6f, 2.6f, 4.6f),
            new Hole("H6", 16.8f, 9.6f, 11.6f, -2.4f, -0.4f),
            new Hole("H5", 14.0f, 9.6f, 11.6f, 2.6f, 4.6f),
            new Hole("H4", 11.2f, 9.6f, 11.6f, -3.8f, -0.4f),
            new Hole("H3", 8.4f, 9.6f, 11.6f, -3.8f, -1.8f),
            new Hole("H2", 5.6f, 9.6f, 11.6f, 2.6f, 4.6f),
        };
        const float HoleClearDepth = 2.6f, HoleInset = 0.05f;

        struct Drop
        {
            public readonly string name;
            public readonly Vector3 start; // 바닥판 0.5m 위
            public readonly float fall;   // 아래로 맞아야 하는 거리
            public Drop(string name, float x, float y, float z, float fall) { this.name = name; start = new Vector3(x, y, z); this.fall = fall; }
        }

        // B9: 구멍 가운데 0.5m 위에서 아래로 쏜 선이 맞는 거리. H4는 먼 쪽이 H3 위라 두 층(6.1m), 가까운 쪽은 한 층
        static readonly Drop[] Drops =
        {
            new Drop("H8 가운데", 11.6f, 22.9f, -3.9f, 3.3f),
            new Drop("H7 가운데", 10.6f, 20.1f, 3.6f, 3.3f),
            new Drop("H6 가운데", 10.6f, 17.3f, -1.4f, 3.3f),
            new Drop("H5 가운데", 10.6f, 14.5f, 3.6f, 3.3f),
            new Drop("H4 먼 쪽 (H3 위)", 10.6f, 11.7f, -2.8f, 6.1f),
            new Drop("H4 가까운 쪽", 10.6f, 11.7f, -1.1f, 3.3f),
            new Drop("H3 가운데", 10.6f, 8.9f, -2.8f, 3.3f),
            new Drop("H2 가운데", 10.6f, 6.1f, 3.6f, 3.3f),
        };
        const float DropTolerance = 0.15f;

        // B11: 문간·틈의 가운데 (내비메시가 지나가야 한다)
        static readonly Pt[] Doorways =
        {
            new Pt("D1 (204호 문)", 8.35f, 2.8f, -3.7f),
            new Pt("D3a (403호 문)", 4.65f, 8.4f, -3.7f),
            new Pt("D3b (402호 문)", -4.65f, 8.4f, -3.7f),
            new Pt("D5a (홀 서쪽 문)", -4.65f, 14.0f, -3.7f),
            new Pt("D5b (홀 동쪽 문)", 4.65f, 14.0f, -3.7f),
            new Pt("D5c (604호 문)", 8.35f, 14.0f, -3.7f),
            new Pt("D7 (804호 문)", 8.35f, 19.6f, -3.7f),
            new Pt("홀 바리케이드 서쪽 틈", -3.725f, 14.0f, -2.7f),
            new Pt("홀 바리케이드 동쪽 틈", 3.725f, 14.0f, -2.7f),
            new Pt("4층 세대벽 구멍", 0f, 8.4f, 3.3f),
            new Pt("403호 남쪽 칸막이 문", 3.5f, 8.4f, -1.65f),
            new Pt("403호 북쪽 칸막이 문", 1.2f, 8.4f, 1.65f),
            new Pt("402호 북쪽 칸막이 문", -3.1f, 8.4f, 1.65f),
            new Pt("402호 남쪽 칸막이 문", -1.1f, 8.4f, -1.65f),
            new Pt("동쪽 끝벽 뚫린 곳", 12.875f, 2.8f, 3.6f),
        };

        // B11: 판자로 막은 문 (벽이 z 방향으로 뻗어 있다). 플레이어만 앉아서 지나간다
        static readonly Pt[] CrawlGaps =
        {
            new Pt("BD2 (303호 판자문)", 4.65f, 5.6f, 3.7f),
            new Pt("BD8 (옥상 판자문)", 8.35f, 22.4f, 3.8f),
        };
        const float PlankMin = 1.38f, PlankMax = 1.55f, DoorSample = 0.25f;
        const float MeshLift = 0.08f; // 구운 내비메시가 바닥 위로 떠 있는 높이 어림 (복셀 0.167의 반 칸)

        // B12: 옥상 가장자리. 이 사각형을 벗어나기 전에 반드시 무언가에 막혀야 한다
        const float RoofX0 = 6f, RoofX1 = 13f, RoofZ0 = -5.5f, RoofZ1 = 5.5f;
        static readonly float[] SweepHeights = { 0.5f, 1.5f, 2.9f };
        static readonly Pt[] SweepPoints =
        {
            new Pt("싸움터", 9.1f, 22.4f, -4.4f), new Pt("싸움터", 10.9f, 22.4f, -4.4f), new Pt("싸움터", 12.3f, 22.4f, -4.4f),
            new Pt("싸움터", 9.1f, 22.4f, -2.2f), new Pt("싸움터", 10.9f, 22.4f, -2.2f), new Pt("싸움터", 12.3f, 22.4f, -2.2f),
            new Pt("싸움터", 9.1f, 22.4f, 2.4f), new Pt("싸움터", 10.9f, 22.4f, 2.4f), new Pt("싸움터", 12.3f, 22.4f, 2.4f),
            new Pt("싸움터", 9.1f, 22.4f, 4.6f), new Pt("싸움터", 10.9f, 22.4f, 4.6f), new Pt("싸움터", 12.3f, 22.4f, 4.6f),
            new Pt("계단참", 6.5f, 22.4f, 2.8f), new Pt("계단참", 7.1f, 22.4f, 3.7f), new Pt("계단참", 7.8f, 22.4f, 4.7f),
        };
        const int SweepDirections = 24;

        struct Post
        {
            public readonly string name;
            public readonly SpawnKind kind;
            public readonly int tier;
            public readonly Vector3 p;
            public readonly float yaw;
            public Post(string name, SpawnKind kind, int tier, float x, float y, float z, float yaw) { this.name = name; this.kind = kind; this.tier = tier; p = new Vector3(x, y, z); this.yaw = yaw; }
        }

        // B2: 반드시 서는 지킴이 (설계 Q4의 표)
        static readonly Post[] Posts =
        {
            new Post("G1", SpawnKind.Normal, 0, 10.2f, 2.8f, -3.4f, 180f),
            new Post("G2", SpawnKind.Normal, 0, 6.5f, 5.6f, 3.9f, 180f),
            new Post("G3a", SpawnKind.Normal, 0, 2.6f, 8.4f, -3.6f, 90f),
            new Post("G3b", SpawnKind.Leather, 0, -2.3f, 8.4f, 0f, 0f),
            new Post("G5a", SpawnKind.Raider, 0, 1.6f, 14.0f, 3.6f, 110f),
            new Post("G5b", SpawnKind.Raider, 0, 3.8f, 14.0f, 1.2f, 300f),
            new Post("G6", SpawnKind.Leather, 0, 6.5f, 16.8f, 3.9f, 180f),
            new Post("G7", SpawnKind.Normal, 0, 9.4f, 19.6f, -3.6f, 270f),
            new Post("G8", SpawnKind.Heavy, 0, 9.4f, 22.4f, -1.8f, 0f),
            new Post("G9", SpawnKind.Ranged, 0, 11.4f, 22.4f, -2.0f, 0f),
            new Post("G4", SpawnKind.Leather, 1, -6.5f, 11.2f, 3.9f, 180f),
            new Post("G5c", SpawnKind.Ranged, 1, -3.2f, 14.0f, 4.0f, 150f),
            new Post("G8b", SpawnKind.Leather, 1, 10.6f, 22.4f, 3.6f, 180f),
            new Post("D1", SpawnKind.Dog, 0, -10.5f, 0f, 14.0f, 90f),
            new Post("D2", SpawnKind.Dog, 0, 11.0f, 0f, 18.6f, 200f),
        };
        const float HeavyMinY = 22f; // 무거운 감염자는 천장이 없는 옥상에만 선다

        // B2: 거리를 돌아다니는 감염자가 나오는 자리 (Roam1~Roam6)
        static readonly Pt[] RoamPoints =
        {
            new Pt("Roam", -18.5f, 0f, 2f), new Pt("Roam", -16f, 0f, -9f), new Pt("Roam", 4f, 0f, -9.5f),
            new Pt("Roam", 21.5f, 0f, -7f), new Pt("Roam", 22.5f, 0f, 16f), new Pt("Roam", -18f, 0f, 20f),
        };

        struct Loot
        {
            public readonly string room, fixedId; // fixedId가 있으면 반드시 놓이는 자리
            public readonly int amount;
            public readonly Vector3 p;
            public Loot(string room, float x, float y, float z, string fixedId = null, int amount = 0) { this.room = room; this.fixedId = fixedId; this.amount = amount; p = new Vector3(x, y, z); }
        }

        // B2: 실내 아이템 자리 (생성 작업서 8절의 표: 무작위 25곳 + 반드시 놓이는 6곳). 임시 수치
        static readonly Loot[] LootTable =
        {
            new Loot("204", 9.0f, 3.0f, 1.2f), new Loot("204", 12.2f, 3.0f, -1.0f), new Loot("204", 8.9f, 3.0f, -4.8f, "battery", 1),
            new Loot("303", 0.8f, 5.8f, 4.6f), new Loot("303", 2.4f, 5.8f, 0.7f), new Loot("303", 4.0f, 5.8f, 4.7f), new Loot("303", 0.7f, 5.8f, 1.2f, "bandage", 2),
            new Loot("304", 12.2f, 5.8f, -4.4f), new Loot("304", 9.0f, 5.8f, 0.5f), new Loot("304", 12.2f, 5.8f, 1.5f, "bandage", 1),
            new Loot("403", 0.8f, 8.6f, -4.6f), new Loot("403", 0.6f, 8.6f, -1.0f), new Loot("403", 4.1f, 8.6f, 1.2f), new Loot("403", 3.9f, 8.6f, 4.7f),
            new Loot("402", -3.9f, 8.6f, 4.6f), new Loot("402", -0.7f, 8.6f, -1.0f), new Loot("402", -0.7f, 8.6f, -4.7f),
            new Loot("404", 12.2f, 8.6f, 1.2f),
            new Loot("504", 9.0f, 11.4f, 4.8f, "bandage", 1), new Loot("504", 12.2f, 11.4f, 1.5f),
            new Loot("hall", 4.1f, 14.2f, 3.4f), new Loot("hall", 4.1f, 14.2f, 4.5f), new Loot("hall", 3.2f, 14.2f, 4.7f), new Loot("hall", 0.4f, 14.2f, 4.7f), new Loot("hall", 2.4f, 14.2f, 4.7f, "canned_food", 1),
            new Loot("604", 12.2f, 14.2f, -4.6f), new Loot("604", 9.0f, 14.2f, 0.8f),
            new Loot("704", 12.2f, 17.0f, 4.6f, "copper_wire", 2), new Loot("704", 9.0f, 17.0f, 2.0f), new Loot("704", 12.2f, 17.0f, -4.6f),
            new Loot("804", 12.2f, 19.8f, 0.5f),
        };
        // 점검 문서는 "무작위 22 + 고정 7"이라 적었고 생성 작업서의 표를 세면 25 + 6이다. 어느 쪽이든 받아들이고 어느 쪽인지 적는다
        const int RandomByTable = 25, FixedByTable = 6, RandomByText = 22, FixedByText = 7, OutdoorItems = 5;

        struct Zone
        {
            public readonly string name;
            public readonly HazardZone.Kind kind;
            public readonly Vector3 p, half;
            public readonly int bit;
            public Zone(string name, HazardZone.Kind kind, float x, float y, float z, float hx, float hy, float hz, int bit)
            {
                this.name = name; this.kind = kind; this.bit = bit;
                p = new Vector3(x, y, z); half = new Vector3(hx, hy, hz);
            }
        }

        // B2: 위험 구역 일곱 (불길 둘, 유리 조각 둘, 힌트 셋). 힌트 비트 128 = 울림, 256 = 추락
        static readonly Zone[] Zones =
        {
            new Zone("Fire_F1", HazardZone.Kind.Fire, 2.3f, 8.4f, 0f, 0.7f, 1.0f, 0.7f, 0),
            new Zone("Fire_F2", HazardZone.Kind.Fire, 10.6f, 8.4f, 4.2f, 1.4f, 1.0f, 0.8f, 0),
            new Zone("Glass_N1", HazardZone.Kind.Glass, 1.6f, 8.4f, 3.5f, 1.5f, 1.0f, 1.6f, 0),
            new Zone("Glass_N2", HazardZone.Kind.Glass, -2.6f, 14.0f, -0.4f, 1.7f, 1.0f, 1.9f, 0),
            new Zone("Hint_Echo", HazardZone.Kind.Hint, 12.4f, 2.8f, 3.6f, 0.9f, 1.2f, 1.4f, 128),
            new Zone("Hint_Fall_Roof", HazardZone.Kind.Hint, 10.0f, 22.4f, -3.9f, 1.0f, 1.2f, 1.0f, 256),
            new Zone("Hint_Fall_504", HazardZone.Kind.Hint, 10.6f, 11.2f, 1.2f, 1.5f, 1.2f, 1.0f, 256),
        };

        // B1: 씬의 CityDirector 값. 임시 수치 (생성기 맨 위의 상수와 같이 바꾼다)
        const float WantNormal = 60f, WantLeather = 30f, WantRanged = 10f, WantHeavy = 0f, WantFill = 0f, WantSafeRadius = 18f;
        const int WantStart = 2, WantMax = 5, WantDogs = 0;
        static readonly Vector3 SignSpot = new Vector3(-21.0f, 0f, 28.2f);   // 돌아가는 표지판
        static readonly Vector3 PlayerSpot = new Vector3(-14f, 0.05f, 30f);  // 출발 자리

        // ================= 상태 =================

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static readonly List<string> lines = new List<string>();
        static int pass, fail, warn;
        static Scene scene;
        static Transform root, geo, site, tower, facade, points, dyn, player;
        static bool navOk, rampOk;
        static Vector3 rampOn;
        static readonly List<Transform> tripoHolders = new List<Transform>();     // 이름이 Tripo_로 시작하는 받침 (경로·자리 순)
        static readonly HashSet<Transform> tripoParts = new HashSet<Transform>(); // 받침의 Model과 그 아래 전부 (품질별 판)

        // ================= 보고 =================

        public static string Report()
        {
            lines.Clear();
            pass = fail = warn = 0;
            root = geo = site = tower = facade = points = dyn = player = null;
            navOk = rampOk = false;
            tripoHolders.Clear();
            tripoParts.Clear();

            bool open = false;
            Step("B0", () => open = OpenScene());
            if (open)
            {
                Physics.SyncTransforms(); // 편집 모드의 물리 질의가 지금 씬의 콜라이더 자리를 보게 한다
                Step("B0", TripoScan);
                Step("A.guard", GuardResult);
                Step("B1", Conventions);
                Step("B1.director", DirectorCheck);
                Step("B1.cache", CacheCheck);
                Step("B1.exit", ExitCheck);
                Step("B1.player", PlayerCheck);
                Step("B1.parents", ParentCheck);
                Step("B1.surface", SurfaceCheck);
                Step("B2", PointTables);
                Step("B2.items", ItemTables);
                Step("B2.hazard", HazardTable);
                Step("B3", Budgets);
                Step("B3.tripo", TripoBudget);
                Step("B4", MeshesAndFiles);
                Step("B5.mesh", NavPrep);
                Step("B5", Sampling);
                Step("B6", Connectivity);
                Step("B7", NegativeChecks);
                Step("B8", Islands);
                Step("B9", HoleChecks);
                Step("B10", FlightChecks);
                Step("B11", DoorwayChecks);
                Step("B12", RoofEdges);
                Step("B13", BuildList);
                Step("B14", CoplanarAndZFight);
                Step("B15", TripoModels);
                Step("B15.props", TripoProps);
            }

            string done = "[Apt102Probe] DONE pass=" + pass + " fail=" + fail + " warn=" + warn;
            lines.Add(done);
            string text = string.Join("\n", lines) + "\n";
            try
            {
                File.WriteAllText(Path.Combine(Path.GetDirectoryName(Application.dataPath), ReportPath), text, new UTF8Encoding(false));
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Apt102Probe] 보고서 파일을 쓰지 못했다: " + e.Message);
            }
            Debug.Log(done);
            return text;
        }

        // 점검 하나가 예외로 죽어도 나머지는 계속한다
        static void Step(string id, System.Action body)
        {
            try { body(); }
            catch (System.Exception e) { Fail(id, "점검 도중 예외로 멈췄다: " + e.GetType().Name + ": " + e.Message); }
        }

        static string OneLine(string s) => s.Replace("\r", " ").Replace("\n", " ");
        static void Pass(string id, string text) { pass++; lines.Add("[Apt102Probe] PASS " + id + " " + OneLine(text)); }
        static void Warn(string id, string text) { warn++; string l = "[Apt102Probe] WARN " + id + " " + OneLine(text); lines.Add(l); Debug.LogWarning(l); }
        static void Fail(string id, string text) { fail++; string l = "[Apt102Probe] FAIL " + id + " " + OneLine(text); lines.Add(l); Debug.LogError(l); }

        // ================= 공통 도우미 =================

        static string F(float v) => v.ToString("0.00", Inv);
        static string V(Vector3 p) => "(" + F(p.x) + ", " + F(p.y) + ", " + F(p.z) + ")";
        static float Flat(Vector3 a, Vector3 b) { float dx = a.x - b.x, dz = a.z - b.z; return Mathf.Sqrt(dx * dx + dz * dz); }

        // 이 씬에 있는 T 전부 (꺼진 것 포함). 다른 씬이 같이 열려 있어도 섞이지 않는다
        static List<T> InScene<T>() where T : Component
        {
            var list = new List<T>();
            foreach (var c in Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (c != null && c.gameObject.scene == scene) list.Add(c);
            return list;
        }

        static string PathOf(Transform t)
        {
            if (t == null) return "(없음)";
            string s = t.name;
            int depth = 0;
            for (var p = t.parent; p != null && depth < 3; p = p.parent, depth++) s = p.name + "/" + s;
            return s;
        }

        static bool Under(Transform t, Transform ancestor) => t != null && ancestor != null && t.IsChildOf(ancestor);

        static Transform RootNamed(string name)
        {
            foreach (var go in scene.GetRootGameObjects())
                if (go.name == name) return go.transform;
            return null;
        }

        static Transform Child(Transform parent, string name) => parent == null ? null : parent.Find(name);

        // under 아래에서 이름이 name인 것 전부 (skip 아래는 뺀다: 가져다 쓴 아파트 겉모습에도 같은 이름이 있다)
        static List<Transform> Named(Transform under, string name, Transform skip = null)
        {
            var list = new List<Transform>();
            if (under == null) return list;
            foreach (var t in under.GetComponentsInChildren<Transform>(true))
                if (t.name == name && (skip == null || !t.IsChildOf(skip))) list.Add(t);
            return list;
        }

        // ---- Tripo 품질별 모델 ----

        // 씬의 Tripo 받침과 그 모델 조각을 모아 둔다 (B3·B4·B15와 수 세기가 쓴다). Tripo 모델이 없는 씬에서는 둘 다 빈다
        static void TripoScan()
        {
            tripoHolders.Clear();
            tripoParts.Clear();
            var order = new Dictionary<Transform, string>();
            foreach (var t in InScene<Transform>())
                if (t.name.StartsWith(TripoHolderPrefix)) { tripoHolders.Add(t); order[t] = PathOf(t) + " " + V(t.position); }
            tripoHolders.Sort((a, b) => string.CompareOrdinal(order[a], order[b]));
            foreach (var h in tripoHolders)
            {
                var model = h.Find(TripoModelName);
                if (model == null) continue;
                foreach (var c in model.GetComponentsInChildren<Transform>(true)) tripoParts.Add(c);
            }
        }

        static bool InTripoModel(Transform t) => tripoParts.Contains(t);

        // 모델의 판 넷을 품질 순으로 (q = 0 낮음 … 3 매우 높음). 이름 약속 LOD{3 - q}_q{q}. 없는 칸은 null
        static Transform[] TripoTiers(Transform model)
        {
            var tiers = new Transform[4];
            for (int q = 0; q < 4; q++) tiers[q] = model.Find("LOD" + (3 - q) + "_q" + q);
            return tiers;
        }

        static Mesh MeshOf(Transform t)
        {
            var mf = t == null ? null : t.GetComponent<MeshFilter>();
            return mf == null ? null : mf.sharedMesh;
        }

        // t 아래에 그려지는 메시가 하나라도 있는가 (켜진 MeshRenderer + 메시). 윤곽선도 이런 메시에만 붙는다
        static bool HasLook(Transform t)
        {
            foreach (var r in t.GetComponentsInChildren<MeshRenderer>())
                if (r.enabled && MeshOf(r.transform) != null) return true;
            return false;
        }

        // 받침의 모델이 차지하는 월드 상자 (가장 좋은 판 기준). 판이 없으면 false
        static bool TripoBounds(Transform holder, out Bounds b)
        {
            b = new Bounds();
            var model = holder.Find(TripoModelName);
            if (model == null) return false;
            var tiers = TripoTiers(model);
            for (int q = 3; q >= 0; q--)
            {
                var r = tiers[q] == null ? null : tiers[q].GetComponent<MeshRenderer>();
                if (r == null || MeshOf(tiers[q]) == null) continue;
                b = r.bounds;
                return true;
            }
            return false;
        }

        struct TripoSum
        {
            public int models, kinds, meshes;   // 모델(받침) 수, 받침 이름 가짓수, 서로 다른 판 메시 수
            public long meshVertices;           // 서로 다른 판 메시의 정점 합 (메모리에 올라오는 양)
            public long lowPlaced, highPlaced;  // 놓인 모델마다 가장 낮은 판 / 가장 좋은 판의 정점을 더한 것
        }

        static TripoSum TripoCount()
        {
            var sum = new TripoSum();
            var kinds = new HashSet<string>();
            var meshes = new HashSet<Mesh>();
            foreach (var h in tripoHolders)
            {
                var model = h.Find(TripoModelName);
                if (model == null) continue;
                sum.models++;
                kinds.Add(h.name);
                foreach (var mf in model.GetComponentsInChildren<MeshFilter>(true))
                    if (mf.sharedMesh != null) meshes.Add(mf.sharedMesh);
                Mesh low = null, high = null;
                foreach (var tier in TripoTiers(model))
                {
                    var m = MeshOf(tier);
                    if (m == null) continue;
                    if (low == null) low = m;
                    high = m;
                }
                if (low != null) { sum.lowPlaced += low.vertexCount; sum.highPlaced += high.vertexCount; }
            }
            sum.kinds = kinds.Count;
            sum.meshes = meshes.Count;
            foreach (var m in meshes) sum.meshVertices += m.vertexCount;
            return sum;
        }

        // 여러 개를 한 줄에 적을 때: 앞의 몇 개만 적고 나머지는 수로
        static string Some(List<string> items, int max = 4)
        {
            if (items.Count <= max) return string.Join(", ", items);
            return string.Join(", ", items.GetRange(0, max)) + " 외 " + (items.Count - max) + "개";
        }

        // p에 가장 가까운 내비메시 점. 1m 안에 있고 같은 층(높이 차 0.3m)·수평 0.6m 안일 때만 "그 자리가 메시 위"로 친다
        static bool Snap(Vector3 p, out Vector3 on, out string why)
        {
            on = p;
            why = null;
            if (!NavMesh.SamplePosition(p, out var hit, SnapRadius, NavMesh.AllAreas))
            {
                why = NavMesh.SamplePosition(p, out var far, 4f, NavMesh.AllAreas)
                    ? "1m 안에 내비메시가 없다. 가장 가까운 메시는 " + V(far.position) + ", " + F(Vector3.Distance(far.position, p)) + "m 떨어짐"
                    : "4m 안에 내비메시가 없다";
                why += Blockers(p);
                return false;
            }
            on = hit.position;
            float dy = Mathf.Abs(hit.position.y - p.y), dxz = Flat(hit.position, p);
            if (dy > SnapY || dxz > SnapXZ)
            {
                why = "가장 가까운 메시 " + V(hit.position) + "가 수평 " + F(dxz) + "m · 높이 " + F(dy) + "m 떨어져 있다 (허용 " + F(SnapXZ) + " · " + F(SnapY) + ")" + Blockers(p);
                return false;
            }
            return true;
        }

        // 그 자리에 서 있을 몸통과 겹치는 콜라이더 이름 (왜 메시가 없는지 짐작하게)
        static string Blockers(Vector3 p)
        {
            var names = new List<string>();
            foreach (var c in Physics.OverlapCapsule(p + Vector3.up * 0.6f, p + Vector3.up * 1.4f, 0.3f, AllLayers, QueryTriggerInteraction.Ignore))
                if (!Under(c.transform, player)) names.Add(PathOf(c.transform));
            names.Sort(string.CompareOrdinal);
            return names.Count == 0 ? "" : " · 그 자리를 막는 콜라이더: " + Some(names, 3);
        }

        // a에서 b까지의 내비메시 경로. 길이와 끝난 자리도 돌려준다 (끊기면 끝난 자리가 끊긴 곳이다)
        static NavMeshPathStatus PathBetween(Vector3 a, Vector3 b, out float length, out Vector3 end, List<Vector3> cornersOut = null)
        {
            length = 0f;
            end = a;
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(a, b, NavMesh.AllAreas, path)) return NavMeshPathStatus.PathInvalid;
            var c = path.corners;
            for (int i = 1; i < c.Length; i++) length += Vector3.Distance(c[i - 1], c[i]);
            if (c.Length > 0) end = c[c.Length - 1];
            if (cornersOut != null) cornersOut.AddRange(c);
            // 끝까지 갔다고 해도 끝점이 목표에서 멀면 믿지 않는다
            if (path.status == NavMeshPathStatus.PathComplete && Vector3.Distance(end, b) > 0.5f) return NavMeshPathStatus.PathPartial;
            return path.status;
        }

        static bool Complete(Vector3 a, Vector3 b) => PathBetween(a, b, out _, out _) == NavMeshPathStatus.PathComplete;

        static string Broken(NavMeshPathStatus st, Vector3 end) =>
            st == NavMeshPathStatus.PathPartial ? "경로가 " + V(end) + "에서 끊긴다" : "경로를 만들지 못했다";

        // RampFoot에서 p까지 끝까지 이어지는가. 아니면 왜인지 FAIL 한 줄
        static bool ReachFromRamp(string id, string name, Vector3 p)
        {
            if (!Snap(p, out var on, out string why))
            {
                Fail(id, name + " " + V(p) + ": 내비메시 위가 아니다 (" + why + ")");
                return false;
            }
            var st = PathBetween(rampOn, on, out _, out var end);
            if (st == NavMeshPathStatus.PathComplete) return true;
            Fail(id, name + " " + V(p) + ": 경사로 발치에서 닿지 않는다 (" + Broken(st, end) + ")");
            return false;
        }

        static bool NeedNav(string id)
        {
            if (navOk) return true;
            Fail(id, "내비메시가 없어 잴 수 없다 (B5.mesh 참고)");
            return false;
        }

        static bool NeedRamp(string id)
        {
            if (!NeedNav(id)) return false;
            if (rampOk) return true;
            Fail(id, "경사로 발치가 내비메시 위가 아니라 잴 수 없다 (B5.mesh 참고)");
            return false;
        }

        // ================= B0: 씬이 열려 있는가 =================

        static bool OpenScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Fail("B0", "플레이 중에는 점검할 수 없다. 플레이를 멈추고 다시 실행한다");
                return false;
            }
            scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.name != Destinations.Apt102.Scene)
            {
                Fail("B0", "열려 있는 씬이 '" + scene.name + "'이다. " + ScenePath + "를 열고 다시 실행한다");
                return false;
            }
            int loaded = 0;
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isLoaded) loaded++;
            if (scene.path != ScenePath) Warn("B0", "씬 경로가 " + scene.path + "이다 (기대 " + ScenePath + ")");
            if (loaded > 1) Warn("B0", "씬 " + loaded + "개가 함께 열려 있다. 다른 씬의 콜라이더·내비메시가 결과에 섞일 수 있다");
            if (scene.isDirty) Warn("B0", "씬에 저장하지 않은 변경이 있다. 이 결과는 디스크의 파일이 아니라 지금 메모리의 씬을 잰 것이다");
            Pass("B0", scene.path + " 편집 모드 점검 (목적지 표: " + Destinations.Apt102.UnlockTurn + "일차에 열림, 늦게 서는 지킴이는 " + Destinations.Apt102.LateTierTurn + "일차부터)");
            return true;
        }

        // ================= A: 생성 전후 지킴 검사 결과 =================

        static void GuardResult()
        {
            int diffs = Apt102GuardDiffs;
            if (diffs < 0) Warn("A.guard", "이 에디터 세션에서 씬을 생성한 적이 없어 지킴 검사 결과가 없다 (Apt102GuardDiffs = -1)");
            else if (diffs == 0) Pass("A.guard", "생성 전후 지킴 검사: 달라진 파일 0개");
            else Fail("A.guard", "생성 전후 지킴 검사: 달라진 파일 " + diffs + "개 (기존 재질·밸런스·다른 씬과 그 메시·설정 가운데 무언가가 바뀌었다. 생성 로그의 오류 줄을 본다)");
        }

        // ================= B1: 씬 약속 =================

        static void Conventions()
        {
            root = RootNamed("Apt102");
            geo = Child(root, "Geometry");
            points = Child(root, "SpawnPoints");
            dyn = Child(root, "Dynamic");
            site = Child(geo, "Site");
            tower = Child(geo, "Tower");
            facade = Child(tower, "Apartment_102");
            var players = InScene<PlayerController>();
            if (players.Count > 0) player = players[0].transform;

            // --- 이름 약속 ---
            var lost = new List<string>();
            if (root == null) lost.Add("Apt102 (씬 루트)");
            if (geo == null) lost.Add("Apt102/Geometry");
            if (points == null) lost.Add("Apt102/SpawnPoints");
            if (dyn == null) lost.Add("Apt102/Dynamic");
            if (site == null) lost.Add("Geometry/Site");
            if (tower == null) lost.Add("Geometry/Tower");
            if (facade == null) lost.Add("Tower/Apartment_102");
            foreach (string n in new[] { "ShelterSign", "RelayCabinet", "RoofHeap", "PadWall", "Tank", "B1", "B2", "A3", "A4", "B5", "B6", "B7", "R1" })
            {
                int c = Named(geo, n, facade).Count;
                if (c != 1) lost.Add(n + " " + c + "개 (기대 1)");
            }
            int guards = Named(geo, "LedgeGuard", facade).Count, invisible = Named(geo, "Invisible", facade).Count;
            if (guards == 0) lost.Add("LedgeGuard 0개 (옥상 가장자리 막이)");
            if (invisible == 0) lost.Add("Invisible 0개 (마당 경계 막이)");
            if (lost.Count > 0) Fail("B1.names", "이름 약속에 맞는 물체가 없거나 수가 다르다: " + Some(lost, 12));
            else Pass("B1.names", "Apt102 / Geometry (Site, Tower/Apartment_102) / SpawnPoints / Dynamic, 표지판·중계기함·계단 8개·옥상 구조물 이름 확인 (LedgeGuard " + guards + ", Invisible " + invisible + ")");
            if (guards > 0 && guards != 5) Warn("B1.names", "LedgeGuard가 " + guards + "개다 (설계 5개). 막힘 여부는 B12가 잰다");
            if (invisible > 0 && invisible != 4) Warn("B1.names", "Invisible이 " + invisible + "개다 (설계 4개)");
            if (tower != null && (tower.position.sqrMagnitude > 1e-6f || Quaternion.Angle(tower.rotation, Quaternion.identity) > 0.01f || (tower.lossyScale - Vector3.one).sqrMagnitude > 1e-6f))
                Warn("B1.names", "Tower가 원점·회전 0·크기 1이 아니다 (위치 " + V(tower.position) + "). 이 점검은 설계 좌표를 월드 좌표로 쓰므로 결과가 어긋날 수 있다");
            foreach (var f in Flights)
            {
                var g = Named(geo, f.name, facade);
                if (g.Count == 1 && Vector3.Distance(g[0].position, f.foot) > 0.05f)
                    Warn("B1.names", "계단 " + f.name + " 그룹의 원점이 " + V(g[0].position) + "이다 (설계의 발치 " + V(f.foot) + "). 실제 경사면은 B10이 잰다");
            }

            // --- 하나씩만 있어야 하는 것, 없어야 하는 것 ---
            var bad = new List<string>();
            void Count<T>(string label, int want) where T : Component
            {
                int n = InScene<T>().Count;
                if (n != want) bad.Add(label + " " + n + "개 (기대 " + want + ")");
            }
            Count<CityDirector>("CityDirector", 1);
            Count<ExpeditionExit>("ExpeditionExit", 1);
            Count<LootCache>("LootCache", 1);
            Count<FallWatch>("FallWatch", 1);
            Count<PlayerController>("PlayerController", 1);
            Count<CityExit>("CityExit", 0);
            Count<Door>("Door", 0);
            Count<LockedDoor>("LockedDoor", 0);
            Count<TruckSite>("TruckSite", 0);
            Count<Trader>("Trader", 0);
            Count<Weather>("Weather", 0);
            if (bad.Count > 0) Fail("B1.count", Some(bad, 12));
            else Pass("B1.count", "CityDirector·ExpeditionExit·LootCache·FallWatch·PlayerController 하나씩, CityExit·Door·LockedDoor·TruckSite·Trader·Weather 없음");
        }

        static void DirectorCheck()
        {
            var list = InScene<CityDirector>();
            if (list.Count == 0) { Fail("B1.director", "CityDirector가 없어 값을 볼 수 없다"); return; }
            var d = list[0];
            var bad = new List<string>();
            void Same(string label, float got, float want) { if (Mathf.Abs(got - want) > 0.001f) bad.Add(label + " " + F(got) + " (기대 " + F(want) + ")"); }
            Same("normalWeight", d.normalWeight, WantNormal);
            Same("leatherWeight", d.leatherWeight, WantLeather);
            Same("rangedWeight", d.rangedWeight, WantRanged);
            Same("heavyWeight", d.heavyWeight, WantHeavy);
            Same("upstairsFill", d.upstairsFill, WantFill);
            Same("safeRadius", d.safeRadius, WantSafeRadius);
            Same("startZombies", d.startZombies, WantStart);
            Same("maxZombies", d.maxZombies, WantMax);
            Same("dogCount", d.dogCount, WantDogs);
            if (d.weatherMode != CityDirector.WeatherMode.None) bad.Add("weatherMode " + d.weatherMode + " (기대 None)");
            if (d.zombiePrefab == null) bad.Add("zombiePrefab 비어 있음");
            if (d.dogPrefab == null) bad.Add("dogPrefab 비어 있음 (들개 자리 D1·D2가 비게 된다)");
            if (d.exitPoint == null) bad.Add("exitPoint 비어 있음 (나침반이 가리킬 곳이 없다)");
            else if (Flat(d.exitPoint.position, SignSpot) > 1f) bad.Add("exitPoint가 " + V(d.exitPoint.position) + "에 있다 (표지판 자리 " + V(SignSpot) + ")");
            if (d.transform.parent != null) bad.Add("CityDirector가 씬 루트가 아니라 " + PathOf(d.transform.parent) + " 아래에 있다");
            if (bad.Count > 0) Fail("B1.director", "CityDirector: " + Some(bad, 12));
            else Pass("B1.director", "가중치 60/30/10/0, upstairsFill 0, weatherMode None, safeRadius 18, startZombies 2, maxZombies 5, dogCount 0, exitPoint = " + PathOf(d.exitPoint));
        }

        static void CacheCheck()
        {
            var list = InScene<LootCache>();
            if (list.Count == 0) { Fail("B1.cache", "LootCache가 없다"); return; }
            var c = list[0];
            var bad = new List<string>();
            if (c.name != "Body" || c.transform.parent == null || c.transform.parent.name != "RelayCabinet") bad.Add("LootCache가 RelayCabinet/Body가 아니라 " + PathOf(c.transform) + "에 붙어 있다");
            if (c.destinationId != Destinations.Apt102Id) bad.Add("destinationId '" + c.destinationId + "' (기대 '" + Destinations.Apt102Id + "')");
            if (c.door == null) bad.Add("door 비어 있음");
            else
            {
                // 문짝은 코드 그대로 남아 경첩과 함께 돌아야 한다. 몸통을 Tripo 모델로 바꾸면서 문짝 겉모습까지 지우거나 모델을 경첩 아래에 두면 여기서 걸린다
                if (!HasLook(c.door)) bad.Add("door (" + PathOf(c.door) + ") 아래에 보이는 문짝이 없다 (열어도 움직이는 것이 없다)");
                foreach (var h in tripoHolders)
                    if (h.IsChildOf(c.door)) { bad.Add("Tripo 받침 " + PathOf(h) + "이 door 아래에 있다 (문을 열면 모델째 돈다)"); break; }
            }
            if (c.led == null) bad.Add("led 비어 있음");
            if (c.ledBulb == null) bad.Add("ledBulb 비어 있음");
            if (c.dropPoints == null || c.dropPoints.Length != 3) bad.Add("dropPoints " + (c.dropPoints == null ? 0 : c.dropPoints.Length) + "개 (기대 3)");
            if (c.dropPoints != null)
                for (int i = 0; i < c.dropPoints.Length; i++)
                {
                    var dp = c.dropPoints[i];
                    if (dp == null) { bad.Add("dropPoints[" + i + "] 비어 있음"); continue; }
                    // 보상은 이 자리에서 바닥으로 붙는다 (PickupItem.Spawn). 구멍 위거나 떠 있으면 아래층으로 떨어진다
                    if (!Physics.Raycast(dp.position + Vector3.up * 0.2f, Vector3.down, out var hit, 1f, AllLayers, QueryTriggerInteraction.Ignore))
                        bad.Add("dropPoints[" + i + "] " + V(dp.position) + " 아래 0.8m 안에 바닥이 없다 (보상이 구멍으로 떨어진다)");
                    else if (hit.collider.name != "Rubble_Roof")
                        bad.Add("dropPoints[" + i + "] " + V(dp.position) + " 아래가 옥상 바닥이 아니라 " + PathOf(hit.collider.transform) + "이다");
                }
            var col = c.GetComponentInChildren<Collider>(true);
            if (col == null) bad.Add("콜라이더가 없어 조준할 수 없다");
            else if (AimSpot(ArenaPoints[2].p, out var stand))
            {
                string blocked = AimBlocked(stand, col, c);
                if (blocked != null) bad.Add("중계기함 앞 " + V(stand) + "에 서서 조준할 수 없다: " + blocked);
            }
            if (bad.Count > 0) Fail("B1.cache", "LootCache: " + Some(bad, 12));
            else Pass("B1.cache", "RelayCabinet/Body에 LootCache, 떨어질 자리 셋 모두 옥상 바닥 위, 문짝·불빛·전구 연결됨, 앞에 서서 조준 가능");
        }

        static void ExitCheck()
        {
            var list = InScene<ExpeditionExit>();
            if (list.Count == 0) { Fail("B1.exit", "ExpeditionExit가 없다 (돌아갈 길이 없다)"); return; }
            var e = list[0];
            var bad = new List<string>();
            if (e.name != "Board" || e.transform.parent == null || e.transform.parent.name != "ShelterSign") bad.Add("ExpeditionExit가 ShelterSign/Board가 아니라 " + PathOf(e.transform) + "에 붙어 있다");
            if (Flat(e.transform.position, SignSpot) > 1f) bad.Add("표지판이 " + V(e.transform.position) + "에 있다 (설계 " + V(SignSpot) + ")");
            var directors = InScene<CityDirector>();
            if (directors.Count > 0 && directors[0].exitPoint != null && directors[0].exitPoint != e.transform && !directors[0].exitPoint.IsChildOf(e.transform) && !e.transform.IsChildOf(directors[0].exitPoint))
                bad.Add("CityDirector.exitPoint (" + PathOf(directors[0].exitPoint) + ")가 표지판이 아니다");
            var col = e.GetComponentInChildren<Collider>(true);
            if (col == null) bad.Add("콜라이더가 없어 조준할 수 없다");
            else if (AimSpot(ReachTargets[21].p, out var stand))
            {
                string blocked = AimBlocked(stand, col, e);
                if (blocked != null) bad.Add("표지판 앞 " + V(stand) + "에 서서 조준할 수 없다: " + blocked);
            }
            if (bad.Count > 0) Fail("B1.exit", "ExpeditionExit: " + Some(bad, 12));
            else Pass("B1.exit", "ShelterSign/Board에 ExpeditionExit " + V(e.transform.position) + ", 앞에 서서 조준 가능");
        }

        // 조준 시험을 할 자리 (내비메시 위). 내비메시를 아직 확인하기 전이라 여기서 직접 잰다
        static bool AimSpot(Vector3 p, out Vector3 stand) => Snap(p, out stand, out _);

        // stand에 선 플레이어가 col을 조준할 수 있는가 (PlayerInteractor와 같은 방식). 되면 null, 아니면 까닭
        static string AimBlocked(Vector3 stand, Collider col, Interactable want)
        {
            var eye = new Vector3(stand.x, stand.y + EyeHeight, stand.z);
            var to = col.bounds.center - eye;
            if (to.magnitude > Reach + col.bounds.extents.magnitude) return "너무 멀다 (" + F(to.magnitude) + "m)";
            if (!Physics.Raycast(eye, to.normalized, out var hit, Reach, AllLayers, QueryTriggerInteraction.Collide)) return "조준선이 " + F(Reach) + "m 안에서 아무것도 맞지 않는다";
            var got = hit.collider.GetComponentInParent<Interactable>();
            return got == want ? null : "조준선이 먼저 " + PathOf(hit.collider.transform) + "에 맞는다";
        }

        static void PlayerCheck()
        {
            if (player == null) { Fail("B1.player", "플레이어가 없다"); return; }
            var bad = new List<string>();
            if (player.parent != null) bad.Add("플레이어가 씬 루트가 아니라 " + PathOf(player.parent) + " 아래에 있다");
            if (Vector3.Distance(player.position, PlayerSpot) > 0.5f) bad.Add("출발 자리가 " + V(player.position) + "이다 (설계 " + V(PlayerSpot) + ")");
            // 서 있는 몸통(반지름 0.35, 키 1.8)이 무언가와 겹치면 시작하자마자 끼인다
            var names = new List<string>();
            foreach (var c in Physics.OverlapCapsule(player.position + Vector3.up * 0.45f, player.position + Vector3.up * 1.45f, 0.35f, AllLayers, QueryTriggerInteraction.Ignore))
                if (!c.transform.IsChildOf(player)) names.Add(PathOf(c.transform));
            if (names.Count > 0) bad.Add("출발 자리의 몸통이 콜라이더와 겹친다: " + Some(names, 3));
            if (!Physics.Raycast(player.position + Vector3.up * 0.5f, Vector3.down, out var hit, 1.5f, AllLayers, QueryTriggerInteraction.Ignore)) bad.Add("출발 자리 아래 1m 안에 바닥이 없다");
            else if (hit.collider.name != "Ground") bad.Add("출발 자리의 바닥이 Ground가 아니라 " + PathOf(hit.collider.transform) + "이다");
            if (bad.Count > 0) Fail("B1.player", Some(bad, 6));
            else Pass("B1.player", "플레이어가 씬 루트의 " + V(player.position) + ", yaw " + F(player.eulerAngles.y) + ", 마당 바닥 위에 서 있다");

            var roots = new List<string>();
            bool moon = false, volume = false;
            foreach (var go in scene.GetRootGameObjects())
            {
                var l = go.GetComponent<Light>();
                if (go.name == "Moonlight" && l != null && l.type == LightType.Directional) moon = true;
                var v = go.GetComponent<Volume>();
                if (v != null && v.sharedProfile != null) volume = true;
            }
            if (!moon) roots.Add("씬 루트에 방향광 Moonlight가 없다");
            if (!volume) roots.Add("씬 루트에 프로필이 걸린 Volume이 없다");
            if (roots.Count > 0) Warn("B1.player", Some(roots, 4));
        }

        static void ParentCheck()
        {
            var bad = new List<string>();
            int colliders = 0;
            var outside = new List<string>();
            var triggers = new List<string>();
            foreach (var c in InScene<Collider>())
            {
                if (Under(c.transform, player)) continue;
                colliders++;
                if (c.isTrigger) triggers.Add(PathOf(c.transform));
                if (!Under(c.transform, geo)) outside.Add(PathOf(c.transform));
            }
            outside.Sort(string.CompareOrdinal);
            triggers.Sort(string.CompareOrdinal);
            if (outside.Count > 0) bad.Add("Geometry 밖의 콜라이더 " + outside.Count + "개 (내비메시에 구워지지 않는다): " + Some(outside));
            if (Under(player, geo)) bad.Add("플레이어가 Geometry 아래에 있다 (몸통이 내비메시에 구워진다)");

            int strayPoints = 0, underGeo = 0;
            var examples = new List<string>();
            foreach (var z in InScene<ZombieSpawnPoint>())
            {
                if (Under(z.transform, geo)) underGeo++;
                if (!Under(z.transform, points)) { strayPoints++; examples.Add(z.name); }
            }
            foreach (var it in InScene<ItemSpawnPoint>())
            {
                if (Under(it.transform, geo)) underGeo++;
                if (!Under(it.transform, points)) { strayPoints++; examples.Add(it.name); }
            }
            if (underGeo > 0) bad.Add("Geometry 아래에 있는 자리 " + underGeo + "개");
            if (strayPoints > 0) bad.Add("SpawnPoints 아래가 아닌 자리 " + strayPoints + "개: " + Some(examples));

            int volumes = 0;
            var oddVolumes = new List<string>();
            foreach (var v in InScene<NavMeshModifierVolume>())
            {
                volumes++;
                if (v.name != "NavCut" || v.area != 1 || !Under(v.transform, geo)) oddVolumes.Add(PathOf(v.transform) + " (area " + v.area + ")");
            }
            if (oddVolumes.Count > 0) bad.Add("이름이 NavCut이 아니거나 area 1이 아니거나 Geometry 밖인 내비메시 볼륨: " + Some(oddVolumes));

            var noAim = new List<string>();
            foreach (var it in InScene<Interactable>())
                if (it.GetComponentInChildren<Collider>(true) == null) noAim.Add(PathOf(it.transform));
            if (noAim.Count > 0) bad.Add("콜라이더가 없어 조준할 수 없는 상호작용 물체: " + Some(noAim));

            if (bad.Count > 0) Fail("B1.parents", Some(bad, 8));
            else Pass("B1.parents", "콜라이더 " + colliders + "개 모두 Geometry 아래, 자리는 모두 SpawnPoints 아래, NavCut 볼륨 " + volumes + "개 (area 1), 상호작용 물체마다 콜라이더 있음");
            if (volumes < 13 && oddVolumes.Count == 0) Warn("B1.parents", "NavCut 볼륨이 " + volumes + "개다 (설계는 적어도 13개: 경계 밖 넷, 몸체 꼭대기, 잔해 더미, 경비실 지붕, 303·304·404·504·704호, 옥상 동쪽 길). 잘렸는지는 B7·B8이 잰다");

            if (triggers.Count > 0) Fail("B1.trigger", "트리거 콜라이더 " + triggers.Count + "개 (조준선 상호작용을 가로챈다): " + Some(triggers));
            else Pass("B1.trigger", "트리거 콜라이더 없음");

            var loose = new List<string>();
            foreach (var h in InScene<HazardZone>())
                if (!Under(h.transform, dyn)) loose.Add(PathOf(h.transform));
            foreach (var f in InScene<FallWatch>())
                if (!Under(f.transform, dyn)) loose.Add(PathOf(f.transform));
            if (loose.Count > 0) Warn("B1.parents", "Dynamic 아래가 아닌 위험 구역·FallWatch: " + Some(loose));
        }

        static void SurfaceCheck()
        {
            var surfaces = InScene<NavMeshSurface>();
            if (surfaces.Count != 1) { Fail("B1.surface", "NavMeshSurface가 " + surfaces.Count + "개다 (기대: Geometry에 하나)"); return; }
            var s = surfaces[0];
            var bad = new List<string>();
            if (s.transform != geo) bad.Add("NavMeshSurface가 Geometry가 아니라 " + PathOf(s.transform) + "에 있다");
            if (s.navMeshData == null) bad.Add("navMeshData가 비어 있다");
            else
            {
                string path = AssetDatabase.GetAssetPath(s.navMeshData);
                if (path != NavPath) bad.Add("내비메시 데이터가 " + (string.IsNullOrEmpty(path) ? "에셋으로 저장되지 않았다 (씬을 다시 열면 사라진다)" : path + "에 있다") + " (기대 " + NavPath + ")");
            }
            if (s.collectObjects != CollectObjects.Children) bad.Add("collectObjects " + s.collectObjects + " (기대 Children)");
            if (s.useGeometry != NavMeshCollectGeometry.PhysicsColliders) bad.Add("useGeometry " + s.useGeometry + " (기대 PhysicsColliders)");
            if (s.agentTypeID != 0) bad.Add("agentTypeID " + s.agentTypeID + " (감염자 프리팹은 0번 Humanoid를 쓴다)");
            if (bad.Count > 0) Fail("B1.surface", Some(bad, 6));
            else
            {
                var a = NavMesh.GetSettingsByID(s.agentTypeID);
                Pass("B1.surface", "Geometry의 NavMeshSurface → " + NavPath + " (Children, PhysicsColliders, 반지름 " + F(a.agentRadius) + " 키 " + F(a.agentHeight) + " 턱 " + F(a.agentClimb) + " 경사 " + F(a.agentSlope) + ")");
            }
            if (s.layerMask.value != ~(1 << 2)) Warn("B1.surface", "NavMeshSurface.layerMask가 " + s.layerMask.value + "이다 (설계: Ignore Raycast 레이어만 뺀 " + ~(1 << 2) + ")");
        }

        // ================= B2: 자리 표 =================

        static void PointTables()
        {
            var zs = InScene<ZombieSpawnPoint>();
            zs.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            var byName = new Dictionary<string, List<ZombieSpawnPoint>>();
            foreach (var z in zs)
            {
                if (!byName.TryGetValue(z.name, out var l)) byName[z.name] = l = new List<ZombieSpawnPoint>();
                l.Add(z);
            }

            // --- 반드시 서는 지킴이 15 ---
            int good = 0;
            var known = new HashSet<string>();
            foreach (var post in Posts)
            {
                known.Add(post.name);
                byName.TryGetValue(post.name, out var l);
                if (l == null || l.Count != 1) { Fail("B2.guard", post.name + ": 이 이름의 ZombieSpawnPoint가 " + (l == null ? 0 : l.Count) + "개다 (기대 1)"); continue; }
                var z = l[0];
                var bad = new List<string>();
                if (z.kind != post.kind) bad.Add("kind " + z.kind + " (기대 " + post.kind + ")");
                if (z.tier != post.tier) bad.Add("tier " + z.tier + " (기대 " + post.tier + ")");
                if (!z.upstairs) bad.Add("upstairs 꺼짐 (지킴이로 서지 않고 거리 감염자가 여기서 나온다)");
                if (!z.guaranteed) bad.Add("guaranteed 꺼짐 (서지 않을 수 있다)");
                float off = Vector3.Distance(z.transform.position, post.p);
                // 설계 자리가 애초에 설 수 없는 곳(내비메시 없음)이라 가까운 데로 옮긴 것은 틀린 것으로 치지 않는다
                bool moved = off > 0.5f && off <= 4f && !Snap(post.p, out _, out _) && Snap(z.transform.position, out _, out _);
                if (off > 0.5f && !moved) bad.Add("자리 " + V(z.transform.position) + " (설계 " + V(post.p) + ", " + F(off) + "m 벗어남)");
                if (bad.Count > 0) { Fail("B2.guard", post.name + ": " + Some(bad, 6)); continue; }
                good++;
                if (moved) Warn("B2.guard", post.name + ": 설계 자리 " + V(post.p) + "에는 내비메시가 없어 " + V(z.transform.position) + "로 " + F(off) + "m 옮겨져 있다" + Blockers(post.p));
                else if (off > 0.05f) Warn("B2.guard", post.name + ": 자리가 설계에서 " + F(off) + "m 옮겨졌다 " + V(z.transform.position) + " (설계 " + V(post.p) + ")");
                float turn = Mathf.Abs(Mathf.DeltaAngle(z.transform.eulerAngles.y, post.yaw));
                if (turn > 2f) Warn("B2.guard", post.name + ": 보는 쪽 yaw " + F(z.transform.eulerAngles.y) + " (설계 " + F(post.yaw) + ")");
            }
            if (good == Posts.Length) Pass("B2.guard", "지킴이 15곳 (G1 G2 G3a G3b G4 G5a G5b G5c G6 G7 G8 G8b G9 D1 D2) 종류·tier·자리·upstairs·guaranteed가 표와 같다");

            // --- 수 ---
            int guaranteed = 0, tier0 = 0, tier1 = 0, dogs = 0, roam = 0;
            var odd = new List<string>();
            foreach (var z in zs)
            {
                if (z.guaranteed)
                {
                    guaranteed++;
                    if (z.tier == 0) tier0++; else tier1++;
                    if (z.kind == SpawnKind.Dog) dogs++;
                    if (!known.Contains(z.name)) odd.Add(z.name + " (표에 없는 지킴이)");
                }
                else if (z.upstairs) odd.Add(z.name + " (upstairs인데 guaranteed가 아니다)");
                else
                {
                    roam++;
                    if (z.kind != SpawnKind.Any || z.tier != 0) odd.Add(z.name + " (배회 자리인데 kind " + z.kind + ", tier " + z.tier + ")");
                    if (!z.name.StartsWith("Roam")) odd.Add(z.name + " (배회 자리 이름은 Roam1~Roam6)");
                }
            }
            if (guaranteed != 15 || tier0 != 12 || tier1 != 3 || dogs != 2 || roam != 6 || odd.Count > 0)
                Fail("B2.count", "지킴이 " + guaranteed + " (기대 15: tier 0 " + tier0 + "/12, 그 가운데 들개 " + dogs + "/2, tier 1 " + tier1 + "/3), 배회 자리 " + roam + " (기대 6)" + (odd.Count > 0 ? " · " + Some(odd, 6) : ""));
            else Pass("B2.count", "ZombieSpawnPoint " + zs.Count + "개: 지킴이 15 (tier 0 12곳 가운데 들개 2, tier 1 3곳), 배회 자리 6");

            // --- 무거운 감염자는 옥상에만 ---
            var low = new List<string>();
            foreach (var z in zs)
                if (z.kind == SpawnKind.Heavy && z.transform.position.y < HeavyMinY) low.Add(z.name + " " + V(z.transform.position));
            if (low.Count > 0) Fail("B2.heavy", "y " + F(HeavyMinY) + " 아래의 Heavy 자리 (천장 2.6m에 걸린다): " + Some(low));
            else Pass("B2.heavy", "Heavy 자리는 옥상에만 있다");

            // --- 늦게 서는 지킴이의 날 ---
            var dest = Destinations.Apt102;
            if (tier1 > 0 && dest.LateTierTurn <= 0) Fail("B2.tier", "tier 1 자리가 " + tier1 + "곳인데 Destinations.Apt102.LateTierTurn이 " + dest.LateTierTurn + "이다 (처음부터 선다)");
            else if (dest.LateTierTurn <= dest.UnlockTurn) Warn("B2.tier", "LateTierTurn " + dest.LateTierTurn + "일이 UnlockTurn " + dest.UnlockTurn + "일보다 늦지 않다 (tier 1이 첫 방문부터 선다)");
            else Pass("B2.tier", "tier 1 지킴이 " + tier1 + "곳은 " + dest.LateTierTurn + "일차부터 선다 (목적지는 " + dest.UnlockTurn + "일차에 열린다)");

            // --- 배회 자리 여섯 ---
            var missing = new List<string>();
            foreach (var want in RoamPoints)
            {
                bool found = false;
                foreach (var z in zs)
                    if (!z.upstairs && Vector3.Distance(z.transform.position, want.p) <= 0.5f) { found = true; break; }
                if (!found) missing.Add(V(want.p));
            }
            if (missing.Count > 0) Fail("B2.roam", "설계의 배회 자리에 ZombieSpawnPoint가 없다: " + Some(missing, 6));
            else Pass("B2.roam", "배회 자리 6곳이 설계 좌표에 있다");
        }

        static void ItemTables()
        {
            var items = InScene<ItemSpawnPoint>();
            items.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            int random = 0, fixedCount = 0, outdoor = 0;
            var odd = new List<string>();
            var unknown = new List<string>();
            foreach (var it in items)
            {
                string want;
                if (it.outdoor) { outdoor++; want = "ItemOut_"; }
                else if (it.guaranteed) { fixedCount++; want = "ItemFixed_"; }
                else { random++; want = "Item_"; }
                if (!it.name.StartsWith(want)) odd.Add(it.name + " (이름이 " + want + "…여야 한다)");
                if (it.guaranteed && (it.items == null || it.items.Length == 0)) odd.Add(it.name + " (반드시 놓이는 자리인데 items가 비었다)");
                if (it.items != null)
                    foreach (string id in it.items)
                        if (!ItemDatabase.Items.ContainsKey(id ?? "")) unknown.Add(it.name + ": '" + id + "'");
            }
            bool byTable = random == RandomByTable && fixedCount == FixedByTable, byText = random == RandomByText && fixedCount == FixedByText;
            if ((!byTable && !byText) || outdoor != OutdoorItems || odd.Count > 0)
                Fail("B2.items", "아이템 자리: 실내 무작위 " + random + ", 실내 고정 " + fixedCount + ", 바깥 " + outdoor + " (기대: 생성 작업서 표 25 + 6 또는 점검 문서 22 + 7, 바깥 5)" + (odd.Count > 0 ? " · " + Some(odd, 6) : ""));
            else Pass("B2.items", "아이템 자리: 실내 무작위 " + random + ", 실내 고정 " + fixedCount + ", 바깥 " + outdoor + (byTable ? " (생성 작업서 8절의 표와 같다. 점검 문서의 22 + 7과는 다르다)" : " (점검 문서의 22 + 7과 같다. 생성 작업서 표는 25 + 6)"));

            // 보상 표의 id도 같이 본다
            if (Destinations.Apt102.Rewards != null)
                foreach (var row in Destinations.Apt102.Rewards)
                    if (row != null)
                        foreach (var r in row)
                            if (r == null || !ItemDatabase.Items.ContainsKey(r.id ?? "")) unknown.Add("Destinations.Apt102.Rewards: '" + (r == null ? null : r.id) + "'");
            if (unknown.Count > 0) Fail("B2.ids", "ItemDatabase.Items에 없는 아이템 id: " + Some(unknown, 8));
            else Pass("B2.ids", "자리와 보상 표의 아이템 id가 모두 ItemDatabase.Items에 있다");

            // 표의 자리마다 씬에 자리가 있는가 (좌표·고정 품목). 수치가 임시라 어긋나도 경고만 한다
            var notes = new List<string>();
            var used = new HashSet<ItemSpawnPoint>();
            foreach (var want in LootTable)
            {
                ItemSpawnPoint best = null;
                float bestD = 0.35f;
                foreach (var it in items)
                {
                    if (it.outdoor || used.Contains(it)) continue;
                    float d = Vector3.Distance(it.transform.position, want.p);
                    if (d <= bestD) { bestD = d; best = it; }
                }
                if (best == null) { notes.Add(want.room + "호 " + V(want.p) + "에 자리가 없다"); continue; }
                used.Add(best);
                if (want.fixedId == null)
                {
                    if (best.guaranteed) notes.Add(best.name + ": 표에서는 무작위 자리인데 guaranteed다");
                }
                else if (!best.guaranteed || best.items == null || best.items.Length != 1 || best.items[0] != want.fixedId || best.amount != want.amount)
                    notes.Add(best.name + ": 표는 " + want.fixedId + " ×" + want.amount + " 고정인데 guaranteed " + best.guaranteed + ", items [" + (best.items == null ? "" : string.Join(",", best.items)) + "], amount " + best.amount);
            }
            foreach (var it in items)
                if (!it.outdoor && !used.Contains(it)) notes.Add(it.name + " " + V(it.transform.position) + ": 표에 없는 자리");
            if (notes.Count > 0) Warn("B2.itemtable", "생성 작업서 8절의 표와 다른 실내 아이템 자리 " + notes.Count + "곳: " + Some(notes, 6));
            else Pass("B2.itemtable", "실내 아이템 자리 31곳의 좌표와 고정 품목 (배터리·붕대·통조림·구리선)이 표와 같다");
        }

        static void HazardTable()
        {
            var zones = InScene<HazardZone>();
            int good = 0;
            foreach (var want in Zones)
            {
                HazardZone z = null;
                int n = 0;
                foreach (var h in zones)
                    if (h.name == want.name) { z = h; n++; }
                if (n != 1) { Fail("B2.hazard", want.name + ": 이 이름의 HazardZone이 " + n + "개다 (기대 1)"); continue; }
                var bad = new List<string>();
                if (z.kind != want.kind) bad.Add("kind " + z.kind + " (기대 " + want.kind + ")");
                float off = Vector3.Distance(z.transform.position, want.p);
                if (off > 0.3f) bad.Add("자리 " + V(z.transform.position) + " (설계 " + V(want.p) + ", " + F(off) + "m 벗어남)");
                if (want.kind == HazardZone.Kind.Hint)
                {
                    if (z.hintBit == 0) bad.Add("hintBit 0 (한 번만 뜨는 힌트로 기억되지 않는다)");
                    if (string.IsNullOrEmpty(z.hintText)) bad.Add("hintText 비어 있음");
                }
                if (z.GetComponentInChildren<Collider>(true) != null) bad.Add("콜라이더가 붙어 있다 (위험 구역은 콜라이더 없이 자리만 본다)");
                if (bad.Count > 0) { Fail("B2.hazard", want.name + ": " + Some(bad, 6)); continue; }
                good++;
                if ((z.halfSize - want.half).magnitude > 0.05f) Warn("B2.hazard", want.name + ": halfSize " + V(z.halfSize) + " (설계 " + V(want.half) + ")");
                if (want.kind == HazardZone.Kind.Hint && z.hintBit != want.bit) Warn("B2.hazard", want.name + ": hintBit " + z.hintBit + " (설계 " + want.bit + ")");
                if (Quaternion.Angle(z.transform.rotation, Quaternion.identity) > 0.5f || (z.transform.lossyScale - Vector3.one).magnitude > 0.01f) Warn("B2.hazard", want.name + ": 회전이나 크기가 기본값이 아니다 (halfSize가 그만큼 틀어진다)");
            }
            if (zones.Count != Zones.Length) Fail("B2.hazard", "HazardZone이 " + zones.Count + "개다 (기대 7: Fire_F1 Fire_F2 Glass_N1 Glass_N2 Hint_Echo Hint_Fall_Roof Hint_Fall_504)");
            else if (good == Zones.Length) Pass("B2.hazard", "위험 구역 7곳의 이름·종류·자리가 설계와 같다");
        }

        // ================= B3: 예산 =================

        static void Budgets()
        {
            // 한도는 코드로 지은 메시에 건다. Tripo 품질별 모델의 판 메시는 모델 폴더의 에셋이라 이 씬의 메시 묶음에 들지 않고,
            // 판 넷 가운데 하나만 그려지므로 넷을 다 더하면 한도의 뜻(이 씬이 새로 만든 메시의 크기)과 어긋난다. 그쪽은 B3.tripo가 따로 센다
            var meshes = new HashSet<Mesh>();
            long instanced = 0;
            int tripoFilters = 0;
            foreach (var mf in InScene<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                if (InTripoModel(mf.transform)) { tripoFilters++; continue; }
                meshes.Add(mf.sharedMesh);
                instanced += mf.sharedMesh.vertexCount;
            }
            long unique = 0;
            foreach (var m in meshes) unique += m.vertexCount;
            // 메시 묶음 파일에 든 메시 (씬이 쓰지 않는 것까지 센다. 파일 크기는 이쪽을 따른다)
            int stored = 0;
            long storedVertices = 0;
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(MeshPath))
                if (o is Mesh m) { stored++; storedVertices += m.vertexCount; }
            string text = "씬이 쓰는 서로 다른 메시 " + meshes.Count + "개의 정점 합 " + unique + ", 메시 묶음 파일의 메시 " + stored + "개의 정점 합 " + storedVertices + " (한도 " + MaxVertices + " 미만) · 놓인 물체 기준 합 " + instanced;
            if (tripoFilters > 0) text += " · Tripo 품질별 모델의 판 " + tripoFilters + "개는 빼고 셌다 (B3.tripo)";
            if (unique >= MaxVertices || storedVertices >= MaxVertices) Fail("B3.vertices", text);
            else Pass("B3.vertices", text);

            int lights = 0, point = 0, shadowPoint = 0, onPlayer = 0;
            foreach (var l in InScene<Light>())
            {
                lights++;
                if (Under(l.transform, player)) onPlayer++;
                if (l.type == LightType.Point)
                {
                    point++;
                    if (l.shadows != LightShadows.None) shadowPoint++;
                }
            }
            if (lights > MaxLights) Fail("B3.lights", "빛 " + lights + "개 (한도 " + MaxLights + "): 점광 " + point + ", 플레이어 손전등 " + onPlayer);
            else Pass("B3.lights", "빛 " + lights + "개 (한도 " + MaxLights + "): 점광 " + point + " (그림자 켠 것 " + shadowPoint + "), 플레이어 손전등 " + onPlayer);
            if (shadowPoint > MaxShadowPointLights) Warn("B3.lights", "그림자를 드리우는 점광이 " + shadowPoint + "개다 (설계 " + MaxShadowPointLights + "개: 부엌 불과 모닥불)");

            int objects = 0, active = 0;
            foreach (var t in InScene<Transform>())
            {
                objects++;
                if (t.gameObject.activeInHierarchy) active++;
            }
            Pass("B3.objects", "오브젝트 " + objects + "개 (켜진 것 " + active + "), 렌더러 " + InScene<Renderer>().Count + ", 콜라이더 " + InScene<Collider>().Count + ", 파티클 " + InScene<ParticleSystem>().Count + " (설계 어림 2,000개)"
                + (tripoHolders.Count > 0 ? " · 그 가운데 Tripo 받침·모델·판 " + (tripoHolders.Count + tripoParts.Count) + "개" : ""));
        }

        // Tripo 품질별 모델의 정점 (코드로 지은 메시의 한도와 따로. 어림 한도를 넘으면 경고)
        static void TripoBudget()
        {
            var t = TripoCount();
            if (t.models == 0)
            {
                Pass("B3.tripo", "Tripo 품질별 모델 없음 (코드로 지은 소품만 있는 씬)");
                return;
            }
            string text = "Tripo 품질별 모델 " + t.models + "개 (" + t.kinds + "가지), 서로 다른 판 메시 " + t.meshes + "개의 정점 합 " + t.meshVertices + " (어림 한도 " + MaxTripoMeshVertices + " 미만: 품질 설정과 상관없이 모두 메모리에 올라온다)"
                + " · 놓인 것 기준 합: 낮음 판만 그릴 때 " + t.lowPlaced + " (어림 한도 " + MaxTripoLowVertices + " 미만), 매우 높음 판만 그릴 때 " + t.highPlaced + " (가까운 몇 개만 이 판으로 그려진다)";
            if (t.lowPlaced >= MaxTripoLowVertices || t.meshVertices >= MaxTripoMeshVertices) Warn("B3.tripo", text + " — 어림 한도를 넘었다. 같은 모델을 너무 많이 놓았거나 낮음 판이 무겁다");
            else Pass("B3.tripo", text);
        }

        // ================= B4: 메시가 살아 있는가, 파일 크기 =================

        static void MeshesAndFiles()
        {
            // 씬을 다시 열었을 때 메시가 비는 것을 잡는다. 주 작업 흐름이 씬을 다시 연 뒤에 이 점검을 돌린다
            var empty = new List<string>();
            int filters = 0, meshColliders = 0, embedded = 0, tierMeshes = 0;
            var foreign = new Dictionary<string, int>(); // 다른 씬의 메시 묶음
            var others = new Dictionary<string, int>();  // 그 밖의 메시 에셋
            void Source(Mesh m, bool tripoModel)
            {
                string path = AssetDatabase.GetAssetPath(m);
                if (string.IsNullOrEmpty(path)) { embedded++; return; }
                if (path == MeshPath || !path.StartsWith("Assets/")) return; // 이 씬의 메시 묶음, 또는 유니티 기본 도형
                // Tripo 품질별 모델의 판은 모델 폴더의 메시를 그대로 가리키는 것이 맞다 (씬을 다시 생성해도 사라지지 않는 에셋. 판마다의 폴더는 B15.tripo가 본다).
                // 모델 밖(코드 소품의 MeshFilter·MeshCollider)에서 그 메시를 쓰면 예전처럼 아래에서 경고한다
                if (tripoModel && path.StartsWith(TripoMeshRoot)) { tierMeshes++; return; }
                var into = path.StartsWith("Assets/Generated/") ? foreign : others;
                into.TryGetValue(path, out int n);
                into[path] = n + 1;
            }
            foreach (var mf in InScene<MeshFilter>())
            {
                filters++;
                if (mf.sharedMesh == null) empty.Add("MeshFilter " + PathOf(mf.transform));
                else Source(mf.sharedMesh, InTripoModel(mf.transform));
            }
            foreach (var mc in InScene<MeshCollider>())
            {
                meshColliders++;
                if (mc.sharedMesh == null) empty.Add("MeshCollider " + PathOf(mc.transform));
                else Source(mc.sharedMesh, false);
            }
            empty.Sort(string.CompareOrdinal);
            if (empty.Count > 0) Fail("B4.mesh", "메시가 빈 것 " + empty.Count + "개 (메시 묶음이 저장되지 않았을 수 있다): " + Some(empty, 5));
            else Pass("B4.mesh", "MeshFilter " + filters + "개, MeshCollider " + meshColliders + "개 모두 메시가 있다");

            if (foreign.Count > 0)
            {
                var list = new List<string>();
                foreach (var kv in foreign) list.Add(kv.Key + " (" + kv.Value + "곳)");
                list.Sort(string.CompareOrdinal);
                Fail("B4.source", "다른 씬의 메시 묶음을 가리키는 메시가 있다 (그 씬을 다시 생성하면 사라진다): " + Some(list, 4));
            }
            else Pass("B4.source", "다른 씬의 메시 묶음을 가리키는 메시 없음 (이 씬의 묶음 " + MeshPath + ")" + (tierMeshes > 0 ? " · Tripo 품질별 모델의 판 " + tierMeshes + "곳은 모델 폴더 " + TripoMeshRoot + "의 메시를 그대로 쓴다" : ""));
            if (others.Count > 0)
            {
                var list = new List<string>();
                foreach (var kv in others) list.Add(kv.Key + " (" + kv.Value + "곳)");
                list.Sort(string.CompareOrdinal);
                Warn("B4.source", "이 씬의 묶음도 유니티 기본 도형도 아닌 메시 에셋: " + Some(list, 4));
            }
            if (embedded > 0) Warn("B4.source", "에셋이 아닌 메시 " + embedded + "곳 (씬 파일 안에 저장돼 씬이 커진다)");

            // 재질: 비어 있으면 분홍으로 보이고, 에셋이 아니면 불러오기만 한다는 규칙을 어긴 것
            var noMat = new List<string>();
            int looseMat = 0;
            foreach (var r in InScene<Renderer>())
            {
                // 입자는 첫 재질만 본다 (꼬리 재질 칸은 꼬리를 쓰지 않으면 비어 있다)
                var mats = r is ParticleSystemRenderer ? new[] { r.sharedMaterial } : r.sharedMaterials;
                foreach (var m in mats)
                {
                    if (m == null) noMat.Add(PathOf(r.transform));
                    else if (!EditorUtility.IsPersistent(m)) looseMat++;
                }
            }
            noMat.Sort(string.CompareOrdinal);
            if (noMat.Count > 0) Fail("B4.material", "재질이 빈 렌더러 " + noMat.Count + "개: " + Some(noMat, 5));
            else Pass("B4.material", "렌더러의 재질이 모두 채워져 있다");
            if (looseMat > 0) Warn("B4.material", "에셋이 아닌 재질 " + looseMat + "곳 (이 씬은 기존 재질을 불러오기만 해야 한다)");

            // 파일
            var bad = new List<string>();
            long sceneBytes = Size(ScenePath), meshBytes = Size(MeshPath), navBytes = Size(NavPath);
            if (sceneBytes < 0) bad.Add(ScenePath + " 없음");
            else if (sceneBytes >= MaxFileBytes) bad.Add(ScenePath + " " + Mb(sceneBytes) + " (한도 20MB 미만)");
            if (meshBytes < 0) bad.Add(MeshPath + " 없음");
            else if (meshBytes >= MaxFileBytes) bad.Add(MeshPath + " " + Mb(meshBytes) + " (한도 20MB 미만)");
            else if (meshBytes <= MinMeshBytes) bad.Add(MeshPath + " " + Mb(meshBytes) + " (0.5MB보다 커야 한다. 메시가 저장되지 않았을 수 있다)");
            if (navBytes < 0) bad.Add(NavPath + " 없음");
            if (Size(ProfilePath) < 0) bad.Add(ProfilePath + " 없음");
            if (bad.Count > 0) Fail("B4.files", Some(bad, 6));
            else Pass("B4.files", "씬 " + Mb(sceneBytes) + ", 메시 묶음 " + Mb(meshBytes) + ", 내비메시 " + Mb(navBytes) + ", 프로필 있음");
            if (Size(OcclusionPath) < 0) Warn("B4.files", OcclusionPath + "가 없다 (가림 굽기를 하지 않았다)");
        }

        static long Size(string assetPath)
        {
            string full = Path.Combine(Path.GetDirectoryName(Application.dataPath), assetPath);
            return File.Exists(full) ? new FileInfo(full).Length : -1;
        }

        static string Mb(long bytes) => (bytes / 1048576f).ToString("0.00", Inv) + "MB";

        // ================= B5: 자리마다 내비메시·바닥 =================

        static void NavPrep()
        {
            var tri = NavMesh.CalculateTriangulation();
            int tris = tri.indices == null ? 0 : tri.indices.Length / 3;
            navOk = tris > 0;
            if (!navOk)
            {
                Fail("B5.mesh", "열린 씬에 내비메시가 없다 (NavMeshSurface의 데이터가 비었거나 불러오지 못했다). 내비메시를 쓰는 점검은 건너뛴다");
                return;
            }
            rampOk = Snap(RampFoot, out rampOn, out string why);
            if (!rampOk) Fail("B5.mesh", "경사로 발치 " + V(RampFoot) + "가 내비메시 위가 아니다 (" + why + "). 이어짐 점검은 건너뛴다");
            else Pass("B5.mesh", "내비메시 삼각형 " + tris + "개, 경사로 발치 " + V(RampFoot) + " → 메시 위 " + V(rampOn));
        }

        static void Sampling()
        {
            if (NeedNav("B5.nav"))
            {
                var zs = InScene<ZombieSpawnPoint>();
                zs.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
                int good = 0;
                float worstXZ = 0f, worstY = 0f;
                string worst = "";
                foreach (var z in zs)
                {
                    var p = z.transform.position;
                    if (!Snap(p, out var on, out string why)) { Fail("B5.nav", z.name + " " + V(p) + ": " + why + (z.upstairs ? " (이 지킴이는 서지 않는다)" : "")); continue; }
                    good++;
                    float dxz = Flat(on, p), dy = Mathf.Abs(on.y - p.y);
                    if (dxz > worstXZ) { worstXZ = dxz; worst = z.name; }
                    worstY = Mathf.Max(worstY, dy);
                }
                if (zs.Count == 0) Fail("B5.nav", "ZombieSpawnPoint가 하나도 없다");
                else if (good == zs.Count) Pass("B5.nav", "지킴이·배회 자리 " + zs.Count + "곳 모두 1m 안에 내비메시 (수평으로 가장 먼 것 " + (worst == "" ? "없음" : worst + " " + F(worstXZ) + "m") + ", 높이 차 최대 " + F(worstY) + "m · 허용 " + F(SnapXZ) + " / " + F(SnapY) + ")");
            }

            // 아이템은 자리 0.2m 위에서 아래로 3m 안의 바닥에 붙는다 (PickupItem.Spawn). 바닥이 0.5m 넘게 아래면 엉뚱한 데 놓인다
            var items = InScene<ItemSpawnPoint>();
            items.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            int ok = 0;
            float deepest = 0f;
            foreach (var it in items)
            {
                var p = it.transform.position;
                var above = p + Vector3.up * 0.2f;
                var inside = Physics.OverlapSphere(above, 0.02f, AllLayers, QueryTriggerInteraction.Ignore);
                if (inside.Length > 0) { Fail("B5.item", it.name + " " + V(p) + ": 자리가 콜라이더 " + PathOf(inside[0].transform) + " 안에 있다 (아이템이 그 속에 놓인다)"); continue; }
                if (!Physics.Raycast(above, Vector3.down, out var hit, 3f, AllLayers, QueryTriggerInteraction.Ignore)) { Fail("B5.item", it.name + " " + V(p) + ": 아래 3m 안에 바닥이 없다"); continue; }
                float below = p.y - hit.point.y;
                if (below > 0.5f) { Fail("B5.item", it.name + " " + V(p) + ": 바닥 " + PathOf(hit.collider.transform) + "이 " + F(below) + "m 아래다 (허용 0.5m)"); continue; }
                ok++;
                deepest = Mathf.Max(deepest, below);
            }
            if (items.Count == 0) Fail("B5.item", "ItemSpawnPoint가 하나도 없다");
            else if (ok == items.Count) Pass("B5.item", "아이템 자리 " + items.Count + "곳 모두 0.5m 안에 바닥 (가장 깊은 것 " + F(deepest) + "m), 콜라이더 속에 든 자리 없음");
        }

        // ================= B6: 경사로 발치에서 이어지는가 =================

        static void Connectivity()
        {
            if (!NeedRamp("B6.reach")) return;
            int good = 0;
            foreach (var t in ReachTargets)
                if (ReachFromRamp("B6.reach", t.name, t.p)) good++;
            if (good == ReachTargets.Length) Pass("B6.reach", "경사로 발치에서 " + ReachTargets.Length + "곳 (204호 → 4층 가로지르기 → 6층 홀 → 옥상 계단참, 골목 표지판, 출발 지점) 모두 끝까지 이어진다");

            // 옥상 싸움터의 자리는 본체와 끊겨 있어야 한다 (B7이 본다). 나머지 자리는 모두 닿아야 감염자가 소리를 듣고 온다
            var zs = InScene<ZombieSpawnPoint>();
            zs.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            int reached = 0, tested = 0;
            foreach (var z in zs)
            {
                var p = z.transform.position;
                if (OnArena(p)) continue;
                tested++;
                if (ReachFromRamp("B6.points", z.name, p)) reached++;
            }
            if (tested > 0 && reached == tested) Pass("B6.points", "옥상 싸움터 밖의 지킴이·배회 자리 " + tested + "곳 모두 경사로 발치와 이어진다");

            // 길이: 끊긴 곳은 위에서 잡히고, 여기서는 벽이 빠져 생긴 지름길이나 엉뚱하게 도는 길을 잡는다
            if (Snap(Pad, out var padOn, out _))
            {
                var st = PathBetween(rampOn, padOn, out float len, out var end);
                if (st != NavMeshPathStatus.PathComplete) Fail("B6.length", "경사로 발치 → 옥상 계단참: " + Broken(st, end));
                else if (len < RouteMin || len > RouteMax) Fail("B6.length", "경사로 발치 → 옥상 계단참 경로 길이 " + F(len) + "m (기대 " + F(RouteMin) + "~" + F(RouteMax) + "m)");
                else Pass("B6.length", "경사로 발치 → 옥상 계단참 경로 길이 " + F(len) + "m (기대 " + F(RouteMin) + "~" + F(RouteMax) + "m)");
            }
            var legs = new List<string>();
            bool legsOk = true;
            for (int i = 1; i < LandingChain.Length; i++)
            {
                var a = ReachTargets[LandingChain[i - 1]];
                var b = ReachTargets[LandingChain[i]];
                if (!Snap(a.p, out var aOn, out _) || !Snap(b.p, out var bOn, out _)) { legsOk = false; continue; } // 위에서 이미 FAIL로 적혔다
                var st = PathBetween(aOn, bOn, out float len, out var end);
                if (st != NavMeshPathStatus.PathComplete) { legsOk = false; Fail("B6.length", a.name + " → " + b.name + ": " + Broken(st, end)); }
                else if (len < LegMin || len > LegMax) { legsOk = false; Fail("B6.length", a.name + " → " + b.name + " 경로 길이 " + F(len) + "m (기대 " + F(LegMin) + "~" + F(LegMax) + "m)"); }
                else legs.Add(F(len));
            }
            if (legsOk) Pass("B6.length", "참에서 다음 참까지 " + legs.Count + "구간 길이 (m): " + string.Join(" · ", legs) + " (기대 " + F(LegMin) + "~" + F(LegMax) + ")");
        }

        static bool OnArena(Vector3 p) => p.y > RoofY - 0.5f && p.x > ArenaX0 - 0.1f && p.x < ArenaX1 + 0.1f && p.z > ArenaZ0 - 0.1f && p.z < ArenaZ1 + 0.1f;

        // ================= B7: 있으면 안 되는 곳, 옥상 싸움터 =================

        static void NegativeChecks()
        {
            if (!NeedNav("B7.cut")) return;
            int good = 0;
            foreach (var n in NoMeshPoints)
            {
                if (NavMesh.SamplePosition(n.p, out var hit, 0.4f, NavMesh.AllAreas))
                    Fail("B7.cut", n.name + " " + V(n.p) + ": 내비메시가 없어야 하는데 " + V(hit.position) + "에 있다 (" + F(Vector3.Distance(hit.position, n.p)) + "m)");
                else good++;
            }
            if (good == NoMeshPoints.Length) Pass("B7.cut", "잘라 낸 방 (303·304·404·504·704호), 옥상 동쪽 좁은 길·잔해 더미, 몸체 꼭대기, 경비실 지붕, 경계 밖 네 곳에 내비메시 없음");

            // 옥상 싸움터: 세 점이 서로 이어지고, 본체(경사로 발치·계단참)와는 끊겨 있어야 감염자가 옥상에서 내려오지 못한다
            var on = new Vector3[ArenaPoints.Length];
            bool all = true;
            for (int i = 0; i < ArenaPoints.Length; i++)
                if (!Snap(ArenaPoints[i].p, out on[i], out string why))
                {
                    all = false;
                    Fail("B7.arena", ArenaPoints[i].name + " " + V(ArenaPoints[i].p) + ": 내비메시 위가 아니다 (" + why + ")");
                }
            if (!all) return;
            bool ok = true;
            for (int i = 0; i < on.Length; i++)
                for (int j = i + 1; j < on.Length; j++)
                {
                    var st = PathBetween(on[i], on[j], out _, out var end);
                    if (st == NavMeshPathStatus.PathComplete) continue;
                    ok = false;
                    Fail("B7.arena", ArenaPoints[i].name + " → " + ArenaPoints[j].name + ": 싸움터 안에서 이어지지 않는다 (" + Broken(st, end) + ")");
                }
            bool padOk = Snap(Pad, out var padOn, out _);
            for (int i = 0; i < on.Length; i++)
            {
                if (rampOk && Complete(rampOn, on[i])) { ok = false; Fail("B7.arena", ArenaPoints[i].name + ": 경사로 발치에서 걸어서 닿는다. 옥상 싸움터는 본체와 끊겨 있어야 한다"); }
                if (padOk && Complete(padOn, on[i])) { ok = false; Fail("B7.arena", ArenaPoints[i].name + ": 옥상 계단참에서 걸어서 닿는다. 판자문 틈으로 내비메시가 이어졌다"); }
            }
            if (ok) Pass("B7.arena", "옥상 싸움터의 세 점이 서로 이어지고 경사로 발치·계단참과는 끊겨 있다");
        }

        // ================= B8: 외딴 내비메시 조각 =================

        static void Islands()
        {
            if (!NeedRamp("B8.island")) return;
            var tri = NavMesh.CalculateTriangulation();
            var v = tri.vertices;
            var idx = tri.indices;
            int[] group = TriangleGroups(v, idx, out int groups);
            var area = new float[groups];
            var biggest = new float[groups];
            var rep = new Vector3[groups];    // 가장 큰 삼각형의 무게중심 (경로를 물어볼 자리)
            var centre = new Vector3[groups]; // 넓이로 무게를 준 가운데
            for (int t = 0; t < group.Length; t++)
            {
                Vector3 a = v[idx[t * 3]], b = v[idx[t * 3 + 1]], c = v[idx[t * 3 + 2]];
                float s = TriangleArea(a, b, c);
                int g = group[t];
                area[g] += s;
                centre[g] += (a + b + c) / 3f * s;
                if (s > biggest[g]) { biggest[g] = s; rep[g] = (a + b + c) / 3f; }
            }

            // 같은 변을 쓰는 삼각형끼리만 묶었으므로 타일 경계에서는 한 덩어리가 여럿으로 나뉜다. 그래서 닿는지는 경로로 다시 가린다
            const int Main = 1, Arena = 2;
            var kind = new int[groups];
            var mainReps = new List<Vector3> { rampOn };
            var arenaReps = new List<Vector3>();
            foreach (var a in ArenaPoints)
                if (Snap(a.p, out var on, out _)) arenaReps.Add(on);
            bool arenaByBox = arenaReps.Count == 0;
            // 싸움터가 본체에 붙어 버렸으면 싸움터 조각이 모두 본체로 세어진다. 그때는 "싸움터에 메시가 없다"가 아니라 "붙었다"고 적는다
            bool arenaJoined = arenaReps.Count > 0 && Complete(rampOn, arenaReps[0]);
            for (int round = 0; round < 3; round++)
            {
                bool changed = false;
                for (int g = 0; g < groups; g++)
                {
                    if (kind[g] != 0 || biggest[g] <= 0f) continue;
                    if (ReachAny(mainReps, rep[g])) { kind[g] = Main; mainReps.Add(rep[g]); changed = true; }
                    else if (ReachAny(arenaReps, rep[g])) { kind[g] = Arena; arenaReps.Add(rep[g]); changed = true; }
                }
                if (!changed) break;
            }

            float mainArea = 0f, arenaArea = 0f, total = 0f;
            int mainParts = 0, arenaParts = 0;
            var stray = new List<int>();
            for (int g = 0; g < groups; g++)
            {
                total += area[g];
                if (kind[g] == 0 && arenaByBox && area[g] > 0f && OnArena(centre[g] / area[g])) kind[g] = Arena;
                if (kind[g] == Main) { mainArea += area[g]; mainParts++; }
                else if (kind[g] == Arena) { arenaArea += area[g]; arenaParts++; }
                else if (area[g] > 0f) stray.Add(g);
            }

            // 서로 닿는 외딴 조각끼리는 한 섬으로 센다 (타일 경계에서 나뉜 조각이 1m² 아래로 쪼개져 빠져나가지 않게)
            var island = new int[stray.Count];
            for (int i = 0; i < island.Length; i++) island[i] = i;
            int Find(int a) { while (island[a] != a) { island[a] = island[island[a]]; a = island[a]; } return a; }
            for (int i = 0; i < stray.Count; i++)
                for (int j = i + 1; j < stray.Count; j++)
                {
                    if (area[stray[i]] < 0.02f || area[stray[j]] < 0.02f) continue; // 부스러기끼리는 묻지 않는다
                    if (Find(i) != Find(j) && Complete(rep[stray[i]], rep[stray[j]])) island[Find(i)] = Find(j);
                }
            var islandArea = new Dictionary<int, float>();
            var islandCentre = new Dictionary<int, Vector3>();
            var islandParts = new Dictionary<int, int>();
            for (int i = 0; i < stray.Count; i++)
            {
                int k = Find(i), g = stray[i];
                islandArea.TryGetValue(k, out float s);
                islandCentre.TryGetValue(k, out var c);
                islandParts.TryGetValue(k, out int n);
                islandArea[k] = s + area[g];
                islandCentre[k] = c + centre[g];
                islandParts[k] = n + 1;
            }
            int big = 0, small = 0;
            float smallArea = 0f;
            foreach (var kv in islandArea)
            {
                if (kv.Value <= 1f) { small++; smallArea += kv.Value; continue; }
                big++;
                var mid = islandCentre[kv.Key] / kv.Value;
                // 무엇의 윗면인지 적어 준다 (소품 지붕이면 그 위를 잘라 내면 되고, 방 바닥이면 길이 끊긴 것이다)
                string under = Physics.Raycast(mid + Vector3.up * 0.6f, Vector3.down, out var below, 1.6f, AllLayers, QueryTriggerInteraction.Ignore) ? PathOf(below.collider.transform) : "(바로 아래 콜라이더 없음)";
                Fail("B8.island", "외딴 내비메시 " + F(kv.Value) + "m² (조각 " + islandParts[kv.Key] + "개), 가운데 " + V(mid) + ", 아래는 " + under + ": 경사로 발치에서 닿지 않고 옥상 싸움터도 아니다 (잘라 내는 볼륨이 빠졌거나 길이 끊겼다)");
            }
            if (arenaByBox) Warn("B8.island", "옥상 싸움터의 점이 내비메시 위가 아니라 (B7.arena 참고) 싸움터 조각을 자리로만 가렸다");
            if (arenaJoined) Fail("B8.island", "옥상 싸움터가 본체와 한 덩어리다. 일부러 떼어 놓은 섬이어야 한다 (B7.arena 참고). 본체 " + F(mainArea) + "m² (조각 " + mainParts + ")");
            else if (arenaParts == 0) Fail("B8.island", "옥상 싸움터에 내비메시가 없다 (G8·G9·G8b가 서지 못한다)");
            if (big == 0 && arenaParts > 0 && !arenaJoined)
                Pass("B8.island", "내비메시 " + F(total) + "m²: 본체 " + F(mainArea) + "m² (조각 " + mainParts + "), 옥상 싸움터 " + F(arenaArea) + "m² (조각 " + arenaParts + "), 1m² 이하 외딴 조각 " + small + "개 합 " + F(smallArea) + "m²");
        }

        // 가까운 것부터 몇 곳에서 물어 닿는 곳이 하나라도 있으면 true (경로가 너무 길어 끊기는 경우에 대비해 여러 곳에서 묻는다)
        static bool ReachAny(List<Vector3> sources, Vector3 target)
        {
            if (sources.Count == 0) return false;
            if (Complete(sources[0], target)) return true;
            var order = new List<int>();
            for (int i = 1; i < sources.Count; i++) order.Add(i);
            order.Sort((a, b) => (sources[a] - target).sqrMagnitude.CompareTo((sources[b] - target).sqrMagnitude));
            for (int i = 0; i < order.Count && i < 6; i++)
                if (Complete(sources[order[i]], target)) return true;
            return false;
        }

        static float TriangleArea(Vector3 a, Vector3 b, Vector3 c) => Vector3.Cross(b - a, c - a).magnitude * 0.5f;

        // (순수 계산) 삼각형을 같은 변으로 이어진 덩어리로 묶는다. 꼭짓점은 1cm 격자로 맞붙인다. group[삼각형] = 덩어리 번호
        static int[] TriangleGroups(Vector3[] v, int[] idx, out int groups)
        {
            int tris = idx.Length / 3;
            var weld = new Dictionary<long, int>();
            var id = new int[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                long key = (Quantize(v[i].x) << 42) | (Quantize(v[i].y) << 21) | Quantize(v[i].z);
                if (!weld.TryGetValue(key, out int w)) { w = weld.Count; weld[key] = w; }
                id[i] = w;
            }
            var parent = new int[tris];
            for (int t = 0; t < tris; t++) parent[t] = t;
            int Find(int a) { while (parent[a] != a) { parent[a] = parent[parent[a]]; a = parent[a]; } return a; }
            var edges = new Dictionary<long, int>();
            long n = weld.Count;
            for (int t = 0; t < tris; t++)
                for (int k = 0; k < 3; k++)
                {
                    int a = id[idx[t * 3 + k]], b = id[idx[t * 3 + (k + 1) % 3]];
                    if (a == b) continue;
                    long key = a < b ? a * n + b : b * n + a;
                    if (edges.TryGetValue(key, out int other)) parent[Find(t)] = Find(other);
                    else edges[key] = t;
                }
            var number = new Dictionary<int, int>();
            var group = new int[tris];
            for (int t = 0; t < tris; t++)
            {
                int r = Find(t);
                if (!number.TryGetValue(r, out int g)) { g = number.Count; number[r] = g; }
                group[t] = g;
            }
            groups = number.Count;
            return group;
        }

        // 좌표를 1cm 단위의 0 이상 정수로 (±10km까지 21비트에 들어간다)
        static long Quantize(float f) => (long)Mathf.RoundToInt(f * 100f) + 1048576L;

        // ================= B9: 바닥 구멍 =================

        static void HoleChecks()
        {
            int good = 0;
            foreach (var d in Drops)
            {
                if (!Physics.Raycast(d.start, Vector3.down, out var hit, d.fall + 3.5f, AllLayers, QueryTriggerInteraction.Ignore))
                {
                    Fail("B9.drop", d.name + " " + V(d.start) + ": 아래로 " + F(d.fall + 3.5f) + "m 안에 아무것도 없다 (기대 " + F(d.fall) + "m 아래의 바닥)");
                    continue;
                }
                if (Mathf.Abs(hit.distance - d.fall) > DropTolerance)
                {
                    Fail("B9.drop", d.name + " " + V(d.start) + ": " + F(hit.distance) + "m 아래의 " + PathOf(hit.collider.transform) + "에 맞는다 (기대 " + F(d.fall) + " ± " + F(DropTolerance) + "m" + (hit.distance < d.fall ? ", 구멍이 막혔거나 중간에 걸리는 것이 있다" : ", 아래층 바닥이 없다") + ")");
                    continue;
                }
                good++;
            }
            if (good == Drops.Length) Pass("B9.drop", "구멍 일곱 (H2~H8) 아래로 한 층 3.3m씩 떨어지고, H4의 먼 쪽만 H3를 지나 두 층 6.1m 떨어진다");

            int clear = 0;
            foreach (var h in Holes)
            {
                var centre = new Vector3((h.x0 + h.x1) / 2f, h.y - HoleClearDepth / 2f, (h.z0 + h.z1) / 2f);
                var half = new Vector3((h.x1 - h.x0) / 2f - HoleInset, HoleClearDepth / 2f, (h.z1 - h.z0) / 2f - HoleInset);
                var names = new List<string>();
                foreach (var c in Physics.OverlapBox(centre, half, Quaternion.identity, AllLayers, QueryTriggerInteraction.Collide))
                    names.Add(PathOf(c.transform) + " @" + V(c.bounds.center));
                names.Sort(string.CompareOrdinal);
                if (names.Count > 0) Fail("B9.clear", h.name + " (x " + F(h.x0) + "~" + F(h.x1) + ", z " + F(h.z0) + "~" + F(h.z1) + ", 바닥판 " + F(h.y) + "): 구멍 자리 아래 " + F(HoleClearDepth) + "m 안에 콜라이더 " + names.Count + "개: " + Some(names, 3));
                else clear++;
            }
            if (clear == Holes.Length) Pass("B9.clear", "구멍 일곱의 자리와 그 아래 " + F(HoleClearDepth) + "m에 걸리는 콜라이더 없음");
        }

        // ================= B10: 계단·경사로 =================

        static void FlightChecks()
        {
            foreach (var f in Flights)
            {
                float length = Vector3.Distance(f.foot, f.top);
                var dir = (f.top - f.foot) / length;
                int samples = 0, blocked = 0, wrong = 0;
                string firstBlock = null, firstWrong = null;
                for (int i = 0; ; i++)
                {
                    float s = Mathf.Min(i * FlightStep, length);
                    var p = f.foot + dir * s;
                    samples++;
                    // 머리 위: 서 있는 몸통(키 2.08m까지)이 지나갈 수 있어야 한다
                    if (Physics.CheckCapsule(p + Vector3.up * CapsuleLow, p + Vector3.up * CapsuleHigh, CapsuleRadius, AllLayers, QueryTriggerInteraction.Ignore))
                    {
                        blocked++;
                        if (firstBlock == null)
                        {
                            var names = new List<string>();
                            foreach (var c in Physics.OverlapCapsule(p + Vector3.up * CapsuleLow, p + Vector3.up * CapsuleHigh, CapsuleRadius, AllLayers, QueryTriggerInteraction.Ignore))
                                names.Add(PathOf(c.transform));
                            names.Sort(string.CompareOrdinal);
                            firstBlock = "발치에서 " + F(s) + "m " + V(p) + "에서 몸통이 " + Some(names, 3) + "에 걸린다";
                        }
                    }
                    // 발밑: 양 끝은 이어지는 바닥(참·마당)이 먼저 맞을 수 있어 가운데 표본만 이름을 본다
                    if (s >= 0.2f && s <= length - 0.2f)
                    {
                        string got = null;
                        if (!Physics.Raycast(p + Vector3.up, Vector3.down, out var hit, 2f, AllLayers, QueryTriggerInteraction.Ignore)) got = "아래 2m 안에 아무것도 없다";
                        else if (hit.collider.name != f.surface) got = PathOf(hit.collider.transform) + "에 맞는다 (기대 이름 " + f.surface + ")";
                        else if (Mathf.Abs(hit.point.y - p.y) > 0.12f) got = "경사면 높이가 " + F(hit.point.y) + "이다 (설계 " + F(p.y) + ")";
                        if (got != null)
                        {
                            wrong++;
                            if (firstWrong == null) firstWrong = "발치에서 " + F(s) + "m " + V(p) + ": " + got;
                        }
                    }
                    if (s >= length) break;
                }
                if (blocked > 0) Fail("B10." + f.name, "표본 " + samples + "곳 가운데 " + blocked + "곳에서 머리 위가 막혔다. 처음: " + firstBlock);
                if (wrong > 0) Fail("B10." + f.name, "표본 가운데 " + wrong + "곳의 발밑이 설계와 다르다. 처음: " + firstWrong);
                if (blocked == 0 && wrong == 0) Pass("B10." + f.name, V(f.foot) + " → " + V(f.top) + " 경사 " + F(length) + "m, 표본 " + samples + "곳 모두 머리 위가 비었고 발밑은 " + f.surface);
            }

            int good = 0;
            foreach (var s in Surfaces)
            {
                Collider top = null;
                float topY = float.MinValue;
                foreach (var hit in Physics.RaycastAll(s.p + Vector3.up, Vector3.down, 1.5f, AllLayers, QueryTriggerInteraction.Ignore))
                    if (Mathf.Abs(hit.point.y - s.p.y) <= 0.15f && hit.point.y > topY) { topY = hit.point.y; top = hit.collider; }
                if (top == null) Fail("B10.surface", s.name + " " + V(s.p) + ": 그 높이에 바닥 콜라이더가 없다");
                else if (top.name != s.collider) Fail("B10.surface", s.name + " " + V(s.p) + ": 바닥 이름이 '" + top.name + "'이다 (이름 약속 '" + s.collider + "', 발소리가 달라진다) " + PathOf(top.transform));
                else good++;
            }
            if (good == Surfaces.Length) Pass("B10.surface", "바닥 이름: 마당 Ground, 경사로 위 턱 Slab, 실내 참·홀 Floor, 옥상 계단참·싸움터 Rubble_Roof");
        }

        // ================= B11: 문간과 판자문 =================

        static void DoorwayChecks()
        {
            if (NeedNav("B11.door"))
            {
                int good = 0;
                foreach (var d in Doorways)
                {
                    // 구운 메시는 바닥보다 조금(복셀 반 칸쯤) 떠 있다. 그 높이에서 재야 0.25m를 수평 여유로 온전히 쓴다
                    var at = d.p + Vector3.up * MeshLift;
                    if (NavMesh.SamplePosition(at, out _, DoorSample, NavMesh.AllAreas)) { good++; continue; }
                    string near = NavMesh.SamplePosition(at, out var far, 2f, NavMesh.AllAreas) ? "가장 가까운 메시 " + V(far.position) + ", " + F(Vector3.Distance(far.position, at)) + "m" : "2m 안에 메시 없음";
                    Fail("B11.door", d.name + " " + V(d.p) + ": 문간 가운데 " + F(DoorSample) + "m 안에 내비메시가 없다 (" + near + ")" + Blockers(d.p));
                }
                if (good == Doorways.Length) Pass("B11.door", "문 일곱 (D1 D3a D3b D5a D5b D5c D7), 홀 바리케이드 양쪽 틈, 세대벽 구멍, 칸막이 문 넷, 끝벽 뚫린 곳에 내비메시가 지나간다");
            }

            foreach (var g in CrawlGaps)
            {
                var bad = new List<string>();
                float low = float.MaxValue, high = 0f;
                // 틈의 가운데와 양옆 0.45m: 맨 아래 판자의 밑면이 바닥에서 1.38~1.55m여야 앉은 플레이어(1.1m)만 지나간다
                foreach (float along in new[] { -0.45f, 0f, 0.45f })
                {
                    var origin = new Vector3(g.p.x, g.p.y + 0.1f, g.p.z + along);
                    if (!Physics.Raycast(origin, Vector3.up, out var hit, 3f, AllLayers, QueryTriggerInteraction.Ignore)) { bad.Add("z " + F(origin.z) + "에서 위로 3m 안에 아무것도 없다 (판자가 없다)"); continue; }
                    float h = hit.distance + 0.1f;
                    low = Mathf.Min(low, h);
                    high = Mathf.Max(high, h);
                    if (h < PlankMin || h > PlankMax) bad.Add("z " + F(origin.z) + "에서 " + PathOf(hit.collider.transform) + "의 밑면이 바닥에서 " + F(h) + "m (기대 " + F(PlankMin) + "~" + F(PlankMax) + ")");
                    else if (!hit.collider.name.StartsWith("Plank")) bad.Add("z " + F(origin.z) + "에서 판자가 아니라 " + PathOf(hit.collider.transform) + "에 맞는다");
                }
                if (navOk && NavMesh.SamplePosition(g.p + Vector3.up * MeshLift, out var on, DoorSample, NavMesh.AllAreas)) bad.Add("틈 가운데에 내비메시가 있다 " + V(on.position) + " (감염자가 지나간다)");
                if (bad.Count > 0) Fail("B11.crawl", g.name + " " + V(g.p) + ": " + Some(bad, 4));
                else Pass("B11.crawl", g.name + " " + V(g.p) + ": 맨 아래 판자 밑면 " + F(low) + "~" + F(high) + "m (기대 " + F(PlankMin) + "~" + F(PlankMax) + ")" + (navOk ? ", 틈에 내비메시 없음" : ""));
            }
        }

        // ================= B12: 옥상 가장자리 =================

        static void RoofEdges()
        {
            int rays = 0, leaks = 0, skipped = 0;
            var first = new List<string>();
            foreach (var s in SweepPoints)
                foreach (float h in SweepHeights)
                {
                    var origin = new Vector3(s.p.x, s.p.y + h, s.p.z);
                    if (Physics.CheckSphere(origin, 0.05f, AllLayers, QueryTriggerInteraction.Ignore)) { skipped++; continue; } // 표본 자리가 무언가의 속이다
                    for (int k = 0; k < SweepDirections; k++)
                    {
                        float angle = k * 360f / SweepDirections * Mathf.Deg2Rad;
                        float dx = Mathf.Sin(angle), dz = Mathf.Cos(angle);
                        float exit = ExitDistance(origin.x, origin.z, dx, dz, RoofX0, RoofX1, RoofZ0, RoofZ1);
                        rays++;
                        // 경계에 딱 붙은 면(남은 몸체의 x = 6 면)도 맞게 0.1m 더 쏜다
                        if (Physics.Raycast(origin, new Vector3(dx, 0f, dz), exit + 0.1f, AllLayers, QueryTriggerInteraction.Ignore)) continue;
                        leaks++;
                        if (first.Count < 4) first.Add(s.name + " " + V(s.p) + " 높이 " + F(h) + "m에서 " + (k * 360 / SweepDirections) + "도 쪽 (" + V(origin + new Vector3(dx, 0f, dz) * exit) + "로 빠져나간다)");
                    }
                }
            if (leaks > 0) Fail("B12.edge", "옥상에서 밖으로 뚫린 방향 " + leaks + "개 (" + rays + "개 가운데): " + string.Join(" / ", first));
            else if (rays == 0) Fail("B12.edge", "표본 자리가 모두 콜라이더 속이라 잴 수 없다");
            else Pass("B12.edge", "싸움터 12곳·계단참 3곳에서 높이 0.5 / 1.5 / 2.9m로 " + SweepDirections + "방향씩 " + rays + "개 모두 x " + F(RoofX0) + "~" + F(RoofX1) + ", z " + F(RoofZ0) + "~" + F(RoofZ1) + " 안에서 막힌다");
            if (skipped > 0) Warn("B12.edge", "표본 " + skipped + "곳은 콜라이더 속이라 건너뛰었다 (옥상 구조물 자리가 설계와 다를 수 있다)");
        }

        // (순수 계산) 사각형 [x0, x1] × [z0, z1] 안의 점에서 (dx, dz) 쪽으로 사각형을 벗어날 때까지의 거리
        static float ExitDistance(float px, float pz, float dx, float dz, float x0, float x1, float z0, float z1)
        {
            float t = float.MaxValue;
            if (dx > 1e-6f) t = Mathf.Min(t, (x1 - px) / dx);
            else if (dx < -1e-6f) t = Mathf.Min(t, (x0 - px) / dx);
            if (dz > 1e-6f) t = Mathf.Min(t, (z1 - pz) / dz);
            else if (dz < -1e-6f) t = Mathf.Min(t, (z0 - pz) / dz);
            return Mathf.Max(0f, t);
        }

        // ================= B13: 빌드 씬 목록 =================

        static void BuildList()
        {
            var list = EditorBuildSettings.scenes;
            var enabled = new List<string>();
            int mine = 0;
            bool mineEnabled = false;
            foreach (var s in list)
            {
                if (s.enabled) enabled.Add(s.path);
                if (s.path == ScenePath) { mine++; mineEnabled = s.enabled; }
            }
            var bad = new List<string>();
            for (int i = 0; i < OldScenes.Length; i++)
                if (enabled.Count <= i || enabled[i] != OldScenes[i]) { bad.Add("켜진 씬의 " + (i + 1) + "번째가 " + (enabled.Count > i ? enabled[i] : "(없음)") + "이다 (기대 " + OldScenes[i] + ")"); break; }
            if (mine == 0) bad.Add(ScenePath + "가 목록에 없다 (목적지가 게임에 나오지 않는다)");
            else if (mine > 1) bad.Add(ScenePath + "가 목록에 " + mine + "번 들어 있다");
            else if (!mineEnabled) bad.Add(ScenePath + "가 목록에 있지만 꺼져 있다");
            if (bad.Count > 0) Fail("B13.list", Some(bad, 4));
            else Pass("B13.list", "빌드 목록의 켜진 씬: " + string.Join(", ", enabled.ConvertAll(p => Path.GetFileNameWithoutExtension(p))) + " (편집 모드의 CanStreamedLevelBeLoaded(\"" + Destinations.Apt102.Scene + "\") = " + Application.CanStreamedLevelBeLoaded(Destinations.Apt102.Scene) + ")");
            if (enabled.Count > OldScenes.Length + 1 && bad.Count == 0) Warn("B13.list", "켜진 씬이 " + enabled.Count + "개다 (기대 5개)");

            // 두 번 불러도 목록이 그대로인가. 목록에 이미 들어 있을 때만 부른다 (없을 때 부르면 이 점검이 목록을 바꾸게 된다)
            if (bad.Count == 0)
            {
                const BindingFlags Any = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
                var add = typeof(GameSceneBuilder).GetMethod("AddApt102ToBuildList", Any, null, System.Type.EmptyTypes, null);
                var calc = typeof(GameSceneBuilder).GetMethod("Apt102BuildListWith", Any, null, new[] { typeof(EditorBuildSettingsScene[]) }, null);
                if (add == null || calc == null) Warn("B13.twice", "GameSceneBuilder." + (add == null ? "AddApt102ToBuildList()" : "Apt102BuildListWith()") + "가 없어 두 번 부르기를 시험하지 못했다");
                else
                {
                    var wrong = new List<string>();
                    string before = Describe(list);
                    add.Invoke(null, null);
                    add.Invoke(null, null);
                    string after = Describe(EditorBuildSettings.scenes);
                    if (before != after)
                    {
                        EditorBuildSettings.scenes = list; // 점검이 남긴 변화는 되돌린다
                        wrong.Add("AddApt102ToBuildList()를 두 번 부르자 목록이 바뀌었다 (원래대로 되돌렸다). 전: " + before + " / 후: " + after);
                    }
                    // 이미 든 목록에서는 위의 두 번이 아무것도 하지 않고 돌아온다. 넣는 길과 켜는 길은 목록의 사본으로 계산만 해 본다
                    // (Apt102BuildListWith: 고친 새 목록을 돌려주고, 고칠 것이 없으면 null. 실제 목록은 건드리지 않는다)
                    EditorBuildSettingsScene[] Fix(EditorBuildSettingsScene[] input) => (EditorBuildSettingsScene[])calc.Invoke(null, new object[] { input });
                    var without = new List<EditorBuildSettingsScene>();
                    var expect = new List<string>();
                    foreach (var s in list)
                    {
                        if (s.path == ScenePath) continue;
                        without.Add(new EditorBuildSettingsScene(s.path, s.enabled));
                        expect.Add((s.enabled ? "+" : "-") + s.path);
                        if (s.path == OldScenes[OldScenes.Length - 1]) expect.Add("+" + ScenePath); // City 바로 뒤
                    }
                    string want = string.Join(" ", expect);
                    var put = Fix(without.ToArray());
                    if (put == null || Describe(put) != want) wrong.Add("Apt102가 없는 목록에 넣은 결과가 " + (put == null ? "(그대로)" : Describe(put)) + "이다 (기대 " + want + ")");
                    else
                    {
                        if (Fix(put) != null) wrong.Add("Apt102를 넣은 목록에 한 번 더 부르자 목록이 또 바뀌었다");
                        var off = new List<EditorBuildSettingsScene>();
                        foreach (var s in put) off.Add(new EditorBuildSettingsScene(s.path, s.enabled && s.path != ScenePath));
                        var on = Fix(off.ToArray());
                        if (on == null || Describe(on) != want) wrong.Add("Apt102가 꺼진 목록을 고친 결과가 " + (on == null ? "(그대로)" : Describe(on)) + "이다 (기대 " + want + ")");
                        else if (Fix(on) != null) wrong.Add("Apt102를 켠 목록에 한 번 더 부르자 목록이 또 바뀌었다");
                    }
                    if (wrong.Count > 0) Fail("B13.twice", Some(wrong, 4));
                    else Pass("B13.twice", "AddApt102ToBuildList()를 두 번 불러도 목록이 그대로다. 사본으로 해 본 넣기(City 바로 뒤, 켜짐)·켜기도 한 번에 끝나고 두 번째에는 바뀌지 않는다");
                }
            }
            else Warn("B13.twice", "목록이 기대와 달라 두 번 부르기 시험은 하지 않았다");

            string full = Path.Combine(Path.GetDirectoryName(Application.dataPath), BuildListPath);
            if (!File.Exists(full) || !File.ReadAllText(full).Contains(ScenePath)) Warn("B13.disk", BuildListPath + " 파일에는 아직 " + ScenePath + "가 없다 (목록은 에디터 메모리에만 있다. 빌드할 때는 씬 목록을 직접 적어 넣는다)");
            else Pass("B13.disk", BuildListPath + " 파일에도 " + ScenePath + "가 들어 있다");
        }

        static string Describe(EditorBuildSettingsScene[] list)
        {
            var parts = new List<string>();
            foreach (var s in list) parts.Add((s.enabled ? "+" : "-") + s.path);
            return string.Join(" ", parts);
        }

        // ================= B14: 겹친 면 =================

        static void CoplanarAndZFight()
        {
            var renderers = InScene<MeshRenderer>();

            // --- FixCoplanarFaces가 아직 고칠 것이 남았는가 (같은 판정을 읽기만 한다) ---
            const float Eps = 0.002f, Same = 0.0015f;
            var boxes = new List<MeshRenderer>();
            foreach (var r in renderers)
            {
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null || mf.sharedMesh.vertexCount != 24) continue; // 상자만
                if (RightAngles(r.transform)) boxes.Add(r);
            }
            boxes.Sort((a, b) => a.bounds.min.x.CompareTo(b.bounds.min.x));
            int fixable = 0, partial = 0;
            var examples = new List<string>();
            for (int i = 0; i < boxes.Count; i++)
            {
                Bounds a = boxes[i].bounds;
                for (int j = i + 1; j < boxes.Count && boxes[j].bounds.min.x <= a.max.x + Same; j++)
                {
                    Bounds b = boxes[j].bounds;
                    for (int axis = 0; axis < 3; axis++)
                        for (int side = -1; side <= 1; side += 2)
                        {
                            float pa = side < 0 ? a.min[axis] : a.max[axis], pb = side < 0 ? b.min[axis] : b.max[axis];
                            if (Mathf.Abs(pa - pb) > Same) continue;
                            int u = (axis + 1) % 3, w = (axis + 2) % 3;
                            float ou = Mathf.Min(a.max[u], b.max[u]) - Mathf.Max(a.min[u], b.min[u]);
                            float ow = Mathf.Min(a.max[w], b.max[w]) - Mathf.Max(a.min[w], b.min[w]);
                            if (ou <= 0.005f || ow <= 0.005f) continue;
                            float areaA = a.size[u] * a.size[w], areaB = b.size[u] * b.size[w];
                            bool inside = ou * ow >= Mathf.Min(areaA, areaB) * 0.98f;
                            if (!inside && axis == 1) { partial++; continue; } // 위·아래 면이 일부만 겹친 것은 원래 고치지 않는다
                            float thickness = (areaA <= areaB ? a : b).size[axis];
                            if (thickness <= Eps * 4) continue;                // 너무 얇아 줄일 수 없는 것
                            fixable++;
                            if (examples.Count < 4) examples.Add(PathOf(boxes[i].transform) + " ↔ " + PathOf(boxes[j].transform) + " @" + V(a.center));
                        }
                }
            }
            if (fixable > 0) Warn("B14.coplanar", "같은 면에 같은 쪽으로 겹친 상자 면이 " + fixable + "곳 남아 있다 (저장 전에 FixCoplanarFaces가 돌지 않았거나 다 고치지 못했다): " + Some(examples));
            else Pass("B14.coplanar", "축에 나란한 상자 " + boxes.Count + "개 가운데 같은 면에 겹쳐 남은 것 0곳 (위·아래 면이 일부만 겹쳐 그대로 둔 것 " + partial + "곳)");

            // --- 얇은 수평 렌더러끼리 윗면이 1.2mm 안에서 겹치는가 (맵 세션의 수평 훑기. 여러 층이라 높이 제한은 뺐다) ---
            var thin = new List<MeshRenderer>();
            foreach (var r in renderers)
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                var b = r.bounds;
                if (b.size.y <= 0.03f && b.size.x > 0.05f && b.size.z > 0.05f) thin.Add(r);
            }
            thin.Sort((a, b) => a.bounds.min.x.CompareTo(b.bounds.min.x));
            var kinds = new Dictionary<string, int>();
            var spots = new Dictionary<string, string>();
            int oldPairs = 0;
            for (int i = 0; i < thin.Count; i++)
            {
                Bounds a = thin[i].bounds;
                for (int j = i + 1; j < thin.Count; j++)
                {
                    Bounds b = thin[j].bounds;
                    if (b.min.x > a.max.x - 0.02f) break;
                    if (thin[i].transform.parent == thin[j].transform.parent) continue;
                    if (Mathf.Abs(a.max.y - b.max.y) > 0.0012f) continue;
                    float ox = Mathf.Min(a.max.x, b.max.x) - Mathf.Max(a.min.x, b.min.x);
                    float oz = Mathf.Min(a.max.z, b.max.z) - Mathf.Max(a.min.z, b.min.z);
                    if (ox < 0.03f || oz < 0.03f) continue;
                    if (Under(thin[i].transform, facade) && Under(thin[j].transform, facade)) { oldPairs++; continue; }
                    AddPair(kinds, spots, thin[i], thin[j], "윗면 높이 차 " + F(Mathf.Abs(a.max.y - b.max.y) * 1000f) + "mm");
                }
            }
            ReportPairs("B14.zfightH", "얇은 수평 면", thin.Count, kinds, spots, oldPairs);

            // --- 얇은 세로 렌더러끼리 같은 쪽을 보는 면이 1.5mm 안에서 겹치는가 (맵 세션의 세로 훑기. 앞뒤로 맞닿기만 한 쌍은 세기만 한다) ---
            var thinX = new List<MeshRenderer>();
            var thinZ = new List<MeshRenderer>();
            foreach (var r in renderers)
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                var b = r.bounds;
                if (b.size.x <= 0.03f && b.size.y > 0.05f && b.size.z > 0.05f) thinX.Add(r);
                else if (b.size.z <= 0.03f && b.size.y > 0.05f && b.size.x > 0.05f) thinZ.Add(r);
            }
            kinds = new Dictionary<string, int>();
            spots = new Dictionary<string, string>();
            oldPairs = 0;
            int touching = 0;
            for (int which = 0; which < 2; which++)
            {
                var list = which == 0 ? thinX : thinZ;
                int axis = which == 0 ? 0 : 2, other = which == 0 ? 2 : 0;
                for (int i = 0; i < list.Count; i++)
                    for (int j = i + 1; j < list.Count; j++)
                    {
                        Bounds a = list[i].bounds, b = list[j].bounds;
                        // 깜빡이는 것은 같은 쪽을 보는 두 면이 한 평면에 있을 때다 (FixCoplanarFaces와 같은 기준: 앞면끼리, 뒷면끼리).
                        // 한쪽의 뒷면이 다른 쪽의 앞면에 맞닿은 것(문짝 위의 명판, 사물함 판 위의 띠)은 두 면이 서로 등져 있어 깜빡이지 않는다
                        float gap = Mathf.Min(Mathf.Abs(a.min[axis] - b.min[axis]), Mathf.Abs(a.max[axis] - b.max[axis]));
                        float back = Mathf.Min(Mathf.Abs(a.min[axis] - b.max[axis]), Mathf.Abs(a.max[axis] - b.min[axis]));
                        if (gap > 0.0015f && back > 0.0015f) continue;
                        float oy = Mathf.Min(a.max.y, b.max.y) - Mathf.Max(a.min.y, b.min.y);
                        float ow = Mathf.Min(a.max[other], b.max[other]) - Mathf.Max(a.min[other], b.min[other]);
                        if (oy < 0.05f || ow < 0.05f) continue;
                        if (Under(list[i].transform, facade) && Under(list[j].transform, facade)) { oldPairs++; continue; }
                        if (gap > 0.0015f) { touching++; continue; }
                        AddPair(kinds, spots, list[i], list[j], "틈 " + F(gap * 1000f) + "mm");
                    }
            }
            ReportPairs("B14.zfightV", "얇은 세로 면", thinX.Count + thinZ.Count, kinds, spots, oldPairs, touching > 0 ? " · 앞뒤로 맞닿기만 한 쌍 " + touching + "개는 서로 등진 면이라 세지 않았다" : "");

            // --- 상호작용 물체가 정적 배칭에 남아 있으면 윤곽선이 생기지 않는다 ---
            var batched = new List<string>();
            foreach (var it in InScene<Interactable>())
                foreach (var t in it.GetComponentsInChildren<Transform>(true))
                    if ((GameObjectUtility.GetStaticEditorFlags(t.gameObject) & StaticEditorFlags.BatchingStatic) != 0) { batched.Add(PathOf(t)); break; }
            batched.Sort(string.CompareOrdinal);
            if (batched.Count > 0) Warn("B14.outline", "정적 배칭에 남아 있는 상호작용 물체 " + batched.Count + "개 (조준해도 윤곽선이 생기지 않는다. UnbatchInteractables가 돌지 않았다): " + Some(batched));
            else Pass("B14.outline", "상호작용 물체는 모두 정적 배칭에서 빠져 있다");
        }

        // ================= B15: Tripo 품질별 모델 =================

        static void TripoModels()
        {
            if (tripoHolders.Count == 0)
            {
                Pass("B15.tripo", "Tripo 받침(" + TripoHolderPrefix + "…) 없음: 코드로 지은 소품만 있는 씬이라 모델 점검은 건너뛴다");
                return;
            }

            // --- 받침마다: Model + LODGroup + QualityLod, 판 넷의 메시·재질·텍스처 ---
            var bad = new List<string>();
            var kinds = new SortedDictionary<string, int>(System.StringComparer.Ordinal);
            foreach (var h in tripoHolders)
            {
                string id = h.name.Substring(TripoHolderPrefix.Length);
                kinds.TryGetValue(id, out int n);
                kinds[id] = n + 1;
                string why = TripoFault(h);
                if (why != null) bad.Add(PathOf(h) + " @" + V(h.position) + ": " + why);
            }
            var list = new List<string>();
            foreach (var kv in kinds) list.Add(kv.Key + " " + kv.Value);
            if (bad.Count > 0) Fail("B15.tripo", "Tripo 받침 " + tripoHolders.Count + "개 가운데 " + bad.Count + "개가 품질별 모델의 약속과 다르다: " + Some(bad, 5));
            else Pass("B15.tripo", "Tripo 받침 " + tripoHolders.Count + "개 모두 Model(LODGroup 4칸 + QualityLod)과 판 넷(메시는 그 품질 폴더, 재질 " + TripoMatPrefix + "…_q0~3, _BaseMap은 같은 폴더의 텍스처)을 갖췄다: " + string.Join(", ", list));

            // --- 정적 배칭: 묶이면 놓인 모델마다 판 넷의 꼭짓점이 묶음 버퍼에 복사된다 (메모리가 크게 늘고, 같은 메시를 함께 그리는 인스턴싱도 풀린다) ---
            var batched = new List<string>();
            int renderers = 0;
            foreach (var t in tripoParts)
            {
                if (t.GetComponent<Renderer>() == null) continue;
                renderers++;
                if ((GameObjectUtility.GetStaticEditorFlags(t.gameObject) & StaticEditorFlags.BatchingStatic) != 0) batched.Add(PathOf(t) + " @" + V(t.position));
            }
            batched.Sort(string.CompareOrdinal);
            if (batched.Count > 0) Fail("B15.static", "정적 배칭에 든 Tripo 모델의 판 " + batched.Count + "개 (판 넷이 모두 묶음 버퍼에 복사된다): " + Some(batched));
            else Pass("B15.static", "Tripo 모델의 판 " + renderers + "개는 모두 정적 배칭에서 빠져 있다");
        }

        // 받침 하나가 품질별 모델의 약속대로인가. 맞으면 null, 아니면 처음 걸린 까닭
        static string TripoFault(Transform holder)
        {
            var model = holder.Find(TripoModelName);
            if (model == null) return TripoModelName + "이 없다 (품질별 판을 찾지 못해 원본 FBX 하나로 대신했거나 받침이 비었다)";
            var lg = model.GetComponent<LODGroup>();
            if (lg == null) return TripoModelName + "에 LODGroup이 없다";
            var lods = lg.GetLODs();
            if (lods.Length != 4) return "LOD 칸이 " + lods.Length + "개다 (기대 4)";
            var ql = model.GetComponent<QualityLod>();
            if (ql == null) return TripoModelName + "에 QualityLod가 없다 (세부 묘사 품질을 바꿔도 판이 바뀌지 않는다)";
            if (ql.tiers == null || ql.tiers.Length != 4) return "QualityLod.tiers가 " + (ql.tiers == null ? 0 : ql.tiers.Length) + "칸이다 (기대 4)";
            if (ql.heights == null || ql.heights.Length != 4) return "QualityLod.heights가 " + (ql.heights == null ? 0 : ql.heights.Length) + "칸이다 (기대 4)";
            var tiers = TripoTiers(model);
            for (int q = 0; q < 4; q++)
            {
                string name = "LOD" + (3 - q) + "_q" + q;
                if (tiers[q] == null) return name + " 판이 없다";
                var r = tiers[q].GetComponent<MeshRenderer>();
                if (r == null) return name + "에 MeshRenderer가 없다";
                if (!r.enabled) return name + "의 렌더러가 꺼져 있다";
                var mesh = MeshOf(tiers[q]);
                if (mesh == null) return name + "의 메시가 비었다";
                // 판 메시는 모델 폴더의 그 품질 폴더(Q{q}_…)에 든 에셋이어야 한다 (다른 품질의 메시가 들어가면 그 판의 텍스처와 맞지 않는다)
                string meshPath = AssetDatabase.GetAssetPath(mesh);
                if (string.IsNullOrEmpty(meshPath)) return name + "의 메시가 에셋이 아니다 (씬 파일 안에 저장된다)";
                string dir = meshPath.Substring(0, meshPath.LastIndexOf('/') + 1);
                if (!meshPath.StartsWith(TripoMeshRoot) || !dir.Contains("/Q" + q + "_")) return name + "의 메시가 " + meshPath + "에 있다 (기대 " + TripoMeshRoot + "<모델>/Q" + q + "_…)";
                var mats = r.sharedMaterials;
                if (mats.Length == 0) return name + "에 재질 칸이 없다";
                foreach (var m in mats)
                {
                    if (m == null) return name + "의 재질이 비었다";
                    if (!m.name.StartsWith(TripoMatPrefix) || !m.name.EndsWith("_q" + q)) return name + "의 재질이 " + m.name + "이다 (기대 " + TripoMatPrefix + "…_q" + q + ")";
                    var tex = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : null;
                    if (tex == null) return name + "의 재질 " + m.name + "에 _BaseMap이 비었다 (텍스처 없이 민색으로 보인다)";
                    string texPath = AssetDatabase.GetAssetPath(tex);
                    if (string.IsNullOrEmpty(texPath) || !texPath.StartsWith(dir)) return name + "의 재질 " + m.name + "이 그 판의 폴더 밖 텍스처 " + texPath + "를 쓴다 (메시는 " + dir + ")";
                }
                var slot = lods[3 - q].renderers;
                if (slot == null || slot.Length != 1 || slot[0] != r) return "LOD 칸 " + (3 - q) + "이 " + name + " 판 하나를 가리키지 않는다";
                if (ql.tiers[q] != r) return "QualityLod.tiers[" + q + "]이 " + name + "의 렌더러가 아니다";
            }
            // 축마다 맞춘 상자의 한 변이 0이면 모델이 납작하게 눌려 보이지 않는다
            var size = tiers[3].GetComponent<MeshRenderer>().bounds.size;
            if (Mathf.Min(size.x, Mathf.Min(size.y, size.z)) < 0.001f) return "모델이 한 축으로 0에 가깝게 눌렸다 (크기 " + V(size) + ")";
            return null;
        }

        // under 아래의 Tripo 받침
        static List<Transform> HoldersUnder(Transform under)
        {
            var list = new List<Transform>();
            foreach (var h in tripoHolders)
                if (h.IsChildOf(under)) list.Add(h);
            return list;
        }

        // 겉모습을 바꾼 코드 소품의 약속: 승합차는 코드 그대로, 세단은 모델에 맞춘 상자 콜라이더 하나, 소파·옷장의 새 콜라이더, 상호작용 물체의 윤곽선
        static void TripoProps()
        {
            bool on = tripoHolders.Count > 0;

            // --- 차: 탄 승합차(몸체 아래 Cab·Cargo)는 코드 모습 그대로 (사용자 결정 2026-11-26: 세단 모델로 바꾸지 않는다) ---
            int vans = 0, sedans = 0, swapped = 0;
            string vanText = "";
            var vanBad = new List<string>();
            var carBad = new List<string>();
            var carOdd = new List<string>();
            foreach (var car in Named(geo, "WreckedCar", facade))
            {
                var body = car.Find("Body");
                var holders = HoldersUnder(car);
                var solid = new List<Collider>();
                foreach (var c in car.GetComponentsInChildren<Collider>(true))
                    if (!c.isTrigger) solid.Add(c);
                if (body != null && (body.Find("Cargo") != null || body.Find("Cab") != null))
                {
                    vans++;
                    if (holders.Count > 0) vanBad.Add("승합차 " + V(car.position) + "에 Tripo 받침 " + holders[0].name + "이 붙어 있다 (세단 모델로 바뀌었다)");
                    foreach (string part in new[] { "Lower", "Cab", "Cargo" })
                    {
                        var p = body.Find(part);
                        var r = p == null ? null : p.GetComponent<MeshRenderer>();
                        if (p != null && (r == null || !r.enabled || MeshOf(p) == null)) vanBad.Add("승합차 " + V(car.position) + "의 Body/" + part + " 겉모습이 지워졌다");
                    }
                    if (solid.Count == 0) vanBad.Add("승합차 " + V(car.position) + "에 콜라이더가 없다");
                    vanText = V(car.position) + " (몸체 렌더러 " + body.GetComponentsInChildren<MeshRenderer>().Length + "개, 콜라이더 " + solid.Count + "개)";
                    continue;
                }
                sedans++;
                Transform model = null;
                foreach (var h in holders)
                    if (h.name.StartsWith(TripoHolderPrefix + "wrecked_sedan")) model = h;
                if (model == null) continue;
                swapped++;
                // 모델을 든 차: 예전 부품 콜라이더(코드 세단 길이)는 지우고 받침에 모델 크기의 상자 하나 (도시의 TripoCarColliders와 같다)
                if (solid.Count == 0) carBad.Add(V(car.position) + ": 콜라이더가 없다 (차를 뚫고 지나간다)");
                else if (solid.Count != 1 || !(solid[0] is BoxCollider) || solid[0].transform != model)
                    carOdd.Add(V(car.position) + ": 받침의 상자 하나가 아니라 콜라이더 " + solid.Count + "개 (" + PathOf(solid[0].transform) + " 등. 예전 부품 콜라이더가 남으면 모델보다 긴 보이지 않는 벽이 된다)");
            }
            if (vans != 1) vanBad.Add("승합차(몸체 아래 Cab·Cargo가 있는 WreckedCar)가 " + vans + "대다 (기대 1: 주차 칸의 탄 승합차)");
            if (vanBad.Count > 0) Fail("B15.van", Some(vanBad, 6));
            else Pass("B15.van", "탄 승합차 " + vanText + "는 코드 모습 그대로다 (Tripo 받침 없음)");
            if (carBad.Count > 0) Fail("B15.car", "Tripo 차: " + Some(carBad));
            else if (swapped == 0) Pass("B15.car", "세단 " + sedans + "대 모두 코드 모습이다 (Tripo 차 없음)");
            else Pass("B15.car", "세단 " + sedans + "대 가운데 Tripo 차 " + swapped + "대, 저마다 콜라이더가 있다");
            if (carOdd.Count > 0) Warn("B15.car", "Tripo 차의 콜라이더가 약속(받침의 상자 하나)과 다르다: " + Some(carOdd));
            if (on && swapped < sedans) Warn("B15.car", "Tripo 모델이 있는 씬인데 세단 " + (sedans - swapped) + "대가 코드 모습이다 (계획: 세단 둘은 wrecked_sedan_01)");

            // --- 새 가구 콜라이더 (사용자 결정: 소파·옷장만 막는다). 모델이 없는 씬에는 있으면 안 된다 (보이지 않는 벽) ---
            var bad = new List<string>();
            FurnitureColliders(bad, "SofaCollider", TripoHolderPrefix + "apt_sofa_01", on ? 1 : 0);
            FurnitureColliders(bad, "WardrobeCollider", TripoHolderPrefix + "apt_wardrobe_01", on ? 2 : 0);
            if (bad.Count > 0) Fail("B15.collider", Some(bad, 6));
            else if (on) Pass("B15.collider", "SofaCollider 1개 (402호 소파)·WardrobeCollider 2개 (403호 옷장, 204호 넘어진 장)가 저마다 자기 모델과 겹쳐 있다");
            else Pass("B15.collider", "Tripo 모델이 없는 씬: SofaCollider·WardrobeCollider도 없다");

            // --- 윤곽선: 상호작용 물체 아래의 메시에만 붙는다 (InteractOutline). 겉모습을 지우고 받침을 그 물체 옆에 두면 조준해도 빛나지 않는다 ---
            if (!on) return;
            var dark = new List<string>();
            foreach (var it in InScene<Interactable>())
            {
                if (HasLook(it.transform) || it.transform.parent == null) continue;
                // 처음부터 콜라이더뿐인 것은 그대로 둔다. 같은 부모 아래에 받침이 나란히 놓인 것만 적는다
                foreach (var h in tripoHolders)
                    if (h.parent == it.transform.parent) { dark.Add(PathOf(it.transform) + " (받침 " + h.name + ")"); break; }
            }
            dark.Sort(string.CompareOrdinal);
            if (dark.Count > 0) Warn("B15.outline", "겉모습이 Tripo 모델로 바뀌었는데 자기 아래에 메시가 없는 상호작용 물체 " + dark.Count + "개 (조준해도 윤곽선이 생기지 않는다. 받침을 그 물체의 자식으로 둔다): " + Some(dark));
            else Pass("B15.outline", "겉모습을 Tripo 모델로 바꾼 상호작용 물체는 자기 아래에 메시가 있다 (윤곽선이 붙는다)");
        }

        // 이름이 name인 콜라이더가 want개이고, 저마다 트리거가 아닌 상자이며 그 모델(받침 holderName)과 겹쳐 있는가
        static void FurnitureColliders(List<string> bad, string name, string holderName, int want)
        {
            var list = new List<Collider>();
            foreach (var c in InScene<Collider>())
                if (c.name == name) list.Add(c);
            if (list.Count != want) bad.Add(name + " " + list.Count + "개 (기대 " + want + ")");
            foreach (var c in list)
            {
                if (!(c is BoxCollider) || c.isTrigger) { bad.Add(name + " " + V(c.bounds.center) + "가 트리거가 아닌 상자 콜라이더가 아니다"); continue; }
                bool onModel = false;
                foreach (var h in tripoHolders)
                    if (h.name == holderName && TripoBounds(h, out var b) && b.Intersects(c.bounds)) { onModel = true; break; }
                if (!onModel) bad.Add(name + " " + V(c.bounds.center) + "가 " + holderName + " 모델과 겹치지 않는다 (보이지 않는 벽이 된다)");
            }
        }

        static void AddPair(Dictionary<string, int> kinds, Dictionary<string, string> spots, MeshRenderer a, MeshRenderer b, string detail)
        {
            string key = string.CompareOrdinal(a.name, b.name) <= 0 ? a.name + " | " + b.name : b.name + " | " + a.name;
            kinds.TryGetValue(key, out int n);
            kinds[key] = n + 1;
            if (!spots.ContainsKey(key)) spots[key] = PathOf(a.transform) + " @" + V(a.bounds.center) + " ↔ " + PathOf(b.transform) + " @" + V(b.bounds.center) + ", " + detail;
        }

        static void ReportPairs(string id, string what, int candidates, Dictionary<string, int> kinds, Dictionary<string, string> spots, int oldPairs, string note = "")
        {
            if (kinds.Count == 0)
            {
                Pass(id, what + " " + candidates + "개 가운데 새로 지은 것끼리 겹친 쌍 없음 (가져다 쓴 아파트 겉모습 안의 쌍 " + oldPairs + "개는 도시와 같아 세지 않는다)" + note);
                return;
            }
            var keys = new List<string>(kinds.Keys);
            keys.Sort((p, q) => kinds[q] != kinds[p] ? kinds[q].CompareTo(kinds[p]) : string.CompareOrdinal(p, q));
            var parts = new List<string>();
            int total = 0;
            foreach (string k in keys)
            {
                total += kinds[k];
                if (parts.Count < 5) parts.Add(kinds[k] + "쌍 [" + k + "] 예: " + spots[k]);
            }
            Fail(id, what + "끼리 겹쳐 깜빡일 수 있는 쌍 " + total + "개 (" + keys.Count + "가지, 보기 문제이고 걷는 데는 지장 없다): " + string.Join(" / ", parts) + (keys.Count > 5 ? " 외 " + (keys.Count - 5) + "가지" : "") + note);
        }

        // 회전이 90도의 배수인가 (경계 상자가 곧 상자 자체인 것만 겹친 면을 따진다)
        static bool RightAngles(Transform t)
        {
            var e = t.rotation.eulerAngles;
            for (int k = 0; k < 3; k++)
            {
                float m = Mathf.Repeat(e[k], 90f);
                if (m > 0.5f && m < 89.5f) return false;
            }
            return true;
        }

        // ================= 걷기 시험용 경로 =================

        public static string Route()
        {
            try
            {
                if (!Snap(RampFoot, out var start, out string why)) return "status=Error;reason=RampFoot " + V(RampFoot) + " " + OneLine(why);
                if (!Snap(Pad, out var goal, out why)) return "status=Error;reason=pad " + V(Pad) + " " + OneLine(why);
                var corners = new List<Vector3>();
                string mode = "single", status = "PathComplete", broken = "";
                float length;
                if (PathBetween(start, goal, out length, out _, corners) != NavMeshPathStatus.PathComplete)
                {
                    // 한 번에 이어지지 않으면 설계의 참을 차례로 이어 본다 (어느 구간에서 끊기는지 알 수 있다)
                    mode = "legs";
                    corners.Clear();
                    length = 0f;
                    var stops = new List<Pt> { ReachTargets[0] };
                    foreach (int i in LandingChain) stops.Add(ReachTargets[i]);
                    var prev = start;
                    string prevName = "RampFoot";
                    foreach (var stop in stops)
                    {
                        if (!Snap(stop.p, out var next, out _)) { status = "PathPartial"; broken = prevName + " -> " + stop.name + " (not on NavMesh)"; break; }
                        var leg = new List<Vector3>();
                        var st = PathBetween(prev, next, out float legLength, out _, leg);
                        if (corners.Count > 0 && leg.Count > 0) leg.RemoveAt(0); // 앞 구간의 끝점과 같은 점
                        corners.AddRange(leg);
                        length += legLength;
                        if (st != NavMeshPathStatus.PathComplete) { status = "PathPartial"; broken = prevName + " -> " + stop.name; break; }
                        prev = next;
                        prevName = stop.name;
                    }
                }
                var sb = new StringBuilder();
                sb.Append("status=").Append(status).Append(";mode=").Append(mode).Append(";length=").Append(F(length)).Append(";corners=").Append(corners.Count);
                if (broken != "") sb.Append(";broken=").Append(broken);
                foreach (var c in corners)
                    sb.Append('|').Append(c.x.ToString("0.000", Inv)).Append(',').Append(c.y.ToString("0.000", Inv)).Append(',').Append(c.z.ToString("0.000", Inv));
                return sb.ToString();
            }
            catch (System.Exception e)
            {
                return "status=Error;reason=" + e.GetType().Name + " " + OneLine(e.Message);
            }
        }

        // ================= 수 세기 =================

        public static string Counts()
        {
            scene = SceneManager.GetActiveScene();
            var players = InScene<PlayerController>();
            player = players.Count > 0 ? players[0].transform : null;
            TripoScan();

            int objects = 0, active = 0;
            foreach (var t in InScene<Transform>())
            {
                objects++;
                if (t.gameObject.activeInHierarchy) active++;
            }
            var meshes = new HashSet<Mesh>();
            long instanced = 0, unique = 0;
            int filters = 0;
            // uniqueMeshes·uniqueVertices·instancedVertices는 B3.vertices와 같이 코드로 지은 메시만 센다. Tripo 품질별 모델은 아래 tripo… 칸
            foreach (var mf in InScene<MeshFilter>())
            {
                filters++;
                if (mf.sharedMesh == null || InTripoModel(mf.transform)) continue;
                meshes.Add(mf.sharedMesh);
                instanced += mf.sharedMesh.vertexCount;
            }
            foreach (var m in meshes) unique += m.vertexCount;
            var tripo = TripoCount();
            int lights = 0, pointLights = 0, shadowLights = 0;
            foreach (var l in InScene<Light>())
            {
                lights++;
                if (l.type == LightType.Point) pointLights++;
                if (l.shadows != LightShadows.None) shadowLights++;
            }
            int colliders = 0, boxColliders = 0, meshColliders = 0, triggers = 0;
            foreach (var c in InScene<Collider>())
            {
                colliders++;
                if (c is BoxCollider) boxColliders++;
                if (c is MeshCollider) meshColliders++;
                if (c.isTrigger) triggers++;
            }
            var tri = NavMesh.CalculateTriangulation();
            int navTriangles = tri.indices == null ? 0 : tri.indices.Length / 3;
            float navArea = 0f;
            for (int t = 0; t < navTriangles; t++)
                navArea += TriangleArea(tri.vertices[tri.indices[t * 3]], tri.vertices[tri.indices[t * 3 + 1]], tri.vertices[tri.indices[t * 3 + 2]]);

            var sb = new StringBuilder();
            sb.Append("{\"scene\":\"").Append(scene.name).Append('"');
            void Add(string key, long value) => sb.Append(",\"").Append(key).Append("\":").Append(value.ToString(Inv));
            Add("objects", objects);
            Add("activeObjects", active);
            Add("renderers", InScene<Renderer>().Count);
            Add("meshFilters", filters);
            Add("uniqueMeshes", meshes.Count);
            Add("uniqueVertices", unique);
            Add("instancedVertices", instanced);
            Add("tripoModels", tripo.models);
            Add("tripoKinds", tripo.kinds);
            Add("tripoMeshes", tripo.meshes);
            Add("tripoMeshVertices", tripo.meshVertices);
            Add("tripoLowVertices", tripo.lowPlaced);
            Add("tripoHighVertices", tripo.highPlaced);
            Add("lights", lights);
            Add("pointLights", pointLights);
            Add("shadowLights", shadowLights);
            Add("colliders", colliders);
            Add("boxColliders", boxColliders);
            Add("meshColliders", meshColliders);
            Add("triggers", triggers);
            Add("particleSystems", InScene<ParticleSystem>().Count);
            Add("zombiePoints", InScene<ZombieSpawnPoint>().Count);
            Add("itemPoints", InScene<ItemSpawnPoint>().Count);
            Add("hazardZones", InScene<HazardZone>().Count);
            Add("interactables", InScene<Interactable>().Count);
            Add("navCutVolumes", InScene<NavMeshModifierVolume>().Count);
            Add("navTriangles", navTriangles);
            sb.Append(",\"navArea\":").Append(navArea.ToString("0.0", Inv));
            Add("sceneBytes", Size(ScenePath));
            Add("meshBytes", Size(MeshPath));
            Add("navBytes", Size(NavPath));
            sb.Append('}');
            return sb.ToString();
        }
    }
}
