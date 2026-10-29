"""Rules baseline for the root-cause classifier (Objective 3).

Three ordered rules, written by hand and fixed before any model is fitted.
Nothing is learned from data, so there is nothing to tune and nothing to
freeze: the rules are the baseline, and any model has to beat them.

    1. A failed network request was recorded (net::ERR_FAILED)  -> failed_request
    2. Otherwise a 10 s TimeoutException (the step itself timed
       out, not an expectation)                                  -> overload
    3. Otherwise                                                 -> slow_response

Reasoning, from how the profiles work rather than from scores:
    Retry aborts requests outright, so the browser reports a failed request.
    Load makes a page too heavy to finish, so a step waits out its full
    10 s timeout with no request failing.
    Timing and Latency slow every request down, so an expectation (5 s)
    runs out before the page settles.

Disclosure: the rules were written after viewing development feature means
by label (never the test table). They are a sensible reading of the
mechanisms, not a blind guess, and the development score below is
optimistic for that reason. The test score is the one that counts.

    python analysis/root_cause_rules.py
"""
import numpy as np
import pandas as pd

DEV = "data/failure-table.csv"
CLASSES = ["slow_response", "failed_request", "overload"]
SEED = 20261029
BOOTSTRAPS = 2000


def predict(df):
    """Apply the three rules to a table with the FEATURES columns."""
    return np.select(
        [df.net_failed > 0, df.exc_is_timeout == 1],
        ["failed_request", "overload"],
        default="slow_response",
    )


def per_class(y, p):
    out = {}
    for c in CLASSES:
        tp = int(((p == c) & (y == c)).sum())
        pp = int((p == c).sum())
        ap = int((y == c).sum())
        out[c] = (tp / pp if pp else np.nan, tp / ap if ap else np.nan)
    return out


def cluster_bootstrap(df, y, p):
    """Resample whole conditions (scenario, profile, magnitude), not rows."""
    key = df.scenario_name + "|" + df.fault_profile + "|" + df.fault_magnitude.astype(str)
    groups = {k: np.flatnonzero((key == k).to_numpy()) for k in key.unique()}
    names = list(groups)
    rng = np.random.default_rng(SEED)
    res = {c: ([], []) for c in CLASSES}
    for _ in range(BOOTSTRAPS):
        idx = np.concatenate([groups[names[i]] for i in rng.integers(0, len(names), len(names))])
        for c, (pr, rc) in per_class(y[idx], p[idx]).items():
            res[c][0].append(pr)
            res[c][1].append(rc)
    ci = lambda v: tuple(float(x) for x in np.round(np.nanpercentile(v, [2.5, 97.5]), 2))
    return {c: (ci(res[c][0]), ci(res[c][1])) for c in CLASSES}


def report(df, title):
    y = df.label.to_numpy()
    p = predict(df)
    print(f"\n{title}: {len(df)} failures, {df.groupby(['scenario_name','fault_profile','fault_magnitude']).ngroups} conditions")
    print(pd.crosstab(pd.Series(y, name="actual"), pd.Series(p, name="predicted")).reindex(
        index=CLASSES, columns=CLASSES, fill_value=0).to_string())
    pc, ci = per_class(y, p), cluster_bootstrap(df, y, p)
    for c in CLASSES:
        print(f"  {c:15s} precision {pc[c][0]:.2f} {ci[c][0]}   recall {pc[c][1]:.2f} {ci[c][1]}")
    print(f"  accuracy {np.mean(y == p):.2f}   macro recall {np.nanmean([pc[c][1] for c in CLASSES]):.2f}")


def main():
    dev = pd.read_csv(DEV)
    report(dev, "development, all rows")
    report(dev[dev.recovered == 0], "development, live capture only")


if __name__ == "__main__":
    main()