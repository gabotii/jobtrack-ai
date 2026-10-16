import {
  DndContext,
  DragOverlay,
  KeyboardSensor,
  PointerSensor,
  TouchSensor,
  useDroppable,
  useSensor,
  useSensors,
  type Announcements,
  type DragEndEvent,
  type DragStartEvent,
} from '@dnd-kit/core'
import { KanbanSquare, Plus, Search } from 'lucide-react'
import { useMemo, useState } from 'react'
import { toast } from 'sonner'
import { EmptyState, ErrorState, PageHeader } from '@/components/page'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/display'
import { Input, Select } from '@/components/ui/form-controls'
import { useBoard, useChangeStatus } from '@/features/applications/api'
import { CreateApplicationDialog } from '@/features/applications/CreateApplicationDialog'
import { useCompanyOptions } from '@/features/companies/api'
import { useDebouncedValue } from '@/hooks/use-debounced-value'
import { errorMessage } from '@/lib/api-client'
import { APPLICATION_STATUSES, STATUS_META } from '@/lib/labels'
import { cn } from '@/lib/utils'
import type { ApplicationStatus, ApplicationSummary } from '@/types/api'
import { checkMove, groupByStatus } from './board-logic'
import { KanbanCard } from './KanbanCard'

const labelOf = (id: string | number | undefined) =>
  id && id in STATUS_META ? STATUS_META[id as ApplicationStatus].label : 'fuera del tablero'

export function KanbanPage() {
  const [search, setSearch] = useState('')
  const [companyId, setCompanyId] = useState('')
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [hidden, setHidden] = useState<ApplicationStatus[]>([])
  const [activeCard, setActiveCard] = useState<ApplicationSummary | null>(null)
  const [createOpen, setCreateOpen] = useState(false)
  const debouncedSearch = useDebouncedValue(search)
  const companies = useCompanyOptions()
  const board = useBoard({
    search: debouncedSearch || undefined,
    companyId: companyId || undefined,
    from: from || undefined,
    to: to || undefined,
  })
  const changeStatus = useChangeStatus()

  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 6 } }),
    useSensor(TouchSensor, { activationConstraint: { delay: 200, tolerance: 6 } }),
    useSensor(KeyboardSensor),
  )

  const columns = useMemo(() => groupByStatus(board.data ?? []), [board.data])
  const visibleStatuses = APPLICATION_STATUSES.filter((s) => !hidden.includes(s))

  const move = (card: ApplicationSummary, target: ApplicationStatus) => {
    const check = checkMove(card, target)
    if (!check.ok) {
      toast.error(check.reason)
      return
    }
    changeStatus.mutate(
      { id: card.id, status: target, note: null, rejectionReason: null },
      {
        onSuccess: () => toast.success(`«${card.jobTitle}» movida a ${STATUS_META[target].label}`),
        onError: (error) => toast.error(`No se pudo mover la postulación: ${errorMessage(error)}`),
      },
    )
  }

  const onDragStart = (event: DragStartEvent) =>
    setActiveCard((event.active.data.current as { card: ApplicationSummary }).card)

  const onDragEnd = (event: DragEndEvent) => {
    setActiveCard(null)
    const card = (event.active.data.current as { card: ApplicationSummary }).card
    const target = event.over?.id as ApplicationStatus | undefined
    if (!target || target === card.status) return
    move(card, target)
  }

  const announcements: Announcements = {
    onDragStart: ({ active }) =>
      `Has levantado «${(active.data.current as { card: ApplicationSummary }).card.jobTitle}».`,
    onDragOver: ({ over }) => (over ? `Sobre la columna ${labelOf(over.id)}.` : 'Fuera de las columnas.'),
    onDragEnd: ({ over }) => (over ? `Soltada en ${labelOf(over.id)}.` : 'Movimiento cancelado.'),
    onDragCancel: () => 'Movimiento cancelado.',
  }

  const filtering = debouncedSearch !== '' || companyId !== '' || from !== '' || to !== ''

  return (
    <>
      <PageHeader
        title="Tablero"
        description="Arrastra las tarjetas para cambiar su estado, o usa el botón de cada tarjeta."
        actions={
          <Button onClick={() => setCreateOpen(true)}>
            <Plus /> Nueva postulación
          </Button>
        }
      />

      <div className="mb-3 grid gap-2 md:grid-cols-[1fr_12rem_9.5rem_9.5rem]">
        <div className="relative">
          <Search
            className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted-foreground"
            aria-hidden
          />
          <Input
            type="search"
            aria-label="Buscar en el tablero"
            placeholder="Buscar por puesto o empresa"
            className="pl-9"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
        </div>
        <Select aria-label="Empresa" value={companyId} onChange={(e) => setCompanyId(e.target.value)}>
          <option value="">Todas las empresas</option>
          {companies.data?.items.map((c) => (
            <option key={c.id} value={c.id}>
              {c.name}
            </option>
          ))}
        </Select>
        <Input
          type="date"
          aria-label="Desde"
          title="Desde"
          value={from}
          onChange={(e) => setFrom(e.target.value)}
        />
        <Input
          type="date"
          aria-label="Hasta"
          title="Hasta"
          value={to}
          onChange={(e) => setTo(e.target.value)}
        />
      </div>
      <div className="mb-4 flex flex-wrap items-center gap-1.5" role="group" aria-label="Columnas visibles">
        <span className="mr-1 text-xs font-medium text-muted-foreground">Columnas:</span>
        {APPLICATION_STATUSES.map((status) => {
          const visible = !hidden.includes(status)
          return (
            <button
              key={status}
              type="button"
              aria-pressed={visible}
              onClick={() =>
                setHidden((current) => (visible ? [...current, status] : current.filter((s) => s !== status)))
              }
              className={cn(
                'rounded-full border px-2.5 py-0.5 text-xs font-medium',
                visible
                  ? 'border-primary/40 bg-primary-soft text-primary'
                  : 'border-border text-muted-foreground line-through',
              )}
            >
              {STATUS_META[status].label}
            </button>
          )
        })}
      </div>

      {board.isPending ? (
        <div className="flex gap-3 overflow-hidden">
          {Array.from({ length: 4 }, (_, i) => (
            <Skeleton key={i} className="h-72 w-72 shrink-0" />
          ))}
        </div>
      ) : board.isError ? (
        <ErrorState error={board.error} onRetry={() => board.refetch()} />
      ) : board.data.length === 0 && !filtering ? (
        <EmptyState
          icon={KanbanSquare}
          title="Tu tablero está vacío"
          description="Crea una postulación a partir de una oferta guardada y aparecerá aquí."
          action={
            <Button onClick={() => setCreateOpen(true)}>
              <Plus /> Nueva postulación
            </Button>
          }
        />
      ) : (
        <DndContext
          sensors={sensors}
          onDragStart={onDragStart}
          onDragEnd={onDragEnd}
          onDragCancel={() => setActiveCard(null)}
          accessibility={{
            announcements,
            screenReaderInstructions: {
              draggable:
                'Para mover una postulación, pulsa espacio o intro. Usa las flechas para elegir columna y pulsa espacio de nuevo para soltarla, o escape para cancelar.',
            },
          }}
        >
          <div className="-mx-4 flex snap-x scroll-px-4 gap-3 overflow-x-auto px-4 pb-4 sm:-mx-6 sm:scroll-px-6 sm:px-6 lg:-mx-8 lg:scroll-px-8 lg:px-8">
            {visibleStatuses.map((status) => (
              <KanbanColumn
                key={status}
                status={status}
                cards={columns[status]}
                activeCard={activeCard}
                onMove={move}
              />
            ))}
          </div>
          <DragOverlay>
            {activeCard ? <KanbanCard card={activeCard} onMove={move} overlay /> : null}
          </DragOverlay>
        </DndContext>
      )}

      <CreateApplicationDialog open={createOpen} onOpenChange={setCreateOpen} />
    </>
  )
}

function KanbanColumn({
  status,
  cards,
  activeCard,
  onMove,
}: {
  status: ApplicationStatus
  cards: ApplicationSummary[]
  activeCard: ApplicationSummary | null
  onMove: (card: ApplicationSummary, target: ApplicationStatus) => void
}) {
  const { setNodeRef, isOver } = useDroppable({ id: status })
  const meta = STATUS_META[status]
  const Icon = meta.icon
  const dragging = activeCard !== null && activeCard.status !== status
  const allowed = dragging && activeCard.allowedTransitions.includes(status)

  return (
    <section
      ref={setNodeRef}
      aria-labelledby={`column-${status}`}
      className={cn(
        'flex w-72 shrink-0 snap-start flex-col rounded-xl border border-transparent bg-muted/70 transition-colors',
        dragging && !allowed && 'opacity-50',
        isOver && allowed && 'border-primary bg-primary-soft',
        isOver && dragging && !allowed && 'border-danger',
      )}
    >
      <header
        className="flex items-center gap-2 px-3 pt-3 pb-2"
        style={{ borderTop: `3px solid ${meta.color}`, borderRadius: '0.75rem 0.75rem 0 0' }}
      >
        <Icon className="size-4" style={{ color: meta.color }} aria-hidden />
        <h2 id={`column-${status}`} className="text-sm font-semibold">
          {meta.label}
        </h2>
        <span className="ml-auto rounded-full bg-surface px-2 text-xs font-semibold text-muted-foreground">
          {cards.length}
        </span>
      </header>
      {dragging && !allowed ? (
        <p className="px-3 text-xs text-muted-foreground">No disponible para esta tarjeta</p>
      ) : null}
      <div className="flex min-h-32 flex-1 flex-col gap-2 p-2">
        {cards.length === 0 ? (
          <p className="rounded-lg border border-dashed border-border px-3 py-6 text-center text-xs text-muted-foreground">
            Sin postulaciones
          </p>
        ) : (
          cards.map((card) => <KanbanCard key={card.id} card={card} onMove={onMove} />)
        )}
      </div>
    </section>
  )
}
