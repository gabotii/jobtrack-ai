import { Download, Eye, FileSearch, FileText, ShieldCheck, Sparkles, Trash2, Upload } from 'lucide-react'
import { useRef, useState, type DragEvent } from 'react'
import { Link, useSearchParams } from 'react-router'
import { toast } from 'sonner'
import { FormField } from '@/components/form-field'
import { EmptyState, ErrorState, PageHeader } from '@/components/page'
import { Button } from '@/components/ui/button'
import { ConfirmDialog, Dialog, DialogContent } from '@/components/ui/dialog'
import { Card, CardContent, CardHeader, CardTitle, Skeleton, Spinner } from '@/components/ui/display'
import { Checkbox, Select } from '@/components/ui/form-controls'
import { useJobs } from '@/features/jobs/api'
import { downloadFile, errorMessage } from '@/lib/api-client'
import { formatRelative } from '@/lib/dates'
import { cn } from '@/lib/utils'
import type { Resume } from '@/types/api'
import {
  MAX_RESUME_BYTES,
  useAnalyses,
  useAnalysis,
  useAnalysisProviders,
  useCreateAnalysis,
  useDeleteAnalysis,
  useDeleteResume,
  useResumeDetail,
  useResumes,
  useUploadResume,
} from './api'
import { AnalysisResultView } from './AnalysisResultView'

export function CvAnalysisPage() {
  const [searchParams] = useSearchParams()
  const [selectedAnalysisId, setSelectedAnalysisId] = useState<string | null>(null)
  const selected = useAnalysis(selectedAnalysisId)

  return (
    <>
      <PageHeader
        title="Análisis de CV"
        description="Compara tu CV con una oferta para ver qué coincide, qué falta y cómo adaptarlo."
      />
      <div className="grid gap-4 xl:grid-cols-[22rem_1fr]">
        <div className="space-y-4">
          <ResumeLibrary />
          <AnalysisForm initialJobId={searchParams.get('jobId') ?? ''} onCreated={setSelectedAnalysisId} />
          <AnalysisHistory selectedId={selectedAnalysisId} onSelect={setSelectedAnalysisId} />
        </div>
        <div>
          {selectedAnalysisId === null ? (
            <EmptyState
              icon={FileSearch}
              title="Elige un CV y una oferta"
              description="El resultado aparecerá aquí. Podrás volver a consultarlo en el historial."
            />
          ) : selected.isPending ? (
            <Skeleton className="h-96 w-full" />
          ) : selected.isError ? (
            <ErrorState error={selected.error} onRetry={() => selected.refetch()} />
          ) : (
            <AnalysisResultView analysis={selected.data} />
          )}
        </div>
      </div>
    </>
  )
}

function formatSize(bytes: number) {
  return bytes < 1024 * 1024 ? `${Math.round(bytes / 1024)} KB` : `${(bytes / 1024 / 1024).toFixed(1)} MB`
}

function ResumeLibrary() {
  const resumes = useResumes()
  const upload = useUploadResume()
  const remove = useDeleteResume()
  const inputRef = useRef<HTMLInputElement>(null)
  const [dragging, setDragging] = useState(false)
  const [viewing, setViewing] = useState<Resume | null>(null)
  const [deleting, setDeleting] = useState<Resume | null>(null)

  const uploadFile = async (file: File | undefined) => {
    if (!file) return
    const extension = file.name.split('.').pop()?.toLowerCase()
    if (extension !== 'pdf' && extension !== 'docx') {
      toast.error('Solo se admiten archivos PDF o DOCX.')
      return
    }
    if (file.size > MAX_RESUME_BYTES) {
      toast.error('El archivo supera el tamaño máximo de 5 MB.')
      return
    }
    try {
      await upload.mutateAsync(file)
      toast.success('CV subido. Revisa el texto extraído antes de analizarlo.')
    } catch (error) {
      toast.error(errorMessage(error))
    } finally {
      if (inputRef.current) inputRef.current.value = ''
    }
  }

  const onDrop = (event: DragEvent) => {
    event.preventDefault()
    setDragging(false)
    void uploadFile(event.dataTransfer.files[0])
  }

  const confirmDelete = async () => {
    if (!deleting) return
    try {
      await remove.mutateAsync(deleting.id)
      toast.success('CV y análisis asociados eliminados')
    } catch (error) {
      toast.error(errorMessage(error))
    } finally {
      setDeleting(null)
    }
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>Mis CV</CardTitle>
      </CardHeader>
      <CardContent className="space-y-3">
        <div
          onDragOver={(e) => {
            e.preventDefault()
            setDragging(true)
          }}
          onDragLeave={() => setDragging(false)}
          onDrop={onDrop}
          className={cn(
            'rounded-lg border-2 border-dashed p-4 text-center text-sm transition-colors',
            dragging ? 'border-primary bg-primary-soft' : 'border-border',
          )}
        >
          <Upload className="mx-auto mb-2 size-5 text-muted-foreground" aria-hidden />
          <p className="text-muted-foreground">Arrastra tu CV aquí o</p>
          <Button
            variant="outline"
            size="sm"
            className="mt-2"
            disabled={upload.isPending}
            onClick={() => inputRef.current?.click()}
          >
            {upload.isPending ? 'Subiendo…' : 'Seleccionar archivo'}
          </Button>
          <input
            ref={inputRef}
            type="file"
            accept=".pdf,.docx,application/pdf,application/vnd.openxmlformats-officedocument.wordprocessingml.document"
            className="sr-only"
            aria-label="Subir CV en PDF o DOCX"
            onChange={(e) => void uploadFile(e.target.files?.[0])}
          />
          <p className="mt-2 text-xs text-muted-foreground">
            PDF o DOCX, máximo 5 MB. Los PDF escaneados no tienen texto.
          </p>
        </div>

        {resumes.isPending ? (
          <Skeleton className="h-16 w-full" />
        ) : resumes.isError ? (
          <p className="text-sm text-danger">{errorMessage(resumes.error)}</p>
        ) : resumes.data.length === 0 ? (
          <p className="text-sm text-muted-foreground">Todavía no has subido ningún CV.</p>
        ) : (
          <ul className="divide-y divide-border">
            {resumes.data.map((resume) => (
              <li key={resume.id} className="flex items-center gap-2 py-2 text-sm">
                <FileText className="size-4 shrink-0 text-muted-foreground" aria-hidden />
                <div className="min-w-0 flex-1">
                  <p className="truncate font-medium">{resume.fileName}</p>
                  <p className="text-xs text-muted-foreground">
                    {formatSize(resume.sizeBytes)} · {formatRelative(resume.createdAt)} ·{' '}
                    {resume.analysisCount} análisis
                  </p>
                </div>
                <Button
                  variant="ghost"
                  size="icon"
                  aria-label={`Ver texto extraído de ${resume.fileName}`}
                  onClick={() => setViewing(resume)}
                >
                  <Eye />
                </Button>
                <Button
                  variant="ghost"
                  size="icon"
                  aria-label={`Descargar ${resume.fileName}`}
                  onClick={() =>
                    downloadFile(`/resumes/${resume.id}/file`, resume.fileName).catch((error) =>
                      toast.error(errorMessage(error)),
                    )
                  }
                >
                  <Download />
                </Button>
                <Button
                  variant="ghost"
                  size="icon"
                  aria-label={`Eliminar ${resume.fileName}`}
                  onClick={() => setDeleting(resume)}
                >
                  <Trash2 />
                </Button>
              </li>
            ))}
          </ul>
        )}
      </CardContent>
      <ExtractedTextDialog resume={viewing} onClose={() => setViewing(null)} />
      <ConfirmDialog
        open={deleting !== null}
        onOpenChange={(open) => !open && setDeleting(null)}
        title="¿Eliminar CV?"
        description={`Se eliminarán «${deleting?.fileName}», su texto extraído y los análisis basados en él. Las postulaciones que lo usaban quedarán sin CV asociado.`}
        confirmLabel="Eliminar"
        pending={remove.isPending}
        onConfirm={confirmDelete}
      />
    </Card>
  )
}

function ExtractedTextDialog({ resume, onClose }: { resume: Resume | null; onClose: () => void }) {
  const detail = useResumeDetail(resume?.id ?? null)
  return (
    <Dialog open={resume !== null} onOpenChange={(open) => !open && onClose()}>
      <DialogContent
        size="lg"
        title="Texto extraído"
        description="Esto es exactamente lo que se compara con las ofertas. Si falta información, revisa el formato del archivo."
      >
        {detail.isPending ? (
          <Spinner />
        ) : detail.isError ? (
          <p className="text-sm text-danger">{errorMessage(detail.error)}</p>
        ) : (
          <pre className="max-h-[60vh] overflow-auto rounded-lg bg-muted p-3 font-sans text-xs whitespace-pre-wrap">
            {detail.data.extractedText}
          </pre>
        )}
      </DialogContent>
    </Dialog>
  )
}

function AnalysisForm({
  initialJobId,
  onCreated,
}: {
  initialJobId: string
  onCreated: (id: string) => void
}) {
  const resumes = useResumes()
  const jobs = useJobs({ pageSize: 100, sort: 'savedAt', desc: true })
  const providers = useAnalysisProviders()
  const create = useCreateAnalysis()
  const [chosenResumeId, setResumeId] = useState('')
  const [jobId, setJobId] = useState(initialJobId)
  const [providerId, setProviderId] = useState('local')
  const [consent, setConsent] = useState(false)

  // Until the user picks one, the most recent CV is selected.
  const resumeId = chosenResumeId || resumes.data?.[0]?.id || ''

  const provider = providers.data?.find((p) => p.id === providerId)
  const needsConsent = provider?.external ?? false
  const canSubmit =
    resumeId && jobId && provider?.available && (!needsConsent || consent) && !create.isPending

  const submit = async (event: React.FormEvent) => {
    event.preventDefault()
    if (!canSubmit) return
    try {
      const analysis = await create.mutateAsync({
        resumeId,
        jobOfferId: jobId,
        provider: providerId,
        consentToExternalProcessing: needsConsent && consent,
      })
      onCreated(analysis.summary.id)
      toast.success('Análisis completado')
    } catch (error) {
      toast.error(errorMessage(error))
    }
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>Nuevo análisis</CardTitle>
      </CardHeader>
      <CardContent>
        <form onSubmit={submit} className="space-y-4">
          <FormField label="CV" required>
            <Select value={resumeId} onChange={(e) => setResumeId(e.target.value)}>
              <option value="">{resumes.data?.length === 0 ? 'Sube primero un CV' : 'Selecciona…'}</option>
              {resumes.data?.map((r) => (
                <option key={r.id} value={r.id}>
                  {r.fileName}
                </option>
              ))}
            </Select>
          </FormField>
          <FormField
            label="Oferta"
            required
            hint={
              jobs.data?.items.length === 0 ? (
                <>
                  No tienes ofertas guardadas.{' '}
                  <Link to="/jobs" className="text-primary hover:underline">
                    Guarda una
                  </Link>
                  .
                </>
              ) : undefined
            }
          >
            <Select value={jobId} onChange={(e) => setJobId(e.target.value)}>
              <option value="">Selecciona…</option>
              {jobs.data?.items.map((job) => (
                <option key={job.id} value={job.id}>
                  {job.title} — {job.companyName}
                </option>
              ))}
            </Select>
          </FormField>

          <fieldset className="space-y-2">
            <legend className="mb-1 text-sm font-medium">Tipo de análisis</legend>
            {providers.isPending ? (
              <Spinner />
            ) : (
              providers.data?.map((p) => (
                <label
                  key={p.id}
                  className={cn(
                    'flex cursor-pointer gap-3 rounded-lg border p-3 text-sm',
                    providerId === p.id ? 'border-primary bg-primary-soft/50' : 'border-border',
                    !p.available && 'cursor-not-allowed opacity-60',
                  )}
                >
                  <input
                    type="radio"
                    name="provider"
                    value={p.id}
                    className="mt-0.5 accent-primary"
                    checked={providerId === p.id}
                    disabled={!p.available}
                    onChange={() => {
                      setProviderId(p.id)
                      setConsent(false)
                    }}
                  />
                  <span>
                    <span className="flex items-center gap-1.5 font-medium">
                      {p.external ? (
                        <Sparkles className="size-3.5 text-accent" aria-hidden />
                      ) : (
                        <ShieldCheck className="size-3.5 text-primary" aria-hidden />
                      )}
                      {p.name}
                    </span>
                    <span className="block text-xs text-muted-foreground">
                      {p.available
                        ? p.description
                        : 'No disponible: el servidor no tiene configurada una clave de IA.'}
                    </span>
                  </span>
                </label>
              ))
            )}
          </fieldset>

          {needsConsent ? (
            <div className="rounded-lg border border-accent/40 bg-accent-soft p-3 text-xs">
              <p className="mb-2">
                Para este análisis, el <strong>texto de tu CV y de la oferta se enviará a Anthropic</strong>{' '}
                (proveedor de Claude) a través de su API. JobTrack no usa tu CV para entrenar modelos;
                consulta la política de datos del proveedor si tienes dudas. El archivo original no se envía.
              </p>
              <label className="flex items-start gap-2 font-medium">
                <Checkbox
                  checked={consent}
                  onChange={(e) => setConsent(e.target.checked)}
                  className="mt-0.5"
                />
                Acepto enviar el texto de mi CV para este análisis.
              </label>
            </div>
          ) : null}

          <Button type="submit" className="w-full" disabled={!canSubmit}>
            {create.isPending ? (
              <>
                <Spinner className="text-white" label="Analizando" /> Analizando…
              </>
            ) : (
              'Analizar'
            )}
          </Button>
        </form>
      </CardContent>
    </Card>
  )
}

function AnalysisHistory({
  selectedId,
  onSelect,
}: {
  selectedId: string | null
  onSelect: (id: string | null) => void
}) {
  const analyses = useAnalyses()
  const remove = useDeleteAnalysis()

  const onDelete = async (id: string) => {
    try {
      await remove.mutateAsync(id)
      if (selectedId === id) onSelect(null)
      toast.success('Análisis eliminado')
    } catch (error) {
      toast.error(errorMessage(error))
    }
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>Historial</CardTitle>
      </CardHeader>
      <CardContent>
        {analyses.isPending ? (
          <Skeleton className="h-16 w-full" />
        ) : analyses.isError ? (
          <p className="text-sm text-danger">{errorMessage(analyses.error)}</p>
        ) : analyses.data.length === 0 ? (
          <p className="text-sm text-muted-foreground">Aún no hay análisis.</p>
        ) : (
          <ul className="space-y-1">
            {analyses.data.map((a) => (
              <li key={a.id} className="flex items-center gap-1">
                <button
                  type="button"
                  onClick={() => onSelect(a.id)}
                  aria-current={selectedId === a.id ? 'true' : undefined}
                  className={cn(
                    'flex min-w-0 flex-1 items-center gap-3 rounded-lg p-2 text-left text-sm hover:bg-muted',
                    selectedId === a.id && 'bg-muted',
                  )}
                >
                  <span className="w-11 shrink-0 text-right font-semibold">{a.matchScore}%</span>
                  <span className="min-w-0">
                    <span className="block truncate font-medium">{a.jobTitle}</span>
                    <span className="block truncate text-xs text-muted-foreground">
                      {a.companyName} · {a.provider === 'local' ? 'básico' : 'IA'} ·{' '}
                      {formatRelative(a.createdAt)}
                    </span>
                  </span>
                </button>
                <Button
                  variant="ghost"
                  size="icon"
                  aria-label={`Eliminar análisis de ${a.jobTitle}`}
                  onClick={() => void onDelete(a.id)}
                >
                  <Trash2 />
                </Button>
              </li>
            ))}
          </ul>
        )}
      </CardContent>
    </Card>
  )
}
