#!/usr/bin/env python3
"""Measure POST /api/Verify against docs/challenge/verify-eval-cases.json.

Usage:
    python scripts/eval_verify.py                       # API on http://localhost:5147
    python scripts/eval_verify.py --api https://host/api --runs 3
    python scripts/eval_verify.py --dry-run             # only validate the cases file

Reports per group and overall: pass rate, false attributions (a negative answered
exact/variant) and abstentions. --runs repeats every case to show consistency.
Uses only the standard library.
"""
import argparse
import json
import statistics
import sys
import time
import urllib.request
from collections import defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
CASES_PATH = ROOT / "docs" / "challenge" / "verify-eval-cases.json"
STATUSES = {"exact", "variant", "not-found", "invalid"}


def load_cases():
    cases = json.loads(CASES_PATH.read_text(encoding="utf-8"))["cases"]
    ids = set()
    for c in cases:
        assert c["id"] not in ids, f"duplicate id {c['id']}"
        ids.add(c["id"])
        assert set(c["accept"]) <= STATUSES, f"{c['id']}: unknown status in accept"
        assert set(c.get("reject", [])) <= STATUSES, f"{c['id']}: unknown status in reject"
    return cases


def verify(api, text):
    req = urllib.request.Request(
        f"{api}/Verify",
        data=json.dumps({"text": text}).encode("utf-8"),
        headers={"Content-Type": "application/json"},
        method="POST",
    )
    start = time.perf_counter()
    with urllib.request.urlopen(req, timeout=60) as res:
        body = json.load(res)
    return body, (time.perf_counter() - start) * 1000


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--api", default="http://localhost:5147/api")
    ap.add_argument("--runs", type=int, default=1)
    ap.add_argument("--dry-run", action="store_true")
    args = ap.parse_args()

    cases = load_cases()
    if args.dry_run:
        print(f"OK: {len(cases)} cases valid")
        return 0

    api = args.api.rstrip("/")
    groups = defaultdict(lambda: [0, 0])  # group -> [passed, total]
    false_attr = 0
    negatives = 0
    abstained = 0
    latencies = []
    unstable = []
    failures = []

    for c in cases:
        seen = []
        for _ in range(args.runs):
            body, ms = verify(api, c["text"])
            latencies.append(ms)
            seen.append(body["status"])
        if len(set(seen)) > 1:
            unstable.append((c["id"], seen))
        status = seen[0]
        ok = status in c["accept"] and status not in c.get("reject", [])
        groups[c["group"]][0] += ok
        groups[c["group"]][1] += 1
        if c["group"] == "negative":
            negatives += 1
            false_attr += status in ("exact", "variant")
            abstained += status == "not-found"
        if not ok:
            failures.append((c["id"], status, c["accept"], body.get("method")))
        print(f"{'PASS' if ok else 'FAIL'}  {c['id']:4} {status:10} method={body.get('method')}")

    print("\nBy group:")
    for g, (p, t) in groups.items():
        print(f"  {g:18} {p}/{t}")
    total_p = sum(p for p, _ in groups.values())
    total_t = sum(t for _, t in groups.values())
    print(f"\nOverall: {total_p}/{total_t} passed")
    if negatives:
        print(f"Negatives: {false_attr} false attribution(s), {abstained}/{negatives} abstained")
    print(f"Latency ms: median {statistics.median(latencies):.0f}, max {max(latencies):.0f} ({len(latencies)} calls)")
    if unstable:
        print(f"Unstable across {args.runs} runs: {unstable}")
    elif args.runs > 1:
        print(f"Stable across {args.runs} runs")
    for f in failures:
        print(f"FAILED {f[0]}: got {f[1]}, accept {f[2]}, method {f[3]}")
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
