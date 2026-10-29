#!/usr/bin/env node
// Rappix - Sonda del hub SignalR de Tracking via gateway (Node >= 22, sin dependencias: fetch + WebSocket nativos).
//
// Habla el protocolo SignalR JSON a mano: negotiate -> WebSocket -> handshake -> Subscribe(orderId)
// -> espera el snapshot StatusChanged y la completion de la invocacion -> Unsubscribe -> cierre.
// Sirve para comprobar que el hub responde y que Tracking emite los spans de
// Microsoft.AspNetCore.SignalR.Server (ver en Aspire: http://localhost:18888, servicio "tracking").
//
// Uso:  node tools/signalr-tracking-probe.mjs [orderId]
//       GATEWAY=http://localhost:5100 node tools/signalr-tracking-probe.mjs
// Sin orderId usa el pedido mas reciente de cliente@rappix.test (datos de tools/seed-smoke.sh).

const GATEWAY = process.env.GATEWAY ?? 'http://localhost:5000';
const EMAIL = process.env.EMAIL ?? 'cliente@rappix.test';
const PASSWORD = process.env.PASSWORD ?? 'Sup3rSecret!';
const TIMEOUT_MS = Number(process.env.TIMEOUT_MS ?? 15000);
const RS = '\x1e'; // separador de registros del protocolo SignalR

const ok = (msg) => console.log(`  [OK] ${msg}`);
const fail = (msg) => {
  console.error(`  [FAIL] ${msg}`);
  process.exit(1);
};

async function http(method, path, token, body) {
  const response = await fetch(`${GATEWAY}${path}`, {
    method,
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
    body: body ? JSON.stringify(body) : undefined,
  });
  const text = await response.text();
  if (!response.ok) fail(`HTTP ${response.status} ${method} ${path}: ${text}`);
  return text ? JSON.parse(text) : null;
}

const login = await http('POST', '/api/v1/auth/login', null, { identifier: EMAIL, password: PASSWORD });
const token = login.accessToken ?? login.token;
ok(`Login ${EMAIL}`);

let orderId = process.argv[2];
if (!orderId) {
  const orders = await http('GET', '/api/v1/orders?page=1&pageSize=1', token);
  const latest = (Array.isArray(orders) ? orders : orders.items)?.[0];
  if (!latest) fail('El cliente no tiene pedidos: corre tools/e2e-order-flow.sh');
  orderId = latest.orderId ?? latest.id;
}
ok(`Pedido ${orderId}`);

const negotiation = await http('POST', '/hubs/tracking/negotiate?negotiateVersion=1', token);
const connectionToken = negotiation.connectionToken ?? negotiation.connectionId;
if (!negotiation.availableTransports?.some((t) => t.transport === 'WebSockets')) {
  fail(`El hub no ofrece WebSockets: ${JSON.stringify(negotiation.availableTransports)}`);
}
ok('Negotiate (WebSockets disponible)');

const wsUrl = `${GATEWAY.replace(/^http/, 'ws')}/hubs/tracking?id=${encodeURIComponent(connectionToken)}` +
  `&access_token=${encodeURIComponent(token)}`;
const socket = new WebSocket(wsUrl);
const send = (message) => socket.send(JSON.stringify(message) + RS);

const timer = setTimeout(() => fail(`Timeout (${TIMEOUT_MS} ms) esperando respuesta del hub`), TIMEOUT_MS);
let handshakeDone = false;
let statusReceived = false;
let subscribeCompleted = false;

socket.addEventListener('open', () => socket.send(JSON.stringify({ protocol: 'json', version: 1 }) + RS));
socket.addEventListener('error', (event) => fail(`WebSocket error: ${event.message ?? event.type}`));
socket.addEventListener('close', (event) => {
  clearTimeout(timer);
  if (!statusReceived || !subscribeCompleted) fail(`WebSocket cerrado antes de tiempo (code ${event.code})`);
  console.log(`\nHUB OK: Subscribe/StatusChanged/Unsubscribe sobre WebSocket para el pedido ${orderId}`);
  process.exit(0);
});

socket.addEventListener('message', async (event) => {
  const data = typeof event.data === 'string' ? event.data : await event.data.text();
  for (const frame of data.split(RS).filter(Boolean)) {
    const message = JSON.parse(frame);

    if (!handshakeDone) {
      if (message.error) fail(`Handshake rechazado: ${message.error}`);
      handshakeDone = true;
      ok('Handshake json v1');
      send({ type: 1, invocationId: '1', target: 'Subscribe', arguments: [orderId] });
      continue;
    }

    switch (message.type) {
      case 1: // Invocation servidor -> cliente
        if (message.target === 'StatusChanged') {
          statusReceived = true;
          ok(`StatusChanged recibido: ${JSON.stringify(message.arguments[0])}`);
        } else {
          ok(`${message.target} recibido`);
        }
        break;
      case 3: // Completion
        if (message.error) fail(`Invocacion ${message.invocationId} fallo: ${message.error}`);
        if (message.invocationId === '1') {
          subscribeCompleted = true;
          ok('Subscribe completado');
          send({ type: 1, invocationId: '2', target: 'Unsubscribe', arguments: [orderId] });
        } else if (message.invocationId === '2') {
          ok('Unsubscribe completado');
          send({ type: 7 }); // Close
          socket.close(1000);
        }
        break;
      case 6: // Ping
        break;
      case 7: // Close del servidor
        if (message.error) fail(`El servidor cerro la conexion: ${message.error}`);
        break;
      default:
        ok(`Mensaje tipo ${message.type}`);
    }
  }
});
