import { BriefcaseBusiness, CalendarClock, MapPin, Plus, Search } from 'lucide-react'
import { useState } from 'react'
import { Link } from 'react-router'
import { EmptyState, ErrorState, ListSkeleton, PageHeader, Pagination, StatusBadge } from '@/components/page'
import { Button } from '@/components/ui/button'
import { Badge, Card } from '@/components/ui/display'
import { Input, Select } from '@/components/ui/form-controls'
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { useCompanyOptions } from '@/features/companies/api'
import { useDebouncedValue } from '@/hooks/use-debounced-value'
import { formatDate, formatRelative } from '@/lib/dates'
import { formatSalary, WORK_MODE_LABELS } from '@/lib/labels'
import type { WorkMode } from '@/types/api'
import { useJobs, type JobQuery } from './api'
import { JobFormDialog } from './JobFormDialog'

type Scope = 'all' | 'saved' | 'applied'

export function JobsPage() {
  const [search, setSearch] = useState('')
  const [scope, setScope] = useState<Scope>('all')
  const [companyId, setCompanyId] = useState('')
  const [workMode, setWorkMode] = useState<WorkMode | ''>('')
  const [sort, setSort] = useState<NonNullable<JobQuery['sort']>>('savedAt')
  const [page, setPage] = useState(1)
  const [formOpen, setFormOpen] = useState(false)
  const debouncedSearch = useDebouncedValue(search)
  const companies = useCompanyOptions()

  const query = useJobs({
    search: debouncedSearch,
    companyId: companyId || undefined,
    workMode: workMode || undefined,
    hasApplication: scope === 'all' ? undefined : scope === 'applied',
    sort,
    desc: sort === 'savedAt',
    page,
    pageSize: 20,
  })

  const resetPage =
    <T,>(setter: (value: T) => void) =>
    (value: T) => {
      setter(value)
      setPage(1)
    }
  const jobs = query.data?.items ?? []
  const filtering = debouncedSearch !== '' || companyId !== '' || workMode !== '' || scope !== 'all'

  return (
    <>
      <PageHeader
        title="Ofertas"
        description="Ofertas guardadas, hayas postulado o no. Las postulaciones se gestionan aparte."
        actions={
          <Button onClick={() => setFormOpen(true)}>
            <Plus /> Guardar oferta
          </Button>
        }
      />

      <Tabs value={scope} onValueChange={(v) => resetPage(setScope)(v as Scope)} className="mb-4">
        <TabsList aria-label="Filtrar por estado de la postulación">
          <TabsTrigger value="all">Todas</TabsTrigger>
          <TabsTrigger value="saved">Sin postular</TabsTrigger>
          <TabsTrigger value="applied">Con postulación</TabsTrigger>
        </TabsList>
      </Tabs>

      <div className="mb-4 grid gap-2 md:grid-cols-[1fr_12rem_10rem_11rem]">
        <div className="relative">
          <Search
            className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted-foreground"
            aria-hidden
          />
          <Input
            type="search"
            aria-label="Buscar ofertas"
            placeholder="Buscar por puesto, empresa o ubicación"
            className="pl-9"
            value={search}
            onChange={(e) => resetPage(setSearch)(e.target.value)}
          />
        </div>
        <Select
          aria-label="Empresa"
          value={companyId}
          onChange={(e) => resetPage(setCompanyId)(e.target.value)}
        >
          <option value="">Todas las empresas</option>
          {companies.data?.items.map((c) => (
            <option key={c.id} value={c.id}>
              {c.name}
            </option>
          ))}
        </Select>
        <Select
          aria-label="Modalidad"
          value={workMode}
          onChange={(e) => resetPage(setWorkMode)(e.target.value as WorkMode | '')}
        >
          <option value="">Toda modalidad</option>
          {Object.entries(WORK_MODE_LABELS).map(([value, label]) => (
            <option key={value} value={value}>
              {label}
            </option>
          ))}
        </Select>
        <Select aria-label="Ordenar" value={sort} onChange={(e) => setSort(e.target.value as typeof sort)}>
          <option value="savedAt">Guardadas recientemente</option>
          <option value="deadline">Fecha límite</option>
          <option value="title">Título</option>
          <option value="company">Empresa</option>
        </Select>
      </div>

      {query.isPending ? (
        <ListSkeleton />
      ) : query.isError ? (
        <ErrorState error={query.error} onRetry={() => query.refetch()} />
      ) : jobs.length === 0 ? (
        <EmptyState
          icon={BriefcaseBusiness}
          title={filtering ? 'Sin resultados' : 'Todavía no has guardado ofertas'}
          description={
            filtering
              ? 'Prueba con otros filtros.'
              : 'Guarda las ofertas que te interesen para compararlas, postular y hacer seguimiento.'
          }
          action={
            filtering ? undefined : (
              <Button onClick={() => setFormOpen(true)}>
                <Plus /> Guardar la primera oferta
              </Button>
            )
          }
        />
      ) : (
        <>
          <Card className="divide-y divide-border">
            {jobs.map((job) => {
              const salary = formatSalary(job.salaryMin, job.salaryMax, job.currency)
              return (
                <article key={job.id} className="flex flex-col gap-3 p-4 sm:flex-row sm:items-center">
                  <div className="min-w-0 flex-1">
                    <Link to={`/jobs/${job.id}`} className="font-semibold hover:text-primary">
                      {job.title}
                    </Link>
                    <p className="text-sm text-muted-foreground">
                      {job.companyName}
                      {job.location ? (
                        <>
                          {' · '}
                          <MapPin className="inline size-3.5" aria-hidden /> {job.location}
                        </>
                      ) : null}
                    </p>
                    <div className="mt-2 flex flex-wrap gap-1.5">
                      <Badge className="bg-muted text-muted-foreground">
                        {WORK_MODE_LABELS[job.workMode]}
                      </Badge>
                      {salary ? <Badge className="bg-accent-soft text-accent">{salary}</Badge> : null}
                      {job.requiredSkills.slice(0, 4).map((skill) => (
                        <Badge key={skill} className="bg-primary-soft text-primary">
                          {skill}
                        </Badge>
                      ))}
                    </div>
                  </div>
                  <div className="flex shrink-0 flex-col items-start gap-1 text-xs text-muted-foreground sm:items-end">
                    {job.application ? (
                      <StatusBadge status={job.application.status} />
                    ) : (
                      <Badge className="bg-muted">Sin postular</Badge>
                    )}
                    {job.deadline ? (
                      <span className="flex items-center gap-1">
                        <CalendarClock className="size-3.5" aria-hidden /> Límite {formatDate(job.deadline)}
                      </span>
                    ) : null}
                    <span>Guardada {formatRelative(job.savedAt)}</span>
                  </div>
                </article>
              )
            })}
          </Card>
          <Pagination
            page={query.data.page}
            totalPages={query.data.totalPages}
            totalCount={query.data.totalCount}
            onPageChange={setPage}
          />
        </>
      )}

      <JobFormDialog open={formOpen} onOpenChange={setFormOpen} />
    </>
  )
}
