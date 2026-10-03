"""Parse the two books on al-Hakim's narrators into registry entries.

- الروض الباسم في تراجم شيوخ الحاكم (Shamela 14463): "[n] name" + "سمع:" / "وعنه:" lists
  and the author's verdict "قلت: [ثقة]". Every entry is a shaykh of al-Hakim.
- رجال الحاكم في المستدرك (Shamela 29742): "n - name:" + the Mustadrak isnad where the
  narrator occurs; the names right before/after him there give a student/shaykh.

Entries already in Tahdhib al-Kamal (same ism + father + grandfather) are skipped, and the
two books are merged with each other the same way. A synthetic entry for al-Hakim lists
all of al-Rawd al-Basim's narrators as his shuyukh.
Usage: python parse_hakim_books.py dump_dir tahdhib.json out.json
"""
import json
import re
import sys
from collections import Counter

DUMP, TAHDHIB, OUT = sys.argv[1:4]
AR_DIGITS = str.maketrans('٠١٢٣٤٥٦٧٨٩', '0123456789')
HARAKAT = re.compile(r'[ً-ْٰـ]')
STOP = {'بن', 'ابن', 'بنت', 'ابو', 'ام'}
VERB = r'(?:حدثنا|حدثني|حدثناه|أخبرنا|أخبرني|أخبرناه|أنبأنا|أنبأ|أنبأني|ثنا|نا|أنا|عن|قالا|قالوا)'


def load(bid: int) -> str:
    pages = []
    with open(f'{DUMP}/{bid}_pages.tsv', encoding='utf-8') as f:
        for line in f:
            pid, body = line.rstrip('\n').split('\t', 1)
            pages.append((int(pid.split('-')[1]), body.replace('\\n', '\n')))
    pages.sort()
    text = '\n'.join(b for _, b in pages)
    text = re.sub(r'<span[^>]*>|</span>', '', text)
    text = re.sub(r'\s*\(¬?[٠-٩]+\)', '', text)            # footnote refs
    return HARAKAT.sub('', text)


def norm_tokens(s: str, n: int = 3) -> tuple:
    s = re.sub(r'-[^-]*-', ' ', s)                       # "علي بن حمشاذ -واسمه محمد- بن سختويه"
    s = re.sub('[أإآ]', 'ا', s).replace('ى', 'ي').replace('ة', 'ه')
    s = re.sub(r'\bال(?=\S)', '', re.split(r'[،.:]', s)[0])
    s = re.sub(r'\bعبد\s+(?:ال)?(\S+)', r'عبد_\1', s)     # "عبد الله" is one name, not two
    return tuple(w for w in re.sub(r'[^ء-ي_ ]', ' ', s).split() if w not in STOP)[:n]


def split_names(seg: str) -> list[dict]:
    """'X، وببغداد: Y -وأكثر عنه- وZ، وآخرون' -> [X, Y, Z]"""
    seg = re.sub(r'-[^-\n]{0,40}-', ' ', seg)            # parenthetical "-وأكثر عنه-"
    seg = re.sub(r'"[^"]*"', ' ', seg)                    # book titles
    items = []
    for part in re.split(r'،|\sو(?=[ء-ي])', seg):
        part = part.strip()
        if ':' in part:                                   # "فسمع بالري من: محمد بن مندة", "وبهمذان: X"
            part = part.rsplit(':', 1)[1]
        part = re.sub(rf'^و?{VERB}\s+', '', part.strip())  # "وأخبرني أحمد ..." -> "أحمد ..."
        part = re.sub(r'^و(?=[ء-ي]{3})', '', part).strip(' .')
        part = re.sub(r'\s+في\s*$', '', part)              # "أبو عبد الله الحاكم في" ("مستدركه")
        if not part or len(part.split()) > 9 or NOT_NAME.match(part):
            continue
        # A name has an ism + nasab, a kunya, or an "ابن X" form.
        if not re.search(r'(?:^|\s)(?:بن|ابن|أبو|أبي|أبا|بنت|أم)\s', part + ' '):
            continue
        items.append({'name': part, 'symbols': '', 'note': ''})
    return items


NOT_NAME = re.compile(r'(?:آخرون|غيرهم|طبقته|جماعة|أقران|خلق|حج|صفه|وصفه|أكثر|سمع|فسمع|رحل|قدم|كان|ولد|قال|'
                      r'حدث|روى|كتب|سنة|في سنة|سبعين|ثمانين|تسعين|مائة|وغير)(?:\s|$)')


# Keys use 4 name tokens: "عبد الله" alone is two tokens, so 3 tokens would equate
# "يحيى بن محمد بن عبد الله بن العنبر" with any "يحيى بن محمد بن عبد الله".
KEY_LEN = 4
tahdhib_keys = {norm_tokens(e['header'], KEY_LEN) for e in json.load(open(TAHDHIB, encoding='utf-8'))
                if e['kind'] == 'entry'}
entries: list[dict] = []
by_key: dict[tuple, dict] = {}
rawd_by_two: dict[tuple, list] = {}
stats = Counter()


def add(entry: dict) -> None:
    key = norm_tokens(entry['header'], KEY_LEN)
    if len(key) >= KEY_LEN and key in tahdhib_keys:
        stats[f'{entry["source"]}: already in Tahdhib'] += 1
        return
    old = by_key.get(key) if len(key) >= KEY_LEN else None
    if old is None and entry['source'] == 'rijal_hakim':
        # Rijal al-Hakim often gives a short head ("علي بن حمشاذ العدل"): merge on ism + father
        # when exactly one al-Rawd al-Basim narrator has them.
        same = rawd_by_two.get(norm_tokens(entry['header'], 2), [])
        old = same[0] if len(same) == 1 else None
    if old is not None and old['source'] != entry['source']:   # same narrator in both books: merge lists
        old['shuyukh'] += entry['shuyukh']
        old['talamidh'] += entry['talamidh']
        old['verdict'] = old['verdict'] or entry['verdict']
        old['source'] += '+' + entry['source']
        stats['merged across the two books'] += 1
        return
    entries.append(entry)
    by_key.setdefault(key, entry)
    if entry['source'] == 'rawd':
        rawd_by_two.setdefault(norm_tokens(entry['header'], 2), []).append(entry)
    stats[f'{entry["source"]}: added'] += 1


# 1. al-Rawd al-Basim
rawd = load(14463)
heads = list(re.finditer(r'\[([٠-٩]+)\]\s*([^\n]+)', rawd))
for k, m in enumerate(heads):
    body = rawd[m.end():heads[k + 1].start() if k + 1 < len(heads) else len(rawd)]
    sh = re.search(r'(?:^|\n)\s*(?:سمع|روى عن|حدث عن)\s*(?:من)?\s*:\s*([^\n]+)', body)
    tl = re.search(r'(?:^|\n)\s*(?:وعنه|روى عنه|حدث عنه)\s*:\s*([^\n]+)', body)
    verdict = re.findall(r'قلت\s*:\s*\[([^\]]{2,80})\]', body)
    # Drop the author's asides: "علي بن حمشاذ -واسمه محمد- بن سختويه" -> "علي بن حمشاذ بن سختويه".
    header = re.sub(r'\s*-[^-\n]{1,60}-\s*', ' ', m.group(2)).strip(' .')
    add({'kind': 'entry', 'num': int(m.group(1).translate(AR_DIGITS)), 'num_suspect': False, 'symbols': '',
         'header': header, 'raw_header': m.group(2).strip(' .'), 'name': header.split('،')[0].strip(),
         'shuyukh': split_names(sh.group(1)) if sh else [], 'talamidh': split_names(tl.group(1)) if tl else [],
         'quotes': [], 'rawa_lahu': None, 'verdict': verdict[-1] if verdict else None, 'source': 'rawd'})
rawd_names = [e['header'] for e in entries] + [  # shaykhs of al-Hakim already in Tahdhib are his shaykhs too
    re.sub(r'\s*-[^-\n]{1,60}-\s*', ' ', m.group(2)) for m in heads
    if norm_tokens(re.sub(r'\s*-[^-\n]{1,60}-\s*', ' ', m.group(2)), KEY_LEN) in tahdhib_keys]

# 2. Rijal al-Hakim fi al-Mustadrak
rijal = load(29742)
heads = list(re.finditer(r'(?m)^\s*([٠-٩]+)\s*-\s*([^\n:]{3,120}):?\s*$', rijal))
for k, m in enumerate(heads):
    body = rijal[m.end():heads[k + 1].start() if k + 1 < len(heads) else len(rijal)]
    name = m.group(2).strip()
    if re.search(r'انظره|انظر', name):
        continue                                          # "انظره في إبراهيم ..." redirect
    shuyukh, talamidh = [], []
    key = norm_tokens(name, 2)
    for snippet in re.findall(r'\* قال الحاكم[^\n]*\n([^\n]+)', body):
        segs = [s.strip(' ،.') for s in re.split(rf'(?:^|\s|،)و?{VERB}(?=\s|،|:)', snippet)]
        segs = [s for s in segs if 1 < len(s) < 80]
        for i, s in enumerate(segs):
            if norm_tokens(s, 2) == key or (key and key[0] in norm_tokens(s, 4) and len(key) > 1 and key[1] in norm_tokens(s, 4)):
                if i > 0:
                    talamidh.append({'name': segs[i - 1], 'symbols': '', 'note': 'isnad'})
                if i + 1 < len(segs):
                    shuyukh.append({'name': segs[i + 1], 'symbols': '', 'note': 'isnad'})
                break
    m1 = re.search(r'ذكر من (?:مشايخه|شيوخه)\s*:\s*([^\n]+)', body)
    m2 = re.search(r'(?:من الرواة عنه|ومن الرواة عنه|وعنه)\s*:\s*([^\n]+)', body)
    shuyukh += split_names(m1.group(1)) if m1 else []
    talamidh += split_names(m2.group(1)) if m2 else []
    add({'kind': 'entry', 'num': int(m.group(1).translate(AR_DIGITS)), 'num_suspect': False, 'symbols': '',
         'header': name, 'name': name, 'shuyukh': shuyukh, 'talamidh': talamidh, 'quotes': [],
         'rawa_lahu': None, 'verdict': None, 'source': 'rijal_hakim'})

# 3. al-Hakim himself: his shuyukh are al-Rawd al-Basim's narrators.
entries.append({'kind': 'entry', 'num': 0, 'num_suspect': False, 'symbols': '', 'source': 'compiler',
                'header': 'محمد بن عبد الله بن محمد بن حمدويه الحاكم، أبو عبد الله النيسابوري، ابن البيع',
                'name': 'محمد بن عبد الله بن محمد بن حمدويه', 'verdict': 'الإمام الحافظ',
                'shuyukh': [{'name': h.split('،')[0], 'symbols': '', 'note': 'rawd'} for h in rawd_names],
                'talamidh': [], 'quotes': [], 'rawa_lahu': None})

for k, v in sorted(stats.items()):
    print(f'{k:40} {v}')
print(f'new entries: {len(entries)}  with shuyukh: {sum(1 for e in entries if e["shuyukh"])}  '
      f'with talamidh: {sum(1 for e in entries if e["talamidh"])}  with verdict: {sum(1 for e in entries if e["verdict"])}')
print('verdicts:', Counter((e['verdict'] or '').split(' ')[0] for e in entries if e['verdict']).most_common(10))
json.dump(entries, open(OUT, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
