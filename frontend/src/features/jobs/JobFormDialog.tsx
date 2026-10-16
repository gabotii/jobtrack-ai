import { zodResolver } from '@hookform/resolvers/zod'
import { Plus } from 'lucide-react'
import { useEffect, useState } from 'react'
import { Controller, useForm } from 'react-hook-form'
import { toast } from 'sonner'
import { z } from 'zod'
import { FormField } from '@/components/form-field'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent } from '@/components/ui/dialog'
import { Input, Select, Textarea } from '@/components/ui/form-controls'
import { CompanyFormDialog } from '@/features/companies/CompanyFormDialog'
import { useCompanyOptions } from '@/features/companies/api'
import { errorMessage } from '@/lib/api-client'
import { CONTRACT_TYPE_LABELS, WORK_MODE_LABELS } from '@/lib/labels'
import { emptyToNull, splitList } from '@/lib/utils'
import type { ContractType, JobOffer, WorkMode } from '@/types/api'
import { useSaveJob } from './api'

const money = z
  .string()
  .trim()
  .refine(
    (v) => v === '' || (/^\d+([.,]\d{1,2})?$/.test(v) && Number(v.replace(',', '.')) < 1e9),
    'Importe no válido.',
  )

const skills = z
  .string()
  .refine((v) => splitList(v).length <= 50, 'Máximo 50 habilidades.')
  .refine((v) => splitList(v).every((s) => s.length <= 80), 'Cada habilidad admite hasta 80 caracteres.')

const schema = z
  .object({
    companyId: z.string().min(1, 'Selecciona una empresa.'),
    title: z.string().trim().min(1, 'El título es obligatorio.').max(200),
    url: z
      .string()
      .trim()
      .max(2048)
      .refine((v) => v === '' || /^https?:\/\/\S+$/i.test(v), 'Debe empezar por http:// o https://'),
    location: z.string().trim().max(200),
    workMode: z.enum(['onSite', 'hybrid', 'remote']),
    contractType: z.enum([
      'fullTime',
      'partTime',
      'contract',
      'freelance',
      'internship',
      'temporary',
      'other',
    ]),
    salaryMin: money,
    salaryMax: money,
    currency: z
      .string()
      .trim()
      .refine((v) => v === '' || /^[A-Za-z]{3}$/.test(v), 'Código ISO de 3 letras, p. ej. EUR.'),
    requiredSkills: skills,
    preferredSkills: skills,
    publishedOn: z.string(),
    deadline: z.string(),
    description: z.string().trim().max(50000),
    notes: z.string().trim().max(10000),
  })
  .refine((v) => !(v.salaryMin && v.salaryMax) || toNumber(v.salaryMin)! <= toNumber(v.salaryMax)!, {
    path: ['salaryMax'],
    message: 'Debe ser mayor o igual que el mínimo.',
  })
  .refine((v) => !(v.salaryMin || v.salaryMax) || v.currency !== '', {
    path: ['currency'],
    message: 'Indica la moneda del salario.',
  })
  .refine((v) => !(v.publishedOn && v.deadline) || v.deadline >= v.publishedOn, {
    path: ['deadline'],
    message: 'No puede ser anterior a la publicación.',
  })

type FormValues = z.infer<typeof schema>

function toNumber(value: string): number | null {
  return value.trim() === '' ? null : Number(value.replace(',', '.'))
}

function toFormValues(job?: JobOffer, companyId?: string): FormValues {
  return {
    companyId: job?.companyId ?? companyId ?? '',
    title: job?.title ?? '',
    url: job?.url ?? '',
    location: job?.location ?? '',
    workMode: job?.workMode ?? 'hybrid',
    contractType: job?.contractType ?? 'fullTime',
    salaryMin: job?.salaryMin?.toString() ?? '',
    salaryMax: job?.salaryMax?.toString() ?? '',
    currency: job?.currency ?? 'EUR',
    requiredSkills: job?.requiredSkills.join(', ') ?? '',
    preferredSkills: job?.preferredSkills.join(', ') ?? '',
    publishedOn: job?.publishedOn ?? '',
    deadline: job?.deadline ?? '',
    description: job?.description ?? '',
    notes: job?.notes ?? '',
  }
}

interface Props {
  open: boolean
  onOpenChange: (open: boolean) => void
  job?: JobOffer
  /** Pre-selects a company when creating from a company page. */
  companyId?: string
  onSaved?: (job: JobOffer) => void
}

export function JobFormDialog({ open, onOpenChange, job, companyId, onSaved }: Props) {
  const save = useSaveJob(job?.id)
  const companies = useCompanyOptions()
  const [companyFormOpen, setCompanyFormOpen] = useState(false)
  const {
    register,
    control,
    handleSubmit,
    reset,
    setValue,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({ resolver: zodResolver(schema), defaultValues: toFormValues(job, companyId) })

  useEffect(() => {
    if (open) reset(toFormValues(job, companyId))
  }, [open, job, companyId, reset])

  const onSubmit = handleSubmit(async (values) => {
    const hasSalary = values.salaryMin !== '' || values.salaryMax !== ''
    try {
      const saved = await save.mutateAsync({
        companyId: values.companyId,
        title: values.title,
        url: emptyToNull(values.url),
        location: emptyToNull(values.location),
        workMode: values.workMode as WorkMode,
        contractType: values.contractType as ContractType,
        salaryMin: toNumber(values.salaryMin),
        salaryMax: toNumber(values.salaryMax),
        currency: hasSalary ? values.currency.toUpperCase() : null,
        requiredSkills: splitList(values.requiredSkills),
        preferredSkills: splitList(values.preferredSkills),
        publishedOn: values.publishedOn || null,
        deadline: values.deadline || null,
        description: emptyToNull(values.description),
        notes: emptyToNull(values.notes),
      })
      toast.success(job ? 'Oferta actualizada' : 'Oferta guardada')
      onOpenChange(false)
      onSaved?.(saved)
    } catch (error) {
      toast.error(errorMessage(error))
    }
  })

  const companyList = companies.data?.items ?? []

  return (
    <>
      <Dialog open={open} onOpenChange={onOpenChange}>
        <DialogContent
          size="xl"
          title={job ? 'Editar oferta' : 'Guardar oferta'}
          description="Guarda la oferta aunque todavía no hayas postulado. La postulación se crea aparte."
          footer={
            <>
              <Button variant="outline" onClick={() => onOpenChange(false)}>
                Cancelar
              </Button>
              <Button type="submit" form="job-form" disabled={isSubmitting}>
                {isSubmitting ? 'Guardando…' : 'Guardar'}
              </Button>
            </>
          }
        >
          <form id="job-form" onSubmit={onSubmit} noValidate className="grid gap-4 md:grid-cols-6">
            <FormField
              label="Título del puesto"
              error={errors.title?.message}
              required
              className="md:col-span-4"
            >
              <Input autoFocus {...register('title')} />
            </FormField>
            <div className="flex items-end gap-2 md:col-span-2">
              <FormField label="Empresa" error={errors.companyId?.message} required className="flex-1">
                <Controller
                  control={control}
                  name="companyId"
                  render={({ field }) => (
                    <Select {...field} disabled={companies.isPending}>
                      <option value="">{companies.isPending ? 'Cargando…' : 'Selecciona…'}</option>
                      {companyList.map((c) => (
                        <option key={c.id} value={c.id}>
                          {c.name}
                        </option>
                      ))}
                    </Select>
                  )}
                />
              </FormField>
              <Button
                variant="outline"
                size="icon"
                aria-label="Crear empresa"
                onClick={() => setCompanyFormOpen(true)}
              >
                <Plus />
              </Button>
            </div>
            <FormField label="URL de la oferta" error={errors.url?.message} className="md:col-span-4">
              <Input type="url" placeholder="https://" {...register('url')} />
            </FormField>
            <FormField label="Ubicación" error={errors.location?.message} className="md:col-span-2">
              <Input {...register('location')} />
            </FormField>
            <FormField label="Modalidad" className="md:col-span-3">
              <Select {...register('workMode')}>
                {Object.entries(WORK_MODE_LABELS).map(([value, label]) => (
                  <option key={value} value={value}>
                    {label}
                  </option>
                ))}
              </Select>
            </FormField>
            <FormField label="Tipo de contrato" className="md:col-span-3">
              <Select {...register('contractType')}>
                {Object.entries(CONTRACT_TYPE_LABELS).map(([value, label]) => (
                  <option key={value} value={value}>
                    {label}
                  </option>
                ))}
              </Select>
            </FormField>
            <FormField label="Salario mínimo" error={errors.salaryMin?.message} className="md:col-span-2">
              <Input inputMode="decimal" {...register('salaryMin')} />
            </FormField>
            <FormField label="Salario máximo" error={errors.salaryMax?.message} className="md:col-span-2">
              <Input inputMode="decimal" {...register('salaryMax')} />
            </FormField>
            <FormField label="Moneda" error={errors.currency?.message} className="md:col-span-2">
              <Input maxLength={3} className="uppercase" {...register('currency')} />
            </FormField>
            <FormField
              label="Habilidades requeridas"
              error={errors.requiredSkills?.message}
              hint="Separadas por comas."
              className="md:col-span-3"
            >
              <Input placeholder="C#, React, SQL" {...register('requiredSkills')} />
            </FormField>
            <FormField
              label="Habilidades deseables"
              error={errors.preferredSkills?.message}
              hint="Separadas por comas."
              className="md:col-span-3"
            >
              <Input placeholder="Docker, Azure" {...register('preferredSkills')} />
            </FormField>
            <FormField label="Fecha de publicación" className="md:col-span-3">
              <Input type="date" {...register('publishedOn')} />
            </FormField>
            <FormField label="Fecha límite" error={errors.deadline?.message} className="md:col-span-3">
              <Input type="date" {...register('deadline')} />
            </FormField>
            <FormField
              label="Descripción"
              error={errors.description?.message}
              hint="Pega el texto de la oferta: se usa para comparar tu CV."
              className="md:col-span-6"
            >
              <Textarea rows={6} {...register('description')} />
            </FormField>
            <FormField label="Notas personales" error={errors.notes?.message} className="md:col-span-6">
              <Textarea rows={3} {...register('notes')} />
            </FormField>
          </form>
        </DialogContent>
      </Dialog>
      <CompanyFormDialog
        open={companyFormOpen}
        onOpenChange={setCompanyFormOpen}
        onSaved={(company) => setValue('companyId', company.id, { shouldValidate: true })}
      />
    </>
  )
}
