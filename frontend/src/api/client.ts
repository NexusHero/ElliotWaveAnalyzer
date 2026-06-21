import type { LlmValidation, WaveValidationRequest } from './types'

/**
 * Thin API client. Uses same-origin relative paths: in dev the Vite proxy forwards
 * `/api/*` to the backend; in production the backend serves the built frontend, so
 * the same paths work without CORS.
 */

/** Validates a wave annotation set via `POST /api/wave-analysis`. */
export async function validateWaveCount(
  request: WaveValidationRequest,
  signal?: AbortSignal,
): Promise<LlmValidation> {
  const response = await fetch('/api/wave-analysis', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
    signal,
  })

  if (!response.ok) {
    throw new Error(await extractErrorDetail(response))
  }

  return (await response.json()) as LlmValidation
}

/** Pulls a human-readable message out of an RFC7807 problem response, with a fallback. */
async function extractErrorDetail(response: Response): Promise<string> {
  try {
    const problem = (await response.json()) as { detail?: string; title?: string }
    return problem.detail ?? problem.title ?? `Request failed (${response.status})`
  } catch {
    return `Request failed (${response.status})`
  }
}
