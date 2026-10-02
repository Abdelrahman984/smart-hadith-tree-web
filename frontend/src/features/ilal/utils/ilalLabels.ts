import { IlalFindingDto, IlalReportDto, IllahSeverity, IllahType } from '@/types/api';

export const ILLAH_TYPE_LABELS: Record<IllahType, string> = {
  Tadlis: 'التدليس',
  Ikhtilat: 'الاختلاط',
  HiddenInqita: 'الانقطاع الخفي',
  Ziyadah: 'زيادة الثقة',
  Shudhudh: 'الشذوذ',
  Nakarah: 'النكارة',
  Idtirab: 'الاضطراب',
  RafWaqf: 'الرفع والوقف',
  WaslIrsal: 'الوصل والإرسال',
};

/** Display order of finding groups: isnad defects first, then matn defects. */
export const ILLAH_TYPE_ORDER: IllahType[] = [
  'Tadlis', 'Ikhtilat', 'HiddenInqita', 'WaslIrsal', 'RafWaqf', 'Idtirab', 'Shudhudh', 'Nakarah', 'Ziyadah',
];

export const SEVERITY_STYLES: Record<IllahSeverity, { label: string; badge: string; card: string; dot: string }> = {
  Qadihah: {
    label: 'علة قادحة',
    badge: 'bg-red-100 text-red-800 border-red-200',
    card: 'border-red-200 bg-red-50/60',
    dot: 'bg-red-500',
  },
  GhayrQadihah: {
    label: 'غير قادحة',
    badge: 'bg-amber-100 text-amber-800 border-amber-200',
    card: 'border-amber-200 bg-amber-50/60',
    dot: 'bg-amber-500',
  },
  Tanbih: {
    label: 'تنبيه',
    badge: 'bg-slate-100 text-slate-700 border-slate-200',
    card: 'border-slate-200 bg-slate-50',
    dot: 'bg-slate-400',
  },
};

export interface IlalEdgeDecoration {
  label: string;
  color: string;
  dash: string;
}

const EDGE_DECORATIONS: Partial<Record<IllahType, IlalEdgeDecoration>> = {
  Tadlis: { label: 'عنعنة مدلس', color: '#ea580c', dash: '2 4' },
  HiddenInqita: { label: 'لم يثبت اللقاء', color: '#d97706', dash: '8 4' },
  Ikhtilat: { label: 'رواية عن مختلط', color: '#ca8a04', dash: '6 3' },
};

/**
 * Maps edge ids (`e-{sheikhId}-{studentId}`, as built by the canvases) to the styling of the
 * isnad-link finding on that edge. Finding narrator order is [student, sheikh] for tadlis and
 * [sheikh, student] for the others.
 */
export function getIlalEdgeDecorations(report: IlalReportDto | null): Map<string, IlalEdgeDecoration> {
  const map = new Map<string, IlalEdgeDecoration>();
  if (!report) return map;

  for (const finding of report.findings) {
    const decoration = EDGE_DECORATIONS[finding.type];
    if (!decoration || finding.narratorIds.length < 2) continue;

    const [a, b] = finding.narratorIds;
    const [sheikh, student] = finding.type === 'Tadlis' ? [b, a] : [a, b];
    const id = `e-${sheikh}-${student}`;
    if (!map.has(id)) map.set(id, decoration);
  }
  return map;
}

/** Groups findings by type in display order, keeping each finding's index in the report. */
export function groupFindings(findings: IlalFindingDto[]) {
  return ILLAH_TYPE_ORDER
    .map((type) => ({
      type,
      items: findings.map((f, index) => ({ finding: f, index })).filter((x) => x.finding.type === type),
    }))
    .filter((g) => g.items.length > 0);
}
