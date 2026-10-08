"""Exploratory evaluation of the flaky-condition classifier.

Compares feature sets on exactly the same conditions. Cross-validation is
leave-one-scenario-out: every magnitude and profile of one scenario is
held out together. A random split would be misleading, because
neighbouring magnitudes of the same scenario behave almost identically and
would leak across the split.

Reports AUC and precision/recall at a 0.5 threshold with bootstrap 95%
confidence intervals (positives are few, so the intervals are wide), plus
the best balanced precision/recall over all thresholds. That last figure is
OPTIMISTIC: the threshold is tuned on the same predictions, so treat it as
an upper bound on what the features could achieve.

Usage (from the repo root, after build_training_table.py):
    python analysis/evaluate_baseline.py [path to a training table]
The default table is data/training-table.csv. For the stricter labelling
rule build with --min-minority 3 and pass data/training-table-min3.csv.
Conditions labelled "ambiguous" are excluded from modelling and counted.
"""
import sys
from pathlib import Path

import numpy as np
import pandas as pd
from sklearn.linear_model import LogisticRegression
from sklearn.metrics import precision_recall_curve, precision_recall_fscore_support, roc_auc_score
from sklearn.model_selection import LeaveOneGroupOut
from sklearn.pipeline import make_pipeline
from sklearn.preprocessing import StandardScaler

TABLE = Path(sys.argv[1]) if len(sys.argv) > 1 else Path("data/training-table.csv")
FEATURE_SETS = {
    "total duration ratio": ["feat_median_pass_ratio", "feat_max_pass_ratio"],
    "assertion step": ["feat_then_median_s", "feat_then_max_s"],
    "assertion + action steps": [
        "feat_then_median_s", "feat_then_max_s", "feat_action_median_s", "feat_action_max_s",
    ],
}

# Pre-registered 8th Oct (docs/harness-design.md): one added feature, the
# spread of the assertion step across passing runs. Judged on the
# conditions that have it, against the plain assertion step on the SAME
# conditions, so the comparison is like for like.
SPREAD_SET = ("assertion step + spread", ["feat_then_median_s", "feat_then_max_s", "feat_then_std_s"])


def predict(d, cols):
    X, y, groups = d[cols].values, d.is_flaky.values, d.scenario.values
    pred = np.full(len(d), np.nan)
    for train, test in LeaveOneGroupOut().split(X, y, groups):
        if y[train].sum() == 0:
            continue
        model = make_pipeline(
            StandardScaler(), LogisticRegression(class_weight="balanced", max_iter=1000)
        ).fit(X[train], y[train])
        pred[test] = model.predict_proba(X[test])[:, 1]
    ok = ~np.isnan(pred)
    return y[ok], pred[ok]


def report(d, label, cols):
    y, p = predict(d, cols)
    flagged = p >= 0.5
    prec, rec, _, _ = precision_recall_fscore_support(y, flagged, average="binary", zero_division=0)
    rng = np.random.default_rng(0)
    precs, recs = [], []
    for _ in range(2000):
        i = rng.integers(0, len(y), len(y))
        if y[i].sum() == 0 or flagged[i].sum() == 0:
            continue
        a, b, _, _ = precision_recall_fscore_support(y[i], flagged[i], average="binary", zero_division=0)
        precs.append(a)
        recs.append(b)
    ci = lambda v: f"{np.percentile(v, 2.5):.2f}-{np.percentile(v, 97.5):.2f}"
    pr, rc, _ = precision_recall_curve(y, p)
    best = max(min(a, b) for a, b in zip(pr, rc))
    print(
        f"  {label:<26} AUC {roc_auc_score(y, p):.2f} | at 0.5: precision {prec:.2f} ({ci(precs)}), "
        f"recall {rec:.2f} ({ci(recs)}) | best balanced (optimistic) {best:.2f}"
    )


if __name__ == "__main__":
    t = pd.read_csv(TABLE)
    print(f"Table: {TABLE} | " + ", ".join(f"{k} {v}" for k, v in t.label.value_counts().items()))
    print("(ambiguous conditions are excluded from modelling)\n" if (t.label == "ambiguous").any() else "")
    modelling = t[t.label.isin(["always_pass", "flaky"])]  # always_fail has no passing runs
    for name, d in (
        ("All profiles", modelling),
        ("Delay-type faults (Timing, Latency)", modelling[modelling.profile.isin(["Timing", "Latency"])]),
    ):
        before = len(d)
        d = d.dropna(subset=sum(FEATURE_SETS.values(), []))
        note = f" ({before - len(d)} dropped for missing step features)" if len(d) != before else ""
        print(f"{name}: {len(d)} conditions, {int(d.is_flaky.sum())} flaky{note}")
        for label, cols in FEATURE_SETS.items():
            report(d, label, cols)
        if "feat_then_std_s" in d.columns:
            common = d.dropna(subset=["feat_then_std_s"])
            print(f"  Spread comparison on the {len(common)} conditions that have it "
                  f"({len(d) - len(common)} dropped for fewer than 2 passing runs, "
                  f"{int(common.is_flaky.sum())} flaky):")
            report(common, "assertion step", FEATURE_SETS["assertion step"])
            report(common, SPREAD_SET[0], SPREAD_SET[1])
        print()