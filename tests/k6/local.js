import http from 'k6/http';

const origin = __ENV.BASE_URL || 'http://127.0.0.1:5000';
if (!/^https?:\/\/(localhost|127\.0\.0\.1|\[::1\])(:\d+)?$/.test(origin)) {
  throw new Error('BASE_URL debe ser el origen de la API local de ensayo.');
}
export const api = origin + '/api/v1';
export const tienda = '22222222-2222-2222-2222-222222222222';
export const skuPos = 'd8279f63-3aa9-4746-bf94-638f4383fcd2';
export const skuWeb = 'e5a40b8e-5b12-4f2b-8a71-6c2e3f890123';
export const smoke = __ENV.SMOKE === 'true';
export const vus = smoke ? 2 : 2000;
export const stage = smoke ? '2s' : '20s';

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

export function ventaPayload(productoId = skuPos, cantidad = 1) {
  return JSON.stringify({
    turnoCajaId: '44444444-4444-4444-4444-444444444444', clienteId: 'c1010101-0000-0000-0000-000000000001',
    items: [{ productoId, cantidad, unidadMedida: 'UND', precioUnitario: 15 }],
    pagos: [{ metodoPago: 'Efectivo', monto: cantidad * 15 }], tipoComprobante: 'Boleta',
  });
}

export function vender(token) {
  return http.post(api + '/pos/ventas', ventaPayload(), params(token, 'venta_pos'));
}
