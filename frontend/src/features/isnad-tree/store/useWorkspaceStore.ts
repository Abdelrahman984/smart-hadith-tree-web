import { create } from "zustand";

/** Id of the tab the workspace adds itself while a narrator is selected on a wide screen. */
export const NARRATOR_TAB = "narrator";

interface WorkspaceState {
  /** The side panel is open (on a phone it then covers the graph). */
  panelOpen: boolean;
  activeTab: string;
  /** The tab to return to when the narrator tab closes. */
  previousTab: string;
  /** Called by the workspace when it mounts, with the page's defaults. */
  init: (defaults: { tab: string; open: boolean }) => void;
  setPanelOpen: (open: boolean) => void;
  /** Shows a tab without opening the panel. */
  setTab: (id: string) => void;
  /** Shows a tab and opens the panel. */
  openTab: (id: string) => void;
}

export const useWorkspaceStore = create<WorkspaceState>((set, get) => ({
  panelOpen: false,
  activeTab: "",
  previousTab: "",
  init: ({ tab, open }) => set({ activeTab: tab, previousTab: tab, panelOpen: open }),
  setPanelOpen: (panelOpen) => set({ panelOpen }),
  setTab: (id) => {
    const { activeTab } = get();
    if (id === activeTab) return;
    // Remember where to come back to; the narrator tab is temporary.
    set({ activeTab: id, previousTab: activeTab === NARRATOR_TAB ? get().previousTab : activeTab });
  },
  openTab: (id) => {
    get().setTab(id);
    set({ panelOpen: true });
  },
}));
