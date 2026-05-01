// =============================================================================
// lib/signalr.ts — Cliente SignalR para atualizações em tempo real
// =============================================================================
import * as signalR from '@microsoft/signalr'
import Cookies from 'js-cookie'

const BASE_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000'

let connection: signalR.HubConnection | null = null

export function getComandaHub(): signalR.HubConnection {
  if (!connection) {
    connection = new signalR.HubConnectionBuilder()
      .withUrl(`${BASE_URL}/hubs/comanda`, {
        accessTokenFactory: () => Cookies.get('accessToken') || '',
        transport: signalR.HttpTransportType.WebSockets |
                   signalR.HttpTransportType.ServerSentEvents |
                   signalR.HttpTransportType.LongPolling,
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
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
