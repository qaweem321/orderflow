import { errorMessage, formatDateTime, formatMoney, safeNextPath } from './format';

describe('format', () => {
  it('shows money from cents', () => {
    expect(formatMoney(8900)).toBe('$89.00');
    expect(formatMoney(5)).toBe('$0.05');
  });

  it('shows unambiguous UTC dates', () => {
    expect(formatDateTime('2026-10-29T23:30:00Z')).toBe('Oct 29, 2026, 23:30 UTC');
  });

  it('only allows same-site paths after login', () => {
    expect(safeNextPath('/orders/1?x=2')).toBe('/orders/1?x=2');
    expect(safeNextPath('https://evil.example')).toBe('/');
    expect(safeNextPath('//evil.example')).toBe('/');
    expect(safeNextPath('/\\evil.example')).toBe('/');
    expect(safeNextPath(null)).toBe('/');
  });

  it('reads problem details', () => {
    expect(errorMessage({ error: { title: 'Only 2 of Mouse left.' } })).toBe('Only 2 of Mouse left.');
    expect(errorMessage({ error: { title: 'One or more validation errors occurred.', errors: { Password: ['Too short.'] } } })).toBe('Too short.');
    expect(errorMessage(new Error('network'))).toBe('Something went wrong. Please try again.');
  });
});
