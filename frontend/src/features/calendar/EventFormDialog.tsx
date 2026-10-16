import { zodResolver } from '@hookform/resolvers/zod'
import { useEffect } from 'react'
import { useForm, useWatch } from 'react-hook-form'
import { toast } from 'sonner'
import { z } from 'zod'
import { FormField } from '@/components/form-field'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent } from '@/components/ui/dialog'
import { Input, Select, Textarea } from '@/components/ui/form-controls'
import { useApplications } from '@/features/applications/api'
import { useCurrentUser } from '@/features/auth/auth-context'
import { useCompanyOptions } from '@/features/companies/api'
import { errorMessage } from '@/lib/api-client'
import { fromInstant, supportedTimeZones, toInstant, todayIn } from '@/lib/dates'
import { EVENT_TYPE_META } from '@/lib/labels'
import { emptyToNull } from '@/lib/utils'
import type { CalendarEvent, CalendarEventType } from '@/types/api'
import { useSaveEvent } from './api'

const schema = z
  .object({
    title: z.string().trim().min(1, 'El título es obligatorio.').max(200),
    type: z.enum(['interview', 'call', 'followUp', 'reminder', 'other']),
    date: z.string().min(1, 'Indica la fecha.'),
    startTime: z.string().min(1, 'Indica la hora de inicio.'),
    endDate: z.string().min(1, 'Indica la fecha de fin.'),
    endTime: z.string().min(1, 'Indica la hora de fin.'),
    timeZone: z.string().min(1),
    applicationId: z.string(),
    companyId: z.string(),
    location: z.string().trim().max(300),
    meetingUrl: z
      .string()
      .trim()
      .max(2048)
      .refine((v) => v === '' || /^https?:\/\/\S+$/i.test(v), 'Debe empezar por http:// o https://'),
    notes: z.string().trim().max(10000),
  })
  // Same zone on both ends, so comparing wall-clock strings is enough.
  .refine((v) => `${v.endDate}T${v.endTime}` > `${v.date}T${v.startTime}`, {
    path: ['endTime'],
    message: 'El fin debe ser posterior al inicio.',
  })

type FormValues = z.infer<typeof schema>

export interface EventDraft {
  date?: string
  startTime?: string
  endDate?: string
  endTime?: string
  applicationId?: string
  companyId?: string
  title?: string
  type?: CalendarEventType
}

function toFormValues(userZone: string, event?: CalendarEvent, draft?: EventDraft): FormValues {
  if (event) {
    const start = fromInstant(event.startsAt, event.timeZone)
    const end = fromInstant(event.endsAt, event.timeZone)
    return {
      title: event.title,
      type: event.type,
      date: start.date,
      startTime: start.time,
      endDate: end.date,
      endTime: end.time,
      timeZone: event.timeZone,
      applicationId: event.applicationId ?? '',
      companyId: event.companyId ?? '',
      location: event.location ?? '',
      meetingUrl: event.meetingUrl ?? '',
      notes: event.notes ?? '',
    }
  }
  return {
    title: draft?.title ?? '',
    type: draft?.type ?? 'interview',
    date: draft?.date ?? todayIn(userZone),
    startTime: draft?.startTime ?? '10:00',
    endDate: draft?.endDate ?? draft?.date ?? todayIn(userZone),
    endTime: draft?.endTime ?? '11:00',
    timeZone: userZone,
    applicationId: draft?.applicationId ?? '',
    companyId: draft?.companyId ?? '',
    location: '',
    meetingUrl: '',
    notes: '',
  }
}

interface Props {
  open: boolean
  onOpenChange: (open: boolean) => void
  event?: CalendarEvent
  draft?: EventDraft
}

export function EventFormDialog({ open, onOpenChange, event, draft }: Props) {
  const user = useCurrentUser()
  const save = useSaveEvent(event?.id)
  const companies = useCompanyOptions()
  const applications = useApplications({ pageSize: 100, sort: 'updated', desc: true })
  const {
    register,
    control,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: toFormValues(user.timeZone, event, draft),
  })
  const applicationId = useWatch({ control, name: 'applicationId' })

  useEffect(() => {
    if (open) reset(toFormValues(user.timeZone, event, draft))
  }, [open, event, draft, user.timeZone, reset])

  const onSubmit = handleSubmit(async (values) => {
    try {
      await save.mutateAsync({
        title: values.title,
        type: values.type,
        startsAt: toInstant(values.date, values.startTime, values.timeZone),
        endsAt: toInstant(values.endDate, values.endTime, values.timeZone),
        timeZone: values.timeZone,
        applicationId: values.applicationId || null,
        // The API derives the company from the application; only send it for unlinked events.
        companyId: values.applicationId ? null : values.companyId || null,
        location: emptyToNull(values.location),
        meetingUrl: emptyToNull(values.meetingUrl),
        notes: emptyToNull(values.notes),
      })
      toast.success(event ? 'Evento actualizado' : 'Evento programado')
      onOpenChange(false)
    } catch (error) {
      toast.error(errorMessage(error))
    }
  })

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent
        size="lg"
        title={event ? 'Editar evento' : 'Nuevo evento'}
        description="La hora se guarda en la zona horaria indicada y se muestra correctamente en cualquier otra."
        footer={
          <>
            <Button variant="outline" onClick={() => onOpenChange(false)}>
              Cancelar
            </Button>
            <Button type="submit" form="event-form" disabled={isSubmitting}>
              {isSubmitting ? 'Guardando…' : 'Guardar'}
            </Button>
          </>
        }
      >
        <form id="event-form" onSubmit={onSubmit} noValidate className="grid gap-4 sm:grid-cols-6">
          <FormField label="Título" error={errors.title?.message} required className="sm:col-span-4">
            <Input autoFocus {...register('title')} />
          </FormField>
          <FormField label="Tipo" className="sm:col-span-2">
            <Select {...register('type')}>
              {Object.entries(EVENT_TYPE_META).map(([value, meta]) => (
                <option key={value} value={value}>
                  {meta.label}
                </option>
              ))}
            </Select>
          </FormField>
          <FormField label="Fecha de inicio" error={errors.date?.message} required className="sm:col-span-3">
            <Input type="date" {...register('date')} />
          </FormField>
          <FormField
            label="Hora de inicio"
            error={errors.startTime?.message}
            required
            className="sm:col-span-3"
          >
            <Input type="time" {...register('startTime')} />
          </FormField>
          <FormField label="Fecha de fin" error={errors.endDate?.message} required className="sm:col-span-3">
            <Input type="date" {...register('endDate')} />
          </FormField>
          <FormField label="Hora de fin" error={errors.endTime?.message} required className="sm:col-span-3">
            <Input type="time" {...register('endTime')} />
          </FormField>
          <FormField label="Zona horaria" hint="Por defecto, la de tu perfil." className="sm:col-span-6">
            <Select {...register('timeZone')}>
              {supportedTimeZones().map((zone) => (
                <option key={zone} value={zone}>
                  {zone.replaceAll('_', ' ')}
                </option>
              ))}
            </Select>
          </FormField>
          <FormField label="Postulación relacionada" className="sm:col-span-3">
            <Select {...register('applicationId')}>
              <option value="">Ninguna</option>
              {applications.data?.items.map((a) => (
                <option key={a.id} value={a.id}>
                  {a.jobTitle} — {a.companyName}
                </option>
              ))}
            </Select>
          </FormField>
          <FormField
            label="Empresa"
            hint={applicationId ? 'Se usa la empresa de la postulación.' : undefined}
            className="sm:col-span-3"
          >
            <Select {...register('companyId')} disabled={Boolean(applicationId)}>
              <option value="">Ninguna</option>
              {companies.data?.items.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.name}
                </option>
              ))}
            </Select>
          </FormField>
          <FormField label="Ubicación" error={errors.location?.message} className="sm:col-span-3">
            <Input placeholder="Oficina, dirección…" {...register('location')} />
          </FormField>
          <FormField
            label="Enlace de videollamada"
            error={errors.meetingUrl?.message}
            className="sm:col-span-3"
          >
            <Input type="url" placeholder="https://" {...register('meetingUrl')} />
          </FormField>
          <FormField label="Notas" error={errors.notes?.message} className="sm:col-span-6">
            <Textarea rows={3} {...register('notes')} />
          </FormField>
        </form>
      </DialogContent>
    </Dialog>
  )
}
