import { ArrowLeft, ExternalLink, FileSearch, Pencil, Send, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import { toast } from 'sonner'
import { ErrorState, PageHeader, StatusBadge } from '@/components/page'
import { Button } from '@/components/ui/button'
import { ConfirmDialog } from '@/components/ui/dialog'
import { Badge, Card, CardContent, CardHeader, CardTitle, Skeleton } from '@/components/ui/display'
import { CreateApplicationDialog } from '@/features/applications/CreateApplicationDialog'
import { errorMessage } from '@/lib/api-client'
import { formatDate, formatRelative } from '@/lib/dates'
import { CONTRACT_TYPE_LABELS, formatSalary, WORK_MODE_LABELS } from '@/lib/labels'
import { useDeleteJob, useJob } from './api'
import { JobFormDialog } from './JobFormDialog'

export function JobDetailPage() {
  const { id = '' } = useParams()
  const navigate = useNavigate()
  const query = useJob(id)
  const remove = useDeleteJob()
  const [editOpen, setEditOpen] = useState(false)
  const [applyOpen, setApplyOpen] = useState(false)
  const [deleteOpen, setDeleteOpen] = useState(false)

  if (query.isPending) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-8 w-1/2" />
        <Skeleton className="h-64 w-full" />
      </div>
    )
  }
  if (query.isError) return <ErrorState error={query.error} onRetry={() => query.refetch()} />

  const job = query.data
  const salary = formatSalary(job.salaryMin, job.salaryMax, job.currency)

  const confirmDelete = async () => {
    try {
      await remove.mutateAsync(job.id)
      toast.success('Oferta eliminada')
      navigate('/jobs')
    } catch (error) {
      toast.error(errorMessage(error))
      setDeleteOpen(false)
    }
  }

  return (
    <>
      <Link
        to="/jobs"
        className="mb-3 inline-flex items-center gap-1 text-sm text-muted-foreground hover:text-foreground"
      >
        <ArrowLeft className="size-4" aria-hidden /> Ofertas
      </Link>
      <PageHeader
        title={job.title}
        description={
          <Link to={`/companies/${job.companyId}`} className="font-medium text-primary hover:underline">
            {job.companyName}
          </Link>
        }
        actions={
          <>
            {job.application ? (
              <Button asChild variant="secondary">
                <Link to={`/applications/${job.application.id}`}>Ver postulación</Link>
              </Button>
            ) : (
              <Button onClick={() => setApplyOpen(true)}>
                <Send /> Crear postulación
              </Button>
            )}
            <Button asChild variant="outline">
              <Link to={`/cv-analysis?jobId=${job.id}`}>
                <FileSearch /> Comparar con mi CV
              </Link>
            </Button>
            <Button variant="outline" onClick={() => setEditOpen(true)}>
              <Pencil /> Editar
            </Button>
            <Button variant="ghost" aria-label="Eliminar oferta" onClick={() => setDeleteOpen(true)}>
              <Trash2 />
            </Button>
          </>
        }
      />

      <div className="grid gap-4 lg:grid-cols-[1fr_20rem]">
        <Card>
          <CardHeader>
            <CardTitle>Descripción</CardTitle>
          </CardHeader>
          <CardContent>
            {job.description ? (
              <p className="text-sm leading-relaxed whitespace-pre-line">{job.description}</p>
            ) : (
              <p className="text-sm text-muted-foreground">
                Sin descripción. Pega el texto de la oferta para poder compararlo con tu CV.
              </p>
            )}
            {job.notes ? (
              <div className="mt-6 rounded-lg bg-accent-soft p-3 text-sm">
                <p className="mb-1 font-semibold text-accent">Notas personales</p>
                <p className="whitespace-pre-line">{job.notes}</p>
              </div>
            ) : null}
          </CardContent>
        </Card>

        <div className="space-y-4">
          <Card>
            <CardContent className="space-y-3 pt-5 text-sm">
              <div className="flex items-center justify-between">
                <span className="text-muted-foreground">Postulación</span>
                {job.application ? (
                  <StatusBadge status={job.application.status} />
                ) : (
                  <Badge className="bg-muted">Sin postular</Badge>
                )}
              </div>
              <Detail label="Modalidad" value={WORK_MODE_LABELS[job.workMode]} />
              <Detail label="Contrato" value={CONTRACT_TYPE_LABELS[job.contractType]} />
              <Detail label="Ubicación" value={job.location} />
              <Detail label="Salario" value={salary} />
              <Detail label="Publicada" value={job.publishedOn ? formatDate(job.publishedOn) : null} />
              <Detail label="Fecha límite" value={job.deadline ? formatDate(job.deadline) : null} />
              <Detail label="Guardada" value={formatRelative(job.savedAt)} />
              {job.url ? (
                <a
                  href={job.url}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="inline-flex items-center gap-1 font-medium text-primary hover:underline"
                >
                  Ver oferta original <ExternalLink className="size-3.5" aria-hidden />
                </a>
              ) : null}
            </CardContent>
          </Card>
          <SkillCard title="Habilidades requeridas" skills={job.requiredSkills} />
          <SkillCard title="Habilidades deseables" skills={job.preferredSkills} />
        </div>
      </div>

      <JobFormDialog open={editOpen} onOpenChange={setEditOpen} job={job} />
      <CreateApplicationDialog
        open={applyOpen}
        onOpenChange={setApplyOpen}
        jobOfferId={job.id}
        jobTitle={job.title}
      />
      <ConfirmDialog
        open={deleteOpen}
        onOpenChange={setDeleteOpen}
        title="¿Eliminar oferta?"
        description={
          job.application
            ? 'Esta oferta tiene una postulación. Elimina primero la postulación para poder borrarla.'
            : 'Se eliminará la oferta y sus notas. Los análisis de CV guardados conservarán el título como referencia.'
        }
        confirmLabel="Eliminar"
        pending={remove.isPending}
        onConfirm={confirmDelete}
      />
    </>
  )
}

function Detail({ label, value }: { label: string; value: string | null }) {
  return (
    <div className="flex items-center justify-between gap-4">
      <span className="text-muted-foreground">{label}</span>
      <span className="text-right font-medium">{value ?? '—'}</span>
    </div>
  )
}

function SkillCard({ title, skills }: { title: string; skills: string[] }) {
  if (skills.length === 0) return null
  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-sm">{title}</CardTitle>
      </CardHeader>
      <CardContent className="flex flex-wrap gap-1.5">
        {skills.map((skill) => (
          <Badge key={skill} className="bg-primary-soft text-primary">
            {skill}
          </Badge>
        ))}
      </CardContent>
    </Card>
  )
}
