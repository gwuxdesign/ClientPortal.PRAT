"""Bring data/harness-results.csv up to the current column set.

The workflow's append step refuses to write unless the file's header
matches its schema exactly. When columns are added, run this once, after
pulling and BEFORE pushing the updated workflow. Existing rows get empty
values for any new columns.

It only ever appends missing columns at the end, in order, so it works
from any earlier version of the file. Safe to run twice: it does nothing
if the file is already current. To add a column in future, add it to
COLUMNS here and to the row in the workflow.

Usage (from the repo root):  python analysis/migrate_schema.py
"""
import csv
import io
from pathlib import Path

PATH = Path("data/harness-results.csv")
COLUMNS = [
    "run_id", "run_url", "ran_at_utc", "commit_sha", "fault_profile",
    "fault_magnitude", "target_category", "iteration", "scenario_name",
    "outcome", "duration_seconds", "step_seconds",
    "error_message", "failed_step", "network_events",
]


def main() -> None:
    text = PATH.read_bytes().decode("utf-8")
    rows = list(csv.reader(io.StringIO(text, newline="")))
    header = rows[0]

    if header == COLUMNS:
        print("Already current, nothing to do.")
        return
    if header != COLUMNS[: len(header)]:
        raise SystemExit(f"Refusing to migrate: header is not a prefix of the current schema.\nFound: {header}")

    bad = [i for i, r in enumerate(rows[1:], start=2) if len(r) != len(header)]
    if bad:
        raise SystemExit(f"Refusing to migrate: rows with the wrong column count at lines {bad[:5]}")

    added = COLUMNS[len(header):]
    out = [COLUMNS] + [r + [""] * len(added) for r in rows[1:]]
    with PATH.open("w", newline="", encoding="utf-8") as f:
        csv.writer(f, lineterminator="\r\n").writerows(out)
    print(f"Added {added} to {len(rows) - 1} rows ({len(header)} -> {len(COLUMNS)} columns).")


if __name__ == "__main__":
    main()
