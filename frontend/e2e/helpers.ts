import { expect, type Page } from '@playwright/test'

export async function registerNewUser(page: Page, name = 'E2E Tester') {
  const email = `e2e-${Date.now()}-${Math.random().toString(36).slice(2, 8)}@test.local`
  await page.goto('/register')
  await page.getByLabel(/Nombre completo/).fill(name)
  await page.getByLabel(/Correo electrónico/).fill(email)
  await page.getByLabel(/^Contraseña/).fill('E2eSecret1')
  await page.getByLabel(/Repite la contraseña/).fill('E2eSecret1')
  await page.getByRole('button', { name: 'Crear cuenta' }).click()
  await expect(page.getByRole('heading', { name: `Hola, ${name.split(' ')[0]}` })).toBeVisible()
  return email
}

export async function createCompany(page: Page, name: string) {
  await page.goto('/companies')
  await page.getByRole('button', { name: 'Nueva empresa' }).first().click()
  const dialog = page.getByRole('dialog')
  await dialog.getByLabel(/^Nombre/).fill(name)
  await dialog.getByLabel(/Industria/).fill('Software')
  await dialog.getByRole('button', { name: 'Guardar' }).click()
  await expect(page.getByText('Empresa creada')).toBeVisible()
}

export async function createOffer(
  page: Page,
  title: string,
  company: string,
  description: string,
  skills: string,
) {
  await page.goto('/jobs')
  await page.getByRole('button', { name: 'Guardar oferta' }).first().click()
  const dialog = page.getByRole('dialog')
  await dialog.getByLabel(/Título del puesto/).fill(title)
  await dialog.getByLabel(/^Empresa/).selectOption({ label: company })
  await dialog.getByLabel(/Habilidades requeridas/).fill(skills)
  await dialog.getByLabel(/^Descripción/).fill(description)
  await dialog.getByRole('button', { name: 'Guardar' }).click()
  await expect(page.getByText('Oferta guardada')).toBeVisible()
}

/** A minimal, valid single-page PDF with real text (offsets computed so any PDF parser can read it). */
export function buildPdf(lines: string[]): Buffer {
  const content = [
    'BT',
    '/F1 11 Tf',
    '14 TL',
    '50 780 Td',
    ...lines.map((l) => `(${l.replace(/[()\\]/g, '')}) Tj T*`),
    'ET',
  ].join('\n')
  const objects = [
    '<< /Type /Catalog /Pages 2 0 R >>',
    '<< /Type /Pages /Kids [3 0 R] /Count 1 >>',
    '<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>',
    `<< /Length ${Buffer.byteLength(content)} >>\nstream\n${content}\nendstream`,
    '<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>',
  ]
  let pdf = '%PDF-1.4\n'
  const offsets: number[] = []
  objects.forEach((body, index) => {
    offsets.push(Buffer.byteLength(pdf))
    pdf += `${index + 1} 0 obj\n${body}\nendobj\n`
  })
  const xref = Buffer.byteLength(pdf)
  pdf += `xref\n0 ${objects.length + 1}\n0000000000 65535 f \n`
  pdf += offsets.map((o) => `${String(o).padStart(10, '0')} 00000 n \n`).join('')
  pdf += `trailer\n<< /Size ${objects.length + 1} /Root 1 0 R >>\nstartxref\n${xref}\n%%EOF\n`
  return Buffer.from(pdf, 'latin1')
}
