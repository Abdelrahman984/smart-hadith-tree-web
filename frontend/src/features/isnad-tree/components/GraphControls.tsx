"use client";

import { useReactFlow, Panel } from "@xyflow/react";
import { Download, EyeOff, Eye } from "lucide-react";
import { toPng } from "html-to-image";

interface GraphControlsProps {
  showWeakOnly: boolean;
  setShowWeakOnly: (val: boolean) => void;
}

export default function GraphControls({ showWeakOnly, setShowWeakOnly }: GraphControlsProps) {
  const { getNodes } = useReactFlow();

  const handleDownload = () => {
    const nodesBounds = getNodes().reduce(
      (acc, node) => {
        if (!node.width || !node.height) return acc;
        acc.xMin = Math.min(acc.xMin, node.position.x);
        acc.yMin = Math.min(acc.yMin, node.position.y);
        acc.xMax = Math.max(acc.xMax, node.position.x + node.width);
        acc.yMax = Math.max(acc.yMax, node.position.y + node.height);
        return acc;
      },
      { xMin: Infinity, yMin: Infinity, xMax: -Infinity, yMax: -Infinity }
    );

    const transform = document.querySelector(".react-flow__viewport") as HTMLElement;
    if (!transform) return;

    toPng(document.querySelector(".react-flow") as HTMLElement, {
      backgroundColor: "#f8fafc",
      width: (nodesBounds.xMax - nodesBounds.xMin) + 200,
      height: (nodesBounds.yMax - nodesBounds.yMin) + 200,
      style: {
        width: "100%",
        height: "100%",
        transform: `translate(${-nodesBounds.xMin + 100}px, ${-nodesBounds.yMin + 100}px) scale(1)`,
      },
    }).then((dataUrl) => {
      const a = document.createElement("a");
      a.setAttribute("download", "isnad-tree.png");
      a.setAttribute("href", dataUrl);
      a.click();
    });
  };

  return (
    <Panel position="top-left" className="flex flex-col gap-2 bg-white/90 backdrop-blur p-2 rounded-xl shadow-md border border-slate-200">
      <button
        onClick={() => setShowWeakOnly(!showWeakOnly)}
        className={`flex items-center gap-2 px-3 py-2 rounded-lg text-sm font-semibold transition-colors ${
          showWeakOnly 
            ? "bg-red-50 text-red-600 border border-red-200" 
            : "bg-slate-50 text-slate-700 border border-slate-200 hover:bg-slate-100"
        }`}
        title="تظليل الرواة الثقات وإبراز الضعفاء"
      >
        {showWeakOnly ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
        <span>{showWeakOnly ? "إظهار الجميع" : "إبراز الضعفاء"}</span>
      </button>

      <button
        onClick={handleDownload}
        className="flex items-center gap-2 px-3 py-2 bg-slate-50 text-slate-700 border border-slate-200 rounded-lg text-sm font-semibold hover:bg-slate-100 transition-colors"
      >
        <Download className="w-4 h-4" />
        <span>تصدير صورة</span>
      </button>
    </Panel>
  );
}
