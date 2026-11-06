# 벙커랩 (BUNKER LAB)

핵폭발 이후 방공호에 숨은 주인공이 폐허 도시를 오가며 물자를 모아 200턴을 버티는 1인칭 공포 생존 게임. Team_31

- 엔진: Unity 6.3 (6000.3.x) · URP · Input System
- 플랫폼: PC (Windows)
- 현재 버전: **26.0.2.1** (BETA_VER) — 변경 내역은 [PATCH_NOTES.md](PATCH_NOTES.md), 게임 안에서는 메인 메뉴 맨 아래 **패치노트**

## 바로 플레이하기

[Releases](../../releases)에서 원하는 버전의 `UNIV_GAMEDEV_<버전>.zip`을 받아 압축을 풀고 `UNIV_GAMEDEV.exe`를 실행합니다.

## 프로젝트 열기

> 프로젝트에 Unity MCP(MCP for Unity) 패키지가 Git 주소로 들어 있어서, 프로젝트를 열 PC에는 [Git](https://git-scm.com)이 설치되어 있어야 합니다.

1. Unity Hub에서 이 폴더를 **Add project**로 추가하고 Unity 6000.3.x로 엽니다. (첫 실행 시 `Library` 폴더를 다시 만드느라 몇 분 걸립니다)
2. `Assets/Scenes/MainMenu.unity`를 열고 ▶ 플레이

## 조작

| 키 | 동작 |
|---|---|
| W A S D / 마우스 | 이동 / 시점 |
| Shift / C | 달리기(스테미너 소모) / 앉기 |
| F | 줍기 · 문 · 시설 사용 |
| Q | 손전등 |
| 1~8 · 휠 / 우클릭 / G | 핫바 선택 / 사용 / 버리기 |
| 좌클릭 | 공격 (둔기를 고르면 더 강함) |
| Tab / Esc | 가방 / 일시정지 |

## 폴더 구조

| 경로 | 내용 |
|---|---|
| `Assets/Scripts/Core` | 게임 진행(턴·상태·씬 이동), 저장(`SaveSystem`), 아이템·등급·조합법, 밸런스, 설정 |
| `Assets/Scripts/Player` | 이동·스테미너, 상호작용, 손전등, 공격, 손 모델, 핫바 |
| `Assets/Scripts/Shelter` | 방공호 시설 (출입구·제작대·창고·침대·발전기·조명·라디오), 정전 때 좀비 침입(`ShelterInvasion`·`BlastDoor`) |
| `Assets/Scripts/City` | 도시 탐험 진행, 좀비 4종·약탈자(`Zombie`·`ZombieLooks`), 원거리 투사체, 들개, 잡상인, 해치 |
| `Assets/Scripts/UI` | HUD, 메뉴, 설정, 아이콘, 도입 컷씬, 제작대 화면 |
| `Assets/Scripts/World` | 문, 바닥 아이템(줍기), 아이템 모양, 깜빡이는 조명 |
| `Assets/Scripts/Audio` | 코드로 합성하는 효과음 |
| `Assets/Editor/GameSceneBuilder*.cs` | 씬·프리팹·머티리얼 자동 생성 도구 (공통 / `.Prefabs` / `.Shelter` / `.City` / `.Menu` / `.Cutscene`) |
| `Assets/Resources/Cutscene` | 새 게임 도입 컷씬 사진 3장 (`cut1~3.png`, 1920×1080) |
| `Assets/Resources/ItemIcons` | 2D 아이템 아이콘 (`ItemDatabase.cs`의 `icon:` 이름과 같은 파일명) |
| `Assets/Resources/GameBalance.asset` | **밸런스 수치** (인스펙터에서 수정) |
| `Assets/Resources/PatchNotes.json` | 게임 안 패치노트 (버전별 추가·수정·패치 사항) |
| `Assets/SurvivalInventory` | 등급·아이콘이 있는 새 인벤토리 UI (UI Toolkit). 지금은 `Demo/InventoryDemo` 씬에서만 사용, 자세한 내용은 폴더 안 README |

## 자주 하는 작업

- **밸런스 조정**: `Assets/Resources/GameBalance.asset`을 선택하고 인스펙터에서 숫자를 바꿉니다.
- **아이템·조합법·거래 추가**: `Assets/Scripts/Core/ItemDatabase.cs` (도시에 떨어지게 하려면 같은 파일의 `LootWeights`에도 추가). 새 아이템의 모양은 `Assets/Scripts/World/ItemVisuals.cs`에 추가하며, 없으면 회색 상자로 보입니다.
- **씬 다시 만들기**: 메뉴 `Tools > 핵폭발 생존 > 게임 씬 전체 생성` — MainMenu·Shelter·City 씬과 함께 `Assets/Prefabs`(Player·Zombie·Dog), `Assets/Materials/Game`, `Assets/Settings`의 포스트 프로세싱 프로필, `Assets/Generated` 메시·내비메시, URP 설정, Build Settings 씬 목록을 새로 만듭니다. 이것들을 손으로 고친 내용은 사라지므로, 바꾸고 싶으면 `Assets/Editor/GameSceneBuilder*.cs`를 고치세요. (`GameBalance.asset`은 없을 때만 만들어서 팀이 고친 값은 유지됩니다)
- **컷씬 사진 다시 만들기**: 메뉴 `Tools > 핵폭발 생존 > 컷씬 이미지 생성`. 직접 그린 그림으로 바꾸려면 `Assets/Resources/Cutscene/cut1~3.png`를 같은 이름으로 덮어쓰면 됩니다. 자막은 `Assets/Scripts/UI/UIManager.Cutscene.cs`의 `CutShots`.
- **아이템 아이콘·등급 바꾸기**: 아이콘은 `Assets/Resources/ItemIcons`에 PNG를 넣고 `ItemDatabase.cs`에서 그 아이템의 `icon:` 이름을 적습니다. 등급 색은 `ItemRarityInfo.Color`, 칸 배경은 `Assets/SurvivalInventory/Resources/SurvivalInventory/Rarity`.
- **저장 파일**: `%USERPROFILE%\AppData\LocalLow\DefaultCompany\UNIV_GAMEDEV\save.json` (지우면 처음부터)
- **버전 올리기**: `Edit > Project Settings > Player > Version`을 바꾸고, `Assets/Resources/PatchNotes.json` 맨 위에 그 버전 항목(`added` 추가사항 · `changed` 수정사항 · `patched` 패치사항)을 추가한 뒤 빌드합니다. 메인 메뉴의 패치노트 화면이 이 파일을 읽습니다. GitHub용 `PATCH_NOTES.md`도 같은 내용으로 맞춥니다.

## 사용한 에셋

- 텍스처: [Poly Haven](https://polyhaven.com) (CC0)
- 폰트: [Pretendard](https://github.com/orioncactus/pretendard) v1.3.9 (SIL Open Font License 1.1) — `Assets/Resources/Fonts`, 라이선스 전문은 `Pretendard-LICENSE.txt`
- 효과음: 코드로 합성 (외부 에셋 없음)
