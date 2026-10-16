import { expect, test } from '@playwright/test'

test('the app is usable on a phone without horizontal page scroll', async ({ page }) => {
  await page.goto('/login')
  await page.getByLabel(/Correo electrónico/).fill('demo@jobtrack.local')
  await page.getByLabel(/Contraseña/).fill('Demo12345')
  await page.getByRole('button', { name: 'Entrar' }).click()
  await expect(page.getByRole('heading', { name: 'Hola, Usuario' })).toBeVisible()

  for (const path of ['/', '/applications', '/calendar', '/cv-analysis']) {
    await page.goto(path)
    await page.waitForLoadState('networkidle')
    const overflow = await page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth)
    expect(overflow, `horizontal overflow on ${path}`).toBeLessThanOrEqual(1)
  }

  await page.getByRole('button', { name: 'Abrir menú' }).click()
  await page.getByRole('link', { name: 'Tablero' }).click()
  await expect(page.getByRole('heading', { name: 'Tablero' })).toBeVisible()
  await expect(page.getByRole('region', { name: 'Entrevista' })).toBeVisible()
})
