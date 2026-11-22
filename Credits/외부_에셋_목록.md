# 벙커랩 (BUNKER LAB) — 외부 에셋 목록

갱신: 2026-11-23 · 항목 22개

> 게임에 쓰인 외부(직접 만들지 않은) 에셋 전부. 이 JSON이 원본이고, 같은 폴더의 .md와 .pdf는 build_credits.py로 만든다. 새 에셋을 쓰면 여기에 한 항목을 더하고 스크립트를 다시 돌린다. CC0는 출처 표기 의무가 없고 SIL OFL은 글꼴 파일을 그대로 넣을 때 라이선스 문서를 같이 두면 된다.

## 라이선스 요약

| 약칭 | 이름 | 뜻 | 원문 |
|---|---|---|---|
| CC0 | Creative Commons CC0 1.0 (퍼블릭 도메인 기증) | 표기 없이 상업용 포함 자유롭게 사용·수정 가능 | https://creativecommons.org/publicdomain/zero/1.0/ |
| OFL | SIL Open Font License 1.1 | 게임에 내장·배포 가능, 글꼴 자체를 단독 판매만 금지, 라이선스 문서 동봉 | https://openfontlicense.org/ |
| CC-BY-3.0 | Creative Commons Attribution 3.0 (저작자 표시) | 제작자와 출처를 표기하면 상업용 포함 사용·수정 가능 (이 목록과 게임 크레딧에 표기) | https://creativecommons.org/licenses/by/3.0/ |
| Tripo-유료 | Tripo AI API 이용 약관 (Holymolly Ltd, 2025-07-11판) | 유료 사용자는 만든 결과물의 권리를 가지며(5.2.2) 상업·비상업 용도로 쓸 수 있다(3.2). 출처 표기 의무 없음. 무료 사용자의 결과물은 Tripo가 권리를 가진다(5.2.1). 약관과 어긋나게 제3자에게 배포하는 것은 금지(3.2). 이 프로젝트의 Tripo 모델은 모두 사용자가 Tripo 유료 구독(Max 플랜)을 결제한 뒤에 만들었다(사용자 11-23 확인) — www.tripo3d.ai/terms(2026-11-13판) 5.2.2의 유료 결과물(Paid Content: 유료 구독 중에 만든 것) | https://developers.tripo3d.ai/en/terms |
| Higgsfield | Higgsfield AI 이용 약관 (2026-10-23판) | 회사는 사용자의 입력·결과물 소유권을 주장하지 않고 결과물의 상업적 사용을 제한하지 않는다(4.4, 5.1(e)). 결과물 권리는 다른 사람에게 넘기거나 재허락할 수 있고, 구독을 끝내도 남는다(4.4). 무료·유료 구분 없이 같은 조항이다(체험은 유료 구독으로 바뀐다, 10.7). 출처 표기 의무 없음. 단서: 결과물을 공개하기 전 권리 확인은 사용자 책임(13.2), 결과물의 고유성은 보장하지 않음(4.4), 결과물로 다른 AI 모델을 학습시키지 않음(5.2(iv)), 출처 표시(워터마크·C2PA)를 지우지 않음(5.5), 외부 모델(Tripo·Google 등)의 금지 용도 정책을 따른다(8). 이 프로젝트 계정은 Ultra 요금제(11-23 확인) | https://higgsfield.ai/terms |

## 에셋 목록

| # | 종류 | 에셋 | 만든 곳 / 사람 | 라이선스 | 위치 | 쓰는 곳 | 추가일 | 상태 |
|---|---|---|---|---|---|---|---|---|
| 1 | 텍스처 | Poly Haven 텍스처 9종<br><small>asphalt_02, brick_wall_10, concrete_floor_worn_001 (2k), concrete_wall_008 (2k), cracked_concrete_wall, dirty_concrete, rusty_metal_02, wood_planks_dirt</small> | Poly Haven | CC0 | `Assets/ThirdParty/PolyHaven/*.jpg` | 벽·바닥·금속·나무 재질. 방공호 벽·바닥 2종은 2k판(2026-11-20, 그래픽 품질 '매우 높음'용. 파일 이름은 _1k 그대로). rusty_metal_02는 가로등 품질별 판 굽기의 잔무늬로도 씀(11-23) | 2026-10-28 | 사용 중 |
| 2 | 3D 모델 | Poly Haven 모델 7종 (1K FBX + 색·노멀 텍스처)<br><small>metal_tool_chest, old_military_crate, metal_jerrycan, old_gas_mask, medical_box, old_tyre, trashbag</small> | Poly Haven | CC0 | `Assets/ThirdParty/PolyHaven/Models/` | 방공호 소품, 메인 화면 터널 | 2026-11-02 | 사용 중 |
| 3 | 효과음 | Zombie noises and moans (zombienoises.zip) | ianzazz (OpenGameArt) | CC0 | `Assets/ThirdParty/OpenGameArt/ianzazz_zombie_noises/ → 잘라서 Assets/Resources/Audio/Zombie/` | 좀비 신음·맞음·공격 소리 | 2026-11-02 | 사용 중 |
| 4 | 효과음 | Zombie moans (darsycho__zombie-moans.ogg) | Darsycho (OpenGameArt) | CC0 | `Assets/ThirdParty/OpenGameArt/darsycho_zombie_moans/ → Assets/Resources/Audio/Zombie/` | 좀비 신음·공격 소리 | 2026-11-02 | 사용 중 |
| 5 | 효과음 | Zomby sfx pack (01~07_zombey.mp3) | saturn91 (saturn91.dev, OpenGameArt) | CC0 | `Assets/ThirdParty/OpenGameArt/saturn91_zomby_sfx/ → Assets/Resources/Audio/Zombie/` | 좀비 발견·신음 소리 | 2026-11-02 | 사용 중 |
| 6 | 글꼴 | Pretendard (Regular·SemiBold·Bold) | 길형진 (orioncactus) | OFL | `Assets/Resources/Fonts/Pretendard-*.ttf (+ Pretendard-LICENSE.txt)` | UI 본문 글꼴 | 2026-10-28 | 사용 중 |
| 7 | 글꼴 | Do Hyeon (도현체) | 우아한형제들 (Woowahan Brothers) | OFL | `Assets/Resources/Fonts/DoHyeon-Regular.ttf (+ DoHyeon-LICENSE.txt)` | UI 제목·라벨 (대피소 서류 스타일) | 2026-11-03 | 사용 중 |
| 8 | 글꼴 | Share Tech Mono | Carrois Apostrophe (Ralph du Carrois) | OFL | `Assets/Resources/Fonts/ShareTechMono-Regular.ttf (+ ShareTechMono-LICENSE.txt)` | 숫자·시간 표시 (라틴·숫자만) | 2026-11-03 | 사용 중 |
| 9 | 파티클 스프라이트 | Kenney Particle Pack (80장, 512px) | Kenney (kenney.nl) | CC0 | `(파티클 단계에서 받을 예정) Assets/ThirdParty/Kenney/ParticlePack/` | 연기·먼지·불티 파티클 | 2026-11-03 | 예정 |
| 10 | 효과음 | Fantozzi's Footsteps (Grass/Sand & Stone) — Fantozzi-footsteps.7z<br><small>Fantozzi-StoneL1~L3·R1~R3 → stone, Fantozzi-SandL1~L3·R1~R3 → sand</small> | Fantozzi (Freesound 팩 10338), 잘라 올린 이 qubodup (OpenGameArt) | CC0 | `Assets/Resources/Audio/Footsteps/stone/, sand/ (메모 Assets/ThirdParty/OpenGameArt/Fantozzi-footsteps/README.txt)` | 플레이어 발소리: 방공호·복도·터널·보도(돌), 흙·재(모래), 출발 연출 걸음 | 2026-11-05 | 사용 중 |
| 11 | 효과음 | Impact Sounds 1.0 — footstep_concrete_000~004 | Kenney (kenney.nl) | CC0 | `Assets/Resources/Audio/Footsteps/interior/ (License.txt: Assets/ThirdParty/Kenney/impact-sounds/)` | 플레이어 발소리: 건물 안 바닥 | 2026-11-05 | 사용 중 |
| 12 | 효과음 | footsteps on different surfaces (footsteps_0.zip) — gravel 0~9, boots 0~8, water 0~4<br><small>gravel: Ali_6868 'Gravel Footsteps' (Freesound 팩 21608) 파생 — CC0, boots: swuing 'footstep-concrete.wav' (Freesound 38873) 파생 — CC-BY 3.0, water: EminYILDIRIM 'Water Footsteps' (Freesound 608663) + swuing footstep-concrete 파생 — CC-BY 3.0</small> | 묶음 OpenGameArt 업로더 / 원 제작 Ali_6868, swuing, EminYILDIRIM (Freesound) | CC-BY-3.0 | `Assets/Resources/Audio/Footsteps/gravel/, boots/, water/ (폴더별 license 사본: Assets/ThirdParty/OpenGameArt/footsteps-different-surfaces/)` | 플레이어 발소리: 잔해·자갈, 도시 아스팔트(군화), 웅덩이; 귀환 연출 걸음(군화) | 2026-11-05 | 사용 중 |
| 13 | 3D 모델 | 버려진 소형 세단 (wrecked_sedan_01) — Tripo AI로 글에서 만든 모델<br><small>v3.1 저폴리, 삼각형 6872개, 색 텍스처 2048 원본 → 512로 들여옴, 작업 176e117c(생성)·f0f9c1d8(FBX 변환), 35크레딧, 매우 높음 판(Q3_매우높음/): 그림→3D 면 제한 20000, 삼각형 19,090개, 텍스처 1024로 들여옴, 작업 f6128ec5(생성)·f0f6bc82(FBX 변환), 45크레딧</small> | Tripo AI (Holymolly Ltd)로 생성, 맵 모델링 세션 | Tripo-유료 | `Assets/ThirdParty/Tripo/Models/wrecked_sedan_01/ (만든 방법: SOURCE.md)` | 도시 길가 부서진 차 (시험 모델, 아직 씬에는 넣지 않음). Max 플랜 유료 구독 중에 만듦(11-23 사용자 확인) | 2026-11-21 | 시험 |
| 14 | 3D 모델 | 녹슨 200리터 드럼통 (steel_drum_01) — Tripo AI로 글에서 만든 모델<br><small>v3.1 저폴리, 삼각형 2048개, 색 텍스처 2048 원본 → 512로 들여옴, 작업 094e3fe9(생성)·3dae0078(FBX 변환), 35크레딧, 매우 높음 판(Q3_매우높음/): 그림→3D 면 제한 10000, 삼각형 14,860개, 텍스처 1024로 들여옴, 작업 b4d97c3d(생성)·88c14ee4(FBX 변환), 45크레딧 (먼저 만든 면 2만 판은 깨져 버림, 45크레딧 더 씀)</small> | Tripo AI (Holymolly Ltd)로 생성, 맵 모델링 세션 | Tripo-유료 | `Assets/ThirdParty/Tripo/Models/steel_drum_01/ (만든 방법: SOURCE.md)` | 드럼통 (시험 모델, 아직 씬에는 넣지 않음). Max 플랜 유료 구독 중에 만듦(11-23 사용자 확인) | 2026-11-22 | 시험 |
| 15 | 3D 모델 | 에어컨 실외기 (ac_unit_01) — Tripo AI로 글에서 만든 모델<br><small>v3.1 저폴리, 삼각형 3076개, 색 텍스처 2048 원본 → 512로 들여옴, 작업 b0a47db2(생성)·42a844cd(FBX 변환), 35크레딧</small> | Tripo AI (Holymolly Ltd)로 생성, 맵 모델링 세션 | Tripo-유료 | `Assets/ThirdParty/Tripo/Models/ac_unit_01/ (만든 방법: SOURCE.md)` | 건물 벽 실외기 (시험 모델, 아직 씬에는 넣지 않음). Max 플랜 유료 구독 중에 만듦(11-23 사용자 확인) | 2026-11-22 | 시험 |
| 16 | 3D 모델 | 소화전 (fire_hydrant_01) — Tripo AI로 글에서 만든 모델<br><small>v3.1 저폴리, 삼각형 3984개, 색 텍스처 2048 원본 → 512로 들여옴, 작업 afb3dacf(생성)·dc1246f2(FBX 변환), 35크레딧, 매우 높음 판(Q3_매우높음/): 그림→3D 면 제한 20000, 삼각형 24,581개, 텍스처 1024로 들여옴, 작업 8f8ff6dc(생성)·52f864d3(FBX 변환), 45크레딧</small> | Tripo AI (Holymolly Ltd)로 생성, 맵 모델링 세션 | Tripo-유료 | `Assets/ThirdParty/Tripo/Models/fire_hydrant_01/ (만든 방법: SOURCE.md)` | 보도 소화전 (시험 모델, 아직 씬에는 넣지 않음). Max 플랜 유료 구독 중에 만듦(11-23 사용자 확인) | 2026-11-22 | 시험 |
| 17 | 3D 모델 | 고장 난 픽업트럭 (pickup_truck_01) — Tripo AI로 글에서 만든 모델<br><small>v3.1 저폴리, 삼각형 7511개, 색 텍스처 2048 원본 → 512로 들여옴, 작업 488d2baf(생성)·1ccfaec3(FBX 변환), 35크레딧, 다시 만든 판(11-23, 바퀴·엔진 없는 수리용): 그림 편집 130e1dda(5)·그림→3D 면 1만 d31b76aa(40)·FBX 06d399c7(5), 삼각형 12,970, 낮음~높음 1,192·3,255·7,000 (면 1만5천 판 33e81d48·5a4b61f1 45크레딧은 짐칸 벽이 빠져 버림)</small> | Tripo AI (Holymolly Ltd)로 생성, 맵 모델링 세션 | Tripo-유료 | `Assets/ThirdParty/Tripo/Models/pickup_truck_01/ (만든 방법: SOURCE.md)` | 트럭 마당 픽업트럭 후보 (시험, 바퀴·엔진이 붙어 있어 수리 목표로는 아직 못 씀). Max 플랜 유료 구독 중에 만듦(11-23 사용자 확인) | 2026-11-22 | 시험 |
| 18 | 3D 모델 | 가로등 (lamp_post_01) — Tripo AI로 글에서 만든 모델<br><small>v3.1 저폴리, 삼각형 2251개, 색 텍스처 2048 원본 → 512로 들여옴, 작업 b0a7a989(생성)·f828e544(FBX 변환), 35크레딧, 품질 4단계 판(11-23): 모양은 직접 짬(코드 가로등 치수), 칠은 Tripo 그림 편집 bbaa2387(5)을 비춰 굽고 Poly Haven rusty_metal_02(CC0) 잔무늬. 삼각형 120·394·1,920·2,912 (그림→3D 9df2f9cd·FBX 4afd4e86 45크레딧은 기둥이 끊겨 버림)</small> | Tripo AI (Holymolly Ltd)로 생성, 맵 모델링 세션 | Tripo-유료 | `Assets/ThirdParty/Tripo/Models/lamp_post_01/ (만든 방법: SOURCE.md)` | 길가 가로등 후보 (시험, 아직 씬에는 넣지 않음). Max 플랜 유료 구독 중에 만듦(11-23 사용자 확인) | 2026-11-22 | 시험 |
| 19 | 3D 모델 | 버스정류장 (bus_stop_01) — Tripo AI로 글에서 만든 모델<br><small>v3.1 저폴리, 삼각형 3700개, 색 텍스처 2048 원본 → 512로 들여옴, 작업 c14b7f84(생성)·dc3c245f(FBX 변환), 35크레딧, 매우 높음 판(Q3_매우높음/): 그림→3D 면 제한 10000, 삼각형 13,093개, 텍스처 1024로 들여옴, 작업 57dca359(생성)·98d7f36b(FBX 변환), 45크레딧 (먼저 만든 면 2만 판은 깨져 버림, 45크레딧 더 씀)</small> | Tripo AI (Holymolly Ltd)로 생성, 맵 모델링 세션 | Tripo-유료 | `Assets/ThirdParty/Tripo/Models/bus_stop_01/ (만든 방법: SOURCE.md)` | 버스정류장 후보 (시험, 아직 씬에는 넣지 않음). Max 플랜 유료 구독 중에 만듦(11-23 사용자 확인) | 2026-11-22 | 시험 |
| 20 | 3D 모델 | 불탄 가로수 (dead_tree_01) — Tripo AI로 글에서 만든 모델<br><small>v3.1 저폴리, 삼각형 5098개, 색 텍스처 2048 원본 → 512로 들여옴, 작업 7f16f36d(생성)·12f13270(FBX 변환), 35크레딧</small> | Tripo AI (Holymolly Ltd)로 생성, 맵 모델링 세션 | Tripo-유료 | `Assets/ThirdParty/Tripo/Models/dead_tree_01/ (만든 방법: SOURCE.md)` | 길가 죽은 나무 후보 (시험, 아직 씬에는 넣지 않음). Max 플랜 유료 구독 중에 만듦(11-23 사용자 확인) | 2026-11-22 | 시험 |
| 21 | 3D 모델 | 뒤집힌 버려진 소형 세단, 바닥 부품이 보이는 판 (wrecked_sedan_flipped_01) — Tripo AI로 그림(우리 차를 뒤집어 찍고 이미지 편집)에서 만든 모델<br><small>v3.1 저폴리, 삼각형 7790개, 색 텍스처 512로 들여옴, 작업 cd45c2be(이미지 편집 5)·b3ded922(그림→3D 40)·463abcdb(FBX 변환 5), 50크레딧, 매우 높음 판(Q3_매우높음/): 그림→3D 면 제한 20000, 삼각형 24,488개, 텍스처 1024로 들여옴, 작업 dca4d5fc(생성)·7dd1335a(FBX 변환), 45크레딧</small> | Tripo AI (Holymolly Ltd)로 생성, 맵 모델링 세션 | Tripo-유료 | `Assets/ThirdParty/Tripo/Models/wrecked_sedan_flipped_01/ (만든 방법: SOURCE.md)` | 도시의 뒤집힌 차 5대 (시험, 11-22 도시 씬에 넣음, 아직 커밋 전). Max 플랜 유료 구독 중에 만듦(11-23 사용자 확인) | 2026-11-22 | 시험 |
| 22 | 3D 모델 | 감염자 (일반 좀비, zombie_common) — Higgsfield에서 만든 컨셉 그림과 3D 모델(Tripo H3.1)을 잘라 다듬은 모델<br><small>컨셉: Character_Design/Enemy01_CommonZombie.png를 바탕으로 Higgsfield nano_banana_2 (claude.ai 채팅 무료체험, 크레딧 0), 3D: Higgsfield tripo_h3_1_multiview_to_3d, job 1c153104 (7,694면, 9크레딧), 자동 뼈대 참고 3d_rigging job 4872a143 (5크레딧), Blender에서 관절별로 자르고 하나의 몸으로 다시 만들고 다듬음: 뼈 53개, LOD0 84,626 · LOD1 20,000 · LOD2 4,999 삼각형, 색·노멀 2048 (작업 기록 ModelWork/Enemies/zombie_common/README.md)</small> | Higgsfield AI(컨셉·3D 생성), Tripo H3.1 모델, Blender 손질은 적 모델링 세션 | Higgsfield | `Assets/Resources/Enemies/ZombieCommon/ (원본·만든 방법: 저장소 밖 ModelWork/Enemies/zombie_common/)` | 일반 좀비(감염자) 몸 — CharacterModels.Skinned.cs, SkinnedZombieDriver.cs. 약관 확인 11-23: 상업적 사용·공개 배포 가능, 소유권 주장 없음 | 2026-11-23 | 시험 |

## 출처 링크

- Poly Haven 텍스처 9종 — https://polyhaven.com/textures
- Poly Haven 모델 7종 (1K FBX + 색·노멀 텍스처) — https://polyhaven.com/models
- Zombie noises and moans (zombienoises.zip) — https://opengameart.org/
- Zombie moans (darsycho__zombie-moans.ogg) — https://opengameart.org/
- Zomby sfx pack (01~07_zombey.mp3) — https://opengameart.org/
- Pretendard (Regular·SemiBold·Bold) — https://github.com/orioncactus/pretendard
- Do Hyeon (도현체) — https://fonts.google.com/specimen/Do+Hyeon
- Share Tech Mono — https://fonts.google.com/specimen/Share+Tech+Mono
- Kenney Particle Pack (80장, 512px) — https://kenney.nl/assets/particle-pack
- Fantozzi's Footsteps (Grass/Sand & Stone) — Fantozzi-footsteps.7z — https://opengameart.org/content/fantozzis-footsteps-grasssand-stone
- Impact Sounds 1.0 — footstep_concrete_000~004 — https://kenney.nl/assets/impact-sounds
- footsteps on different surfaces (footsteps_0.zip) — gravel 0~9, boots 0~8, water 0~4 — https://opengameart.org/content/footsteps-on-different-surfaces
- 버려진 소형 세단 (wrecked_sedan_01) — Tripo AI로 글에서 만든 모델 — https://www.tripo3d.ai/ (API: https://developers.tripo3d.ai/)
- 녹슨 200리터 드럼통 (steel_drum_01) — Tripo AI로 글에서 만든 모델 — https://www.tripo3d.ai/ (API: https://developers.tripo3d.ai/)
- 에어컨 실외기 (ac_unit_01) — Tripo AI로 글에서 만든 모델 — https://www.tripo3d.ai/ (API: https://developers.tripo3d.ai/)
- 소화전 (fire_hydrant_01) — Tripo AI로 글에서 만든 모델 — https://www.tripo3d.ai/ (API: https://developers.tripo3d.ai/)
- 고장 난 픽업트럭 (pickup_truck_01) — Tripo AI로 글에서 만든 모델 — https://www.tripo3d.ai/ (API: https://developers.tripo3d.ai/)
- 가로등 (lamp_post_01) — Tripo AI로 글에서 만든 모델 — https://www.tripo3d.ai/ (API: https://developers.tripo3d.ai/)
- 버스정류장 (bus_stop_01) — Tripo AI로 글에서 만든 모델 — https://www.tripo3d.ai/ (API: https://developers.tripo3d.ai/)
- 불탄 가로수 (dead_tree_01) — Tripo AI로 글에서 만든 모델 — https://www.tripo3d.ai/ (API: https://developers.tripo3d.ai/)
- 뒤집힌 버려진 소형 세단, 바닥 부품이 보이는 판 (wrecked_sedan_flipped_01) — Tripo AI로 그림(우리 차를 뒤집어 찍고 이미지 편집)에서 만든 모델 — https://www.tripo3d.ai/ (API: https://developers.tripo3d.ai/)
- 감염자 (일반 좀비, zombie_common) — Higgsfield에서 만든 컨셉 그림과 3D 모델(Tripo H3.1)을 잘라 다듬은 모델 — https://higgsfield.ai/

## 메모

- 빗소리·바람·발소리 등 나머지 효과음과 UI 스프라이트·캐릭터·아이템 모델은 전부 코드로 만든다 (`Scripts/Audio/ProceduralAudio.cs`, `Scripts/UI/UIStyle.cs`, `Scripts/Characters/ProcMesh.cs` 등).
- 좀비 소리는 위 원본을 잘라 크기를 맞춘 것이다 (메뉴 Tools → 핵폭발 생존 → 좀비 소리 만들기).
- 같은 내용의 JSON·PDF: `Credits/외부_에셋_목록.json`, `Credits/외부_에셋_목록.pdf`
