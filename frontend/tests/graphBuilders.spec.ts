import { test, expect } from '@playwright/test';
import { buildComparativeGraph, buildSingleGraph } from '../src/features/isnad-tree/utils/buildIsnadGraph';
import { decorateEdgeWithIlal } from '../src/features/isnad-tree/utils/edgeStyle';
import type { ComparativeIsnadNodeDto, ComparativeTreeResponseDto, IsnadNodeDto } from '../src/types/api';

const node = (over: Partial<IsnadNodeDto> & Pick<IsnadNodeDto, 'id' | 'narratorId' | 'stepOrder' | 'parentNodeId'>): IsnadNodeDto => ({
  narratorName: `name-${over.narratorId}`,
  knownAs: null,
  generationTier: null,
  transmissionTerm: null,
  ...over,
});

const cnode = (
  over: Partial<ComparativeIsnadNodeDto> & Pick<ComparativeIsnadNodeDto, 'id' | 'narratorId' | 'stepOrder' | 'parentNodeId'>
): ComparativeIsnadNodeDto => ({ ...node(over), sourceHadithIds: [], sourceBooks: [], ...over });

// Compiler C <- N1 <- N2 (N2 is the sheikh of N1, who is the sheikh of C).
const chain: IsnadNodeDto[] = [
  node({ id: 't0', narratorId: 'C', stepOrder: 0, parentNodeId: null }),
  node({ id: 't1', narratorId: 'N1', stepOrder: 1, parentNodeId: 't0', transmissionTerm: 'حدثنا' }),
  node({ id: 't2', narratorId: 'N2', stepOrder: 2, parentNodeId: 't1', isAnomaly: true, anomalyReason: 'x' }),
];

test.describe('buildSingleGraph', () => {
  const graph = buildSingleGraph({ hadithId: 'h', bookName: 'صحيح البخاري', hadithNumber: 1, matnArabic: '', nodes: chain });

  test('step 0 is the compiler card, everyone else a narrator card', () => {
    expect(graph.nodes.map((n) => [n.id, n.type])).toEqual([
      ['C', 'reference'],
      ['N1', 'narrator'],
      ['N2', 'narrator'],
    ]);
    expect(graph.bookNames).toEqual(['صحيح البخاري']);
  });

  test('edges run from the sheikh to the student', () => {
    expect(graph.edges.map((e) => [e.source, e.target])).toEqual([
      ['N1', 'C'],
      ['N2', 'N1'],
    ]);
  });

  test('a broken link is red, dashed and labelled; a normal one is grey', () => {
    const [normal, broken] = graph.edges;
    expect(normal.style?.stroke).toBe('#64748b');
    expect(normal.label).toBeUndefined();
    expect(broken.style?.stroke).toBe('#ef4444');
    expect(broken.style?.strokeDasharray).toBe('5 5');
    expect(broken.label).toBe('انقطاع');
  });

  test('a transmission whose parent is missing makes no edge', () => {
    const g = buildSingleGraph({
      hadithId: 'h', bookName: 'b', hadithNumber: 1, matnArabic: '',
      nodes: [node({ id: 'a', narratorId: 'A', stepOrder: 1, parentNodeId: 'gone' })],
    });
    expect(g.edges).toHaveLength(0);
    expect(g.nodes).toHaveLength(1);
  });
});

test.describe('buildComparativeGraph', () => {
  const sources = [
    { hadithId: 'h1', bookName: 'صحيح البخاري', hadithNumber: 1, matnSnippet: '' },
    { hadithId: 'h2', bookName: 'صحيح مسلم', hadithNumber: 2, matnSnippet: '' },
  ];
  const build = (nodes: ComparativeIsnadNodeDto[]) =>
    buildComparativeGraph({ sources, nodes } as ComparativeTreeResponseDto);

  test('the same narrator pair in two hadiths is one edge with both books', () => {
    const g = build([
      cnode({ id: 'a0', narratorId: 'C', stepOrder: 0, parentNodeId: null, sourceHadithIds: ['h1'], sourceBooks: ['صحيح البخاري'] }),
      cnode({ id: 'a1', narratorId: 'N1', stepOrder: 1, parentNodeId: 'a0', sourceBooks: ['صحيح البخاري'] }),
      cnode({ id: 'b0', narratorId: 'C', stepOrder: 0, parentNodeId: null, sourceHadithIds: ['h2'], sourceBooks: ['صحيح مسلم'] }),
      cnode({ id: 'b1', narratorId: 'N1', stepOrder: 1, parentNodeId: 'b0', sourceBooks: ['صحيح مسلم'] }),
    ]);
    expect(g.nodes).toHaveLength(2);
    expect(g.edges).toHaveLength(1);
    expect(g.edges[0].data?.books).toEqual(['صحيح البخاري', 'صحيح مسلم']);
    expect(g.bookNames).toEqual(['صحيح البخاري', 'صحيح مسلم']);
    // Shared by two books: dark and thick.
    expect(g.edges[0].style?.stroke).toBe('#334155');
    expect(g.edges[0].style?.strokeWidth).toBe(3);
    expect((g.nodes.find((n) => n.id === 'N1')!.data as { sourceBooks: string[] }).sourceBooks).toEqual([
      'صحيح البخاري',
      'صحيح مسلم',
    ]);
  });

  test('a link used by one book takes that book\'s colour', () => {
    const g = build([
      cnode({ id: 'a0', narratorId: 'C', stepOrder: 0, parentNodeId: null }),
      cnode({ id: 'a1', narratorId: 'N1', stepOrder: 1, parentNodeId: 'a0', sourceBooks: ['صحيح البخاري'] }),
    ]);
    expect(g.edges[0].style?.stroke).toBe('#2563eb');
  });

  test('rule order: broken beats wording difference beats book colour', () => {
    const base = { stepOrder: 1, sourceBooks: ['صحيح البخاري'] };
    const g = build([
      cnode({ id: 'r', narratorId: 'C', stepOrder: 0, parentNodeId: null }),
      cnode({ id: 'x', narratorId: 'X', parentNodeId: 'r', ...base, isAnomaly: true, hasMatnVariation: true }),
      cnode({ id: 'y', narratorId: 'Y', parentNodeId: 'r', ...base, hasMatnVariation: true }),
    ]);
    const x = g.edges.find((e) => e.source === 'X')!;
    const y = g.edges.find((e) => e.source === 'Y')!;
    expect([x.style?.stroke, x.label]).toEqual(['#ef4444', 'انقطاع']);
    expect([y.style?.stroke, y.label]).toEqual(['#f59e0b', 'اختلاف باللفظ']);
  });

  test('a compiler who is also an intermediate sheikh stays a compiler card', () => {
    const g = build([
      cnode({ id: 'a0', narratorId: 'C', stepOrder: 0, parentNodeId: null, sourceHadithIds: ['h1'], sourceBooks: ['صحيح البخاري'] }),
      cnode({ id: 'b0', narratorId: 'D', stepOrder: 0, parentNodeId: null, sourceHadithIds: ['h2'], sourceBooks: ['صحيح مسلم'] }),
      cnode({ id: 'b1', narratorId: 'C', stepOrder: 1, parentNodeId: 'b0', sourceBooks: ['صحيح مسلم'] }),
    ]);
    expect(g.nodes.find((n) => n.id === 'C')!.type).toBe('reference');
    expect(g.edges.map((e) => [e.source, e.target])).toEqual([['C', 'D']]);
  });

  test('variation and travel hints seen on a later transmission are kept on the card', () => {
    const g = build([
      cnode({ id: 'r', narratorId: 'C', stepOrder: 0, parentNodeId: null }),
      cnode({ id: 'a', narratorId: 'N', stepOrder: 1, parentNodeId: 'r' }),
      cnode({ id: 'b', narratorId: 'N', stepOrder: 1, parentNodeId: 'r', hasMatnVariation: true, matnVariationSnippet: 's', travelNote: 't' }),
    ]);
    const data = g.nodes.find((n) => n.id === 'N')!.data as { hasMatnVariation?: boolean; matnVariationSnippet?: string; travelNote?: string };
    expect([data.hasMatnVariation, data.matnVariationSnippet, data.travelNote]).toEqual([true, 's', 't']);
  });
});

test.describe('decorateEdgeWithIlal', () => {
  const deco = { label: 'عنعنة مدلس', color: '#ea580c', dash: '2 4' };
  const single = buildSingleGraph({ hadithId: 'h', bookName: 'b', hadithNumber: 1, matnArabic: '', nodes: chain });

  test('an ordinary edge takes the finding\'s colour, dash and label', () => {
    const out = decorateEdgeWithIlal(single.edges[0], deco);
    expect(out.style?.stroke).toBe('#ea580c');
    expect(out.style?.strokeDasharray).toBe('2 4');
    expect(out.label).toBe('عنعنة مدلس');
    expect(out.animated).toBe(true);
  });

  test('a broken edge stays red and shows both labels', () => {
    const out = decorateEdgeWithIlal(single.edges[1], deco);
    expect(out.style?.stroke).toBe('#ef4444');
    expect(out.label).toBe('انقطاع · عنعنة مدلس');
  });
});
