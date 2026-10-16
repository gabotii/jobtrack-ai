import { expect, test } from '@playwright/test'
import { buildPdf, createCompany, createOffer, registerNewUser } from './helpers'

test('a new user tracks an application from offer to interview', async ({ page }) => {
  await registerNewUser(page)
  await expect(page.getByText('Primeros pasos')).toBeVisible()

  await createCompany(page, 'Acme E2E')
  await createOffer(
    page,
    'Ingeniera Backend',
    'Acme E2E',
    'Buscamos experiencia con C# y React.',
    'C#, React',
  )

  // Create the application from the offer page.
  await page.getByRole('link', { name: 'Ingeniera Backend' }).click()
  await page.getByRole('button', { name: 'Crear postulación' }).click()
  await page.getByRole('dialog').getByLabel('Próxima acción').fill('Enviar portafolio')
  await page.getByRole('dialog').getByRole('button', { name: 'Crear postulación' }).click()
  await expect(page.getByRole('heading', { name: 'Ingeniera Backend' })).toBeVisible()
  await expect(page.getByText('Creada como «Postulada»')).toBeVisible()

  // Change the status from the detail page; the history records it.
  await page.getByRole('button', { name: 'Cambiar estado' }).click()
  await page
    .getByRole('dialog')
    .getByLabel(/Nuevo estado/)
    .selectOption({ label: 'Entrevista' })
  await page
    .getByRole('dialog')
    .getByLabel(/Nota para el historial/)
    .fill('Me contactaron por LinkedIn')
  await page.getByRole('dialog').getByRole('button', { name: 'Guardar' }).click()
  await expect(page.getByText('Postulada → Entrevista')).toBeVisible()
  await expect(page.getByText('“Me contactaron por LinkedIn”')).toBeVisible()

  // Schedule an interview linked to the application.
  await page.getByRole('button', { name: 'Programar' }).click()
  const eventDialog = page.getByRole('dialog')
  await eventDialog.getByLabel(/Fecha de inicio/).fill('2030-05-20')
  await eventDialog.getByLabel(/Hora de inicio/).fill('10:00')
  await eventDialog.getByLabel(/Fecha de fin/).fill('2030-05-20')
  await eventDialog.getByLabel(/Hora de fin/).fill('11:00')
  await eventDialog.getByRole('button', { name: 'Guardar' }).click()
  await expect(page.getByText('Evento programado')).toBeVisible()
  await expect(page.getByText(/20 may 2030, 10:00/)).toBeVisible()

  // Move the card on the Kanban board with the accessible menu; the change survives a reload.
  await page.goto('/kanban')
  const interviewColumn = page.getByRole('region', { name: 'Entrevista' })
  await expect(interviewColumn.getByText('Ingeniera Backend')).toBeVisible()
  await page.getByRole('button', { name: 'Mover «Ingeniera Backend» a otro estado' }).click()
  await page.getByRole('menuitem', { name: 'Oferta recibida' }).click()
  await expect(page.getByText('«Ingeniera Backend» movida a Oferta recibida')).toBeVisible()
  await page.reload()
  await expect(
    page.getByRole('region', { name: 'Oferta recibida' }).getByText('Ingeniera Backend'),
  ).toBeVisible()

  // Reminders: create and complete.
  await page.goto('/reminders')
  await page.getByRole('button', { name: 'Nuevo recordatorio' }).first().click()
  await page
    .getByRole('dialog')
    .getByLabel(/^Título/)
    .fill('Preparar preguntas técnicas')
  await page.getByRole('dialog').getByRole('button', { name: 'Guardar' }).click()
  await page.getByRole('button', { name: 'Marcar «Preparar preguntas técnicas» como completado' }).click()
  await expect(page.getByText('Recordatorio completado')).toBeVisible()
  await page.getByRole('tab', { name: 'Completados' }).click()
  await expect(page.getByText('Preparar preguntas técnicas')).toBeVisible()

  // The dashboard reflects the real data.
  await page.goto('/')
  const indicators = page.getByRole('region', { name: 'Indicadores' })
  const tile = (label: string) => indicators.getByRole('link', { name: new RegExp(`^${label}`) })
  await expect(tile('Postulaciones')).toContainText('1')
  await expect(tile('Entrevistas próximas')).toContainText('1')
  await expect(page.getByText('Primeros pasos')).toBeHidden()

  // Logging out protects private routes.
  await page.getByRole('button', { name: 'Cerrar sesión' }).click()
  await page.goto('/kanban')
  await expect(page).toHaveURL(/\/login$/)
})

test('a user cannot open another user’s data', async ({ browser }) => {
  const ownerPage = await (await browser.newContext()).newPage()
  await registerNewUser(ownerPage, 'Owner')
  await createCompany(ownerPage, 'Empresa privada')
  await ownerPage.getByRole('link', { name: /Empresa privada/ }).click()
  await ownerPage.waitForURL(/\/companies\/[0-9a-f-]{36}$/)
  const privateUrl = ownerPage.url()

  const intruderPage = await (await browser.newContext()).newPage()
  await registerNewUser(intruderPage, 'Intruder')
  await intruderPage.goto(privateUrl)
  await expect(intruderPage.getByText('No se pudo cargar la información')).toBeVisible()
  await expect(intruderPage.getByText('Empresa no encontrado.')).toBeVisible()
  await expect(intruderPage.getByText('Empresa privada')).toBeHidden()
})

test('a CV can be uploaded and compared with an offer locally', async ({ page }) => {
  await registerNewUser(page, 'Candidata')
  await createCompany(page, 'DataCorp')
  await createOffer(
    page,
    'Data Engineer',
    'DataCorp',
    'Pipelines con Python y SQL. Valorable Airflow.',
    'Python, SQL, Airflow',
  )

  await page.goto('/cv-analysis')
  await page.getByLabel('Subir CV en PDF o DOCX').setInputFiles({
    name: 'cv-candidata.pdf',
    mimeType: 'application/pdf',
    buffer: buildPdf([
      'Candidata Ejemplo - Ingeniera de datos',
      'Tres anos construyendo pipelines ETL con Python y SQL sobre PostgreSQL.',
      'Visualizacion de datos con Power BI y despliegues con Docker.',
    ]),
  })
  await expect(page.getByText('CV subido.', { exact: false })).toBeVisible()

  await page.getByRole('button', { name: 'Ver texto extraído de cv-candidata.pdf' }).click()
  await expect(page.getByRole('dialog').getByText(/pipelines ETL con Python/)).toBeVisible()
  await page.keyboard.press('Escape')

  await page.getByLabel(/^Oferta/).selectOption({ label: 'Data Engineer — DataCorp' })
  await expect(
    page.getByText('No disponible: el servidor no tiene configurada una clave de IA.'),
  ).toBeVisible()
  await page.getByRole('button', { name: 'Analizar' }).click()

  await expect(page.getByText('Análisis completado')).toBeVisible()
  await expect(page.getByRole('meter', { name: 'Compatibilidad orientativa' })).toHaveAttribute(
    'aria-valuenow',
    '67',
  )
  await expect(page.getByText('No es una probabilidad de contratación', { exact: false })).toBeVisible()
  await expect(page.getByText('Airflow').first()).toBeVisible()
})

test('the demo account shows seeded data', async ({ page }) => {
  await page.goto('/login')
  await page.getByLabel(/Correo electrónico/).fill('demo@jobtrack.local')
  await page.getByLabel(/Contraseña/).fill('Demo12345')
  await page.getByRole('button', { name: 'Entrar' }).click()
  await expect(page.getByRole('heading', { name: 'Hola, Usuario' })).toBeVisible()
  await expect(page.getByText('Entrevista técnica').first()).toBeVisible()
  await expect(page.getByRole('img', { name: /Gráfico de columnas/ })).toBeVisible()

  await page.getByRole('button', { name: /Ver Postulaciones por semana como tabla/ }).click()
  await expect(page.getByRole('columnheader', { name: 'Semana' })).toBeVisible()
})
