"use client";

import { useEffect, useState, useCallback } from "react";
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

const nodeTypes = {
  comparativeNarrator: ComparativeNarratorNode,
  reference: ReferenceNode,
};

const getBookColor = (book: string) => {
  switch(book) {
    case 'صحيح البخاري': return '#2563eb';
    case 'صحيح مسلم': return '#16a34a';
    case 'سنن أبي داود': return '#d97706';
    case 'جامع الترمذي': return '#9333ea';
    case 'سنن النسائي': return '#0284c7';
    case 'سنن ابن ماجه': return '#e11d48';
    case 'مسند أحمد': return '#b45309';
    case 'موطأ مالك': return '#0d9488';
    default: return '#64748b';
  }
};

interface ComparativeTreeCanvasProps {
  treeData: ComparativeTreeResponseDto;
  narratorsTooltips?: Record<string, NarratorSummaryDto>;
}

export default function ComparativeTreeCanvas({ treeData, narratorsTooltips }: ComparativeTreeCanvasProps) {
  const [nodes, setNodes, onNodesChange] = useNodesState<Node>([]);
  const [edges, setEdges, onEdgesChange] = useEdgesState<Edge>([]);
  const { openDrawer } = useNarratorDrawerStore();
  const [showWeakOnly, setShowWeakOnly] = useState(false);

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

            uniqueEdges.set(edgeId, {
              id: edgeId,
              source: n.narratorId, // Sheikh
              target: parentNode.narratorId, // Student
              type: "smoothstep",
              markerEnd: {
                type: MarkerType.ArrowClosed,
                width: 20,
                height: 20,
                color: edgeColor,
              },
              style: { 
                stroke: edgeColor, 
                strokeWidth: strokeWidth,
                strokeDasharray: n.isAnomaly || n.hasMatnVariation ? "5 5" : undefined 
              },
              animated: n.isAnomaly || n.hasMatnVariation,
              label: n.isAnomaly ? "انقطاع" : n.hasMatnVariation ? "اختلاف باللفظ" : undefined,
              labelStyle: { fill: edgeColor, fontWeight: "bold" },
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

  return (
    <div className="absolute inset-0 bg-slate-50" dir="ltr">
      <ReactFlow
        nodes={nodes}
        edges={edges}
        onNodesChange={onNodesChange}
        onEdgesChange={onEdgesChange}
        onNodeClick={onNodeClick}
        nodeTypes={nodeTypes}
        fitView
        attributionPosition="bottom-right"
        className="bg-slate-50"
      >
        <GraphControls showWeakOnly={showWeakOnly} setShowWeakOnly={setShowWeakOnly} />
        <BookLegend />
        <Background color="#cbd5e1" gap={16} />
        <Controls />
      </ReactFlow>
    </div>
  );
}
