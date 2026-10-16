import type { ComponentProps } from 'react'
import { Select } from '@/components/ui/form-controls'
import { useResumes } from './api'

/** Select of the user's uploaded CVs; works with react-hook-form's register() spread. */
export function ResumeSelect(props: ComponentProps<'select'>) {
  const resumes = useResumes()
  return (
    <Select {...props} disabled={props.disabled || resumes.isPending}>
      <option value="">{resumes.isPending ? 'Cargando…' : 'Sin especificar'}</option>
      {resumes.data?.map((resume) => (
        <option key={resume.id} value={resume.id}>
          {resume.fileName}
        </option>
      ))}
    </Select>
  )
}
