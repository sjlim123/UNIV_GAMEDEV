using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

// 조작키: 설정 → 컨트롤에서 바꾼다 (PlayerPrefs "key_<동작>"에 저장). 키보드 키와 마우스 버튼 모두 쓸 수 있다
public enum GameAction
{
    MoveForward, MoveBack, MoveLeft, MoveRight, Run, Crouch, Jump,
    Attack, UseItem, Interact, Flashlight, Inventory, DropItem,
    Slot1, Slot2, Slot3, Slot4, Slot5, Slot6, Slot7, Slot8,
}

public static class KeyBindings
{
    // 마우스 버튼은 Key 값 뒤쪽 번호로 저장한다
    public const int MouseLeft = 1000, MouseRight = 1001, MouseMiddle = 1002, MouseBack = 1003, MouseForward = 1004;

    public static readonly GameAction[] All = (GameAction[])System.Enum.GetValues(typeof(GameAction));

    static readonly Dictionary<GameAction, int> defaults = new Dictionary<GameAction, int>
    {
        { GameAction.MoveForward, (int)Key.W }, { GameAction.MoveBack, (int)Key.S },
        { GameAction.MoveLeft, (int)Key.A }, { GameAction.MoveRight, (int)Key.D },
        { GameAction.Run, (int)Key.LeftShift }, { GameAction.Crouch, (int)Key.C }, { GameAction.Jump, (int)Key.Space },
        { GameAction.Attack, MouseLeft }, { GameAction.UseItem, MouseRight },
        { GameAction.Interact, (int)Key.F }, { GameAction.Flashlight, (int)Key.Q },
        { GameAction.Inventory, (int)Key.E }, { GameAction.DropItem, (int)Key.G },
        { GameAction.Slot1, (int)Key.Digit1 }, { GameAction.Slot2, (int)Key.Digit2 }, { GameAction.Slot3, (int)Key.Digit3 }, { GameAction.Slot4, (int)Key.Digit4 },
        { GameAction.Slot5, (int)Key.Digit5 }, { GameAction.Slot6, (int)Key.Digit6 }, { GameAction.Slot7, (int)Key.Digit7 }, { GameAction.Slot8, (int)Key.Digit8 },
    };

    static readonly Dictionary<GameAction, int> current = new Dictionary<GameAction, int>();
    static bool loaded;

    public static string ActionName(GameAction a)
    {
        switch (a)
        {
            case GameAction.MoveForward: return "앞으로";
            case GameAction.MoveBack: return "뒤로";
            case GameAction.MoveLeft: return "왼쪽";
            case GameAction.MoveRight: return "오른쪽";
            case GameAction.Run: return "달리기";
            case GameAction.Crouch: return "앉기";
            case GameAction.Jump: return "점프";
            case GameAction.Attack: return "공격";
            case GameAction.UseItem: return "아이템 사용";
            case GameAction.Interact: return "상호작용";
            case GameAction.Flashlight: return "손전등";
            case GameAction.Inventory: return "소지품 열기";
            case GameAction.DropItem: return "아이템 버리기";
            default: return $"핫바 {(int)a - (int)GameAction.Slot1 + 1}번";
        }
    }

    public static int Get(GameAction a)
    {
        Load();
        return current.TryGetValue(a, out var v) ? v : defaults[a];
    }

    public static void Set(GameAction a, int code)
    {
        Load();
        // 다른 동작이 같은 키를 쓰고 있었으면 서로 바꾼다
        foreach (var other in All)
            if (other != a && Get(other) == code)
            {
                current[other] = Get(a);
                PlayerPrefs.SetInt("key_" + other, current[other]);
            }
        current[a] = code;
        PlayerPrefs.SetInt("key_" + a, code);
        PlayerPrefs.Save();
    }

    public static void ResetAll()
    {
        current.Clear();
        foreach (var a in All) PlayerPrefs.DeleteKey("key_" + a);
        PlayerPrefs.Save();
    }

    static void Load()
    {
        if (loaded) return;
        loaded = true;
        foreach (var a in All)
            if (PlayerPrefs.HasKey("key_" + a)) current[a] = PlayerPrefs.GetInt("key_" + a);
    }

    // 설정 초기화(PlayerPrefs 전체 삭제) 뒤 다시 읽게 한다
    public static void Reload()
    {
        loaded = false;
        current.Clear();
    }

    public static bool Held(GameAction a) => Control(Get(a))?.isPressed ?? false;
    public static bool Pressed(GameAction a) => Control(Get(a))?.wasPressedThisFrame ?? false;

    static ButtonControl Control(int code)
    {
        if (code >= MouseLeft)
        {
            var m = Mouse.current;
            if (m == null) return null;
            switch (code)
            {
                case MouseLeft: return m.leftButton;
                case MouseRight: return m.rightButton;
                case MouseMiddle: return m.middleButton;
                case MouseBack: return m.backButton;
                default: return m.forwardButton;
            }
        }
        var kb = Keyboard.current;
        if (kb == null || code <= 0) return null;
        return kb[(Key)code];
    }

    // 화면에 보여 줄 키 이름 (예: "E", "Shift", "좌클릭")
    public static string Label(GameAction a) => Label(Get(a));

    public static string Label(int code)
    {
        switch (code)
        {
            case MouseLeft: return "좌클릭";
            case MouseRight: return "우클릭";
            case MouseMiddle: return "휠클릭";
            case MouseBack: return "마우스4";
            case MouseForward: return "마우스5";
        }
        var key = (Key)code;
        switch (key)
        {
            case Key.LeftShift: return "Shift";
            case Key.RightShift: return "오른쪽 Shift";
            case Key.LeftCtrl: return "Ctrl";
            case Key.RightCtrl: return "오른쪽 Ctrl";
            case Key.LeftAlt: return "Alt";
            case Key.Space: return "Space";
            case Key.Tab: return "Tab";
            case Key.CapsLock: return "CapsLock";
            case Key.Enter: return "Enter";
            case Key.Backspace: return "Backspace";
            case Key.UpArrow: return "↑";
            case Key.DownArrow: return "↓";
            case Key.LeftArrow: return "←";
            case Key.RightArrow: return "→";
        }
        if (key >= Key.Digit1 && key <= Key.Digit9) return ((int)key - (int)Key.Digit1 + 1).ToString();
        if (key == Key.Digit0) return "0";
        if (key >= Key.Numpad0 && key <= Key.Numpad9) return "숫자패드 " + ((int)key - (int)Key.Numpad0);
        string s = key.ToString();
        return s.Length == 1 ? s.ToUpperInvariant() : s;
    }

    // 키 바꾸기 창에서 지금 눌린 키·버튼 (없으면 -1). Esc는 취소용이라 돌려주지 않는다
    public static int ReadAnyPressed()
    {
        var m = Mouse.current;
        if (m != null)
        {
            if (m.leftButton.wasPressedThisFrame) return MouseLeft;
            if (m.rightButton.wasPressedThisFrame) return MouseRight;
            if (m.middleButton.wasPressedThisFrame) return MouseMiddle;
            if (m.backButton.wasPressedThisFrame) return MouseBack;
            if (m.forwardButton.wasPressedThisFrame) return MouseForward;
        }
        var kb = Keyboard.current;
        if (kb == null) return -1;
        foreach (var k in kb.allKeys)
            if (k.wasPressedThisFrame && k.keyCode != Key.Escape && k.keyCode != Key.None) return (int)k.keyCode;
        return -1;
    }
}
