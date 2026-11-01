# 2보스 파네스(Phanes) 패턴 코드

파네스 기획서(30쪽)를 기존 보스 FSM(`ReversalTower.Boss`) 위에 얹은 코드입니다.
아라쿠스와 같은 `BossController`·`BossPatternData`·전투 데이터 수집기·DDA를 그대로 쓰고,
파네스만의 것(상시 비행, 순간이동, 월드 좌표 판정, 기믹)을 `Assets/ReversalTower/Boss/Phanes`에 더했습니다.

> 이 코드는 Unity에서 컴파일·플레이 확인을 하지 못한 상태입니다. 처음 열 때 콘솔 오류가 있으면 그대로 알려 주세요.

## 바로 해 보기

1. 이 브랜치(`feat/phanes-boss`)를 받고 Unity를 엽니다.
2. 메뉴 **역전의 탑 ▸ ★ 3. 파네스 테스트 씬 만들기 (안전, 복제본)**
   - `BossTestScene`을 복제한 `PhanesTestScene`에 파네스를 세웁니다. 원본 씬은 건드리지 않습니다.
   - 더미 보스·아라쿠스·돌진 기둥은 지우지 않고 끄기만 합니다.
   - 패턴 에셋 16개가 `Assets/Data/Boss/Phanes`에 생깁니다.
3. Play. 왼쪽 위에 보스 상태 / 현재 패턴 / 그로기 게이지가 보입니다.

다른 보스 오브젝트에 붙이고 싶으면: `BossController`가 있는 오브젝트를 고르고
**역전의 탑 ▸ 파네스 ▸ 선택한 보스에 파네스 연결 (전체 11종 / 핵심 6종만)** — 이때 `PhanesBrain`(+ `PhanesMotor`, `PhanesArena` 자동)을 붙여야 스킬이 실행됩니다.

## 기획서 → 구현 대응

| 기획서 | 패턴 에셋 | 기획서 수치 (그대로) | 구현 방식 |
|---|---|---|---|
| 3.1 이동 (Teleport) | `Phanes_Teleport` | 패턴 전 잦은 순간이동, 근접 시 순간이동 | 스킬 후 40% 확률 + 플레이어가 2.2m 안에 0.5초 붙으면. 흐려진 동안 무적 |
| 4.1 일반 공격 1 유도 광탄 | `Phanes_HomingOrb_P1/P2` | 1P 1발 / 2P 0.2초 간격 3발, 직경 0.5m, 1.5초 뒤 유도 상실 | 투사체가 판정을 들고 감. 패링하면 사라짐 |
| 4.1 일반 공격 2 부채꼴 산탄 | `Phanes_FanVolley_P1/P2` | 시전 시점 조준, 전방 60°, 1P 3갈래 / 2P 5갈래, 폭 0.4m | 같은 방출에는 한 번만 맞음 |
| 4.2 굴절의 광탄 | `Phanes_RefractionOrb` | 순간이동 후, 직경 1.2m 저속 유도, 패링 시 역반사 → 피해 + 그로기 | 반사 후 푸른빛으로 바뀌어 보스 추적, 그로기 게이지 +50 |
| 4.2 단죄의 빛 기둥 | `Phanes_JudgmentPillar` | 발밑 경고 → 폭 1.6m, Y축 무한, 다단 히트, 점프 회피 불가 | 바닥~천장 판정, 0.4초 간격 3타 |
| 4.2 신벌의 징벌선 | `Phanes_PunishmentLine_P1/P2` | 바닥 기준 높이 0.8m, 맵 끝까지, 1P 단방향 / 2P 양방향 또는 잔류 불꽃 | 바닥으로 내려와 레이저가 고속으로 뻗음 |
| 4.2 천벌의 잔향 | `Phanes_HeavenlyEcho_P1/P2` | 1.0초 간격 3~4회, 표식 0.8초 뒤 폭발, 폭 1.0 × 높이 1.5m | 1P 3회 / 2P 4회. 표식이 이어지는 동안 보스는 다음 패턴 |
| 4.2 영벌의 업화 | `Phanes_Hellfire` | 1.2초 전조, 최하단 바닥 전체 5초, 높이 1.2m, 발판 안전 | 1초마다 화상 틱 × 5. 타는 동안 보스는 다음 패턴 |
| 4.3 단절의 결계 | `Phanes_SeveranceBarrier` | 맵 중앙 결계벽 폭 2.0m·천장 관통·통과 불가, 실패 시 맵 전체 즉사급, 성공 시 행동 불능 3초 | HP 50% 진입 시 강제 1회. 성공 → 보스 추락 그로기 3초 |
| 4.3 성운의 파편 | `Phanes_NebulaShards` | 중앙 상공 구체 반경 1.5m, 별 모양 줄기 | 12갈래 경고선 → 번개 + 파편 탄막 3번(매번 반 칸 엇갈림) |
| 4.3 인력의 왜곡 | `Phanes_GravityDistortion` | 3.0초 흡인 반경 4.5m(가까울수록 강함), 종료 폭발 반경 2.0m | 플레이어 이동 속도보다 약해서 반대로 달리면 버팀 |
| 4.3 환영 분신 | `Phanes_PhantomClones` | 좌우 3.0m 분신 2체(HP 1), 정답 → 경직 / 오답 → 자폭 2.0m + 암흑·침묵 / 시간 초과 → 일반 공격 복제 | 구분 단서: 보석 색 + 뒤 마법진 회전 방향 |
| 3.2 행동 트리 | `PhaseTrigger` | HP 50% 미만 → 2페이즈 + 결계 확정 1회 | 50%: 2페이즈 + 강제 패턴 결계 |
| 3.1 무력화 | `PhanesBrain` | 결계 파훼 시 바닥으로 추락 | 그로기마다 바닥으로 떨어졌다 다시 떠오름 |

**핵심 6종 / 전체 11종**: 2.2 스킬 리스트와 3.2 행동 트리에는 일반 공격 2종, 굴절의 광탄, 빛 기둥, 결계, 성운의 파편만 있고,
징벌선·잔향·업화·인력·분신은 4.2/4.3 상세 페이지에만 있습니다. 둘 다 연결할 수 있게 메뉴를 나눴습니다 (기본은 전체).

## 기획 확인이 필요한 것

| 내용 | 지금 구현 | 바꾸는 곳 |
|---|---|---|
| 유도 광탄 2페이즈: 2.2는 "2번 연속", 4.1은 "3개 연속" | 4.1대로 3발 | `Phanes_HomingOrb_P2` hits[0].repeatCount |
| 성운의 파편: 3.2 행동 트리는 1페이즈 루프에도 있음, 2.2·4.3은 2페이즈 | 2페이즈 전용 | `Phanes_NebulaShards` minPhase |
| 결계 성공 조건: 2.2는 "파괴 후 보스에게 피해", 3.2·4.3 플로우는 "파괴 = 성공" | 벽 파괴만으로 성공 | barrier.requireBossHitAfterBreak |
| 징벌선 2페이즈: "양방향 또는 잔류 불꽃" | 양방향만 켬 | line.bothDirections / line.residualFire |
| 기획서에 없는 숫자: 결계 제한 시간·체력, 투사체 속도, 쿨타임, 데미지 계수, 분신 고를 시간 등 | 임시값 | 각 에셋 designNote에 '임시'로 표시 |

## 기존 코드에서 바뀐 것 (5개 파일)

아라쿠스 동작은 그대로입니다. 새 기능을 꽂을 자리만 더했습니다.

- `BossEnums.cs` — `PatternKind.Scripted`(보스 전용 실행기), `HitCarrier.World`(월드 좌표 판정) 추가
- `BossController.cs` — `ResolveHitBox`(기울어진 사각형), `ResolveHitCircle`(원형), `SignalParry`(투사체가 직접 패링 신호) 추가.
  기존 `ResolveHitAt`은 같은 동작으로 내부만 나눔. 투사체 판정은 시간표 패링 신호를 건너뜀 (아라쿠스 방패는 원래 패링 불가라 영향 없음)
- `ProjectileExecutor.cs`, `BossHitboxVisualizer.cs` — `World` 판정을 보스 몸 판정으로 착각하지 않게 한 줄씩
- `Assets/Scripts/PlayerAttackTrigger.cs` — 결계벽·분신을 때릴 수 있게 `IPlayerAttackTarget` 확인 몇 줄 추가 (BossController 확인 바로 위)

## 알려진 한계

- 그림은 코드로 만든 임시 실루엣·이펙트입니다. `MyAssets/Pixel Holy Spell Effect` 시트를 경고 마법진 등에 쓰면 어울립니다.
- 침묵 디버프는 `PhanesStatusEffects.IsSilenced`로 상태만 켭니다. 실제로 Q/W/E를 막으려면 `PlayerMovement` 스킬 입력 조건에 한 줄이 필요합니다 (정훈 담당 코드라 손대지 않음).
- `PlayerMovement`의 Q/W/E 스킬은 지금 `DummyBoss`만 때립니다 (아라쿠스도 같음). 결계벽은 기본 공격으로 부숴야 합니다.
- 영벌의 업화는 공중 발판이 있어야 피할 수 있습니다 (`BossTestScene`에는 가운데 발판이 있음).
