import http from 'k6/http';
import { sleep } from 'k6';
import exec from 'k6/execution';
import { Counter, Rate, Trend } from 'k6/metrics';
import { api, capacity, idCarril, login, params, registrarRespuesta, skuWeb, smoke, stage, tienda, umbralesRespuesta, vender, vus } from './local.js';

const aislamiento = new Trend('pos_relativo_al_p95_base');
const ttlValido = new Rate('ttl_reserva_correcto');
const reservas = new Counter('reservas_confirmadas');
const ttl = Number(__ENV.EXPECTED_CARD_TTL_MINUTES || 20);
if (!Number.isInteger(ttl) || ttl <= 0) throw new Error('EXPECTED_CARD_TTL_MINUTES debe ser un entero positivo.');
export const options = {
  scenarios: {
    web: { executor: 'ramping-vus', exec: 'portal', startVUs: 0,
      stages: [0.1, 0.5, 1, 1, 0].map(n => ({ duration: stage, target: Math.ceil(n * (vus - 1)) })) },
    caja: { executor: 'constant-vus', exec: 'canario', vus: 1, duration: smoke ? '10s' : '100s' },
  },
  thresholds: {
    ...umbralesRespuesta('catalogo'),
    ...umbralesRespuesta('reserva', capacity),
    ...umbralesRespuesta('canario_pos', true),
    reservas_confirmadas: ['count>0'],
    ttl_reserva_correcto: ['rate==1'],
    ...(capacity ? { 'http_req_duration{scenario:caja}': ['p(95)<250'] } : {}),
    ...(capacity && !smoke ? { pos_relativo_al_p95_base: ['p(95)<=1.10'] } : {}),
  },
};

export function setup() {
  const staff = login(true);
  const web = login(false);
  const tiempos = [];
  for (let i = 0; i < 40; i++) {
    const r = vender(staff.token);
    if (r.status !== 201) throw new Error('Falló la venta de referencia POS, HTTP ' + r.status);
    if (i >= 5) tiempos.push(r.timings.duration);
    sleep(0.1);
  }
  tiempos.sort((a, b) => a - b);
  const p95 = tiempos[Math.ceil(0.95 * tiempos.length) - 1];
  console.log('Referencia p95 de venta POS (ms): ' + p95);
  return { staff, web, p95 };
}

export function canario({ staff, p95 }) {
  const venta = vender(staff.token);
  registrarRespuesta(venta, 'canario_pos', 201, true);
  aislamiento.add(venta.timings.duration / p95);
  sleep(0.1);
}

export function portal({ web }) {
  const catalogo = http.get(api + '/public/catalogo/productos?search=K6-WEB', {
    tags: { name: 'catalogo' }, timeout: '10s', redirects: 0, responseCallback: http.expectedStatuses(200),
  });
  registrarRespuesta(catalogo, 'catalogo', 200);
  if (__ITER % 3 === 0) {
    const r = http.post(api + '/web/pedidos/reservar', JSON.stringify({
      clienteId: web.id, metodoPago: 'PasarelaTarjeta', modalidadEntrega: 'RetiroEnTienda', tiendaDestinoId: tienda,
      items: [{ productoId: capacity ? idCarril('web', exec.vu.idInTest) : skuWeb, cantidad: 1,
        precioUnitario: 15, unidadMedida: 'UND', factorConversion: 1 }],
    }), { ...params(web.token, 'reserva'), responseCallback: http.expectedStatuses(201, 409) });
    registrarRespuesta(r, 'reserva', 201, true);
    reservas.add(r.status === 201 ? 1 : 0);
    if (r.status === 201) {
      let fechaCreacion;
      let fechaExpiracion;
      try {
        fechaCreacion = Date.parse(r.json('fechaCreacionUtc'));
        fechaExpiracion = Date.parse(r.json('fechaExpiracionUtc'));
      } catch (_) {
        // Una respuesta de éxito sin el contrato de fechas incumple el TTL.
      }
      ttlValido.add(fechaExpiracion - fechaCreacion === ttl * 60000);
    }
  }
  sleep(0.1);
}
