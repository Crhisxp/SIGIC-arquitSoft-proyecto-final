import http from 'k6/http';
import { Rate } from 'k6/metrics';

const origin = __ENV.BASE_URL || 'http://127.0.0.1:5000';
if (!/^https?:\/\/(localhost|127\.0\.0\.1|\[::1\])(:\d+)?$/.test(origin)) {
  throw new Error('BASE_URL debe ser el origen de la API local de ensayo.');
}
export const api = origin + '/api/v1';
export const tienda = '22222222-2222-2222-2222-222222222222';
export const skuPos = 'd8279f63-3aa9-4746-bf94-638f4383fcd2';
export const skuWeb = 'e5a40b8e-5b12-4f2b-8a71-6c2e3f890123';
export const smoke = __ENV.SMOKE === 'true';
const modo = __ENV.LOAD_MODE || 'contention';
if (!['contention', 'capacity'].includes(modo)) throw new Error('LOAD_MODE debe ser contention o capacity.');
export const capacity = modo === 'capacity';
export const vus = Number(__ENV.VUS || (smoke ? 2 : 2000));
if (!Number.isInteger(vus) || vus < 2 || vus > 2000) throw new Error('VUS debe ser un entero entre 2 y 2000.');
export const stage = smoke ? '2s' : '20s';
export const turnoPos = '44444444-4444-4444-4444-444444444444';

const fallosTecnicos = new Rate('fallos_tecnicos');
const rechazosStock = new Rate('rechazos_stock');
const respuestasInesperadas = new Rate('respuestas_inesperadas');

export function registrarRespuesta(r, operacion, esperado, admiteStock = false) {
  const tecnico = r.status === 0 || r.status === 408 || r.status >= 500;
  const stock = admiteStock && r.status === 409;
  const tags = { operacion };
  fallosTecnicos.add(tecnico, tags);
  rechazosStock.add(stock, tags);
  respuestasInesperadas.add(!tecnico && !stock && r.status !== esperado, tags);
}

export function umbralesRespuesta(operacion, sinAgotamiento = false) {
  return {
    [`fallos_tecnicos{operacion:${operacion}}`]: ['rate<0.01'],
    [`respuestas_inesperadas{operacion:${operacion}}`]: ['rate==0'],
    [`rechazos_stock{operacion:${operacion}}`]: [sinAgotamiento ? 'rate==0' : 'rate>=0'],
  };
}

export function idCarril(tipo, id) {
  const prefijo = { pos: 'a1520201', web: 'a1520202', turno: 'a1520203' }[tipo];
  return `${prefijo}-0000-4000-8000-${String(id).padStart(12, '0')}`;
}

export function params(token, name) {
  return { headers: { 'Content-Type': 'application/json', Authorization: 'Bearer ' + token },
    tags: { name }, timeout: '10s', redirects: 0 };
}

export function login(staff) {
  const kind = staff ? 'staff' : 'web';
  const res = http.post(api + '/auth/login-' + kind,
    JSON.stringify({ username: staff ? 'k6.cajero' : 'k6.cliente', password: 'K6-solo-local' }),
    { headers: { 'Content-Type': 'application/json' }, redirects: 0 });
  if (res.status !== 200 || !res.json('token')) throw new Error('Login ' + kind + ' falló. Verifique fixtures.sql.');
  return { token: res.json('token'), id: res.json('userId') };
}

export function ventaPayload(productoId = skuPos, cantidad = 1, turnoCajaId = turnoPos) {
  return JSON.stringify({
    turnoCajaId, clienteId: 'c1010101-0000-0000-0000-000000000001',
    items: [{ productoId, cantidad, unidadMedida: 'UND', precioUnitario: 15 }],
    pagos: [{ metodoPago: 'Efectivo', monto: cantidad * 15 }], tipoComprobante: 'Boleta',
  });
}

export function vender(token, productoId, turnoCajaId) {
  return http.post(api + '/pos/ventas', ventaPayload(productoId, 1, turnoCajaId), {
    ...params(token, 'venta_pos'), responseCallback: http.expectedStatuses(201, 409),
  });
}
