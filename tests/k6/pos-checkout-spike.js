import http from 'k6/http';
import { sleep } from 'k6';
import exec from 'k6/execution';
import { Counter } from 'k6/metrics';
import { api, capacity, idCarril, login, params, registrarRespuesta, skuPos, smoke, stage, tienda, umbralesRespuesta, ventaPayload, vender, vus } from './local.js';

const erroresConciliacion = new Counter('sobreventas_o_descuadres');
const ventas = new Counter('ventas_confirmadas');
export const options = {
  scenarios: { caja: { executor: 'ramping-vus', startVUs: 0,
    stages: [0.05, 0.25, 0.5, 1, 1, 0].map(n => ({ duration: stage, target: Math.ceil(n * vus) })) } },
  thresholds: {
    ...umbralesRespuesta('venta_pos', capacity),
    ...(capacity ? { 'http_req_duration{name:venta_pos}': ['p(95)<500'] } : umbralesRespuesta('carrera_stock')),
    ventas_confirmadas: ['count>0'], sobreventas_o_descuadres: ['count==0'],
  },
};

function saldosCarriles(token) {
  const respuesta = http.get(api + '/inventario/stock?almacenId=' + tienda, params(token, 'stock_carriles'));
  if (respuesta.status !== 200) throw new Error('No se pudo consultar el inventario de los carriles POS.');
  const porProducto = new Map(respuesta.json().map(saldo => [saldo.productoId, saldo]));
  return Array.from({ length: vus }, (_, i) => {
    const saldo = porProducto.get(idCarril('pos', i + 1));
    if (!saldo) throw new Error('Cargue fixtures-capacity.sql antes de ejecutar la prueba de capacidad.');
    return { fisico: saldo.stockFisico, comprometido: saldo.stockComprometido };
  });
}

function snapshot(token, producto = skuPos) {
  const stock = http.get(api + '/inventario/stock?productoId=' + producto + '&almacenId=' + tienda, params(token, 'stock'));
  const kardex = http.get(api + '/inventario/kardex/' + producto + '?almacenId=' + tienda, params(token, 'kardex'));
  if (stock.status !== 200 || kardex.status !== 200 || stock.json().length !== 1)
    throw new Error('Stock o kardex de la fixture inaccesible.');
  const saldo = stock.json()[0];
  const vendidas = kardex.json().filter(m => m.tipoOperacion === 'VENTA_POS').reduce((s, m) => s + m.cantidadBase, 0);
  return { fisico: saldo.stockFisico, comprometido: saldo.stockComprometido, vendidas };
}

export function setup() {
  const { token } = login(true);
  if (capacity) {
    const inicial = saldosCarriles(token);
    if (inicial.some(s => s.fisico <= s.comprometido)) throw new Error('Los carriles requieren stock disponible.');
    return { token, inicial };
  }
  const limitado = 'a1520100-0000-0000-0000-000000000001';
  const antes = snapshot(token, limitado);
  const disponible = antes.fisico - antes.comprometido;
  if (disponible < 1) throw new Error('Use una fixture nueva para la carrera de stock limitado.');
  const cantidad = Math.floor(disponible / 2) + 1;
  const peticiones = Array.from({ length: 5 }, () => ({
    method: 'POST', url: api + '/pos/ventas', body: ventaPayload(limitado, cantidad),
    params: { ...params(token, 'carrera_stock'), responseCallback: http.expectedStatuses(201, 409) },
  }));
  const respuestas = http.batch(peticiones);
  respuestas.forEach(r => registrarRespuesta(r, 'carrera_stock', 201, true));
  const despues = snapshot(token, limitado);
  const carreraCorrecta = respuestas.filter(r => r.status === 201).length === 1 &&
    respuestas.filter(r => r.status === 409).length === 4 && despues.fisico === antes.fisico - cantidad &&
    despues.comprometido === antes.comprometido && despues.vendidas === antes.vendidas + cantidad;
  erroresConciliacion.add(carreraCorrecta ? 0 : 1);
  if (!carreraCorrecta) throw new Error('La carrera concurrente no respetó el saldo disponible.');
  const inicial = snapshot(token);
  if (inicial.fisico <= inicial.comprometido) throw new Error('La fixture requiere unidades disponibles.');
  return { token, inicial };
}

export default function ({ token }) {
  const carril = exec.vu.idInTest;
  const r = capacity ? vender(token, idCarril('pos', carril), idCarril('turno', carril)) : vender(token);
  registrarRespuesta(r, 'venta_pos', 201, true);
  ventas.add(r.status === 201 ? 1 : 0);
  sleep(smoke ? 0.3 : 0.1);
}

export function teardown({ token, inicial }) {
  // La lectura contable es independiente del código HTTP (incluye ventas cuyo cliente agotó el timeout).
  try {
    if (capacity) {
      const final = saldosCarriles(token);
      const correcto = final.every((s, i) => s.comprometido === inicial[i].comprometido &&
        s.fisico >= s.comprometido && s.fisico <= inicial[i].fisico);
      erroresConciliacion.add(correcto ? 0 : 1);
      console.log(JSON.stringify({ carriles: final.length, unidadesDescontadas: final.reduce((s, saldo, i) => s + inicial[i].fisico - saldo.fisico, 0) }));
      return;
    }
    const final = snapshot(token);
    const vendidas = final.vendidas - inicial.vendidas;
    const correcto = vendidas > 0 && vendidas <= inicial.fisico - inicial.comprometido &&
      final.fisico === inicial.fisico - vendidas && final.comprometido === inicial.comprometido;
    erroresConciliacion.add(correcto ? 0 : 1);
    console.log(JSON.stringify({ inicial, final, vendidas }));
  } catch (error) {
    erroresConciliacion.add(1);
    throw error;
  }
}
