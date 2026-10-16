import { createBrowserRouter, Link, type RouteObject } from 'react-router'
import { EmptyState } from '@/components/page'
import { Button } from '@/components/ui/button'
import { LoginPage } from '@/features/auth/LoginPage'
import { RegisterPage } from '@/features/auth/RegisterPage'
import { RedirectIfAuthenticated, RequireAuth } from '@/features/auth/RequireAuth'
import { SearchX } from 'lucide-react'
import { AppLayout } from './AppLayout'
import { RouteError } from './RouteError'

function NotFound() {
  return (
    <EmptyState
      icon={SearchX}
      title="Página no encontrada"
      description="La dirección no existe o el recurso ya no está disponible."
      action={
        <Button asChild>
          <Link to="/">Volver al panel</Link>
        </Button>
      }
    />
  )
}

// Feature pages are code-split so heavy libraries (calendar, charts, drag and drop) load only when needed.
export const routes: RouteObject[] = [
  {
    path: '/login',
    element: (
      <RedirectIfAuthenticated>
        <LoginPage />
      </RedirectIfAuthenticated>
    ),
  },
  {
    path: '/register',
    element: (
      <RedirectIfAuthenticated>
        <RegisterPage />
      </RedirectIfAuthenticated>
    ),
  },
  {
    path: '/',
    element: (
      <RequireAuth>
        <AppLayout />
      </RequireAuth>
    ),
    errorElement: <RouteError />,
    children: [
      {
        index: true,
        lazy: () =>
          import('@/features/dashboard/DashboardPage').then((m) => ({ Component: m.DashboardPage })),
      },
      {
        path: 'kanban',
        lazy: () => import('@/features/kanban/KanbanPage').then((m) => ({ Component: m.KanbanPage })),
      },
      {
        path: 'applications',
        lazy: () =>
          import('@/features/applications/ApplicationsPage').then((m) => ({ Component: m.ApplicationsPage })),
      },
      {
        path: 'applications/:id',
        lazy: () =>
          import('@/features/applications/ApplicationDetailPage').then((m) => ({
            Component: m.ApplicationDetailPage,
          })),
      },
      {
        path: 'jobs',
        lazy: () => import('@/features/jobs/JobsPage').then((m) => ({ Component: m.JobsPage })),
      },
      {
        path: 'jobs/:id',
        lazy: () => import('@/features/jobs/JobDetailPage').then((m) => ({ Component: m.JobDetailPage })),
      },
      {
        path: 'companies',
        lazy: () =>
          import('@/features/companies/CompaniesPage').then((m) => ({ Component: m.CompaniesPage })),
      },
      {
        path: 'companies/:id',
        lazy: () =>
          import('@/features/companies/CompanyDetailPage').then((m) => ({ Component: m.CompanyDetailPage })),
      },
      {
        path: 'calendar',
        lazy: () => import('@/features/calendar/CalendarPage').then((m) => ({ Component: m.CalendarPage })),
      },
      {
        path: 'reminders',
        lazy: () =>
          import('@/features/reminders/RemindersPage').then((m) => ({ Component: m.RemindersPage })),
      },
      {
        path: 'cv-analysis',
        lazy: () =>
          import('@/features/cv-analysis/CvAnalysisPage').then((m) => ({ Component: m.CvAnalysisPage })),
      },
      {
        path: 'profile',
        lazy: () => import('@/features/profile/ProfilePage').then((m) => ({ Component: m.ProfilePage })),
      },
      { path: '*', element: <NotFound /> },
    ],
  },
]

export const router = createBrowserRouter(routes)
