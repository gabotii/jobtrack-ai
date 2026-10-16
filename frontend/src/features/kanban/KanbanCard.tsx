import { useDraggable } from '@dnd-kit/core'
import { CSS } from '@dnd-kit/utilities'
import { ArrowRightLeft, CalendarDays, GripVertical, MapPin } from 'lucide-react'
import { Link } from 'react-router'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { formatDate } from '@/lib/dates'
import { STATUS_META } from '@/lib/labels'
import { cn } from '@/lib/utils'
import type { ApplicationStatus, ApplicationSummary } from '@/types/api'

interface Props {
  card: ApplicationSummary
  onMove: (card: ApplicationSummary, target: ApplicationStatus) => void
  /** Rendered inside the drag overlay: no drag bindings, just the visual. */
  overlay?: boolean
}

export function KanbanCard({ card, onMove, overlay = false }: Props) {
  const { attributes, listeners, setNodeRef, transform, isDragging } = useDraggable({
    id: card.id,
    data: { card },
    disabled: overlay,
  })

  return (
    <article
      ref={overlay ? undefined : setNodeRef}
      style={overlay ? undefined : { transform: CSS.Translate.toString(transform) }}
      aria-label={`${card.jobTitle} en ${card.companyName}`}
      className={cn(
        'rounded-lg border border-border bg-surface p-3 text-sm shadow-xs',
        isDragging && 'opacity-40',
        overlay && 'rotate-1 shadow-xl',
      )}
    >
      <div className="flex items-start gap-1">
        <button
          type="button"
          className="-ml-1 cursor-grab touch-none rounded p-0.5 text-muted-foreground hover:bg-muted active:cursor-grabbing"
          aria-label={`Arrastrar «${card.jobTitle}». Pulsa espacio para levantar y las flechas para mover.`}
          {...(overlay ? {} : listeners)}
          {...(overlay ? {} : attributes)}
        >
          <GripVertical className="size-4" aria-hidden />
        </button>
        <div className="min-w-0 flex-1">
          <Link to={`/applications/${card.id}`} className="line-clamp-2 font-semibold hover:text-primary">
            {card.jobTitle}
          </Link>
          <p className="truncate text-xs text-muted-foreground">{card.companyName}</p>
        </div>
        {overlay ? null : (
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button
                variant="ghost"
                size="icon"
                className="size-7"
                aria-label={`Mover «${card.jobTitle}» a otro estado`}
              >
                <ArrowRightLeft />
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end">
              <DropdownMenuLabel>Mover a…</DropdownMenuLabel>
              {card.allowedTransitions.length === 0 ? (
                <DropdownMenuItem disabled>Sin cambios posibles</DropdownMenuItem>
              ) : (
                card.allowedTransitions.map((status) => {
                  const Icon = STATUS_META[status].icon
                  return (
                    <DropdownMenuItem key={status} onSelect={() => onMove(card, status)}>
                      <Icon style={{ color: STATUS_META[status].color }} aria-hidden />
                      {STATUS_META[status].label}
                    </DropdownMenuItem>
                  )
                })
              )}
            </DropdownMenuContent>
          </DropdownMenu>
        )}
      </div>

      <dl className="mt-2 space-y-1 text-xs text-muted-foreground">
        {card.location ? (
          <div className="flex items-center gap-1.5">
            <dt>
              <MapPin className="size-3.5" aria-label="Ubicación" />
            </dt>
            <dd className="truncate">{card.location}</dd>
          </div>
        ) : null}
        {card.appliedOn ? (
          <div className="flex items-center gap-1.5">
            <dt>
              <CalendarDays className="size-3.5" aria-label="Fecha de postulación" />
            </dt>
            <dd>Postulada el {formatDate(card.appliedOn)}</dd>
          </div>
        ) : null}
      </dl>
      {card.nextAction ? (
        <p className="mt-2 rounded-md bg-accent-soft px-2 py-1 text-xs text-accent">
          <span className="font-semibold">Siguiente:</span> {card.nextAction}
        </p>
      ) : null}
    </article>
  )
}
