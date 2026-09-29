import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { ApiError } from '@/api/http'
import { todosApi } from '@/api/todos'
import type { Todo } from '@/api/types'
import TodoItem from './TodoItem.vue'

vi.mock('@/api/todos')

function makeTodo(overrides: Partial<Todo> = {}): Todo {
  return {
    id: 't1',
    title: 'Write tests',
    description: 'Cover the happy path',
    isCompleted: false,
    completedAt: null,
    dueDate: null,
    createdAt: '2030-01-01T00:00:00Z',
    updatedAt: '2030-01-01T00:00:00Z',
    ...overrides,
  }
}

const buttonByText = (wrapper: ReturnType<typeof mount>, text: string) =>
  wrapper.findAll('button').find((b) => b.text() === text)!

describe('TodoItem', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.mocked(todosApi.list).mockResolvedValue({ items: [], page: 1, pageSize: 10, totalCount: 0 })
  })

  it('renders title, description and due date', () => {
    const wrapper = mount(TodoItem, { props: { todo: makeTodo({ dueDate: '2999-12-31T00:00:00Z' }) } })

    expect(wrapper.text()).toContain('Write tests')
    expect(wrapper.text()).toContain('Cover the happy path')
    expect(wrapper.text()).toContain('Due')
    expect(wrapper.text()).not.toContain('Overdue')
  })

  it('flags open tasks past their due date as overdue, but not completed ones', () => {
    const open = mount(TodoItem, { props: { todo: makeTodo({ dueDate: '2000-01-01T00:00:00Z' }) } })
    const done = mount(TodoItem, {
      props: { todo: makeTodo({ dueDate: '2000-01-01T00:00:00Z', isCompleted: true }) },
    })

    expect(open.text()).toContain('Overdue')
    expect(done.text()).not.toContain('Overdue')
  })

  it('marks completed todos visually and reflects state in the checkbox', () => {
    const wrapper = mount(TodoItem, { props: { todo: makeTodo({ isCompleted: true }) } })

    expect(wrapper.classes()).toContain('done')
    expect(wrapper.find<HTMLInputElement>('input[type="checkbox"]').element.checked).toBe(true)
  })

  it('toggles completion through the API', async () => {
    vi.mocked(todosApi.setCompletion).mockResolvedValue(makeTodo({ isCompleted: true }))
    const wrapper = mount(TodoItem, { props: { todo: makeTodo() } })

    await wrapper.find('input[type="checkbox"]').setValue(true)
    await flushPromises()

    expect(todosApi.setCompletion).toHaveBeenCalledWith('t1', true)
  })

  it('requires confirmation before deleting', async () => {
    vi.mocked(todosApi.remove).mockResolvedValue()
    const wrapper = mount(TodoItem, { props: { todo: makeTodo() } })

    await buttonByText(wrapper, 'Delete').trigger('click')
    expect(todosApi.remove).not.toHaveBeenCalled()

    await buttonByText(wrapper, 'Keep').trigger('click')
    expect(todosApi.remove).not.toHaveBeenCalled()

    await buttonByText(wrapper, 'Delete').trigger('click')
    await buttonByText(wrapper, 'Yes, delete').trigger('click')
    await flushPromises()

    expect(todosApi.remove).toHaveBeenCalledWith('t1')
  })

  it('shows an inline error when an action fails', async () => {
    vi.mocked(todosApi.remove).mockRejectedValue(new ApiError(404, 'Todo not found.'))
    const wrapper = mount(TodoItem, { props: { todo: makeTodo() } })

    await buttonByText(wrapper, 'Delete').trigger('click')
    await buttonByText(wrapper, 'Yes, delete').trigger('click')
    await flushPromises()

    expect(wrapper.find('[role="alert"]').text()).toBe('Todo not found.')
  })

  it('edits in place and saves through the API', async () => {
    vi.mocked(todosApi.update).mockResolvedValue(makeTodo({ title: 'Renamed' }))
    const wrapper = mount(TodoItem, { props: { todo: makeTodo() } })

    await buttonByText(wrapper, 'Edit').trigger('click')
    await wrapper.find('input[type="text"]').setValue('Renamed')
    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(todosApi.update).toHaveBeenCalledWith('t1', {
      title: 'Renamed',
      description: 'Cover the happy path',
      dueDate: null,
    })
    expect(wrapper.find('form').exists()).toBe(false)
  })

  it('leaves edit mode without saving on cancel', async () => {
    const wrapper = mount(TodoItem, { props: { todo: makeTodo() } })

    await buttonByText(wrapper, 'Edit').trigger('click')
    await buttonByText(wrapper, 'Cancel').trigger('click')

    expect(todosApi.update).not.toHaveBeenCalled()
    expect(wrapper.find('form').exists()).toBe(false)
  })
})
