"use client";

import { useEffect, useState, useMemo, useCallback } from "react";
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
import NarratorNode from "./NarratorNode";
import { IsnadTreeResponseDto, NarratorSummaryDto } from "@/types/api";
import { useNarratorDrawerStore } from "@/features/narrator-details/store/useNarratorDrawerStore";

const nodeTypes = {
  narrator: NarratorNode,
};

interface TreeCanvasProps {
  treeData: IsnadTreeResponseDto;
  narratorsTooltips?: Record<string, NarratorSummaryDto>;
}

export default function TreeCanvas({ treeData, narratorsTooltips }: TreeCanvasProps) {
  const [nodes, setNodes, onNodesChange] = useNodesState<Node>([]);
  const [edges, setEdges, onEdgesChange] = useEdgesState<Edge>([]);
  const { openDrawer } = useNarratorDrawerStore();

  useEffect(() => {
    if (!treeData || treeData.nodes.length === 0) return;

    // We want Narrators to be the Nodes, and Transmissions to be the Edges.
    // The API returns a flat list of Transmissions (IsnadNodeDto), where:
    // n.id = Transmission.Id
    // n.narratorId = Sheikh's NarratorId (or Compiler's NarratorId for anchor)
    // n.parentNodeId = Parent Transmission.Id (to find the Student)
    
    // 1. Deduplicate Narrators to create React Flow Nodes
    const uniqueNarrators = new Map<string, Node>();
    
    treeData.nodes.forEach((n) => {
      if (!uniqueNarrators.has(n.narratorId)) {
        const tooltip = narratorsTooltips?.[n.narratorId];
        uniqueNarrators.set(n.narratorId, {
          id: n.narratorId,
          type: "narrator",
          position: { x: 0, y: 0 },
          data: {
            narratorName: n.knownAs || n.narratorName,
            generationTier: n.generationTier,
            transmissionTerm: n.transmissionTerm,
            gradeSummary: tooltip?.gradeSummary,
          },
        });
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
            uniqueEdges.set(edgeId, {
              id: edgeId,
              source: n.narratorId, // Sheikh
              target: parentNode.narratorId, // Student
              type: "smoothstep",
              markerEnd: {
                type: MarkerType.ArrowClosed,
                color: "#94a3b8",
              },
              style: { stroke: "#cbd5e1", strokeWidth: 2 },
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
    });
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [treeData?.hadithId, narratorsTooltips, setNodes, setEdges]);

  const onNodeClick = useCallback((event: React.MouseEvent, node: Node) => {
    // node.id is now the narratorId!
    openDrawer(node.id);
  }, [openDrawer]);

  return (
    <div className="absolute inset-0" dir="ltr">
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
        <Background color="#cbd5e1" gap={16} />
        <Controls />
      </ReactFlow>
    </div>
  );
}
