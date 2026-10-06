import type { Edge, Node } from "@xyflow/react";
import type {
  ComparativeTreeResponseDto,
  IsnadNodeDto,
  IsnadTreeResponseDto,
  NarratorSummaryDto,
} from "@/types/api";
import { buildEdge, type EdgeFacts } from "./edgeStyle";
import { getFamousReferenceOwnerName } from "./formatFamousReferenceName";
import { disambiguateNarratorNames, formatScholarlyNarratorName, formatTwoPartNarratorName } from "./formatNarratorName";
import type { BuiltGraph, NarratorNodeData, ReferenceNodeData } from "./graphTypes";

/**
 * The API returns a flat list of transmissions. Here narrators become nodes and links become edges:
 *   n.id            the transmission's id
 *   n.narratorId    the sheikh's narrator id (the compiler's, for the anchor at step 0)
 *   n.parentNodeId  the parent transmission, whose narrator is the student
 * The functions are pure so the graph can be tested without rendering.
 */

type Tooltips = Record<string, NarratorSummaryDto> | undefined;

/** Narrator card data shared by both trees. */
function narratorData(n: IsnadNodeDto, tooltips: Tooltips): Omit<NarratorNodeData, "narratorName"> {
  return {
    fullName: n.narratorName,
    generationTier: n.generationTier,
    transmissionTerm: n.transmissionTerm,
    gradeSummary: tooltips?.[n.narratorId]?.gradeSummary,
    gradeEn: n.gradeEn,
    isAnomaly: n.isAnomaly,
    anomalyReason: n.anomalyReason,
    travelNote: n.travelNote,
    isMudallis: n.isMudallis,
    hasMukhtalit: n.hasMukhtalit,
    residencePlaces: n.residencePlaces,
    deathPlace: n.deathPlace,
    gawamiRank: n.gawamiRank,
    totalNarrationsCount: n.totalNarrationsCount,
    uniqueHadithCount: n.uniqueHadithCount,
  };
}

/**
 * One edge per sheikh → student pair. The same pair in another hadith merges into the same edge: its books are added
 * (so book focus finds it and a link used by several books is drawn as shared) and a break or wording difference seen
 * on either transmission counts.
 */
function buildEdges<T extends IsnadNodeDto>(nodes: T[], factsOf: (n: T) => EdgeFacts): Edge[] {
  const byId = new Map(nodes.map((n) => [n.id, n]));
  const merged = new Map<string, { source: string; target: string; facts: EdgeFacts }>();

  for (const n of nodes) {
    const parent = n.parentNodeId ? byId.get(n.parentNodeId) : undefined;
    if (!parent) continue;

    const id = `e-${n.narratorId}-${parent.narratorId}`;
    const facts = factsOf(n);
    const existing = merged.get(id);
    if (!existing) {
      merged.set(id, { source: n.narratorId, target: parent.narratorId, facts });
      continue;
    }
    existing.facts = {
      isAnomaly: existing.facts.isAnomaly || facts.isAnomaly,
      hasMatnVariation: existing.facts.hasMatnVariation || facts.hasMatnVariation,
      sourceBooks: Array.from(new Set([...(existing.facts.sourceBooks ?? []), ...(facts.sourceBooks ?? [])])),
    };
  }

  return Array.from(merged, ([id, { source, target, facts }]) => buildEdge(id, source, target, facts));
}

/**
 * The source card of a book's hadith: the book and its number, named after its compiler. It is not a narrator, so it
 * hangs below the first narrator of the chain instead of replacing him.
 */
function referenceNode(id: string, bookName: string, hadithNumber: ReferenceNodeData["hadithNumber"], sourceBooks?: string[]): Node {
  const data: ReferenceNodeData = {
    famousName: getFamousReferenceOwnerName(bookName),
    bookName,
    hadithNumber,
    sourceBooks,
  };
  return { id, type: "reference", position: { x: 0, y: 0 }, data };
}

/** The data of the narrator cards, so names can be adjusted in place. */
function narratorCards(nodes: Map<string, Node>): NarratorNodeData[] {
  return Array.from(nodes.values())
    .filter((n) => n.type === "narrator")
    .map((n) => n.data as NarratorNodeData);
}

/** Tree of one hadith. */
export function buildSingleGraph(tree: IsnadTreeResponseDto, tooltips?: Tooltips): BuiltGraph {
  const nodes = new Map<string, Node>();
  const edges = buildEdges(tree.nodes, (n) => ({ isAnomaly: n.isAnomaly }));

  for (const n of tree.nodes) {
    if (nodes.has(n.narratorId)) continue;
    const data: NarratorNodeData = {
      narratorName: formatTwoPartNarratorName(n.narratorName || n.knownAs),
      ...narratorData(n, tooltips),
    };
    nodes.set(n.narratorId, { id: n.narratorId, type: "narrator", position: { x: 0, y: 0 }, data });

    if (n.stepOrder === 0) {
      const ref = referenceNode(`ref-${tree.hadithId}`, tree.bookName, tree.hadithNumber);
      nodes.set(ref.id, ref);
      edges.push(buildEdge(`e-${n.narratorId}-${ref.id}`, n.narratorId, ref.id, {}));
    }
  }

  disambiguateNarratorNames(narratorCards(nodes));
  return { nodes: Array.from(nodes.values()), edges, bookNames: [tree.bookName] };
}

/** Comparative tree: the chains of several hadiths merged into one graph. */
export function buildComparativeGraph(tree: ComparativeTreeResponseDto, tooltips?: Tooltips): BuiltGraph {
  const sources = tree.sources ?? [];
  const edges = buildEdges(tree.nodes, (n) => ({
    isAnomaly: n.isAnomaly,
    hasMatnVariation: n.hasMatnVariation,
    sourceBooks: n.sourceBooks,
  }));

  const nodes = new Map<string, Node>();
  // One source card per hadith, linked to every first narrator of its chains (a hadith can have several chains).
  const references = new Map<string, { bookName: string; hadithNumber?: string | number; narratorIds: Set<string> }>();

  for (const n of tree.nodes) {
    if (n.stepOrder === 0) {
      const own = sources.filter((s) => n.sourceHadithIds?.includes(s.hadithId));
      const found = own.length > 0
        ? own.map((s) => ({ key: `ref-${s.hadithId}`, bookName: s.bookName, hadithNumber: s.hadithNumber }))
        : (n.sourceBooks ?? []).map((bookName) => ({ key: `ref-${bookName}`, bookName, hadithNumber: undefined }));
      for (const f of found) {
        const ref = references.get(f.key) ?? { bookName: f.bookName, hadithNumber: f.hadithNumber, narratorIds: new Set<string>() };
        ref.narratorIds.add(n.narratorId);
        references.set(f.key, ref);
      }
    }

    const existing = nodes.get(n.narratorId);
    if (existing) {
      // Seen again in another chain: merge what the cards show about it.
      const data = existing.data as NarratorNodeData;
      data.sourceBooks = Array.from(new Set([...(data.sourceBooks ?? []), ...(n.sourceBooks ?? [])]));
      data.travelNote ??= n.travelNote;
      if (n.hasMatnVariation && !data.hasMatnVariation) {
        data.hasMatnVariation = true;
        data.matnVariationSnippet = n.matnVariationSnippet;
      }
      continue;
    }

    const data: NarratorNodeData = {
      narratorName: formatScholarlyNarratorName(n.narratorName, n.knownAs),
      ...narratorData(n, tooltips),
      hasMatnVariation: n.hasMatnVariation,
      matnVariationSnippet: n.matnVariationSnippet,
      sourceBooks: n.sourceBooks ?? [],
    };
    nodes.set(n.narratorId, { id: n.narratorId, type: "narrator", position: { x: 0, y: 0 }, data });
  }

  for (const [id, { bookName, hadithNumber, narratorIds }] of references) {
    nodes.set(id, referenceNode(id, bookName, hadithNumber, [bookName]));
    for (const narratorId of narratorIds) {
      edges.push(buildEdge(`e-${narratorId}-${id}`, narratorId, id, { sourceBooks: [bookName] }));
    }
  }

  disambiguateNarratorNames(narratorCards(nodes));

  return {
    nodes: Array.from(nodes.values()),
    edges,
    bookNames: Array.from(new Set(sources.map((s) => s.bookName))),
  };
}
