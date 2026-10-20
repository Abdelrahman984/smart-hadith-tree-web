#!/usr/bin/env python3
"""Snapshot the tree, takhreej, ilal and narrator-search API output for a fixed set of hadiths, and diff two snapshots.

Built for the SQL Server -> PostgreSQL move (docs/postgres_migration.md, phases 0 and 6): the same corpus loaded into
two databases gets different random GUIDs, so cases are keyed by (book, chapter, hadith number) and every GUID in a
response is replaced by a stable label before comparing: hadiths by "H:book#number", narrators by "N:name", tree nodes
by "T:narrator@step". Lists are compared as multisets, so row order that differs only on ties does not count.

Usage:
    python scripts/snapshot_api.py --save snap_sqlserver.json                  # the running API (default :5147)
    python scripts/snapshot_api.py --api http://localhost:5148/api --save snap_pg.json
    python scripts/snapshot_api.py --compare snap_sqlserver.json snap_pg.json  # prints differing endpoints

Cases: docs/challenge/evaluation/postgres-baseline/snapshot-cases.json. Standard library only.
"""
import argparse
import json
import re
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
CASES_PATH = ROOT / "docs" / "challenge" / "evaluation" / "postgres-baseline" / "snapshot-cases.json"
GUID = re.compile(r"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$")
RELATED_IN_COMPARATIVE = 5


def http_json(url):
    for attempt in range(8):
        start = time.perf_counter()
        try:
            with urllib.request.urlopen(url, timeout=180) as res:
                return json.load(res), (time.perf_counter() - start) * 1000
        except urllib.error.HTTPError as e:
            if e.code == 404:
                return {"status": 404}, (time.perf_counter() - start) * 1000
            if e.code != 429:
                raise
            time.sleep(8 * (attempt + 1))
    raise RuntimeError("still rate limited after several retries")


def q(s):
    return urllib.parse.quote(s, safe="")


def resolve_id(api, case):
    rows, _ = http_json(f"{api}/books/{q(case['book'])}/chapters/{q(case['chapter'])}/hadiths")
    for r in rows if isinstance(rows, list) else []:
        if r["hadithNumber"] == case["number"]:
            return r["id"]
    return None


def save(api, out):
    cases = json.loads(CASES_PATH.read_text(encoding="utf-8"))
    snap = {"api": api, "taken": time.strftime("%Y-%m-%dT%H:%M:%S"), "hadiths": [], "narrators": [], "latencyMs": {}}
    lat = {}
    for i, c in enumerate(cases["hadiths"], 1):
        hid = resolve_id(api, c)
        entry = {"case": c, "id": hid}
        if hid:
            for key, url in (("tree", f"{api}/tree/{hid}"), ("related", f"{api}/takhreej/related/{hid}"),
                             ("ilal", f"{api}/ilal/{hid}")):
                entry[key], ms = http_json(url)
                lat.setdefault(key, []).append(ms)
            related = [r["id"] for r in entry["related"]][:RELATED_IN_COMPARATIVE] if isinstance(entry["related"], list) else []
            entry["comparative"], ms = http_json(f"{api}/takhreej?ids={','.join([hid] + related)}")
            lat.setdefault("comparative", []).append(ms)
        print(f"[{i}/{len(cases['hadiths'])}] {c['book']} #{c['number']} {'ok' if hid else 'NOT FOUND'}")
        snap["hadiths"].append(entry)
    for name in cases["narratorQueries"]:
        res, ms = http_json(f"{api}/narrators/search?q={q(name)}")
        lat.setdefault("narrators", []).append(ms)
        snap["narrators"].append({"query": name, "results": res})
    snap["latencyMs"] = {k: {"median": round(sorted(v)[len(v) // 2]), "max": round(max(v))} for k, v in lat.items()}
    Path(out).write_text(json.dumps(snap, ensure_ascii=False, indent=1), encoding="utf-8")
    print("Latency ms:", json.dumps(snap["latencyMs"]))
    print(f"Saved to {out}")


def collect_labels(obj, labels):
    """First pass: learn a stable label for every GUID that names a hadith, narrator or tree node."""
    if isinstance(obj, dict):
        hid = obj.get("hadithId") or (obj.get("id") if "hadithNumber" in obj else None)
        if hid and "bookName" in obj and "hadithNumber" in obj:
            labels.setdefault(hid, f"H:{obj['bookName']}#{obj['hadithNumber']}")
        if "narratorId" in obj and "narratorName" in obj:
            labels.setdefault(obj["narratorId"], f"N:{obj['narratorName']}")
        if "id" in obj and "fullName" in obj:
            labels.setdefault(obj["id"], f"N:{obj['fullName']}")
        for v in obj.values():
            collect_labels(v, labels)
    elif isinstance(obj, list):
        for v in obj:
            collect_labels(v, labels)


def collect_node_labels(obj, labels):
    """Second pass: tree nodes are labelled by narrator and step, once narrators have labels."""
    if isinstance(obj, dict):
        if "narratorId" in obj and "stepOrder" in obj and "id" in obj and obj["id"] not in labels:
            labels[obj["id"]] = f"T:{labels.get(obj['narratorId'], '?')}@{obj['stepOrder']}"
        for v in obj.values():
            collect_node_labels(v, labels)
    elif isinstance(obj, list):
        for v in obj:
            collect_node_labels(v, labels)


def relabel(obj, labels):
    if isinstance(obj, dict):
        return {k: relabel(v, labels) for k, v in obj.items()}
    if isinstance(obj, list):
        # Multiset: order of equal-ranked rows is engine-dependent, so sort by content.
        return sorted((relabel(v, labels) for v in obj), key=lambda v: json.dumps(v, ensure_ascii=False, sort_keys=True))
    if isinstance(obj, str) and GUID.match(obj):
        return labels.get(obj, "<guid>")
    return obj


def normalize(entry):
    labels = {}
    collect_labels(entry, labels)
    collect_node_labels(entry, labels)
    return relabel({k: v for k, v in entry.items() if k != "id"}, labels)


def compare(a_path, b_path):
    a = json.loads(Path(a_path).read_text(encoding="utf-8"))
    b = json.loads(Path(b_path).read_text(encoding="utf-8"))
    diffs = 0
    for ea, eb in zip(a["hadiths"], b["hadiths"]):
        na, nb = normalize(ea), normalize(eb)
        c = ea["case"]
        for key in ("tree", "related", "ilal", "comparative"):
            if na.get(key) != nb.get(key):
                diffs += 1
                print(f"DIFF {key:12} {c['book']} #{c['number']} ({c['kind']})")
    for ea, eb in zip(a["narrators"], b["narrators"]):
        if normalize(ea) != normalize(eb):
            diffs += 1
            print(f"DIFF narrators   {ea['query']}")
    print(f"Latency ms  A: {json.dumps(a['latencyMs'])}\n            B: {json.dumps(b['latencyMs'])}")
    print("No differences" if diffs == 0 else f"{diffs} differing endpoint(s)")
    return 1 if diffs else 0


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--api", default="http://localhost:5147/api")
    ap.add_argument("--save")
    ap.add_argument("--compare", nargs=2, metavar=("A", "B"))
    args = ap.parse_args()
    if args.compare:
        sys.exit(compare(*args.compare))
    if not args.save:
        ap.error("--save or --compare is required")
    save(args.api, args.save)


if __name__ == "__main__":
    main()
