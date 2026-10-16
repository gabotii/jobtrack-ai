import { zodResolver } from '@hookform/resolvers/zod'
import { useForm } from 'react-hook-form'
import { toast } from 'sonner'
import { z } from 'zod'
import { FormField } from '@/components/form-field'
import { PageHeader } from '@/components/page'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/display'
import { Input, Select } from '@/components/ui/form-controls'
import { useAuth, useCurrentUser } from '@/features/auth/auth-context'
import { passwordSchema } from '@/features/auth/schemas'
import { useQueryClient } from '@tanstack/react-query'
import { api, ApiError, errorMessage } from '@/lib/api-client'
import { browserTimeZone, formatDate, supportedTimeZones } from '@/lib/dates'
import type { AuthResponse, User } from '@/types/api'

const profileSchema = z.object({
  fullName: z.string().trim().min(1, 'Indica tu nombre.').max(150),
  timeZone: z.string().min(1),
})

const passwordFormSchema = z
  .object({
    currentPassword: z.string().min(1, 'Introduce tu contraseña actual.'),
    newPassword: passwordSchema,
    confirmPassword: z.string(),
  })
  .refine((v) => v.newPassword === v.confirmPassword, { path: ['confirmPassword'], message: 'No coinciden.' })
  .refine((v) => v.newPassword !== v.currentPassword, {
    path: ['newPassword'],
    message: 'Debe ser distinta de la actual.',
  })

export function ProfilePage() {
  const user = useCurrentUser()
  return (
    <>
      <PageHeader
        title="Perfil y configuración"
        description={`Cuenta creada el ${formatDate(user.createdAt.slice(0, 10))}.`}
      />
      <div className="grid max-w-4xl gap-4 lg:grid-cols-2">
        <ProfileForm user={user} />
        <PasswordForm />
      </div>
    </>
  )
}

function ProfileForm({ user }: { user: User }) {
  const { setUser } = useAuth()
  const queryClient = useQueryClient()
  const detected = browserTimeZone()
  const {
    register,
    handleSubmit,
    setValue,
    formState: { errors, isSubmitting, isDirty },
    reset,
  } = useForm<z.infer<typeof profileSchema>>({
    resolver: zodResolver(profileSchema),
    defaultValues: { fullName: user.fullName, timeZone: user.timeZone },
  })

  const onSubmit = handleSubmit(async (values) => {
    try {
      const updated = await api.put<User>('/users/me', values)
      setUser(updated)
      reset({ fullName: updated.fullName, timeZone: updated.timeZone })
      // Dates such as "today" and weekly stats depend on the zone.
      await queryClient.invalidateQueries()
      toast.success('Perfil actualizado')
    } catch (error) {
      toast.error(errorMessage(error))
    }
  })

  return (
    <Card>
      <CardHeader>
        <CardTitle>Datos personales</CardTitle>
      </CardHeader>
      <CardContent>
        <form onSubmit={onSubmit} noValidate className="space-y-4">
          <FormField label="Correo electrónico" hint="El correo no se puede cambiar.">
            <Input value={user.email} readOnly disabled />
          </FormField>
          <FormField label="Nombre completo" error={errors.fullName?.message} required>
            <Input autoComplete="name" {...register('fullName')} />
          </FormField>
          <FormField
            label="Zona horaria"
            hint={
              detected !== user.timeZone ? (
                <>
                  Tu navegador está en {detected}.{' '}
                  <button
                    type="button"
                    className="font-semibold text-primary hover:underline"
                    onClick={() => setValue('timeZone', detected, { shouldDirty: true })}
                  >
                    Usar esta zona
                  </button>
                </>
              ) : (
                'Se usa para mostrar horas y calcular «hoy» en estadísticas y seguimientos.'
              )
            }
          >
            <Select {...register('timeZone')}>
              {supportedTimeZones().map((zone) => (
                <option key={zone} value={zone}>
                  {zone.replaceAll('_', ' ')}
                </option>
              ))}
            </Select>
          </FormField>
          <Button type="submit" disabled={!isDirty || isSubmitting}>
            {isSubmitting ? 'Guardando…' : 'Guardar'}
          </Button>
        </form>
      </CardContent>
    </Card>
  )
}

function PasswordForm() {
  const { applySession } = useAuth()
  const {
    register,
    handleSubmit,
    reset,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<z.infer<typeof passwordFormSchema>>({
    resolver: zodResolver(passwordFormSchema),
    defaultValues: { currentPassword: '', newPassword: '', confirmPassword: '' },
  })

  const onSubmit = handleSubmit(async ({ currentPassword, newPassword }) => {
    try {
      const session = await api.post<AuthResponse>('/users/me/password', { currentPassword, newPassword })
      applySession(session)
      reset()
      toast.success('Contraseña actualizada. Se cerraron las demás sesiones.')
    } catch (error) {
      if (error instanceof ApiError && error.fieldErrors.currentPassword) {
        setError('currentPassword', { message: error.fieldErrors.currentPassword[0] })
      } else {
        toast.error(errorMessage(error))
      }
    }
  })

  return (
    <Card>
      <CardHeader>
        <CardTitle>Cambiar contraseña</CardTitle>
      </CardHeader>
      <CardContent>
        <form onSubmit={onSubmit} noValidate className="space-y-4">
          <FormField label="Contraseña actual" error={errors.currentPassword?.message} required>
            <Input type="password" autoComplete="current-password" {...register('currentPassword')} />
          </FormField>
          <FormField
            label="Nueva contraseña"
            error={errors.newPassword?.message}
            hint="Mínimo 8 caracteres, con mayúscula, minúscula y número."
            required
          >
            <Input type="password" autoComplete="new-password" {...register('newPassword')} />
          </FormField>
          <FormField label="Repite la nueva contraseña" error={errors.confirmPassword?.message} required>
            <Input type="password" autoComplete="new-password" {...register('confirmPassword')} />
          </FormField>
          <Button type="submit" disabled={isSubmitting}>
            {isSubmitting ? 'Actualizando…' : 'Actualizar contraseña'}
          </Button>
        </form>
      </CardContent>
    </Card>
  )
}
