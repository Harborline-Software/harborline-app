import { render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'

// Ticket 153 review: an unconfigured production bundle used to throw during bundle
// evaluation — before React mounted — leaving a white page with a console-only error.
// The failure must stay loud but land ON SCREEN.
describe('App configuration-error surface', () => {
  afterEach(() => {
    vi.unstubAllEnvs()
    vi.resetModules()
  })

  it('renders a visible configuration-error screen instead of crashing before mount', async () => {
    vi.stubEnv('VITE_FORMS_API_ORIGIN', '')
    vi.stubEnv('VITE_FORMS_FIXTURE', '')
    vi.resetModules()

    // Importing App evaluates the module-scope client creation with the stubbed (empty) env —
    // the exact moment the old code threw pre-mount. It must import AND render.
    const { App } = await import('../App')
    render(<App />)

    const alert = screen.getByRole('alert')
    expect(alert).toHaveTextContent('Forms admin is not configured')
    expect(alert).toHaveTextContent(/VITE_FORMS_API_ORIGIN/)
  })
})
