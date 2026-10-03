"""Measure how much of a book's isnads the Tahdhib/Taqrib registry covers.

Unlike isnad_test.py, the compiler need not be in the registry: each chain is walked from
the compiler's shaykh down, a name is looked up among the previous narrator's shuyukh when
that narrator is known, and globally otherwise.
Usage: python gap_test.py tahdhib.json book_dir [sample]
"""
import glob
import json
import random
import re
import sys
from collections import Counter, defaultdict

sys.argv, (TAHDHIB, BOOK_DIR, *rest) = [sys.argv[0], sys.argv[1]], sys.argv[1:]
SAMPLE = int(rest[0]) if rest else 1000
exec(open(__file__.replace('gap_test.py', 'link_tahdhib.py'), encoding='utf-8').read().split('stats = {k')[0])

shuyukh_of = []
for i, e in enumerate(entries):
    s = set()
    for it in e['shuyukh']:
        s |= set(symbol_filter(set(candidates(it['name'])) - {i}, it['symbols']))
    shuyukh_of.append(s)

# Later books abbreviate the transmission verbs: ثنا، نا، أنا، أنبأ.
VERBS = (r'(?:^|\s|،)(?:حدثنا|حدثني|حدثه|أخبرنا|أخبرني|أخبره|أنبأنا|أنبأني|أنبأ|ثنا|نا|أنا|أبنا|سمعت|سمع'
         r'|عن|قال|قالت|أن|يقول)(?=\s|،|:)')
HARAKAT_RE = re.compile(r'[ً-ْٰـ]')


def chain_segments(arabic: str) -> list[str]:
    t = HARAKAT_RE.sub('', arabic)
    t = re.split(r'رسول الله|النبي|ﷺ|صلى الله عليه وسلم', t)[0]
    segs = [s.strip(' ،,:.') for s in re.split(VERBS, t)]
    return [s for s in segs if 1 < len(s) < 70 and not s.startswith(('قال', 'يقول'))]


def lookup(seg: str, prev: int | None) -> set[int]:
    if prev is not None and re.fullmatch(r'أبيه|ابيه', seg):
        father = tokens(entries[prev]['header'][:80])[1:2]
        return {j for j in shuyukh_of[prev] if ism[j] == father}
    return set(candidates(seg)) - ({prev} if prev is not None else set())


hadiths = []
for f in glob.glob(f'{BOOK_DIR}/*.json'):
    if not f.endswith('index.json'):
        hadiths += [h['arabic'] for h in json.load(open(f, encoding='utf-8')) if h.get('arabic')]
random.seed(1)
sample = random.sample(hadiths, min(SAMPLE, len(hadiths)))

MAX_DEPTH = 8
by_depth = defaultdict(Counter)
missing_names = Counter()
entered_at = Counter()
for text in sample:
    segs = chain_segments(text)[:MAX_DEPTH]
    prev, entered = None, None
    for depth, seg in enumerate(segs):
        c = lookup(seg, prev)
        within = c & shuyukh_of[prev] if prev is not None else set()
        if len(within) > 1 and depth + 1 < len(segs):
            within = {j for j in within if lookup(segs[depth + 1], j) & shuyukh_of[j]} or within
        if len(within) > 1 and (p := fame_pick(within)) is not None:
            within = {p}
        if len(within) == 1:
            status, prev = 'resolved (teacher list)', next(iter(within))
        elif len(c) == 1:
            status, prev = 'resolved (unique name)', next(iter(c))
        elif c:
            status, prev = 'ambiguous', None
        else:
            status, prev = 'not in registry', None
            missing_names[re.sub(r'\s+', ' ', seg)] += 1
        by_depth[depth][status] += 1
        if status.startswith('resolved') and entered is None:
            entered = depth
    entered_at['never' if entered is None else entered] += 1

print(f'hadiths: {len(sample)} of {len(hadiths)}')
statuses = ['resolved (teacher list)', 'resolved (unique name)', 'ambiguous', 'not in registry']
print(f'{"depth":>5} {"names":>6}  ' + '  '.join(f'{s[:22]:>22}' for s in statuses))
for d in sorted(by_depth):
    total = sum(by_depth[d].values())
    print(f'{d:>5} {total:>6}  ' + '  '.join(f'{by_depth[d][s] / total:>22.0%}' for s in statuses))
allc = Counter()
for c in by_depth.values():
    allc.update(c)
total = sum(allc.values())
print('all depths:', ', '.join(f'{s}: {allc[s] / total:.1%}' for s in statuses))
print('first resolved narrator at depth:', sorted(entered_at.items(), key=lambda x: (x[0] == 'never', x[0])))
top = missing_names.most_common(25)
print(f'distinct missing names: {len(missing_names)}; the 25 most frequent cover '
      f'{sum(n for _, n in top) / max(1, sum(missing_names.values())):.0%} of misses:')
for name, n in top:
    print(f'   {n:4}  {name[:70]}')

json.dump({'missing': missing_names, 'by_depth': {d: dict(c) for d, c in by_depth.items()}},
          open('gap_result.json', 'w', encoding='utf-8'), ensure_ascii=False)
