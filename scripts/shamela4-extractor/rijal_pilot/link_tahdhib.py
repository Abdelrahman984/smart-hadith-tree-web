"""Pilot: resolve the names in Tahdhib al-Kamal's shuyukh/talamidh lists to entries of the same book.

A link A -> B (B listed as a shaykh of A) is "confirmed" when B's own entry lists A as a student.
Usage: python link_tahdhib.py tahdhib.json
"""
import json
import re
import sys
from collections import Counter, defaultdict
from functools import lru_cache

data = json.load(open(sys.argv[1], encoding='utf-8'))
entries = [e for e in data if e['kind'] == 'entry']
xrefs = [e for e in data if e['kind'] == 'crossref']
STOP = {'بن', 'ابن', 'بنت', 'ويقال', 'يقال', 'وهو', 'مولي', 'مولاهم', 'نزيل', 'صاحب', 'والد', 'اخو', 'ام', 'ثم'}


def norm(s: str) -> str:
    s = re.sub(r'[ً-ْٰـ]', '', s)
    s = re.sub('[أإآ]', 'ا', s).replace('ى', 'ي').replace('ة', 'ه')
    s = re.sub(r'\bابي\b', 'ابو', s)            # genitive kunya in lists: "عن أبي مسلم"
    s = re.sub(r'\bابيه\b', '', s)              # "أبيه السائب" -> "السائب"
    s = re.sub(r'\bال(?=\S)', '', s)            # drop the article: البصري ~ بصري
    return re.sub(r'[^ء-ي ]', ' ', s)


# Leading relation words / "عن:" in list items: "عمه X", "جده X", "مولاه X".
LEADING = {'عن', 'عمه', 'جده', 'اخيه', 'خاله', 'مولاه', 'مولاته', 'امه', 'جدته', 'عمته', 'خالته', 'ابنه', 'زوجه'}


def tokens(s: str) -> list[str]:
    toks = [w for w in norm(s).split() if w not in STOP and len(w) > 1]
    while toks and toks[0] in LEADING:
        toks.pop(0)
    return toks


def soft_norm(s: str) -> str:
    """Like norm() but keeps the comma, so kunya positions can be read."""
    s = re.sub(r'[ً-ْٰـ]', '', s)
    s = re.sub('[أإآ]', 'ا', s).replace('ى', 'ي').replace('ة', 'ه')
    s = re.sub(r'\bابي\b', 'ابو', s)
    return re.sub(r'\bال(?=\S)', '', s)


# Index each entry by the tokens of its header (name, kunya, nisbas, laqab).
entry_tokens = [set(tokens(e['header'][:250])) for e in entries]
first_two = [tokens(e['header'][:120])[:2] for e in entries]
by_token = defaultdict(set)
for i, toks in enumerate(entry_tokens):
    for w in toks:
        by_token[w].add(i)


def all_tokens_match(toks: list[str]) -> set[int]:
    cands = set(by_token.get(toks[0], ()))
    for w in toks[1:]:
        cands &= by_token.get(w, set())
    return cands


# The narrator's own ism is the first token of the header; his kunyas are "أبو X" in it.
# Matching on any header token lets relatives match ("أخو محمد بن سيرين"), so the
# item must start with the entry's ism, or with one of its kunyas followed by the ism.
ism = [tokens(e['header'][:120])[:1] for e in entries]
# Own kunya only: at the start or after "،"/"ويقال:", never "والد أبي X" / "أخو أبي X".
KUNYA = re.compile(r'(?:^|،\s*|يقال\s*:?\s*|وهو\s+)ابو (\S+)')
kunyas = [set(KUNYA.findall(soft_norm(e['header'][:200]).strip())) for e in entries]


def starts_like(toks: list[str], j: int) -> bool:
    if not ism[j]:
        return False
    if toks[0] == ism[j][0]:
        return True
    return toks[0] == 'ابو' and len(toks) > 1 and toks[1] in kunyas[j]


@lru_cache(maxsize=None)
def candidates(name: str) -> frozenset[int]:
    toks = tokens(name)
    if not toks or toks[0] == 'نبي':        # "النبي ﷺ" is not a narrator entry
        return frozenset()
    exact = all_tokens_match(toks)
    if exact:
        anchored = {j for j in exact if starts_like(toks, j)}
        if toks[0] == 'ابو' and len(toks) > 2:      # "أبي أمامة أسعد ..." -> the ism after the kunya decides
            anchored = {j for j in anchored if toks[2] == ism[j][0]} or anchored
        return frozenset(anchored)          # unanchored matches are usually relatives: drop them
    # Fallback: ism + father must match the entry's own ism + father; extra tokens rank the rest.
    if len(toks) >= 2:
        base = {i for i in all_tokens_match(toks[:2]) if first_two[i] == toks[:2]}
        if base:
            best = max(len(set(toks) & entry_tokens[i]) for i in base)
            return frozenset(i for i in base if len(set(toks) & entry_tokens[i]) == best)
    return frozenset()


# Cross-references add alias names: "أحمد بن بكار الدمشقي، هو: أحمد بن عبد الرحمن بن بكار".
alias_hits = 0
for x in xrefs:
    if not x.get('target'):
        continue
    target = candidates(x['target'])
    if len(target) == 1:
        (j,) = target
        alias = re.split(r'[،:]', x['header'])[0]
        for w in tokens(alias):
            by_token[w].add(j)
        entry_tokens[j] |= set(tokens(alias))
        alias_hits += 1
candidates.cache_clear()
print(f'entries: {len(entries)}  crossref aliases attached: {alias_hits}/{len(xrefs)}')


@lru_cache(maxsize=None)
def lists_resolve_to(j: int, key: str, other: int) -> bool:
    return any(other in candidates(it['name']) for it in entries[j][key])


# Book symbols: an item "(خ م)" in A's list means A narrates from B in those books,
# so B's own entry symbols must cover them. "ع" = the six books, "٤" = the four Sunan.
SIX = {'خ', 'م', 'د', 'ت', 'س', 'ق'}


def expand(sym: str) -> set[str]:
    out = set()
    for s in re.sub(r'[()]', ' ', sym).split():
        if s == 'ع':
            out |= SIX
        elif s == '٤':
            out |= {'د', 'ت', 'س', 'ق'}
        elif s == '٣':
            out |= {'د', 'ت', 'س'}
        else:
            out.add(s)
    return out


entry_syms = [expand(e['symbols']) for e in entries]


def symbol_filter(c: set[int], item_sym: str) -> set[int]:
    need = expand(item_sym)
    if not need:
        return c
    kept = {j for j in c if need <= entry_syms[j]}
    return kept or c


stats = {k: Counter() for k in ('shuyukh', 'talamidh')}
unresolved = Counter()
for i, e in enumerate(entries):
    for key, back in (('shuyukh', 'talamidh'), ('talamidh', 'shuyukh')):
        for it in e[key]:
            c = candidates(it['name']) - {i}
            if not c:
                stats[key]['unresolved'] += 1
                unresolved[key, it['symbols'] != ''] += 1
                continue
            c = symbol_filter(c, it['symbols'])
            confirmed = [j for j in c if lists_resolve_to(j, back, i)] if len(c) <= 60 else []
            if len(confirmed) == 1:
                stats[key]['confirmed (reciprocal)'] += 1
            elif len(c) == 1:
                stats[key]['unique, not reciprocal'] += 1
            elif len(confirmed) > 1:
                stats[key]['ambiguous after reciprocity'] += 1
            else:
                stats[key]['ambiguous'] += 1

for key, c in stats.items():
    total = sum(c.values())
    print(f'== {key}: {total} names')
    for k, v in c.most_common():
        print(f'   {k:28} {v:6}  {v / total:.1%}')
# A name carrying book symbols, e.g. "(خ م)", is a six-books narrator and should have an entry.
for key in ('shuyukh', 'talamidh'):
    with_sym = sum(1 for e in entries for it in e[key] if it['symbols'])
    print(f'{key}: unresolved among names WITH symbols: {unresolved[key, True]}/{with_sym} '
          f'({unresolved[key, True] / with_sym:.1%}); without symbols: {unresolved[key, False]}')
