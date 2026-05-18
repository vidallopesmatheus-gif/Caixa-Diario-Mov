import React from 'react'
import ReactDOM from 'react-dom/client'
import './styles/theme.css'
import './styles/global.css'
import App from './App'
import ErrorBoundary from './components/ErrorBoundary'

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <ErrorBoundary>
      <App />
    </ErrorBoundary>
  </React.StrictMode>
)
