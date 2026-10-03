"""Pilot: resolve Bukhari isnad names using only the Tahdhib al-Kamal registry.

Walks each chain from al-Bukhari down; a name is looked up among the shuyukh of the
previously resolved narrator. Usage: python isnad_test.py tahdhib.json bukhari_dir [sample]
"""
import glob
import json
import random
import re
import sys
from collections import Counter

sys.argv, (TAHDHIB, BOOK_DIR, *rest) = [sys.argv[0], sys.argv[1]], sys.argv[1:]
SAMPLE = int(rest[0]) if rest else 500
exec(open(__file__.replace('isnad_test.py', 'link_tahdhib.py'), encoding='utf-8').read().split('stats = {k')[0])

# Shaykh candidates of every entry, through its own shuyukh list.
shuyukh_of = []
for i, e in enumerate(entries):
    s = set()
    for it in e['shuyukh']:
        s |= set(symbol_filter(set(candidates(it['name'])) - {i}, it['symbols']))
    shuyukh_of.append(s)

bukhari = next(i for i, e in enumerate(entries) if e['header'].startswith('محمد بن إسماعيل بن إبراهيم بن المغيرة'))
print('al-Bukhari entry found:', entries[bukhari]['header'][:60])

VERBS = r'(?:حدثنا|حدثني|أخبرنا|أخبرني|أنبأنا|سمعت|عن|قال|قالت|أن)'
HARAKAT_RE = re.compile(r'[ً-ْٰـ]')


def chain_segments(arabic: str) -> list[str]:
    """Narrator names between transmission verbs, stopping at the Prophet / matn."""
    t = HARAKAT_RE.sub('', arabic)
    t = re.split(r'(?:رسول الله|النبي|ﷺ|صلى الله عليه وسلم)', t)[0]
    segs = [s.strip(' ،,:.') for s in re.split(VERBS, t)]
    return [s for s in segs if s and len(s) < 60 and not s.startswith(('قال', 'يقول'))]


hadiths = []
for f in glob.glob(f'{BOOK_DIR}/*.json'):
    if f.endswith('index.json'):
        continue
    hadiths += [h['arabic'] for h in json.load(open(f, encoding='utf-8')) if h.get('arabic')]
random.seed(1)
sample = random.sample(hadiths, min(SAMPLE, len(hadiths)))

# Narrators of a Bukhari chain must carry a Bukhari symbol (خ, or خت/بخ/عخ ... for his other works).
in_bukhari = {j for j, s in enumerate(entry_syms) if s & {'خ', 'خت', 'بخ', 'عخ', 'ر', 'ي', 'كن'}}


def lookup(seg: str, prev: int) -> set[int]:
    if re.fullmatch(r'أبيه|ابيه', seg.strip()):
        # "عن أبيه": the father's ism is the second token of prev's own name.
        father = tokens(entries[prev]['header'][:80])[1:2]
        return {j for j in shuyukh_of[prev] if ism[j] == father}
    return set(candidates(seg)) - {prev}


stats, broken_at, examples = Counter(), Counter(), []
for text in sample:
    segs = [s for s in chain_segments(text)[:8] if len(s) > 1]
    prev = bukhari
    for depth, seg in enumerate(segs):
        c = lookup(seg, prev)
        within = c & shuyukh_of[prev]
        if len(within) > 1:
            within = (within & in_bukhari) or within
        if len(within) > 1 and depth + 1 < len(segs):
            # Look ahead: keep the candidates among whose shaykhs the next name is found.
            ahead = {j for j in within if lookup(segs[depth + 1], j) & shuyukh_of[j]}
            within = ahead or within
        if len(within) > 1 and (p := fame_pick(within)) is not None:
            within = {p}                          # e.g. "الزهري" among Ma'mar's shuyukh
        if len(within) == 1:
            stats['resolved via teacher list'] += 1
            prev = next(iter(within))
        elif len(c) == 1:
            stats['resolved (unique name, not in teacher list)'] += 1
            prev = next(iter(c))
        else:
            stats['ambiguous' if (within or c) else 'unresolved'] += 1
            broken_at[depth] += 1
            if len(examples) < 14:
                examples.append((entries[prev]['header'][:30], seg, len(within or c)))
            break

total = sum(stats.values())
print(f'hadiths: {len(sample)}  name lookups: {total}')
for k, v in stats.most_common():
    print(f'   {k:45} {v:5}  {v / total:.1%}')
print('chain stopped at depth:', sorted(broken_at.items()))
for prev, seg, n in examples:
    print(f'   after [{prev}] could not resolve: "{seg}" ({n} candidates)')
