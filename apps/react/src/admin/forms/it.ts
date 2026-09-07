import type { InternationalizedText } from './client/types'

export function formatIt(text: InternationalizedText | null, fallback = '—'): string {
  if (!text) return fallback
  return text.values[text.defaultLocale] ?? Object.values(text.values)[0] ?? fallback
}

export function formatUtc(iso: string): string {
  const date = new Date(iso)
  const year = date.getUTCFullYear()
  const month = String(date.getUTCMonth() + 1).padStart(2, '0')
  const day = String(date.getUTCDate()).padStart(2, '0')
  const hour = String(date.getUTCHours()).padStart(2, '0')
  const minute = String(date.getUTCMinutes()).padStart(2, '0')
  return `${year}-${month}-${day} ${hour}:${minute} UTC`
}
