import {
  ArrowLeft,
  BriefcaseBusiness,
  Globe,
  Mail,
  MapPin,
  Pencil,
  Phone,
  Plus,
  Trash2,
  UserRound,
} from 'lucide-react'
import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import { toast } from 'sonner'
import { EmptyState, ErrorState, PageHeader, StatusBadge } from '@/components/page'
import { Button } from '@/components/ui/button'
import { ConfirmDialog } from '@/components/ui/dialog'
import { Badge, Card, CardContent, CardHeader, CardTitle, Skeleton } from '@/components/ui/display'
import { useJobs } from '@/features/jobs/api'
import { JobFormDialog } from '@/features/jobs/JobFormDialog'
import { errorMessage } from '@/lib/api-client'
import { formatRelative } from '@/lib/dates'
import { WORK_MODE_LABELS } from '@/lib/labels'
import { useCompany, useDeleteCompany } from './api'
import { CompanyFormDialog } from './CompanyFormDialog'

export function CompanyDetailPage() {
  const { id = '' } = useParams()
  const navigate = useNavigate()
  const query = useCompany(id)
  const jobs = useJobs({ companyId: id, pageSize: 100, sort: 'savedAt', desc: true })
  const remove = useDeleteCompany()
  const [editOpen, setEditOpen] = useState(false)
  const [jobOpen, setJobOpen] = useState(false)
  const [deleteOpen, setDeleteOpen] = useState(false)

  if (query.isPending) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-8 w-1/3" />
        <Skeleton className="h-48 w-full" />
      </div>
    )
  }
  if (query.isError) return <ErrorState error={query.error} onRetry={() => query.refetch()} />
  const company = query.data

  const confirmDelete = async () => {
    try {
      await remove.mutateAsync(company.id)
      toast.success('Empresa eliminada')
      navigate('/companies')
    } catch (error) {
      toast.error(errorMessage(error))
      setDeleteOpen(false)
    }
  }

  return (
    <>
      <Link
        to="/companies"
        className="mb-3 inline-flex items-center gap-1 text-sm text-muted-foreground hover:text-foreground"
      >
        <ArrowLeft className="size-4" aria-hidden /> Empresas
      </Link>
      <PageHeader
        title={company.name}
        description={[company.industry, company.location].filter(Boolean).join(' · ') || undefined}
        actions={
          <>
            <Button onClick={() => setJobOpen(true)}>
              <Plus /> Nueva oferta
            </Button>
            <Button variant="outline" onClick={() => setEditOpen(true)}>
              <Pencil /> Editar
            </Button>
            <Button variant="ghost" aria-label="Eliminar empresa" onClick={() => setDeleteOpen(true)}>
              <Trash2 />
            </Button>
          </>
        }
      />

      <div className="grid gap-4 lg:grid-cols-[20rem_1fr]">
        <div className="space-y-4">
          <Card>
            <CardHeader>
              <CardTitle>Información</CardTitle>
            </CardHeader>
            <CardContent className="space-y-2 text-sm">
              {company.description ? <p className="whitespace-pre-line">{company.description}</p> : null}
              {company.location ? (
                <p className="flex items-center gap-2 text-muted-foreground">
                  <MapPin className="size-4" aria-hidden /> {company.location}
                </p>
              ) : null}
              {company.website ? (
                <a
                  href={company.website}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="flex items-center gap-2 text-primary hover:underline"
                >
                  <Globe className="size-4" aria-hidden /> {company.website.replace(/^https?:\/\//, '')}
                </a>
              ) : null}
              {!company.description && !company.location && !company.website ? (
                <p className="text-muted-foreground">Sin información adicional.</p>
              ) : null}
            </CardContent>
          </Card>
          <Card>
            <CardHeader>
              <CardTitle>Contacto</CardTitle>
            </CardHeader>
            <CardContent className="space-y-2 text-sm">
              {company.contactName || company.contactEmail || company.contactPhone ? (
                <>
                  {company.contactName ? (
                    <p className="flex items-center gap-2">
                      <UserRound className="size-4 text-muted-foreground" aria-hidden /> {company.contactName}
                    </p>
                  ) : null}
                  {company.contactEmail ? (
                    <a
                      href={`mailto:${company.contactEmail}`}
                      className="flex items-center gap-2 text-primary hover:underline"
                    >
                      <Mail className="size-4" aria-hidden /> {company.contactEmail}
                    </a>
                  ) : null}
                  {company.contactPhone ? (
                    <a
                      href={`tel:${company.contactPhone}`}
                      className="flex items-center gap-2 hover:underline"
                    >
                      <Phone className="size-4 text-muted-foreground" aria-hidden /> {company.contactPhone}
                    </a>
                  ) : null}
                </>
              ) : (
                <p className="text-muted-foreground">No hay un contacto registrado.</p>
              )}
            </CardContent>
          </Card>
          {company.notes ? (
            <Card className="bg-accent-soft">
              <CardHeader>
                <CardTitle className="text-accent">Notas personales</CardTitle>
              </CardHeader>
              <CardContent className="text-sm whitespace-pre-line">{company.notes}</CardContent>
            </Card>
          ) : null}
        </div>

        <Card>
          <CardHeader>
            <CardTitle>Ofertas ({company.jobOfferCount})</CardTitle>
          </CardHeader>
          <CardContent>
            {jobs.isPending ? (
              <Skeleton className="h-24 w-full" />
            ) : jobs.isError ? (
              <ErrorState error={jobs.error} onRetry={() => jobs.refetch()} />
            ) : jobs.data.items.length === 0 ? (
              <EmptyState
                icon={BriefcaseBusiness}
                title="Sin ofertas"
                description="Guarda una oferta de esta empresa para empezar a seguirla."
                action={
                  <Button onClick={() => setJobOpen(true)}>
                    <Plus /> Nueva oferta
                  </Button>
                }
              />
            ) : (
              <ul className="divide-y divide-border">
                {jobs.data.items.map((job) => (
                  <li key={job.id} className="flex flex-wrap items-center justify-between gap-2 py-3">
                    <div>
                      <Link to={`/jobs/${job.id}`} className="font-medium hover:text-primary">
                        {job.title}
                      </Link>
                      <p className="text-xs text-muted-foreground">
                        {WORK_MODE_LABELS[job.workMode]} · guardada {formatRelative(job.savedAt)}
                      </p>
                    </div>
                    {job.application ? (
                      <StatusBadge status={job.application.status} />
                    ) : (
                      <Badge className="bg-muted">Sin postular</Badge>
                    )}
                  </li>
                ))}
              </ul>
            )}
          </CardContent>
        </Card>
      </div>

      <CompanyFormDialog open={editOpen} onOpenChange={setEditOpen} company={company} />
      <JobFormDialog open={jobOpen} onOpenChange={setJobOpen} companyId={company.id} />
      <ConfirmDialog
        open={deleteOpen}
        onOpenChange={setDeleteOpen}
        title="¿Eliminar empresa?"
        description={
          company.jobOfferCount > 0
            ? `Esta empresa tiene ${company.jobOfferCount} oferta(s). Elimínalas antes de borrar la empresa.`
            : 'Se eliminará la empresa y sus notas. Los eventos vinculados se conservarán sin empresa.'
        }
        confirmLabel="Eliminar"
        pending={remove.isPending}
        onConfirm={confirmDelete}
      />
    </>
  )
}
