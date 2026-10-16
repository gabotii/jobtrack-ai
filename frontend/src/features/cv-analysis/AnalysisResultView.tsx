import { CheckCircle2, CircleHelp, Info, Lightbulb, Quote, TrendingUp } from 'lucide-react'
import type { ReactNode } from 'react'
import { Badge, Card, CardContent, CardHeader, CardTitle } from '@/components/ui/display'
import { formatRelative } from '@/lib/dates'
import type { ResumeAnalysis } from '@/types/api'

function scoreBand(score: number) {
  if (score >= 70) return { label: 'Alta coincidencia', track: 'bg-primary-soft', fill: 'bg-primary' }
  if (score >= 40) return { label: 'Coincidencia parcial', track: 'bg-accent-soft', fill: 'bg-accent' }
  return { label: 'Coincidencia baja', track: 'bg-danger-soft', fill: 'bg-danger' }
}

function Evidence({ label, text }: { label: string; text: string | null }) {
  if (!text) return null
  return (
    <blockquote className="mt-1 flex gap-1.5 border-l-2 border-border pl-2 text-xs text-muted-foreground">
      <Quote className="mt-0.5 size-3 shrink-0" aria-hidden />
      <span>
        <span className="sr-only">{label}: </span>
        {text}
      </span>
    </blockquote>
  )
}

function Section({ title, icon, children }: { title: string; icon: ReactNode; children: ReactNode }) {
  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2 text-sm">
          {icon}
          {title}
        </CardTitle>
      </CardHeader>
      <CardContent>{children}</CardContent>
    </Card>
  )
}

function BulletList({ items, empty }: { items: string[]; empty: string }) {
  if (items.length === 0) return <p className="text-sm text-muted-foreground">{empty}</p>
  return (
    <ul className="list-disc space-y-1.5 pl-5 text-sm">
      {items.map((item) => (
        <li key={item}>{item}</li>
      ))}
    </ul>
  )
}

export function AnalysisResultView({ analysis }: { analysis: ResumeAnalysis }) {
  const { summary, result } = analysis
  const band = scoreBand(result.matchScore)

  return (
    <div className="space-y-4">
      <Card>
        <CardContent className="pt-5">
          <div className="flex flex-wrap items-start justify-between gap-4">
            <div className="min-w-0">
              <p className="text-sm text-muted-foreground">
                {summary.jobTitle} · {summary.companyName}
              </p>
              <p className="text-xs text-muted-foreground">
                CV: {summary.resumeFileName} · {formatRelative(summary.createdAt)} ·{' '}
                {summary.provider === 'local' ? 'Análisis básico (local)' : `IA: ${summary.model}`}
              </p>
            </div>
            <div className="text-right">
              <p className="text-4xl font-semibold">{result.matchScore}%</p>
              <p className="text-xs font-medium text-muted-foreground">{band.label}</p>
            </div>
          </div>
          <div
            className={`mt-3 h-2.5 w-full overflow-hidden rounded-full ${band.track}`}
            role="meter"
            aria-valuemin={0}
            aria-valuemax={100}
            aria-valuenow={result.matchScore}
            aria-label="Compatibilidad orientativa"
          >
            <div className={`h-full rounded-full ${band.fill}`} style={{ width: `${result.matchScore}%` }} />
          </div>
          <p className="mt-3 text-sm">{result.summary}</p>
          <p className="mt-3 flex gap-2 rounded-lg bg-muted px-3 py-2 text-xs text-muted-foreground">
            <Info className="mt-0.5 size-3.5 shrink-0" aria-hidden />
            El porcentaje es orientativo: mide cuánto del texto de la oferta se refleja en tu CV. No es una
            probabilidad de contratación ni una medida objetiva de tu empleabilidad. Úsalo como apoyo, no como
            veredicto.
          </p>
        </CardContent>
      </Card>

      <div className="grid gap-4 lg:grid-cols-2">
        <Section
          title={`Habilidades que coinciden (${result.matchedSkills.length})`}
          icon={<CheckCircle2 className="size-4 text-primary" aria-hidden />}
        >
          {result.matchedSkills.length === 0 ? (
            <p className="text-sm text-muted-foreground">
              No se encontraron coincidencias en el texto del CV.
            </p>
          ) : (
            <ul className="space-y-3 text-sm">
              {result.matchedSkills.map((skill) => (
                <li key={skill.skill}>
                  <span className="font-medium">{skill.skill}</span>
                  <Evidence label="En tu CV" text={skill.resumeEvidence} />
                </li>
              ))}
            </ul>
          )}
        </Section>

        <Section
          title={`No encontradas en el CV (${result.missingSkills.length})`}
          icon={<CircleHelp className="size-4 text-accent" aria-hidden />}
        >
          <p className="mb-3 text-xs text-muted-foreground">
            Que no aparezcan en el texto no significa que no las tengas: si las dominas, hazlas visibles.
          </p>
          {result.missingSkills.length === 0 ? (
            <p className="text-sm text-muted-foreground">
              Todas las habilidades de la oferta aparecen en tu CV.
            </p>
          ) : (
            <ul className="space-y-3 text-sm">
              {result.missingSkills.map((skill) => (
                <li key={skill.skill}>
                  <span className="flex flex-wrap items-center gap-2 font-medium">
                    {skill.skill}
                    <Badge
                      className={
                        skill.required ? 'bg-danger-soft text-danger' : 'bg-muted text-muted-foreground'
                      }
                    >
                      {skill.required ? 'Requerida' : 'Deseable'}
                    </Badge>
                  </span>
                  <Evidence label="En la oferta" text={skill.offerEvidence} />
                </li>
              ))}
            </ul>
          )}
        </Section>

        <Section
          title="Experiencia relevante"
          icon={<TrendingUp className="size-4 text-primary" aria-hidden />}
        >
          {result.relevantExperience.length === 0 ? (
            <p className="text-sm text-muted-foreground">
              No se identificó experiencia directamente relacionada.
            </p>
          ) : (
            <ul className="space-y-3 text-sm">
              {result.relevantExperience.map((item, index) => (
                <li key={index}>
                  {item.description}
                  <Evidence label="En tu CV" text={item.resumeEvidence} />
                </li>
              ))}
            </ul>
          )}
        </Section>

        <Section
          title="Palabras clave que faltan"
          icon={<Info className="size-4 text-muted-foreground" aria-hidden />}
        >
          {result.missingKeywords.length === 0 ? (
            <p className="text-sm text-muted-foreground">Sin palabras clave destacadas pendientes.</p>
          ) : (
            <div className="flex flex-wrap gap-1.5">
              {result.missingKeywords.map((keyword) => (
                <Badge key={keyword} className="bg-muted text-foreground">
                  {keyword}
                </Badge>
              ))}
            </div>
          )}
        </Section>

        <Section title="Fortalezas" icon={<CheckCircle2 className="size-4 text-primary" aria-hidden />}>
          <BulletList items={result.strengths} empty="Sin fortalezas destacadas para esta oferta." />
        </Section>

        <Section title="Áreas de mejora" icon={<CircleHelp className="size-4 text-accent" aria-hidden />}>
          <BulletList items={result.improvementAreas} empty="No se detectaron áreas de mejora concretas." />
        </Section>
      </div>

      <Section
        title="Recomendaciones para adaptar tu CV"
        icon={<Lightbulb className="size-4 text-accent" aria-hidden />}
      >
        <BulletList items={result.recommendations} empty="Sin recomendaciones adicionales." />
        <p className="mt-3 text-xs text-muted-foreground">
          Incluye solo experiencia, títulos y habilidades reales. Las citas mostradas se han comprobado contra
          el texto de tu CV y de la oferta.
        </p>
      </Section>
    </div>
  )
}
