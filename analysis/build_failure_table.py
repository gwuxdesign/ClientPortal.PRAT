"""Build the root-cause failure tables (Objective 3).

One row per failed run. The label is the mechanism of the injected fault:
slow_response (Timing, Latency), failed_request (Retry), overload (Load).

Two files, so that development code cannot touch test rows by accident:
    data/failure-table.csv        development failures
    data/failure-test-table.csv   test failures (written only with --test)

Test rows are: the held-out Latency and Timing conditions, Retry at 4 and 5
and Load at 4750, 6500 and 7000 (the same family test batches), and every
row where a profile ran against a family other than its own (the cross
category batches). They are scored once, after the model is frozen. Do not
open the test table before then.

Features are scenario agnostic by design (see docs/harness-design.md, 29th
Oct 2026): nothing that names the page, the profile or the magnitude. The
columns in FEATURES are the only ones a model may use; every other column is
metadata for grouping and reporting.

Failure text for runs captured before 8th Oct comes from
data/recovered-failure-text.csv and is flagged recovered = 1.

    python analysis/build_failure_table.py [--test]
"""
import argparse
import re
from pathlib import Path

import numpy as np
import pandas as pd

SRC = Path("data/harness-results.csv")
RECOVERED = Path("data/recovered-failure-text.csv")
DEV_OUT = Path("data/failure-table.csv")
TEST_OUT = Path("data/failure-test-table.csv")

DEFAULT_CATEGORY = {"Timing": "Login", "Latency": "Navigation", "Retry": "PasswordReset", "Load": "Documents"}
HELD_OUT = {"Timing": [3250, 3750], "Latency": [2250, 2750, 3250, 3750]}
RETRY_TEST = [4, 5]
LOAD_TEST = [4750, 6500, 7000]
LABEL = {"Timing": "slow_response", "Latency": "slow_response", "Retry": "failed_request", "Load": "overload"}

FEATURES = [
    "exc_is_timeout",        # System.TimeoutException (10 s) as opposed to a failed expectation
    "exc_is_expectation",    # a Playwright expectation failed (5 s)
    "timeout_ms",            # the timeout stated in the message, 0 if none
    "step_is_given", "step_is_when", "step_is_then",
    "failed_step_position",  # 1 based position of the step that failed
    "secs_in_failed_step",
    "secs_before_failed_step",
    "net_events",            # failed network requests recorded (0 to 3)
    "net_aborted", "net_failed", "net_other",
    "net_first_is_post",
]
KEY = ["run_id", "fault_magnitude", "iteration", "scenario_name"]
TEXT = ["error_message", "failed_step", "step_seconds", "network_events"]


def parse_steps(text):
    if not isinstance(text, str) or not text:
        return []
    return [float(p.split("=")[1]) for p in text.split(";") if "=" in p]


def features(row):
    msg = row["error_message"] if isinstance(row["error_message"], str) else ""
    step = row["failed_step"] if isinstance(row["failed_step"], str) else ""
    net = row["network_events"] if isinstance(row["network_events"], str) else ""
    secs = parse_steps(row["step_seconds"])
    exc = msg.split(" : ")[0].strip()
    timeout = re.search(r"(?i)timeout (\d+)\s*ms", msg)
    kw = step.split(" ")[0] if step else ""
    events = [e for e in net.split(" | ") if e.strip()]
    first = events[0].split(" ")[0] if events else ""
    return pd.Series({
        "exc_is_timeout": int(exc.endswith("TimeoutException")),
        "exc_is_expectation": int(exc.endswith("PlaywrightException")),
        "timeout_ms": int(timeout.group(1)) if timeout else 0,
        "step_is_given": int(kw == "Given"),
        "step_is_when": int(kw == "When"),
        "step_is_then": int(kw == "Then"),
        "failed_step_position": len(secs),
        "secs_in_failed_step": secs[-1] if secs else np.nan,
        "secs_before_failed_step": float(sum(secs[:-1])) if secs else np.nan,
        "net_events": len(events),
        "net_aborted": sum("ERR_ABORTED" in e for e in events),
        "net_failed": sum("ERR_FAILED" in e for e in events),
        "net_other": sum(("net::" in e) and ("ERR_ABORTED" not in e) and ("ERR_FAILED" not in e) for e in events),
        "net_first_is_post": int(first == "POST"),
    })


def load():
    df = pd.read_csv(SRC)
    df = df[df.outcome == "Failed"].copy()
    rec = pd.read_csv(RECOVERED)
    df["recovered"] = 0
    df = df.merge(rec[KEY + TEXT], on=KEY, how="left", suffixes=("", "_rec"))
    missing = df.error_message.isna() | (df.error_message.astype(str) == "")
    for c in TEXT:
        df.loc[missing, c] = df.loc[missing, c + "_rec"]
    df.loc[missing & df.error_message.notna(), "recovered"] = 1
    df = df.drop(columns=[c + "_rec" for c in TEXT])
    return df


def tag(df):
    default_family = df.target_category.eq(df.fault_profile.map(DEFAULT_CATEGORY))
    held = pd.Series([m in HELD_OUT.get(p, []) for p, m in zip(df.fault_profile, df.fault_magnitude)], index=df.index)
    retry = (df.fault_profile == "Retry") & df.fault_magnitude.isin(RETRY_TEST) & default_family
    load = (df.fault_profile == "Load") & df.fault_magnitude.isin(LOAD_TEST) & default_family
    df["test_kind"] = np.select(
        [~default_family, held, retry, load],
        ["cross", "heldout_delay", "retry_test", "load_test"], default="")
    df["split"] = np.where(df.test_kind == "", "development", "test")
    df["label"] = df.fault_profile.map(LABEL)
    df["family"] = df.target_category
    return df


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--test", action="store_true", help="write the test table (do not open it before the model is frozen)")
    args = ap.parse_args()

    df = tag(load())
    no_text = df.error_message.isna() | (df.error_message.astype(str) == "")
    if no_text.any():
        print(f"{int(no_text.sum())} failures have no text and are left out")
        df = df[~no_text]
    feats = df.apply(features, axis=1)
    meta = ["run_id", "scenario_name", "fault_profile", "fault_magnitude", "family", "iteration",
            "label", "split", "test_kind", "recovered"]
    table = pd.concat([df[meta].reset_index(drop=True), feats.reset_index(drop=True)], axis=1)
    assert not set(FEATURES) & set(meta), "a feature column is also metadata"
    assert list(feats.columns) == FEATURES

    if not args.test:
        dev = table[table.split == "development"].drop(columns=["split", "test_kind"])
        dev.to_csv(DEV_OUT, index=False)
        print(f"wrote {DEV_OUT}: {len(dev)} development failures "
              f"({int(dev.recovered.sum())} recovered)")
        print(dev.groupby(["label", "fault_profile"]).size().to_string())
        print("scenarios per label:", dev.groupby("label").scenario_name.nunique().to_dict())
    else:
        test = table[table.split == "test"].drop(columns=["split"])
        test.to_csv(TEST_OUT, index=False)
        print(f"wrote {TEST_OUT}: {len(test)} test failures. Do not open it until the model is frozen.")
        print(test.groupby(["family", "test_kind", "label"]).size().to_string())


if __name__ == "__main__":
    main()