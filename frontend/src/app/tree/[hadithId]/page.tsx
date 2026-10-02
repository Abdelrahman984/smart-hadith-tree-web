import { getIsnadTree } from "@/lib/api";
import TreeCanvas from "@/features/isnad-tree/components/TreeCanvas";
import NarratorDrawer from "@/features/narrator-details/components/NarratorDrawer";
import ReturnToSearchButton from "@/features/isnad-tree/components/ReturnToSearchButton";
import IlalLauncher from "@/features/ilal/components/IlalLauncher";

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
      <div className="flex flex-col items-center justify-center h-full space-y-4">
        <h2 className="text-2xl font-bold text-slate-800 mb-2">لم يتم العثور على الحديث</h2>
        <ReturnToSearchButton variant="button" label="العودة للبحث" />
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
        
        <div className="flex items-center gap-2 shrink-0">
          <IlalLauncher hadithId={hadithId} />
          <ReturnToSearchButton variant="button" label="عودة للبحث" />
        </div>
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
