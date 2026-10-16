import { describe, expect, it } from 'vitest'
import { card } from '@/test/factories'
import { checkMove, groupByStatus } from './board-logic'

describe('board logic', () => {
  it('allows moves the API declared valid', () => {
    expect(checkMove(card(), 'interview')).toEqual({ ok: true })
  })

  it('explains why a move is not allowed', () => {
    const result = checkMove(card(), 'saved')
    expect(result.ok).toBe(false)
    expect(!result.ok && result.reason).toBe('No se puede pasar de «Postulada» a «Guardada».')
    expect(checkMove(card(), 'applied').ok).toBe(false)
  })

  it('groups cards by status with every column present', () => {
    const groups = groupByStatus([card({ id: '1' }), card({ id: '2', status: 'offer' }), card({ id: '3' })])
    expect(groups.applied.map((c) => c.id)).toEqual(['1', '3'])
    expect(groups.offer).toHaveLength(1)
    expect(groups.rejected).toEqual([])
    expect(Object.keys(groups)).toHaveLength(9)
  })
})
