import { ListChecks, Plus, Search } from 'lucide-react'
import { useState } from 'react'
import { Link } from 'react-router'
import { EmptyState, ErrorState, ListSkeleton, PageHeader, Pagination, StatusBadge } from '@/components/page'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/display'
import { Input, Select } from '@/components/ui/form-controls'
import { useCompanyOptions } from '@/features/companies/api'
import { useDebouncedValue } from '@/hooks/use-debounced-value'
import { formatDate, formatRelative } from '@/lib/dates'
import { APPLICATION_STATUSES, STATUS_META } from '@/lib/labels'
import { cn } from '@/lib/utils'
import type { ApplicationStatus } from '@/types/api'
import { useApplications, type ApplicationQuery } from './api'
import { CreateApplicationDialog } from './CreateApplicationDialog'

export function ApplicationsPage() {
  const [search, setSearch] = useState('')
  const [statuses, setStatuses] = useState<ApplicationStatus[]>([])
  const [companyId, setCompanyId] = useState('')
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [sort, setSort] = useState<NonNullable<ApplicationQuery['sort']>>('updated')
  const [page, setPage] = useState(1)
  const [createOpen, setCreateOpen] = useState(false)
  const debouncedSearch = useDebouncedValue(search)
  const companies = useCompanyOptions()

  const query = useApplications({
    search: debouncedSearch,
    status: statuses,
    companyId: companyId || undefined,
    from: from || undefined,
    to: to || undefined,
    sort,
    desc: sort === 'updated' || sort === 'appliedOn',
    page,
    pageSize: 20,
  })

  const toggleStatus = (status: ApplicationStatus) => {
    setStatuses((current) =>
      current.includes(status) ? current.filter((s) => s !== status) : [...current, status],
    )
    setPage(1)
  }
  const filtering =
    debouncedSearch !== '' || statuses.length > 0 || companyId !== '' || from !== '' || to !== ''
  const items = query.data?.items ?? []

  return (
    <>
      <PageHeader
        title="Postulaciones"
        description="Todas tus postulaciones con su estado, próxima acción y fechas clave."
        actions={
          <>
            <Button variant="outline" asChild>
              <Link to="/kanban">Ver tablero</Link>
            </Button>
            <Button onClick={() => setCreateOpen(true)}>
              <Plus /> Nueva postulación
            </Button>
          </>
        }
      />

      <div className="mb-3 flex flex-wrap gap-1.5" role="group" aria-label="Filtrar por estado">
        {APPLICATION_STATUSES.map((status) => {
          const active = statuses.includes(status)
          const Icon = STATUS_META[status].icon
          return (
            <button
              key={status}
              type="button"
              aria-pressed={active}
              onClick={() => toggleStatus(status)}
              className={cn(
                'inline-flex items-center gap-1 rounded-full border px-2.5 py-1 text-xs font-semibold transition-colors',
                active
                  ? 'border-primary bg-primary text-primary-foreground'
                  : 'border-border bg-surface hover:bg-muted',
              )}
            >
              <Icon className="size-3.5" aria-hidden />
              {STATUS_META[status].label}
            </button>
          )
        })}
      </div>

      <div className="mb-4 grid gap-2 md:grid-cols-[1fr_12rem_9.5rem_9.5rem_11rem]">
        <div className="relative">
          <Search
            className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted-foreground"
            aria-hidden
          />
          <Input
            type="search"
            aria-label="Buscar postulaciones"
            placeholder="Buscar por puesto o empresa"
            className="pl-9"
            value={search}
            onChange={(e) => {
              setSearch(e.target.value)
              setPage(1)
            }}
          />
        </div>
        <Select
          aria-label="Empresa"
          value={companyId}
          onChange={(e) => (setCompanyId(e.target.value), setPage(1))}
        >
          <option value="">Todas las empresas</option>
          {companies.data?.items.map((c) => (
            <option key={c.id} value={c.id}>
              {c.name}
            </option>
          ))}
        </Select>
        <Input
          type="date"
          aria-label="Desde"
          title="Desde"
          value={from}
          onChange={(e) => (setFrom(e.target.value), setPage(1))}
        />
        <Input
          type="date"
          aria-label="Hasta"
          title="Hasta"
          value={to}
          onChange={(e) => (setTo(e.target.value), setPage(1))}
        />
        <Select aria-label="Ordenar" value={sort} onChange={(e) => setSort(e.target.value as typeof sort)}>
          <option value="updated">Última actualización</option>
          <option value="appliedOn">Fecha de postulación</option>
          <option value="followUp">Próximo seguimiento</option>
          <option value="company">Empresa</option>
        </Select>
      </div>

      {query.isPending ? (
        <ListSkeleton />
      ) : query.isError ? (
        <ErrorState error={query.error} onRetry={() => query.refetch()} />
      ) : items.length === 0 ? (
        <EmptyState
          icon={ListChecks}
          title={filtering ? 'Sin resultados' : 'Aún no tienes postulaciones'}
          description={
            filtering
              ? 'Ninguna postulación coincide con los filtros.'
              : 'Crea una postulación a partir de una oferta guardada para seguir su evolución.'
          }
          action={
            filtering ? undefined : (
              <Button onClick={() => setCreateOpen(true)}>
                <Plus /> Nueva postulación
              </Button>
            )
          }
        />
      ) : (
        <>
          <Card className="overflow-x-auto">
            <table className="w-full min-w-[46rem] text-sm">
              <caption className="sr-only">Listado de postulaciones</caption>
              <thead className="border-b border-border bg-muted/50 text-left text-xs text-muted-foreground uppercase">
                <tr>
                  <th scope="col" className="px-4 py-3 font-semibold">
                    Puesto
                  </th>
                  <th scope="col" className="px-4 py-3 font-semibold">
                    Estado
                  </th>
                  <th scope="col" className="px-4 py-3 font-semibold">
                    Postulación
                  </th>
                  <th scope="col" className="px-4 py-3 font-semibold">
                    Próxima acción
                  </th>
                  <th scope="col" className="px-4 py-3 font-semibold">
                    Seguimiento
                  </th>
                  <th scope="col" className="px-4 py-3 font-semibold">
                    Actualizada
                  </th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                {items.map((a) => (
                  <tr key={a.id} className="hover:bg-muted/40">
                    <td className="px-4 py-3">
                      <Link to={`/applications/${a.id}`} className="font-semibold hover:text-primary">
                        {a.jobTitle}
                      </Link>
                      <p className="text-xs text-muted-foreground">{a.companyName}</p>
                    </td>
                    <td className="px-4 py-3">
                      <StatusBadge status={a.status} />
                    </td>
                    <td className="px-4 py-3 whitespace-nowrap">{formatDate(a.appliedOn)}</td>
                    <td className="max-w-56 truncate px-4 py-3" title={a.nextAction ?? undefined}>
                      {a.nextAction ?? '—'}
                    </td>
                    <td className="px-4 py-3 whitespace-nowrap">{formatDate(a.followUpOn)}</td>
                    <td className="px-4 py-3 whitespace-nowrap text-muted-foreground">
                      {formatRelative(a.updatedAt)}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </Card>
          <Pagination
            page={query.data.page}
            totalPages={query.data.totalPages}
            totalCount={query.data.totalCount}
            onPageChange={setPage}
          />
        </>
      )}

      <CreateApplicationDialog open={createOpen} onOpenChange={setCreateOpen} />
    </>
  )
}
