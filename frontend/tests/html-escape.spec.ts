import { expect, test } from '@playwright/test'
import { escapeHtml } from '../lib/html'

test.describe('escapeHtml', () => {
  test('escapa caracteres que podem alterar o template do comprovante', () => {
    expect(escapeHtml(`<script>alert('x') & "y"</script>`))
      .toBe('&lt;script&gt;alert(&#39;x&#39;) &amp; &quot;y&quot;&lt;/script&gt;')
  })

  test('preserva texto comum, acentos e valores formatados', () => {
    expect(escapeHtml('João — R$ 10,00')).toBe('João — R$ 10,00')
  })
})
