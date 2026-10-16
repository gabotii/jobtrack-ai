import { Building2, Globe, MapPin, MoreHorizontal, Pencil, Plus, Search, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { Link } from 'react-router'
import { toast } from 'sonner'
import { EmptyState, ErrorState, ListSkeleton, PageHeader, Pagination } from '@/components/page'
import { Button } from '@/components/ui/button'
import { ConfirmDialog } from '@/components/ui/dialog'
import { Card } from '@/components/ui/display'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { Input, Select } from '@/components/ui/form-controls'
import { useDebouncedValue } from '@/hooks/use-debounced-value'
import { errorMessage } from '@/lib/api-client'
import type { Company } from '@/types/api'
import { useCompanies, useDeleteCompany } from './api'
import { CompanyFormDialog } from './CompanyFormDialog'

export function CompaniesPage() {
  const [search, setSearch] = useState('')
  const [sort, setSort] = useState<'name' | 'createdAt'>('name')
  const [page, setPage] = useState(1)
  const debouncedSearch = useDebouncedValue(search)
  const query = useCompanies({
    search: debouncedSearch,
    sort,
    desc: sort === 'createdAt',
    page,
    pageSize: 20,
  })

  const [formOpen, setFormOpen] = useState(false)
  const [editing, setEditing] = useState<Company | undefined>()
  const [deleting, setDeleting] = useState<Company | undefined>()
  const remove = useDeleteCompany()

  const openCreate = () => {
    setEditing(undefined)
    setFormOpen(true)
  }

  const confirmDelete = async () => {
    if (!deleting) return
    try {
      await remove.mutateAsync(deleting.id)
      toast.success('Empresa eliminada')
    } catch (error) {
      toast.error(errorMessage(error))
    } finally {
      setDeleting(undefined)
    }
  }

  const companies = query.data?.items ?? []
  const filtering = debouncedSearch.trim() !== ''

  return (
    <>
      <PageHeader
        title="Empresas"
        description="Empresas en las que te interesa trabajar, con su contacto y tus notas."
        actions={
          <Button onClick={openCreate}>
            <Plus /> Nueva empresa
          </Button>
        }
      />

      <div className="mb-4 flex flex-col gap-2 sm:flex-row">
        <div className="relative flex-1">
          <Search
            className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted-foreground"
            aria-hidden
          />
          <Input
            type="search"
            aria-label="Buscar empresas"
            placeholder="Buscar por nombre, industria o ubicación"
            className="pl-9"
            value={search}
            onChange={(e) => {
              setSearch(e.target.value)
              setPage(1)
            }}
          />
        </div>
        <Select
          aria-label="Ordenar"
          className="sm:w-48"
          value={sort}
          onChange={(e) => setSort(e.target.value as typeof sort)}
        >
          <option value="name">Nombre (A-Z)</option>
          <option value="createdAt">Más recientes</option>
        </Select>
      </div>

      {query.isPending ? (
        <ListSkeleton />
      ) : query.isError ? (
        <ErrorState error={query.error} onRetry={() => query.refetch()} />
      ) : companies.length === 0 ? (
        <EmptyState
          icon={Building2}
          title={filtering ? 'Sin resultados' : 'Aún no tienes empresas'}
          description={
            filtering
              ? 'Ninguna empresa coincide con la búsqueda.'
              : 'Registra las empresas que te interesan para asociarles ofertas y entrevistas.'
          }
          action={
            filtering ? undefined : (
              <Button onClick={openCreate}>
                <Plus /> Añadir la primera empresa
              </Button>
            )
          }
        />
      ) : (
        <>
          <ul className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
            {companies.map((company) => (
              <li key={company.id}>
                <Card className="flex h-full flex-col p-4 transition-shadow hover:shadow-md">
                  <div className="flex items-start justify-between gap-2">
                    <Link
                      to={`/companies/${company.id}`}
                      className="min-w-0 font-semibold hover:text-primary"
                    >
                      <span className="block truncate">{company.name}</span>
                      {company.industry ? (
                        <span className="block text-xs font-normal text-muted-foreground">
                          {company.industry}
                        </span>
                      ) : null}
                    </Link>
                    <DropdownMenu>
                      <DropdownMenuTrigger asChild>
                        <Button variant="ghost" size="icon" aria-label={`Acciones para ${company.name}`}>
                          <MoreHorizontal />
                        </Button>
                      </DropdownMenuTrigger>
                      <DropdownMenuContent align="end">
                        <DropdownMenuItem
                          onSelect={() => {
                            setEditing(company)
                            setFormOpen(true)
                          }}
                        >
                          <Pencil /> Editar
                        </DropdownMenuItem>
                        <DropdownMenuItem className="text-danger" onSelect={() => setDeleting(company)}>
                          <Trash2 /> Eliminar
                        </DropdownMenuItem>
                      </DropdownMenuContent>
                    </DropdownMenu>
                  </div>
                  <div className="mt-3 space-y-1 text-sm text-muted-foreground">
                    {company.location ? (
                      <p className="flex items-center gap-1.5">
                        <MapPin className="size-3.5" aria-hidden /> {company.location}
                      </p>
                    ) : null}
                    {company.website ? (
                      <p className="flex items-center gap-1.5 truncate">
                        <Globe className="size-3.5" aria-hidden />
                        <a
                          href={company.website}
                          target="_blank"
                          rel="noopener noreferrer"
                          className="truncate hover:underline"
                        >
                          {company.website.replace(/^https?:\/\//, '')}
                        </a>
                      </p>
                    ) : null}
                  </div>
                  <div className="mt-auto flex gap-4 pt-4 text-xs text-muted-foreground">
                    <span>
                      <strong className="text-foreground">{company.jobOfferCount}</strong> ofertas
                    </span>
                    <span>
                      <strong className="text-foreground">{company.activeApplicationCount}</strong>{' '}
                      postulaciones activas
                    </span>
                  </div>
                </Card>
              </li>
            ))}
          </ul>
          <Pagination
            page={query.data.page}
            totalPages={query.data.totalPages}
            totalCount={query.data.totalCount}
            onPageChange={setPage}
          />
        </>
      )}

      <CompanyFormDialog open={formOpen} onOpenChange={setFormOpen} company={editing} />
      <ConfirmDialog
        open={deleting !== undefined}
        onOpenChange={(open) => !open && setDeleting(undefined)}
        title="¿Eliminar empresa?"
        description={
          deleting && deleting.jobOfferCount > 0
            ? `«${deleting.name}» tiene ${deleting.jobOfferCount} oferta(s). Debes eliminarlas antes de borrar la empresa.`
            : `Se eliminará «${deleting?.name}» y sus notas. Los eventos vinculados se conservarán sin empresa.`
        }
        confirmLabel="Eliminar"
        pending={remove.isPending}
        onConfirm={confirmDelete}
      />
    </>
  )
}
