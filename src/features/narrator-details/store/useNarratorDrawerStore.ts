import { create } from 'zustand';

interface DrawerState {
  isOpen: boolean;
  selectedNarratorId: string | null;
  openDrawer: (id: string) => void;
  closeDrawer: () => void;
}

export const useNarratorDrawerStore = create<DrawerState>((set) => ({
  isOpen: false,
  selectedNarratorId: null,
  openDrawer: (id) => set({ isOpen: true, selectedNarratorId: id }),
  closeDrawer: () => set({ isOpen: false, selectedNarratorId: null }),
}));
