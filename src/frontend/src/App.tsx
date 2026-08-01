import { useState } from 'react'
import './App.css'

type CreateConversionRequest = {
  fromCurrency: string
  toCurrency: string
  amount: number
}

type ConversionResult = {
  conversionId: string
  fromCurrency: string
  toCurrency: string
  amount: number
  rate: number
  convertedAmount: number
  providerDateMarker: string
  executedAtUtc: string
}

const API_BASE_URL = (window as any).__VITE_API_URL__ ?? ''

function apiUrl(path: string) {
  const base = typeof API_BASE_URL === 'string' ? API_BASE_URL.trim() : ''
  if (!base) return path
  return `${base.replace(/\/$/, '')}${path}`
}

function isValidCurrencyCode(value: string) {
  return /^[A-Za-z]{3}$/.test(value)
}

async function fetchProblemDetails(res: Response): Promise<string> {
  try {
    const body = (await res.json()) as { title?: string; detail?: string }
    if (body?.title) return body.title
    if (body?.detail) return body.detail
    return `Request failed with status ${res.status}`
  } catch {
    return `Request failed with status ${res.status}`
  }
}

function App() {
  const [fromCurrency, setFromCurrency] = useState('USD')
  const [toCurrency, setToCurrency] = useState('EUR')
  const [amount, setAmount] = useState('')

  const [conversion, setConversion] = useState<ConversionResult | null>(null)
  const [conversionError, setConversionError] = useState<string | null>(null)

  const [lookupId, setLookupId] = useState('')
  const [lookup, setLookup] = useState<ConversionResult | null>(null)
  const [lookupError, setLookupError] = useState<string | null>(null)

  async function onConvert() {
    setConversionError(null)
    setConversion(null)

    const parsedAmount = Number(amount)
    const from = fromCurrency.trim().toUpperCase()
    const to = toCurrency.trim().toUpperCase()

    if (!Number.isFinite(parsedAmount) || parsedAmount <= 0) {
      setConversionError('Amount must be a positive number.')
      return
    }
    if (!isValidCurrencyCode(from) || !isValidCurrencyCode(to)) {
      setConversionError('Currency codes must be three letters (e.g., USD, EUR).')
      return
    }
    if (from === to) {
      setConversionError('From and to currencies must be different.')
      return
    }

    const payload: CreateConversionRequest = {
      fromCurrency: from,
      toCurrency: to,
      amount: parsedAmount,
    }

    const res = await fetch(apiUrl('/api/conversions'), {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    })

    if (!res.ok) {
      setConversionError(await fetchProblemDetails(res))
      return
    }

    const data = (await res.json()) as ConversionResult
    setConversion(data)
    setLookupId(data.conversionId)
  }

  async function onLookup() {
    setLookupError(null)
    setLookup(null)

    const id = lookupId.trim()
    if (!id) {
      setLookupError('Conversion ID is required.')
      return
    }

    const res = await fetch(apiUrl(`/api/conversions/${encodeURIComponent(id)}`), {
      method: 'GET',
    })

    if (!res.ok) {
      setLookupError(await fetchProblemDetails(res))
      return
    }

    const data = (await res.json()) as ConversionResult
    setLookup(data)
  }

  return (
    <div className="page">
      <header className="header">
        <h1>Real-Time Currency Conversion</h1>
        <p className="sub">
          Submit a conversion and immediately get an audit-reconstructable record.
        </p>
      </header>

      <main className="grid">
        <section className="card">
          <h2>Live Conversion</h2>

          <div className="form">
            <label>
              Amount
              <input
                value={amount}
                onChange={(e) => setAmount(e.target.value)}
                inputMode="decimal"
                placeholder="e.g., 100.00"
              />
            </label>
            <div className="row">
              <label>
                From
                <input value={fromCurrency} onChange={(e) => setFromCurrency(e.target.value)} />
              </label>
              <label>
                To
                <input value={toCurrency} onChange={(e) => setToCurrency(e.target.value)} />
              </label>
            </div>

            {conversionError ? <div className="error">{conversionError}</div> : null}
            <button type="button" className="primary" onClick={onConvert}>
              Convert
            </button>
          </div>
        </section>

        <section className="card">
          <h2>Audit Lookup</h2>

          <div className="form">
            <label>
              Conversion ID
              <input value={lookupId} onChange={(e) => setLookupId(e.target.value)} />
            </label>
            {lookupError ? <div className="error">{lookupError}</div> : null}
            <button type="button" className="primary" onClick={onLookup}>
              Retrieve
            </button>
          </div>
        </section>

        <section className="card span2">
          <h2>Result</h2>
          {conversion ? (
            <div className="result">
              <div className="kv">
                <div>
                  <div className="k">Converted Amount</div>
                  <div className="v">{conversion.convertedAmount.toFixed(2)}</div>
                </div>
                <div>
                  <div className="k">Rate</div>
                  <div className="v">{conversion.rate}</div>
                </div>
                <div>
                  <div className="k">Provider Date Marker</div>
                  <div className="v">{conversion.providerDateMarker}</div>
                </div>
                <div>
                  <div className="k">Executed At (UTC)</div>
                  <div className="v">{conversion.executedAtUtc}</div>
                </div>
              </div>
              <div className="mono">
                Conversion ID: <span>{conversion.conversionId}</span>
              </div>
            </div>
          ) : lookup ? (
            <div className="result">
              <div className="kv">
                <div>
                  <div className="k">Converted Amount</div>
                  <div className="v">{lookup.convertedAmount.toFixed(2)}</div>
                </div>
                <div>
                  <div className="k">Rate</div>
                  <div className="v">{lookup.rate}</div>
                </div>
                <div>
                  <div className="k">Provider Date Marker</div>
                  <div className="v">{lookup.providerDateMarker}</div>
                </div>
                <div>
                  <div className="k">Executed At (UTC)</div>
                  <div className="v">{lookup.executedAtUtc}</div>
                </div>
              </div>
              <div className="mono">
                Conversion ID: <span>{lookup.conversionId}</span>
              </div>
            </div>
          ) : (
            <div className="hint">Submit a conversion or retrieve an existing record.</div>
          )}
        </section>
      </main>
    </div>
  )
}

export default App
