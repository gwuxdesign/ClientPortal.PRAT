"""Final evaluation of the flaky-condition classifier on the HELD-OUT table.

Run ONCE, after the freeze commit (tag final-model-freeze) described in
docs/harness-design.md. Nothing here is tuned: the model, its features,
the labelling rule and the threshold are fixed below and in that entry.

The model is trained on every development condition (always_pass and
flaky, all profiles) and applied to the held-out conditions, which
played no part in its design. Reported for all held-out conditions and
by profile, with precision and recall at the fixed threshold and
bootstrap 95% intervals (2000 resamples, seed 0, resamples without a
positive or without a flagged condition skipped, as in
evaluate_baseline.py).

The assertion step model is the frozen one. The total duration model is
reported for context only; it is not a selected alternative.

Usage (from the repo root):
    python analysis/build_training_table.py
    python analysis/build_training_table.py --heldout-only
    python analysis/final_evaluation.py
and, for the stricter labelling rule reported beside it:
    python analysis/build_training_table.py --min-minority 3
    python analysis/build_training_table.py --heldout-only --min-minority 3
    python analysis/final_evaluation.py data/training-table-min3.csv data/heldout-table-min3.csv
"""
import sys
from pathlib import Path

import numpy as np
import pandas as pd
from sklearn.linear_model import LogisticRegression
from sklearn.metrics import precision_recall_fscore_support
from sklearn.pipeline import make_pipeline
from sklearn.preprocessing import StandardScaler

THRESHOLD = 0.5
MODELS = {
    "assertion step (FROZEN MODEL)": ["feat_then_median_s", "feat_then_max_s"],
    "total duration ratio (context only)": ["feat_median_pass_ratio", "feat_max_pass_ratio"],
}
TRAIN = Path(sys.argv[1]) if len(sys.argv) > 1 else Path("data/training-table.csv")
TEST = Path(sys.argv[2]) if len(sys.argv) > 2 else Path("data/heldout-table.csv")


def usable(t, cols):
    """Conditions with a clear label and complete features."""
    return t[t.label.isin(["always_pass", "flaky"])].dropna(subset=cols)


def ci(v):
    return f"{np.percentile(v, 2.5):.2f} to {np.percentile(v, 97.5):.2f}"


def score(name, scope, y, flagged):
    tp = int(((flagged == 1) & (y == 1)).sum())
    fp = int(((flagged == 1) & (y == 0)).sum())
    fn = int(((flagged == 0) & (y == 1)).sum())
    tn = int(((flagged == 0) & (y == 0)).sum())
    prec, rec, _, _ = precision_recall_fscore_support(y, flagged, average="binary", zero_division=0)
    rng = np.random.default_rng(0)
    ps, rs = [], []
    for _ in range(2000):
        i = rng.integers(0, len(y), len(y))
        if y[i].sum() == 0 or flagged[i].sum() == 0:
            continue
        a, b, _, _ = precision_recall_fscore_support(y[i], flagged[i], average="binary", zero_division=0)
        ps.append(a)
        rs.append(b)
    print(
        f"  {scope:<12} {len(y):>3} conditions, {int(y.sum()):>2} flaky | TP {tp} FP {fp} FN {fn} TN {tn} | "
        f"precision {prec:.2f} ({ci(ps) if ps else 'n/a'}), recall {rec:.2f} ({ci(rs) if rs else 'n/a'})"
    )


if __name__ == "__main__":
    train, test = pd.read_csv(TRAIN), pd.read_csv(TEST)
    print(f"Train: {TRAIN} ({', '.join(f'{k} {v}' for k, v in train.label.value_counts().items())})")
    print(f"Test:  {TEST} ({', '.join(f'{k} {v}' for k, v in test.label.value_counts().items())})")
    print(f"Threshold {THRESHOLD}; ambiguous and always_fail conditions are not modelled\n")
    for name, cols in MODELS.items():
        tr, te = usable(train, cols), usable(test, cols)
        model = make_pipeline(
            StandardScaler(), LogisticRegression(class_weight="balanced", max_iter=1000)
        ).fit(tr[cols].values, tr.is_flaky.values)
        te = te.assign(flag=(model.predict_proba(te[cols].values)[:, 1] >= THRESHOLD).astype(int))
        print(f"{name}: trained on {len(tr)} conditions ({int(tr.is_flaky.sum())} flaky)")
        score(name, "all held-out", te.is_flaky.values, te.flag.values)
        for profile in sorted(te.profile.unique()):
            sub = te[te.profile == profile]
            score(name, profile, sub.is_flaky.values, sub.flag.values)
        print()