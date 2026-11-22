# lamp_post_01 — 가로등

- 만든 곳: Tripo AI API (Holymolly Ltd), 2026-11-22, 맵 모델링 세션
- 만든 방법: 글 → 3D (text_to_model), 모델 v3.1-20260211, 게임용 저폴리(smart_low_poly), 면 제한 1500, 텍스처 standard (텍스처 모델 v3.5-20260815)
  - 작업 번호: b0a7a989-ae03-4625-b12e-5d779f290e59 (생성, 30크레딧), f828e544-4c4e-4368-94b1-73fa581b3221 (FBX 변환, 5크레딧)
  - 글: "Korean city street lamp post, game prop. Tall slim grey steel pole about 8 meters high, round base flange with bolts, small control box near the bottom, curved arm at the top holding a flat rectangular lamp head. Weathered, rust streaks, dirt. Realistic, no text. Single object, no ground."
  - 빼는 글: "logo, text, letters, sticker, ground plane, base slab, light glow, people"
- 파일
  - `lamp_post_01.fbx`: Tripo FBX 변환 결과 (삼각형 2251개, 텍스처가 안에 들어 있지만 재질은 가져오지 않는다)
  - `lamp_post_01_diff.jpg`: GLB에서 꺼낸 색 텍스처 원본 2048×2048. Unity에는 512로 들여온다 (Docs/텍스처_규칙.md 소품 규칙)
- 라이선스: Tripo API 약관(developers.tripo3d.ai/en/terms, 2025-07-11판) 5.2.2 — 유료 사용자는 만든 결과물의 권리를 가진다. 3.2 — 상업·비상업 용도로 쓸 수 있다. 무료 사용자는 5.2.1에 따라 Tripo가 권리를 가진다. 출처 표기 의무는 없다.

## 품질 4단계 판 — 직접 짠 모양 (2026-11-23)

- 왜: 위 처음 판은 등이 기둥 꼭대기에 바로 붙어 게임 가로등(긴 팔)과 달랐다. 다시 만들 때 그림 → 3D(작업 9df2f9cd, 40크레딧 + FBX 4afd4e86 5크레딧)는 8 m 기둥이 끊겨 버렸다. 사용자 "트럭 1만·가로등 직접"
- 모양: 게임 코드 가로등(GameSceneBuilder.City.cs LampPost)과 같은 치수로 직접 짰다 (`Assets/Editor/GameSceneBuilder.TripoLamp.cs`, 메뉴 '가로등 품질별 판 만들기 (직접 짠 모양)'). 크레딧 0
- 칠: Tripo 그림 편집 (seedream_v5, 작업 bbaa2387-6cca-4c30-99d6-94f6f56595e8, 5크레딧) — 우리 코드 가로등을 옆에서 찍은 사진을 같은 모양의 녹슨 강철 가로등으로 바꾼 그림 `source/lamp_post_01_paint_src.png`. 같은 구도의 카메라로 비춰 부위별로 굽고, 잔무늬는 Poly Haven rusty_metal_02(CC0)의 밝기를 곱했다
- 파일: `Q0_낮음`..`Q3_매우높음/lamp_post_01_q{n}.asset`·`_diff.png`·`.prefab` — 삼각형 120 · 394 · 1,920 · 2,912, 텍스처 256 · 512 · 512 · 1024(HighRes). 앞(+Z) = 팔 쪽, 원점 = 기둥 밑
- 2026-11-23 사용자 통과. 재질 값·도시 넣기는 아직 (사진 Docs/맵모델링_미리보기/10_가로등_트럭)
- 권리 (11-23 사용자 확인): 모든 Tripo 작업은 사용자가 Tripo 유료 구독(Max 플랜)을 결제한 뒤에 했다 (API 크레딧은 구독과 별도 잔액이지만, 만든 때는 유료 구독 중). Tripo 약관(www.tripo3d.ai/terms, 2026-11-13판) 5.2.2 — 유료 구독 중이거나 유료 이용권으로 만든 결과물(Paid Content)은 사용자 것이고 구독이 끝나도 그대로다. 결제 영수증을 보관한다.
