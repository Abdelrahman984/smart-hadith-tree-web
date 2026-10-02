"use client";

import { useEffect, useState, useCallback, useMemo } from "react";
import {
  ReactFlow,
  Controls,
  Background,
  useNodesState,
  useEdgesState,
  Node,
  Edge,
  MarkerType,
} from "@xyflow/react";
import { getLayoutedElements } from "../utils/elkLayout";
import { formatTwoPartNarratorName } from "../utils/formatNarratorName";
import ComparativeNarratorNode from "./ComparativeNarratorNode";
import ReferenceNode from "./ReferenceNode";
import { getFamousReferenceOwnerName } from "../utils/formatFamousReferenceName";
import { ComparativeTreeResponseDto, NarratorSummaryDto } from "@/types/api";
import { useNarratorDrawerStore } from "@/features/narrator-details/store/useNarratorDrawerStore";
import GraphControls from './GraphControls';
import BookLegend from './BookLegend';
import { useIlalStore } from '@/features/ilal/store/useIlalStore';
import { getIlalEdgeDecorations } from '@/features/ilal/utils/ilalLabels';
import { getBookMeta } from '@/lib/bookTheme';

const nodeTypes = {
  comparativeNarrator: ComparativeNarratorNode,
  reference: ReferenceNode,
};

const getBookColor = (book: string) => getBookMeta(book).color;

interface ComparativeTreeCanvasProps {
  treeData: ComparativeTreeResponseDto;
  narratorsTooltips?: Record<string, NarratorSummaryDto>;
}

export default function ComparativeTreeCanvas({ treeData, narratorsTooltips }: ComparativeTreeCanvasProps) {
  const [nodes, setNodes, onNodesChange] = useNodesState<Node>([]);
  const [edges, setEdges, onEdgesChange] = useEdgesState<Edge>([]);
  const { openDrawer } = useNarratorDrawerStore();
  const [showWeakOnly, setShowWeakOnly] = useState(false);

  const activeBooks = useMemo(
    () => Array.from(new Set((treeData.sources || []).map((s) => s.bookName))),
    [treeData.sources]
  );

  useEffect(() => {
    if (!treeData || treeData.nodes.length === 0) return;

    const uniqueNarrators = new Map<string, Node>();
    
    treeData.nodes.forEach((n) => {
      if (!uniqueNarrators.has(n.narratorId)) {
        const isReference = n.stepOrder === 0;
        const tooltip = narratorsTooltips?.[n.narratorId];

        if (isReference) {
          const matchedSources = (treeData.sources || []).filter(s => n.sourceHadithIds?.includes(s.hadithId));
          const bookName = matchedSources.map(s => s.bookName).join(' / ') || n.sourceBooks?.join(' / ') || 'المصدر';
          const hadithNumbers = matchedSources.map(s => s.hadithNumber).filter(Boolean);
          const hadithNumberText = hadithNumbers.length > 0 ? hadithNumbers.join(', ') : undefined;
          const famousName = getFamousReferenceOwnerName(n.narratorName, n.knownAs, bookName);

          uniqueNarrators.set(n.narratorId, {
            id: n.narratorId,
            type: "reference",
            position: { x: 0, y: 0 },
            data: {
              famousName,
              fullName: n.narratorName,
              twoPartName: formatTwoPartNarratorName(n.narratorName || n.knownAs),
              bookName,
              hadithNumber: hadithNumberText,
              generationTier: n.generationTier,
              gradeSummary: tooltip?.gradeSummary,
              gradeEn: n.gradeEn,
              sourceBooks: n.sourceBooks || [],
            },
          });
        } else {
          uniqueNarrators.set(n.narratorId, {
            id: n.narratorId,
            type: "comparativeNarrator",
            position: { x: 0, y: 0 },
            data: {
              narratorName: formatTwoPartNarratorName(n.narratorName || n.knownAs),
              fullName: n.narratorName,
              generationTier: n.generationTier,
              transmissionTerm: n.transmissionTerm,
              gradeSummary: tooltip?.gradeSummary,
              gradeEn: n.gradeEn,
              isAnomaly: n.isAnomaly,
              anomalyReason: n.anomalyReason,
              isMudallis: n.isMudallis,
              hasMukhtalit: n.hasMukhtalit,
              residencePlaces: n.residencePlaces,
              deathPlace: n.deathPlace,
              gawamiRank: n.gawamiRank,
              totalNarrationsCount: n.totalNarrationsCount,
              uniqueHadithCount: n.uniqueHadithCount,
              sourceBooks: n.sourceBooks || [],
            },
          });
        }
      } else {
        // Merge source books if seen again
        const existingNode = uniqueNarrators.get(n.narratorId)!;
        const newBooks = n.sourceBooks || [];
        const mergedBooks = Array.from(new Set([...((existingNode.data.sourceBooks as string[]) || []), ...newBooks]));
        existingNode.data.sourceBooks = mergedBooks;
      }
    });

    const initialNodes = Array.from(uniqueNarrators.values());

    const uniqueEdges = new Map<string, Edge>();
    
    treeData.nodes.forEach((n) => {
      if (n.parentNodeId) {
        const parentNode = treeData.nodes.find(p => p.id === n.parentNodeId);
        if (parentNode) {
          const edgeId = `e-${n.narratorId}-${parentNode.narratorId}`;
          if (!uniqueEdges.has(edgeId)) {
            let edgeColor = '#64748b';
            let strokeWidth = 2;
            
            if (n.sourceBooks && n.sourceBooks.length === 1) {
              edgeColor = getBookColor(n.sourceBooks[0]);
            } else if (n.sourceBooks && n.sourceBooks.length > 1) {
              edgeColor = '#334155';
              strokeWidth = 3;
            }

            if (n.isAnomaly) {
               edgeColor = "#ef4444";
               strokeWidth = 3;
            } else if (n.hasMatnVariation) {
               edgeColor = "#f59e0b"; // Amber for variation
               strokeWidth = 3;
            }

            const isAnomaly = n.isAnomaly;
            const hasVariation = n.hasMatnVariation;
            const labelText = isAnomaly ? "انقطاع" : hasVariation ? "اختلاف باللفظ" : undefined;
            const labelBg = isAnomaly 
              ? { fill: "#fef2f2", stroke: "#fca5a5", strokeWidth: 1, rx: 4, ry: 4 }
              : hasVariation 
              ? { fill: "#fffbeb", stroke: "#fcd34d", strokeWidth: 1, rx: 4, ry: 4 }
              : undefined;

            uniqueEdges.set(edgeId, {
              id: edgeId,
              source: n.narratorId, // Sheikh
              target: parentNode.narratorId, // Student
              type: "bezier",
              markerEnd: {
                type: MarkerType.ArrowClosed,
                width: 20,
                height: 20,
                color: edgeColor,
              },
              style: { 
                stroke: edgeColor, 
                strokeWidth: strokeWidth,
                strokeDasharray: isAnomaly || hasVariation ? "5 5" : undefined 
              },
              animated: isAnomaly || hasVariation,
              label: labelText,
              labelStyle: { fill: edgeColor, fontWeight: "bold", fontSize: 11 },
              labelBgStyle: labelBg,
              labelBgPadding: [4, 8],
            });
          } else {
             // If edge exists (which shouldn't usually happen with same nodes unless duplicate transmissions), we could merge properties if needed
          }
        }
      }
    });

    const initialEdges = Array.from(uniqueEdges.values());

    // Apply ELK layout
    getLayoutedElements(initialNodes, initialEdges).then(({ nodes: layoutedNodes, edges: layoutedEdges }) => {
      setNodes(layoutedNodes);
      setEdges(layoutedEdges);
    });
  }, [treeData, narratorsTooltips, setNodes, setEdges]);

  const onNodeClick = useCallback((event: React.MouseEvent, node: Node) => {
    openDrawer(node.id);
  }, [openDrawer]);

  useEffect(() => {
    setNodes((nds) => 
      nds.map(node => ({
        ...node,
        data: {
          ...node.data,
          showWeakOnly
        }
      }))
    );
  }, [showWeakOnly, setNodes]);


  // Overlay isnad-link ilal (tadlis, unproven meeting, ikhtilat) on the laid-out edges.
  const ilalReport = useIlalStore((s) => s.report);
  const decoratedEdges = useMemo(() => {
    const decorations = getIlalEdgeDecorations(ilalReport);
    if (decorations.size === 0) return edges;
    return edges.map((edge) => {
      const d = decorations.get(edge.id);
      if (!d) return edge;
      return {
        ...edge,
        animated: true,
        label: edge.label ? `${edge.label} · ${d.label}` : d.label,
        style: { ...edge.style, stroke: d.color, strokeWidth: 3, strokeDasharray: d.dash },
        markerEnd: { type: MarkerType.ArrowClosed, width: 20, height: 20, color: d.color },
        labelStyle: { fill: d.color, fontWeight: "bold", fontSize: 11 },
        labelBgStyle: { fill: "#fff7ed", stroke: d.color, strokeWidth: 1, rx: 4, ry: 4 },
        labelBgPadding: [4, 8] as [number, number],
      };
    });
  }, [edges, ilalReport]);

  return (
    <div className="absolute inset-0 bg-slate-50" dir="ltr">
      <ReactFlow
        nodes={nodes}
        edges={decoratedEdges}
        onNodesChange={onNodesChange}
        onEdgesChange={onEdgesChange}
        onNodeClick={onNodeClick}
        nodeTypes={nodeTypes}
        fitView
        attributionPosition="bottom-right"
        className="bg-slate-50"
      >
        <GraphControls showWeakOnly={showWeakOnly} setShowWeakOnly={setShowWeakOnly} />
        <BookLegend activeBooks={activeBooks} />
        <Background color="#cbd5e1" gap={16} />
        <Controls />
      </ReactFlow>
    </div>
  );
}
