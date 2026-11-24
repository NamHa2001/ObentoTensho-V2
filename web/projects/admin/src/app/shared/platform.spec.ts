import { appLabel, discountOf, splitRevenue } from './platform';

const RATES = { deductionPercent: 25, vatPercent: 3, pitPercent: 1.5 };

// docs/srs/23 §9: màn hình tính thử phải ra cùng số với server
describe('đơn app', () => {
  it('Grab 100.000 giảm 15% thì thực nhận 59.925', () => {
    const off = discountOf(100_000, 'percent', 15);
    expect(off).toEqual({ amount: 15_000, percent: 15 });
    expect(splitRevenue(100_000 - off.amount, RATES)).toEqual({ net: 85_000, deduction: 21_250, vat: 2_550, pit: 1_275, received: 59_925 });
  });

  it('tại quán 200.000 không giảm thì thực nhận 141.000', () => {
    expect(splitRevenue(200_000, RATES).received).toBe(141_000);
  });

  it('nhập số tiền giảm thì hiện % một chữ số lẻ', () => {
    expect(discountOf(90_000, 'amount', 20_000)).toEqual({ amount: 20_000, percent: 22.2 });
  });

  it('Xanh không có kênh con nên chỉ hiện tên nhóm', () => {
    expect(appLabel('xanh', 'xanh')).toBe('Xanh');
    expect(appLabel('hima', 'be')).toBe('Be Hima');
  });
});
