"""One-off migration: add the step_seconds column to data/harness-results.csv.

The workflow now records per-step timings in a new last column, and its
append step refuses to write if the header does not match. Run this once,
after pulling, BEFORE pushing the updated workflow. Existing rows get an
empty step_seconds (their step timings were never kept).

Safe to run twice: it does nothing if the column is already present.

Usage (from the repo root):  python analysis/migrate_add_step_seconds.py
"""
import csv
import io
from pathlib import Path

PATH = Path("data/harness-results.csv")
NEW_COLUMN = "step_seconds"


def main() -> None:
    text = PATH.read_bytes().decode("utf-8")
    rows = list(csv.reader(io.StringIO(text, newline="")))
    header = rows[0]

    if NEW_COLUMN in header:
        print(f"{NEW_COLUMN} is already present, nothing to do.")
        return

    width = len(header)
    bad = [i for i, r in enumerate(rows[1:], start=2) if len(r) != width]
    if bad:
        raise SystemExit(f"Refusing to migrate: rows with the wrong column count at lines {bad[:5]}")

    out = [header + [NEW_COLUMN]] + [r + [""] for r in rows[1:]]
    with PATH.open("w", newline="", encoding="utf-8") as f:
        csv.writer(f, lineterminator="\r\n").writerows(out)
    print(f"Added {NEW_COLUMN} to {len(rows) - 1} rows ({width} -> {width + 1} columns).")


if __name__ == "__main__":
    main()
