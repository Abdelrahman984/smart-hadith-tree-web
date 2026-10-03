"use client";

import { useEffect, useState, useCallback, useMemo, useRef } from "react";
import {
  ReactFlow,
  Controls,
  Background,
  useNodesState,
  useEdgesState,
  Node,
  Edge,
  MarkerType,
  ReactFlowInstance,
} from "@xyflow/react";
import { getLayoutedElements } from "../utils/elkLayout";
import { applyGraphFocus } from "../utils/graphFocus";
import { useMeasuredRelayout } from "../hooks/useMeasuredRelayout";
import { formatTwoPartNarratorName } from "../utils/formatNarratorName";
import NarratorNode from "./NarratorNode";
import ReferenceNode from "./ReferenceNode";
import ElkEdge from "./ElkEdge";
import { getFamousReferenceOwnerName } from "../utils/formatFamousReferenceName";
import { IsnadTreeResponseDto, NarratorSummaryDto } from "@/types/api";
import { useNarratorDrawerStore } from "@/features/narrator-details/store/useNarratorDrawerStore";
import GraphControls from './GraphControls';
import BookLegend from './BookLegend';
import { useIlalStore } from '@/features/ilal/store/useIlalStore';
import { getIlalEdgeDecorations } from '@/features/ilal/utils/ilalLabels';

const edgeTypes = { elk: ElkEdge };

const nodeTypes = {
  narrator: NarratorNode,
  reference: ReferenceNode,
};

interface TreeCanvasProps {
  treeData: IsnadTreeResponseDto;
  narratorsTooltips?: Record<string, NarratorSummaryDto>;
}

export default function TreeCanvas({ treeData, narratorsTooltips }: TreeCanvasProps) {
  const [nodes, setNodes, onNodesChange] = useNodesState<Node>([]);
  const [edges, setEdges, onEdgesChange] = useEdgesState<Edge>([]);
  const { openDrawer } = useNarratorDrawerStore();
  const [showWeakOnly, setShowWeakOnly] = useState(false);
  const [hoveredNodeId, setHoveredNodeId] = useState<string | null>(null);
  const flowRef = useRef<ReactFlowInstance | null>(null);
  const refit = useCallback(() => flowRef.current?.fitView({ duration: 0 }), []);
  const { phase, setPhase } = useMeasuredRelayout({ nodes, edges, setNodes, setEdges, onRelaid: refit });

  useEffect(() => {
    if (!treeData || treeData.nodes.length === 0) return;
    setPhase("idle");

    // We want Narrators to be the Nodes, and Transmissions to be the Edges.
    // The API returns a flat list of Transmissions (IsnadNodeDto), where:
    // n.id = Transmission.Id
    // n.narratorId = Sheikh's NarratorId (or Compiler's NarratorId for anchor)
    // n.parentNodeId = Parent Transmission.Id (to find the Student)
    
    // 1. Deduplicate Narrators to create React Flow Nodes
    const uniqueNarrators = new Map<string, Node>();
    
    treeData.nodes.forEach((n) => {
      if (!uniqueNarrators.has(n.narratorId)) {
        const isReference = n.stepOrder === 0;
        const tooltip = narratorsTooltips?.[n.narratorId];

        if (isReference) {
          uniqueNarrators.set(n.narratorId, {
            id: n.narratorId,
            type: "reference",
            position: { x: 0, y: 0 },
            data: {
              famousName: getFamousReferenceOwnerName(n.narratorName, n.knownAs, treeData.bookName),
              fullName: n.narratorName,
              twoPartName: formatTwoPartNarratorName(n.narratorName || n.knownAs),
              bookName: treeData.bookName,
              hadithNumber: treeData.hadithNumber,
              generationTier: n.generationTier,
              gradeSummary: tooltip?.gradeSummary,
              gradeEn: n.gradeEn,
            },
          });
        } else {
          uniqueNarrators.set(n.narratorId, {
            id: n.narratorId,
            type: "narrator",
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
            },
          });
        }
      }
    });

    const initialNodes = Array.from(uniqueNarrators.values());

    // 2. Create Edges
    // We need to link Sheikh (source) to Student (target).
    // In our CTE, a node 'n' represents a Transmission from n.narratorId (Sheikh) to its parent's narrator.
    // To find the Student, we look at the node whose n.id == n.parentNodeId.
    const uniqueEdges = new Map<string, Edge>();
    
    treeData.nodes.forEach((n) => {
      if (n.parentNodeId) {
        const parentNode = treeData.nodes.find(p => p.id === n.parentNodeId);
        if (parentNode) {
          const edgeId = `e-${n.narratorId}-${parentNode.narratorId}`;
          if (!uniqueEdges.has(edgeId)) {
            const edgeColor = n.isAnomaly ? "#ef4444" : "#64748b";
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
                strokeWidth: n.isAnomaly ? 3 : 2,
                strokeDasharray: n.isAnomaly ? "5 5" : undefined 
              },
              animated: n.isAnomaly,
              label: n.isAnomaly ? "انقطاع" : undefined,
              labelStyle: { fill: "#ef4444", fontWeight: "bold", fontSize: 11 },
              labelBgStyle: { fill: "#fef2f2", stroke: "#fca5a5", strokeWidth: 1, rx: 4, ry: 4 },
              labelBgPadding: [4, 8],
            });
          }
        }
      }
    });

    const initialEdges = Array.from(uniqueEdges.values());

    // Apply ELK layout
    getLayoutedElements(initialNodes, initialEdges).then(({ nodes: layoutedNodes, edges: layoutedEdges }) => {
      setNodes(layoutedNodes);
      setEdges(layoutedEdges);
      setPhase("measure");
    });
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [treeData?.hadithId, narratorsTooltips, setNodes, setEdges, setPhase]);

  const onNodeClick = useCallback((event: React.MouseEvent, node: Node) => {
    // node.id is now the narratorId!
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

  // Dim everything except the hovered narrator's chain.
  const focused = useMemo(
    () => applyGraphFocus(nodes, decoratedEdges, { nodeId: hoveredNodeId }),
    [nodes, decoratedEdges, hoveredNodeId]
  );

  return (
    <div
      className={`absolute inset-0 bg-slate-50 transition-opacity duration-150 ${phase === "ready" ? "opacity-100" : "opacity-0"}`}
      dir="ltr"
    >
      <ReactFlow
        nodes={focused.nodes}
        edges={focused.edges}
        onNodesChange={onNodesChange}
        onEdgesChange={onEdgesChange}
        onNodeClick={onNodeClick}
        onNodeMouseEnter={(_, node) => setHoveredNodeId(node.id)}
        onNodeMouseLeave={() => setHoveredNodeId(null)}
        minZoom={0.1}
        nodeTypes={nodeTypes}
        edgeTypes={edgeTypes}
        onInit={(instance) => {
          flowRef.current = instance;
        }}
        fitView
        attributionPosition="bottom-right"
        className="bg-slate-50"
      >
        <GraphControls showWeakOnly={showWeakOnly} setShowWeakOnly={setShowWeakOnly} />
        <BookLegend activeBooks={[treeData.bookName]} />
        <Background color="#cbd5e1" gap={16} />
        <Controls />
      </ReactFlow>
    </div>
  );
}
