"use client";

import { useCallback, useEffect, useMemo, useRef } from "react";
import {
  Background,
  Controls,
  Node,
  Panel,
  ReactFlow,
  ReactFlowInstance,
  useEdgesState,
  useNodesState,
  Edge,
} from "@xyflow/react";
import { X } from "lucide-react";
import { useNarratorDrawerStore } from "@/features/narrator-details/store/useNarratorDrawerStore";
import { useIlalStore } from "@/features/ilal/store/useIlalStore";
import { getIlalEdgeDecorations } from "@/features/ilal/utils/ilalLabels";
import { useMeasuredRelayout } from "../hooks/useMeasuredRelayout";
import { useRefitOnResize } from "../hooks/useRefitOnResize";
import { useGraphViewStore } from "../store/useGraphViewStore";
import { decorateEdgeWithIlal } from "../utils/edgeStyle";
import { getLayoutedElements } from "../utils/elkLayout";
import { applyGraphFocus } from "../utils/graphFocus";
import type { BuiltGraph } from "../utils/graphTypes";
import BookLegend from "./BookLegend";
import ElkEdge from "./ElkEdge";
import GraphControls from "./GraphControls";
import NarratorNode from "./NarratorNode";
import ReferenceNode from "./ReferenceNode";

const edgeTypes = { elk: ElkEdge };
const nodeTypes = { narrator: NarratorNode, reference: ReferenceNode };

interface IsnadGraphCanvasProps {
  /** Memoize it: the canvas lays out again whenever this object changes. */
  graph: BuiltGraph;
  /** Lets the legend highlight one book's routes (comparative tree). */
  bookFocus?: boolean;
}

/**
 * The isnad graph, for one hadith or several: two-pass ELK layout, Ilal overlay on the edges, chain / book focus,
 * legend and controls. The page decides what to draw by building the `graph`.
 */
export default function IsnadGraphCanvas({ graph, bookFocus = false }: IsnadGraphCanvasProps) {
  const [nodes, setNodes, onNodesChange] = useNodesState<Node>([]);
  const [edges, setEdges, onEdgesChange] = useEdgesState<Edge>([]);
  const openDrawer = useNarratorDrawerStore((s) => s.openDrawer);

  const hoveredNodeId = useGraphViewStore((s) => s.hoveredNodeId);
  const focusBook = useGraphViewStore((s) => s.focusBook);
  const setHoveredNodeId = useGraphViewStore((s) => s.setHoveredNodeId);
  const setFocusBook = useGraphViewStore((s) => s.setFocusBook);
  const resetView = useGraphViewStore((s) => s.reset);
  useEffect(() => resetView, [resetView]);

  const flowRef = useRef<ReactFlowInstance | null>(null);
  const containerRef = useRef<HTMLDivElement>(null);
  const refit = useCallback(() => flowRef.current?.fitView({ duration: 0, padding: 0.1 }), []);
  const { phase, setPhase } = useMeasuredRelayout({ nodes, edges, setNodes, setEdges, onRelaid: refit });
  useRefitOnResize(containerRef, refit, phase === "ready");

  // First layout pass, with estimated sizes; useMeasuredRelayout does the second one with the measured sizes.
  useEffect(() => {
    if (graph.nodes.length === 0) return;
    let cancelled = false;
    setPhase("idle");
    getLayoutedElements(graph.nodes, graph.edges).then(({ nodes: laidOut, edges: laidOutEdges }) => {
      if (cancelled) return;
      setNodes(laidOut);
      setEdges(laidOutEdges);
      setPhase("measure");
    });
    return () => {
      cancelled = true;
    };
  }, [graph, setNodes, setEdges, setPhase]);

  const onNodeClick = useCallback(
    (_: React.MouseEvent, node: Node) => {
      const data = node.data as { anomalyReason?: string | null; travelNote?: string | null };
      openDrawer(node.id, { anomalyReason: data.anomalyReason, travelNote: data.travelNote });
    },
    [openDrawer]
  );

  // Overlay isnad-link ilal (tadlis, unproven meeting, ikhtilat) on the laid-out edges.
  const ilalReport = useIlalStore((s) => s.report);
  const decoratedEdges = useMemo(() => {
    const decorations = getIlalEdgeDecorations(ilalReport);
    if (decorations.size === 0) return edges;
    return edges.map((edge) => {
      const decoration = decorations.get(edge.id);
      return decoration ? decorateEdgeWithIlal(edge, decoration) : edge;
    });
  }, [edges, ilalReport]);

  // Escape clears the book focus.
  const activeBook = bookFocus ? focusBook : null;
  useEffect(() => {
    if (!activeBook) return;
    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === "Escape") setFocusBook(null);
    };
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [activeBook, setFocusBook]);

  // Dim everything except the hovered narrator's chain or the focused book.
  const focused = useMemo(
    () => applyGraphFocus(nodes, decoratedEdges, { nodeId: hoveredNodeId, book: activeBook }),
    [nodes, decoratedEdges, hoveredNodeId, activeBook]
  );

  return (
    <div
      ref={containerRef}
      className={`absolute inset-0 bg-surface-muted transition-opacity duration-150 ${phase === "ready" ? "opacity-100" : "opacity-0"}`}
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
        className="bg-surface-muted"
      >
        <GraphControls />
        {activeBook && (
          <Panel position="top-center" className="m-2" dir="rtl">
            <button
              type="button"
              onClick={() => setFocusBook(null)}
              title="إلغاء الإبراز (Esc)"
              className="flex items-center gap-2 bg-surface/95 backdrop-blur-md px-3 py-1.5 rounded-full shadow-md border border-slate-300 text-xs font-bold text-slate-700 hover:bg-surface-muted cursor-pointer"
            >
              <span>إبراز: {activeBook}</span>
              <X className="w-3.5 h-3.5 text-ink-subtle" />
            </button>
          </Panel>
        )}
        <BookLegend
          activeBooks={graph.bookNames}
          focusBook={activeBook}
          onFocusBook={bookFocus ? setFocusBook : undefined}
        />
        <Background color="#cbd5e1" gap={16} />
        <Controls />
      </ReactFlow>
    </div>
  );
}
