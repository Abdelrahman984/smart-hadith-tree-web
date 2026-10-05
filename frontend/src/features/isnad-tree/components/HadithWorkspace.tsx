"use client";

import { useEffect, useRef, type ReactNode } from "react";
import { ChevronLeft, ChevronRight, UserCheck, X, type LucideIcon } from "lucide-react";
import SiteHeader from "@/components/SiteHeader";
import { useMediaQuery } from "@/components/useMediaQuery";
import { useIlalStore } from "@/features/ilal/store/useIlalStore";
import NarratorDetails from "@/features/narrator-details/components/NarratorDetails";
import NarratorDrawer from "@/features/narrator-details/components/NarratorDrawer";
import { useNarratorDrawerStore } from "@/features/narrator-details/store/useNarratorDrawerStore";
import { isPhone } from "@/lib/viewport";
import { useWorkspaceUrlState } from "../hooks/useWorkspaceUrlState";
import { NARRATOR_TAB, useWorkspaceStore } from "../store/useWorkspaceStore";

export interface WorkspaceTab {
  id: string;
  label: string;
  Icon: LucideIcon;
  badge?: ReactNode;
  /** `active` is false while another tab is showing; the content stays mounted so it keeps its state. */
  render: (active: boolean) => ReactNode;
}

interface HadithWorkspaceProps {
  /** The hadith (or comparison) title block, under the site header. */
  header: ReactNode;
  /** An optional strip between the header and the graph. */
  summary?: ReactNode;
  canvas: ReactNode;
  tabs: WorkspaceTab[];
  defaultTab: string;
  /** "desktop": the panel starts open beside the graph (not on phones). "never": it starts closed. */
  panelStartsOpen: "desktop" | "never";
  /** Word for the panel's toggle button: «إظهار <label>». */
  panelLabel: string;
  /** The page compares books, so `?book=` is kept in the address. */
  bookFocus?: boolean;
}

/**
 * The page shared by the single tree and the takhreej tree: site header, hadith header, graph, and a side panel of
 * tabs. On wide screens (1024px up) the selected narrator opens as a tab of the panel, so the graph stays visible
 * and the card stays selected; below that it opens as a drawer over the graph.
 */
export default function HadithWorkspace({
  header,
  summary,
  canvas,
  tabs,
  defaultTab,
  panelStartsOpen,
  panelLabel,
  bookFocus = false,
}: HadithWorkspaceProps) {
  // Set the page's defaults before the first render, so the panel does not slide open on load.
  const initialized = useRef<true | null>(null);
  if (initialized.current == null) {
    initialized.current = true;
    useWorkspaceStore.getState().init({ tab: defaultTab, open: panelStartsOpen === "desktop" && !isPhone() });
  }

  const panelOpen = useWorkspaceStore((s) => s.panelOpen);
  const activeTab = useWorkspaceStore((s) => s.activeTab);
  const previousTab = useWorkspaceStore((s) => s.previousTab);
  const setPanelOpen = useWorkspaceStore((s) => s.setPanelOpen);
  const setTab = useWorkspaceStore((s) => s.setTab);
  const openTab = useWorkspaceStore((s) => s.openTab);

  const isWide = useMediaQuery("(min-width: 1024px)", true);
  const narratorOpen = useNarratorDrawerStore((s) => s.isOpen);
  const selectedNarratorId = useNarratorDrawerStore((s) => s.selectedNarratorId);
  const closeNarrator = useNarratorDrawerStore((s) => s.closeDrawer);
  const narratorInPanel = isWide && narratorOpen;

  useWorkspaceUrlState({ tabIds: tabs.map((t) => t.id), defaultTab, bookFocus });

  // A selected narrator opens the narrator tab on wide screens, and closing it returns to the previous tab.
  useEffect(() => {
    if (narratorInPanel) openTab(NARRATOR_TAB);
    else if (useWorkspaceStore.getState().activeTab === NARRATOR_TAB) setTab(useWorkspaceStore.getState().previousTab);
  }, [narratorInPanel, openTab, setTab]);

  // On a phone the panel covers the graph: picking a finding closes it, so the narrators it names can be seen.
  useEffect(
    () =>
      useIlalStore.subscribe((state, prev) => {
        if (state.activeFindingIndex !== null && state.activeFindingIndex !== prev.activeFindingIndex && isPhone()) {
          setPanelOpen(false);
        }
      }),
    [setPanelOpen]
  );

  const allTabs: WorkspaceTab[] = narratorInPanel
    ? [...tabs, { id: NARRATOR_TAB, label: "الراوي", Icon: UserCheck, render: () => null }]
    : tabs;
  const shownTab = allTabs.some((t) => t.id === activeTab) ? activeTab : previousTab || defaultTab;

  return (
    <div className="h-screen w-full flex flex-col overflow-hidden bg-surface-muted relative" dir="rtl">
      <SiteHeader variant="compact" />
      {header}
      {summary}

      <div className="flex-1 flex overflow-hidden relative">
        <main id="main-content" className="flex-1 relative h-full">
          {canvas}
          {!isWide && <NarratorDrawer />}
        </main>

        <aside
          id="workspace-panel"
          aria-label={`لوحة ${panelLabel}`}
          className={`absolute inset-y-0 left-0 z-20 md:relative md:z-10 transition-all duration-300 ease-in-out border-r border-line bg-surface flex flex-col overflow-hidden shadow-xl md:shadow-none ${
            panelOpen ? "w-full sm:w-96" : "w-0 border-r-0"
          }`}
        >
          <div className="flex min-h-0 flex-1 flex-col min-w-[100vw] sm:min-w-[24rem]">
            <div className="flex items-center justify-between border-b border-line px-4 py-2 md:hidden">
              <span className="text-sm font-bold text-ink">{panelLabel}</span>
              <button
                type="button"
                onClick={() => setPanelOpen(false)}
                className="flex items-center gap-1 rounded-lg px-2 py-1 text-xs font-semibold text-ink-muted hover:bg-slate-100 cursor-pointer"
              >
                <X className="w-4 h-4" aria-hidden />
                <span>إغلاق والعودة للشجرة</span>
              </button>
            </div>

            <div role="tablist" aria-label="أقسام اللوحة" className="mx-4 mt-4 flex shrink-0 rounded-lg bg-slate-100 p-1 text-sm font-semibold">
              {allTabs.map(({ id, label, Icon, badge }) => (
                <button
                  key={id}
                  type="button"
                  role="tab"
                  id={`workspace-tab-${id}`}
                  aria-selected={shownTab === id}
                  aria-controls={`workspace-tabpanel-${id}`}
                  onClick={() => setTab(id)}
                  className={`flex flex-1 items-center justify-center gap-1.5 rounded-md py-1.5 transition-colors cursor-pointer ${
                    shownTab === id ? "bg-surface text-brand-blue shadow-sm" : "text-ink-subtle hover:text-ink"
                  }`}
                >
                  <Icon className="w-4 h-4" aria-hidden /> {label}
                  {badge}
                </button>
              ))}
            </div>

            <div className="min-h-0 flex-1 overflow-y-auto">
              {tabs.map((tab) => (
                <div
                  key={tab.id}
                  role="tabpanel"
                  id={`workspace-tabpanel-${tab.id}`}
                  aria-labelledby={`workspace-tab-${tab.id}`}
                  hidden={shownTab !== tab.id}
                  className="p-4 space-y-5"
                >
                  {tab.render(shownTab === tab.id)}
                </div>
              ))}
              {narratorInPanel && (
                <div
                  role="tabpanel"
                  id={`workspace-tabpanel-${NARRATOR_TAB}`}
                  aria-labelledby={`workspace-tab-${NARRATOR_TAB}`}
                  hidden={shownTab !== NARRATOR_TAB}
                >
                  <div className="flex items-center justify-between border-b border-line px-4 py-2">
                    <h2 className="text-base font-bold text-ink">تفاصيل الراوي</h2>
                    <button
                      type="button"
                      onClick={closeNarrator}
                      aria-label="إغلاق تفاصيل الراوي"
                      className="rounded-full p-1 text-ink-subtle hover:bg-slate-100 cursor-pointer"
                    >
                      <X size={18} />
                    </button>
                  </div>
                  <NarratorDetails key={selectedNarratorId} />
                </div>
              )}
            </div>
          </div>
        </aside>

        <button
          type="button"
          onClick={() => setPanelOpen(!panelOpen)}
          title={panelOpen ? `إخفاء ${panelLabel}` : `إظهار ${panelLabel}`}
          aria-label={panelOpen ? `إخفاء ${panelLabel}` : `إظهار ${panelLabel}`}
          aria-controls="workspace-panel"
          aria-expanded={panelOpen}
          className={`absolute top-1/2 -translate-y-1/2 bg-surface border border-l-0 border-line p-2 rounded-r-lg shadow-md z-20 text-ink-subtle hover:text-brand-blue transition-all duration-300 ease-in-out items-center justify-center gap-1 cursor-pointer ${
            panelOpen ? "hidden md:flex md:left-96" : "flex left-0"
          }`}
        >
          {panelOpen ? <ChevronLeft className="w-5 h-5" /> : <ChevronRight className="w-5 h-5" />}
          {!panelOpen && <span className="text-xs font-semibold md:hidden">{panelLabel}</span>}
        </button>
      </div>
    </div>
  );
}
