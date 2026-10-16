import { BellRing, Check, MoreHorizontal, Pencil, Plus, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { Link } from 'react-router'
import { toast } from 'sonner'
import { EmptyState, ErrorState, ListSkeleton, PageHeader, Pagination } from '@/components/page'
import { Button } from '@/components/ui/button'
import { ConfirmDialog } from '@/components/ui/dialog'
import { Badge, Card } from '@/components/ui/display'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { useCurrentUser } from '@/features/auth/auth-context'
import { errorMessage } from '@/lib/api-client'
import { formatDateTime, formatRelative, isPast } from '@/lib/dates'
import { PRIORITY_META } from '@/lib/labels'
import { cn } from '@/lib/utils'
import type { Reminder } from '@/types/api'
import { useDeleteReminder, useReminders, useToggleReminder, type ReminderQuery } from './api'
import { ReminderFormDialog } from './ReminderFormDialog'

type Scope = NonNullable<ReminderQuery['status']>

export function RemindersPage() {
  const user = useCurrentUser()
  const [scope, setScope] = useState<Scope>('pending')
  const [page, setPage] = useState(1)
  const query = useReminders({ status: scope, page, pageSize: 20 })
  const toggle = useToggleReminder()
  const remove = useDeleteReminder()
  const [formOpen, setFormOpen] = useState(false)
  const [editing, setEditing] = useState<Reminder | undefined>()
  const [deleting, setDeleting] = useState<Reminder | undefined>()

  const onToggle = (reminder: Reminder) =>
    toggle.mutate(
      { id: reminder.id, completed: !reminder.isCompleted },
      {
        onSuccess: () =>
          toast.success(reminder.isCompleted ? 'Recordatorio reabierto' : 'Recordatorio completado'),
        onError: (error) => toast.error(errorMessage(error)),
      },
    )

  const confirmDelete = async () => {
    if (!deleting) return
    try {
      await remove.mutateAsync(deleting.id)
      toast.success('Recordatorio eliminado')
    } catch (error) {
      toast.error(errorMessage(error))
    } finally {
      setDeleting(undefined)
    }
  }

  const items = query.data?.items ?? []

  return (
    <>
      <PageHeader
        title="Recordatorios"
        description="Tareas con fecha: preparar entrevistas, contactar reclutadores, enviar documentación…"
        actions={
          <Button
            onClick={() => {
              setEditing(undefined)
              setFormOpen(true)
            }}
          >
            <Plus /> Nuevo recordatorio
          </Button>
        }
      />
      <p className="-mt-3 mb-4 text-xs text-muted-foreground">
        Los recordatorios se muestran dentro de JobTrack (aquí, en el panel y en cada postulación). Todavía no
        se envían notificaciones por correo ni push.
      </p>

      <Tabs value={scope} onValueChange={(v) => (setScope(v as Scope), setPage(1))} className="mb-4">
        <TabsList aria-label="Filtrar recordatorios">
          <TabsTrigger value="pending">Pendientes</TabsTrigger>
          <TabsTrigger value="completed">Completados</TabsTrigger>
          <TabsTrigger value="all">Todos</TabsTrigger>
        </TabsList>
      </Tabs>

      {query.isPending ? (
        <ListSkeleton />
      ) : query.isError ? (
        <ErrorState error={query.error} onRetry={() => query.refetch()} />
      ) : items.length === 0 ? (
        <EmptyState
          icon={BellRing}
          title={scope === 'completed' ? 'Nada completado todavía' : 'No tienes recordatorios pendientes'}
          description="Crea recordatorios para no olvidar seguimientos y preparativos."
          action={
            scope === 'completed' ? undefined : (
              <Button onClick={() => setFormOpen(true)}>
                <Plus /> Nuevo recordatorio
              </Button>
            )
          }
        />
      ) : (
        <>
          <Card className="divide-y divide-border">
            {items.map((reminder) => {
              const overdue = !reminder.isCompleted && isPast(reminder.dueAt)
              return (
                <div key={reminder.id} className="flex items-start gap-3 p-4">
                  <button
                    type="button"
                    aria-label={
                      reminder.isCompleted
                        ? `Reabrir «${reminder.title}»`
                        : `Marcar «${reminder.title}» como completado`
                    }
                    aria-pressed={reminder.isCompleted}
                    onClick={() => onToggle(reminder)}
                    className={cn(
                      'mt-0.5 grid size-6 shrink-0 place-items-center rounded-full border-2 transition-colors',
                      reminder.isCompleted
                        ? 'border-primary bg-primary text-white'
                        : 'border-border hover:border-primary',
                    )}
                  >
                    {reminder.isCompleted ? <Check className="size-3.5" aria-hidden /> : null}
                  </button>
                  <div className="min-w-0 flex-1">
                    <p
                      className={cn(
                        'font-medium',
                        reminder.isCompleted && 'text-muted-foreground line-through',
                      )}
                    >
                      {reminder.title}
                    </p>
                    {reminder.description ? (
                      <p className="text-sm text-muted-foreground">{reminder.description}</p>
                    ) : null}
                    <p
                      className={cn(
                        'mt-1 text-xs',
                        overdue ? 'font-semibold text-danger' : 'text-muted-foreground',
                      )}
                    >
                      {overdue ? 'Vencido · ' : ''}
                      {formatDateTime(reminder.dueAt, user.timeZone)}
                      {reminder.isCompleted && reminder.completedAt
                        ? ` · completado ${formatRelative(reminder.completedAt)}`
                        : ''}
                    </p>
                    {reminder.applicationId ? (
                      <Link
                        to={`/applications/${reminder.applicationId}`}
                        className="mt-1 inline-block text-xs font-medium text-primary hover:underline"
                      >
                        {reminder.applicationJobTitle} · {reminder.companyName}
                      </Link>
                    ) : null}
                  </div>
                  <Badge className={PRIORITY_META[reminder.priority].className}>
                    Prioridad {PRIORITY_META[reminder.priority].label.toLowerCase()}
                  </Badge>
                  <DropdownMenu>
                    <DropdownMenuTrigger asChild>
                      <Button variant="ghost" size="icon" aria-label={`Acciones para «${reminder.title}»`}>
                        <MoreHorizontal />
                      </Button>
                    </DropdownMenuTrigger>
                    <DropdownMenuContent align="end">
                      <DropdownMenuItem
                        onSelect={() => {
                          setEditing(reminder)
                          setFormOpen(true)
                        }}
                      >
                        <Pencil /> Editar
                      </DropdownMenuItem>
                      <DropdownMenuItem className="text-danger" onSelect={() => setDeleting(reminder)}>
                        <Trash2 /> Eliminar
                      </DropdownMenuItem>
                    </DropdownMenuContent>
                  </DropdownMenu>
                </div>
              )
            })}
          </Card>
          <Pagination
            page={query.data.page}
            totalPages={query.data.totalPages}
            totalCount={query.data.totalCount}
            onPageChange={setPage}
          />
        </>
      )}

      <ReminderFormDialog open={formOpen} onOpenChange={setFormOpen} reminder={editing} />
      <ConfirmDialog
        open={deleting !== undefined}
        onOpenChange={(open) => !open && setDeleting(undefined)}
        title="¿Eliminar recordatorio?"
        description={`Se eliminará «${deleting?.title}».`}
        confirmLabel="Eliminar"
        pending={remove.isPending}
        onConfirm={confirmDelete}
      />
    </>
  )
}
