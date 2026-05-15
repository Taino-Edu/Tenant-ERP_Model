// =============================================================================
// lib/auth.ts — Utilitários de autenticação
//
// Os tokens JWT (accessToken, refreshToken) são gerenciados exclusivamente
// pelo backend como cookies HttpOnly — não acessíveis via JavaScript.
// Apenas metadados não sensíveis (role, nome, userId) são salvos em cookies
// comuns para uso na UI (sem impacto na segurança se expostos via JS).
// =============================================================================
import Cookies from 'js-cookie'
import { AuthResponse } from './api'

export function saveAuth(auth: AuthResponse) {
  // Não salvar accessToken nem refreshToken — vêm como HttpOnly cookies do backend.
  // Salvar apenas metadados da sessão para uso na interface.
  Cookies.set('userRole',  auth.role,     { expires: 30 })
  Cookies.set('userName',  auth.userName, { expires: 30 })
  Cookies.set('userId',    auth.userId,   { expires: 30 })
}

export function clearAuth() {
  // Remove apenas os metadados da UI — o backend limpa os HttpOnly cookies no logout.
  ;['userRole', 'userName', 'userId'].forEach(k => Cookies.remove(k))
}

export function getRole():     string  { return Cookies.get('userRole') || '' }
export function getUserName(): string  { return Cookies.get('userName') || '' }
export function getUserId():   string  { return Cookies.get('userId')   || '' }
export function isAdmin():     boolean { return getRole() === 'Admin' }

// isLoggedIn verifica se há metadados de sessão.
// A validade real do token é verificada pelo backend a cada requisição.
export function isLoggedIn():  boolean { return !!Cookies.get('userRole') }
