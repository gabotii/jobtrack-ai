import { zodResolver } from '@hookform/resolvers/zod'
import { useEffect } from 'react'
import { useForm } from 'react-hook-form'
import { toast } from 'sonner'
import { z } from 'zod'
import { FormField } from '@/components/form-field'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent } from '@/components/ui/dialog'
import { Input, Select, Textarea } from '@/components/ui/form-controls'
import { useApplications } from '@/features/applications/api'
import { useCurrentUser } from '@/features/auth/auth-context'
import { errorMessage } from '@/lib/api-client'
import { fromInstant, toInstant, todayIn } from '@/lib/dates'
import { PRIORITY_META } from '@/lib/labels'
import { emptyToNull } from '@/lib/utils'
import type { Reminder, ReminderPriority } from '@/types/api'
import { useSaveReminder } from './api'

const schema = z.object({
  title: z.string().trim().min(1, 'El título es obligatorio.').max(200),
  description: z.string().trim().max(5000),
  date: z.string().min(1, 'Indica la fecha.'),
  time: z.string().min(1, 'Indica la hora.'),
  priority: z.enum(['low', 'medium', 'high']),
  applicationId: z.string(),
})

type FormValues = z.infer<typeof schema>

interface Props {
  open: boolean
  onOpenChange: (open: boolean) => void
  reminder?: Reminder
  applicationId?: string
  calendarEventId?: string
  defaultTitle?: string
}

export function ReminderFormDialog({
  open,
  onOpenChange,
  reminder,
  applicationId,
  calendarEventId,
  defaultTitle,
}: Props) {
  const user = useCurrentUser()
  const save = useSaveReminder(reminder?.id)
  const applications = useApplications({ pageSize: 100, sort: 'updated', desc: true })

  const defaults = (): FormValues => {
    const due = reminder
      ? fromInstant(reminder.dueAt, user.timeZone)
      : { date: todayIn(user.timeZone), time: '09:00' }
    return {
      title: reminder?.title ?? defaultTitle ?? '',
      description: reminder?.description ?? '',
      date: due.date,
      time: due.time,
      priority: reminder?.priority ?? 'medium',
      applicationId: reminder?.applicationId ?? applicationId ?? '',
    }
  }
  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({ resolver: zodResolver(schema), defaultValues: defaults() })

  useEffect(() => {
    if (open) reset(defaults())
    // eslint-disable-next-line react-hooks/exhaustive-deps -- reset only when the dialog opens or its target changes
  }, [open, reminder, applicationId])

  const onSubmit = handleSubmit(async (values) => {
    try {
      await save.mutateAsync({
        title: values.title,
        description: emptyToNull(values.description),
        dueAt: toInstant(values.date, values.time, user.timeZone),
        priority: values.priority as ReminderPriority,
        applicationId: values.applicationId || null,
        calendarEventId: reminder?.calendarEventId ?? calendarEventId ?? null,
      })
      toast.success(reminder ? 'Recordatorio actualizado' : 'Recordatorio creado')
      onOpenChange(false)
    } catch (error) {
      toast.error(errorMessage(error))
    }
  })

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent
        title={reminder ? 'Editar recordatorio' : 'Nuevo recordatorio'}
        description={`Fecha y hora en tu zona horaria (${user.timeZone}). Los avisos se muestran dentro de la aplicación.`}
        footer={
          <>
            <Button variant="outline" onClick={() => onOpenChange(false)}>
              Cancelar
            </Button>
            <Button type="submit" form="reminder-form" disabled={isSubmitting}>
              {isSubmitting ? 'Guardando…' : 'Guardar'}
            </Button>
          </>
        }
      >
        <form id="reminder-form" onSubmit={onSubmit} noValidate className="grid gap-4 sm:grid-cols-2">
          <FormField label="Título" error={errors.title?.message} required className="sm:col-span-2">
            <Input autoFocus placeholder="p. ej. Preparar la entrevista técnica" {...register('title')} />
          </FormField>
          <FormField label="Fecha" error={errors.date?.message} required>
            <Input type="date" {...register('date')} />
          </FormField>
          <FormField label="Hora" error={errors.time?.message} required>
            <Input type="time" {...register('time')} />
          </FormField>
          <FormField label="Prioridad">
            <Select {...register('priority')}>
              {Object.entries(PRIORITY_META).map(([value, meta]) => (
                <option key={value} value={value}>
                  {meta.label}
                </option>
              ))}
            </Select>
          </FormField>
          <FormField label="Postulación">
            <Select {...register('applicationId')}>
              <option value="">Ninguna</option>
              {applications.data?.items.map((a) => (
                <option key={a.id} value={a.id}>
                  {a.jobTitle} — {a.companyName}
                </option>
              ))}
            </Select>
          </FormField>
          <FormField label="Descripción" error={errors.description?.message} className="sm:col-span-2">
            <Textarea rows={3} {...register('description')} />
          </FormField>
        </form>
      </DialogContent>
    </Dialog>
  )
}
