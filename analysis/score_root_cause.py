"""Score the frozen root-cause model on the test table, once (Objective 3).

Reads data/failure-test-table.csv and data/root-cause-frozen-model.json (the
file tagged root-cause-freeze). Nothing is refitted. The script:

  1. refuses to run unless the model file is identical to the tagged copy;
  2. refuses to run if data/root-cause-test-results.txt already exists, so
     the test is scored once (delete the file deliberately to override, and
     say so in the design doc);
  3. scores the frozen model and the rules baseline on the two primary parts
     and reports the secondary batches without a criterion;
  4. writes everything it prints to data/root-cause-test-results.txt.

Primary parts (design doc, 29th Oct 2026):
    Documents: overload (Load test batch) against slow response (C1)
    Login:     failed request (C3) against slow response (held-out Timing)
Success criterion: precision and recall of at least 0.75 for each class in
each part, by point estimate with the interval shown. Decision rule: the
model is used in PRAT only if the mean of the two parts' macro F1 beats the
rules' mean; otherwise the rules are used. Macro F1 in a part is the mean F1
of the part's two classes, and a prediction of the third class counts as an
error for the true class. Intervals: cluster bootstrap by condition (scenario,
profile, magnitude), 2000 resamples, seed 20261101.

    python analysis/score_root_cause.py
"""
import argparse
import json
import subprocess
import sys
from pathlib import Path

import numpy as np
import pandas as pd

from root_cause_rules import predict as rules_predict

TEST = Path("data/failure-test-table.csv")
MODEL = Path("data/root-cause-frozen-model.json")
RESULTS = Path("data/root-cause-test-results.txt")
TAG = "root-cause-freeze"
SEED = 20261101
BOOTSTRAPS = 2000
THRESHOLD = 0.75

PARTS = {
    "Documents": ("Documents", {"load_test", "cross"}, ["overload", "slow_response"]),
    "Login": ("Login", {"cross", "heldout_delay"}, ["failed_request", "slow_response"]),
}
SECONDARY = [("PasswordReset", "cross"), ("PasswordReset", "retry_test"), ("Navigation", "heldout_delay")]

OUT = []


def say(s=""):
    print(s)
    OUT.append(s)


def check_frozen():
    r = subprocess.run(["git", "show", f"{TAG}:{MODEL.as_posix()}"], capture_output=True, text=True)
    if r.returncode != 0:
        sys.exit(f"cannot read {MODEL} from tag {TAG}: {r.stderr.strip()}")
    if r.stdout != MODEL.read_text():
        sys.exit(f"{MODEL} differs from the copy tagged {TAG}; refusing to score")


def model_predict(spec, df):
    X = df[spec["features"]].to_numpy(dtype=float)
    z = (X - np.array(spec["mean"])) / np.array(spec["scale"])
    scores = z @ np.array(spec["coef"]).T + np.array(spec["intercept"])
    return np.array(spec["classes"], dtype=object)[scores.argmax(axis=1)]


def stats(y, p, classes):
    out, f1s = {}, []
    for c in classes:
        tp = int(((p == c) & (y == c)).sum())
        pp, ap = int((p == c).sum()), int((y == c).sum())
        pr = tp / pp if pp else 0.0
        rc = tp / ap if ap else 0.0
        out[c] = (pr, rc)
        f1s.append(2 * pr * rc / (pr + rc) if pr + rc else 0.0)
    return out, float(np.mean(f1s))


def cluster_idx(df):
    key = (df.scenario_name + "|" + df.fault_profile + "|" + df.fault_magnitude.astype(str)).to_numpy()
    return [np.flatnonzero(key == k) for k in np.unique(key)]


def bootstrap(df, y, pm, pr_, classes):
    groups = cluster_idx(df)
    rng = np.random.default_rng(SEED)
    cells = {(m, c, k): [] for m in ("model", "rules") for c in classes for k in (0, 1)}
    f1 = {"model": [], "rules": [], "diff": []}
    for _ in range(BOOTSTRAPS):
        idx = np.concatenate([groups[i] for i in rng.integers(0, len(groups), len(groups))])
        res = {}
        for m, p in (("model", pm), ("rules", pr_)):
            s, f = stats(y[idx], p[idx], classes)
            res[m] = f
            f1[m].append(f)
            for c in classes:
                cells[(m, c, 0)].append(s[c][0])
                cells[(m, c, 1)].append(s[c][1])
        f1["diff"].append(res["model"] - res["rules"])
    ci = lambda v: tuple(float(x) for x in np.round(np.percentile(v, [2.5, 97.5]), 2))
    return {k: ci(v) for k, v in cells.items()}, {k: ci(v) for k, v in f1.items()}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--table", default=str(TEST))
    ap.add_argument("--dry-run", action="store_true", help="mechanics check on another table; skips the freeze check and the once only lock")
    args = ap.parse_args()
    if not args.dry_run:
        if RESULTS.exists():
            sys.exit(f"{RESULTS} exists: the test has been scored once already")
        check_frozen()
    spec = json.loads(MODEL.read_text())
    df = pd.read_csv(args.table)
    df["pred_model"] = model_predict(spec, df)
    df["pred_rules"] = rules_predict(df)

    say(f"Frozen model: {spec['type']} (tag {TAG}); table {args.table}, {len(df)} test failures")
    summary = {}
    for name, (fam, kinds, classes) in PARTS.items():
        d = df[(df.family == fam) & df.test_kind.isin(kinds) & df.label.isin(classes)].reset_index(drop=True)
        y, pm, pr_ = d.label.to_numpy(), d.pred_model.to_numpy(), d.pred_rules.to_numpy()
        sm, fm = stats(y, pm, classes)
        sr, fr = stats(y, pr_, classes)
        cells, f1ci = bootstrap(d, y, pm, pr_, classes)
        say(f"\n=== Primary part: {name} ({len(d)} failures: " + ", ".join(f"{c} {int((y == c).sum())}" for c in classes) + ") ===")
        for m, s, p in (("model", sm, pm), ("rules", sr, pr_)):
            say(f"{m}: predicted counts " + ", ".join(f"{c} {int((p == c).sum())}" for c in sorted(set(p) | set(classes))))
            for c in classes:
                (pv, rv) = s[c]
                ok = "PASS" if pv >= THRESHOLD and rv >= THRESHOLD else "fail"
                say(f"  {c:15s} precision {pv:.2f} {cells[(m, c, 0)]}  recall {rv:.2f} {cells[(m, c, 1)]}  [{ok}]")
        say(f"macro F1: model {fm:.2f} {f1ci['model']}, rules {fr:.2f} {f1ci['rules']}, difference {fm - fr:+.2f} {f1ci['diff']}")
        summary[name] = (fm, fr)

    mm = np.mean([v[0] for v in summary.values()])
    mr = np.mean([v[1] for v in summary.values()])
    say(f"\nMean macro F1 over the two parts: model {mm:.2f}, rules {mr:.2f}")
    say("DECISION (by the rule fixed in the design doc): " +
        ("use the frozen model in PRAT" if mm > mr else "use the rules in PRAT (the model does not beat them)"))

    say("\n=== Secondary batches (no criterion) ===")
    for fam, kind in SECONDARY:
        d = df[(df.family == fam) & (df.test_kind == kind)]
        if d.empty:
            continue
        say(f"{fam} / {kind}: {len(d)} failures")
        for m, col in (("model", "pred_model"), ("rules", "pred_rules")):
            ct = pd.crosstab(d.label, d[col])
            say(f"  {m}: " + "; ".join(f"true {a}: " + ", ".join(f"{b} {ct.loc[a, b]}" for b in ct.columns if ct.loc[a, b])
                                     for a in ct.index))

    if not args.dry_run:
        RESULTS.write_text("\n".join(OUT) + "\n")
        say(f"\nwrote {RESULTS}. Record it in the design doc before doing anything else.")


if __name__ == "__main__":
    main()