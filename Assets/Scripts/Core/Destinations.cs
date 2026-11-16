using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// 탐험 목적지 한 곳 (Destinations 표의 한 줄). 맵마다 달라지는 글귀·열리는 날·보상만 담는다
public sealed class Destination
{
    public string Id, Scene;
    public string Name, Blurb, BlurbSpent, Risk;   // 출발 창의 한 줄. 보상이 거의 바닥나면(clears >= Rewards.Length - 1) Blurb 대신 BlurbSpent. Risk는 null이어도 된다. Risk의 {0} = 탐험 제한시간(분)
    public string AreaLabel, PauseLabel;           // HUD 지역 이름, 일시정지 창 부제
    public string LoadingTitle, LoadingTag;        // LoadingTag가 null이면 UIManager가 제목을 보고 고른다 (예전 규칙)
    public string Arrival, ExitTo;                 // 도착 안내문, 돌아갈 곳을 가리키는 말 ("해치로" / "골목으로")
    public int UnlockTurn;                         // 열리는 날 (0 = 처음부터 열려 있다)
    public string SoonLine, UnlockNotice;          // SoonLine의 {0} = 남은 날 수
    public int LateTierTurn;                       // tier가 1 이상인 지킴이 자리는 이 날부터 선다 (0 = 그런 자리가 없는 맵)
    public ItemCount[][] Rewards;                  // LootCache에 든 것. 지금까지 연 횟수로 줄을 고르고 마지막 줄을 되풀이한다. null = 없음
}

// 탐험 목적지 표. 방공호 출입구의 고르는 창, 로딩 화면, HUD·일시정지 창의 지역 이름, 도착·귀환 안내문이 모두 여기서 글귀를 가져간다.
// 도시 줄의 글귀는 목적지가 도시 하나뿐이던 때의 글자 그대로다 (도시에서는 아무것도 달라 보이지 않아야 한다)
public static class Destinations
{
    public const string CityId = "city", Apt102Id = "apt102";
    public const int NoticeLeadTurns = 3; // 열리기 며칠 전부터 출발 창에 알리는가

    // 시험용: 켜면 날짜·처음 안내와 상관없이 모든 목적지가 열린다. 저장하지 않고, 게임 코드는 켜지 않는다 (에디터 메뉴가 켠다)
    public static bool DebugUnlockAll;

    public static readonly Destination City = new Destination
    {
        Id = CityId,
        Scene = "City",
        Name = "도시",
        Blurb = "방공호 앞 거리. 건물을 뒤져 재료를 모은다.",
        AreaLabel = "도시 탐색",
        PauseLabel = "도시 탐험 중",
        LoadingTitle = "도시로 나가는 중",
        LoadingTag = null,
        Arrival = "폐허가 된 도시. 붉은 등이 켜진 해치가 돌아갈 곳이다.",
        ExitTo = "해치로",
    };

    public static readonly Destination Apt102 = new Destination
    {
        Id = Apt102Id,
        Scene = "Apt102",
        Name = "삼일아파트 102동",
        Blurb = "폭격 맞은 동. 꼭대기 중계기함에 귀한 부품이 남아 있다.",
        BlurbSpent = "폭격 맞은 동. 중계기함은 거의 비었다. 자투리만 남았다.",
        Risk = "! 층마다 놈들이 버티고 있다. {0}분이 빠듯하다.", // 숫자를 적어 두면 제한시간(GameBalance)을 고쳤을 때 출발 창의 윗줄과 어긋난다
        AreaLabel = "102동 수색",
        PauseLabel = "102동 수색 중",
        LoadingTitle = "삼일아파트 102동으로 가는 중",
        LoadingTag = "DEPARTING  ·  APT 102",
        Arrival = "삼일아파트 102동. 붉은 등이 켜진 골목이 돌아갈 길이다.",
        ExitTo = "골목으로",
        UnlockTurn = 67, // 임시 수치
        SoonLine = "· 삼일아파트 102동 쪽 불길이 잦아들고 있다. {0}일 뒤면 다가갈 수 있다.",
        UnlockNotice = "<color=#d8a523>새 목적지</color>  삼일아파트 102동. 불길이 잦아들었다. 출입구에서 갈 곳을 고른다.",
        LateTierTurn = 77, // 임시 수치
        Rewards = new[]
        {
            new[] { Item("semiconductor", 2), Item("circuit_board", 1) },                   // 임시 수치: 처음 열 때
            new[] { Item("semiconductor", 1), Item("circuit_board", 2) },                   // 임시 수치: 두 번째
            new[] { Item("circuit_board", 1), Item("battery", 2), Item("copper_wire", 3) }, // 임시 수치: 그 뒤로는 자투리
        },
    };

    // City·Apt102보다 뒤에 적어야 한다 (정적 필드는 적힌 순서대로 만들어진다)
    public static readonly Destination[] All = { City, Apt102 };

    public static Destination Default => City;

    static ItemCount Item(string id, int count) => new ItemCount { id = id, count = count };

    // id로 찾는다 (모르는 id면 null)
    public static Destination Get(string id)
    {
        foreach (var d in All)
            if (d.Id == id) return d;
        return null;
    }

    // 씬 이름으로 찾는다 (목적지가 아닌 씬이면 null)
    public static Destination ForScene(string sceneName)
    {
        foreach (var d in All)
            if (d.Scene == sceneName) return d;
        return null;
    }

    // 지금 열려 있는 씬의 목적지. 방공호·메인 메뉴·컷씬에서는 null
    public static Destination Current => ForScene(SceneManager.GetActiveScene().name);

    // 씬 이름마다 한 번만 확인해 둔다 (출발 창과 방공호의 알림이 자주 묻는다)
    static readonly Dictionary<string, bool> inBuild = new Dictionary<string, bool>();

    // 목적지의 씬이 빌드 목록에 들어 있는가 (없으면 고르는 창에 나오지 않고, 출발도 하루를 쓰지 않고 취소된다)
    public static bool InBuild(Destination d)
    {
        if (d == null || string.IsNullOrEmpty(d.Scene)) return false;
        if (!inBuild.TryGetValue(d.Scene, out bool ok))
        {
            ok = Application.CanStreamedLevelBeLoaded(d.Scene);
            inBuild[d.Scene] = ok;
        }
        return ok;
    }

    // 빌드 목록을 바꾼 뒤에는 다시 확인하게 한다 (에디터의 씬 생성 메뉴·시험이 부른다)
    public static void ForgetBuildCache() => inBuild.Clear();

    // 열렸는가: 처음부터 열린 곳이거나, 열리는 날이 됐고 처음 안내의 첫 귀환까지 마쳤다 (DebugUnlockAll이면 둘 다 건너뛴다)
    public static bool Unlocked(Destination d, GameState s)
    {
        if (d == null) return false;
        if (d.UnlockTurn <= 0 || DebugUnlockAll) return true;
        return s != null && s.Turn >= d.UnlockTurn && s.TutorialStep >= Tutorial.StepAfter("return");
    }

    // 출발 창에 나올 목적지. 도시는 늘 맨 앞이다 (창이 비지 않게 빌드 목록도 따지지 않는다). 나머지는 열렸고 빌드 목록에 든 곳만 표 순서대로
    public static List<Destination> Choices(GameState s)
    {
        var list = new List<Destination> { City };
        foreach (var d in All)
            if (d != City && Unlocked(d, s) && InBuild(d)) list.Add(d);
        return list;
    }

    // 곧 열릴 목적지를 알리는 한 줄 (열리기 NoticeLeadTurns일 전부터, 첫 귀환을 마친 뒤에만). 없으면 null
    public static string SoonLineFor(GameState s)
    {
        if (s == null || DebugUnlockAll) return null;
        if (s.TutorialStep < Tutorial.StepAfter("return")) return null;
        foreach (var d in All)
        {
            int left = d.UnlockTurn - s.Turn;
            if (d.UnlockTurn <= 0 || left <= 0 || left > NoticeLeadTurns) continue;
            if (string.IsNullOrEmpty(d.SoonLine) || !InBuild(d)) continue;
            return string.Format(d.SoonLine, left);
        }
        return null;
    }

    // 열렸는데 아직 알리지 않은 첫 목적지 (없으면 null). 알린 쪽이 기록의 announced를 켠다.
    // 시험용으로 연 것은 알리지 않는다 (기록을 건드리면 저장에 남는다)
    public static Destination PendingNotice(GameState s)
    {
        if (s == null || DebugUnlockAll) return null;
        foreach (var d in All)
        {
            if (d.UnlockTurn <= 0 || !Unlocked(d, s) || !InBuild(d)) continue;
            var rec = s.Record(d.Id, false);
            if (rec == null || !rec.announced) return d;
        }
        return null;
    }

    static readonly ItemCount[] NoReward = new ItemCount[0];

    // 이번에 나올 보상: 지금까지 연 횟수로 줄을 고르고, 표가 끝나면 마지막 줄을 되풀이한다. 보상이 없는 곳은 빈 배열
    public static ItemCount[] RewardFor(Destination d, int clears)
    {
        if (d == null || d.Rewards == null || d.Rewards.Length == 0) return NoReward;
        return d.Rewards[Mathf.Clamp(clears, 0, d.Rewards.Length - 1)] ?? NoReward;
    }
}
