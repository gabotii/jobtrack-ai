import {
  Archive,
  Award,
  Bookmark,
  CheckCircle2,
  Gift,
  MessagesSquare,
  PhoneCall,
  Search,
  Send,
  Undo2,
  XCircle,
  type LucideIcon,
} from 'lucide-react'
import type {
  ApplicationSource,
  ApplicationStatus,
  CalendarEventStatus,
  CalendarEventType,
  ContractType,
  ReminderPriority,
  WorkMode,
} from '@/types/api'

export interface StatusMeta {
  label: string
  /** Badge classes. Each status also has an icon so meaning never depends on color alone. */
  className: string
  /** Accent used for the Kanban column header and charts. */
  color: string
  icon: LucideIcon
}

export const APPLICATION_STATUSES: ApplicationStatus[] = [
  'saved',
  'toApply',
  'applied',
  'screening',
  'interview',
  'offer',
  'accepted',
  'rejected',
  'withdrawn',
]

export const STATUS_META: Record<ApplicationStatus, StatusMeta> = {
  saved: { label: 'Guardada', className: 'bg-slate-100 text-slate-700', color: '#64748b', icon: Bookmark },
  toApply: { label: 'Por postular', className: 'bg-sky-50 text-sky-800', color: '#0284c7', icon: Archive },
  applied: { label: 'Postulada', className: 'bg-indigo-50 text-indigo-800', color: '#4f46e5', icon: Send },
  screening: {
    label: 'Evaluación inicial',
    className: 'bg-violet-50 text-violet-800',
    color: '#7c3aed',
    icon: Search,
  },
  interview: {
    label: 'Entrevista',
    className: 'bg-amber-50 text-amber-900',
    color: '#d97706',
    icon: MessagesSquare,
  },
  offer: { label: 'Oferta recibida', className: 'bg-teal-50 text-teal-800', color: '#0d9488', icon: Gift },
  accepted: {
    label: 'Aceptada',
    className: 'bg-emerald-100 text-emerald-900',
    color: '#059669',
    icon: Award,
  },
  rejected: { label: 'Rechazada', className: 'bg-red-50 text-red-800', color: '#dc2626', icon: XCircle },
  withdrawn: { label: 'Retirada', className: 'bg-zinc-100 text-zinc-700', color: '#71717a', icon: Undo2 },
}

export const WORK_MODE_LABELS: Record<WorkMode, string> = {
  onSite: 'Presencial',
  hybrid: 'Híbrida',
  remote: 'Remota',
}

export const CONTRACT_TYPE_LABELS: Record<ContractType, string> = {
  fullTime: 'Jornada completa',
  partTime: 'Media jornada',
  contract: 'Contrato temporal / proyecto',
  freelance: 'Freelance',
  internship: 'Prácticas',
  temporary: 'Temporal',
  other: 'Otro',
}

export const SOURCE_LABELS: Record<ApplicationSource, string> = {
  companyWebsite: 'Web de la empresa',
  linkedIn: 'LinkedIn',
  jobBoard: 'Portal de empleo',
  referral: 'Referido',
  recruiter: 'Reclutador',
  other: 'Otra',
}

export const EVENT_TYPE_META: Record<CalendarEventType, { label: string; color: string; icon: LucideIcon }> =
  {
    interview: { label: 'Entrevista', color: '#0f766e', icon: MessagesSquare },
    call: { label: 'Llamada', color: '#4f46e5', icon: PhoneCall },
    followUp: { label: 'Seguimiento', color: '#b45309', icon: Send },
    reminder: { label: 'Recordatorio', color: '#7c3aed', icon: Bookmark },
    other: { label: 'Otro', color: '#475569', icon: CheckCircle2 },
  }

export const EVENT_STATUS_LABELS: Record<CalendarEventStatus, string> = {
  scheduled: 'Programado',
  completed: 'Realizado',
  cancelled: 'Cancelado',
}

export const PRIORITY_META: Record<ReminderPriority, { label: string; className: string }> = {
  low: { label: 'Baja', className: 'bg-slate-100 text-slate-700' },
  medium: { label: 'Media', className: 'bg-amber-50 text-amber-900' },
  high: { label: 'Alta', className: 'bg-red-50 text-red-800' },
}

export function formatSalary(min: number | null, max: number | null, currency: string | null): string | null {
  if (min === null && max === null) return null
  const fmt = (value: number) =>
    new Intl.NumberFormat('es-ES', {
      style: currency ? 'currency' : 'decimal',
      currency: currency ?? undefined,
      maximumFractionDigits: 0,
    }).format(value)
  if (min !== null && max !== null) return `${fmt(min)} – ${fmt(max)}`
  return min !== null ? `Desde ${fmt(min)}` : `Hasta ${fmt(max!)}`
}
