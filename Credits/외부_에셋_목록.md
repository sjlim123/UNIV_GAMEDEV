# 벙커랩 (BUNKER LAB) — 외부 에셋 목록

갱신: 2026-11-03 · 항목 10개

> 게임에 쓰인 외부(직접 만들지 않은) 에셋 전부. 이 JSON이 원본이고, 같은 폴더의 .md와 .pdf는 build_credits.py로 만든다. 새 에셋을 쓰면 여기에 한 항목을 더하고 스크립트를 다시 돌린다. CC0는 출처 표기 의무가 없고 SIL OFL은 글꼴 파일을 그대로 넣을 때 라이선스 문서를 같이 두면 된다.

## 라이선스 요약

| 약칭 | 이름 | 뜻 | 원문 |
|---|---|---|---|
| CC0 | Creative Commons CC0 1.0 (퍼블릭 도메인 기증) | 표기 없이 상업용 포함 자유롭게 사용·수정 가능 | https://creativecommons.org/publicdomain/zero/1.0/ |
| OFL | SIL Open Font License 1.1 | 게임에 내장·배포 가능, 글꼴 자체를 단독 판매만 금지, 라이선스 문서 동봉 | https://openfontlicense.org/ |

## 에셋 목록

| # | 종류 | 에셋 | 만든 곳 / 사람 | 라이선스 | 위치 | 쓰는 곳 | 추가일 | 상태 |
|---|---|---|---|---|---|---|---|---|
| 1 | 텍스처 | Poly Haven 텍스처 9종<br><small>asphalt_02, brick_wall_10, concrete_floor_worn_001, concrete_wall_008, cracked_concrete_wall, dirty_concrete, rusty_metal_02, wood_planks_dirt</small> | Poly Haven | CC0 | `Assets/ThirdParty/PolyHaven/*.jpg` | 벽·바닥·금속·나무 재질 | 2026-10-28 | 사용 중 |
| 2 | 3D 모델 | Poly Haven 모델 7종 (1K FBX + 색·노멀 텍스처)<br><small>metal_tool_chest, old_military_crate, metal_jerrycan, old_gas_mask, medical_box, old_tyre, trashbag</small> | Poly Haven | CC0 | `Assets/ThirdParty/PolyHaven/Models/` | 방공호 소품, 메인 화면 터널 | 2026-11-02 | 사용 중 |
| 3 | 효과음 | Zombie noises and moans (zombienoises.zip) | ianzazz (OpenGameArt) | CC0 | `Assets/ThirdParty/OpenGameArt/ianzazz_zombie_noises/ → 잘라서 Assets/Resources/Audio/Zombie/` | 좀비 신음·맞음·공격 소리 | 2026-11-02 | 사용 중 |
| 4 | 효과음 | Zombie moans (darsycho__zombie-moans.ogg) | Darsycho (OpenGameArt) | CC0 | `Assets/ThirdParty/OpenGameArt/darsycho_zombie_moans/ → Assets/Resources/Audio/Zombie/` | 좀비 신음·공격 소리 | 2026-11-02 | 사용 중 |
| 5 | 효과음 | Zomby sfx pack (01~07_zombey.mp3) | saturn91 (saturn91.dev, OpenGameArt) | CC0 | `Assets/ThirdParty/OpenGameArt/saturn91_zomby_sfx/ → Assets/Resources/Audio/Zombie/` | 좀비 발견·신음 소리 | 2026-11-02 | 사용 중 |
| 6 | 글꼴 | Pretendard (Regular·SemiBold·Bold) | 길형진 (orioncactus) | OFL | `Assets/Resources/Fonts/Pretendard-*.ttf (+ Pretendard-LICENSE.txt)` | UI 본문 글꼴 | 2026-10-28 | 사용 중 |
| 7 | 글꼴 | Do Hyeon (도현체) | 우아한형제들 (Woowahan Brothers) | OFL | `Assets/Resources/Fonts/DoHyeon-Regular.ttf (+ DoHyeon-LICENSE.txt)` | UI 제목·라벨 (대피소 서류 스타일) | 2026-11-03 | 사용 중 |
| 8 | 글꼴 | Nanum Pen Script (나눔손글씨 펜) | 산돌커뮤니케이션 (네이버 나눔글꼴) | OFL | `Assets/Resources/Fonts/NanumPenScript-Regular.ttf (+ NanumPenScript-LICENSE.txt)` | 손글씨 메모·일지 | 2026-11-03 | 사용 중 |
| 9 | 글꼴 | Share Tech Mono | Carrois Apostrophe (Ralph du Carrois) | OFL | `Assets/Resources/Fonts/ShareTechMono-Regular.ttf (+ ShareTechMono-LICENSE.txt)` | 숫자·시간 표시 (라틴·숫자만) | 2026-11-03 | 사용 중 |
| 10 | 파티클 스프라이트 | Kenney Particle Pack (80장, 512px) | Kenney (kenney.nl) | CC0 | `(파티클 단계에서 받을 예정) Assets/ThirdParty/Kenney/ParticlePack/` | 연기·먼지·불티 파티클 | 2026-11-03 | 예정 |

## 출처 링크

- Poly Haven 텍스처 9종 — https://polyhaven.com/textures
- Poly Haven 모델 7종 (1K FBX + 색·노멀 텍스처) — https://polyhaven.com/models
- Zombie noises and moans (zombienoises.zip) — https://opengameart.org/
- Zombie moans (darsycho__zombie-moans.ogg) — https://opengameart.org/
- Zomby sfx pack (01~07_zombey.mp3) — https://opengameart.org/
- Pretendard (Regular·SemiBold·Bold) — https://github.com/orioncactus/pretendard
- Do Hyeon (도현체) — https://fonts.google.com/specimen/Do+Hyeon
- Nanum Pen Script (나눔손글씨 펜) — https://fonts.google.com/specimen/Nanum+Pen+Script
- Share Tech Mono — https://fonts.google.com/specimen/Share+Tech+Mono
- Kenney Particle Pack (80장, 512px) — https://kenney.nl/assets/particle-pack

## 메모

- 빗소리·바람·발소리 등 나머지 효과음과 UI 스프라이트·캐릭터·아이템 모델은 전부 코드로 만든다 (`Scripts/Audio/ProceduralAudio.cs`, `Scripts/UI/UIStyle.cs`, `Scripts/Characters/ProcMesh.cs` 등).
- 좀비 소리는 위 원본을 잘라 크기를 맞춘 것이다 (메뉴 Tools → 핵폭발 생존 → 좀비 소리 만들기).
- 같은 내용의 JSON·PDF: `Credits/외부_에셋_목록.json`, `Credits/외부_에셋_목록.pdf`
