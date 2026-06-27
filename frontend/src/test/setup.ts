import '@testing-library/jest-dom'

// Lightweight Charts calls ResizeObserver internally.
// jsdom doesn't implement it, so we stub it here.
window.ResizeObserver = class ResizeObserver {
  observe() {}
  unobserve() {}
  disconnect() {}
}

// jsdom in this setup doesn't provide Web Storage; stub a minimal in-memory localStorage.
if (!('localStorage' in window) || window.localStorage == null) {
  const store = new Map<string, string>()
  const localStorageMock: Storage = {
    getItem: (key) => store.get(key) ?? null,
    setItem: (key, value) => void store.set(key, String(value)),
    removeItem: (key) => void store.delete(key),
    clear: () => store.clear(),
    key: (index) => Array.from(store.keys())[index] ?? null,
    get length() {
      return store.size
    },
  }
  Object.defineProperty(window, 'localStorage', { value: localStorageMock, configurable: true })
}
