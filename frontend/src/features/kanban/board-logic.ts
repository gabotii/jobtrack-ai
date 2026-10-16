import { STATUS_META } from '@/lib/labels'
import type { ApplicationStatus, ApplicationSummary } from '@/types/api'

export type MoveCheck = { ok: true } | { ok: false; reason: string }

/** Client-side mirror of the server's answer (allowedTransitions), used to give instant feedback while dragging. */
export function checkMove(card: ApplicationSummary, target: ApplicationStatus): MoveCheck {
  if (card.status === target) return { ok: false, reason: 'La postulación ya está en esa columna.' }
  if (!card.allowedTransitions.includes(target)) {
    return {
      ok: false,
      reason: `No se puede pasar de «${STATUS_META[card.status].label}» a «${STATUS_META[target].label}».`,
    }
  }
  return { ok: true }
}

export function groupByStatus(cards: ApplicationSummary[]): Record<ApplicationStatus, ApplicationSummary[]> {
  const groups = {
    saved: [],
    toApply: [],
    applied: [],
    screening: [],
    interview: [],
    offer: [],
    accepted: [],
    rejected: [],
    withdrawn: [],
  } as Record<ApplicationStatus, ApplicationSummary[]>
  for (const card of cards) groups[card.status].push(card)
  return groups
}
