import { zodResolver } from '@hookform/resolvers/zod'
import { useEffect } from 'react'
import { useForm } from 'react-hook-form'
import { toast } from 'sonner'
import { z } from 'zod'
import { FormField } from '@/components/form-field'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent } from '@/components/ui/dialog'
import { Input, Textarea } from '@/components/ui/form-controls'
import { ApiError, errorMessage } from '@/lib/api-client'
import { emptyToNull } from '@/lib/utils'
import type { Company } from '@/types/api'
import { useSaveCompany } from './api'

const optionalUrl = z
  .string()
  .trim()
  .max(2048)
  .refine((v) => v === '' || /^https?:\/\/\S+$/i.test(v), 'Debe empezar por http:// o https://')

const schema = z.object({
  name: z.string().trim().min(1, 'El nombre es obligatorio.').max(200),
  website: optionalUrl,
  industry: z.string().trim().max(200),
  location: z.string().trim().max(200),
  description: z.string().trim().max(5000),
  contactName: z.string().trim().max(200),
  contactEmail: z.union([z.literal(''), z.email('Correo no válido.')]),
  contactPhone: z.string().trim().max(50),
  notes: z.string().trim().max(10000),
})

type FormValues = z.infer<typeof schema>

function toFormValues(company?: Company): FormValues {
  return {
    name: company?.name ?? '',
    website: company?.website ?? '',
    industry: company?.industry ?? '',
    location: company?.location ?? '',
    description: company?.description ?? '',
    contactName: company?.contactName ?? '',
    contactEmail: company?.contactEmail ?? '',
    contactPhone: company?.contactPhone ?? '',
    notes: company?.notes ?? '',
  }
}

interface Props {
  open: boolean
  onOpenChange: (open: boolean) => void
  company?: Company
  onSaved?: (company: Company) => void
}

export function CompanyFormDialog({ open, onOpenChange, company, onSaved }: Props) {
  const save = useSaveCompany(company?.id)
  const {
    register,
    handleSubmit,
    reset,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({ resolver: zodResolver(schema), defaultValues: toFormValues(company) })

  useEffect(() => {
    if (open) reset(toFormValues(company))
  }, [open, company, reset])

  const onSubmit = handleSubmit(async (values) => {
    try {
      const saved = await save.mutateAsync({
        name: values.name,
        website: emptyToNull(values.website),
        industry: emptyToNull(values.industry),
        location: emptyToNull(values.location),
        description: emptyToNull(values.description),
        contactName: emptyToNull(values.contactName),
        contactEmail: emptyToNull(values.contactEmail),
        contactPhone: emptyToNull(values.contactPhone),
        notes: emptyToNull(values.notes),
      })
      toast.success(company ? 'Empresa actualizada' : 'Empresa creada')
      onOpenChange(false)
      onSaved?.(saved)
    } catch (error) {
      if (error instanceof ApiError && error.code === 'company.duplicate_name') {
        setError('name', { message: error.message })
      } else {
        toast.error(errorMessage(error))
      }
    }
  })

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent
        size="lg"
        title={company ? 'Editar empresa' : 'Nueva empresa'}
        description="Solo el nombre es obligatorio. Puedes completar el resto más adelante."
        footer={
          <>
            <Button variant="outline" onClick={() => onOpenChange(false)}>
              Cancelar
            </Button>
            <Button type="submit" form="company-form" disabled={isSubmitting}>
              {isSubmitting ? 'Guardando…' : 'Guardar'}
            </Button>
          </>
        }
      >
        <form id="company-form" onSubmit={onSubmit} noValidate className="grid gap-4 sm:grid-cols-2">
          <FormField label="Nombre" error={errors.name?.message} required className="sm:col-span-2">
            <Input autoFocus {...register('name')} />
          </FormField>
          <FormField label="Sitio web" error={errors.website?.message}>
            <Input type="url" placeholder="https://" {...register('website')} />
          </FormField>
          <FormField label="Industria" error={errors.industry?.message}>
            <Input {...register('industry')} />
          </FormField>
          <FormField label="Ubicación" error={errors.location?.message} className="sm:col-span-2">
            <Input {...register('location')} />
          </FormField>
          <FormField label="Descripción" error={errors.description?.message} className="sm:col-span-2">
            <Textarea rows={3} {...register('description')} />
          </FormField>
          <fieldset className="grid gap-4 rounded-lg border border-border p-4 sm:col-span-2 sm:grid-cols-3">
            <legend className="px-1 text-sm font-semibold">Contacto (opcional)</legend>
            <FormField label="Persona de contacto" error={errors.contactName?.message}>
              <Input autoComplete="off" {...register('contactName')} />
            </FormField>
            <FormField label="Correo de contacto" error={errors.contactEmail?.message}>
              <Input type="email" autoComplete="off" {...register('contactEmail')} />
            </FormField>
            <FormField label="Teléfono de contacto" error={errors.contactPhone?.message}>
              <Input type="tel" autoComplete="off" {...register('contactPhone')} />
            </FormField>
          </fieldset>
          <FormField label="Notas personales" error={errors.notes?.message} className="sm:col-span-2">
            <Textarea rows={3} {...register('notes')} />
          </FormField>
        </form>
      </DialogContent>
    </Dialog>
  )
}
