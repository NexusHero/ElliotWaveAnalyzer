import type { Theme } from '../hooks/useTheme'
import styles from './ThemeToggle.module.css'

interface ThemeToggleProps {
  theme: Theme
  onToggle: () => void
}

/** Dark/light theme switch. */
export default function ThemeToggle({ theme, onToggle }: ThemeToggleProps) {
  const next = theme === 'dark' ? 'light' : 'dark'
  return (
    <button
      type="button"
      className={styles.toggle}
      onClick={onToggle}
      aria-label={`Switch to ${next} mode`}
      title={`Switch to ${next} mode`}
    >
      {theme === 'dark' ? '☀ Light' : '☾ Dark'}
    </button>
  )
}
