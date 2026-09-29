import { describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { ApiError } from '@/api/http'
import type { Todo } from '@/api/types'
import TodoForm from './TodoForm.vue'

const existing: Todo = {
  id: '1',
  title: 'Existing',
  description: 'Old description',
  isCompleted: false,
  completedAt: null,
  dueDate: '2030-03-10T00:00:00Z',
  createdAt: '2030-01-01T00:00:00Z',
  updatedAt: '2030-01-01T00:00:00Z',
}

describe('TodoForm', () => {
  it('blocks submit and shows an error when the title is blank', async () => {
    const onSave = vi.fn()
    const wrapper = mount(TodoForm, { props: { submitLabel: 'Add task', onSave } })

    await wrapper.find('input').setValue('   ')
    await wrapper.find('form').trigger('submit')

    expect(onSave).not.toHaveBeenCalled()
    expect(wrapper.text()).toContain('Please enter a title.')
  })

  it('submits a trimmed payload, converting the due date to an API date', async () => {
    const onSave = vi.fn().mockResolvedValue(undefined)
    const wrapper = mount(TodoForm, { props: { submitLabel: 'Add task', onSave } })

    await wrapper.find('input[type="text"]').setValue('  Buy milk  ')
    await wrapper.find('textarea').setValue('  2 litres ')
    await wrapper.find('input[type="date"]').setValue('2030-05-01')
    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(onSave).toHaveBeenCalledWith({
      title: 'Buy milk',
      description: '2 litres',
      dueDate: '2030-05-01T00:00:00Z',
    })
  })

  it('sends null for an empty description and due date', async () => {
    const onSave = vi.fn().mockResolvedValue(undefined)
    const wrapper = mount(TodoForm, { props: { submitLabel: 'Add task', onSave } })

    await wrapper.find('input[type="text"]').setValue('Just a title')
    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(onSave).toHaveBeenCalledWith({ title: 'Just a title', description: null, dueDate: null })
  })

  it('clears the fields after a successful create', async () => {
    const wrapper = mount(TodoForm, { props: { submitLabel: 'Add task', onSave: vi.fn().mockResolvedValue(undefined) } })
    const title = wrapper.find<HTMLInputElement>('input[type="text"]')

    await title.setValue('Something')
    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(title.element.value).toBe('')
  })

  it('keeps the input and shows the server error when saving fails', async () => {
    const onSave = vi.fn().mockRejectedValue(new ApiError(400, 'Title is too long.'))
    const wrapper = mount(TodoForm, { props: { submitLabel: 'Add task', onSave } })
    const title = wrapper.find<HTMLInputElement>('input[type="text"]')

    await title.setValue('Keep me')
    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(wrapper.find('[role="alert"]').text()).toBe('Title is too long.')
    expect(title.element.value).toBe('Keep me')
  })

  it('pre-fills from an existing todo and offers cancel', async () => {
    const wrapper = mount(TodoForm, { props: { todo: existing, submitLabel: 'Save', onSave: vi.fn() } })

    expect(wrapper.find<HTMLInputElement>('input[type="text"]').element.value).toBe('Existing')
    expect(wrapper.find<HTMLTextAreaElement>('textarea').element.value).toBe('Old description')
    expect(wrapper.find<HTMLInputElement>('input[type="date"]').element.value).toBe('2030-03-10')

    await wrapper.findAll('button').find((b) => b.text() === 'Cancel')!.trigger('click')
    expect(wrapper.emitted('cancel')).toHaveLength(1)
  })

  it('disables the submit button while saving', async () => {
    let finish!: () => void
    const onSave = vi.fn(() => new Promise<void>((resolve) => (finish = resolve)))
    const wrapper = mount(TodoForm, { props: { submitLabel: 'Add task', onSave } })

    await wrapper.find('input[type="text"]').setValue('Slow')
    await wrapper.find('form').trigger('submit')

    expect(wrapper.find('button[type="submit"]').attributes('disabled')).toBeDefined()
    finish()
    await flushPromises()
    expect(wrapper.find('button[type="submit"]').attributes('disabled')).toBeUndefined()
  })

  it('does not wipe text typed for the next task while the previous save is in flight', async () => {
    let finish!: () => void
    const onSave = vi.fn(() => new Promise<void>((resolve) => (finish = resolve)))
    const wrapper = mount(TodoForm, { props: { submitLabel: 'Add task', onSave } })
    const title = wrapper.find<HTMLInputElement>('input[type="text"]')

    await title.setValue('First')
    await wrapper.find('form').trigger('submit')
    await title.setValue('Second, typed while saving')
    finish()
    await flushPromises()

    expect(title.element.value).toBe('Second, typed while saving')
  })

  it('ignores a second submit while one is already in flight', async () => {
    let finish!: () => void
    const onSave = vi.fn(() => new Promise<void>((resolve) => (finish = resolve)))
    const wrapper = mount(TodoForm, { props: { submitLabel: 'Add task', onSave } })

    await wrapper.find('input[type="text"]').setValue('Once')
    await wrapper.find('form').trigger('submit')
    await wrapper.find('form').trigger('submit')
    finish()
    await flushPromises()

    expect(onSave).toHaveBeenCalledTimes(1)
  })


  describe('compact (quick-add) mode', () => {
    const mountCompact = (onSave = vi.fn().mockResolvedValue(undefined)) =>
      mount(TodoForm, { props: { compact: true, submitLabel: 'Add task', onSave } })

    it('hides description and due date until Details is toggled', async () => {
      const wrapper = mountCompact()
      const toggle = wrapper.findAll('button').find((b) => b.text() === 'Details')!

      expect(wrapper.find('textarea').exists()).toBe(false)
      expect(toggle.attributes('aria-expanded')).toBe('false')

      await toggle.trigger('click')

      expect(wrapper.find('textarea').exists()).toBe(true)
      expect(wrapper.find('input[type="date"]').exists()).toBe(true)
      expect(toggle.attributes('aria-expanded')).toBe('true')
      expect(toggle.text()).toBe('Hide details')
    })

    it('adds a task from the title alone', async () => {
      const onSave = vi.fn().mockResolvedValue(undefined)
      const wrapper = mountCompact(onSave)

      await wrapper.find('input[type="text"]').setValue('Quick one')
      await wrapper.find('form').trigger('submit')
      await flushPromises()

      expect(onSave).toHaveBeenCalledWith({ title: 'Quick one', description: null, dueDate: null })
    })

    it('collapses the details again after a successful add', async () => {
      const wrapper = mountCompact()

      await wrapper.findAll('button').find((b) => b.text() === 'Details')!.trigger('click')
      await wrapper.find('input[type="text"]').setValue('With details')
      await wrapper.find('form').trigger('submit')
      await flushPromises()

      expect(wrapper.find('textarea').exists()).toBe(false)
    })
  })
})
