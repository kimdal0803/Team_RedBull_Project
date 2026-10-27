import json, numpy as np, sim
from sim import *
from scipy.stats import mannwhitneyu
sim.CTX_MODE = "stepavg"
log30 = playtest_log(n_players=30)
rng = np.random.default_rng(11)
POOL = 450
def pool(mk):
    out = []
    for i in range(POOL):
        s0 = list(SKILLS.values())[i % 3]
        out.append(simulate_player(mk(), s0, rng, n_battles=10))
    return out
P = {"off": pool(lambda: Off()), "adopted": pool(lambda: LinUCBTrack(0.2, log30))}
def metric(run, K):
    xs = [x for b in run[1:K] for x in b["oin"]]   # 2번째 전투부터 K번째까지
    return np.mean(xs) if xs else np.nan
res = {}
prng = np.random.default_rng(5)
for K in [3, 5, 10]:
    mo = np.array([metric(r, K) for r in P["off"]]); ma = np.array([metric(r, K) for r in P["adopted"]])
    print("K", K, "mean observed in-band off", round(np.nanmean(mo),3), "adopted", round(np.nanmean(ma),3))
    for n in [10, 15, 20, 30, 40]:
        hits = 0; R = 400
        for _ in range(R):
            a = prng.choice(mo, n); b = prng.choice(ma, n)
            hits += mannwhitneyu(a[~np.isnan(a)], b[~np.isnan(b)], alternative="two-sided").pvalue < 0.05
        res[f"{K}|{n}"] = hits / R
        print("  n", n, "power", hits / R)
json.dump(res, open("power.json", "w"))
