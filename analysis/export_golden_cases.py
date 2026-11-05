"""Export golden cases for the C# port of the root-cause classifier.

One case per recorded failure (development and test tables, 799 in all):
the raw inputs the C# code receives (error message, failed step, step
seconds, network events), the 14 features the Python builds from them, whether
the failure has timeout or failed request evidence (the gate), and what the
frozen model predicts, with class probabilities. The C# feature builder and
classifier must reproduce the features and labels exactly, and the
probabilities to within 1e-9.

This covers feature building and scoring from the already extracted strings.
Extracting those strings from a test's raw standard output is a separate step
and is tested separately.

    python analysis/export_golden_cases.py [output.json]
"""
import json
import sys
from pathlib import Path

import numpy as np
import pandas as pd

from build_failure_table import FEATURES, features, load, tag
from score_root_cause import MODEL, model_predict

OUT = Path("TestRunner.Web.Tests/Golden/root-cause-golden.json")
INPUTS = ["error_message", "failed_step", "step_seconds", "network_events"]


def probabilities(spec, X):
    z = (X - np.array(spec["mean"])) / np.array(spec["scale"])
    s = z @ np.array(spec["coef"]).T + np.array(spec["intercept"])
    e = np.exp(s - s.max(axis=1, keepdims=True))
    return e / e.sum(axis=1, keepdims=True)


def clean(v):
    if isinstance(v, (float, np.floating)):
        return None if np.isnan(v) else float(v)
    if isinstance(v, (np.integer,)):
        return int(v)
    return v


def main():
    out = Path(sys.argv[1]) if len(sys.argv) > 1 else OUT
    spec = json.loads(MODEL.read_text())
    df = tag(load())
    df = df[df.error_message.notna() & (df.error_message.astype(str) != "")].reset_index(drop=True)
    feats = df.apply(features, axis=1)[FEATURES]

    # consistency with the committed tables
    dev = pd.read_csv("data/failure-table.csv")
    assert len(df[df.split == "development"]) == len(dev)
    assert np.allclose(feats[df.split == "development"].to_numpy(dtype=float),
                       dev[FEATURES].to_numpy(dtype=float), equal_nan=True), "features differ from failure-table.csv"

    pred = model_predict(spec, feats)
    prob = probabilities(spec, feats.to_numpy(dtype=float))
    assert (np.array(spec["classes"], dtype=object)[prob.argmax(axis=1)] == pred).all()
    gate = (feats.exc_is_timeout == 1) | (feats.timeout_ms > 0) | (feats.net_events > 0)

    cases = []
    for i in range(len(df)):
        cases.append({
            "id": i,
            "split": df.split[i],
            "true_label": df.label[i],
            "inputs": {c: (None if pd.isna(df[c][i]) else str(df[c][i])) for c in INPUTS},
            "features": {f: clean(feats[f][i]) for f in FEATURES},
            "gate": bool(gate[i]),
            "predicted": str(pred[i]),
            "probabilities": {c: float(prob[i][k]) for k, c in enumerate(spec["classes"])},
        })
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(json.dumps({"classes": spec["classes"], "features": FEATURES, "cases": cases}, indent=1))
    print(f"wrote {out}: {len(cases)} cases "
          f"({sum(c['split'] == 'development' for c in cases)} development, "
          f"{sum(c['split'] == 'test' for c in cases)} test), {int(gate.sum())} pass the gate")


if __name__ == "__main__":
    main()