import { zodResolver } from '@hookform/resolvers/zod'
import { useEffect } from 'react'
import { useForm, useWatch } from 'react-hook-form'
import { useNavigate } from 'react-router'
import { toast } from 'sonner'
import { z } from 'zod'
import { FormField } from '@/components/form-field'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent } from '@/components/ui/dialog'
import { Input, Select, Textarea } from '@/components/ui/form-controls'
import { useCurrentUser } from '@/features/auth/auth-context'
import { useJobs } from '@/features/jobs/api'
import { ResumeSelect } from '@/features/cv-analysis/ResumeSelect'
import { ApiError, errorMessage } from '@/lib/api-client'
import { todayIn } from '@/lib/dates'
import { SOURCE_LABELS, STATUS_META } from '@/lib/labels'
import { emptyToNull } from '@/lib/utils'
import type { ApplicationSource, ApplicationStatus } from '@/types/api'
import { useCreateApplication } from './api'

const INITIAL_STATUSES: ApplicationStatus[] = [
  'saved',
  'toApply',
  'applied',
  'screening',
  'interview',
  'offer',
]
const SENT: ApplicationStatus[] = ['applied', 'screening', 'interview', 'offer']

const schema = z.object({
  jobOfferId: z.string().min(1, 'Selecciona una oferta.'),
  status: z.enum(['saved', 'toApply', 'applied', 'screening', 'interview', 'offer']),
  appliedOn: z.string(),
  source: z.string(),
  resumeId: z.string(),
  nextAction: z.string().trim().max(300),
  followUpOn: z.string(),
  notes: z.string().trim().max(10000),
  coverLetter: z.string().trim().max(20000),
})

type FormValues = z.infer<typeof schema>

interface Props {
  open: boolean
  onOpenChange: (open: boolean) => void
  /** When set, the offer is fixed (e.g. opened from the offer page). */
  jobOfferId?: string
  jobTitle?: string
}

export function CreateApplicationDialog({ open, onOpenChange, jobOfferId, jobTitle }: Props) {
  const user = useCurrentUser()
  const navigate = useNavigate()
  const create = useCreateApplication()
  // Only offers without an application can be chosen.
  const offers = useJobs(
    { hasApplication: false, pageSize: 100, sort: 'savedAt', desc: true },
    open && !jobOfferId,
  )

  const defaults: FormValues = {
    jobOfferId: jobOfferId ?? '',
    status: 'applied',
    appliedOn: todayIn(user.timeZone),
    source: '',
    resumeId: '',
    nextAction: '',
    followUpOn: '',
    notes: '',
    coverLetter: '',
  }
  const {
    register,
    control,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({ resolver: zodResolver(schema), defaultValues: defaults })
  const status = useWatch({ control, name: 'status' })

  useEffect(() => {
    if (open) reset(defaults)
    // eslint-disable-next-line react-hooks/exhaustive-deps -- reset only when the dialog opens
  }, [open, jobOfferId])

  const onSubmit = handleSubmit(async (values) => {
    try {
      const created = await create.mutateAsync({
        jobOfferId: values.jobOfferId,
        status: values.status,
        appliedOn: SENT.includes(values.status) && values.appliedOn ? values.appliedOn : null,
        resumeId: values.resumeId || null,
        source: (values.source || null) as ApplicationSource | null,
        nextAction: emptyToNull(values.nextAction),
        followUpOn: values.followUpOn || null,
        notes: emptyToNull(values.notes),
        coverLetter: emptyToNull(values.coverLetter),
      })
      toast.success('Postulación creada')
      onOpenChange(false)
      navigate(`/applications/${created.summary.id}`)
    } catch (error) {
      toast.error(
        error instanceof ApiError && error.code === 'application.duplicate'
          ? error.message
          : errorMessage(error),
      )
    }
  })

  const offerList = offers.data?.items ?? []

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent
        size="lg"
        title="Nueva postulación"
        description={
          jobTitle ? `Para la oferta «${jobTitle}».` : 'Vincula la postulación a una oferta guardada.'
        }
        footer={
          <>
            <Button variant="outline" onClick={() => onOpenChange(false)}>
              Cancelar
            </Button>
            <Button type="submit" form="application-form" disabled={isSubmitting}>
              {isSubmitting ? 'Creando…' : 'Crear postulación'}
            </Button>
          </>
        }
      >
        <form id="application-form" onSubmit={onSubmit} noValidate className="grid gap-4 sm:grid-cols-2">
          {jobOfferId ? null : (
            <FormField
              label="Oferta"
              error={errors.jobOfferId?.message}
              required
              className="sm:col-span-2"
              hint={
                !offers.isPending && offerList.length === 0
                  ? 'No hay ofertas sin postulación. Guarda primero una oferta en «Ofertas».'
                  : undefined
              }
            >
              <Select {...register('jobOfferId')} disabled={offers.isPending}>
                <option value="">{offers.isPending ? 'Cargando…' : 'Selecciona una oferta…'}</option>
                {offerList.map((offer) => (
                  <option key={offer.id} value={offer.id}>
                    {offer.title} — {offer.companyName}
                  </option>
                ))}
              </Select>
            </FormField>
          )}
          <FormField label="Estado inicial" required>
            <Select {...register('status')}>
              {INITIAL_STATUSES.map((s) => (
                <option key={s} value={s}>
                  {STATUS_META[s].label}
                </option>
              ))}
            </Select>
          </FormField>
          <FormField
            label="Fecha de postulación"
            hint={SENT.includes(status) ? undefined : 'Se fijará al postular.'}
          >
            <Input type="date" disabled={!SENT.includes(status)} {...register('appliedOn')} />
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
          <FormField label="CV utilizado">
            <ResumeSelect {...register('resumeId')} />
          </FormField>
          <FormField label="Próxima acción" error={errors.nextAction?.message}>
            <Input placeholder="p. ej. Escribir al reclutador" {...register('nextAction')} />
          </FormField>
          <FormField label="Fecha de seguimiento">
            <Input type="date" {...register('followUpOn')} />
          </FormField>
          <FormField label="Notas" error={errors.notes?.message} className="sm:col-span-2">
            <Textarea rows={3} {...register('notes')} />
          </FormField>
          <FormField
            label="Carta de presentación"
            error={errors.coverLetter?.message}
            className="sm:col-span-2"
          >
            <Textarea rows={4} {...register('coverLetter')} />
          </FormField>
        </form>
      </DialogContent>
    </Dialog>
  )
}
