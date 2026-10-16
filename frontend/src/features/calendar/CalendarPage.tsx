import type { DatesSetArg, EventClickArg, EventInput } from '@fullcalendar/core'
import esLocale from '@fullcalendar/core/locales/es'
import dayGridPlugin from '@fullcalendar/daygrid'
import interactionPlugin, { type DateClickArg } from '@fullcalendar/interaction'
import listPlugin from '@fullcalendar/list'
import FullCalendar from '@fullcalendar/react'
import timeGridPlugin from '@fullcalendar/timegrid'
import { Ban, CheckCircle2, Globe2, MapPin, Pencil, Plus, Trash2, Video } from 'lucide-react'
import { useMemo, useState } from 'react'
import { Link } from 'react-router'
import { toast } from 'sonner'
import { ErrorState, PageHeader } from '@/components/page'
import { Button } from '@/components/ui/button'
import { ConfirmDialog, Dialog, DialogContent } from '@/components/ui/dialog'
import { Badge, Card, CardContent, CardHeader, CardTitle } from '@/components/ui/display'
import { useCurrentUser } from '@/features/auth/auth-context'
import { errorMessage } from '@/lib/api-client'
import { formatDateTime, formatTime, toInstant, toWallClock } from '@/lib/dates'
import { EVENT_STATUS_LABELS, EVENT_TYPE_META } from '@/lib/labels'
import type { CalendarEvent } from '@/types/api'
import { useCalendarEvents, useChangeEventStatus, useDeleteEvent, useUpcomingEvents } from './api'
import { EventFormDialog, type EventDraft } from './EventFormDialog'

/**
 * Time zones: FullCalendar can only render named zones with an extra plugin, so the grid runs in "UTC" and receives
 * floating wall-clock times already converted to the user's profile zone (DST-aware via @date-fns/tz). Its notion of
 * "now" is shifted the same way, so "today" and the now-indicator match the user's zone.
 */
export function CalendarPage() {
  const user = useCurrentUser()
  const zone = user.timeZone
  const [range, setRange] = useState<{ from: string; to: string } | null>(null)
  const events = useCalendarEvents(range?.from ?? null, range?.to ?? null)
  const upcoming = useUpcomingEvents(undefined, 8)
  const [formOpen, setFormOpen] = useState(false)
  const [editing, setEditing] = useState<CalendarEvent | undefined>()
  const [draft, setDraft] = useState<EventDraft | undefined>()
  const [selected, setSelected] = useState<CalendarEvent | null>(null)

  const calendarEvents = useMemo<EventInput[]>(
    () =>
      (events.data ?? []).map((event) => ({
        id: event.id,
        title: event.status === 'cancelled' ? `✕ ${event.title}` : event.title,
        start: toWallClock(event.startsAt, zone),
        end: toWallClock(event.endsAt, zone),
        backgroundColor: event.status === 'cancelled' ? '#cbd5e1' : EVENT_TYPE_META[event.type].color,
        textColor: event.status === 'cancelled' ? '#334155' : '#ffffff',
        classNames: event.status === 'cancelled' ? ['line-through'] : [],
        extendedProps: { event },
      })),
    [events.data, zone],
  )

  const openCreate = (newDraft?: EventDraft) => {
    setEditing(undefined)
    setDraft(newDraft)
    setFormOpen(true)
  }

  const onDatesSet = (arg: DatesSetArg) => {
    // arg.start/end are floating wall-clock dates (labelled UTC); convert them back to real instants in the user zone.
    const from = toInstant(arg.startStr.slice(0, 10), '00:00', zone)
    const to = toInstant(arg.endStr.slice(0, 10), '00:00', zone)
    setRange((current) => (current?.from === from && current.to === to ? current : { from, to }))
  }

  const onDateClick = (arg: DateClickArg) => {
    const date = arg.dateStr.slice(0, 10)
    if (arg.allDay) {
      openCreate({ date })
      return
    }
    const time = arg.dateStr.slice(11, 16)
    const [h, m] = time.split(':').map(Number)
    const endMinutes = Math.min(h * 60 + m + 60, 23 * 60 + 59)
    const endTime = `${String(Math.floor(endMinutes / 60)).padStart(2, '0')}:${String(endMinutes % 60).padStart(2, '0')}`
    openCreate({ date, startTime: time, endTime })
  }

  const onEventClick = (arg: EventClickArg) => setSelected(arg.event.extendedProps.event as CalendarEvent)

  return (
    <>
      <PageHeader
        title="Calendario"
        description={
          <span className="inline-flex items-center gap-1">
            <Globe2 className="size-3.5" aria-hidden /> Horas mostradas en tu zona horaria: {zone}
          </span>
        }
        actions={
          <Button onClick={() => openCreate()}>
            <Plus /> Nuevo evento
          </Button>
        }
      />

      <div className="grid gap-4 xl:grid-cols-[1fr_20rem]">
        <Card className="p-3 sm:p-4">
          {events.isError ? (
            <ErrorState error={events.error} onRetry={() => events.refetch()} />
          ) : (
            <div className={events.isFetching ? 'opacity-70 transition-opacity' : undefined}>
              <FullCalendar
                plugins={[dayGridPlugin, timeGridPlugin, listPlugin, interactionPlugin]}
                initialView="dayGridMonth"
                locale={esLocale}
                timeZone="UTC"
                now={() => toWallClock(new Date(), zone)}
                headerToolbar={{
                  left: 'prev,next today',
                  center: 'title',
                  right: 'dayGridMonth,timeGridWeek,timeGridDay,listWeek',
                }}
                buttonText={{ today: 'Hoy', month: 'Mes', week: 'Semana', day: 'Día', list: 'Agenda' }}
                height="auto"
                nowIndicator
                dayMaxEvents={3}
                events={calendarEvents}
                datesSet={onDatesSet}
                dateClick={onDateClick}
                eventClick={onEventClick}
                eventTimeFormat={{ hour: '2-digit', minute: '2-digit', hour12: false }}
                slotLabelFormat={{ hour: '2-digit', minute: '2-digit', hour12: false }}
                firstDay={1}
              />
            </div>
          )}
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Próximos</CardTitle>
          </CardHeader>
          <CardContent>
            {upcoming.isPending ? (
              <p className="text-sm text-muted-foreground">Cargando…</p>
            ) : upcoming.isError ? (
              <p className="text-sm text-danger">{errorMessage(upcoming.error)}</p>
            ) : upcoming.data.length === 0 ? (
              <div className="text-sm text-muted-foreground">
                <p>No tienes eventos próximos.</p>
                <Button variant="link" onClick={() => openCreate()}>
                  Programar una entrevista
                </Button>
              </div>
            ) : (
              <ul className="space-y-3">
                {upcoming.data.map((event) => (
                  <li key={event.id}>
                    <button
                      type="button"
                      onClick={() => setSelected(event)}
                      className="flex w-full gap-3 rounded-lg p-1 text-left text-sm hover:bg-muted"
                    >
                      <span
                        className="mt-1.5 size-2 shrink-0 rounded-full"
                        style={{ backgroundColor: EVENT_TYPE_META[event.type].color }}
                        aria-hidden
                      />
                      <span className="min-w-0">
                        <span className="block truncate font-medium">{event.title}</span>
                        <span className="block text-xs text-muted-foreground">
                          {formatDateTime(event.startsAt, zone)}
                        </span>
                        {event.companyName ? (
                          <span className="block text-xs text-muted-foreground">{event.companyName}</span>
                        ) : null}
                      </span>
                    </button>
                  </li>
                ))}
              </ul>
            )}
          </CardContent>
        </Card>
      </div>

      <EventFormDialog open={formOpen} onOpenChange={setFormOpen} event={editing} draft={draft} />
      <EventDetailDialog
        event={selected}
        zone={zone}
        onClose={() => setSelected(null)}
        onEdit={(event) => {
          setSelected(null)
          setEditing(event)
          setDraft(undefined)
          setFormOpen(true)
        }}
      />
    </>
  )
}

function EventDetailDialog({
  event,
  zone,
  onClose,
  onEdit,
}: {
  event: CalendarEvent | null
  zone: string
  onClose: () => void
  onEdit: (event: CalendarEvent) => void
}) {
  const changeStatus = useChangeEventStatus()
  const remove = useDeleteEvent()
  const [confirmDelete, setConfirmDelete] = useState(false)

  if (!event) return null
  const meta = EVENT_TYPE_META[event.type]
  const otherZone = event.timeZone !== zone

  const setStatus = async (status: CalendarEvent['status']) => {
    try {
      await changeStatus.mutateAsync({ id: event.id, status })
      toast.success(
        status === 'cancelled'
          ? 'Evento cancelado'
          : status === 'completed'
            ? 'Evento completado'
            : 'Evento reprogramado',
      )
      onClose()
    } catch (error) {
      toast.error(errorMessage(error))
    }
  }

  const doDelete = async () => {
    try {
      await remove.mutateAsync(event.id)
      toast.success('Evento eliminado')
      setConfirmDelete(false)
      onClose()
    } catch (error) {
      toast.error(errorMessage(error))
    }
  }

  return (
    <>
      <Dialog open onOpenChange={(open) => !open && onClose()}>
        <DialogContent
          title={event.title}
          description={
            <span className="inline-flex items-center gap-2">
              <span className="size-2 rounded-full" style={{ backgroundColor: meta.color }} aria-hidden />
              {meta.label} · {EVENT_STATUS_LABELS[event.status]}
            </span>
          }
          footer={
            <>
              <Button variant="ghost" aria-label="Eliminar evento" onClick={() => setConfirmDelete(true)}>
                <Trash2 />
              </Button>
              {event.status === 'scheduled' ? (
                <>
                  <Button
                    variant="outline"
                    onClick={() => setStatus('cancelled')}
                    disabled={changeStatus.isPending}
                  >
                    <Ban /> Cancelar evento
                  </Button>
                  <Button
                    variant="outline"
                    onClick={() => setStatus('completed')}
                    disabled={changeStatus.isPending}
                  >
                    <CheckCircle2 /> Realizado
                  </Button>
                </>
              ) : (
                <Button
                  variant="outline"
                  onClick={() => setStatus('scheduled')}
                  disabled={changeStatus.isPending}
                >
                  Marcar como programado
                </Button>
              )}
              <Button onClick={() => onEdit(event)}>
                <Pencil /> Editar
              </Button>
            </>
          }
        >
          <dl className="space-y-3 text-sm">
            <div>
              <dt className="text-xs text-muted-foreground">Cuándo</dt>
              <dd className="font-medium">
                {formatDateTime(event.startsAt, zone)} – {formatTime(event.endsAt, zone)}
              </dd>
              {otherZone ? (
                <dd className="text-xs text-muted-foreground">
                  Programado en {event.timeZone}: {formatDateTime(event.startsAt, event.timeZone)} –{' '}
                  {formatTime(event.endsAt, event.timeZone)}
                </dd>
              ) : null}
            </div>
            {event.applicationId ? (
              <div>
                <dt className="text-xs text-muted-foreground">Postulación</dt>
                <dd>
                  <Link
                    to={`/applications/${event.applicationId}`}
                    className="font-medium text-primary hover:underline"
                  >
                    {event.applicationJobTitle}
                  </Link>
                  {event.companyName ? ` · ${event.companyName}` : ''}
                </dd>
              </div>
            ) : event.companyName ? (
              <div>
                <dt className="text-xs text-muted-foreground">Empresa</dt>
                <dd>
                  <Link
                    to={`/companies/${event.companyId}`}
                    className="font-medium text-primary hover:underline"
                  >
                    {event.companyName}
                  </Link>
                </dd>
              </div>
            ) : null}
            {event.location ? (
              <div className="flex items-center gap-2">
                <MapPin className="size-4 text-muted-foreground" aria-hidden />
                <dt className="sr-only">Ubicación</dt>
                <dd>{event.location}</dd>
              </div>
            ) : null}
            {event.meetingUrl ? (
              <div className="flex items-center gap-2">
                <Video className="size-4 text-muted-foreground" aria-hidden />
                <dt className="sr-only">Videollamada</dt>
                <dd>
                  <a
                    href={event.meetingUrl}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="text-primary hover:underline"
                  >
                    Unirse a la videollamada
                  </a>
                </dd>
              </div>
            ) : null}
            {event.notes ? (
              <div>
                <dt className="text-xs text-muted-foreground">Notas</dt>
                <dd className="whitespace-pre-line">{event.notes}</dd>
              </div>
            ) : null}
            {event.status !== 'scheduled' ? (
              <Badge className="bg-muted text-muted-foreground">{EVENT_STATUS_LABELS[event.status]}</Badge>
            ) : null}
          </dl>
        </DialogContent>
      </Dialog>
      <ConfirmDialog
        open={confirmDelete}
        onOpenChange={setConfirmDelete}
        title="¿Eliminar evento?"
        description="Se eliminará definitivamente. Si solo no se va a celebrar, mejor cancélalo para conservar el registro."
        confirmLabel="Eliminar"
        pending={remove.isPending}
        onConfirm={doDelete}
      />
    </>
  )
}
