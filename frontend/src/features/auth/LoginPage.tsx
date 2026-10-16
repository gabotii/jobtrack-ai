import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { Link, useLocation, useNavigate } from 'react-router'
import { z } from 'zod'
import { FormField } from '@/components/form-field'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/form-controls'
import { errorMessage } from '@/lib/api-client'
import { AuthLayout } from './AuthLayout'
import { useAuth } from './auth-context'

const schema = z.object({
  email: z.email('Introduce un correo electrónico válido.'),
  password: z.string().min(1, 'Introduce tu contraseña.'),
})

type FormValues = z.infer<typeof schema>

export function LoginPage() {
  const { login } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const [serverError, setServerError] = useState<string | null>(null)
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({ resolver: zodResolver(schema), defaultValues: { email: '', password: '' } })

  const from = (location.state as { from?: string } | null)?.from ?? '/'

  const onSubmit = handleSubmit(async (values) => {
    setServerError(null)
    try {
      await login(values.email, values.password)
      navigate(from, { replace: true })
    } catch (error) {
      setServerError(errorMessage(error))
    }
  })

  return (
    <AuthLayout title="Inicia sesión" subtitle="Bienvenido de nuevo. Continúa donde lo dejaste.">
      <form onSubmit={onSubmit} noValidate className="space-y-4">
        {serverError ? (
          <p role="alert" className="rounded-lg bg-danger-soft px-3 py-2 text-sm text-danger">
            {serverError}
          </p>
        ) : null}
        <FormField label="Correo electrónico" error={errors.email?.message} required>
          <Input type="email" autoComplete="email" {...register('email')} />
        </FormField>
        <FormField label="Contraseña" error={errors.password?.message} required>
          <Input type="password" autoComplete="current-password" {...register('password')} />
        </FormField>
        <Button type="submit" className="w-full" disabled={isSubmitting}>
          {isSubmitting ? 'Entrando…' : 'Entrar'}
        </Button>
      </form>
      <p className="mt-6 text-center text-sm text-muted-foreground">
        ¿No tienes cuenta?{' '}
        <Link to="/register" className="font-semibold text-primary hover:underline">
          Crea una gratis
        </Link>
      </p>
    </AuthLayout>
  )
}
