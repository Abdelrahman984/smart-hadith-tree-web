import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { cache } from "react";
import { getIsnadTree, NotFoundError } from "@/lib/api";
import TreeCanvas from "@/features/isnad-tree/components/TreeCanvas";
import NarratorDrawer from "@/features/narrator-details/components/NarratorDrawer";
import ReturnToSearchButton from "@/features/isnad-tree/components/ReturnToSearchButton";
import EvidenceBadge from "@/components/EvidenceBadge";
import ClampedText from "@/components/ClampedText";
import SiteHeader from "@/components/SiteHeader";
import IlalLauncher from "@/features/ilal/components/IlalLauncher";

type Params = Promise<{ hadithId: string }>;

/** One request per render, shared by the metadata and the page. A 404 shows the not-found page; other errors the error page. */
const loadTree = cache(async (hadithId: string) => {
  try {
    return await getIsnadTree(hadithId);
  } catch (error) {
    if (error instanceof NotFoundError) notFound();
    throw error;
  }
});

export async function generateMetadata({ params }: { params: Params }): Promise<Metadata> {
  const { hadithId } = await params;
  const tree = await loadTree(hadithId);
  return { title: `شجرة إسناد ${tree.bookName} ${tree.hadithNumber}` };
}

export default async function TreePage({ params }: { params: Params }) {
  const { hadithId } = await params;
  const treeData = await loadTree(hadithId);

  return (
    <div className="flex flex-col h-screen overflow-hidden relative">
      <SiteHeader variant="compact" />

      {/* Hadith header */}
      <header className="bg-surface border-b border-line px-4 sm:px-6 py-3 flex flex-col sm:flex-row sm:items-start justify-between gap-3 z-10 shadow-sm relative shrink-0">
        <div className="max-w-3xl min-w-0">
          <h1 className="text-lg sm:text-xl font-bold text-brand-dark mb-1">
            {treeData.bookName} - حديث رقم {treeData.hadithNumber}
          </h1>
          <ClampedText text={treeData.matnArabic} className="text-slate-700 font-arabic text-base sm:text-lg leading-relaxed" />
          <p className="mt-2 flex flex-wrap items-center gap-2 text-xs text-ink-muted">
            <span className="font-semibold">حكم الحديث:</span>
            <EvidenceBadge status="insufficient" />
            <span>
              <span className="hidden sm:inline">لا يحمل المشروع حكماً معتمداً لهذا الحديث؛ للتحقق </span>راجع{" "}
              <a href="https://dorar.net/hadith" target="_blank" rel="noopener noreferrer" className="font-semibold text-indigo-700 underline">
                الدرر السنية
              </a>
              .
            </span>
          </p>
        </div>

        <div className="flex items-center gap-2 shrink-0">
          <IlalLauncher hadithId={hadithId} />
          <ReturnToSearchButton variant="button" label="عودة للبحث" />
        </div>
      </header>

      {/* Tree Canvas */}
      <main id="main-content" className="flex-1 relative bg-surface-muted">
        <TreeCanvas treeData={treeData} />
        <NarratorDrawer />
      </main>
    </div>
  );
}
