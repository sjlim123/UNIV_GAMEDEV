# shelter_bed_01 — 방공호 철제 침대

- 만든 곳: Tripo AI 웹 스튜디오 (Holymolly Ltd), 2026-11-23, 맵 모델링 세션 (사용자 '1차 6개로', 승인 한도 450크레딧)
- 만든 방법: 글 → 그림 (Nano Banana Pro, 1:1, 4장, 구독 무료 그림 횟수에서 0크레딧) → 고른 그림: 침대 3번 (얇은 군용 철제 침대, 베개, 접은 담요) → 고정밀 모델 H3.1 (텍스처 2K, 울트라 메시, 조명 제거, PBR, 삼각형) 45크레딧
  - 작업 번호: b7984717-9042-402b-98d7-d76fc77a3819
  - 원본 삼각형 17,381개. 방향: 들여온 FBX(맨 위 X -90°)를 Y축으로 돌려 코드 가구와 같은 축 (머리판 +Z로 180°)
- 받은 것: FBX(Blender 설정, 2K) zip에서 FBX와 색 텍스처(basecolor)만 `Q3_매우높음/shelter_bed_01_q3.fbx`·`shelter_bed_01_q3_diff.jpg`로 (노멀·금속성·거칠기 텍스처는 쓰지 않는다). 재질은 가져오지 않고 판마다 `M_TR_shelter_bed_01_q0..q3` (URP Lit)
- 품질 4단계: 낮음 1,100 · 중간 2,199 · 높음 8,699 · 매우 높음 17,381 (낮음 550에서는 다리가 사라져 1,100, 작은 조각 빼기 끔). 도구 `Assets/Editor/GameSceneBuilder.TripoTiers.cs` 메뉴 'Tripo 품질별 판 만들기 (방공호 가구 6종)'
- 방공호 넣기: `Assets/Editor/GameSceneBuilder.TripoShelter.cs` (틀·다리·머리판·매트리스·베개·담요를 바꿈, 길이 2 m로 맞춤, 콜라이더·Bed 그대로). 미리보기·사진 Docs/맵모델링_미리보기/13_방공호_가구
- 2026-11-23 사용자 통과 (품질 4단계, 방공호 넣기)
- 권리: 사용자가 Tripo 유료 구독(Max 플랜)을 결제한 뒤 웹 스튜디오(studio.tripo3d.ai)에서 그 구독 크레딧으로 만들었다. Tripo 약관(www.tripo3d.ai/terms, 2026-11-13판) 5.2.2 — 유료 구독 중에 만든 결과물(Paid Content)은 사용자 것이고 구독이 끝나도 그대로다. 출처 표기 의무 없음.
