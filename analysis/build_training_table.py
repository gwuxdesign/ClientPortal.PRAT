"""Build the condition-level training table from data/harness-results.csv.

Unit of analysis: one condition = (scenario, fault profile, fault magnitude)
with at least MIN_REPEATS repeats.

Label: flaky = the repeats gave BOTH passes and failures. Always-pass and
always-fail conditions are not flaky (an always-fail condition is
deterministically broken by the fault).

--min-minority N (default 1, the rule above) makes the label stricter: a
mixed condition counts as flaky only with at least N failures AND at least
N passes. A mixed condition with fewer is labelled "ambiguous" and is left
out of modelling. It is deliberately not counted as a negative (that would
punish a model for flagging a genuine rare failure) and not as a positive.
With N above 1 the table is written to data/training-table-minN.csv, so
the default table is never overwritten.

Rows: by default only rows that carry per-step timings (step_seconds) are
used, so every feature set is computed on exactly the same conditions.
Older rows have no step timings. With --all-rows they are included: the
total-duration features then use every passing run, while the step
features use only the step-timed passing runs and are empty (not zero)
for a condition that has none.

Leakage rule (important): the label is defined from the outcomes of these
same repeats, so features must not be built from anything that reveals
outcomes. Features use PASSING runs only. Never use: failed-run durations,
failure counts, the number of passing runs, the fault profile or the
injected magnitude. Feature columns are prefixed feat_ so downstream code
selects them explicitly and cannot pick up a leaky column by accident.

Features (all from passing runs of the condition):
  feat_median_pass_ratio, feat_max_pass_ratio
      total scenario duration relative to the scenario's no-fault baseline
  feat_then_median_s, feat_then_max_s
      the longest assertion ("Then") step of each run, in seconds. Assertions
      have a 5 second window, so this is the headroom signal. A step can hold
      more than one assertion, so it can exceed 5 s and still pass.
  feat_action_median_s, feat_action_max_s
      the longest non-assertion step of each run (actions have a 10 s
      timeout)
  feat_then_std_s
      standard deviation of the longest assertion step across the
      condition's passing runs: how unstable the headroom is. Needs at
      least 2 passing runs; empty otherwise.

Held-out conditions: HELD_OUT lists (profile, magnitude) pairs kept aside
for the final test, fixed in docs/harness-design.md BEFORE they were run.
By default they are EXCLUDED from the table, so nothing built here for
design or tuning can include them. --heldout-only builds a table of only
those conditions (data/heldout-table.csv). Do not use --heldout-only
until the final model and evaluation have been frozen and committed; the
design doc says what freezing means.

Usage (from the repo root):
    python analysis/build_training_table.py [--all-rows] [--min-minority N] [--heldout-only]
"""
import argparse
from pathlib import Path

import numpy as np
import pandas as pd

MIN_REPEATS = 5
HELD_OUT = {"Timing": [3250, 3750], "Latency": [2250, 2750, 3250, 3750]}
SRC = Path("data/harness-results.csv")
OUT = Path("data/training-table.csv")


def parse_steps(text):
    """'Given=2.1;Then=3.8' -> [('Given', 2.1), ('Then', 3.8)]"""
    if not isinstance(text, str) or not text:
        return []
    return [(p.split("=")[0], float(p.split("=")[1])) for p in text.split(";") if "=" in p]


def longest(steps, assertion):
    secs = [s for k, s in steps if (k == "Then") == assertion]
    return max(secs) if secs else 0.0


def build(df: pd.DataFrame, steps_only: bool = True, min_minority: int = 1, heldout_only: bool = False) -> pd.DataFrame:
    df = df.copy()
    if steps_only:
        df = df[df.step_seconds.notna() & (df.step_seconds != "")]
    df["passed"] = df["outcome"].eq("Passed")
    in_heldout = pd.Series(
        [m in HELD_OUT.get(pf, []) for pf, m in zip(df.fault_profile, df.fault_magnitude)],
        index=df.index,
    )
    df = df[in_heldout] if heldout_only else df[~in_heldout]
    # A row without step timings has UNKNOWN step features (NaN), not zero.
    has_steps = df.step_seconds.notna() & (df.step_seconds != "")
    parsed = df.step_seconds.map(parse_steps).where(has_steps)
    df["then_max"] = parsed.map(lambda s: longest(s, True) if isinstance(s, list) else np.nan)
    df["action_max"] = parsed.map(lambda s: longest(s, False) if isinstance(s, list) else np.nan)

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
        p = d[d.passed]
        failures = repeats - len(p)
        base = baseline.get(scenario)
        ok = len(p) > 0
        if failures == repeats:
            label = "always_fail"
        elif failures == 0:
            label = "always_pass"
        elif min(failures, repeats - failures) >= min_minority:
            label = "flaky"
        else:
            label = "ambiguous"
        rows.append(
            {
                # identifiers and label metadata (NOT features)
                "scenario": scenario,
                "profile": profile,
                "magnitude": int(magnitude),
                "repeats": repeats,
                "failures": failures,
                "label": label,
                "is_flaky": int(label == "flaky"),
                # features: passing runs only
                "feat_median_pass_ratio": p.duration_seconds.median() / base if ok and base else None,
                "feat_max_pass_ratio": p.duration_seconds.max() / base if ok and base else None,
                "feat_then_median_s": p.then_max.median() if ok else None,
                "feat_then_max_s": p.then_max.max() if ok else None,
                "feat_action_median_s": p.action_max.median() if ok else None,
                "feat_action_max_s": p.action_max.max() if ok else None,
                "feat_then_std_s": p.then_max.std() if len(p) >= 2 else None,
            }
        )
    return pd.DataFrame(rows)


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--all-rows", action="store_true")
    ap.add_argument("--min-minority", type=int, default=1)
    ap.add_argument("--heldout-only", action="store_true")
    args = ap.parse_args()
    table = build(
        pd.read_csv(SRC), steps_only=not args.all_rows, min_minority=args.min_minority,
        heldout_only=args.heldout_only,
    )
    stem = "heldout-table" if args.heldout_only else "training-table"
    out = OUT.with_name(f"{stem}.csv" if args.min_minority == 1 else f"{stem}-min{args.min_minority}.csv")
    if table.empty:
        raise SystemExit(f"No conditions to write to {out} (none found with at least {MIN_REPEATS} repeats).")
    table.to_csv(out, index=False)
    print(f"wrote {out}: {len(table)} conditions")
    print(table.label.value_counts().to_string())