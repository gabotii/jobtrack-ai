import { BriefcaseBusiness, CalendarCheck2, KanbanSquare, Sparkles } from 'lucide-react'
import type { ReactNode } from 'react'

const highlights = [
  { icon: KanbanSquare, text: 'Tablero Kanban con el estado real de cada postulación' },
  { icon: CalendarCheck2, text: 'Entrevistas y recordatorios en tu zona horaria' },
  { icon: Sparkles, text: 'Compara tu CV con cada oferta y detecta lo que falta' },
]

export function AuthLayout({
  title,
  subtitle,
  children,
}: {
  title: string
  subtitle: string
  children: ReactNode
}) {
  return (
    <div className="grid min-h-dvh lg:grid-cols-[1fr_1.1fr]">
      <aside className="hidden flex-col justify-between bg-sidebar p-10 text-sidebar-foreground lg:flex">
        <div className="flex items-center gap-2 text-lg font-bold text-white">
          <span className="grid size-9 place-items-center rounded-lg bg-primary">
            <BriefcaseBusiness className="size-5" aria-hidden />
          </span>
          JobTrack AI
        </div>
        <div>
          <p className="max-w-md text-3xl leading-tight font-bold text-white">
            Tu búsqueda de empleo, ordenada y bajo control.
          </p>
          <ul className="mt-8 space-y-4">
            {highlights.map(({ icon: Icon, text }) => (
              <li key={text} className="flex items-center gap-3">
                <Icon className="size-5 text-teal-300" aria-hidden />
                {text}
              </li>
            ))}
          </ul>
        </div>
        <p className="text-xs text-sidebar-foreground/70">Tus datos son privados y solo tú puedes verlos.</p>
      </aside>
      <main className="flex items-center justify-center px-4 py-10 sm:px-8">
        <div className="w-full max-w-sm">
          <div className="mb-8 flex items-center gap-2 font-bold lg:hidden">
            <span className="grid size-8 place-items-center rounded-lg bg-primary text-white">
              <BriefcaseBusiness className="size-4" aria-hidden />
            </span>
            JobTrack AI
          </div>
          <h1 className="text-2xl font-bold tracking-tight">{title}</h1>
          <p className="mt-1 mb-6 text-sm text-muted-foreground">{subtitle}</p>
          {children}
        </div>
      </main>
    </div>
  )
}
