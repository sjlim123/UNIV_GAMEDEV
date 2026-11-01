# Survival Inventory (UI Toolkit)

천, 붕대, 연료통, 배터리, 손전등, 가방, 고철 7종을 담는 인벤토리 화면입니다.
Unity 2022.3 LTS 이상 / Unity 6에서 동작하도록 만들었고, 새 Input System과 기존 Input Manager 둘 다 지원합니다.

## 바로 테스트하기

1. `SurvivalInventory` 폴더를 통째로 프로젝트의 `Assets` 안에 넣습니다.
2. 컴파일이 끝나면 상단 메뉴 **Tools → Survival Inventory → 2. 현재 씬에 인벤토리 UI 배치** 를 누릅니다.
   - 아이템 7종 에셋(`Items/`), Panel Settings, 테마가 없으면 자동으로 만들고
   - 씬에 `Inventory UI` 오브젝트를 만들어 시작 아이템까지 채워 넣습니다.
3. Play를 누르면 인벤토리가 열린 상태로 시작합니다.

## 조작

| 조작 | 동작 |
|---|---|
| 칸 클릭 | 오른쪽에 상세 정보 표시 |
| 끌어서 놓기 | 칸 이동 (같은 아이템이면 합쳐짐) |
| 우클릭 | 바로 사용 (사용 가능한 아이템만) |
| I 또는 Tab | 열기 / 닫기 |
| Esc | 닫기 |
| 상단 탭 | 재료·의료·자원·도구·장비 분류만 강조 |
| 정렬 | 분류 → 이름 순 정렬, 나뉜 묶음 합치기 |

## 한글이 네모(□)로 보일 때

Unity 기본 폰트에는 한글이 없는 경우가 많습니다.
Noto Sans KR 같은 한글 폰트 파일(.ttf/.otf)을 프로젝트에 넣고,
`Inventory UI` 오브젝트의 **InventoryUI → Korean Font** 칸에 끌어다 놓으면 됩니다.

## 파일 구성

```
SurvivalInventory/
├─ Scripts/
│  ├─ ItemData.cs       아이템 정의(ScriptableObject) + 분류 이름/색
│  ├─ Inventory.cs      데이터 로직: 추가, 제거, 이동, 정렬, 무게 제한, 이벤트
│  └─ InventoryUI.cs    화면: 칸 그리드, 상세 패널, 드래그, 탭, 무게 막대, 알림
├─ Icons/                 아이템 그림 7종 (512×512 투명 PNG), source/ 에 SVG 원본
├─ Resources/SurvivalInventory/Rarity/   등급별 칸 배경 6종
├─ UI/
│  ├─ Inventory.uxml    레이아웃
│  └─ Inventory.uss     스타일
└─ Editor/
   └─ SurvivalInventorySetup.cs   메뉴: 아이템 생성, 씬 배치
```

## 게임에 연결하기

UI와 데이터가 분리되어 있어서, 게임 쪽에서는 `Inventory`만 다루면 됩니다.

> **이 게임 프로젝트(UNIV_GAMEDEV)에서 쓸 때 주의**: 게임에도 같은 이름의 `Inventory`, `ItemCategory`가 있어서 `using SurvivalInventory;`만 붙이면 게임 쪽 타입이 먼저 잡혀 컴파일 오류가 납니다. 아래 예제의 타입은 `SurvivalInventory.Inventory`, `SurvivalInventory.ItemData`처럼 전체 이름으로 쓰거나 `using SInventory = SurvivalInventory.Inventory;`처럼 별칭을 쓰세요. 테스트는 게임 씬이 아닌 `Demo/InventoryDemo` 씬에서 하세요 (게임 씬에 넣으면 Tab·Esc 키가 게임 UI와 겹칩니다).

```csharp
public class PlayerItemHandler : MonoBehaviour
{
    [SerializeField] Inventory inventory;
    [SerializeField] ItemData battery;

    void OnEnable()
    {
        inventory.ItemUsed += OnItemUsed;
        inventory.ItemDropped += (item, count) => { /* 월드에 아이템 떨구기 */ };
    }

    void OnItemUsed(ItemData item)
    {
        switch (item.id)
        {
            case "bandage":    /* 체력 회복 */ break;
            case "flashlight": /* 배터리 확인 후 켜기 */
                if (inventory.CountOf(battery) == 0) { /* 배터리 없음 */ }
                break;
            case "bag":        inventory.MaxWeight += 15f; break; // 예시
        }
    }
}
```

- 줍기: `inventory.Add(item, 개수)` → 실제로 들어간 개수를 돌려줌 (칸·무게 부족 시 일부만)
- 제작 재료 소모: `inventory.Remove(cloth, 3)` → 모자라면 false, 아무것도 안 뺌
- 개수 확인: `inventory.CountOf(item)`

## 등급

| 등급 | 색 | 기본 아이템 |
|---|---|---|
| 신화 | 빨강 | |
| 전설 | 노랑 | |
| 영웅 | 보라 | |
| 고급 | 초록 | |
| 희귀 | 파랑 | 연료통, 가방 |
| 일반 | 회색 | 천, 붕대, 손전등, 배터리, 고철 |

- 아이템 에셋의 `Rarity` 값만 바꾸면 칸 배경, 상세 패널의 등급 표시, 이름 색이 함께 바뀝니다.
- 칸 배경 그림은 `Resources/SurvivalInventory/Rarity/` 에 있습니다 (common, rare, advanced, heroic, legendary, mythic).
- 아이템 그림(`Icons/`)은 배경이 투명해서 어떤 등급에도 그대로 쓸 수 있습니다.

## 아이템 추가 / 아이콘 넣기

- Project 창 우클릭 → **Create → Survival Inventory → Item Data** 로 새 아이템을 만듭니다.
- 기본 7종은 `Icons/` 폴더의 그림이 자동으로 연결됩니다. 이전 버전으로 이미 아이템을 만들었다면 메뉴 **1. 기본 아이템 7종 만들기 · 갱신**을 한 번 더 누르면 이름·등급·아이콘이 최신으로 바뀝니다 (설명과 수치는 그대로).
- 예전 버전으로 배치한 `Inventory UI`는 시작 아이템 목록이 씬에 저장되어 있어서, 새로 생긴 기본 아이템(예: 고철)이 빠져 있을 수 있습니다. 메뉴 1을 누르면 콘솔에 알려 주고, 메뉴 **3. 열린 씬의 시작 아이템을 기본 7종에 맞추기**를 누르면 빈 칸(Missing)을 지우고 빠진 기본 아이템을 채웁니다. Ctrl+Z로 되돌릴 수 있고, 일부러 뺀 아이템이 있다면 메뉴 3 대신 직접 고치세요.
- 새 아이템은 `Icon` 칸에 스프라이트를 넣으면 됩니다 (텍스처 타입 Sprite). 칸을 꽉 채우는 512×512 정사각형 그림을 권장합니다.
- 아이콘 원본 SVG는 `Icons/source/`에 있어서 색이나 모양을 고칠 수 있습니다.
- 새 분류가 필요하면 `ItemData.cs`의 `ItemCategory`와 `ItemCategoryInfo`에 한 줄씩 추가하면 탭이 자동으로 생깁니다.
- 칸 수, 최대 무게는 `Inventory` 컴포넌트 인스펙터에서 바꿉니다. (6열 기준이라 24, 30, 36 등을 권장)
