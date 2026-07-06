self.addEventListener('push', event => {
  const data = event.data?.json() ?? {}
  event.waitUntil(
    self.registration.showNotification(data.title ?? 'Santuário Nerd', {
      body:  data.body  ?? '',
      image: data.image ?? undefined,
      icon:  '/logo-maikon.png',
      badge: '/logo-maikon.png',
      tag:   'santuario-nerd-notif',
      data:  { url: data.link ?? '/cliente' },
    })
  )
})

self.addEventListener('notificationclick', event => {
  event.notification.close()
  const url = event.notification.data?.url ?? '/cliente'
  event.waitUntil(
    clients.matchAll({ type: 'window', includeUncontrolled: true }).then(cs => {
      const existing = cs.find(c => c.url.includes(url) && 'focus' in c)
      if (existing) return existing.focus()
      return clients.openWindow(url)
    })
  )
})
