import { useQuery } from '@tanstack/react-query'
import {
  AlarmClock,
  ArrowRight,
  BellRing,
  Bookmark,
  Building2,
  BriefcaseBusiness,
  CalendarClock,
  CheckCircle2,
  Circle,
  ListChecks,
  MessagesSquare,
  Send,
  XCircle,
  type LucideIcon,
} from 'lucide-react'
import { Link } from 'react-router'
import { ErrorState, PageHeader, StatusBadge } from '@/components/page'
import { Button } from '@/components/ui/button'
import { Badge, Card, CardContent, CardHeader, CardTitle, Skeleton } from '@/components/ui/display'
import { useCurrentUser } from '@/features/auth/auth-context'
import { api } from '@/lib/api-client'
import { formatDate, formatDateTime, formatRelative, isPast } from '@/lib/dates'
import { EVENT_TYPE_META, PRIORITY_META, STATUS_META } from '@/lib/labels'
import { cn } from '@/lib/utils'
import type { Dashboard } from '@/types/api'
import { StatusChart, WeeklyChart } from './charts'

export function useDashboard() {
  return useQuery({
    queryKey: ['dashboard'],
    queryFn: ({ signal }) => api.get<Dashboard>('/dashboard', undefined, signal),
  })
}

export function DashboardPage() {
  const user = useCurrentUser()
  const query = useDashboard()
  const firstName = user.fullName.split(' ')[0]

  return (
    <>
      <PageHeader
        title={`Hola, ${firstName}`}
        description="Este es el estado actual de tu búsqueda de empleo."
        actions={
          <Button asChild>
            <Link to="/kanban">
              Abrir tablero <ArrowRight />
            </Link>
          </Button>
        }
      />
      {query.isPending ? (
        <DashboardSkeleton />
      ) : query.isError ? (
        <ErrorState error={query.error} onRetry={() => query.refetch()} />
      ) : (
        <DashboardContent data={query.data} timeZone={user.timeZone} />
      )}
    </>
  )
}

function DashboardContent({ data, timeZone }: { data: Dashboard; timeZone: string }) {
  const t = data.totals
  const isNewUser = t.companies === 0 && t.jobOffers === 0 && t.totalApplications === 0

  return (
    <div className="space-y-6">
      {isNewUser || t.totalApplications === 0 ? <GettingStarted totals={t} /> : null}

      <section aria-label="Indicadores" className="grid grid-cols-2 gap-3 md:grid-cols-4">
        <StatTile label="Postulaciones" value={t.totalApplications} icon={ListChecks} to="/applications" />
        <StatTile
          label="Activas"
          value={t.activeApplications}
          icon={Send}
          to="/kanban"
          hint="Enviadas y en proceso"
        />
        <StatTile
          label="Entrevistas próximas"
          value={t.upcomingInterviews}
          icon={MessagesSquare}
          to="/calendar"
        />
        <StatTile label="Ofertas sin postular" value={t.savedOffers} icon={Bookmark} to="/jobs" />
        <StatTile label="Rechazadas" value={t.rejectedApplications} icon={XCircle} to="/applications" />
        <StatTile
          label="Seguimientos pendientes"
          value={t.pendingFollowUps}
          icon={AlarmClock}
          to="/applications"
          attention={t.pendingFollowUps > 0}
          hint="Fecha de seguimiento hoy o vencida"
        />
        <StatTile
          label="Recordatorios vencidos"
          value={t.overdueReminders}
          icon={BellRing}
          to="/reminders"
          attention={t.overdueReminders > 0}
        />
        <StatTile label="Empresas" value={t.companies} icon={Building2} to="/companies" />
      </section>

      <div className="grid gap-4 lg:grid-cols-2">
        <WeeklyChart data={data.applicationsPerWeek} />
        <StatusChart data={data.statusDistribution} />
      </div>

      <div className="grid gap-4 lg:grid-cols-2 xl:grid-cols-4">
        <Card>
          <CardHeader>
            <CardTitle>Próximos eventos</CardTitle>
            <Link to="/calendar" className="text-xs font-medium text-primary hover:underline">
              Calendario
            </Link>
          </CardHeader>
          <CardContent>
            {data.upcomingEvents.length === 0 ? (
              <Empty text="No tienes eventos programados." />
            ) : (
              <ul className="space-y-3">
                {data.upcomingEvents.map((event) => (
                  <li key={event.id} className="flex gap-3 text-sm">
                    <span
                      className="mt-1.5 size-2 shrink-0 rounded-full"
                      style={{ backgroundColor: EVENT_TYPE_META[event.type].color }}
                      aria-hidden
                    />
                    <div className="min-w-0">
                      <p className="truncate font-medium">{event.title}</p>
                      <p className="text-xs text-muted-foreground">
                        {EVENT_TYPE_META[event.type].label} · {formatDateTime(event.startsAt, timeZone)}
                      </p>
                      {event.companyName ? (
                        <p className="text-xs text-muted-foreground">{event.companyName}</p>
                      ) : null}
                    </div>
                  </li>
                ))}
              </ul>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Seguimientos</CardTitle>
          </CardHeader>
          <CardContent>
            {data.followUps.length === 0 ? (
              <Empty text="Nada pendiente en los próximos 7 días." />
            ) : (
              <ul className="space-y-3">
                {data.followUps.map((a) => {
                  const overdue = a.followUpOn !== null && a.followUpOn <= data.today
                  return (
                    <li key={a.id} className="text-sm">
                      <Link to={`/applications/${a.id}`} className="font-medium hover:text-primary">
                        {a.jobTitle}
                      </Link>
                      <p className="text-xs text-muted-foreground">{a.companyName}</p>
                      <p
                        className={cn(
                          'text-xs',
                          overdue ? 'font-semibold text-danger' : 'text-muted-foreground',
                        )}
                      >
                        {overdue ? 'Pendiente desde ' : 'Para el '}
                        {formatDate(a.followUpOn)}
                        {a.nextAction ? ` · ${a.nextAction}` : ''}
                      </p>
                    </li>
                  )
                })}
              </ul>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Recordatorios</CardTitle>
            <Link to="/reminders" className="text-xs font-medium text-primary hover:underline">
              Ver todos
            </Link>
          </CardHeader>
          <CardContent>
            {data.pendingReminders.length === 0 ? (
              <Empty text="No tienes recordatorios pendientes." />
            ) : (
              <ul className="space-y-3">
                {data.pendingReminders.map((r) => {
                  const overdue = isPast(r.dueAt)
                  return (
                    <li key={r.id} className="flex items-start justify-between gap-2 text-sm">
                      <div className="min-w-0">
                        <p className="truncate font-medium">{r.title}</p>
                        <p
                          className={cn(
                            'text-xs',
                            overdue ? 'font-semibold text-danger' : 'text-muted-foreground',
                          )}
                        >
                          {overdue ? 'Vencido · ' : ''}
                          {formatDateTime(r.dueAt, timeZone)}
                        </p>
                      </div>
                      <Badge className={PRIORITY_META[r.priority].className}>
                        {PRIORITY_META[r.priority].label}
                      </Badge>
                    </li>
                  )
                })}
              </ul>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Actividad reciente</CardTitle>
          </CardHeader>
          <CardContent>
            {data.recentActivity.length === 0 ? (
              <Empty text="Aún no hay actividad." />
            ) : (
              <ul className="space-y-3">
                {data.recentActivity.map((item) => (
                  <li key={item.id} className="text-sm">
                    <Link
                      to={`/applications/${item.applicationId}`}
                      className="font-medium hover:text-primary"
                    >
                      {item.jobTitle}
                    </Link>
                    <p className="flex flex-wrap items-center gap-1 text-xs text-muted-foreground">
                      {item.fromStatus ? (
                        <>
                          {STATUS_META[item.fromStatus].label} → <StatusBadge status={item.toStatus} />
                        </>
                      ) : (
                        <>
                          Creada · <StatusBadge status={item.toStatus} />
                        </>
                      )}
                    </p>
                    <p className="text-xs text-muted-foreground">
                      {item.companyName} · {formatRelative(item.changedAt)}
                    </p>
                  </li>
                ))}
              </ul>
            )}
          </CardContent>
        </Card>
      </div>
    </div>
  )
}

function StatTile({
  label,
  value,
  icon: Icon,
  to,
  hint,
  attention = false,
}: {
  label: string
  value: number
  icon: LucideIcon
  to: string
  hint?: string
  attention?: boolean
}) {
  return (
    <Link
      to={to}
      className="group rounded-xl border border-border bg-surface p-4 shadow-xs transition-shadow hover:shadow-md"
      title={hint}
    >
      <div className="flex items-center justify-between text-sm text-muted-foreground">
        <span>{label}</span>
        <Icon className={cn('size-4', attention ? 'text-danger' : 'text-primary')} aria-hidden />
      </div>
      <p className={cn('mt-2 text-3xl font-semibold', attention && 'text-danger')}>{value}</p>
      {attention ? <p className="text-xs font-medium text-danger">Requiere atención</p> : null}
    </Link>
  )
}

function Empty({ text }: { text: string }) {
  return <p className="text-sm text-muted-foreground">{text}</p>
}

function GettingStarted({ totals }: { totals: Dashboard['totals'] }) {
  const steps = [
    {
      done: totals.companies > 0,
      label: 'Añade una empresa que te interese',
      to: '/companies',
      icon: Building2,
    },
    {
      done: totals.jobOffers > 0,
      label: 'Guarda una oferta de empleo',
      to: '/jobs',
      icon: BriefcaseBusiness,
    },
    {
      done: totals.totalApplications > 0,
      label: 'Crea tu primera postulación',
      to: '/applications',
      icon: ListChecks,
    },
    {
      done: totals.upcomingInterviews > 0,
      label: 'Programa una entrevista',
      to: '/calendar',
      icon: CalendarClock,
    },
  ]
  return (
    <Card className="border-primary/30 bg-primary-soft/60">
      <CardHeader>
        <div>
          <CardTitle>Primeros pasos</CardTitle>
          <p className="text-sm text-muted-foreground">Configura tu búsqueda en unos minutos.</p>
        </div>
      </CardHeader>
      <CardContent>
        <ol className="grid gap-2 sm:grid-cols-2 lg:grid-cols-4">
          {steps.map((step, index) => (
            <li key={step.label}>
              <Link
                to={step.to}
                className="flex items-center gap-3 rounded-lg border border-border bg-surface p-3 text-sm hover:border-primary"
              >
                {step.done ? (
                  <CheckCircle2 className="size-5 shrink-0 text-primary" aria-label="Completado" />
                ) : (
                  <Circle className="size-5 shrink-0 text-muted-foreground" aria-label="Pendiente" />
                )}
                <span className={cn(step.done && 'text-muted-foreground line-through')}>
                  {index + 1}. {step.label}
                </span>
              </Link>
            </li>
          ))}
        </ol>
      </CardContent>
    </Card>
  )
}

function DashboardSkeleton() {
  return (
    <div className="space-y-6" role="status" aria-label="Cargando panel">
      <div className="grid grid-cols-2 gap-3 md:grid-cols-4">
        {Array.from({ length: 8 }, (_, i) => (
          <Skeleton key={i} className="h-24" />
        ))}
      </div>
      <div className="grid gap-4 lg:grid-cols-2">
        <Skeleton className="h-72" />
        <Skeleton className="h-72" />
      </div>
    </div>
  )
}
