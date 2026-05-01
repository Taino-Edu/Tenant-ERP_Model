// =============================================================================
// lib/auth.ts — Utilitários de autenticação
// =============================================================================
import Cookies from 'js-cookie'
import { AuthResponse } from './api'

export function saveAuth(auth: AuthResponse) {
  Cookies.set('accessToken',  auth.accessToken,  { expires: 1/24, sameSite: 'strict' })
  Cookies.set('refreshToken', auth.refreshToken, { expires: 30,   sameSite: 'strict' })
  Cookies.set('userRole',     auth.role,         { expires: 30 })
  Cookies.set('userName',     auth.userName,     { expires: 30 })
  Cookies.set('userId',       auth.userId,       { expires: 30 })
}

export function clearAuth() {
  ['accessToken', 'refreshToken', 'userRole', 'userName', 'userId'].forEach(k => Cookies.remove(k))
}

export function getRole():    string { return Cookies.get('userRole') || '' }
export function getUserName(): string { return Cookies.get('userName') || '' }
export function getUserId():  string { return Cookies.get('userId')   || '' }
export function isAdmin():    boolean { return getRole() === 'Admin' }
export function isLoggedIn(): boolean { return !!Cookies.get('accessToken') }
