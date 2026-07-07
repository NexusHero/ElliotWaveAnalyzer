import type { components } from './generated'
import type { RiskAssessment, ScanResult, WaveLevels, WaveVerification } from './types'

/**
 * Compile-time-only contract drift check (#197 AC2). `types.ts` is hand-maintained; `generated.ts`
 * is regenerated from the backend's real OpenAPI document (`npm run generate:api`, deterministic —
 * see the build-time `OpenApiGenerateDocumentsOnBuild` doc generation, no live server needed). This
 * file has no runtime behaviour — it exists purely so `tsc --noEmit` (already a blocking CI step)
 * fails the moment a hand-maintained shape's field set diverges from the real backend contract for
 * one of the representative response DTOs the issue names.
 *
 * Deliberately compares `keyof`, not full structural equality: several backend `decimal` fields
 * serialize as plain JSON numbers but the generated schema types them `number | string` (a known
 * `Microsoft.AspNetCore.OpenApi` decimal-schema quirk, not a real wire-format difference) — a full
 * structural-equality check would fail on that noise instead of on an actual field being added,
 * renamed, or removed, which is the drift this check exists to catch.
 *
 * AC4 (fixing drift): run `npm run generate:api` (regenerates this file's sibling `generated.ts`
 * from the backend's build-time OpenAPI document), reconcile any DTO in `types.ts` that the
 * resulting `tsc` error points at, then commit both files together.
 */
type KeysEqual<A, B> = keyof A extends keyof B ? (keyof B extends keyof A ? true : false) : false

type Check<Ok extends true> = Ok

type _WaveVerification = Check<
  KeysEqual<WaveVerification, components['schemas']['WaveVerification']>
>
type _RiskAssessment = Check<KeysEqual<RiskAssessment, components['schemas']['RiskAssessment']>>
type _ScanResult = Check<KeysEqual<ScanResult, components['schemas']['ScanResult']>>
type _WaveLevels = Check<KeysEqual<WaveLevels, components['schemas']['WaveLevels']>>

// Referencing the checks as a value (never actually executed) forces tsc to evaluate every type
// above — an unused `type` alone can be elided without error under some configurations.
export const _contractChecksHoldAtCompileTime: [
  _WaveVerification,
  _RiskAssessment,
  _ScanResult,
  _WaveLevels,
] = [true, true, true, true]
