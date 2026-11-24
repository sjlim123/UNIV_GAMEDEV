# player_civilian_01 — 플레이어 (얼굴 있는 집 차림, 도입 컷씬)

- 만든 곳: Tripo AI 웹 스튜디오 (Holymolly Ltd), 2026-11-24, 맵 모델링 세션 (사용자: "컷씬에서는 방호복 차림이 아니기에(집에서 방공호로 이동한다는 스토리 상) 사람 얼굴 모습이 있는 모델링 하나가 필요" → "집 차림 하나로 도입 전부")
- 만든 방법: 글 → 그림 (Nano Banana Pro, T 포즈 템플릿, 1:1, 구독 무료 그림 횟수에서 0크레딧, 4장 중 2번: 30대 초 한국인 남성, 짧은 검은 머리, 연회색 반팔 티셔츠·청바지·흰 운동화·왼손 검은 손목시계) → 고정밀 모델 H3.1 (텍스처 4K, 폴리곤 30만, 울트라 메시, 조명 제거, PBR, 삼각형, 부분별 생성 끔) 55크레딧
  - 작업 번호: 05498afe-2553-4e74-ad01-5168ed354365 (그림 ce1ac788-2b53-49a6-a99e-6222446802ed), 삼각형 297,732개
  - 리토폴로지 (쿼드 5만, 스마트 메시 끔, 결과 44,425 쿼드) 10크레딧 → 자동 리깅 (사람형, Unity Humanoid Compatible, 뼈 67개, 방독면 모델과 같은 이름) 20크레딧. 리깅 FBX 변환 a6d2e102-c186-4731-b98f-615f497cb955
  - 합계 85크레딧
- 받은 것: `player_civilian_01_tex.fbx` + 색·노멀 4K (30만 원본), `_리깅5만/player_civilian_01_rig50k.fbx` (Humanoid 아바타) + 색 텍스처
- 다듬기 (편집기 도구, 방독면 모델과 같은 도구에 모델 이름만 바꿔):
  - `Assets/Editor/PlayerModelTools.cs` '플레이어 모델: 얼굴 모델(집 차림) 가중치 옮기기' (평균 1.43 mm, 최대 13.3 mm) → `player_civilian_01_q3_skinned.asset`
  - `Assets/Editor/GameSceneBuilder.PlayerModel.cs` '플레이어 모델: 얼굴 모델(집 차림) 품질 4단계': 매우 높음 297,732 · 높음 88,846 · 중간 24,000 · 낮음 6,000 (텍스처 2048 · 1024 · 512 · 256) → `Q0_낮음..Q3_매우높음/`, `player_civilian_01.prefab`
  - `Assets/Editor/GameSceneBuilder.PlayerShadow.cs` '컷씬 얼굴 몸(집 차림) 프리팹 만들기' → `Assets/Resources/Player/player_civilian_body.prefab`
- 쓰는 곳: 도입 컷씬 장면 0~3의 주인공 (CutsceneActor 집 차림, CharacterModels.Player.cs). 얼굴 클로즈업이 있어 높음 이상 판을 쓴다. 입 가리기는 코끝 아래 입술 겉면을 찾아 손바닥을 댄다
- 미리보기·사진: Docs/맵모델링_미리보기/15_플레이어_모델/그림_얼굴, 얼굴모델
- 권리: 사용자가 Tripo 유료 구독(Max 플랜)을 결제한 뒤 웹 스튜디오(studio.tripo3d.ai)에서 그 구독 크레딧으로 만들었다. Tripo 약관(www.tripo3d.ai/terms, 2026-11-13판) 5.2.2 — 유료 구독 중에 만든 결과물(Paid Content)은 사용자 것이고 구독이 끝나도 그대로다. 출처 표기 의무 없음.
