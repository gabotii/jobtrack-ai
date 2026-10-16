import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { Link, useNavigate } from 'react-router'
import { z } from 'zod'
import { FormField } from '@/components/form-field'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/form-controls'
import { ApiError, errorMessage } from '@/lib/api-client'
import { browserTimeZone } from '@/lib/dates'
import { AuthLayout } from './AuthLayout'
import { useAuth } from './auth-context'
import { passwordSchema } from './schemas'

const schema = z
  .object({
    fullName: z.string().trim().min(1, 'Indica tu nombre.').max(150),
    email: z.email('Introduce un correo electrónico válido.'),
    password: passwordSchema,
    confirmPassword: z.string(),
  })
  .refine((v) => v.password === v.confirmPassword, {
    path: ['confirmPassword'],
    message: 'Las contraseñas no coinciden.',
  })

type FormValues = z.infer<typeof schema>

export function RegisterPage() {
  const { register: registerAccount } = useAuth()
  const navigate = useNavigate()
  const [serverError, setServerError] = useState<string | null>(null)
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { fullName: '', email: '', password: '', confirmPassword: '' },
  })

  const onSubmit = handleSubmit(async ({ fullName, email, password }) => {
    setServerError(null)
    try {
      await registerAccount({ fullName, email, password, timeZone: browserTimeZone() })
      navigate('/', { replace: true })
    } catch (error) {
      if (error instanceof ApiError && error.code === 'auth.email_taken') {
        setError('email', { message: error.message })
      } else {
        setServerError(errorMessage(error))
      }
    }
  })

  return (
    <AuthLayout title="Crea tu cuenta" subtitle="Organiza empresas, ofertas, entrevistas y seguimientos.">
      <form onSubmit={onSubmit} noValidate className="space-y-4">
        {serverError ? (
          <p role="alert" className="rounded-lg bg-danger-soft px-3 py-2 text-sm text-danger">
            {serverError}
          </p>
        ) : null}
        <FormField label="Nombre completo" error={errors.fullName?.message} required>
          <Input autoComplete="name" {...register('fullName')} />
        </FormField>
        <FormField label="Correo electrónico" error={errors.email?.message} required>
          <Input type="email" autoComplete="email" {...register('email')} />
        </FormField>
        <FormField
          label="Contraseña"
          error={errors.password?.message}
          hint="Mínimo 8 caracteres, con mayúscula, minúscula y número."
          required
        >
          <Input type="password" autoComplete="new-password" {...register('password')} />
        </FormField>
        <FormField label="Repite la contraseña" error={errors.confirmPassword?.message} required>
          <Input type="password" autoComplete="new-password" {...register('confirmPassword')} />
        </FormField>
        <p className="text-xs text-muted-foreground">
          Usaremos tu zona horaria actual ({browserTimeZone()}); puedes cambiarla en tu perfil.
        </p>
        <Button type="submit" className="w-full" disabled={isSubmitting}>
          {isSubmitting ? 'Creando cuenta…' : 'Crear cuenta'}
        </Button>
      </form>
      <p className="mt-6 text-center text-sm text-muted-foreground">
        ¿Ya tienes cuenta?{' '}
        <Link to="/login" className="font-semibold text-primary hover:underline">
          Inicia sesión
        </Link>
      </p>
    </AuthLayout>
  )
}
