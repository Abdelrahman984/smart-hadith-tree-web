import { getIsnadTree } from "@/lib/api";
import TreeCanvas from "@/features/isnad-tree/components/TreeCanvas";
import NarratorDrawer from "@/features/narrator-details/components/NarratorDrawer";
import Link from "next/link";
import { ArrowRight } from "lucide-react";

export default async function TreePage({
  params,
}: {
  params: Promise<{ hadithId: string }>;
}) {
  const { hadithId } = await params;
  
  // Fetch tree data on the server
  let treeData = null;
  try {
    treeData = await getIsnadTree(hadithId);
  } catch (error) {
    console.error("Failed to load tree:", error);
  }

  if (!treeData) {
    return (
      <div className="flex flex-col items-center justify-center h-full">
        <h2 className="text-2xl font-bold text-slate-800 mb-4">لم يتم العثور على الحديث</h2>
        <Link href="/search" className="text-brand-blue hover:underline">
          العودة للبحث
        </Link>
      </div>
    );
  }

  return (
    <div className="flex flex-col h-screen overflow-hidden relative">
      {/* Header */}
      <header className="bg-white border-b border-slate-200 px-6 py-4 flex items-start justify-between z-10 shadow-sm relative">
        <div className="max-w-3xl">
          <div className="flex items-center gap-3 mb-2">
            <h1 className="text-xl font-bold text-brand-dark">
              {treeData.bookName} - حديث رقم {treeData.hadithNumber}
            </h1>
          </div>
          <p className="text-slate-700 font-arabic text-lg leading-relaxed">
            {treeData.matnArabic}
          </p>
        </div>
        
        <Link 
          href="/search" 
          className="flex items-center gap-2 px-4 py-2 bg-slate-100 hover:bg-slate-200 text-slate-700 rounded-lg transition-colors shrink-0"
        >
          <span>عودة للبحث</span>
          <ArrowRight className="w-4 h-4" />
        </Link>
      </header>

      {/* Tree Canvas */}
      <main className="flex-1 relative bg-slate-50">
        <TreeCanvas treeData={treeData} />
        
        {/* Drawer Component */}
        <NarratorDrawer />
      </main>
    </div>
  );
}
