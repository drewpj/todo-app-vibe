/**
 * Due dates are calendar dates, not instants. The API stores them as UTC midnight, and we always read/format
 * them in UTC so a task due "2030-01-15" shows as Jan 15 in every timezone.
 */

/** "2030-01-15" (from <input type="date">) -> "2030-01-15T00:00:00Z". */
export function toApiDate(dateInput: string): string | null {
  return dateInput ? `${dateInput}T00:00:00Z` : null
}

/** "2030-01-15T00:00:00Z" -> "2030-01-15" (for <input type="date">). */
export function toDateInput(iso: string | null): string {
  return iso ? iso.slice(0, 10) : ''
}

export function formatDueDate(iso: string): string {
  return new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeZone: 'UTC' }).format(new Date(iso))
}

/** Local calendar day as "YYYY-MM-DD". */
export function todayInput(now: Date = new Date()): string {
  const month = String(now.getMonth() + 1).padStart(2, '0')
  const day = String(now.getDate()).padStart(2, '0')
  return `${now.getFullYear()}-${month}-${day}`
}

export function isOverdue(dueIso: string | null, isCompleted: boolean, now: Date = new Date()): boolean {
  return !isCompleted && dueIso !== null && toDateInput(dueIso) < todayInput(now)
}
