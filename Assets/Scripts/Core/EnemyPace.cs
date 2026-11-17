using UnityEngine;

// 감염자·들개의 추격 속도 성장 (난이도 밸런스): 초반에는 아주 느리게 달려오고, GameBalance.paceStageTurns의 턴(12/22/37/52/67/77/85)이
// 될 때마다 한 단계씩 빨라져 마지막 턴부터는 원래 속도(GameBalance의 EnemyStats.speed)로 달린다. 그 뒤(85~100턴)는 변하지 않는다.
// 시작 배율은 몸집 순으로 다르다: 들개(가장 작음)가 가장 덜 느리고, 약탈자 > 감염자 > 원거리 > 가죽 > 거대(가장 둔함) 순.
// Zombie·Animal이 추격 속도에 Multiplier를 곱한다. 배회·수색 속도는 그대로다.
// 공격력도 같은 날에 한 단계씩 오른다: 첫 단계 전 1배 → 마지막 단계부터 GameBalance.paceDamageMax배 (DamageMultiplier. 적 6종 모두).
// 단계가 오르기 paceWarnTurns턴 전부터 방공호 출입구·침대 창과 돌아왔을 때 문구로 알린다 (Warning · ArrivalNote).
// 출입구·침대 창의 같은 줄에 마지막 며칠의 안내(LastDaysNotice)도 띄운다.
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

    // 지금 날짜의 공격력 배율: 첫 단계 전 1배, 단계마다 고르게 올라 마지막 단계부터 paceDamageMax배 (게임이 시작되지 않았으면 1).
    // 탐험 안에서 시간이 갈수록 붙는 배율(감염자 1 + 0.3 × 위험도, 들개 1 + 0.2 × 위험도)과 곱해진다
    public static float DamageMultiplier()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.State == null) return 1f;
        return DamageMultiplier(gm.State.Turn);
    }

    public static float DamageMultiplier(int turn) => Mathf.Lerp(1f, Mathf.Max(1f, GameBalance.Data.paceDamageMax), Progress(turn));

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

    // 방공호 창(출입구·침대)에 늘 띄워 두는 경고 줄: 추격 속도가 오르기 전의 경고, 또는 마지막 며칠의 안내. 해당하는 날이 아니면 null
    // (둘은 겹치지 않는다: 속도 경고는 마지막 단계 전까지, 마지막 날 안내는 끝나기 이틀 전부터)
    public static string Warning(int turn) => PaceWarning(turn) ?? LastDaysNotice(turn);

    // 추격 속도(와 공격력)가 한 단계 오르기 paceWarnTurns일 전부터의 경고. 경고할 때가 아니면 null
    public static string PaceWarning(int turn)
    {
        var b = GameBalance.Data;
        int left = TurnsToNextStage(turn);
        if (left < 0 || left > b.paceWarnTurns) return null;
        bool last = Stage(turn) + 1 >= b.paceStageTurns.Length;
        string when = left <= 1 ? "내일부터" : $"{left}일 뒤부터";
        if (last) return $"! {when} 감염자들이 원래 속도로 달려든다. 밖은 가장 위험해진다.";
        return b.paceDamageMax > 1f
            ? $"! {when} 감염자들이 더 빠르고 사나워진다. 서두르자."
            : $"! {when} 감염자들이 더 빨리 달려든다. 서두르자.";
    }

    // 마지막 며칠의 안내. 하루가 지나는 일(나가기·돌아오기·잠자기)을 마지막 날에 하면 그 자리에서 끝나므로
    // 탐험은 마지막 날 하루 전까지만 나갈 수 있다 (그 탐험이 트럭에 부품을 넣을 마지막 기회). 해당하는 날이 아니면 null
    public static string LastDaysNotice(int turn)
    {
        int max = GameState.MaxTurns;
        if (turn >= max) return $"! 오늘이 마지막 날이다. 출발하거나 잠들면 그 자리에서 {max}일이 끝난다.";
        if (turn == max - 1) return "! 내일이 마지막 날이다. 트럭에 부품을 넣을 탐험은 이번이 마지막이다.\n  자고 나면 더는 나갈 수 없다.";
        if (turn == max - 2) return "! 모레가 마지막 날이다. 탐험은 한 번만 더 나갈 수 있다.";
        return null;
    }

    // 방공호로 돌아왔을 때 문구 뒤에 덧붙일 줄 ("" 또는 줄바꿈 + 추격 속도 경고). 마지막 날 안내는 창에만 띄운다
    public static string ArrivalNote(int turn)
    {
        string w = PaceWarning(turn);
        return w == null ? "" : "\n" + w;
    }
}
