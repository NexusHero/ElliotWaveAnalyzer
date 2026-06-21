import type { ComponentProps } from 'react'
import { render, screen, fireEvent } from '@testing-library/react'
import { describe, it, expect, vi } from 'vitest'
import WaveAnnotationPanel from './WaveAnnotationPanel'
import type { WaveAnalysisResponse, WaveAnnotation } from '../api/types'

const twoAnnotations: WaveAnnotation[] = [
  { date: '2024-01-05T00:00:00Z', price: 38_000, label: '1' },
  { date: '2024-01-15T00:00:00Z', price: 35_000, label: '2' },
]

const noopHandlers = {
  onAddLabel: vi.fn(),
  onRelabel: vi.fn(),
  onRemove: vi.fn(),
  onSubmit: vi.fn(),
}

function renderPanel(overrides: Partial<ComponentProps<typeof WaveAnnotationPanel>> = {}) {
  const props = {
    annotations: [],
    pending: null,
    result: null,
    error: null,
    loading: false,
    ...noopHandlers,
    ...overrides,
  }
  return render(<WaveAnnotationPanel {...props} />)
}

describe('WaveAnnotationPanel', () => {
  it('shows the label picker only when a point is pending and reports the chosen label', () => {
    const onAddLabel = vi.fn()
    renderPanel({ pending: { time: '2024-02-01', price: 52_000 }, onAddLabel })

    expect(screen.getByTestId('label-picker')).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: '3' }))
    expect(onAddLabel).toHaveBeenCalledWith('3')
  })

  it('disables submit with fewer than two annotations', () => {
    renderPanel({ annotations: [twoAnnotations[0]!] })
    expect(screen.getByRole('button', { name: /validate wave count/i })).toBeDisabled()
  })

  it('submits when at least two annotations exist', () => {
    const onSubmit = vi.fn()
    renderPanel({ annotations: twoAnnotations, onSubmit })

    const button = screen.getByRole('button', { name: /validate wave count/i })
    expect(button).toBeEnabled()
    fireEvent.click(button)
    expect(onSubmit).toHaveBeenCalledOnce()
  })

  it('relabels and removes annotations', () => {
    const onRelabel = vi.fn()
    const onRemove = vi.fn()
    renderPanel({ annotations: twoAnnotations, onRelabel, onRemove })

    fireEvent.change(screen.getByLabelText('Label for annotation 1'), { target: { value: 'B' } })
    expect(onRelabel).toHaveBeenCalledWith(0, 'B')

    fireEvent.click(screen.getByLabelText('Remove annotation 2'))
    expect(onRemove).toHaveBeenCalledWith(1)
  })

  it('renders the validation result', () => {
    const result: WaveAnalysisResponse = {
      result: {
        isValid: false,
        violations: ['Wave 3 is the shortest'],
        warnings: [],
        analysis: 'Rule 2 breached.',
        confidence: 'high',
      },
      ruleReport: {
        bullishAssumed: true,
        rules: [{ name: 'Rule 2 — Wave 3 is not the shortest impulse wave', status: 'Fail', detail: '' }],
        ratios: [{ name: 'Wave 2 retracement of Wave 1', ratio: 0.5 }],
      },
      usage: { provider: 'Gemini', promptTokens: 100, completionTokens: 50, totalTokens: 150 },
    }
    renderPanel({ annotations: twoAnnotations, result })

    expect(screen.getByTestId('validation-result')).toBeInTheDocument()
    expect(screen.getByText(/invalid wave count/i)).toBeInTheDocument()
    expect(screen.getByText('Wave 3 is the shortest')).toBeInTheDocument()
    expect(screen.getByText(/150 tokens/)).toBeInTheDocument()
  })

  it('shows an error message', () => {
    renderPanel({ error: 'LLM API error' })
    expect(screen.getByRole('alert')).toHaveTextContent('LLM API error')
  })
})
