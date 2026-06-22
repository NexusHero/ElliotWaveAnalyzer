import type { Theme } from '../hooks/useTheme'
import { Sun, Moon } from './Icons'

interface ThemeToggleProps {
  theme: Theme
  onToggle: () => void
}

/** Dark/light theme switch, styled as a topbar button. */
export default function ThemeToggle({ theme, onToggle }: ThemeToggleProps) {
  const next = theme === 'dark' ? 'light' : 'dark'
  return (
    <button
      type="button"
      className="tb-btn"
      onClick={onToggle}
      aria-label={`Switch to ${next} mode`}
      title={`Switch to ${next} mode`}
    >
      {theme === 'dark' ? <Sun size={16} /> : <Moon size={16} />}
      <span>{theme === 'dark' ? 'Light' : 'Dark'}</span>
    </button>
  )
}
