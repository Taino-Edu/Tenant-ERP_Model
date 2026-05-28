// =============================================================================
// lib/signalr.ts — Cliente SignalR para atualizações em tempo real
//
// Transportes (ordem de preferência):
//   1. WebSockets  — mais rápido, full-duplex, baixa latência
//   2. SSE         — fallback se WS bloqueado por proxy/firewall
//   3. LongPolling — último recurso
// =============================================================================
import * as signalR from '@microsoft/signalr'

const BASE_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000'

let connection: signalR.HubConnection | null = null

export function getComandaHub(): signalR.HubConnection {
  if (!connection) {
    connection = new signalR.HubConnectionBuilder()
      .withUrl(`${BASE_URL}/hubs/comanda`, {
        // Token via cookie HttpOnly — browser envia automaticamente
        withCredentials: true,
        // WebSocket primeiro (mais rápido), depois SSE, depois LongPolling
        transport: signalR.HttpTransportType.WebSockets |
                   signalR.HttpTransportType.ServerSentEvents |
                   signalR.HttpTransportType.LongPolling,
      })
      // Reconecta imediatamente, depois 1s, 2s, 5s, 10s, 10s, 10s...
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: (ctx) => {
          if (ctx.previousRetryCount === 0) return 0
          if (ctx.previousRetryCount === 1) return 1000
          if (ctx.previousRetryCount === 2) return 2000
          if (ctx.previousRetryCount <= 5)  return 5000
          return 10000
        }
      })
      .configureLogging(signalR.LogLevel.Warning)
      .build()
  }
  return connection
}

export async function startHub(): Promise<signalR.HubConnection> {
  const hub = getComandaHub()
  if (hub.state === signalR.HubConnectionState.Disconnected) {
    await hub.start()
  }
  return hub
}

export async function stopHub() {
  if (connection && connection.state !== signalR.HubConnectionState.Disconnected) {
    await connection.stop()
    connection = null
  }
}

// Tipos dos eventos SignalR
export interface ComandaUpdatedEvent {
  comandaId: string; userId: string; userName: string
  tableIdentifier: string | null; totalInReais: number
  status: string; lastItemAdded: string | null; updatedAt: string
}

export interface ComandaOpenedEvent {
  comandaId: string
  tableIdentifier: string | null
}

export interface ComandaClosedEvent {
  comandaId: string
  paymentMethod: string
}

export interface ComandaCancelledEvent {
  comandaId: string
}
