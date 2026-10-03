"""Pilot: resolve the names in Tahdhib al-Kamal's shuyukh/talamidh lists to entries of the same book.

A link A -> B (B listed as a shaykh of A) is "confirmed" when B's own entry lists A as a student.
Usage: python link_tahdhib.py tahdhib.json
"""
import glob
import json
import os
import re
import sys
from collections import Counter, defaultdict
from functools import lru_cache

data = json.load(open(sys.argv[1], encoding='utf-8'))
entries = [e for e in data if e['kind'] == 'entry']
# Entries from other rijal books (e.g. extra_hakim.json from parse_hakim_books.py) sit next to tahdhib.json.
for _extra in sorted(glob.glob(os.path.join(os.path.dirname(os.path.abspath(sys.argv[1])), 'extra_*.json'))):
    entries += [e for e in json.load(open(_extra, encoding='utf-8')) if e['kind'] == 'entry']
xrefs = [e for e in data if e['kind'] == 'crossref']
STOP = {'بن', 'ابن', 'بنت', 'ويقال', 'يقال', 'وهو', 'مولي', 'مولاهم', 'نزيل', 'صاحب', 'والد', 'اخو', 'ام', 'ثم'}


def norm(s: str) -> str:
    s = re.sub(r'[ً-ْٰـ]', '', s)
    s = re.sub('[أإآ]', 'ا', s).replace('ى', 'ي').replace('ة', 'ه')
    s = re.sub(r'\bابي\b', 'ابو', s)            # genitive kunya in lists: "عن أبي مسلم"
    s = re.sub(r'\bابيه\b', '', s)              # "أبيه السائب" -> "السائب"
    s = re.sub(r'\bال(?=\S)', '', s)            # drop the article: البصري ~ بصري
    s = re.sub(r'\bعبد\s+(\S+)', r'عبد_\1', s)  # "عبد الله" / "عبد السلام" is one name, not "عبد" + another
    return re.sub(r'[^ء-ي_ ]', ' ', s)


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
    s = re.sub(r'\bال(?=\S)', '', s)
    return re.sub(r'\bعبد\s+([^\s،.:]+)', r'عبد_\1', s)


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
KUNYA = re.compile(r'(?:^|،\s*|يقال\s*:?\s*|وهو\s+)ابو ([^\s،.:]+)')
kunyas = [set(KUNYA.findall(soft_norm(e['header'][:200]).strip())) for e in entries]
kunya_index = defaultdict(set)
for _j, _ks in enumerate(kunyas):
    for _k in _ks:
        kunya_index[_k].add(_j)


def starts_like(toks: list[str], j: int) -> bool:
    if not ism[j]:
        return False
    if toks[0] == ism[j][0]:
        return True
    return toks[0] == 'ابو' and len(toks) > 1 and toks[1] in kunyas[j]


@lru_cache(maxsize=None)
def name_candidates(name: str) -> frozenset[int]:
    """Entries whose own name (ism/kunya + nasab) matches `name`."""
    toks = tokens(name)
    if not toks or toks[0] == 'نبي':        # "النبي ﷺ" is not a narrator entry
        return frozenset()
    # The "X بن Y بن Z" part of the name, after a leading kunya if any ("أبو بكر محمد بن أحمد").
    chain = nasab_chain(re.sub(r'^\s*أب[وي]\s+(?:عبد\s+)?\S+\s+(?!بن\s)', '', name))

    def chain_fits(j: int) -> bool:
        # "محمد بن علي" must not match "محمد بن عمر بن علي": the nasab must agree in order.
        return len(chain) < 2 or nasab[j][:len(chain)] == chain[:len(nasab[j])]

    exact = all_tokens_match(toks)
    if exact:
        anchored = {j for j in exact if starts_like(toks, j) and chain_fits(j)}
        if toks[0] == 'ابو' and len(toks) > 2:      # "أبي أمامة أسعد ..." -> the ism after the kunya decides
            anchored = {j for j in anchored if toks[2] == ism[j][0]} or anchored
        return frozenset(anchored)          # unanchored matches are usually relatives: drop them
    # Kunya-led forms common in later isnads:
    #   "أبو بكر بن إسحاق"         -> kunya بكر, father إسحاق
    #   "أبو زكريا العنبري"         -> kunya زكريا + a nisba of the narrator's own name
    #   "أبو بكر محمد بن أحمد بن بالويه" -> kunya + ism + nasab (all tokens present)
    if toks[0] == 'ابو' and len(toks) >= 3:
        k, rest = toks[1], toks[2:]
        pool = kunya_index.get(k, set())
        if re.match(r'\s*أب[وي] \S+ بن ', name):
            # "أبو بكر بن إسحاق" names the father; "أبو بكر بن أبي شيبة" names an ancestor
            # (عبد الله بن محمد بن أبي شيبة), and "أبي شيبة" is two tokens.
            anc = rest[:2] if rest[0] == 'ابو' else rest[:1]
            return frozenset(j for j in pool
                             if any(nasab[j][p:p + len(anc)] == anc for p in range(1, 5)))
        return frozenset(j for j in pool if set(rest) <= (own_tokens[j] | entry_tokens[j]) and chain_fits(j))
    # Fallback: ism + father must match the entry's own ism + father, and a grandfather named in
    # the item ("X بن Y بن Z") must not contradict the entry's grandfather.
    if len(toks) >= 2:
        chain = nasab_chain(name)
        base = {i for i in all_tokens_match(toks[:2]) if first_two[i] == toks[:2]
                and nasab[i][:len(chain)] == chain[:len(nasab[i])]}
        if base:
            best = max(len(set(toks) & entry_tokens[i]) for i in base)
            return frozenset(i for i in base if len(set(toks) & entry_tokens[i]) == best)
    return frozenset()


# ── Shuhra (أسماء الشهرة) ──────────────────────────────────────────────
# 1. Aliases from Taqrib redirects: "سليمان الأعمش هو ابن مهران" -> سليمان بن مهران.
# 2. A bare laqab/nisba or "ابن X" ("الأعمش", "الزهري", "ابن جريج") matches the entry's own
#    name part (not its relatives); context (teacher lists) and fame then pick one.
alias_index: dict[tuple, set[int]] = defaultdict(set)
RELATION = re.compile(r'\s(?:أخو|أخي|والد|والدة|ابن عم|ابن أخي|ابن أخت|عم|خال|زوج|جد|صهر|ختن)\s')
own_tokens = [set(tokens(RELATION.split(re.split(r'[.\n]', e['header'])[0] + ' ')[0])) for e in entries]
fame = [len(e['talamidh']) for e in entries]
# The nasab chain in order (ism, father, grandfather, ...): only names linked by "بن",
# so a trailing nisba ("سليمان بن عمرو النخعي") is not mistaken for a grandfather.
NASAB_CHAIN = re.compile(r'\s*((?:عبد\s+)?\S+(?:\s+(?:بن|ابن)\s+(?:أبي\s+|عبد\s+)?[^\s،.]+)*)')
def nasab_chain(s: str) -> list[str]:
    s = re.sub(r'(?<!\S)بن\s+بن(?!\S)', 'بن', s)      # edition typo: "حماد بن بن سلمة"
    m = NASAB_CHAIN.match(s + ' ')
    return tokens(m.group(1)) if m else []


nasab = [nasab_chain(re.split(r'[،.\n]', e['header'])[0]) for e in entries]
laqab_index = defaultdict(set)
for j, toks in enumerate(own_tokens):
    for w in toks:
        laqab_index[w].add(j)

_dir = os.path.dirname(os.path.abspath(sys.argv[1]))
_taqrib = os.path.join(_dir, 'taqrib.json')
taqrib_alias_hits = 0
if os.path.exists(_taqrib):
    NOTE = re.compile(r'\s(?:بضم|بفتح|بكسر|بالتصغير|مصغر|عن|شيخ|روى|يروي|تقدم|يأتي|في الكنى)\b.*')
    for x in json.load(open(_taqrib, encoding='utf-8'))['xrefs']:
        alias = NOTE.sub('', x['alias']).strip()
        target = NOTE.sub('', x['target']).strip()
        a_toks, t_raw = tokens(alias), norm(target).split()
        if not a_toks or not t_raw:
            continue
        # "X الأعمش هو ابن مهران" completes X's nasab (X may be a compound "عبد الله");
        # otherwise the target is a full name.
        alias_ism = re.match(r'\s*(عبد\s+\S+|\S+)', alias).group(1)
        full = f'{alias_ism} {target}' if t_raw[0] in ('ابن', 'بن') else target
        hit = name_candidates(full)
        if len(hit) == 1:
            alias_index[tuple(a_toks)] |= hit
            taqrib_alias_hits += 1


# Exact name forms recorded by shaykh books ("وورد: أبو القاسم الفقيه"): tokens -> entries.
# Bare kunyas ("أبو خليفة" = الفضل بن الحباب in ري الظمآن; "وورد: أبو إسحاق" for al-Bayhaqi's
# shaykh يحيى بن إبراهيم) are shared by many narrators ("أبو إسحاق" from Shu'ba is al-Sabi'i),
# so they are loose: they only add a candidate and the chain context decides.
exact_aliases: dict[tuple, set[int]] = defaultdict(set)
loose_aliases: dict[tuple, set[int]] = defaultdict(set)
for _j, _e in enumerate(entries):
    for _a in _e.get('aliases', []) + _e.get('kunya_aliases', []):
        _t = tuple(tokens(_a))
        if len(_t) >= 3 or (len(_t) == 2 and _t[0] != 'ابو' and _a in _e.get('aliases', [])):
            exact_aliases[_t].add(_j)
        elif _t:
            loose_aliases[_t].add(_j)


@lru_cache(maxsize=None)
def candidates(name: str) -> frozenset[int]:
    return _candidates(name) | frozenset(loose_aliases.get(tuple(tokens(name)), ()))


def _candidates(name: str) -> frozenset[int]:
    toks = tokens(name)
    if not toks or toks[0] == 'نبي':
        return frozenset()
    if tuple(toks) in exact_aliases:        # the compiler's own spelling of his shaykh (إتحاف المرتقي)
        return frozenset(exact_aliases[tuple(toks)])
    raw = soft_norm(name).strip()
    if raw.startswith('ابن ') and len(toks) <= 3:
        # "ابن جريج", "ابن أبي ذئب": the words must be a father/ancestor in the narrator's own nasab
        # (checked first: dropping "ابن" would otherwise turn "ابن وهب" into a narrator named وهب).
        n = len(toks)
        found = frozenset(j for j in laqab_index.get(toks[0], ())
                          if any(nasab[j][p:p + n] == toks for p in range(1, 5)))
        return found or frozenset(alias_index.get(tuple(toks), ()))
    found = name_candidates(name)
    if found:
        return found
    if tuple(toks) in alias_index:          # aliases only fill in when the name itself matches nobody
        return frozenset(alias_index[tuple(toks)])
    if len(toks) == 1 and re.match(r'ال\S', name.strip()):
        return frozenset(laqab_index.get(toks[0], ()))   # bare laqab / nisba: "الأعمش", "الزهري"
    return frozenset()


def fame_pick(cands: set[int]) -> int | None:
    """The clearly most-cited narrator among `cands` (3x the students of the runner-up), if any."""
    ranked = sorted(cands, key=lambda j: -fame[j])
    if len(ranked) == 1:
        return ranked[0]
    if fame[ranked[0]] >= 3 * max(1, fame[ranked[1]]):
        return ranked[0]
    return None


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
name_candidates.cache_clear()
print(f'entries: {len(entries)}  crossref aliases attached: {alias_hits}/{len(xrefs)}  Taqrib aliases: {taqrib_alias_hits}')


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
