# ac_unit_01 — 에어컨 실외기

- 만든 곳: Tripo AI API (Holymolly Ltd), 2026-11-22, 맵 모델링 세션
- 만든 방법: 글 → 3D (text_to_model), 모델 v3.1-20260211, 게임용 저폴리(smart_low_poly), 면 제한 1500, 텍스처 standard (텍스처 모델 v3.5-20260815)
  - 작업 번호: b0a47db2-1d90-4f64-b623-cbf4fc315135 (생성, 30크레딧), 42a844cd-5d43-4ded-965f-46fad9e55954 (FBX 변환, 5크레딧)
  - 글: "Outdoor air conditioner condenser unit from a Korean apartment, game prop. Off-white metal box casing with one large round fan grille on the front, side vents, pipe connectors on the right side, dirty, weathered, rust streaks. Realistic, no text or logo. Single object, no ground."
  - 빼는 글: "logo, brand, text, letters, sticker, wall, bracket, ground plane, base, people"
- 파일
  - `ac_unit_01.fbx`: Tripo FBX 변환 결과 (삼각형 3076개, 텍스처가 안에 들어 있지만 재질은 가져오지 않는다)
  - `ac_unit_01_diff.jpg`: GLB에서 꺼낸 색 텍스처 원본 2048×2048. Unity에는 512로 들여온다 (Docs/텍스처_규칙.md 소품 규칙)
- 라이선스: Tripo API 약관(developers.tripo3d.ai/en/terms, 2025-07-11판) 5.2.2 — 유료 사용자는 만든 결과물의 권리를 가진다. 3.2 — 상업·비상업 용도로 쓸 수 있다. 무료 사용자는 5.2.1에 따라 Tripo가 권리를 가진다. 출처 표기 의무는 없다.
- 정한 재질 값 (2026-11-22 사용자 통과, 미리보기 Docs/맵모델링_미리보기/02_드럼통_실외기_소화전): URP Lit, _BaseMap = 이 폴더의 색 텍스처, _BaseColor (0.45, 0.45, 0.45), _Metallic 0.2, _Smoothness 0.25 (팬이 앞(+Z), 뒷면을 벽에 붙이고 코드 브래킷은 그대로). 방향: 원본 FBX에 Y축 90° 더 돌려야 코드 소품과 같은 쪽을 본다
