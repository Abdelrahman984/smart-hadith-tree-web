import { create } from "zustand";

/** What the viewer is looking at on the graph; kept out of the node data so changing it does not rebuild the graph. */
interface GraphViewState {
  /** «إبراز الضعفاء»: reliable narrators are dimmed. */
  showWeakOnly: boolean;
  /** Narrator under the pointer: its whole chain is highlighted. */
  hoveredNodeId: string | null;
  /** Canonical name of the book whose routes are highlighted (comparative tree). */
  focusBook: string | null;
  /** The legend panel: null follows the screen (open from 768px up, where it sits beside the graph). */
  legendOpen: boolean | null;
  /**
   * Ask the canvas to bring narrators into view (and select the first). `seq` changes on every request so asking for the
   * same narrator twice still acts.
   */
  reveal: { ids: string[]; select: boolean; seq: number } | null;
  setShowWeakOnly: (value: boolean) => void;
  setHoveredNodeId: (id: string | null) => void;
  setFocusBook: (book: string | null) => void;
  setLegendOpen: (open: boolean) => void;
  revealNodes: (ids: string[], options?: { select?: boolean }) => void;
  reset: () => void;
}

const INITIAL = { showWeakOnly: false, hoveredNodeId: null, focusBook: null, reveal: null, legendOpen: null as boolean | null };

export const useGraphViewStore = create<GraphViewState>((set, get) => ({
  ...INITIAL,
  setShowWeakOnly: (showWeakOnly) => set({ showWeakOnly }),
  setHoveredNodeId: (hoveredNodeId) => set({ hoveredNodeId }),
  setFocusBook: (focusBook) => set({ focusBook }),
  setLegendOpen: (legendOpen) => set({ legendOpen }),
  revealNodes: (ids, options) => {
    if (ids.length === 0) return;
    set({ reveal: { ids, select: options?.select ?? ids.length === 1, seq: (get().reveal?.seq ?? 0) + 1 } });
  },
  reset: () => set(INITIAL),
}));
