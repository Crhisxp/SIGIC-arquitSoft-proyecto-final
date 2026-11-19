import http from 'k6/http';
import { sleep } from 'k6';
import { Rate, Trend } from 'k6/metrics';
import { api, login, params, skuWeb, smoke, stage, tienda, vender, vus } from './local.js';

const aislamiento = new Trend('pos_relativo_al_p95_base');
const ttlValido = new Rate('ttl_reserva_correcto');
const reservas = new Rate('reservas_confirmadas');
const ttl = Number(__ENV.EXPECTED_CARD_TTL_MINUTES || 20);
export const options = {
  scenarios: {
    web: { executor: 'ramping-vus', exec: 'portal', startVUs: 0,
      stages: [0.1, 0.5, 1, 1, 0].map(n => ({ duration: stage, target: Math.ceil(n * (vus - 1)) })) },
    caja: { executor: 'constant-vus', exec: 'canario', vus: 1, duration: smoke ? '10s' : '100s' },
  },
  thresholds: { http_req_failed: ['rate<0.01'], reservas_confirmadas: ['rate>0.99'], ttl_reserva_correcto: ['rate==1'],
    pos_relativo_al_p95_base: ['p(95)<=1.10'], 'http_req_duration{scenario:caja}': ['p(95)<250'] },
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
  aislamiento.add(venta.timings.duration / p95);
  sleep(0.1);
}

export function portal({ web }) {
  http.get(api + '/public/catalogo/productos?search=K6-WEB', { tags: { name: 'catalogo' }, redirects: 0 });
  if (__ITER % 3 === 0) {
    const r = http.post(api + '/web/pedidos/reservar', JSON.stringify({
      clienteId: web.id, metodoPago: 'PasarelaTarjeta', modalidadEntrega: 'RetiroEnTienda', tiendaDestinoId: tienda,
      items: [{ productoId: skuWeb, cantidad: 1, precioUnitario: 15, unidadMedida: 'UND', factorConversion: 1 }],
    }), params(web.token, 'reserva'));
    reservas.add(r.status === 201);
    const orden = r.status === 201 ? r.json() : {};
    ttlValido.add(Date.parse(orden.fechaExpiracionUtc) - Date.parse(orden.fechaCreacionUtc) === ttl * 60000);
  }
  sleep(0.1);
}
