import { useState } from 'react'
import { toast } from 'sonner'
import { FormField } from '@/components/form-field'
import { StatusBadge } from '@/components/page'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent } from '@/components/ui/dialog'
import { Select, Textarea } from '@/components/ui/form-controls'
import { errorMessage } from '@/lib/api-client'
import { STATUS_META } from '@/lib/labels'
import type { ApplicationStatus, ApplicationSummary } from '@/types/api'
import { useChangeStatus } from './api'

interface Props {
  open: boolean
  onOpenChange: (open: boolean) => void
  application: ApplicationSummary
  /** Pre-selected target (e.g. chosen from a menu). */
  initialStatus?: ApplicationStatus
}

/** Status change with an optional note and, when rejecting, an optional reason. Offers only valid transitions. */
export function ChangeStatusDialog({ open, onOpenChange, ...rest }: Props) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      {/* Mounted only while open, so the form starts fresh every time. */}
      {open ? <ChangeStatusContent onClose={() => onOpenChange(false)} {...rest} /> : null}
    </Dialog>
  )
}

function ChangeStatusContent({
  application,
  initialStatus,
  onClose,
}: Omit<Props, 'open' | 'onOpenChange'> & { onClose: () => void }) {
  const changeStatus = useChangeStatus()
  const allowed = application.allowedTransitions
  const [status, setStatus] = useState<ApplicationStatus | ''>(initialStatus ?? allowed[0] ?? '')
  const [note, setNote] = useState('')
  const [reason, setReason] = useState('')

  const submit = async (event: React.FormEvent) => {
    event.preventDefault()
    if (!status) return
    try {
      await changeStatus.mutateAsync({
        id: application.id,
        status,
        note: note.trim() || null,
        rejectionReason: status === 'rejected' ? reason.trim() || null : null,
      })
      toast.success(`Estado actualizado a «${STATUS_META[status].label}»`)
      onClose()
    } catch (error) {
      toast.error(errorMessage(error))
    }
  }

  return (
    <DialogContent
      title="Cambiar estado"
      description={
        <span className="inline-flex items-center gap-2">
          Estado actual: <StatusBadge status={application.status} />
        </span>
      }
      footer={
        <>
          <Button variant="outline" onClick={onClose}>
            Cancelar
          </Button>
          <Button type="submit" form="status-form" disabled={!status || changeStatus.isPending}>
            {changeStatus.isPending ? 'Guardando…' : 'Guardar'}
          </Button>
        </>
      }
    >
      {allowed.length === 0 ? (
        <p className="text-sm text-muted-foreground">
          Esta postulación está cerrada y su estado ya no puede cambiar.
        </p>
      ) : (
        <form id="status-form" onSubmit={submit} className="space-y-4">
          <FormField label="Nuevo estado" required>
            <Select value={status} onChange={(e) => setStatus(e.target.value as ApplicationStatus)}>
              {allowed.map((s) => (
                <option key={s} value={s}>
                  {STATUS_META[s].label}
                </option>
              ))}
            </Select>
          </FormField>
          {status === 'rejected' ? (
            <FormField label="Motivo del rechazo (opcional)">
              <Textarea
                rows={2}
                maxLength={1000}
                value={reason}
                onChange={(e) => setReason(e.target.value)}
              />
            </FormField>
          ) : null}
          <FormField label="Nota para el historial (opcional)">
            <Textarea rows={2} maxLength={1000} value={note} onChange={(e) => setNote(e.target.value)} />
          </FormField>
        </form>
      )}
    </DialogContent>
  )
}
