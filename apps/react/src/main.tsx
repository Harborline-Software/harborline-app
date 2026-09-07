import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'

// The platform stylesheet is a package export ("./style.css"). Importing it here - rather than
// copying rules into this app - is what keeps the two lanes visually identical.
import '@harborline-software/ui-react/style.css'

import { App } from './App'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
)
