import http from 'k6/http';
import { check, sleep } from 'k6';
import { Trend, Rate, Counter } from 'k6/metrics';

/**
 * Escenario de Calidad AC01 & AC02: Stress Test de Checkout Concurrente
 * SIGIC Ferretería v2.2 (IS-488 UNSCH)
 * 
 * Simula 100 usuarios concurrentes (VUs) durante 30 segundos compitiendo
 * simultáneamente por stock crítico de alta rotación (Cemento Portland y Fierro 1/2").
 * 
 * Invariantes evaluadas:
 * - RNF-CON-003: Disponibilidad = Físico - Comprometido.
 * - RF-WEB-005 / Invariante 3: Orden determinista anti-deadlock y bloqueo pesimista corto.
 * - AC01: Latencia P95 < 250ms en checkout bajo contención.
 * - Resiliencia: 0% errores 500 (Internal Server Error) o deadlocks PostgreSQL (código 40P01).
 */

const checkoutLatency = new Trend('sigic_checkout_duration_ms');
const successfulCheckouts = new Rate('sigic_checkout_success_rate');
const stockConflictRate = new Rate('sigic_stock_conflict_rate');
const serverErrorCount = new Counter('sigic_500_errors');

export const options = {
  scenarios: {
    checkout_high_concurrency: {
      executor: 'constant-vus',
      vus: 100,
      duration: '30s',
    },
  },
  thresholds: {
    // AC01: Latencia en percentil 95 menor a 250 milisegundos
    'http_req_duration': ['p(95)<250'],
    'sigic_checkout_duration_ms': ['p(95)<250'],
    
    // Tasa de error de infraestructura < 5% (tolerando 200/201 venta y 409 conflicto controlado)
    'http_req_failed': ['rate<0.05'],
    'sigic_500_errors': ['count==0'],
  },
};

// Configura status codes esperados: 200/201 (éxito) y 409 (conflicto de stock controlado)
// Evita que k6 marque 409 como falla de red/infraestructura en 'http_req_failed'
http.setResponseCallback(http.expectedStatuses(200, 201, 409));

const BASE_URL = __ENV.SIGIC_API_URL || 'http://localhost:5000';

export default function () {
  const url = `${BASE_URL}/api/v1/checkout/procesar`;

  // Payload con múltiples líneas de ferretería pesada
  // Ordenado deterministamente por productoId ASC para cumplir Invariante 3
  const payload = JSON.stringify({
    clienteId: 'c1010101-0000-0000-0000-000000000001',
    tipoCanal: Math.random() > 0.5 ? 'POS' : 'WEB',
    tipoComprobante: 'BOLETA',
    items: [
      {
        productoId: 'd8279f63-3aa9-4746-bf94-638f4383fcd2', // Cemento Portland Tipo I (42.5 kg)
        sku: 'CEM-PORT-001',
        cantidad: Math.floor(Math.random() * 5) + 1,
        precioUnitario: 28.50
      },
      {
        productoId: 'e5a40b8e-5b12-4f2b-8a71-6c2e3f890123', // Fierro Corrugado 1/2" ASTM A615
        sku: 'FIE-ACER-12',
        cantidad: Math.floor(Math.random() * 10) + 1,
        precioUnitario: 42.00
      }
    ]
  });

  const params = {
    headers: {
      'Content-Type': 'application/json',
      'Accept': 'application/json',
      'X-Idempotency-Key': `k6-${__VU}-${__ITER}-${Date.now()}`
    },
    timeout: '5s'
  };

  const startTime = Date.now();
  const res = http.post(url, payload, params);
  const duration = Date.now() - startTime;
  checkoutLatency.add(duration);

  const isSuccess = res.status === 200 || res.status === 201;
  const isStockConflict = res.status === 409;
  const isServerError = res.status >= 500;

  successfulCheckouts.add(isSuccess ? 1 : 0);
  stockConflictRate.add(isStockConflict ? 1 : 0);
  if (isServerError) {
    serverErrorCount.add(1);
  }

  check(res, {
    'Status es 200, 201 o 409 (Sin 500)': (r) => isSuccess || isStockConflict,
    'Sin deadlock PostgreSQL (SQLSTATE no es 40P01)': (r) => {
      if (r.body && typeof r.body === 'string') {
        return !r.body.includes('40P01') && !r.body.includes('deadlock detected');
      }
      return true;
    },
    'Latencia sub-250ms': () => duration < 250
  });

  // Pacing controlado entre transacciones (100ms - 200ms)
  sleep(0.1 + Math.random() * 0.1);
}
