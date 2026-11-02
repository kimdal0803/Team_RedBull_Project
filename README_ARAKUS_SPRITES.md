# 아라쿠스 도트 스프라이트

에셋 목록 엑셀의 **아라쿠스(Arakus)** 시트에 있는 애니메이션 11종을 도트 스프라이트 시트로 만들고,
보스 FSM의 패턴 시간에 맞춰 재생하는 코드를 붙였습니다.

> Unity에서 컴파일·플레이 확인을 하지 못한 상태입니다. 처음 열 때 콘솔 오류가 있으면 그대로 알려 주세요.

## 바로 해 보기

1. 이 브랜치(`feat/arakus-pixel-sprites`)를 받고 Unity를 엽니다.
2. 아라쿠스가 있는 씬을 엽니다 (없으면 **역전의 탑 ▸ ★ 1. 아라쿠스 테스트 씬 만들기**).
3. **역전의 탑 ▸ 아라쿠스 도트 ▸ 2. 씬의 아라쿠스에 입히기**
   - 원래 그림(Dark Knight 등)은 지우지 않고 **끄기만** 합니다.
   - 보스 밑에 `VisualPixel ▸ Body`가 생기고, 보스에 `BossSpriteAnimator`가 붙습니다.
   - 던지는 방패 그림이 방패 투척 패턴 2개(`projectile.sprite`)에 들어갑니다.
4. 씬을 저장하고 Play.

되돌리려면 **역전의 탑 ▸ 아라쿠스 도트 ▸ 되돌리기**.

미리보기(실제 패턴 시간으로 재생): `Docs/ArakusSprites/*.gif`, 전체 프레임: `Docs/ArakusSprites/Arakus_all_frames.png`

## 들어 있는 것

| 위치 | 내용 |
|---|---|
| `Assets/MyAssets/Arakus/Sprites/` | 시트 13장 + 던지는 방패 1장. `.meta`에 자르기·기준점·설정이 들어 있어 따로 자를 필요 없음 |
| `Assets/ReversalTower/Integration/BossSpriteSet.cs` | 보스 하나의 스프라이트 묶음 (ScriptableObject) — 패턴별 프레임과 "언제 어느 프레임" |
| `Assets/ReversalTower/Integration/BossSpriteAnimator.cs` | 패턴 시계에 맞춰 프레임을 넘기는 컴포넌트 (Animator 안 씀) |
| `Assets/ReversalTower/Integration/CameraShake.cs` | 화면 흔들림 (시네머신과 다투지 않음) |
| `Assets/ReversalTower/Integration/Editor/ArakusPixelSetup.cs` | 메뉴 3개 + 패턴별 기본 타이밍 표 |
| `Docs/ArakusSprites/` | 미리보기 GIF 11개, 전체 프레임 한 장 |
| `Tools/ArakusPixel/` | 그림을 다시 만드는 파이썬 스크립트 (아래 "다시 만들기") |

메뉴 '1. 스프라이트 세트 만들기'를 누르면 `Assets/Data/Boss/Arakus/Arakus_SpriteSet.asset`이 생깁니다 ('2'를 누르면 자동으로 만들어짐).

## 시트 목록

모든 프레임은 **156 × 146 px**, 기준점(피벗)은 같은 자리 — 허리 중심 바로 아래 발바닥 선.
그래서 프레임이 바뀌어도 보스가 들썩이지 않고, 보스 오브젝트 위치(발밑)에 그대로 서 있습니다.

| 엑셀 파일 이름 | 패턴 ID | 프레임 (0부터) |
|---|---|---|
| Arakus_NormalAttack_R_PT | Arakus_NormalAttack_R | 0 칼을 뒤로 당기며 낮춤 · 1 횡베기 · 2 복귀 |
| Arakus_NormalAttack_L_PT | Arakus_NormalAttack_L | 0 방패를 머리 위로 · 1 내려찍기 · 2 복귀 |
| Arakus_NormalAttack_F_PT | Arakus_NormalAttack_F | 0 다리를 들고 뒤로 젖힘 · 1 걷어참 · 2 복귀 |
| Arakus_Sword_Swinging_PT | Arakus_SwordSwinging | 0 방패를 등에 메고 어깨 위로 검을 멤 · 1 내려벰 · 2 복귀 |
| Arakus_QuickStep_PT | Arakus_QuickStep | 0 순간 낮춤 · 1 뒤로 뜀 · 2 착지 |
| Arakus_ShieldThrowing_DashAttack_PT | Arakus_ShieldThrowDash | 0 방패를 던질 준비 · 1 던진 손을 뻗고 달려듦 (방패 없음) · 2 복귀 |
| Arakus_ShieldDash_PT | Arakus_ShieldDash | 0 낮춰 달릴 준비 · 1 방패를 앞세워 돌진 · 2 제동 |
| Arakus_Shield_Throwing_Fly_Attack_PT | Arakus_ShieldThrowSlam | 0 던질 준비 · 1 놓음 · 2 도약 · 3 방패를 받고 낙하 · 4 내려찍기 |
| Arakus_Aggressive_Attack_PT | Arakus_SweepingStrike | 0 검에 기를 모음 · 1 휩쓸기 · 2 다음 휩쓸기 준비 · 3 복귀 |
| Arakus_Of_Wrath_PT | Arakus_Rage | 0 무릎 꿇고 기를 모음 · 1 버프 순간 (부풀림) · 2 복귀 |
| Arakus_Charge_PT | Arakus_Charge | 0 낮춰 준비 · 1 돌진 · 2 복귀 |
| Arakus_Idle (표에 없음) | 대기 | 0~2 숨쉬기 (0,1,2,1 순서로 반복) |
| Arakus_Groggy (표에 없음) | 그로기·사망 | 0~1 무릎 꿇고 축 처짐 |
| Arakus_Shield_Projectile (표에 없음) | 던지는 방패 | 한 장 (돌리기는 BossProjectile의 spinSpeed가 함) |

## 크기 기준

- **PPU 45** — 플레이어(Hero Knight)와 같음. 픽셀 크기가 플레이어와 똑같이 보입니다.
- 원본 그림 11장은 AI가 그릴 때마다 크기가 달라서, **투구 높이를 21px**로 맞춰 축척을 통일했습니다.
- 서 있는 자세(횡베기) 키 ≈ 2.4m → 보스 몸 판정(`bodyHeight` 2.4)과 맞음. 대기는 낮춘 자세라 ≈ 2.0m.
- 점 필터, 압축 없음, 밉맵 없음, Read/Write 켬 (피격 번쩍임용 흰 실루엣을 만들 때 필요).

## 게임에서 프레임이 바뀌는 규칙

`BossSpriteAnimator`는 Animator 대신 **패턴 시계**를 봅니다.

- 선딜·공격 단계: `PatternRun.Elapsed` (BossController가 판정과 같은 시계로 흘림)
- 후딜: 후딜에 들어온 뒤 흐른 시간을 이어 붙임
- 그 밖: 대기 (걸으면 2배속) / 그로기 / 사망 (그로기 그림으로 서서히 사라짐)

언제 어느 프레임인지는 세트 에셋의 키 목록이 정합니다. 키는 "기준 시각 + 몇 초 → 몇 번 프레임" 한 줄입니다.
기준 시각은 선딜 시작 / 공격 시작 / 후딜 시작 / 이동 시작·끝 / n번째 판정 / **모든 판정마다** 중에서 고릅니다.
그래서 **DDA가 선딜·타수·속도를 바꿔도 그림이 판정과 어긋나지 않습니다** (타수가 늘면 휩쓸기 동작도 늘어남).

연출 (각각 컴포넌트에서 끌 수 있음):

| 연출 | 내용 |
|---|---|
| 늘어남 | 타격 프레임으로 바뀌는 순간 몸이 앞으로 7% 늘었다 돌아옴 (0.14초) |
| 선딜 진입 | 선딜 시작 0.12초 동안 살짝 눌렸다 펴짐 |
| 떨림 | 선딜이 0.45초 이상이면 공격 직전 0.2초 동안 1픽셀씩 떪 (곧 친다는 신호) |
| 점프 곡선 | 퀵스텝 0.35m, 내려찍기 1.8m. 그림만 뜨고 판정 위치는 그대로 |
| 화면 흔들림 | 약: 횡베기·발차기·휩쓸기·대쉬 베기·돌진 / 강: 방패 내려찍기·거검·내려찍기 착지·분노 |
| 피격 번쩍임 | 맞을 때 0.07초 흰 실루엣 |

색은 건드리지 않습니다 (사망 때 투명도만). 그래서 `feat/arakus-art` 브랜치의 `BossStateFx`(그로기 노란 펄스, 분노 붉은 색)와 같이 써도 겹치지 않습니다.
그 브랜치가 들어오면 화면 흔들림이 겹치는지만 보고, 필요하면 `BossSpriteAnimator.screenShake`를 끄면 됩니다.

## 기획 확인이 필요한 것

| 내용 | 지금 구현 |
|---|---|
| 엑셀에 대기·걷기·그로기·사망 동작이 없음 | 대기 = 방패돌진 준비 자세로 숨쉬기, 걷기 = 대기 2배속, 그로기·사망 = 분노 자세를 축 처지게 |
| 방패 던지고 내려찍기: 기획은 "방패를 돌려받은 뒤 점프", 데이터는 던지고 0.15초 뒤 점프 시작 (방패는 착지 즈음 돌아옴) | 데이터 기준 — 점프 중엔 방패 없음, 내려오기 직전에 받음 |
| 일반공격 2 내려찍기 순간 그림이 따로 없음 | 돌진 그림(방패를 앞세워 낮춘 자세)을 빌려 씀. 방패가 땅에 완전히 닿지는 않음 |
| 거검 휘두르기 타격 순간 그림이 따로 없음 | 휩쓸기 그림에서 칼의 빛만 걷어내 사용 (방패를 등에 멘 것까지 같은 상태) |

## 알려진 한계

- 포즈 한 장에서 만든 3~5프레임 애니메이션입니다 (중간 동작 없음). 움직임은 위의 연출과 이펙트로 채웁니다.
  최종 그림으로 바꿀 때는 같은 파일 이름·같은 프레임 크기로 덮어쓰면 코드는 그대로 씁니다.
- 공격 이펙트(엑셀의 `*_FX`)는 이번 범위가 아닙니다.
- 시트의 프레임 수를 바꾸면 메뉴 '1'을 다시 누르고, 키의 프레임 번호를 확인해 주세요.

## 다시 만들기 (Tools/ArakusPixel)

원본 포즈 11장(2048px, 회색 배경)을 `Tools/ArakusPixel/src/<이름>.png`에 두고 (용량 때문에 저장소에는 없음 — 힉스필드 생성 기록에 있음):

```
python3 cutout.py      # 배경·바닥 그림자 제거 (cut_opts.json에 그림별 보정)
python3 noglow.py      # 휩쓸기 그림에서 칼 빛을 걷어낸 버전
python3 frames.py      # 축척 통일 → 프레임 → 공통 32색 팔레트 → 시트 (out/sheets)
python3 shield.py      # 던지는 방패
python3 preview.py     # 미리보기 GIF (timeline.py의 타이밍)
python3 emit_unity.py  # 시트 + .meta를 Assets/MyAssets/Arakus/Sprites로
```

같은 입력이면 같은 결과가 나옵니다 (팔레트 무작위 시드 고정).
