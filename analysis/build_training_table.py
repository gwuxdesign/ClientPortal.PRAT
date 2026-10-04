"""Build the condition-level training table from data/harness-results.csv.

Unit of analysis: one condition = (scenario, fault profile, fault magnitude)
with at least MIN_REPEATS repeats.

Label: flaky = the repeats gave BOTH passes and failures. Always-pass and
always-fail conditions are not flaky (an always-fail condition is
deterministically broken by the fault).

Leakage rule (important): the label is defined from the outcomes of these
same repeats, so features must not be built from anything that reveals
outcomes. Features here use PASSING runs only, measured against the
scenario's own no-fault baseline. Never use: failed-run durations, failure
counts, the number of passing runs, the fault profile or the injected
magnitude. Feature columns are prefixed feat_ so downstream code can select
them explicitly and cannot pick up a leaky column by accident.

Usage (from the repo root):  python analysis/build_training_table.py
"""
from pathlib import Path

import pandas as pd

MIN_REPEATS = 5
SRC = Path("data/harness-results.csv")
OUT = Path("data/training-table.csv")


def build(df: pd.DataFrame) -> pd.DataFrame:
    df = df.copy()
    df["passed"] = df["outcome"].eq("Passed")

    # Per-scenario no-fault baseline: median duration of passing runs at magnitude 0.
    baseline = (
        df[(df.fault_magnitude == 0) & df.passed]
        .groupby("scenario_name")
        .duration_seconds.median()
    )

    rows = []
    for (scenario, profile, magnitude), d in df.groupby(
        ["scenario_name", "fault_profile", "fault_magnitude"]
    ):
        repeats = len(d)
        if repeats < MIN_REPEATS:
            continue
        passing = d[d.passed].duration_seconds
        failures = repeats - len(passing)
        base = baseline.get(scenario)
        has_features = len(passing) > 0 and base is not None
        rows.append(
            {
                # identifiers and label metadata (NOT features)
                "scenario": scenario,
                "profile": profile,
                "magnitude": int(magnitude),
                "repeats": repeats,
                "failures": failures,
                "label": "flaky" if 0 < failures < repeats
                else "always_fail" if failures == repeats
                else "always_pass",
                "is_flaky": int(0 < failures < repeats),
                # features (passing runs only, relative to own baseline)
                "feat_median_pass_ratio": passing.median() / base if has_features else None,
                "feat_max_pass_ratio": passing.max() / base if has_features else None,
            }
        )
    return pd.DataFrame(rows)


if __name__ == "__main__":
    table = build(pd.read_csv(SRC))
    table.to_csv(OUT, index=False)
    print(f"wrote {OUT}: {len(table)} conditions")
    print(table.label.value_counts().to_string())
