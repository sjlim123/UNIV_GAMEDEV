# shelter_chair_01 — 방공호 나무 의자

- 만든 곳: Tripo AI 웹 스튜디오 (Holymolly Ltd), 2026-11-23, 맵 모델링 세션 (사용자 '1차 6개로', 승인 한도 450크레딧)
- 만든 방법: 글 → 그림 (Nano Banana Pro, 1:1, 4장, 구독 무료 그림 횟수에서 0크레딧) → 고른 그림: 의자 2번 (세로 살 등받이) → 고정밀 모델 H3.1 (텍스처 2K, 울트라 메시, 조명 제거, PBR, 삼각형) 45크레딧
  - 작업 번호: cc624b07-ad96-4e7f-a6b1-9f1cd3bfdd64
  - 원본 삼각형 16,356개. 방향: 들여온 FBX(맨 위 X -90°)를 Y축으로 돌려 코드 가구와 같은 축 (앞 +Z 그대로)
- 받은 것: FBX(Blender 설정, 2K) zip에서 FBX와 색 텍스처(basecolor)만 `Q3_매우높음/shelter_chair_01_q3.fbx`·`shelter_chair_01_q3_diff.jpg`로 (노멀·금속성·거칠기 텍스처는 쓰지 않는다). 재질은 가져오지 않고 판마다 `M_TR_shelter_chair_01_q0..q3` (URP Lit)
- 품질 4단계: 낮음 300 · 중간 1,200 · 높음 5,000 · 매우 높음 16,356. 도구 `Assets/Editor/GameSceneBuilder.TripoTiers.cs` 메뉴 'Tripo 품질별 판 만들기 (방공호 가구 6종)'
- 방공호 넣기: `Assets/Editor/GameSceneBuilder.TripoShelter.cs` (의자 전부를 바꿈, 높이로 맞춤). 미리보기·사진 Docs/맵모델링_미리보기/13_방공호_가구
- 2026-11-23 사용자 통과 (품질 4단계, 방공호 넣기)
- 도시 재사용 (2026-11-24, 사용자 "1차: 재사용+상위 4종"): 2026-11-24부터 도시 2층 방 의자 24개에도. `Assets/Editor/GameSceneBuilder.TripoShelter.cs` / `GameSceneBuilder.TripoCity2.cs`
- 권리: 사용자가 Tripo 유료 구독(Max 플랜)을 결제한 뒤 웹 스튜디오(studio.tripo3d.ai)에서 그 구독 크레딧으로 만들었다. Tripo 약관(www.tripo3d.ai/terms, 2026-11-13판) 5.2.2 — 유료 구독 중에 만든 결과물(Paid Content)은 사용자 것이고 구독이 끝나도 그대로다. 출처 표기 의무 없음.
