"""Compare the Shamela registry's isnad resolution with the current (Itqan-based) system.

Same hadith sample as gap_test.py. For each hadith the current system's chain comes from the
Transmissions table (exported by export_chains.ps1). Prints coverage for both systems and
side-by-side chains for manual review.
Usage: python compare_current.py tahdhib.json book_dir current_chains.json compiler_prefix sample [show]
"""
import json
import os
import re
import sys
from collections import Counter

TAHDHIB, BOOK_DIR, CURRENT, COMPILER_PREFIX, SAMPLE, *rest = sys.argv[1:]
SHOW = int(rest[0]) if rest else 15
sys.argv = [sys.argv[0], TAHDHIB, BOOK_DIR, SAMPLE, COMPILER_PREFIX]
src = open(os.path.join(os.path.dirname(os.path.abspath(__file__)), 'gap_test.py'), encoding='utf-8').read()
src = src.replace("exec(open(__file__.replace('gap_test.py', 'link_tahdhib.py')",
                  "exec(open(os.path.join(os.path.dirname(os.path.abspath(__file__)), 'link_tahdhib.py')")
exec(src.split('MAX_DEPTH = 8')[0])

HARAKAT = re.compile(r'[ً-ْٰـ]')


def key(text: str) -> str:
    return re.sub(r'\s+', ' ', HARAKAT.sub('', text or ''))[:120]


current = {}
for h in json.load(open(CURRENT, encoding='utf-8')):
    current.setdefault(key(h['head']), h)


def our_chain(text: str) -> list[tuple[str, int | None, str]]:
    segs = [s for s in chain_segments(text) if is_name(s)][:8]
    out, prev = [], compiler
    for depth, seg in enumerate(segs):
        c = lookup(seg, prev)
        within = c & shuyukh_of[prev] if prev is not None else set()
        if len(within) > 1 and depth + 1 < len(segs):
            within = {j for j in within if lookup(segs[depth + 1], j) & shuyukh_of[j]} or within
        if len(within) > 1 and (p := fame_pick(within)) is not None:
            within = {p}
        if len(within) == 1:
            prev = next(iter(within)); out.append((seg, prev, 'teacher'))
        elif len(c) == 1:
            prev = next(iter(c)); out.append((seg, prev, 'unique'))
        else:
            prev = None; out.append((seg, None, 'ambiguous' if c else 'missing'))
    return out


matched, rows = 0, []
tot = Counter()
for text in sample:
    cur = current.get(key(text))
    if cur is None:
        continue
    matched += 1
    ours = our_chain(text)
    # The current system's first chain (until the first tahwil reset of StepOrder).
    links, last = [], 0
    for l in sorted(cur['links'], key=lambda l: 0):
        if l['step'] <= last:
            break
        links.append(l); last = l['step']
    tot['name segments'] += len(ours)
    tot['ours resolved'] += sum(1 for _, j, _ in ours if j is not None)
    tot['current links'] += len(links)
    tot['current links (first chain only)'] += len(links)
    tot['hadiths with no current link'] += not cur['links']
    tot['hadiths with nothing resolved by ours'] += not any(j is not None for _, j, _ in ours)
    rows.append((text, ours, links))

print(f'sample: {len(sample)}  matched in DB: {matched}')
n = tot['name segments']
print(f'name segments (first 8 per chain): {n}')
print(f'  ours   : resolved {tot["ours resolved"]} ({tot["ours resolved"] / n:.0%})')
print(f'  current: links    {tot["current links"]} ({tot["current links"] / n:.0%})  '
      f'[a link = one narrator identified]')
print(f'  hadiths with no narrator identified: ours {tot["hadiths with nothing resolved by ours"]}, '
      f'current {tot["hadiths with no current link"]}')

import random
random.seed(99)
for text, ours, links in random.sample(rows, min(SHOW, len(rows))):
    print('\n' + '─' * 100)
    print('ISNAD :', re.sub(r'\s+', ' ', HARAKAT.sub('', text))[:230])
    print('OURS  :', ' ← '.join(
        (entries[j]['header'][:32].replace('\n', ' ') if j is not None else f'?{seg[:20]}?') for seg, j, _ in ours))
    print('NOW   :', ' ← '.join(l['sheikh'][:32] for l in links) or '(no links)')
