"""
Unity DDA 오프라인 검증용 '정답 파일'과 시험용 초기값(sim-0)을 만든다.

  python3 export_golden.py <출력 폴더>
    → <출력 폴더>/dda_golden_v1.json   (Assets/ReversalTower/DDA/Editor/Golden/ 에 둔다)
    → <출력 폴더>/dda_prior_sim0.json  (Assets/ReversalTower/DDA/Priors/ 에 둔다)

sim-0은 가상 플레이테스트(30명 × 10전, 단계 무작위 ±1)로 만든 초기값이다.
실제 플레이테스트 기록이 모이기 전까지 LinUCB가 도는지 확인하는 용도로만 쓴다.
"""
import json, sys, os
import numpy as np
import sim
from sim import phi, ARMS, LinUCBTrack, sigmoid
import ref_engine as R

sim.CTX_MODE = "stepavg"
ALPHA = 0.2
DEFAULT_FAIL = 0.4
out_dir = sys.argv[1] if len(sys.argv) > 1 else "."
os.makedirs(out_dir, exist_ok=True)


def fl(a):
    return [float(v) for v in np.asarray(a).ravel()]


# ───────────── 1. 시험용 초기값 sim-0 ─────────────
log30 = sim.playtest_log(n_players=30)
prior = R.Model(version="sim-0")
for p, x, arm, rate, n in log30:
    prior.update(x, arm, rate, n)
prior.updates = 0
ref = LinUCBTrack(ALPHA, log30)
assert np.allclose(ref.A, prior.A) and np.allclose(ref.b, prior.b), "sim.LinUCBTrack과 초기값이 다르다"

prior_file = dict(version="sim-0", bossId="", rows=len(log30), alpha=ALPHA,
                  note="가상 플레이테스트 30명 x 10전 (ddasim). 실제 플레이테스트 전 시험용. 실험·배포에 쓰지 말 것.",
                  A=fl(prior.A), b=fl(prior.b))
json.dump(prior_file, open(os.path.join(out_dir, "dda_prior_sim0.json"), "w"), ensure_ascii=False)

rng = np.random.default_rng(20261017)


# ───────────── 2. 모델: 예측 · 선택 ─────────────
def model_case(model, x, cur):
    yh, u = model.predict(x)
    return dict(x=fl(x), cur=int(cur), yhat=fl(yh), unc=fl(u), choice=R.linucb_choose(model, x, cur, ALPHA))


def random_x():
    cur = int(rng.integers(-2, 3))
    return np.array([1.0, rng.uniform(0, 1), cur / 2.0, rng.integers(-4, 5) / 4.0,
                     rng.uniform(0, 1), rng.integers(0, 11) / 10.0]), cur


idx = rng.choice(len(log30), 30, replace=False)
model_cases = []
for i in idx:
    _, x, arm, rate, n = log30[i]
    model_cases.append(model_case(prior, x, int(round(x[2] * 2))))
for _ in range(15):
    x, cur = random_x()
    model_cases.append(model_case(prior, x, cur))

# 업데이트 후
log_b = sim.playtest_log(seed=99, n_players=3, n_battles=5)
updates = []
after = R.Model(prior.A, prior.b)
for p, x, arm, rate, n in log_b[:30]:
    updates.append(dict(x=fl(x), arm=int(arm), y=float(rate), n=int(n)))
    after.update(x, arm, rate, n)
after_cases = []
for _ in range(20):
    x, cur = random_x()
    after_cases.append(model_case(after, x, cur))

# ───────────── 3. 문맥 ─────────────
context_cases = []
for k in range(40):
    ps = R.PatternState("P")
    hlen = int(rng.integers(0, 4))
    for _ in range(hlen):
        eff = int(rng.integers(1, 9)) if k % 5 else int(rng.integers(1, 2))
        ps.history.append((eff, int(rng.integers(0, eff + 1)), int(rng.integers(-2, 3))))
    overall = []
    for _ in range(int(rng.integers(0, 4))):
        eff = int(rng.integers(0, 30)) if k % 7 else int(rng.integers(0, 3))
        overall.append((eff, int(rng.integers(0, eff + 1))))
    attempts = int(rng.integers(0, 16))
    cur = int(rng.integers(-2, 3))
    x = R.context(ps, overall, cur, attempts, DEFAULT_FAIL)
    context_cases.append(dict(recEff=[r[0] for r in ps.history], recSucc=[r[1] for r in ps.history],
                              recStep=[r[2] for r in ps.history], allEff=[r[0] for r in overall],
                              allSucc=[r[1] for r in overall], attempts=attempts, cur=cur, x=fl(x)))

# ───────────── 4. 레일 · 규칙 ─────────────
rail_cases = []
for k in range(30):
    sc = -np.maximum(0.0, rng.normal(0.05, 0.08, 5))
    if k % 3 == 0:
        sc[rng.integers(0, 5)] = 0.0
        sc[rng.integers(0, 5)] = 0.0
    cur = int(rng.integers(-2, 3))
    rail_cases.append(dict(scores=fl(sc), cur=cur, choice=R.rail(sc, cur)))
rail_cases.append(dict(scores=[0.0] * 5, cur=0, choice=0))
rail_cases.append(dict(scores=[-0.1, 0.0, -0.2, 0.0, -0.1], cur=0, choice=-1))
rail_cases.append(dict(scores=[0.0, -0.1, -0.1, -0.1, 0.0], cur=-2, choice=-2))

rule_cases = []
for r in [-1.0, 0.0, 0.3, 0.59, 0.6, 0.7, 0.8, 0.81, 1.0]:
    for cur in range(-2, 3):
        rule_cases.append(dict(lastRate=r, cur=cur, choice=R.clamp_step(R.rule_choose(r, cur), cur)))


# ───────────── 5. 시나리오: 결정 → 결과 → 학습 반복 ─────────────
PATTERNS = [("P_A", False), ("P_B", False), ("P_C", False), ("P_D", False), ("P_X", True)]
DIFF = {"P_A": 0.40, "P_B": 0.55, "P_C": 0.65, "P_D": 0.50, "P_X": 0.5}
HITS = {"P_A": 1, "P_B": 1, "P_C": 3, "P_D": 2, "P_X": 1}
CLEARED = [False, False, False, False, True, False, True, True, False, False, False, True, True, True, False, True]


def scenario(name, group, policy, use_prior, seed):
    srng = np.random.default_rng(seed)
    st = R.State("Golden")
    if use_prior:
        st.model = R.Model(prior.A, prior.b, "sim-0")
    battles = []
    for t, cleared in enumerate(CLEARED):
        decisions, protect = R.decide(st, PATTERNS, group, policy, ALPHA, DEFAULT_FAIL)
        steps = {d["id"]: d["step"] for d in decisions}
        outcomes = []
        results = {}
        for pid, _ in PATTERNS:
            uses = int(srng.integers(0, 4))
            strikes = uses * HITS[pid]
            if strikes == 0:
                continue
            ignored = int(srng.binomial(strikes, 0.1))
            eff = strikes - ignored
            p = sigmoid(5 * (0.45 - DIFF[pid]) - 0.55 * steps[pid] + 0.6)
            succ = int(srng.binomial(eff, p))
            parry = int(srng.binomial(succ, 0.5))
            dodge = succ - parry
            outcomes.append(dict(id=pid, strikes=strikes, ignored=ignored, parry=parry, dodge=dodge))
            results[pid] = (strikes, ignored, parry, dodge)
        expects = [dict(id=d["id"], step=int(d["step"]), reason=d["reason"],
                        x=[] if d["x"] is None else fl(d["x"]),
                        predicted=float(d["predicted"]), uncertainty=float(d["uncertainty"])) for d in decisions]
        battles.append(dict(cleared=cleared, outcomes=outcomes, expects=expects, protectionApplied=bool(protect)))
        R.learn(st, decisions, results, cleared, group, policy)
    has_model = st.model is not None
    return dict(name=name, group=group, policy=policy, usePrior=use_prior,
                patternIds=[p for p, _ in PATTERNS], excluded=[e for _, e in PATTERNS], battles=battles,
                finalAttempts=st.attempts, finalLoseStreak=st.lose_streak,
                finalA=fl(st.model.A) if has_model else [], finalB=fl(st.model.b) if has_model else [],
                finalUpdates=st.model.updates if has_model else 0)


scenarios = [
    scenario("linucb_prior", "dda", "linucb", True, 11),
    scenario("linucb_no_prior_fallback", "dda", "linucb", False, 12),
    scenario("rule_with_prior", "dda", "rule", True, 13),
    scenario("control_group", "control", "linucb", True, 14),
]

reasons = {}
for s in scenarios:
    for b in s["battles"]:
        for e in b["expects"]:
            reasons[(s["name"], e["reason"])] = reasons.get((s["name"], e["reason"]), 0) + 1

golden = dict(
    version="v1", generator="ddasim/export_golden.py", alpha=ALPHA, defaultFail=DEFAULT_FAIL,
    bandLow=0.6, bandHigh=0.8, target=0.7, minStrikes=R.MIN_STRIKES, protectAfter=R.PROTECT_AFTER,
    priorVersion="sim-0", priorA=fl(prior.A), priorB=fl(prior.b),
    modelCases=model_cases, updates=updates, afterA=fl(after.A), afterB=fl(after.b), afterCases=after_cases,
    contextCases=context_cases, railCases=rail_cases, ruleCases=rule_cases, scenarios=scenarios,
)
text = json.dumps(golden, ensure_ascii=False)
# UnityEngine.JsonUtility는 1e-05 같은 지수 표기를 제대로 읽지 못한다. 정답 파일에 나오면 C# 검증이 틀린 값을 읽는다.
import re
assert not re.search(r"[0-9][eE][-+]?[0-9]", text), "정답 파일에 지수 표기가 있다 — 값을 바꾸거나 C# 쪽 읽기를 바꿀 것"
open(os.path.join(out_dir, "dda_golden_v1.json"), "w").write(text)

print("prior rows", len(log30))
print("cases", len(model_cases), len(updates), len(after_cases), len(context_cases), len(rail_cases), len(rule_cases))
for k in sorted(reasons):
    print(k, reasons[k])
for s in scenarios:
    print(s["name"], "attempts", s["finalAttempts"], "lose", s["finalLoseStreak"], "updates", s["finalUpdates"],
          "steps last", [e["step"] for e in s["battles"][-1]["expects"]])
