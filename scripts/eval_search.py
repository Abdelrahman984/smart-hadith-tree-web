#!/usr/bin/env python3
"""Measure /api/Search ranking against docs/challenge/search-eval-cases.json.

For every case the runner searches, finds the rank of the hadith the user is looking for (a matn that contains
'target_contains'), and reports hit@1/3/10 and MRR. With --judge it also runs the opt-in AI check on the page and
re-orders by its labels (match, partial, not judged, scattered), reporting how often the target was labelled
'scattered' (the risk of hiding a correct hadith) and whether the model answered.

Usage:
    python scripts/eval_search.py --dry-run                        # validate the cases file
    python scripts/eval_search.py --save current.json              # the running API
    python scripts/eval_search.py --judge --save with_ai.json      # + AI check
    python scripts/eval_search.py --compare baseline.json          # rank changes against a saved run

To get a BEFORE number, run the same command against an API built from the commit before the relevance change
(git worktree add ../baseline <commit>; dotnet run there) and save it as baseline.json.
Standard library only.
"""
import argparse
import json
import re
import sys
import time
import urllib.parse
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
CASES_PATH = ROOT / "docs" / "challenge" / "search-eval-cases.json"
LEVEL_ORDER = {"match": 0, "partial": 1, "not-judged": 2, "scattered": 3}
MARKS = re.compile("[ؐ-ًؚ-ٰٟۖ-ۭـ]")


def norm(text):
    t = MARKS.sub("", text or "")
    t = re.sub("[أإآ]", "ا", t).replace("ة", "ه").replace("ى", "ي")
    return re.sub(r"[^ء-ي]+", " ", t).strip()


def load_cases():
    cases = json.loads(CASES_PATH.read_text(encoding="utf-8"))["cases"]
    ids = set()
    for c in cases:
        assert c["id"] not in ids, f"duplicate id {c['id']}"
        ids.add(c["id"])
        assert c["query"].strip() and c["target_contains"].strip(), f"{c['id']}: empty query or target"
    return cases


def http_json(url, body=None):
    req = urllib.request.Request(
        url, data=None if body is None else json.dumps(body).encode("utf-8"),
        headers={"Content-Type": "application/json"}, method="GET" if body is None else "POST")
    start = time.perf_counter()
    with urllib.request.urlopen(req, timeout=120) as res:
        data = json.load(res)
    return data, (time.perf_counter() - start) * 1000


def rank_of(results, needle):
    n = norm(needle)
    for i, r in enumerate(results, 1):
        if n and n in norm(r.get("matnArabic") or r.get("matnSnippet") or ""):
            return i
    return None


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--api", default="http://localhost:5147/api")
    ap.add_argument("--dry-run", action="store_true")
    ap.add_argument("--judge", action="store_true", help="also run the AI check and re-order by its labels")
    ap.add_argument("--save")
    ap.add_argument("--compare")
    args = ap.parse_args()

    cases = load_cases()
    if args.dry_run:
        print(f"OK: {len(cases)} cases valid")
        return 0

    api = args.api.rstrip("/")
    out, ranks, judged_ranks, latencies = {}, [], [], []
    scattered_targets, model_status, decoy_above = [], {}, []

    for c in cases:
        qs = urllib.parse.urlencode({"query": c["query"], "scope": c.get("scope", 1), "match": c.get("match", 0), "page": 1, "pageSize": 50})
        results, ms = http_json(f"{api}/Search?{qs}")
        latencies.append(ms)
        rank = rank_of(results, c["target_contains"])
        entry = {"rank": rank, "results": len(results)}
        if rank:
            entry["relevance"] = results[rank - 1].get("relevancePercent")
        decoys = [rank_of(results, d) for d in c.get("decoys_contains", [])]
        if rank and any(d and d < rank for d in decoys):
            decoy_above.append(c["id"])
        ranks.append(rank)

        if args.judge and results:
            judged, _ = http_json(f"{api}/Search/ai-judge", {"query": c["query"], "ids": [r["id"] for r in results]})
            model_status[judged["status"]] = model_status.get(judged["status"], 0) + 1
            level = {i["id"]: i["level"] for i in judged["items"]}
            reordered = sorted(results, key=lambda r: LEVEL_ORDER.get(level.get(r["id"], "not-judged"), 2))  # stable
            jr = rank_of(reordered, c["target_contains"])
            entry["rank_after_ai"] = jr
            if rank:
                entry["target_level"] = level.get(results[rank - 1]["id"])
                if entry["target_level"] == "scattered":
                    scattered_targets.append(c["id"])
            judged_ranks.append(jr)
        out[c["id"]] = entry
        print(f"{c['id']}  rank={rank}  results={len(results)}  relevance={entry.get('relevance')}"
              + (f"  after_ai={entry.get('rank_after_ai')} level={entry.get('target_level')}" if args.judge else ""))

    def summary(label, rs):
        total = len(rs)
        hit = lambda k: sum(1 for r in rs if r and r <= k)
        mrr = sum(1 / r for r in rs if r) / total
        print(f"{label:22} hit@1 {hit(1)}/{total}  hit@3 {hit(3)}/{total}  hit@10 {hit(10)}/{total}  MRR {mrr:.3f}  not found {sum(1 for r in rs if not r)}")

    print()
    summary("search order", ranks)
    if args.judge:
        summary("after AI labels", judged_ranks)
        print(f"AI status per case: {model_status}")
        print(f"Target labelled 'scattered' (would be hidden if the user hides): {len(scattered_targets)} {scattered_targets}")
        if any(s != "reviewed" for s in model_status):
            print("WARNING: the model did not fully answer for some cases; this run does not measure its value.")
    if decoy_above:
        print(f"Decoy ranked above the target: {decoy_above}")
    print(f"Latency ms: median {sorted(latencies)[len(latencies)//2]:.0f}, max {max(latencies):.0f}")

    if args.save:
        Path(args.save).write_text(json.dumps(out, ensure_ascii=False, indent=2), encoding="utf-8")
        print(f"Saved to {args.save}")
    if args.compare:
        before = json.loads(Path(args.compare).read_text(encoding="utf-8"))
        changes = {i: (before[i]["rank"], e["rank"]) for i, e in out.items() if i in before and before[i]["rank"] != e["rank"]}
        print(f"Rank changed vs {args.compare} (before, now): {changes if changes else 'none'}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
