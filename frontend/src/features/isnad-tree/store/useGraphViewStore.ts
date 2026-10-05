import { create } from "zustand";

/** What the viewer is looking at on the graph; kept out of the node data so changing it does not rebuild the graph. */
interface GraphViewState {
  /** «إبراز الضعفاء»: reliable narrators are dimmed. */
  showWeakOnly: boolean;
  /** Narrator under the pointer: its whole chain is highlighted. */
  hoveredNodeId: string | null;
  /** Canonical name of the book whose routes are highlighted (comparative tree). */
  focusBook: string | null;
  setShowWeakOnly: (value: boolean) => void;
  setHoveredNodeId: (id: string | null) => void;
  setFocusBook: (book: string | null) => void;
  reset: () => void;
}

const INITIAL = { showWeakOnly: false, hoveredNodeId: null, focusBook: null };

export const useGraphViewStore = create<GraphViewState>((set) => ({
  ...INITIAL,
  setShowWeakOnly: (showWeakOnly) => set({ showWeakOnly }),
  setHoveredNodeId: (hoveredNodeId) => set({ hoveredNodeId }),
  setFocusBook: (focusBook) => set({ focusBook }),
  reset: () => set(INITIAL),
}));
