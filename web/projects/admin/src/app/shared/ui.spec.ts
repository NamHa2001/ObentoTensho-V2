import { utcFromVnInput, vnInputFromUtc } from './ui';

describe('Giờ Việt Nam trong ô nhập', () => {
  it('đổi UTC sang giờ VN để hiện', () => {
    expect(vnInputFromUtc('2026-11-09T17:30:00Z')).toBe('2026-11-10T00:30');
  });

  it('đổi giờ VN sang UTC để gửi', () => {
    expect(utcFromVnInput('2026-11-10T00:30')).toBe('2026-11-09T17:30:00.000Z');
  });

  it('trống thì giữ null', () => {
    expect(vnInputFromUtc(null)).toBeNull();
    expect(utcFromVnInput('')).toBeNull();
  });
});
