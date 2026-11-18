import { chartConfig, foldSlices, plain } from './chart';

const colors = { series: ['#1', '#2', '#3', '#4', '#5'], ink: '#i', line: '#l', surface: '#s' };

describe('Biểu đồ', () => {
  it('quá 5 phần thì gộp phần nhỏ vào Khác, tổng không đổi', () => {
    const slices = foldSlices([1, 2, 3, 4, 5, 6, 7].map((value, i) => ({ label: `L${i}`, value })));
    expect(slices).toHaveLength(5);
    expect(slices.at(-1)).toEqual({ label: 'Khác', value: 6 });
    expect(slices.reduce((s, p) => s + p.value, 0)).toBe(28);
  });

  it('bỏ phần bằng 0 ở biểu đồ tròn', () => {
    expect(foldSlices([{ label: 'A', value: 0 }, { label: 'B', value: 2 }])).toEqual([{ label: 'B', value: 2 }]);
  });

  it('biểu đồ một chuỗi không có chú giải, biểu đồ tròn có', () => {
    const points = [{ label: 'A', value: 1 }];
    expect(chartConfig('line', points, plain, colors).options?.plugins?.legend?.display).toBe(false);
    expect(chartConfig('doughnut', points, plain, colors).options?.plugins?.legend?.display).toBe(true);
  });

  it('khung chú thích khi rê chuột hiện đúng giá trị của điểm ở mọi loại biểu đồ', () => {
    for (const kind of ['line', 'bar', 'hbar', 'doughnut'] as const) {
      const label = chartConfig(kind, [{ label: 'A', value: 1500 }], plain, colors).options?.plugins?.tooltip?.callbacks?.label as (c: unknown) => string;
      expect(label({ raw: 1500 })).toBe(plain(1500));
    }
  });

  it('cột ngang đặt trục giá trị theo chiều ngang', () => {
    const config = chartConfig('hbar', [{ label: 'A', value: 1 }], plain, colors);
    expect(config.options?.indexAxis).toBe('y');
    expect(config.options?.scales?.['x']).toMatchObject({ beginAtZero: true });
  });
});
