# player_survivor_02 — 플레이어 (방독면 탐험 차림)

- 만든 곳: Tripo AI 웹 스튜디오 (Holymolly Ltd), 2026-11-23~24, 맵 모델링 세션 (사용자 "플레이어 모델링 업그레이드": 모델링·리깅 모두 Tripo, 폴리곤 30만, 인체 구조를 생각하며)
- 만든 방법: 글 → 그림 (Nano Banana Pro, T 포즈 템플릿, 구독 무료 그림 횟수에서 0크레딧, 그림 19번: 올리브 후드 야상·검은 전면 방독면(렌즈 불투명)·옆 필터·작업 장갑·카고 바지·군화, 얼굴 없음) → 고정밀 모델 H3.1 (텍스처 4K, 폴리곤 30만, 울트라 메시, 조명 제거, PBR, 삼각형) 55크레딧
  - 작업 번호: 7772ed90-d5a0-4091-be7a-4c1945806606, 삼각형 291,841개
  - 웹 자동 리깅은 30만 면을 '폴리곤 한도 초과'로 거절해 리토폴로지(쿼드 5만, 스마트 메시 끔) 10크레딧 → 자동 리깅(사람형, Unity Humanoid Compatible, 뼈 67개) 20크레딧. 리깅 FBX 변환 a60f647e-553d-4e14-91e8-a56948a8528a
  - 합계 85크레딧 (그 전 그림 11번 A자세 모델 55크레딧·부분별 생성 무료 체험은 T자세로 다시 만들어 쓰지 않음, 저장소에 넣지 않음)
- 받은 것: `player_survivor_02_tex.fbx` + 색·노멀 4K (30만 원본), `_리깅5만/player_survivor_02_rig50k.fbx` (Humanoid 아바타) + 색 텍스처
- 다듬기 (편집기 도구):
  - `Assets/Editor/PlayerModelTools.cs` '플레이어 모델: Tripo 가중치를 30만 메시로 옮기기': 리깅 메시에서 같은 쪽을 보는 가장 가까운 면의 가중치를 30만 꼭짓점으로 (평균 0.76 mm) → `player_survivor_02_q3_skinned.asset`
  - `Assets/Editor/GameSceneBuilder.PlayerModel.cs` '플레이어 모델: 마스크 아래 빨간 곳 칠하기 + 품질 4단계': 방독면 아래 비치던 빨간 살을 어둡게, 품질 4단계 매우 높음 291,841 · 높음 94,737 · 중간 23,999 · 낮음 5,999 (텍스처 2048 · 1024 · 512 · 256) → `Q0_낮음..Q3_매우높음/`, `player_survivor_02.prefab`
  - `Assets/Editor/GameSceneBuilder.PlayerShadow.cs` '그림자 몸 프리팹 만들기' (팔을 가늘게 한 q0·q1 → `Assets/Resources/Player/player_shadow_body.prefab`), '컷씬 몸 프리팹 만들기' (`Assets/Resources/Player/player_body.prefab`)
  - `Assets/Editor/GameSceneBuilder.PlayerArms.cs` '1인칭 팔 만들기' (아래팔·장갑 손만 잘라 HandRig 뼈대에 묶음, 팔 조각 아틀라스 2048·1024 → `_1인칭팔/`, `Assets/Resources/Player/player_fp_arms.asset`)
- 쓰는 곳: 플레이어 그림자 몸(PlayerShadowBody), 1인칭 팔(HandRig), 방공호 해치 귀환 연출의 주인공(CutsceneActor 탐험 차림, CharacterModels.Player.cs)
- 미리보기·사진: Docs/맵모델링_미리보기/15_플레이어_모델 (사용자 통과: 품질 단계 11-24, 그림자 몸 11-24, 1인칭 팔 11-24, 컷씬 11-24)
- 권리: 사용자가 Tripo 유료 구독(Max 플랜)을 결제한 뒤 웹 스튜디오(studio.tripo3d.ai)에서 그 구독 크레딧으로 만들었다. Tripo 약관(www.tripo3d.ai/terms, 2026-11-13판) 5.2.2 — 유료 구독 중에 만든 결과물(Paid Content)은 사용자 것이고 구독이 끝나도 그대로다. 출처 표기 의무 없음.
