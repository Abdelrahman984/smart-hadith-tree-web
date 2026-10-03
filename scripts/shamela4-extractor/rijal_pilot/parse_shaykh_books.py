"""Parse books on a compiler's narrators (شيوخ / رجال) into one set of registry entries.

Each book in BOOKS has a layout:
- 'bracket'  "[n] name" + "سمع:" / "حدث عن:" / "وعنه:" lists + the author's verdict
             "قلت: [ثقة]" or "قلت: (ثقة)"   (الروض الباسم، إرشاد القاصي والداني، الدليل المغني)
- 'paren'    "(n) name" + "روى عن:" / "سمع منه:" lists + "وورد:" forms of the name as the
             compiler writes it in his books, kept as exact aliases       (إتحاف المرتقي)
- 'star'     "* name." entries quoting other rijal books ("يروي عن:" / "روى عنه:")
                                                      (تحفة الغريب)
- 'isnad'    "n - name:" + the compiler's isnad where the narrator occurs; the names right
             before/after him there give a student/shaykh   (رجال الحاكم في المستدرك)
- 'dash'     "n - name." (or "* n - name.") + "روي عن:" / "روى عنه:" lists; "[تمييز]" entries
             (namesakes who are not the compiler's shaykhs) inside the body are cut off
                                                      (ري الظمآن، شيوخ ابن حبان)
- 'runs'     "[n]" parts whose numbering restarts: the compiler's shaykh list, his student
             list, then biographies with "روى عن:" / "وروى عنه:"     (المسالك القويمة)
- 'tajil'    one paragraph per narrator: "N - <symbols>name عن X وعنه Y <verdict>"
                                    (تعجيل المنفعة: narrators of Ahmad, Malik, al-Shafi'i,
                                     Abu Hanifa who are not in Tahdhib)

A book with a 'compiler' key lists that compiler's own shaykhs: a synthetic entry for the
compiler gets all of them as shuyukh, so isnads can be walked from the compiler down.
Narrators already in Tahdhib al-Kamal are skipped; the same narrator in several books is
merged into one entry (short heads such as "علي بن حمشاذ العدل" merge on ism + father when
exactly one full-head entry has them).
Usage: python parse_shaykh_books.py dump_dir tahdhib.json out.json
"""
import json
import re
import sys
from collections import Counter

DUMP, TAHDHIB, OUT = sys.argv[1:4]

COMPILERS = {
    'hakim': 'محمد بن عبد الله بن محمد بن حمدويه الحاكم، أبو عبد الله النيسابوري، ابن البيع',
    'tabarani': 'سليمان بن أحمد بن أيوب بن مطير اللخمي الطبراني، أبو القاسم',
    'bayhaqi': 'أحمد بن الحسين بن علي بن موسى الخسروجردي البيهقي، أبو بكر',
    'ibnhibban': 'محمد بن حبان بن أحمد بن حبان بن معاذ التميمي البستي، أبو حاتم',
    'daraqutni': 'علي بن عمر بن أحمد بن مهدي البغدادي الدارقطني، أبو الحسن',
    'ibnkhuzaymah': 'محمد بن إسحاق بن خزيمة بن المغيرة بن صالح السلمي النيسابوري، أبو بكر',
}
# How other compilers name a compiler in their isnads (al-Bayhaqi: "أبو عبد الله الحافظ" = al-Hakim).
# These become exact aliases, so keep them specific.
COMPILER_ALIASES = {
    'hakim': ['أبو عبد الله الحافظ', 'أبو عبد الله محمد بن عبد الله الحافظ'],
}
BOOKS = [   # full-head books first: short-head books merge into them
    {'id': 14463, 'source': 'rawd', 'layout': 'bracket', 'compiler': 'hakim'},
    {'id': 29745, 'source': 'irshad', 'layout': 'bracket', 'compiler': 'tabarani'},
    {'id': 123667, 'source': 'salsabil', 'layout': 'bracket', 'compiler': 'bayhaqi'},
    {'id': 7852, 'source': 'dalil', 'layout': 'bracket', 'compiler': 'daraqutni'},
    {'id': 123666, 'source': 'ithaf', 'layout': 'paren', 'compiler': 'bayhaqi'},
    {'id': 1498, 'source': 'rayy', 'layout': 'dash', 'compiler': 'ibnhibban'},
    # Its biographies are all narrators of Ibn Khuzaymah outside Tahdhib (not only his shaykhs),
    # so 'lists_of' instead of 'compiler': only the shaykh / student lists go to his entry.
    {'id': 151171, 'source': 'masalik', 'layout': 'runs', 'lists_of': 'ibnkhuzaymah',
     'runs': ['shuyukh', 'talamidh', 'entries']},
    {'id': 29742, 'source': 'rijal_hakim', 'layout': 'isnad', 'isnad_marker': 'الحاكم'},
    {'id': 1208, 'source': 'tuhfa', 'layout': 'star'},
    {'id': 1893, 'source': 'tajil', 'layout': 'tajil'},
]

AR_DIGITS = str.maketrans('٠١٢٣٤٥٦٧٨٩', '0123456789')
HARAKAT = re.compile(r'[ً-ْٰـ]')
STOP = {'بن', 'ابن', 'بنت', 'ابو', 'ام'}
VERB = r'(?:حدثنا|حدثني|حدثناه|أخبرنا|أخبرني|أخبرناه|أنبأنا|أنبأ|أنبأني|ثنا|نا|أنا|عن|قالا|قالوا)'
SHUYUKH_RE = re.compile(r'(?:^|\n)\s*(?:سمع|روى عن|روي عن|حدث عن|يروي عن|روت عن|تروي عن)\s*(?:من)?\s*:\s*([^\n]+)')
TALAMIDH_RE = re.compile(r'(?:^|\n)\s*(?:وعنه|و?روى عنه|روى عنها|حدث عنه|روت عنه|يروي عنه|سمع منه'
                         r'|و?روى عنه أيضا)\s*:\s*([^\n]+)')
# إتحاف المرتقي lists every form in which al-Bayhaqi names the shaykh:
# "وقد ورد هذا الاسم في مصنفات البيهقي:\n<form>\nوورد: <form>\nوورد: <form>"
ALIAS_FIRST_RE = re.compile(r'ورد هذا الاسم في مصنفات[^\n]*:\s*\n([^\n]+)')
ALIAS_RE = re.compile(r'(?m)^\s*وورد\s*:\s*([^\n]+)')
VERDICT_RE = re.compile(r'قلت\s*:\s*[\[(]([^\])\n]{2,80})[\])]')
ASIDE = re.compile(r'\s*-[^-\n]{1,60}-\s*')        # "علي بن حمشاذ -واسمه محمد- بن سختويه"
NOT_NAME = re.compile(r'(?:آخرون|غيرهم|طبقته|جماعة|أقران|خلق|حج|صفه|وصفه|أكثر|سمع|فسمع|رحل|قدم|كان|ولد|قال|'
                      r'حدث|روى|كتب|سنة|في سنة|سبعين|ثمانين|تسعين|مائة|وغير|أهل|الغرباء)(?:\s|$)')
# Keys use 4 name tokens with "عبد X" as one token: 3 would equate
# "يحيى بن محمد بن عبد الله بن العنبر" with any "يحيى بن محمد بن عبد الله".
KEY_LEN = 4


def load(bid: int) -> str:
    pages = []
    with open(f'{DUMP}/{bid}_pages.tsv', encoding='utf-8') as f:
        for line in f:
            pid, body = line.rstrip('\n').split('\t', 1)
            pages.append((int(pid.split('-')[1]), body.replace('\\n', '\n')))
    pages.sort()
    text = '\n'.join(b for _, b in pages)
    text = re.sub(r'<span[^>]*>|</span>', '', text)
    text = re.sub(r'\s*\(¬[٠-٩]+\)', '', text)             # footnote refs "(¬١)"; "(٩٣)" is an entry number
    return HARAKAT.sub('', text)


def norm_tokens(s: str, n: int = 3) -> tuple:
    s = ASIDE.sub(' ', s)
    s = re.sub('[أإآ]', 'ا', s).replace('ى', 'ي').replace('ة', 'ه')
    s = re.sub(r'\bال(?=\S)', '', re.split(r'[،.:(]', s)[0])
    s = re.sub(r'\bعبد\s+(?:ال)?(\S+)', r'عبد_\1', s)
    return tuple(w for w in re.sub(r'[^ء-ي_ ]', ' ', s).split() if w not in STOP)[:n]


def split_names(seg: str, short_ok: bool = False) -> list[dict]:
    """'X، وببغداد: Y -وأكثر عنه- وZ، وآخرون' -> [X, Y, Z]

    short_ok keeps names without بن/أبو of up to 3 words ("عكرمة", "حماد بن سلمة" is kept
    anyway): تعجيل المنفعة lists narrators the way isnads name them."""
    seg = re.sub(r'-[^-\n]{0,40}-', ' ', seg)
    seg = re.sub(r'"[^"]*"', ' ', seg)                    # book titles
    seg = re.sub(r'\([^)\n]{0,30}\)', ' ', seg)           # symbols "(خز، حم)", grades "(ثقة)"
    items = []
    for part in re.split(r'،|\sو(?=[ء-ي])', seg):
        part = part.strip()
        if ':' in part:                                   # "فسمع بالري من: محمد بن مندة", "وبهمذان: X"
            part = part.rsplit(':', 1)[1]
        part = re.sub(rf'^و?{VERB}\s+', '', part.strip())
        part = re.sub(r'^و(?=[ء-ي]{3})', '', part).strip(' .')
        part = re.sub(r'\s+في\s*$', '', part)
        if not part or len(part.split()) > 9 or NOT_NAME.match(part):
            continue
        full = re.search(r'(?:^|\s)(?:بن|ابن|أبو|أبي|أبا|بنت|أم)\s', part + ' ')
        if not full and not (short_ok and len(part.split()) <= 3):
            continue
        # 'short' items ("نافع") give a teacher/student link only when they name one narrator.
        items.append({'name': part, 'symbols': '', 'note': '' if full else 'short'})
    return items


tahdhib_keys = {norm_tokens(e['header'], KEY_LEN) for e in json.load(open(TAHDHIB, encoding='utf-8'))
                if e['kind'] == 'entry'}
entries: list[dict] = []
by_key: dict[tuple, dict] = {}
full_by_two: dict[tuple, list] = {}
compiler_shuyukh: dict[str, list[str]] = {c: [] for c in COMPILERS}
compiler_talamidh: dict[str, list[str]] = {c: [] for c in COMPILERS}
kunya_aliases: list[tuple[str, str, str]] = []          # (source, alias, target name)
stats = Counter()


def add(entry: dict, short_head: bool) -> None:
    key = norm_tokens(entry['header'], KEY_LEN)
    if len(key) >= KEY_LEN and key in tahdhib_keys:
        stats[f'{entry["source"]}: already in Tahdhib'] += 1
        return
    old = by_key.get(key) if len(key) >= KEY_LEN else None
    if old is None and short_head:
        same = full_by_two.get(norm_tokens(entry['header'], 2), [])
        old = same[0] if len(same) == 1 else None
    same_text = old is not None and old['header'] == entry['header']   # a book repeating an entry verbatim
    if old is not None and (same_text or entry['source'] not in old['source'].split('+')):
        old['shuyukh'] += entry['shuyukh']
        old['talamidh'] += entry['talamidh']
        old['aliases'] = old.get('aliases', []) + entry.get('aliases', [])
        old['verdict'] = old['verdict'] or entry['verdict']
        if entry['source'] not in old['source'].split('+'):
            old['source'] += '+' + entry['source']
        stats[f'{entry["source"]}: merged into another entry'] += 1
        return
    entries.append(entry)
    by_key.setdefault(key, entry)
    if not short_head:
        full_by_two.setdefault(norm_tokens(entry['header'], 2), []).append(entry)
    stats[f'{entry["source"]}: added'] += 1


def entry(book: dict, num: int, header: str, shuyukh: list, talamidh: list, verdict: str | None) -> dict:
    return {'kind': 'entry', 'num': num, 'num_suspect': False, 'symbols': '', 'header': header,
            'name': header.split('،')[0].strip(), 'shuyukh': shuyukh, 'talamidh': talamidh, 'quotes': [],
            'rawa_lahu': None, 'verdict': verdict, 'source': book['source']}


def lists(body: str) -> tuple[list, list]:
    sh = [it for m in SHUYUKH_RE.finditer(body) for it in split_names(m.group(1))]
    tl = [it for m in TALAMIDH_RE.finditer(body) for it in split_names(m.group(1))]
    return sh, tl


# تعجيل المنفعة: "N - اأبان بن خالد الحنفي عن عبيد الله بن رواحة عن أنس وعنه أخوه ... وثقه ابن حبان".
# Book symbols lead the entry, and Ahmad's "ا" is often glued to the name ("اإبراهيم", "احرب").
TAJIL_SYMBOLS = {'ك', 'فع', 'ش', 'فه', 'ه', 'عب', 'هب', 'ا', 'خ', 'م', 'د', 'ت', 'س', 'ق', 'ع', 'خت', 'بخ', 'مد', 'كن'}
TAJIL_SHUYUKH = re.compile(r'\s(?:عن|روى عن|يروي عن|يروى عن)\s')
TAJIL_TALAMIDH = re.compile(r'\s(?:وعنه|روى عنه|روت عنه|وروى عنه|يروي عنه|وعنها)\s')
TAJIL_END = re.compile(r'\s(?:وثقه|قال|وقال|قلت|مجهول|فيه|ذكره|وذكره|ليس|ضعيف|صدوق|ثقة|لا يعرف|له صحبة'
                       r'|حديثه|مستور|ذكر|أخرج|اخرج|يأتي|وله|له|حديث|في|أن|انه|أنه)\s')
first_words = {re.split(r'\s', e['header'].strip())[0] for e in json.load(open(TAHDHIB, encoding='utf-8'))
               if e['header'].strip()}


def nasab_key(s: str) -> tuple:
    """"شعبة بن الحجاج بن الورد العتكي" -> (شعبه, حجاج, ورد): the names linked by بن."""
    s = re.sub('[أإآ]', 'ا', s).replace('ى', 'ي').replace('ة', 'ه')
    s = re.sub(r'\bعبد\s+(?:ال)?(\S+)', r'عبد_\1', s)
    m = re.match(r'\s*(\S+(?:\s+(?:بن|ابن)\s+(?:ابي\s+)?\S+)*)', re.split(r'[،.:(]', s)[0])
    return tuple(re.sub(r'^ال', '', w) for w in m.group(1).split() if w not in ('بن', 'ابن')) if m else ()


# Every nasab prefix of a Tahdhib narrator: a تعجيل entry "شعبة بن الحجاج" or "عمرو بن شعيب" is a
# remark on an isnad of that narrator, not a new narrator.
tahdhib_prefixes = set()
tahdhib_by_ism: dict[str, list[tuple]] = {}
for _e in json.load(open(TAHDHIB, encoding='utf-8')):
    _k = nasab_key(_e['header'])
    tahdhib_prefixes |= {_k[:n] for n in range(2, len(_k) + 1)}
    if _k:
        tahdhib_by_ism.setdefault(_k[0], []).append(_k)


def is_tahdhib_narrator(k: tuple) -> bool:
    """A prefix of a Tahdhib nasab, or with 3+ names an in-order subsequence of one that keeps
    the ism: "محمد بن عبد الرحمن بن أبي ذئب" skips المغيرة بن الحارث."""
    if k in tahdhib_prefixes:
        return True
    if len(k) < 3:
        return False
    for t in tahdhib_by_ism.get(k[0], ()):
        it = iter(t[1:])
        if all(w in it for w in k[1:]):
            return True
    return False


def tajil_entry(book: dict, num: int, whole: str) -> dict | None:
    words = whole.split()
    # Symbols, alone or glued to Ahmad's "ا" ("اه", "اك"), and "تمييز" (a namesake note).
    while words and (words[0] in TAJIL_SYMBOLS or words[0] == 'تمييز'
                     or (words[0][:1] == 'ا' and words[0][1:] in TAJIL_SYMBOLS)):
        words.pop(0)
    # A real name in this edition starts with أ/إ, the article or "ابن"; any other leading "ا" is
    # Ahmad's symbol: "اإبراهيم" -> "إبراهيم", "افزارة" -> "فزارة", "االحارث" -> "الحارث".
    if (words and words[0].startswith('ا') and len(words[0]) > 2 and words[0] not in first_words
            and not re.match(r'ال|ابن$|امرأة', words[0])):
        words[0] = words[0][1:]
    whole = ' ' + ' '.join(words) + ' '
    s, t = TAJIL_SHUYUKH.search(whole), TAJIL_TALAMIDH.search(whole)
    cut = min((x.start() for x in (s, t) if x), default=None)
    end = TAJIL_END.search(whole)
    header = whole[:cut if cut is not None else (end.start() if end else 80)].strip(' .،')
    if not header or re.search(r'\s(?:في ترجمة|يأتي في|تقدم في)|\sفي\s', ' ' + header + ' ') and cut is None:
        return None                                          # redirects: "زيد بن طلحة في يزيد بن ركانة"
    if len(header.split()) > 14 or header.startswith('من '):
        return None                                          # "من أهل الثغور", "من بلغ عائشة"
    if is_tahdhib_narrator(nasab_key(header)):
        stats[f'{book["source"]}: a Tahdhib narrator (remark)'] += 1
        return None
    shuyukh, talamidh = [], []
    if s and (not t or s.start() < t.start()):
        seg = whole[s.end():t.start() if t else len(whole)]
        seg = TAJIL_SHUYUKH.split(' ' + seg)[0]              # "عن X عن أنس": only X is his shaykh
        stop = TAJIL_END.search(' ' + seg + ' ')
        shuyukh = split_names(seg[:stop.start()] if stop else seg, short_ok=True)
    if t:
        seg = whole[t.end():]
        stop = TAJIL_END.search(' ' + seg)
        talamidh = split_names(seg[:stop.start()] if stop else seg[:200], short_ok=True)
    if not shuyukh and not talamidh:
        return None     # notes on a name ("عبد الرزاق", "زيد بن يثيع" = ابن أثيع): would only add a namesake
    return entry(book, num, header, shuyukh, talamidh, None)


for book in BOOKS:
    text = load(book['id'])
    layout = book['layout']
    if layout == 'bracket':
        heads = list(re.finditer(r'\[([٠-٩]+)\]\s*([^\n]+)', text))
    elif layout == 'paren':                               # "(٩٣) عبيد الله بن عمر ..." at a line start
        heads = [m for m in re.finditer(r'(?m)^\s*\(([٠-٩]+)\)\s*([^\n]+)', text)
                 if re.search(r'(?:^|\s)(?:بن|أبو)\s', m.group(2))]
    elif layout == 'star':
        heads = list(re.finditer(r'(?m)^\s*\*\s*()([^\n]{3,160})', text))
    elif layout == 'dash':
        # Only the running sequence 1, 2, 3 ... counts: the introduction, the notes ("١ - أنه توفي
        # ...") and the appendix of non-shaykhs restart the numbering.
        text = text[text.find('(حرف الألف)'):]
        heads, last = [], 0
        for m in re.finditer(r'(?m)^\s*\*?\s*([٠-٩]+)\s*-\s*([^\n]{3,200})', text):
            if int(m.group(1).translate(AR_DIGITS)) == last + 1:
                heads.append(m)
                last += 1
        # "[*] أبو خليفة = الفضل بن الحباب الجمحي.": the kunyas under which the compiler names a
        # shaykh, attached below as loose aliases ('kunya_aliases') of the entry the right side names.
        for alias, target in re.findall(r'(?m)^\s*\[\*\]\s*(أبو [^=\n،]{2,40}?)\s*=\s*([^\n]+)', text):
            target = re.sub(r'(?:،\s*)?تقدم.*$', '', target).replace('السراج:', '').strip(' .')
            kunya_aliases.append((book['source'], alias.strip(), target))
    elif layout == 'runs':
        # المسالك القويمة: "[n]" numbering restarts at each part; book['runs'] names the parts.
        # 'shuyukh' / 'talamidh' are bare lists of the compiler's shaykhs / students
        # ("[١] (س) أبو إسحاق إبراهيم بن إسماعيل ... (ثقة)"); 'entries' are biographies whose
        # head starts with book symbols ("[٢٨] (خز، طح): بكر بن إدريس ...").
        runs, last = [[]], 0
        for m in re.finditer(r'\[([٠-٩]+)\]\s*([^\n]+)', text):
            n = int(m.group(1).translate(AR_DIGITS))
            if n == 1 and last:
                runs.append([])
            runs[-1].append(m)
            last = n
        heads = []
        for kind, run in zip(book['runs'], runs):
            if kind == 'entries':
                heads = run
                continue
            target = compiler_shuyukh if kind == 'shuyukh' else compiler_talamidh
            for m in run:
                name = re.sub(r'^\([^)]*\)\s*:?\s*', '', m.group(2))
                target[book['lists_of']].append(re.split(r'\s*[(\[]', name)[0].strip(' .،'))
    elif layout == 'tajil':
        # Numbers repeat and jump in this edition, but every entry starts a line with "N -".
        text = text[re.search(r'(?m)^\s*١\s*-\s*\S*أبان بن خالد', text).start():]
        heads = list(re.finditer(r'(?m)^\s*([٠-٩]+)\s*-\s*([^\n]+)', text))
    else:
        heads = list(re.finditer(r'(?m)^\s*([٠-٩]+)\s*-\s*([^\n:]{3,120}):?\s*$', text))
    if layout == 'tajil':
        for k, m in enumerate(heads):
            whole = (m.group(2) + ' ' + text[m.end():heads[k + 1].start() if k + 1 < len(heads) else len(text)])
            e = tajil_entry(book, int(m.group(1).translate(AR_DIGITS)), re.sub(r'\s+', ' ', whole))
            if e is not None:
                add(e, short_head=False)         # early narrators: no fuzzy merge with later namesakes
        continue
    for k, m in enumerate(heads):
        body = text[m.end():heads[k + 1].start() if k + 1 < len(heads) else len(text)]
        if layout == 'dash':                              # namesakes, kunya table, appendix
            body = re.split(r'\[تمييز\]|\[\*\]|\n\s*\*?\s*[٠-٩]+\s*-\s', body)[0]
        header = ASIDE.sub(' ', m.group(2)).replace(',', '،')     # الدليل المغني: "يعقوب, أبو إسحاق"
        if layout == 'runs':
            header = re.sub(r'^\([^)]*\)\s*:?\s*', '', header)      # book symbols "(خز، طح):"
            header = re.sub(r'\s*\([٠-٩]+\)', '', header)          # footnote marks "(١)"
        header = re.sub(r'\s*\([٠-٩]+\)\s*$', '', header).strip(' .')   # "زحر بن ربيعة (٧٢٧٦)"
        if re.search(r'وهو\s*:', header):                     # "القاضي أبو العلاء وهو: صاعد بن محمد ..."
            header = re.split(r'وهو\s*:', header, maxsplit=1)[1].strip()
        if not header or re.search(r'انظره|انظر', header) or re.match(r'\[|هامش', header):
            continue                                       # redirects and footnote markers
        if re.search(r'\sعن\s', header):
            continue                                       # "الشعبي عن عمه": describes an unnamed narrator
        num = int(m.group(1).translate(AR_DIGITS)) if m.group(1) else k + 1
        if book.get('compiler'):
            compiler_shuyukh[book['compiler']].append(header.split('،')[0])
        if layout == 'isnad':
            shuyukh, talamidh = [], []
            key = norm_tokens(header, 2)
            marker = book['isnad_marker']
            for snippet in re.findall(rf'\* قال {marker}[^\n]*\n([^\n]+)', body):
                segs = [s.strip(' ،.') for s in re.split(rf'(?:^|\s|،)و?{VERB}(?=\s|،|:)', snippet)]
                segs = [s for s in segs if 1 < len(s) < 80]
                for i, s in enumerate(segs):
                    toks = norm_tokens(s, 4)
                    if norm_tokens(s, 2) == key or (len(key) > 1 and key[0] in toks and key[1] in toks):
                        if i > 0:
                            talamidh.append({'name': segs[i - 1], 'symbols': '', 'note': 'isnad'})
                        if i + 1 < len(segs):
                            shuyukh.append({'name': segs[i + 1], 'symbols': '', 'note': 'isnad'})
                        break
            m1 = re.search(r'ذكر من (?:مشايخه|شيوخه)\s*:\s*([^\n]+)', body)
            m2 = re.search(r'(?:من الرواة عنه|ومن الرواة عنه|وعنه)\s*:\s*([^\n]+)', body)
            shuyukh += split_names(m1.group(1)) if m1 else []
            talamidh += split_names(m2.group(1)) if m2 else []
            add(entry(book, num, header, shuyukh, talamidh, None), short_head=True)
        else:
            shuyukh, talamidh = lists(body)
            verdict = VERDICT_RE.findall(body)
            e = entry(book, num, header, shuyukh, talamidh, verdict[-1].strip() if verdict else None)
            aliases = ALIAS_FIRST_RE.findall(body) + ALIAS_RE.findall(body)
            e['aliases'] = [a.strip(' .') for a in aliases if 2 <= len(a.split()) <= 12]
            add(e, short_head=(layout == 'star'))

for source, alias, target in kunya_aliases:
    # The target is one of the same book's entries: match on 3 name tokens, else on 2 when unique.
    in_book = [e for e in entries if source in e['source'].split('+')]
    hits = [e for e in in_book if norm_tokens(e['header']) == norm_tokens(target)]
    if not hits:
        hits = [e for e in in_book if norm_tokens(e['header'], 2) == norm_tokens(target, 2)]
    if len(hits) == 1:
        hits[0]['kunya_aliases'] = hits[0].get('kunya_aliases', []) + [alias]
        stats[f'{source}: kunya alias attached'] += 1
    else:
        stats[f'{source}: kunya alias without a single target'] += 1

for c, names in compiler_shuyukh.items():
    shuyukh = [{'name': n, 'symbols': '', 'note': 'shaykh book'} for n in names]
    talamidh = [{'name': n, 'symbols': '', 'note': 'shaykh book'} for n in compiler_talamidh[c]]
    # A compiler can also be another compiler's shaykh (al-Hakim in al-Bayhaqi's books): give the
    # existing entry the shuyukh instead of creating a second al-Hakim.
    aliases = COMPILER_ALIASES.get(c, [])
    same = by_key.get(norm_tokens(COMPILERS[c], KEY_LEN))
    if same is not None:
        same['shuyukh'] += shuyukh
        same['talamidh'] += talamidh
        same['aliases'] = same.get('aliases', []) + aliases
        same['source'] += '+compiler'
        stats[f'compiler {c}: merged into an existing entry'] += 1
        continue
    entries.append({'kind': 'entry', 'num': 0, 'num_suspect': False, 'symbols': '', 'source': 'compiler',
                    'header': COMPILERS[c], 'name': COMPILERS[c].split('،')[0], 'verdict': 'الإمام الحافظ',
                    'shuyukh': shuyukh, 'talamidh': talamidh, 'quotes': [], 'rawa_lahu': None, 'aliases': aliases})

for k, v in sorted(stats.items()):
    print(f'{k:42} {v}')
print(f'entries: {len(entries)}  with shuyukh: {sum(1 for e in entries if e["shuyukh"])}  '
      f'with talamidh: {sum(1 for e in entries if e["talamidh"])}  with verdict: {sum(1 for e in entries if e["verdict"])}')
print('compiler shuyukh:', {c: len(n) for c, n in compiler_shuyukh.items()})
print('verdicts:', Counter((e['verdict'] or '').split(' ')[0] for e in entries if e['verdict']).most_common(10))
json.dump(entries, open(OUT, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
