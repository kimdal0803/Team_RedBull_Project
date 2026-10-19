"""
DDA 엔진의 파이썬 기준 구현 (C# DdaEngine과 한 줄씩 같은 규칙).

sim.py의 채택안(LinUCBTrack)과 문맥(History.context, stepavg)을 그대로 쓰고,
게임에 들어가는 규칙(DDA 설계서 9장 안전장치)을 더한다.
export_golden.py가 이 코드로 '정답 파일'을 만들고, Unity의 DDA 오프라인 검증 메뉴가
C# 결과와 비교한다. 규칙을 바꾸면 이 파일과 C#을 같이 바꾸고 정답 파일을 다시 만든다.

결정 순서 (패턴마다)
  1. 제외 패턴            → 0단계, excluded
  2. 대조군 · 정책 off     → 0단계, off
  3. 이 보스 첫 전투       → 0단계, first
  4. 지난 전투 유효 타 < 3 → 단계 그대로, noData
  5. 정책 (linucb / rule / random) → 지금 단계 ±1 안, −2~+2, learned
     linucb인데 모델이 없거나 고장 → rule로 대신, fallback
  6. 3연패 보호 (random 제외)       → 한 칸 더 쉽게, protected
"""
import numpy as np
from sim import phi, ARMS, BAND, TARGET

D = 6
F = 3 * D
MIN_STRIKES = 3
PROTECT_AFTER = 3
HISTORY = 3


# ───────────── 모델 (sim.LinUCBTrack과 같은 식) ─────────────

class Model:
    def __init__(self, A=None, b=None, version="", updates=0):
        self.A = np.eye(F) if A is None else np.array(A, float).copy()
        self.b = np.zeros(F) if b is None else np.array(b, float).copy()
        self.version = version
        self.updates = updates

    def predict(self, x):
        Ai = np.linalg.inv(self.A)
        th = Ai @ self.b
        yh = np.array([th @ phi(x, a) for a in ARMS])
        u = np.array([np.sqrt(phi(x, a) @ Ai @ phi(x, a)) for a in ARMS])
        return yh, u

    def update(self, x, arm, y, n):
        f = phi(x, arm)
        self.A += n / 6 * np.outer(f, f)
        self.b += n / 6 * y * f
        self.updates += 1

    def healthy(self):
        return bool(np.all(np.isfinite(self.A)) and np.all(np.isfinite(self.b)))


def rail(scores, cur):
    """지금 단계 ±1 안에서 점수가 가장 높은 팔. 같으면 덜 움직이는 쪽, 그래도 같으면 쉬운 쪽(작은 단계)."""
    sc = np.where(np.abs(ARMS - cur) <= 1, scores, -np.inf)
    best = np.flatnonzero(sc >= sc.max() - 1e-12)
    return int(ARMS[best[np.argmin(np.abs(ARMS[best] - cur))]])


def linucb_choose(model, x, cur, alpha):
    pred, unc = model.predict(x)
    gap = np.maximum(0.0, np.abs(pred - TARGET) - (BAND[1] - BAND[0]) / 2 - alpha * unc)
    return rail(-gap, cur)


def rule_choose(last_rate, cur):
    if last_rate < 0:
        return cur
    if last_rate < BAND[0]:
        return cur - 1
    if last_rate > BAND[1]:
        return cur + 1
    return cur


def clamp_step(step, cur):
    step = max(cur - 1, min(cur + 1, step))
    return max(-2, min(2, step))


# ───────────── 상태 ─────────────

class PatternState:
    def __init__(self, pid):
        self.id = pid
        self.step = 0
        self.last_eff = 0          # 지난 전투 유효 타 (noData 판단)
        self.last_valid_rate = -1.0  # 유효 타 3 이상이던 마지막 전투의 대응 성공률 (rule용)
        self.history = []          # [(eff, succ, step)] 최근 3개, 유효 타 > 0인 전투만


class State:
    def __init__(self, boss_id="Arakus"):
        self.boss_id = boss_id
        self.attempts = 0
        self.lose_streak = 0
        self.patterns = {}
        self.overall = []          # [(eff, succ)] 최근 3전
        self.model = None

    def pattern(self, pid):
        if pid not in self.patterns:
            self.patterns[pid] = PatternState(pid)
        return self.patterns[pid]


def context(ps, overall, cur, attempts, default_fail):
    recent = ps.history[-HISTORY:]
    st = sum(r[0] for r in recent)
    stepavg = np.mean([r[2] for r in recent]) if recent else cur
    fail = 1 - sum(r[1] for r in recent) / st if st >= MIN_STRIKES else default_fail
    a_recent = overall[-HISTORY:]
    ast = sum(r[0] for r in a_recent)
    ofail = 1 - sum(r[1] for r in a_recent) / ast if ast >= MIN_STRIKES else default_fail
    return np.array([1.0, fail, cur / 2.0, stepavg / 2.0, ofail, min(attempts, 10) / 10.0])


# ───────────── 결정 · 학습 ─────────────

def decide(state, patterns, group, policy, alpha, default_fail):
    """patterns: [(id, excluded)]. 반환: [dict(id, step, reason, x, predicted, uncertainty, excluded)], protection"""
    model = state.model if (state.model is not None and state.model.healthy()) else None
    out = []
    protect = (group == "dda" and policy not in ("off", "random") and state.lose_streak >= PROTECT_AFTER)
    for pid, excluded in patterns:
        ps = state.pattern(pid)
        cur = ps.step
        d = dict(id=pid, excluded=excluded, x=None, predicted=-1.0, uncertainty=-1.0)
        if excluded:
            d.update(step=0, reason="excluded")
            out.append(d)
            continue
        x = context(ps, state.overall, cur, state.attempts, default_fail)
        d["x"] = x
        if group != "dda" or policy == "off":
            step, reason = 0, "off"
        elif state.attempts == 0:
            step, reason = 0, "first"
        elif ps.last_eff < MIN_STRIKES:
            step, reason = cur, "noData"
        elif policy == "linucb" and model is not None:
            step, reason = clamp_step(linucb_choose(model, x, cur, alpha), cur), "learned"
        elif policy == "linucb":
            step, reason = clamp_step(rule_choose(ps.last_valid_rate, cur), cur), "fallback"
        elif policy == "rule":
            step, reason = clamp_step(rule_choose(ps.last_valid_rate, cur), cur), "learned"
        else:
            raise ValueError("random은 기준 구현에서 다루지 않는다 (언어마다 난수가 달라서)")
        if protect:
            step, reason = max(step - 1, -2), "protected"
        d.update(step=step, reason=reason)
        if policy == "linucb" and group == "dda" and model is not None:
            yh, u = model.predict(x)
            i = int(np.flatnonzero(ARMS == step)[0])
            d.update(predicted=float(yh[i]), uncertainty=float(u[i]))
        out.append(d)
    return out, protect


def learn(state, decisions, results, cleared, group, policy):
    """results: {id: (strikes, ignored, parry, dodge)} — 이번 전투에 나온 패턴만."""
    model = state.model if (state.model is not None and state.model.healthy()) else None
    tot_eff = tot_succ = 0
    for d in decisions:
        if d["excluded"]:
            continue
        ps = state.pattern(d["id"])
        strikes, ignored, parry, dodge = results.get(d["id"], (0, 0, 0, 0))
        eff = max(0, strikes - ignored)
        succ = max(0, min(eff, parry + dodge))
        ps.last_eff = eff
        ps.step = d["step"]
        if eff > 0:
            ps.history.append((eff, succ, d["step"]))
            ps.history = ps.history[-HISTORY:]
        if eff >= MIN_STRIKES:
            y = succ / eff
            ps.last_valid_rate = y
            if model is not None and group == "dda" and policy != "off":
                model.update(d["x"], d["step"], y, eff)
        tot_eff += eff
        tot_succ += succ
    state.overall.append((tot_eff, tot_succ))
    state.overall = state.overall[-HISTORY:]
    state.attempts += 1
    state.lose_streak = 0 if cleared else state.lose_streak + 1
