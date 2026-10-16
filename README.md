# JobTrack AI

Gestor inteligente de postulaciones laborales: organiza empresas, ofertas, postulaciones, entrevistas y recordatorios
en un solo lugar, con un tablero Kanban, un calendario, un panel con métricas reales y comparación de tu CV con cada
oferta (análisis local o con IA).

> Decisiones técnicas, modelo de datos y reglas de negocio: [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

## Funcionalidades

| Área | Qué incluye |
|---|---|
| Cuenta | Registro, inicio y cierre de sesión, perfil (nombre y zona horaria), cambio de contraseña, bloqueo tras 5 intentos fallidos. |
| Panel | Totales, postulaciones activas, entrevistas próximas, ofertas sin postular, rechazadas, seguimientos pendientes, recordatorios vencidos, gráfico semanal, distribución por estado, próximos eventos, actividad reciente y guía de primeros pasos. |
| Empresas | CRUD con contacto y notas; no se pueden borrar mientras tengan ofertas. |
| Ofertas | CRUD con modalidad, contrato, salario y moneda, habilidades requeridas/deseables, fechas; búsqueda, filtros, orden y paginación. Una oferta puede existir sin postulación. |
| Postulaciones | Una por oferta, estados con reglas de transición, historial con notas, CV utilizado, carta, fuente, próxima acción, seguimiento, motivo de rechazo. Listado con filtros. |
| Tablero Kanban | Arrastrar y soltar (ratón, táctil y teclado) con persistencia, menú «Mover a…» como alternativa accesible, filtros por empresa, fechas y columnas visibles, *rollback* si la API rechaza el cambio. |
| Calendario | Vistas mes, semana, día y agenda; eventos (entrevista, llamada, seguimiento, recordatorio, otro) con zona horaria, enlace de videollamada y estado (programado, realizado, cancelado). |
| Recordatorios | Fecha y hora, prioridad, completar y reabrir, vinculación con postulaciones y eventos. Se muestran dentro de la app (sin correo todavía). |
| Análisis de CV | Subida de PDF/DOCX, revisión del texto extraído, análisis local (sin salida de datos) o con Claude (con consentimiento explícito), resultado con evidencias citadas e historial. |

## Stack

- **Backend:** .NET 10 (LTS), ASP.NET Core Web API, EF Core 10 + PostgreSQL 17 (Npgsql), ASP.NET Core Identity,
  JWT, FluentValidation, Swagger (Swashbuckle), PdfPig, Open XML SDK, SDK oficial de Anthropic.
- **Frontend:** React 19, TypeScript, Vite 8, React Router 7, Tailwind CSS 4, componentes estilo shadcn/ui sobre Radix,
  TanStack Query 5, React Hook Form + Zod, FullCalendar 6, dnd-kit, Recharts 3, date-fns + @date-fns/tz.
- **Calidad:** xUnit + WebApplicationFactory (PostgreSQL real), Vitest + Testing Library + MSW, Playwright, ESLint,
  Prettier, GitHub Actions.
- **Despliegue:** Docker (imágenes multi-etapa, usuario no root) y Docker Compose (PostgreSQL + API + nginx).

## Puesta en marcha rápida (Docker)

Requisitos: Docker con Compose v2.

```bash
cp .env.example .env
# Edita .env: POSTGRES_PASSWORD y JWT_SIGNING_KEY (32+ caracteres aleatorios, p. ej. `openssl rand -base64 48`).
docker compose up -d --build
```

Abre <http://localhost:8080> (o el puerto de `WEB_PORT`) y crea una cuenta. Las migraciones se aplican al arrancar.

Datos de demostración (opcional, crea solo la cuenta `demo@jobtrack.local`, nunca toca otras cuentas):

```bash
# Con DEMO_PASSWORD definido en .env (si falta, se genera una contraseña y se muestra una sola vez).
docker compose run --rm -e ASPNETCORE_ENVIRONMENT=Demo api seed-demo
```

El comando se niega a ejecutarse con `ASPNETCORE_ENVIRONMENT=Production`.

## Desarrollo local

Requisitos: .NET SDK 10, Node.js 22.12+ o 24, PostgreSQL 17 (local o en Docker).

### 1. Base de datos

```bash
# Opción Docker:
docker run -d --name jobtrack-db -p 5432:5432 -e POSTGRES_USER=jobtrack -e POSTGRES_PASSWORD=jobtrack_dev \
  -e POSTGRES_DB=jobtrack postgres:17-alpine
```

La cadena de conexión de desarrollo está en `backend/src/JobTrack.Api/appsettings.Development.json`
(`Host=127.0.0.1;Database=jobtrack;Username=jobtrack;Password=jobtrack_dev`). El rol necesita `CREATEDB` para las
pruebas de integración (crean y eliminan bases temporales).

### 2. API

```bash
cd backend
dotnet run --project src/JobTrack.Api                 # http://localhost:5080, Swagger en /swagger
dotnet run --project src/JobTrack.Api -- seed-demo    # cuenta demo: demo@jobtrack.local / Demo12345
```

En desarrollo las migraciones se aplican al arrancar. Para crear una nueva:

```bash
dotnet tool restore
dotnet ef migrations add <Nombre> -p src/JobTrack.Infrastructure -s src/JobTrack.Api -o Persistence/Migrations
```

### 3. Frontend

```bash
cd frontend
npm install
npm run dev            # http://localhost:5173 (redirige /api a http://127.0.0.1:5080)
```

## Configuración

Variables de entorno de la API (formato `Seccion__Clave`):

| Variable | Obligatoria | Descripción |
|---|---|---|
| `ConnectionStrings__Default` | Sí | Cadena de conexión de PostgreSQL. |
| `Jwt__SigningKey` | Sí | Clave HMAC de al menos 32 caracteres. La API no arranca sin ella. |
| `Jwt__AccessTokenMinutes` / `Jwt__RefreshTokenDays` | No | 15 min / 14 días por defecto. |
| `Database__MigrateOnStartup` | No | Aplica migraciones al arrancar (`true` en desarrollo y en Compose). |
| `Cors__AllowedOrigins__0` | No | Solo si el frontend se sirve desde otro origen (no hace falta con el proxy). |
| `AuthCookie__Secure` | No | `true` por defecto. Los navegadores aceptan cookies `Secure` en `http://localhost`. |
| `ForwardedHeaders__Enabled` | No | Actívalo solo detrás de un proxy de confianza (Compose lo hace). |
| `RateLimiting__AuthPerMinute`, `__RefreshPerMinute`, `__UploadsPerHour`, `__AnalysesPerHour` | No | 10, 60, 20, 15. |
| `Storage__RootPath` | No | Carpeta privada de CV (`App_Data/uploads`; volumen `uploads` en Compose). |
| `Swagger__Enabled` | No | Expone `/swagger` fuera de desarrollo. |
| `Ai__Anthropic__ApiKey` | No | Activa el análisis con IA. Sin clave, solo está disponible el análisis local. |
| `Ai__Anthropic__Model`, `__Effort`, `__TimeoutSeconds`, `__MaxRetries` | No | `claude-opus-5-5`, `medium`, 120, 2. |

Los secretos se pasan por variables de entorno o *user secrets* (`dotnet user-secrets set "Ai:Anthropic:ApiKey" ...`
en `src/JobTrack.Api`), nunca en archivos versionados.

### Configurar Claude (opcional)

1. Crea una clave en la consola de Anthropic.
2. Defínela en `ANTHROPIC_API_KEY` (Compose) o `Ai__Anthropic__ApiKey`.
3. Reinicia la API. En «Análisis de CV» aparecerá «Análisis con IA», que pide consentimiento explícito antes de enviar
   el texto del CV y de la oferta (el archivo original no se envía). JobTrack no usa los CV para entrenar modelos.

> Esta integración no se ha ejecutado contra la API real en el entorno de desarrollo (no había clave). Está cubierta
> por pruebas del comportamiento sin clave, de la construcción del *prompt* y del saneamiento de resultados.

## Pruebas

```bash
# Backend (requiere PostgreSQL; por defecto usa el servidor local con el rol jobtrack)
cd backend
dotnet test
# o con otro servidor:
JOBTRACK_TEST_DB="Host=...;Username=...;Password=...;Database=postgres" dotnet test

# Frontend
cd frontend
npm run lint && npm run typecheck && npm test

# E2E (con la app en marcha; contra el build de producción o Docker Compose)
npm run build && npm run preview                       # http://localhost:4173, con proxy a la API
E2E_BASE_URL=http://localhost:4173 npm run e2e         # E2E_CHANNEL=msedge para usar Edge instalado
E2E_BASE_URL=http://localhost:8080 npm run e2e         # contra Docker Compose (requiere la cuenta demo)
```

> **Windows con Smart App Control:** puede bloquear DLL recién compiladas (`0x800711C7`). `backend/scripts/test.sh`
> recompila con otro hash y reintenta automáticamente.

Resultados verificados al cerrar esta versión: backend 142/142, frontend 33/33, E2E 5/5 (en `vite preview` y en
Docker Compose). El *workflow* de CI (`.github/workflows/ci.yml`) reproduce esos pasos, pero aún no se ha ejecutado en
GitHub.

## API

Documentación interactiva en `/swagger` (desarrollo o `Swagger__Enabled=true`). Resumen (`/api/v1`):

| Recurso | Endpoints |
|---|---|
| Autenticación | `POST /auth/register`, `/auth/login`, `/auth/refresh`, `/auth/logout` |
| Usuario | `GET/PUT /users/me`, `POST /users/me/password` |
| Panel | `GET /dashboard` |
| Empresas | `GET/POST /companies`, `GET/PUT/DELETE /companies/{id}` |
| Ofertas | `GET/POST /jobs`, `GET/PUT/DELETE /jobs/{id}` |
| Postulaciones | `GET/POST /applications`, `GET /applications/board`, `GET/PUT/DELETE /applications/{id}`, `PUT /applications/{id}/status`, `GET /applications/{id}/history` |
| Calendario | `GET/POST /calendar/events`, `GET /calendar/events/upcoming`, `GET/PUT/DELETE /calendar/events/{id}`, `PUT /calendar/events/{id}/status` |
| Recordatorios | `GET/POST /reminders`, `GET/PUT/DELETE /reminders/{id}`, `POST /reminders/{id}/complete`, `POST /reminders/{id}/reopen` |
| CV | `GET/POST /resumes`, `GET/DELETE /resumes/{id}`, `GET /resumes/{id}/file` |
| Análisis | `GET /resume-analyses/providers`, `GET/POST /resume-analyses`, `GET/DELETE /resume-analyses/{id}` |

Convenciones: JSON en camelCase, enums como texto (`"toApply"`), fechas `yyyy-MM-dd`, instantes ISO 8601 **con**
desplazamiento (se devuelven en UTC), colecciones paginadas `{ items, page, pageSize, totalCount, totalPages }`,
errores como *problem details* con `code` y `traceId`.

## Estructura

```
backend/
  src/JobTrack.Domain          entidades, enums y reglas de negocio
  src/JobTrack.Application     casos de uso, DTOs, validadores, análisis local de CV
  src/JobTrack.Infrastructure  EF Core, migraciones, Identity/JWT, almacenamiento, PDF/DOCX, Claude, seed
  src/JobTrack.Api             controladores, middleware, seguridad, Swagger
  tests/JobTrack.Tests         pruebas unitarias e integración
frontend/
  src/app                      layout, rutas y proveedores
  src/components               componentes UI reutilizables
  src/features/*               auth, dashboard, companies, jobs, applications, kanban, calendar, reminders,
                               cv-analysis, profile
  src/lib                      cliente HTTP, fechas y zonas horarias, etiquetas
  e2e/                         pruebas Playwright
docs/ARCHITECTURE.md           decisiones técnicas
docker-compose.yml             stack completo
```

## Limitaciones y trabajo pendiente

- **Notificaciones:** los recordatorios solo se ven dentro de la app; falta un servicio en segundo plano con envío de
  correo (el modelo y los índices ya lo contemplan).
- **IA:** pendiente de validar con una clave real; sin OCR para PDF escaneados.
- **Sesiones:** un access token sigue siendo válido hasta 15 min tras cerrar sesión; no hay verificación de email ni
  recuperación de contraseña (requieren un servicio de correo).
- **Concurrencia:** sin control optimista al editar la misma postulación desde dos pestañas.
- **Escalado:** los CV se guardan en disco/volumen; con varias réplicas habría que usar almacenamiento de objetos.
- **UI:** solo español y tema claro; el bundle inicial (~165 KB gzip) admite optimización.
- **CI:** el workflow está escrito y sus pasos se han ejecutado localmente, pero no se ha lanzado en GitHub.

<img width="1903" height="904" alt="image" src="https://github.com/user-attachments/assets/a519bfed-939d-4a93-b07c-f9f5a39f14e8" />
