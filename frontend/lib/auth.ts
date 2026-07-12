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
  Cookies.set('userRole',  auth.role,     { expires: 30 })
  Cookies.set('userName',  auth.userName, { expires: 30 })
  Cookies.set('userId',    auth.userId,   { expires: 30 })
  if (auth.permissions)
    Cookies.set('userPermissions', JSON.stringify(auth.permissions), { expires: 30 })
  else
    Cookies.remove('userPermissions')
  // Login normal nunca seta esse cookie (só o redeem de impersonação, no
  // backend) — remove qualquer resíduo de uma sessão de impersonação
  // anterior no mesmo navegador, pra nunca mostrar o banner errado.
  Cookies.remove('impersonating')
}

export function clearAuth() {
  ;['userRole', 'userName', 'userId', 'userPermissions', 'impersonating'].forEach(k => Cookies.remove(k))
}

export function getRole():        string    { return Cookies.get('userRole') || '' }
export function getUserName():    string    { return Cookies.get('userName') || '' }
export function getUserId():      string    { return Cookies.get('userId')   || '' }
export function isAdmin():        boolean   { return getRole() === 'Admin' }
export function isOperator():     boolean   { return getRole() === 'Operator' }
export function isPlatformOwner(): boolean  { return getRole() === 'PlatformOwner' }
export function isContador():      boolean  { return getRole() === 'Contador' }
export function isLoggedIn():     boolean   { return !!Cookies.get('userRole') }
export function getImpersonatingOwnerName(): string | null { return Cookies.get('impersonating') || null }

export function getPermissions(): string[] {
  try {
    const raw = Cookies.get('userPermissions')
    return raw ? JSON.parse(raw) : []
  } catch { return [] }
}

export function hasPermission(perm: string): boolean {
  if (isAdmin()) return true
  return getPermissions().includes(perm)
}
