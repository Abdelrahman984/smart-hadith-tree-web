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

export default function TreeCanvas({ treeData, narratorsTooltips = {} }: TreeCanvasProps) {
  const [nodes, setNodes, onNodesChange] = useNodesState<Node>([]);
  const [edges, setEdges, onEdgesChange] = useEdgesState<Edge>([]);
  const { openDrawer } = useNarratorDrawerStore();

  useEffect(() => {
    if (!treeData || treeData.nodes.length === 0) return;

    // Convert API nodes to React Flow nodes
    const initialNodes: Node[] = treeData.nodes.map((n) => {
      const tooltip = narratorsTooltips[n.narratorId];
      return {
        id: n.id,
        type: "narrator",
        position: { x: 0, y: 0 },
        data: {
          narratorName: n.knownAs || n.narratorName,
          generationTier: n.generationTier,
          transmissionTerm: n.transmissionTerm,
          gradeSummary: tooltip?.gradeSummary,
        },
      };
    });

    // Create edges (from parentNodeId to child)
    // Note: ParentNodeId is the edge going DOWN the tree (from Sheikh to Student).
    // In our tree, the "root" is at the bottom visually? No, usually Prophet is at top, Compiler is at bottom.
    // Step 1 is the Compiler. Step 7 is Prophet/Companion.
    // So the ParentNodeId actually points to the Student.
    // This means Sheikh is the SOURCE, Student is the TARGET.
    const initialEdges: Edge[] = treeData.nodes
      .filter((n) => n.parentNodeId)
      .map((n) => ({
        id: `e-${n.id}-${n.parentNodeId}`,
        source: n.id, // Sheikh
        target: n.parentNodeId!, // Student
        type: "smoothstep",
        markerEnd: {
          type: MarkerType.ArrowClosed,
          color: "#94a3b8", // slate-400
        },
        style: { stroke: "#cbd5e1", strokeWidth: 2 },
      }));

    // Apply ELK layout
    getLayoutedElements(initialNodes, initialEdges).then(({ nodes: layoutedNodes, edges: layoutedEdges }) => {
      setNodes(layoutedNodes);
      setEdges(layoutedEdges);
    });
  }, [treeData, narratorsTooltips, setNodes, setEdges]);

  const onNodeClick = useCallback((event: React.MouseEvent, node: Node) => {
    // node.id is the transmissionId in this mapping. Wait, we need narratorId to open drawer!
    const isnadNode = treeData.nodes.find((n) => n.id === node.id);
    if (isnadNode) {
      openDrawer(isnadNode.narratorId);
    }
  }, [treeData, openDrawer]);

  return (
    <div className="w-full h-full" dir="ltr">
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
