import { useCallback, useRef } from 'react'

/**
 * Returns a version of `fn` that cannot be called more than once
 * within `delayMs` milliseconds. Extra clicks during the cooldown are ignored.
 */
export function useThrottle<T extends unknown[]>(
  fn: (...args: T) => void | Promise<void>,
  delayMs = 1500
): (...args: T) => void {
  const lastCall = useRef<number>(0)
  const running  = useRef(false)

  return useCallback(
    (...args: T) => {
      const now = Date.now()
      if (running.current || now - lastCall.current < delayMs) return
      lastCall.current = now
      running.current  = true
      const result = fn(...args)
      if (result instanceof Promise) {
        result.finally(() => { running.current = false })
      } else {
        running.current = false
      }
    },
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [fn, delayMs]
  )
}
