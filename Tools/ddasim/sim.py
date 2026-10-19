"""
가상 플레이어 시뮬레이션 — DDA 설계서 12장.

가정 (설계서에 그대로 적는다)
- 보스 패턴 6개. 패턴마다 기본 난이도 d와 한 번 쓸 때 날아오는 타 수(n)가 있다.
- 플레이어 실력 s (초보 0.30 / 중수 0.55 / 고수 0.80) + 패턴별 편차(표준편차 0.06),
  같은 패턴을 맞을수록 조금씩 는다 (최대 +0.08~0.20).
- 한 타를 막을(패링 또는 회피) 확률 p = sigmoid(5(s - d) - 0.55*step + 0.6).
- 실패한 타는 피해 8 x 데미지 배율 (한 방 상한 최대 HP의 30%). HP는 100 + 5*전투력.
  성공 30번이면 보스를 쓰러뜨린다. 전투마다 전투력 +0.5.
- 한 보스를 20번 싸운다.
"""
import numpy as np
import json, sys, time

SEED = 7
PATTERNS = [  # 이름, 기본 난이도, 한 번에 날아오는 타 수
    ("P1 내려찍기", 0.35, 3),
    ("P2 3연베기", 0.45, 3),
    ("P3 돌진", 0.50, 2),
    ("P4 회전베기", 0.55, 4),
    ("P5 지면파동", 0.65, 3),
    ("P6 잡기", 0.75, 2),
]
NP = len(PATTERNS)
ARMS = np.array([-2, -1, 0, 1, 2])
DMG_SCALE = {-2: 0.80, -1: 0.90, 0: 1.0, 1: 1.10, 2: 1.20}
K_STEP = 0.55
BAND = (0.6, 0.8)
TARGET = 0.7
N_BATTLES = 20
SKILLS = {"초보": 0.30, "중수": 0.55, "고수": 0.80}
BOSS_SUCC_TO_KILL = 30
BASE_DMG = 8.0
HIT_CAP_FRAC = 0.30
MIN_STRIKES = 3
D = 6
CTX_MODE = "stepavg"   # stepavg = 설계서 6장 문맥, base = 처음 계획(전투력 포함)


def sigmoid(z):
    return 1.0 / (1.0 + np.exp(-z))


def reward(rate):
    lo, hi = BAND
    dist = 0.0 if lo <= rate <= hi else (lo - rate if rate < lo else rate - hi)
    return max(0.0, 1.0 - dist / 0.3)


class Player:
    def __init__(self, rng, s0):
        self.s0 = s0
        self.dev = rng.normal(0, 0.06, NP)
        self.learn = rng.uniform(0.08, 0.20)
        self.exposure = np.zeros(NP)
        self.cp = 0.0

    def skill(self, p):
        return self.s0 + self.dev[p] + self.learn * (1 - np.exp(-self.exposure[p] / 60.0))

    def p_success(self, p, step):
        return sigmoid(5 * (self.skill(p) - PATTERNS[p][1]) - K_STEP * step + 0.6)


def run_battle(pl, steps, rng):
    hp = max_hp = 100 + 5 * pl.cp
    strikes = np.zeros(NP, int); succ = np.zeros(NP, int); parry = np.zeros(NP, int)
    total = 0
    while True:
        p = rng.integers(NP)
        st = int(steps[p])
        n = PATTERNS[p][2]
        n_eff = n + (1 if st == 2 else (-1 if st == -2 and n > 1 else 0))
        for _ in range(n_eff):
            pr = pl.p_success(p, st)
            strikes[p] += 1
            pl.exposure[p] += 1
            if rng.random() < pr:
                succ[p] += 1; total += 1
                if rng.random() < np.clip(pl.skill(p), 0.05, 0.95):
                    parry[p] += 1
                if total >= BOSS_SUCC_TO_KILL:
                    return strikes, succ, parry, True
            else:
                hp -= min(BASE_DMG * DMG_SCALE[st], HIT_CAP_FRAC * max_hp)
                if hp <= 0:
                    return strikes, succ, parry, False


# ───────────── 문맥 ─────────────

class History:
    def __init__(self):
        self.rec = [[] for _ in range(NP)]
        self.all = []
        self.attempts = 0

    def context(self, p, pl, cur_step):
        recent = self.rec[p][-3:]
        st = sum(r[0] for r in recent)
        stepavg = np.mean([r[3] for r in recent]) if recent else cur_step
        if st >= MIN_STRIKES:
            fail = 1 - sum(r[1] for r in recent) / st
        else:
            fail = 0.4
        a_recent = self.all[-3:]
        ast = sum(r[0] for r in a_recent)
        overall = 1 - sum(r[1] for r in a_recent) / ast if ast >= MIN_STRIKES else 0.4
        if CTX_MODE == "stepavg":
            # [상수, 이 패턴 실패율(최근 3전), 지금 단계/2, 그 3전의 평균 단계/2, 전체 실패율, 도전 횟수/10]
            return np.array([1.0, fail, cur_step / 2.0, stepavg / 2.0, overall, min(self.attempts, 10) / 10.0])
        # [상수, 이 패턴 실패율(최근 3전), 지금 단계/2, 전체 실패율(최근 3전), 전투력/10, 도전 횟수/10]
        return np.array([1.0, fail, cur_step / 2.0, overall, min(pl.cp, 10) / 10.0,
                         min(self.attempts, 10) / 10.0])

    def push(self, strikes, succ, parry, steps):
        for p in range(NP):
            if strikes[p] > 0:
                self.rec[p].append((strikes[p], succ[p], parry[p], steps[p]))
        self.all.append((strikes.sum(), succ.sum()))
        self.attempts += 1


# ───────────── 정책 ─────────────

def rail(cands_score, cur):
    """안전장치: 지금 단계에서 한 칸까지만. 동점이면 덜 움직이는 쪽."""
    sc = np.where(np.abs(ARMS - cur) <= 1, cands_score, -np.inf)
    best = np.flatnonzero(sc >= sc.max() - 1e-12)
    return int(ARMS[best[np.argmin(np.abs(ARMS[best] - cur))]])


class Off:
    def choose(self, p, x, cur): return 0
    def update(self, p, x, arm, rate, n): pass


class RandomArm:
    def __init__(self, rng): self.rng = rng
    def choose(self, p, x, cur): return int(np.clip(cur + self.rng.integers(-1, 2), -2, 2))
    def update(self, p, x, arm, rate, n): pass


class Rule:
    """지난 전투 성공률이 구간 아래면 한 단계 쉽게, 위면 한 단계 어렵게."""
    def __init__(self): self.last = [None] * NP
    def choose(self, p, x, cur):
        r = self.last[p]
        if r is None: return cur
        return cur - 1 if r < BAND[0] else (cur + 1 if r > BAND[1] else cur)
    def update(self, p, x, arm, rate, n): self.last[p] = rate


def phi(x, a):
    u = a / 2.0
    return np.concatenate([x, u * x, u * u * x])


class LinUCBDisjoint:
    """교과서형 LinUCB: 패턴마다, 팔마다 따로 (A, b). 보상 = 구간 보상."""
    def __init__(self, alpha=0.5, log=None, w=1.0):
        self.alpha = alpha
        self.A = np.tile(np.eye(D), (NP, len(ARMS), 1, 1))
        self.b = np.zeros((NP, len(ARMS), D))
        for p, x, arm, rate, n in (log or []):
            self._add(p, x, arm, reward(rate), w)
    def _add(self, p, x, arm, r, w=1.0):
        self.A[p, arm + 2] += w * np.outer(x, x); self.b[p, arm + 2] += w * r * x
    def choose(self, p, x, cur):
        sc = np.empty(len(ARMS))
        for i in range(len(ARMS)):
            Ai = np.linalg.inv(self.A[p, i])
            sc[i] = (Ai @ self.b[p, i]) @ x + self.alpha * np.sqrt(x @ Ai @ x)
        return rail(sc, cur)
    def update(self, p, x, arm, rate, n): self._add(p, x, arm, reward(rate))


class LinUCBReward:
    """팔을 특징에 넣은 LinUCB (모든 패턴이 모델 하나 공유). 보상 = 구간 보상."""
    def __init__(self, alpha=0.1, log=None, w=1.0):
        self.alpha = alpha
        self.A = np.eye(3 * D); self.b = np.zeros(3 * D)
        for p, x, arm, rate, n in (log or []):
            f = phi(x, arm); self.A += w * np.outer(f, f); self.b += w * reward(rate) * f
    def choose(self, p, x, cur):
        Ai = np.linalg.inv(self.A); th = Ai @ self.b
        sc = np.array([th @ phi(x, a) + self.alpha * np.sqrt(phi(x, a) @ Ai @ phi(x, a)) for a in ARMS])
        return rail(sc, cur)
    def update(self, p, x, arm, rate, n):
        f = phi(x, arm); self.A += np.outer(f, f); self.b += reward(rate) * f


class LinUCBTrack:
    """같은 LinUCB 행렬로 '다음 전투 이 패턴의 대응 성공률'을 예측하고,
    예측이 목표 구간에 가장 가까운 팔을 고른다 (불확실성이 큰 팔에 alpha만큼 가산).
    관측 하나를 타 수(n/6)만큼 무게를 줘서 넣는다."""
    def __init__(self, alpha=0.05, log=None, w=1.0):
        self.alpha = alpha
        self.A = np.eye(3 * D); self.b = np.zeros(3 * D)
        for p, x, arm, rate, n in (log or []):
            f = phi(x, arm); self.A += w * n / 6 * np.outer(f, f); self.b += w * n / 6 * rate * f
    def predict(self, x):
        Ai = np.linalg.inv(self.A); th = Ai @ self.b
        return np.array([th @ phi(x, a) for a in ARMS]), np.array([np.sqrt(phi(x, a) @ Ai @ phi(x, a)) for a in ARMS])
    def choose(self, p, x, cur):
        pred, unc = self.predict(x)
        # 낙관적 거리: 불확실한 만큼 목표 구간에 가까울 수도 있다고 본다 (UCB)
        gap = np.maximum(0.0, np.abs(pred - TARGET) - (BAND[1] - BAND[0]) / 2 - self.alpha * unc)
        return rail(-gap, cur)
    def update(self, p, x, arm, rate, n):
        f = phi(x, arm); self.A += n / 6 * np.outer(f, f); self.b += n / 6 * rate * f


class Oracle:
    """참고선: 진짜 실력을 안다고 치고 성공 확률이 0.7에 가장 가까운 단계 (한 칸 제한 있음)."""
    player = None
    def choose(self, p, x, cur):
        q = np.array([self.player.p_success(p, a) for a in ARMS])
        return rail(-np.abs(q - TARGET), cur)
    def update(self, p, x, arm, rate, n): pass


# ───────────── 한 플레이어 20전 ─────────────

def simulate_player(policy, s0, rng, log=None, n_battles=N_BATTLES, protect=True):
    pl = Player(rng, s0)
    hist = History()
    steps = np.zeros(NP, int)
    out = []
    lose_streak = 0
    if isinstance(policy, Oracle):
        policy.player = pl
    for t in range(n_battles):
        ctx = [hist.context(p, pl, steps[p]) for p in range(NP)]
        protected = False
        if t > 0:
            for p in range(NP):
                steps[p] = int(np.clip(policy.choose(p, ctx[p], steps[p]), -2, 2))
            if protect and not isinstance(policy, (Off, Oracle)) and lose_streak >= 3:
                steps = np.maximum(steps - 1, -2)
                protected = True
        applied = steps.copy()
        ptrue = [float(pl.p_success(p, applied[p])) for p in range(NP)]
        strikes, succ, parry, cleared = run_battle(pl, applied, rng)
        rates = np.where(strikes > 0, succ / np.maximum(strikes, 1), np.nan)
        for p in range(NP):
            if strikes[p] >= MIN_STRIKES:
                policy.update(p, ctx[p], int(applied[p]), float(rates[p]), int(strikes[p]))
                if log is not None:
                    log.append((p, ctx[p], int(applied[p]), float(rates[p]), int(strikes[p])))
        hist.push(strikes, succ, parry, applied)
        lose_streak = 0 if cleared else lose_streak + 1
        pl.cp += 0.5
        out.append(dict(battle=t + 1, cleared=bool(cleared), protected=protected,
                        steps=applied.tolist(), ptrue=ptrue,
                        tin=float(np.mean([BAND[0] <= q <= BAND[1] for q in ptrue])),
                        rate=float(succ.sum() / max(strikes.sum(), 1)),
                        oin=[bool(BAND[0] <= rates[p] <= BAND[1]) for p in range(NP) if strikes[p] >= MIN_STRIKES]))
    return out


def playtest_log(seed=SEED + 100, n_players=60, n_battles=10):
    """웜 스타트 재료: 내부 플레이테스트 60명 x 10판, 단계를 무작위(지금 단계 ±1)로 받았다고 가정."""
    rng = np.random.default_rng(seed)
    log = []
    mix = list(SKILLS.values())
    for i in range(n_players):
        simulate_player(RandomArm(rng), mix[i % 3], rng, log, n_battles=n_battles, protect=False)
    return log


def run_condition(make_policy, n_per_skill, seed=SEED):
    rng = np.random.default_rng(seed)
    return {name: [simulate_player(make_policy(), s0, rng) for _ in range(n_per_skill)]
            for name, s0 in SKILLS.items()}


def summarize(res):
    out = {}
    for skill, runs in res.items():
        arr = lambda key: np.array([[b[key] for b in r] for r in runs], float)
        steps = np.array([[b["steps"] for b in r] for r in runs], float)
        ptrue = np.array([[b["ptrue"] for b in r] for r in runs], float)
        first = [next((b["battle"] for b in r if b["cleared"]), None) for r in runs]
        fc = [f for f in first if f is not None]
        out[skill] = dict(
            tin=arr("tin").mean(0).tolist(), clear=arr("cleared").mean(0).tolist(),
            rate=arr("rate").mean(0).tolist(), protected=arr("protected").mean(0).tolist(),
            step_mean=steps.mean((0, 2)).tolist(),
            pattern_step_last5=steps[:, 15:, :].mean((0, 1)).tolist(),
            pattern_p_last5=ptrue[:, 15:, :].mean((0, 1)).tolist(),
            first_clear_mean=float(np.mean(fc)) if fc else None,
            first_clear_share=len(fc) / len(runs),
            cum_clear_by=[float(np.mean([f is not None and f <= t for f in first])) for t in range(1, N_BATTLES + 1)])
    return out
