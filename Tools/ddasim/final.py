"""설계서 12장에 넣을 최종 실행. 실력별 100명씩, 20전."""
import json, sys, time, numpy as np
import sim
from sim import *

sim.CTX_MODE = "stepavg"
N = int(sys.argv[1]) if len(sys.argv) > 1 else 100
t0 = time.time()
log30 = playtest_log(n_players=30)
log12 = playtest_log(n_players=12)
ADOPT_ALPHA = 0.2

conds = {
    "off": lambda: Off(),
    "rule": lambda: Rule(),
    "oracle": lambda: Oracle(),
    "disjoint_cold": lambda: LinUCBDisjoint(0.25),
    "disjoint_warm": lambda: LinUCBDisjoint(0.25, log30),
    "reward_warm": lambda: LinUCBReward(0.1, log30),
    "track_cold": lambda: LinUCBTrack(ADOPT_ALPHA),
    "adopted": lambda: LinUCBTrack(ADOPT_ALPHA, log30),
}
out = {"meta": dict(N_per_skill=N, battles=N_BATTLES, playtest_rows=len(log30), alpha=ADOPT_ALPHA)}
for k, mk in conds.items():
    out[k] = summarize(run_condition(mk, N))
    print(k, round(np.mean([np.mean(v["tin"]) for v in out[k].values()]), 3), f"{time.time()-t0:.0f}s", flush=True)

sweep = {}
for label, lg in [("cold", None), ("warm12", log12), ("warm30", log30)]:
    for a in [0.0, 0.1, 0.2, 0.5, 1.0]:
        s = summarize(run_condition(lambda: LinUCBTrack(a, lg), N))
        sweep[f"{label}|{a}"] = float(np.mean([np.mean(v["tin"]) for v in s.values()]))
        print(label, a, round(sweep[f"{label}|{a}"], 3), f"{time.time()-t0:.0f}s", flush=True)
out["sweep"] = sweep
json.dump(out, open("final.json", "w"), ensure_ascii=False)
print("done", f"{time.time()-t0:.0f}s")
