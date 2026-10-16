import { zodResolver } from '@hookform/resolvers/zod'
import {
  ArrowLeft,
  ArrowRightLeft,
  BellPlus,
  CalendarPlus,
  Check,
  ExternalLink,
  Trash2,
  Video,
} from 'lucide-react'
import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { Link, useNavigate, useParams } from 'react-router'
import { toast } from 'sonner'
import { z } from 'zod'
import { FormField } from '@/components/form-field'
import { ErrorState, PageHeader, StatusBadge } from '@/components/page'
import { Button } from '@/components/ui/button'
import { ConfirmDialog } from '@/components/ui/dialog'
import { Badge, Card, CardContent, CardHeader, CardTitle, Skeleton } from '@/components/ui/display'
import { Input, Select, Textarea } from '@/components/ui/form-controls'
import { useCurrentUser } from '@/features/auth/auth-context'
import { useUpcomingEvents } from '@/features/calendar/api'
import { EventFormDialog } from '@/features/calendar/EventFormDialog'
import { ResumeSelect } from '@/features/cv-analysis/ResumeSelect'
import { useReminders, useToggleReminder } from '@/features/reminders/api'
import { ReminderFormDialog } from '@/features/reminders/ReminderFormDialog'
import { errorMessage } from '@/lib/api-client'
import { formatDateTime, formatRelative, isPast } from '@/lib/dates'
import { EVENT_TYPE_META, PRIORITY_META, SOURCE_LABELS, STATUS_META } from '@/lib/labels'
import { cn, emptyToNull } from '@/lib/utils'
import type { ApplicationDetail, ApplicationSource } from '@/types/api'
import { useApplication, useDeleteApplication, useUpdateApplication } from './api'
import { ChangeStatusDialog } from './ChangeStatusDialog'

export function ApplicationDetailPage() {
  const { id = '' } = useParams()
  const navigate = useNavigate()
  const query = useApplication(id)
  const remove = useDeleteApplication()
  const [statusOpen, setStatusOpen] = useState(false)
  const [eventOpen, setEventOpen] = useState(false)
  const [reminderOpen, setReminderOpen] = useState(false)
  const [deleteOpen, setDeleteOpen] = useState(false)

  if (query.isPending) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-8 w-1/2" />
        <Skeleton className="h-72 w-full" />
      </div>
    )
  }
  if (query.isError) return <ErrorState error={query.error} onRetry={() => query.refetch()} />

  const detail = query.data
  const { summary } = detail

  const confirmDelete = async () => {
    try {
      await remove.mutateAsync(summary.id)
      toast.success('Postulación eliminada')
      navigate('/applications')
    } catch (error) {
      toast.error(errorMessage(error))
      setDeleteOpen(false)
    }
  }

  return (
    <>
      <Link
        to="/applications"
        className="mb-3 inline-flex items-center gap-1 text-sm text-muted-foreground hover:text-foreground"
      >
        <ArrowLeft className="size-4" aria-hidden /> Postulaciones
      </Link>
      <PageHeader
        title={summary.jobTitle}
        description={
          <span className="flex flex-wrap items-center gap-2">
            <Link to={`/companies/${summary.companyId}`} className="font-medium text-primary hover:underline">
              {summary.companyName}
            </Link>
            <StatusBadge status={summary.status} />
            <Link
              to={`/jobs/${summary.jobOfferId}`}
              className="inline-flex items-center gap-1 hover:underline"
            >
              Ver oferta <ExternalLink className="size-3" aria-hidden />
            </Link>
          </span>
        }
        actions={
          <>
            <Button onClick={() => setStatusOpen(true)} disabled={summary.allowedTransitions.length === 0}>
              <ArrowRightLeft /> Cambiar estado
            </Button>
            <Button variant="outline" onClick={() => setEventOpen(true)}>
              <CalendarPlus /> Programar
            </Button>
            <Button variant="outline" onClick={() => setReminderOpen(true)}>
              <BellPlus /> Recordatorio
            </Button>
            <Button variant="ghost" aria-label="Eliminar postulación" onClick={() => setDeleteOpen(true)}>
              <Trash2 />
            </Button>
          </>
        }
      />

      {detail.rejectionReason ? (
        <p className="mb-4 rounded-lg bg-danger-soft px-4 py-3 text-sm">
          <strong className="text-danger">Motivo del rechazo:</strong> {detail.rejectionReason}
        </p>
      ) : null}

      <div className="grid gap-4 xl:grid-cols-[1fr_24rem]">
        <ApplicationForm detail={detail} />
        <div className="space-y-4">
          <EventsCard applicationId={summary.id} onCreate={() => setEventOpen(true)} />
          <RemindersCard applicationId={summary.id} onCreate={() => setReminderOpen(true)} />
          <HistoryCard detail={detail} />
        </div>
      </div>

      <ChangeStatusDialog open={statusOpen} onOpenChange={setStatusOpen} application={summary} />
      <EventFormDialog
        open={eventOpen}
        onOpenChange={setEventOpen}
        draft={{ applicationId: summary.id, title: `Entrevista — ${summary.companyName}`, type: 'interview' }}
      />
      <ReminderFormDialog open={reminderOpen} onOpenChange={setReminderOpen} applicationId={summary.id} />
      <ConfirmDialog
        open={deleteOpen}
        onOpenChange={setDeleteOpen}
        title="¿Eliminar postulación?"
        description="Se eliminará la postulación y su historial de estados. La oferta se conserva y los eventos y recordatorios vinculados quedarán sin postulación."
        confirmLabel="Eliminar"
        pending={remove.isPending}
        onConfirm={confirmDelete}
      />
    </>
  )
}

const formSchema = z.object({
  appliedOn: z.string(),
  source: z.string(),
  resumeId: z.string(),
  nextAction: z.string().trim().max(300),
  followUpOn: z.string(),
  notes: z.string().trim().max(10000),
  coverLetter: z.string().trim().max(20000),
})
type FormValues = z.infer<typeof formSchema>

function toFormValues(detail: ApplicationDetail): FormValues {
  return {
    appliedOn: detail.summary.appliedOn ?? '',
    source: detail.source ?? '',
    resumeId: detail.resumeId ?? '',
    nextAction: detail.summary.nextAction ?? '',
    followUpOn: detail.summary.followUpOn ?? '',
    notes: detail.notes ?? '',
    coverLetter: detail.coverLetter ?? '',
  }
}

function ApplicationForm({ detail }: { detail: ApplicationDetail }) {
  const update = useUpdateApplication(detail.summary.id)
  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isDirty, isSubmitting },
  } = useForm<FormValues>({ resolver: zodResolver(formSchema), defaultValues: toFormValues(detail) })

  useEffect(() => reset(toFormValues(detail)), [detail, reset])

  const onSubmit = handleSubmit(async (values) => {
    try {
      await update.mutateAsync({
        appliedOn: values.appliedOn || null,
        source: (values.source || null) as ApplicationSource | null,
        resumeId: values.resumeId || null,
        nextAction: emptyToNull(values.nextAction),
        followUpOn: values.followUpOn || null,
        notes: emptyToNull(values.notes),
        coverLetter: emptyToNull(values.coverLetter),
      })
      toast.success('Cambios guardados')
    } catch (error) {
      toast.error(errorMessage(error))
    }
  })

  return (
    <Card>
      <CardHeader>
        <CardTitle>Detalles</CardTitle>
      </CardHeader>
      <CardContent>
        <form onSubmit={onSubmit} noValidate className="grid gap-4 sm:grid-cols-2">
          <FormField label="Fecha de postulación">
            <Input type="date" {...register('appliedOn')} />
          </FormField>
          <FormField label="Fuente">
            <Select {...register('source')}>
              <option value="">Sin especificar</option>
              {Object.entries(SOURCE_LABELS).map(([value, label]) => (
                <option key={value} value={value}>
                  {label}
                </option>
              ))}
            </Select>
          </FormField>
          <FormField label="Próxima acción" error={errors.nextAction?.message}>
            <Input {...register('nextAction')} />
          </FormField>
          <FormField label="Fecha de seguimiento">
            <Input type="date" {...register('followUpOn')} />
          </FormField>
          <FormField label="CV utilizado" className="sm:col-span-2">
            <ResumeSelect {...register('resumeId')} />
          </FormField>
          <FormField label="Notas" error={errors.notes?.message} className="sm:col-span-2">
            <Textarea rows={4} {...register('notes')} />
          </FormField>
          <FormField
            label="Carta de presentación"
            error={errors.coverLetter?.message}
            className="sm:col-span-2"
          >
            <Textarea rows={6} {...register('coverLetter')} />
          </FormField>
          <div className="flex justify-end gap-2 sm:col-span-2">
            <Button variant="outline" disabled={!isDirty} onClick={() => reset(toFormValues(detail))}>
              Descartar
            </Button>
            <Button type="submit" disabled={!isDirty || isSubmitting}>
              {isSubmitting ? 'Guardando…' : 'Guardar cambios'}
            </Button>
          </div>
        </form>
      </CardContent>
    </Card>
  )
}

function EventsCard({ applicationId, onCreate }: { applicationId: string; onCreate: () => void }) {
  const user = useCurrentUser()
  const events = useUpcomingEvents(applicationId, 10)
  return (
    <Card>
      <CardHeader>
        <CardTitle>Próximos eventos</CardTitle>
        <Button variant="link" size="sm" onClick={onCreate}>
          Añadir
        </Button>
      </CardHeader>
      <CardContent>
        {events.isPending ? (
          <Skeleton className="h-12 w-full" />
        ) : events.isError ? (
          <p className="text-sm text-danger">{errorMessage(events.error)}</p>
        ) : events.data.length === 0 ? (
          <p className="text-sm text-muted-foreground">No hay eventos programados.</p>
        ) : (
          <ul className="space-y-3">
            {events.data.map((event) => {
              const meta = EVENT_TYPE_META[event.type]
              return (
                <li key={event.id} className="flex gap-3 text-sm">
                  <span
                    className="mt-1 size-2.5 shrink-0 rounded-full"
                    style={{ backgroundColor: meta.color }}
                    aria-hidden
                  />
                  <div className="min-w-0">
                    <p className="font-medium">{event.title}</p>
                    <p className="text-muted-foreground">
                      {meta.label} · {formatDateTime(event.startsAt, user.timeZone)}
                    </p>
                    {event.meetingUrl ? (
                      <a
                        href={event.meetingUrl}
                        target="_blank"
                        rel="noopener noreferrer"
                        className="inline-flex items-center gap-1 text-primary hover:underline"
                      >
                        <Video className="size-3.5" aria-hidden /> Unirse
                      </a>
                    ) : null}
                  </div>
                </li>
              )
            })}
          </ul>
        )}
        <Link to="/calendar" className="mt-3 inline-block text-xs font-medium text-primary hover:underline">
          Abrir calendario
        </Link>
      </CardContent>
    </Card>
  )
}

function RemindersCard({ applicationId, onCreate }: { applicationId: string; onCreate: () => void }) {
  const user = useCurrentUser()
  const reminders = useReminders({ applicationId, status: 'all', pageSize: 20 })
  const toggle = useToggleReminder()
  return (
    <Card>
      <CardHeader>
        <CardTitle>Recordatorios</CardTitle>
        <Button variant="link" size="sm" onClick={onCreate}>
          Añadir
        </Button>
      </CardHeader>
      <CardContent>
        {reminders.isPending ? (
          <Skeleton className="h-12 w-full" />
        ) : reminders.isError ? (
          <p className="text-sm text-danger">{errorMessage(reminders.error)}</p>
        ) : reminders.data.items.length === 0 ? (
          <p className="text-sm text-muted-foreground">Sin recordatorios.</p>
        ) : (
          <ul className="space-y-2">
            {reminders.data.items.map((reminder) => {
              const overdue = !reminder.isCompleted && isPast(reminder.dueAt)
              return (
                <li key={reminder.id} className="flex items-start gap-3 text-sm">
                  <button
                    type="button"
                    aria-label={
                      reminder.isCompleted ? `Reabrir «${reminder.title}»` : `Completar «${reminder.title}»`
                    }
                    aria-pressed={reminder.isCompleted}
                    onClick={() =>
                      toggle.mutate(
                        { id: reminder.id, completed: !reminder.isCompleted },
                        { onError: (error) => toast.error(errorMessage(error)) },
                      )
                    }
                    className={cn(
                      'mt-0.5 grid size-5 shrink-0 place-items-center rounded-full border',
                      reminder.isCompleted
                        ? 'border-primary bg-primary text-white'
                        : 'border-border hover:border-primary',
                    )}
                  >
                    {reminder.isCompleted ? <Check className="size-3" aria-hidden /> : null}
                  </button>
                  <div className="min-w-0">
                    <p
                      className={cn(
                        'font-medium',
                        reminder.isCompleted && 'text-muted-foreground line-through',
                      )}
                    >
                      {reminder.title}
                    </p>
                    <p
                      className={cn(
                        'text-xs',
                        overdue ? 'font-semibold text-danger' : 'text-muted-foreground',
                      )}
                    >
                      {overdue ? 'Vencido · ' : ''}
                      {formatDateTime(reminder.dueAt, user.timeZone)}
                    </p>
                  </div>
                  <Badge className={cn('ml-auto', PRIORITY_META[reminder.priority].className)}>
                    {PRIORITY_META[reminder.priority].label}
                  </Badge>
                </li>
              )
            })}
          </ul>
        )}
      </CardContent>
    </Card>
  )
}

function HistoryCard({ detail }: { detail: ApplicationDetail }) {
  return (
    <Card>
      <CardHeader>
        <CardTitle>Historial de estados</CardTitle>
      </CardHeader>
      <CardContent>
        <ol className="relative space-y-4 border-l border-border pl-5">
          {detail.history.map((entry) => {
            const Icon = STATUS_META[entry.toStatus].icon
            return (
              <li key={entry.id} className="relative text-sm">
                <span
                  className="absolute top-0.5 -left-[1.95rem] grid size-5 place-items-center rounded-full bg-surface ring-2 ring-border"
                  aria-hidden
                >
                  <Icon className="size-3" style={{ color: STATUS_META[entry.toStatus].color }} />
                </span>
                <p className="font-medium">
                  {entry.fromStatus
                    ? `${STATUS_META[entry.fromStatus].label} → ${STATUS_META[entry.toStatus].label}`
                    : `Creada como «${STATUS_META[entry.toStatus].label}»`}
                </p>
                <p className="text-xs text-muted-foreground">{formatRelative(entry.changedAt)}</p>
                {entry.note ? <p className="mt-1 text-muted-foreground italic">“{entry.note}”</p> : null}
              </li>
            )
          })}
        </ol>
      </CardContent>
    </Card>
  )
}
