import {
  BellRing,
  BriefcaseBusiness,
  Building2,
  CalendarDays,
  FileSearch,
  KanbanSquare,
  LayoutDashboard,
  ListChecks,
  LogOut,
  Menu,
  UserRound,
  X,
} from 'lucide-react'
import { useState } from 'react'
import { NavLink, Outlet } from 'react-router'
import { Button } from '@/components/ui/button'
import { useAuth } from '@/features/auth/auth-context'
import { cn } from '@/lib/utils'

const navigation = [
  { to: '/', label: 'Panel', icon: LayoutDashboard, end: true },
  { to: '/kanban', label: 'Tablero', icon: KanbanSquare },
  { to: '/applications', label: 'Postulaciones', icon: ListChecks },
  { to: '/jobs', label: 'Ofertas', icon: BriefcaseBusiness },
  { to: '/companies', label: 'Empresas', icon: Building2 },
  { to: '/calendar', label: 'Calendario', icon: CalendarDays },
  { to: '/reminders', label: 'Recordatorios', icon: BellRing },
  { to: '/cv-analysis', label: 'Análisis de CV', icon: FileSearch },
]

function Brand() {
  return (
    <div className="flex items-center gap-2 px-2 text-base font-bold text-white">
      <span className="grid size-8 place-items-center rounded-lg bg-primary">
        <BriefcaseBusiness className="size-4" aria-hidden />
      </span>
      JobTrack <span className="text-teal-300">AI</span>
    </div>
  )
}

function NavItems({ onNavigate }: { onNavigate?: () => void }) {
  return (
    <ul className="space-y-1">
      {navigation.map(({ to, label, icon: Icon, end }) => (
        <li key={to}>
          <NavLink
            to={to}
            end={end}
            onClick={onNavigate}
            className={({ isActive }) =>
              cn(
                'flex items-center gap-3 rounded-lg px-3 py-2 text-sm font-medium transition-colors',
                isActive
                  ? 'bg-sidebar-active text-white'
                  : 'text-sidebar-foreground hover:bg-white/5 hover:text-white',
              )
            }
          >
            <Icon className="size-4.5" aria-hidden />
            {label}
          </NavLink>
        </li>
      ))}
    </ul>
  )
}

function UserFooter({ onNavigate }: { onNavigate?: () => void }) {
  const { user, logout } = useAuth()
  return (
    <div className="border-t border-white/10 pt-4">
      <NavLink
        to="/profile"
        onClick={onNavigate}
        className={({ isActive }) =>
          cn(
            'flex items-center gap-3 rounded-lg px-3 py-2 text-sm',
            isActive
              ? 'bg-sidebar-active text-white'
              : 'text-sidebar-foreground hover:bg-white/5 hover:text-white',
          )
        }
      >
        <UserRound className="size-4.5" aria-hidden />
        <span className="min-w-0">
          <span className="block truncate font-medium">{user?.fullName}</span>
          <span className="block truncate text-xs opacity-70">{user?.email}</span>
        </span>
      </NavLink>
      <button
        type="button"
        onClick={() => void logout()}
        className="mt-1 flex w-full items-center gap-3 rounded-lg px-3 py-2 text-sm text-sidebar-foreground hover:bg-white/5 hover:text-white"
      >
        <LogOut className="size-4.5" aria-hidden />
        Cerrar sesión
      </button>
    </div>
  )
}

export function AppLayout() {
  const [mobileOpen, setMobileOpen] = useState(false)

  return (
    <div className="min-h-dvh lg:grid lg:grid-cols-[15rem_1fr]">
      <a
        href="#main"
        className="sr-only focus:not-sr-only focus:fixed focus:top-2 focus:left-2 focus:z-50 focus:rounded-lg focus:bg-surface focus:px-3 focus:py-2"
      >
        Saltar al contenido
      </a>

      {/* Desktop sidebar */}
      <aside className="sticky top-0 hidden h-dvh flex-col gap-6 bg-sidebar p-4 lg:flex">
        <Brand />
        <nav aria-label="Principal" className="flex-1 overflow-y-auto">
          <NavItems />
        </nav>
        <UserFooter />
      </aside>

      {/* Mobile top bar + drawer */}
      <header className="sticky top-0 z-30 flex items-center justify-between bg-sidebar px-4 py-3 lg:hidden">
        <Brand />
        <Button
          variant="ghost"
          size="icon"
          className="text-white hover:bg-white/10"
          aria-label={mobileOpen ? 'Cerrar menú' : 'Abrir menú'}
          aria-expanded={mobileOpen}
          aria-controls="mobile-nav"
          onClick={() => setMobileOpen((open) => !open)}
        >
          {mobileOpen ? <X /> : <Menu />}
        </Button>
      </header>
      {mobileOpen ? (
        <div
          id="mobile-nav"
          className="fixed inset-x-0 top-[60px] bottom-0 z-30 flex flex-col gap-4 bg-sidebar p-4 lg:hidden"
        >
          <nav aria-label="Principal" className="flex-1 overflow-y-auto">
            <NavItems onNavigate={() => setMobileOpen(false)} />
          </nav>
          <UserFooter onNavigate={() => setMobileOpen(false)} />
        </div>
      ) : null}

      <main id="main" className="min-w-0 px-4 py-6 sm:px-6 lg:px-8 lg:py-8">
        <Outlet />
      </main>
    </div>
  )
}
