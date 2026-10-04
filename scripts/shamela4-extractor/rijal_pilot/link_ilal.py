"""Phase 3: link the Ilal data (mudallisin, mukhtalitun) to registry ids.

Usage: python link_ilal.py <data_dir> [<out.json>]
  <data_dir> holds tahdhib.json, taqrib.json, align.json, extra_*.json (the registry inputs),
  mudallisin.json (parse_mudallisin.py) and mukhtalitun.json (the merged ikhtilat extraction).
  Writes <out.json> (default <data_dir>/ilal.json) and prints the linking statistics.

A narrator's name is matched like a list name (link_tahdhib's `candidates`, with the entry's book
symbols). When several narrators fit, the one whose own words (Taqrib's verdict, Tahdhib's quotes)
mention tadlis / ikhtilat is taken, then the one whose header has every word of the name, then a
clearly more cited one (`fame_pick`); otherwise it stays unlinked, with its candidates, for review.
A student heard before / after the ikhtilat must be listed with the mukhtalit (as his student, or the
mukhtalit as his shaykh), except when the name has a single candidate in the whole registry.
"""
import json
import os
import re
import sys
from collections import Counter

SCRIPTS = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, SCRIPTS)
from pipeline import load_linker, registry_ids  # noqa: E402

DATA = os.path.abspath(sys.argv[1])
OUT = sys.argv[2] if len(sys.argv) > 2 else os.path.join(DATA, 'ilal.json')

ns = load_linker(SCRIPTS, DATA, os.path.join(DATA, '..', 'itqan', 'sunni', 'bukhari'))
entries, candidates, symbol_filter, fame_pick = ns['entries'], ns['candidates'], ns['symbol_filter'], ns['fame_pick']
shuyukh_of, talamidh_of, soft_norm, tokens = ns['shuyukh_of'], ns['talamidh_of'], ns['soft_norm'], ns['tokens']
nasab_chain = ns['nasab_chain']
tahdhib = [e for e in json.load(open(os.path.join(DATA, 'tahdhib.json'), encoding='utf-8')) if e['kind'] == 'entry']
N_TAHDHIB = len(tahdhib)
ids = registry_ids(entries, N_TAHDHIB)
taqrib = json.load(open(os.path.join(DATA, 'taqrib.json'), encoding='utf-8'))['entries']
taqrib_of = {p['tahdhib']: taqrib[p['taqrib']] for p in json.load(open(os.path.join(DATA, 'align.json'), encoding='utf-8'))}

TADLIS = re.compile(r'دلس|تدليس')
IKHTILAT = re.compile(r'اختلط|تغير|خلط')
# Symbols in the Ilal books carry remarks ("خت م مقرونا ٤"); only real book symbols are kept.
SYMBOL = re.compile(r'^(?:ع|٤|٣|خ|م|د|ت|س|ق|خت|بخ|ر|عخ|مق|قد|كن|سي|مد|فق|ل|تم|ص|عس|خد|جر|ز|كد|فد|ي|ف|ق د)$')


def own_words(j: int) -> str:
    """What the narrator's entry says about him: Taqrib's verdict and Tahdhib's attributed quotes."""
    t = taqrib_of.get(j)
    quotes = ' '.join(q.get('text', '') if isinstance(q, dict) else str(q) for q in entries[j].get('quotes') or [])
    return ' '.join(filter(None, [entries[j]['header'], t and t['raw'], quotes, entries[j].get('verdict')]))


def name_forms(name: str):
    """The full name, then shorter forms dropping trailing words (nisbas, kunya), down to ism + father."""
    words = re.sub(r'\s+', ' ', name).strip().split(' ')
    for n in range(len(words), 1, -1):
        form = ' '.join(words[:n])
        # Never a lone ism: "عبد الرحمن" is one name, not ism + father.
        if not re.search(r'(?:^|\s)(?:بن|ابن|أبو|أبي|أم|بنت|عبد)$', form) and len(tokens(form)) > 1:
            yield form
    if len(words) == 1:
        yield words[0]


NASAB_WORDS = {'بن', 'ابن', 'بنت', 'ابو', 'ابي', 'ام'}
MIN_COVER = 0.6                     # share of the name's words the narrator's own name must have


def name_words(text: str) -> set[str]:
    return {w for w in tokens(soft_norm(text)) if w not in NASAB_WORDS}


def cover(name: str, j: int) -> float:
    """Share of the name's words found in the narrator's header or his Taqrib name."""
    want = name_words(name)
    t = taqrib_of.get(j)
    have = name_words(entries[j]['header'][:250] + ' ' + (t['name'] if t else ''))
    return len(want & have) / max(1, len(want))


def link_narrator(names: list[str], symbols: list[str] | str, marker: re.Pattern) -> dict:
    """Registry id for a narrator of an Ilal book, how it was decided, and the candidates seen.

    Candidates come from every form of every name (the full name, then shorter forms). The best
    covered one is taken (MIN_COVER at least); a tie goes to the one whose own words mention the
    defect, then to a clearly more cited one. A short form alone matched the wrong man too often
    («الحسين بن عطاء بن يسار» → الحسين بن حفص), hence the cover."""
    syms = ' '.join(s for s in (symbols.split() if isinstance(symbols, str) else symbols) if SYMBOL.match(s))
    pool: dict[int, str] = {}                       # candidate -> the first form that found it
    for name in names:
        for form in name_forms(name):
            for j in symbol_filter(set(candidates(form)), syms):
                pool.setdefault(j, form)
    if not pool:
        return {'id': None, 'how': 'missing'}
    score = {j: max(cover(n, j) for n in names) for j in pool}
    best = max(score.values())
    top = {j for j in pool if score[j] == best}
    how = 'cover'
    if len(top) > 1:
        # The nasab as given, father first: «عبد الرحمن بن عبد الله بن مسعود» is Ibn Mas'ud's son, not
        # al-Mas'udi (عبد الرحمن بن عبد الله بن عتبة بن عبد الله بن مسعود), who also has every word.
        exact = {j for j in top if any(nasab_chain(entries[j]['header'][:250])[:len(c)] == c
                                       for c in (nasab_chain(n) for n in names) if len(c) > 1)}
        if exact:
            top, how = exact, 'nasab'
    if how == 'nasab' and len(top) > 1 and (in_tahdhib := {j for j in top if j < N_TAHDHIB}):
        # A Tahdhib narrator also entered in a compiler's rijal book (رجال الحاكم) is one man.
        if len(in_tahdhib) < len(top):
            top, how = in_tahdhib, 'tahdhib'
    if len(top) > 1:
        marked = {j for j in top if marker.search(own_words(j))}
        if len(marked) == 1:
            top, how = marked, 'marker'
        elif (f := fame_pick(marked or top)) is not None:
            top, how = {f}, 'fame'
    if best < MIN_COVER or len(top) > 1:
        return {'id': None, 'how': 'ambiguous' if best >= MIN_COVER else 'low_cover', 'cover': round(best, 2),
                'candidates': [f'{ids[j]} {score[j]:.2f} {entries[j]["header"][:60]}'
                               for j in sorted(pool, key=lambda j: (-score[j], -ns['fame'][j]))[:6]]}
    (j,) = top
    return {'id': ids[j], 'index': j, 'how': how if len(pool) > 1 else 'unique', 'cover': round(best, 2),
            'form': pool[j], 'header': entries[j]['header'][:100], 'marked': bool(marker.search(own_words(j)))}


def link_student(name: str, mukhtalit: int) -> dict:
    """The student of a mukhtalit: a candidate listed with him, else a name with a single candidate."""
    for form in name_forms(name):
        c = set(candidates(form)) - {mukhtalit}
        if not c:
            continue
        listed = {j for j in c if j in talamidh_of[mukhtalit] or mukhtalit in shuyukh_of[j]}
        if len(listed) == 1:
            (j,) = listed
            return {'id': ids[j], 'how': 'listed', 'form': form, 'header': entries[j]['header'][:80]}
        if len(listed) > 1:
            if (f := fame_pick(listed)) is not None:
                return {'id': ids[f], 'how': 'listed_fame', 'form': form, 'header': entries[f]['header'][:80]}
            return {'id': None, 'how': 'ambiguous', 'form': form,
                    'candidates': [ids[j] + ' ' + entries[j]['header'][:50] for j in listed][:6]}
        if len(c) == 1:
            (j,) = c
            return {'id': ids[j], 'how': 'unique_unlisted', 'form': form, 'header': entries[j]['header'][:80]}
        # The critics name famous students the lists may miss («وكيع بن الجراح» under Ibn Abi Aruba).
        if form == name and (f := fame_pick(c)) is not None:
            return {'id': ids[f], 'how': 'fame_unlisted', 'form': form, 'header': entries[f]['header'][:80]}
        return {'id': None, 'how': 'unlisted', 'form': form, 'n_candidates': len(c)}
    return {'id': None, 'how': 'missing'}


# Links decided by hand after the review (git-tracked, next to this script): "mudallisin" by entry
# number, "mukhtalitun" by name; {"id": <registry id or null>, "note": why}.
OVERRIDES = json.load(open(os.path.join(SCRIPTS, 'ilal_overrides.json'), encoding='utf-8'))
index_of = {r: j for j, r in enumerate(ids)}


def overridden(section: str, key: str, link: dict) -> dict:
    o = OVERRIDES.get(section, {}).get(key)
    if o is None:
        return link
    j = index_of.get(o['id']) if o['id'] else None
    if o['id'] and j is None:
        sys.exit(f'override {section}/{key}: unknown registry id {o["id"]}')
    return {'id': o['id'], 'index': j, 'how': 'manual', 'note': o.get('note'), 'auto': link.get('id'),
            'header': entries[j]['header'][:100] if j is not None else None}


out = {'mudallisin': [], 'mukhtalitun': []}
stats = {}

mud_path = os.path.join(DATA, 'mudallisin.json')
if os.path.exists(mud_path):
    for e in json.load(open(mud_path, encoding='utf-8'))['entries']:
        link = overridden('mudallisin', str(e['num']), link_narrator([e['name']], e['symbols'], TADLIS))
        out['mudallisin'].append({'num': e['num'], 'tier': e['tier'], 'kind': e['kind'], 'name': e['name'],
                                  'also_mukhtalit': e.get('also_mukhtalit', False), **link})
    stats['mudallisin'] = Counter(m['how'] for m in out['mudallisin'])

mukh_path = os.path.join(DATA, 'mukhtalitun.json')
if os.path.exists(mukh_path):
    for e in json.load(open(mukh_path, encoding='utf-8'))['entries']:
        link = overridden('mukhtalitun', e['name'],
                          link_narrator([e['name']] + e.get('short_names', []), e.get('symbols', ''), IKHTILAT))
        rec = {k: e.get(k) for k in ('name', 'books', 'ikhtilat', 'year', 'severity', 'no_one_after', 'group_rules')}
        rec.update(link)
        rec['hearings'] = []
        if link.get('index') is not None:
            for timing in ('before', 'after', 'both'):
                for s in e.get(f'heard_{timing}', []):
                    rec['hearings'].append({'timing': timing, 'name': s['name'], 'source': s.get('source'),
                                            'critic': s.get('critic'), 'quote': s.get('quote'),
                                            **link_student(s['name'], link['index'])})
        out['mukhtalitun'].append(rec)
    stats['mukhtalitun'] = Counter(m['how'] for m in out['mukhtalitun'])
    stats['hearings'] = Counter(h['how'] for m in out['mukhtalitun'] for h in m['hearings'])

# The app's current hand-written seeds, linked the same way, so a bench run can compare the two lists.
SEEDS = os.path.join(SCRIPTS, '..', '..', '..', 'src', 'SmartHadithTree.Etl', 'Seeds')
if os.path.isdir(SEEDS):
    out['seed_mudallisin'] = [{'name': s['names'][0], 'tier': s['tier'], **link_narrator(s['names'], '', TADLIS)}
                              for s in json.load(open(os.path.join(SEEDS, 'mudallisin.json'), encoding='utf-8'))['entries']]
    out['seed_mukhtalitun'] = []
    for s in json.load(open(os.path.join(SEEDS, 'mukhtalitun.json'), encoding='utf-8'))['entries']:
        link = link_narrator(s['names'], '', IKHTILAT)
        hearings = [{'timing': timing, 'name': names[0], **link_student(names[0], link['index'])}
                    for timing, key in (('before', 'heardBefore'), ('after', 'heardAfter')) if link.get('index') is not None
                    for names in s.get(key, [])]
        out['seed_mukhtalitun'].append({'name': s['names'][0], **link, 'hearings': hearings})
    stats['seeds'] = Counter(m['how'] for m in out['seed_mudallisin'] + out['seed_mukhtalitun'])

for m in [m for k in out for m in out[k]]:
    m.pop('index', None)
json.dump(out, open(OUT, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
for k, v in stats.items():
    print(k, dict(v))
