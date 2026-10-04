"""Exploratory baseline evaluation of the flaky-condition classifier.

Cross-validation is leave-one-scenario-out: every magnitude and profile of
one scenario is held out together. A random split would be misleading,
because neighbouring magnitudes of the same scenario behave almost
identically and would leak across the split.

Reports AUC and precision/recall at a 0.5 threshold, with bootstrap 95%
confidence intervals (positives are few, so the intervals are wide).

Usage (from the repo root, after build_training_table.py):
    python analysis/evaluate_baseline.py
"""
from pathlib import Path

import numpy as np
import pandas as pd
from sklearn.linear_model import LogisticRegression
from sklearn.metrics import precision_recall_fscore_support, roc_auc_score
from sklearn.model_selection import LeaveOneGroupOut
from sklearn.pipeline import make_pipeline
from sklearn.preprocessing import StandardScaler

TABLE = Path("data/training-table.csv")
FEATURES = ["feat_median_pass_ratio", "feat_max_pass_ratio"]


def evaluate(d: pd.DataFrame, name: str) -> None:
    X, y, groups = d[FEATURES].values, d.is_flaky.values, d.scenario.values
    pred = np.full(len(d), np.nan)
    for train, test in LeaveOneGroupOut().split(X, y, groups):
        if y[train].sum() == 0:
            continue
        model = make_pipeline(
            StandardScaler(), LogisticRegression(class_weight="balanced", max_iter=1000)
        ).fit(X[train], y[train])
        pred[test] = model.predict_proba(X[test])[:, 1]
    ok = ~np.isnan(pred)
    y, p = y[ok], pred[ok]
    flagged = p >= 0.5
    prec, rec, _, _ = precision_recall_fscore_support(
        y, flagged, average="binary", zero_division=0
    )
    rng = np.random.default_rng(0)
    precs, recs = [], []
    for _ in range(2000):
        i = rng.integers(0, len(y), len(y))
        if y[i].sum() == 0 or flagged[i].sum() == 0:
            continue
        a, b, _, _ = precision_recall_fscore_support(
            y[i], flagged[i], average="binary", zero_division=0
        )
        precs.append(a)
        recs.append(b)
    ci = lambda v: f"{np.percentile(v, 2.5):.2f} to {np.percentile(v, 97.5):.2f}"
    print(f"{name}: {len(y)} conditions, {y.sum()} flaky")
    print(f"  AUC {roc_auc_score(y, p):.2f}")
    print(f"  precision {prec:.2f} (95% CI {ci(precs)})")
    print(f"  recall    {rec:.2f} (95% CI {ci(recs)})")
    print(f"  flagged {flagged.sum()} of {len(y)}\n")


if __name__ == "__main__":
    t = pd.read_csv(TABLE)
    modelling = t[t.label.isin(["always_pass", "flaky"])]  # always_fail has no passing runs
    evaluate(modelling, "All profiles")
    evaluate(modelling[modelling.profile.isin(["Timing", "Latency"])], "Delay-type faults (Timing, Latency)")
