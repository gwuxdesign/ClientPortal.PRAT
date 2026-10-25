"""Recover failure text for older harness runs from their GitHub Actions logs.

Runs before 8 Oct stored no failure text in data/harness-results.csv. The
run logs still hold it, because dotnet test prints each failed test with its
error message and standard output. This script reads logs downloaded with
`gh run view <id> --log`, matches each failed test to its CSV row by run,
magnitude, iteration and scenario name, applies the same masking as the
workflow, and writes the result to a SEPARATE file. It never edits
data/harness-results.csv.

Usage:
    python3 analysis/recover_from_logs.py LOG_DIR [--csv data/harness-results.csv]
                                                  [--out data/recovered-failure-text.csv]
LOG_DIR holds files named <run_id>.log. Keep that folder outside the repo:
the logs are raw.
"""
import argparse
import csv
import re
import sys
from collections import defaultdict
from pathlib import Path

GHERKIN = re.compile(r'^(Given|When|Then|And|But) ')
DURATION = re.compile(r'\((\d+(?:\.\d+)?)s\)')
LOGLINE = re.compile(r'^.*?\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d\.\d+Z ?(.*)$')
HEADER = re.compile(r'^=== Magnitude (\S+), iteration (\d+) of (\d+) ===')
FAILED = re.compile(r'^\s*Failed (.+) \[[^\]]+\]\s*$')
HOST_RE = re.compile(r'https?://[^/\s\'"<>]+')
EMAIL_RE = re.compile(r'[\w.+-]+@[\w-]+(?:\.[\w-]+)+')
STOP = re.compile(r'^(Results File:|WARNING:|Passed!|Failed!|Skipped)|^\s*(Failed|Passed|Skipped) .+ \[')


# The three functions below are copied from the workflow so that recovered
# rows are built exactly as new rows are.
def step_seconds(stdout):
    out, keyword, block = [], None, None
    for raw in (stdout or '').splitlines():
        line = raw.strip()
        if block is None:
            if line.startswith('STEP START:'):
                block = []
            elif GHERKIN.match(line):
                keyword = line.split(' ', 1)[0]
        elif line.startswith('STEP END:'):
            found = DURATION.findall(' '.join(block))
            if found and keyword:
                out.append(f'{keyword}={found[-1]}')
            block = None
        else:
            block.append(line)
    return ';'.join(out)


def failed_step_of(stdout):
    current, block = '', None
    for raw in (stdout or '').splitlines():
        line = raw.strip()
        if block is None:
            if line.startswith('STEP START:'):
                block = []
            elif GHERKIN.match(line):
                current = line
        elif line.startswith('STEP END:'):
            if any(b.startswith('-> error:') for b in block):
                return current
            block = None
        else:
            block.append(line)
    return ''


def network_events_of(stdout):
    events = [
        l.strip().replace('[RequestFailed] ', '')
        for l in (stdout or '').splitlines()
        if l.strip().startswith('[RequestFailed]')
    ]
    return ' | '.join(events[:3])


def clean(text, limit):
    text = re.sub(r'\s+', ' ', text or '').strip()
    text = HOST_RE.sub('***', text)
    text = EMAIL_RE.sub('<email>', text)
    return text[:limit]


def parse_log(path):
    """Yield (magnitude, iteration, scenario, message, stdout) per failed test."""
    magnitude = iteration = None
    block = None
    found = []
    header_seen = False
    test_runs = 0

    def close():
        if block is not None:
            found.append(block)

    for raw in Path(path).read_text(errors='replace').splitlines():
        m = LOGLINE.match(raw)
        text = m.group(1) if m else raw
        h = HEADER.match(text)
        if h:
            close()
            block = None
            header_seen = True
            magnitude, iteration = h.group(1), h.group(2)
            continue
        if text.startswith('Test run for '):
            # Older logs have no iteration heading; each iteration starts
            # one dotnet test run, so count them instead.
            test_runs += 1
            if not header_seen:
                close()
                block = None
                iteration = str(test_runs)
        f = FAILED.match(text)
        if f:
            close()
            block = {'magnitude': magnitude, 'iteration': iteration,
                     'scenario': f.group(1), 'lines': []}
            continue
        if block is not None:
            if STOP.match(text):
                close()
                block = None
            else:
                block['lines'].append(text)
    close()

    for b in found:
        message, stdout, mode = [], [], None
        for line in b['lines']:
            s = line.strip()
            if s == 'Error Message:':
                mode = 'msg'
            elif s == 'Stack Trace:':
                mode = 'stack'
            elif s == 'Standard Output Messages:':
                mode = 'out'
            elif mode == 'msg':
                message.append(line)
            elif mode == 'out':
                stdout.append(line)
        yield (b['magnitude'], b['iteration'], b['scenario'],
               ' '.join(message), '\n'.join(stdout))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('log_dir')
    ap.add_argument('--csv', default='data/harness-results.csv')
    ap.add_argument('--out', default='data/recovered-failure-text.csv')
    args = ap.parse_args()

    rows = list(csv.DictReader(open(args.csv, newline='')))
    wanted = defaultdict(list)  # run_id -> failed rows with no text
    for r in rows:
        if r['outcome'] == 'Failed' and not r['error_message']:
            wanted[r['run_id']].append(r)

    out_rows, report = [], []
    for run_id, missing in sorted(wanted.items()):
        path = Path(args.log_dir) / f'{run_id}.log'
        if not path.exists():
            report.append((run_id, len(missing), 'no log file', 0, 0))
            continue
        index = defaultdict(list)
        for r in missing:
            index[(r['fault_magnitude'], r['iteration'], r['scenario_name'])].append(r)
        run_mags = {r['fault_magnitude'] for r in rows if r['run_id'] == run_id}
        run_iters = {r['iteration'] for r in rows if r['run_id'] == run_id}
        matched = unmatched_log = 0
        used = set()
        for mag, it, name, message, stdout in parse_log(path):
            # Older runs print no header: fall back to the only value the run had.
            if mag is None and len(run_mags) == 1:
                mag = next(iter(run_mags))
            if it is None and len(run_iters) == 1:
                it = next(iter(run_iters))
            key = (str(mag), str(it), name)
            hits = index.get(key, [])
            hits = [h for h in hits if id(h) not in used]
            if len(hits) != 1:
                unmatched_log += 1
                continue
            used.add(id(hits[0]))
            matched += 1
            out_rows.append({
                'run_id': run_id,
                'fault_magnitude': mag,
                'iteration': it,
                'scenario_name': name,
                'error_message': clean(message, 400),
                'failed_step': clean(failed_step_of(stdout), 200),
                'step_seconds': step_seconds(stdout),
                'network_events': clean(network_events_of(stdout), 300),
                'recovered_from_log': 1,
            })
        report.append((run_id, len(missing), 'ok', matched, unmatched_log))

    fields = ['run_id', 'fault_magnitude', 'iteration', 'scenario_name',
              'error_message', 'failed_step', 'step_seconds', 'network_events',
              'recovered_from_log']
    with open(args.out, 'w', newline='') as f:
        w = csv.DictWriter(f, fieldnames=fields)
        w.writeheader()
        w.writerows(out_rows)

    print(f'{"run_id":>12} {"missing":>8} {"matched":>8} {"log-only":>9}  status')
    for run_id, missing, status, matched, lonly in report:
        print(f'{run_id:>12} {missing:>8} {matched:>8} {lonly:>9}  {status}')
    total = sum(r[1] for r in report)
    print(f'\nrecovered {len(out_rows)} of {total} failures without text -> {args.out}')
    left = [r for r in out_rows if '://' in ' '.join(str(v) for v in r.values())]
    if left:
        sys.exit('Unmasked URL found in recovered text; do not commit this file.')


if __name__ == '__main__':
    main()