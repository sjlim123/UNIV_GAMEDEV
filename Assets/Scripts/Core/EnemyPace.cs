using UnityEngine;

// 감염자·들개의 추격 속도 성장 (난이도 밸런스): 초반에는 아주 느리게 달려오고, GameBalance.paceStageTurns의 턴(25/45/75/105/135/155/170)이
// 될 때마다 한 단계씩 빨라져 마지막 턴부터는 원래 속도(GameBalance의 EnemyStats.speed)로 달린다. 그 뒤(170~200턴)는 변하지 않는다.
// 시작 배율은 몸집 순으로 다르다: 들개(가장 작음)가 가장 덜 느리고, 약탈자 > 감염자 > 원거리 > 가죽 > 거대(가장 둔함) 순.
// Zombie·Animal이 추격 속도에 Multiplier를 곱한다. 배회·수색 속도는 그대로다.
// 단계가 오르기 paceWarnTurns턴 전부터 방공호 출입구·침대 창과 돌아왔을 때 문구(Warning)로 알린다.
public static class EnemyPace
{
    // 지금 턴까지 지난 단계 수 (0 = 첫 단계 전, 단계 수 = 마지막 단계 이후)
    public static int Stage(int turn)
    {
        var turns = GameBalance.Data.paceStageTurns;
        if (turns == null || turns.Length == 0) return 0;
        int k = 0;
        foreach (int t in turns) if (turn >= t) k++;
        return k;
    }

    // 0~1: 첫 단계 전 0, 마지막 단계부터 1 (단계마다 고르게 오른다)
    public static float Progress(int turn)
    {
        var turns = GameBalance.Data.paceStageTurns;
        if (turns == null || turns.Length == 0) return 1f;
        return Mathf.Clamp01((float)Stage(turn) / turns.Length);
    }

    // 지금 턴의 추격 속도 배율 (게임이 시작되지 않았으면 1)
    public static float Multiplier(ZombieKind kind) => Multiplier(StartFor(kind));
    public static float DogMultiplier() => Multiplier(GameBalance.Data.paceStartDog);

    static float Multiplier(float start)
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.State == null) return 1f;
        return Mathf.Lerp(Mathf.Clamp01(start), 1f, Progress(gm.State.Turn));
    }

    static float StartFor(ZombieKind kind)
    {
        var b = GameBalance.Data;
        switch (kind)
        {
            case ZombieKind.Raider: return b.paceStartRaider;
            case ZombieKind.Ranged: return b.paceStartRanged;
            case ZombieKind.Leather: return b.paceStartLeather;
            case ZombieKind.Heavy: return b.paceStartHeavy;
            default: return b.paceStartZombie;
        }
    }

    // 다음 단계까지 남은 턴 (더 오를 단계가 없으면 -1)
    public static int TurnsToNextStage(int turn)
    {
        var turns = GameBalance.Data.paceStageTurns;
        if (turns == null) return -1;
        foreach (int t in turns) if (turn < t) return t - turn;
        return -1;
    }

    // 방공호 창(출입구·침대)에 띄울 경고 줄. 경고할 때가 아니면 null
    public static string Warning(int turn)
    {
        var b = GameBalance.Data;
        int left = TurnsToNextStage(turn);
        if (left < 0 || left > b.paceWarnTurns) return null;
        bool last = Stage(turn) + 1 >= b.paceStageTurns.Length;
        string when = left <= 1 ? "다음 턴부터" : $"{left}턴 뒤부터";
        return last
            ? $"! {when} 감염자들이 원래 속도로 달려든다. 밖은 가장 위험해진다."
            : $"! {when} 감염자들이 더 빨리 달려든다. 서두르자.";
    }

    // 방공호로 돌아왔을 때 문구 뒤에 덧붙일 줄 ("" 또는 줄바꿈 + 경고)
    public static string ArrivalNote(int turn)
    {
        string w = Warning(turn);
        return w == null ? "" : "\n" + w;
    }
}
