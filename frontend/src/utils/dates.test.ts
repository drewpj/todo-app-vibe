import { describe, expect, it } from 'vitest'
import { formatDueDate, isOverdue, toApiDate, toDateInput, todayInput } from './dates'

describe('dates', () => {
  it('converts a date input to a UTC-midnight ISO string and back', () => {
    expect(toApiDate('2030-01-15')).toBe('2030-01-15T00:00:00Z')
    expect(toDateInput('2030-01-15T00:00:00Z')).toBe('2030-01-15')
  })

  it('treats empty input as no date', () => {
    expect(toApiDate('')).toBeNull()
    expect(toDateInput(null)).toBe('')
  })

  it('formats the calendar day in UTC regardless of local timezone', () => {
    expect(formatDueDate('2030-01-15T00:00:00Z')).toContain('15')
  })

  it('formats today as a local YYYY-MM-DD string', () => {
    expect(todayInput(new Date(2030, 0, 5, 23, 59))).toBe('2030-01-05')
  })

  describe('isOverdue', () => {
    const now = new Date(2030, 5, 15, 12, 0)

    it('is overdue when the due day is before today and the task is open', () => {
      expect(isOverdue('2030-06-14T00:00:00Z', false, now)).toBe(true)
    })

    it('is not overdue on the due day itself or later', () => {
      expect(isOverdue('2030-06-15T00:00:00Z', false, now)).toBe(false)
      expect(isOverdue('2030-06-16T00:00:00Z', false, now)).toBe(false)
    })

    it('is never overdue when completed or without a due date', () => {
      expect(isOverdue('2030-01-01T00:00:00Z', true, now)).toBe(false)
      expect(isOverdue(null, false, now)).toBe(false)
    })
  })
})
