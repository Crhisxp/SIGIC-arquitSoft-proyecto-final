import { test, expect } from '@playwright/test';

/**
 * Suite de Pruebas End-to-End (E2E) Automatizadas — SIGIC Ferretería v2.2
 * Criterio 6: Verificación E2E de Venta POS y Concurrencia Omnicanal
 * 
 * Herramientas: Playwright con motor Chromium Headless
 * Endpoints evaluados:
 * - POS Mostrador (LAN): https://pos.ferreteria-sigic.pe
 * - Portal Web E-Commerce: https://tienda.ferreteria-sigic.pe
 * - API Gateway: https://api.ferreteria-sigic.pe
 */

test.describe('E2E: Venta Rápida POS y Concurrencia Omnicanal', () => {

  const BASE_URL_POS = process.env.SIGIC_POS_URL || 'https://pos.ferreteria-sigic.pe';
  const BASE_URL_WEB = process.env.SIGIC_WEB_URL || 'https://tienda.ferreteria-sigic.pe';

  test('Escenario 1: Flujo feliz de venta rápida en mostrador POS 100% por teclado', async ({ page }) => {
    // 1. Acceso a terminal POS y Login con PIN
    await page.goto(`${BASE_URL_POS}/login`);
    await expect(page).toHaveTitle(/SIGIC - Terminal POS/);

    // Ingreso de PIN de cajero asignado a Turno
    const pinInput = page.locator('input[data-testid="input-pin-cajero"]');
    await pinInput.fill('4821');
    await page.keyboard.press('Enter');

    // Validación de sesión activa en Caja 1
    const badgeCaja = page.locator('[data-testid="badge-caja-activa"]');
    await expect(badgeCaja).toHaveText(/Caja 01 - LAN/);

    // 2. Escaneo rápido de SKU por código de barras (Fierro Corrugado 1/2")
    const barcodeInput = page.locator('input[data-testid="input-scanner-sku"]');
    await expect(barcodeInput).toBeFocused();

    // Simula lectura de escáner óptico enviando SKU + Enter
    await barcodeInput.fill('FIE-ACER-12');
    await page.keyboard.press('Enter');

    // 3. Verificación de cálculo tributario en grilla de venta
    const filaItem = page.locator('[data-testid="item-grid-FIE-ACER-12"]');
    await expect(filaItem).toBeVisible();
    await expect(filaItem.locator('.item-descripcion')).toContainText('Fierro Corrugado 1/2" ASTM A615');
    await expect(filaItem.locator('.item-cantidad')).toHaveValue('1');

    // Verificación de Subtotal, IGV (18%) y Total
    const subtotal = page.locator('[data-testid="lbl-subtotal"]');
    const igv = page.locator('[data-testid="lbl-igv"]');
    const total = page.locator('[data-testid="lbl-total"]');

    // Precio unitario: S/ 42.00 (Base: 35.59, IGV: 6.41)
    await expect(total).toHaveText(/S\/\s*42\.00/);
    await expect(igv).toHaveText(/S\/\s*6\.41/);

    // 4. Panel de cobro rápido con tecla de función F9
    await page.keyboard.press('F9');
    const modalCobro = page.locator('[data-testid="modal-panel-cobro"]');
    await expect(modalCobro).toBeVisible();

    // Pago en efectivo exacto (S/ 50.00 con vuelto de S/ 8.00)
    const inputEfectivo = page.locator('input[data-testid="input-monto-efectivo"]');
    await inputEfectivo.fill('50.00');
    const lblVuelto = page.locator('[data-testid="lbl-vuelto"]');
    await expect(lblVuelto).toHaveText(/S\/\s*8\.00/);

    // 5. Confirmación de venta y emisión de comprobante con F10 (Boleta Electrónica)
    await page.keyboard.press('F10');

    // Notificación de éxito y retorno de foco automático a escáner
    const toastExito = page.locator('[data-testid="toast-venta-exitosa"]');
    await expect(toastExito).toContainText(/Boleta B001-\d+ emitida con éxito/);
    await expect(barcodeInput).toBeFocused();
  });

  test('Escenario 2: Intento concurrente web con rechazo controlado por falta de stock', async ({ browser }) => {
    // Contexto dual: Terminal POS (Cajero) y Navegador Web (Cliente en línea)
    const contextPOS = await browser.newContext();
    const contextWEB = await browser.newContext();

    const pagePOS = await contextPOS.newPage();
    const pageWEB = await contextWEB.newPage();

    // 1. Preparación del Carrito Web con las últimas 5 varillas de fierro
    await pageWEB.goto(`${BASE_URL_WEB}/producto/FIE-ACER-12`);
    await pageWEB.locator('button[data-testid="btn-agregar-carrito"]').click();
    await pageWEB.goto(`${BASE_URL_WEB}/checkout`);

    // El cliente web visualiza stock disponible preliminar
    const stockWebBadge = pageWEB.locator('[data-testid="badge-disponibilidad"]');
    await expect(stockWebBadge).toContainText(/Stock disponible/);

    // 2. Simultáneamente, el cajero en mostrador POS factura las últimas 5 existencias físicas
    await pagePOS.goto(`${BASE_URL_POS}/ventas`);
    const barcodeInput = pagePOS.locator('input[data-testid="input-scanner-sku"]');
    await barcodeInput.fill('FIE-ACER-12');
    await pagePOS.keyboard.press('Enter');

    // Cambia cantidad a 5 unidades mediante F4
    await pagePOS.keyboard.press('F4');
    await pagePOS.locator('input[data-testid="input-editar-cantidad"]').fill('5');
    await pagePOS.keyboard.press('Enter');

    // Cobra en efectivo y confirma venta física (descuenta stock_fisico en PostgreSQL)
    await pagePOS.keyboard.press('F9');
    await pagePOS.locator('input[data-testid="input-monto-efectivo"]').fill('210.00');
    await pagePOS.keyboard.press('F10');
    await expect(pagePOS.locator('[data-testid="toast-venta-exitosa"]')).toBeVisible();

    // 3. El cliente web intenta presionar "Confirmar Pedido y Pagar" instantes después
    const btnConfirmarWeb = pageWEB.locator('button[data-testid="btn-confirmar-pedido"]');
    await btnConfirmarWeb.click();

    // 4. Verificación del rechazo atómico amigable (HTTP 409 Conflict manejado por UI)
    // El backend rechaza por Invariante RNF-CON-003: (stock_fisico - stock_comprometido) < cantidad
    const alertaStockAgotado = pageWEB.locator('[data-testid="alerta-stock-insuficiente"]');
    await expect(alertaStockAgotado).toBeVisible();
    await expect(alertaStockAgotado).toContainText(/Stock no disponible: El producto 'Fierro Corrugado 1\/2"' se agotó en tienda/);

    // Verificación estricta de estabilidad: NO debe existir error 500 ni pantalla en blanco
    const error500Boundary = pageWEB.locator('text=Internal Server Error');
    await expect(error500Boundary).not.toBeVisible();
    await expect(pageWEB).not.toHaveURL(/error-500/);

    await contextPOS.close();
    await contextWEB.close();
  });

});
