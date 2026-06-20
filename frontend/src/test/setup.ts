import '@testing-library/jest-dom'

// Lightweight Charts calls ResizeObserver internally.
// jsdom doesn't implement it, so we stub it here.
global.ResizeObserver = class ResizeObserver {
  observe() {}
  unobserve() {}
  disconnect() {}
}
