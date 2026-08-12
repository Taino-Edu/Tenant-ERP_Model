export function resolveResetLoginPath(from: string | null, invite: string | null): '/login' | '/entrar' {
  return from === 'admin' || invite === 'platform' ? '/login' : '/entrar'
}
