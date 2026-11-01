"""Development evaluation of the root-cause classifier (Objective 3).

Compares, on development failures only:
    rules       analysis/root_cause_rules.py (nothing fitted)
    tree        decision tree, depth at most 3, balanced class weights
    logistic    standard scaling + logistic regression, balanced
under leave-one-scenario-out validation (each scenario in turn is held back,
the model is fitted on the rest, and the held back failures are predicted;
predictions are pooled). Intervals come from a cluster bootstrap that
resamples whole conditions (scenario, profile, magnitude), 2000 resamples.

Settings, all reported:
    all rows            every development failure
    live capture only   without the 211 rows recovered from run logs
Ablations (feature groups removed), reported for every candidate:
    full, no_network, no_timing, no_exception_step

Selection rule, fixed before this script was run:
    The candidate is the model (tree or logistic, not rules) with the higher
    pooled macro F1 on all rows with the full feature set. If the two are
    within 0.01, the tree is chosen because it is easier to read and to port
    to .NET. The rules are never chosen here: they are the baseline, and the
    test decides between the frozen model and the rules (see the design doc).

Limits to keep in mind when reading the output. Leave-one-scenario-out holds
back scenarios inside a family, so it does not test crossing families, which
only the test batches do. Failed request has two scenarios, so when one is
held back the model sees failed requests from only one.

    python analysis/evaluate_root_cause.py
    python analysis/evaluate_root_cause.py --freeze   # fit the chosen model on all
                                                       # development rows and write JSON
This script never reads the test table.
"""
import argparse
import json
from pathlib import Path

import numpy as np
import pandas as pd
from sklearn.linear_model import LogisticRegression
from sklearn.pipeline import make_pipeline
from sklearn.preprocessing import StandardScaler
from sklearn.tree import DecisionTreeClassifier

from build_failure_table import FEATURES
from root_cause_rules import CLASSES, predict as rules_predict

DEV = Path("data/failure-table.csv")
FROZEN = Path("data/root-cause-frozen-model.json")
SEED = 20261101
BOOTSTRAPS = 2000

GROUPS = {
    "full": [],
    "no_network": ["net_events", "net_aborted", "net_failed", "net_other", "net_first_is_post"],
    "no_timing": ["secs_in_failed_step", "secs_before_failed_step", "failed_step_position"],
    "no_exception_step": ["exc_is_timeout", "exc_is_expectation", "timeout_ms",
                          "step_is_given", "step_is_when", "step_is_then"],
}


def make(name):
    if name == "tree":
        return DecisionTreeClassifier(max_depth=3, class_weight="balanced", random_state=SEED)
    return make_pipeline(StandardScaler(),
                         LogisticRegression(class_weight="balanced", max_iter=2000, random_state=SEED))


def loso(df, feats, name):
    y = df.label.to_numpy()
    X = df[feats].to_numpy(dtype=float)
    pred = np.empty(len(df), dtype=object)
    for s in df.scenario_name.unique():
        test = (df.scenario_name == s).to_numpy()
        m = make(name).fit(X[~test], y[~test])
        pred[test] = m.predict(X[test])
    return pred


def macro_f1(y, p):
    f = []
    for c in CLASSES:
        tp = ((p == c) & (y == c)).sum()
        pp, ap = (p == c).sum(), (y == c).sum()
        pr = tp / pp if pp else 0.0
        rc = tp / ap if ap else 0.0
        f.append(2 * pr * rc / (pr + rc) if pr + rc else 0.0)
    return float(np.mean(f))


def per_class(y, p):
    out = {}
    for c in CLASSES:
        tp = int(((p == c) & (y == c)).sum())
        pp, ap = int((p == c).sum()), int((y == c).sum())
        out[c] = (tp / pp if pp else np.nan, tp / ap if ap else np.nan)
    return out


def boot_ci(df, y, p):
    key = (df.scenario_name + "|" + df.fault_profile + "|" + df.fault_magnitude.astype(str)).to_numpy()
    groups = [np.flatnonzero(key == k) for k in np.unique(key)]
    rng = np.random.default_rng(SEED)
    vals = []
    for _ in range(BOOTSTRAPS):
        idx = np.concatenate([groups[i] for i in rng.integers(0, len(groups), len(groups))])
        vals.append(macro_f1(y[idx], p[idx]))
    return tuple(float(x) for x in np.round(np.percentile(vals, [2.5, 97.5]), 2))


def evaluate(df, title):
    y = df.label.to_numpy()
    print(f"\n=== {title}: {len(df)} failures, {df.scenario_name.nunique()} scenarios ===")
    rows, preds = [], {}
    p = rules_predict(df)
    rows.append(("rules", "full", macro_f1(y, p), boot_ci(df, y, p), np.mean(p == y)))
    preds[("rules", "full")] = p
    for name in ("tree", "logistic"):
        for g, drop in GROUPS.items():
            feats = [f for f in FEATURES if f not in drop]
            p = loso(df, feats, name)
            rows.append((name, g, macro_f1(y, p), boot_ci(df, y, p), np.mean(p == y)))
            preds[(name, g)] = p
    print(f"{'model':9s} {'features':18s} {'macroF1':>7s} {'95% CI':>14s} {'acc':>5s}")
    for m, g, f, ci, a in rows:
        print(f"{m:9s} {g:18s} {f:7.2f} {str(ci):>14s} {a:5.2f}")
    print("per class, full features (precision, recall):")
    for m in ("rules", "tree", "logistic"):
        pc = per_class(y, preds[(m, "full")])
        print(f"  {m:9s} " + "   ".join(f"{c} {pc[c][0]:.2f}/{pc[c][1]:.2f}" for c in CLASSES))
    return {(m, g): f for m, g, f, _, _ in rows}


def choose(scores):
    t, l = scores[("tree", "full")], scores[("logistic", "full")]
    return "tree" if t >= l - 0.01 else "logistic"


def export(model, name, feats):
    if name == "tree":
        t = model.tree_
        return {"type": "decision_tree", "features": feats, "classes": [str(c) for c in model.classes_],
                "children_left": t.children_left.tolist(), "children_right": t.children_right.tolist(),
                "feature": t.feature.tolist(), "threshold": t.threshold.tolist(),
                "value": t.value[:, 0, :].tolist()}
    sc, lr = model.steps[0][1], model.steps[1][1]
    return {"type": "logistic_regression", "features": feats, "classes": [str(c) for c in lr.classes_],
            "mean": sc.mean_.tolist(), "scale": sc.scale_.tolist(),
            "coef": lr.coef_.tolist(), "intercept": lr.intercept_.tolist()}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--freeze", action="store_true")
    args = ap.parse_args()
    dev = pd.read_csv(DEV)
    scores = evaluate(dev, "all rows")
    evaluate(dev[dev.recovered == 0].reset_index(drop=True), "live capture only")
    pick = choose(scores)
    print(f"\nSelection rule applied: tree {scores[('tree','full')]:.2f}, logistic "
          f"{scores[('logistic','full')]:.2f} (all rows, full features) -> candidate: {pick}")

    if args.freeze:
        m = make(pick).fit(dev[FEATURES].to_numpy(dtype=float), dev.label.to_numpy())
        FROZEN.write_text(json.dumps(export(m, pick, FEATURES), indent=1))
        print(f"fitted {pick} on all {len(dev)} development failures; wrote {FROZEN}")
        print("Now tag it:  git tag root-cause-freeze && git push origin root-cause-freeze")


if __name__ == "__main__":
    main()