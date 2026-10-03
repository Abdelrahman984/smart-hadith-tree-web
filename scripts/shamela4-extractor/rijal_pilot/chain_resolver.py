"""Resolve a whole isnad jointly instead of link by link.

Link-by-link resolution loses all context once one narrator is unknown ("عمرو بن علي ←
أبو عاصم ← ابن جريج" all fail when the first name is ambiguous). Here every name gets its
candidate set, and a Viterbi pass picks the assignment that maximises teacher/student
consistency along the chain:

    edge(student, shaykh) = 2 if each lists the other, 1 if only one does, 0 otherwise

A choice is accepted only when it beats every alternative at its position given its
chosen neighbours (strictly higher local score), so coverage does not come at the cost of
guessing. Loaded with exec() by compare_current.py / gap_test-style scripts after
gap_test.py's helpers (chain_segments, is_name, lookup, shuyukh_of, compiler).
"""
import math

MAX_CANDIDATES = 80

# Students as resolved from each entry's talamidh list (the mirror of shuyukh_of).
talamidh_of = []
for _i, _e in enumerate(entries):
    _s = set()
    for _it in _e['talamidh']:
        _s |= set(symbol_filter(set(candidates(_it['name'])) - {_i}, _it['symbols']))
    talamidh_of.append(_s)


def edge(student: int | None, shaykh: int | None) -> int:
    if student is None or shaykh is None:
        return 0
    return (shaykh in shuyukh_of[student]) + (student in talamidh_of[shaykh])


def prior(j: int) -> float:
    return 0.01 * math.log1p(fame[j])        # tie-breaker only; never outweighs one edge


def father_of(p: int) -> set[int]:
    """Entries that can be p's father ("عن أبيه"): p's shaykh whose ism is p's father's name."""
    father = nasab[p][1:2]
    return {j for j in shuyukh_of[p] if ism[j] == father} if father else set()


def resolve(segs: list[str], start: int | None) -> list[tuple[str, int | None, str]]:
    # Candidate sets; "أبيه" depends on the previous position's candidates.
    cands: list[list[int]] = []
    for i, seg in enumerate(segs):
        if seg.strip() in ('أبيه', 'ابيه'):
            prev_c = cands[-1] if cands else ([start] if start is not None else [])
            c = set().union(*(father_of(p) for p in prev_c)) if prev_c else set()
        else:
            c = set(lookup(seg, None))
        c.discard(start)
        ranked = sorted(c, key=lambda j: -fame[j])[:MAX_CANDIDATES]
        cands.append(ranked)

    # Viterbi over states = candidates + None ("unknown here"; breaks the chain's edges).
    layers = [[(start, 0.0, -1)]]                   # (entry, score, back-pointer)
    for c in cands:
        layer = []
        prev_layer = layers[-1]
        for j in c + [None]:
            best, back = -1e9, -1
            for k, (p, sc, _) in enumerate(prev_layer):
                s = sc + edge(p, j)
                if s > best:
                    best, back = s, k
            layer.append((j, best + (prior(j) if j is not None else 0.0), back))
        layers.append(layer)

    # Back-track.
    path, k = [], max(range(len(layers[-1])), key=lambda x: layers[-1][x][1])
    for layer in reversed(layers[1:]):
        j, _, back = layer[k]
        path.append(j)
        k = back
    path.reverse()

    out = []
    for i, (seg, j) in enumerate(zip(segs, path)):
        c = cands[i]
        if not c:
            out.append((seg, None, 'missing'))
            continue
        left = path[i - 1] if i > 0 else start
        right = path[i + 1] if i + 1 < len(path) else None

        def local(x: int) -> int:
            return edge(left, x) + edge(x, right)

        if len(c) == 1:
            out.append((seg, c[0], 'unique'))
        elif j is not None and local(j) > 0 and all(local(x) < local(j) for x in c if x != j):
            out.append((seg, j, 'chain'))
        else:
            out.append((seg, None, 'ambiguous'))
    return out
