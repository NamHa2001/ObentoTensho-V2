import { HttpErrorResponse } from '@angular/common/http';
import { errorText } from './form';

const invalid = (errors: Record<string, string[]>) => new HttpErrorResponse({ status: 400, error: { title: 'Dữ liệu không hợp lệ', errors } });

describe('Chữ báo lỗi từ server', () => {
  it('gọi trường theo nhãn của form, không in tên kỹ thuật', () => {
    expect(errorText(invalid({ Name: ['REQUIRED'] }), { name: 'Tên danh mục' })).toBe('Tên danh mục: chưa nhập');
    expect(errorText(invalid({ '$.startDate': ['INVALID_FORMAT'] }))).toBe('Từ ngày: không đúng định dạng');
  });

  it('lỗi cấp "request" hoặc trường lạ chỉ hiện câu chung', () => {
    expect(errorText(invalid({ request: ['INVALID_FORMAT'], '$.categoryId': ['INVALID_FORMAT'] }))).toBe('Danh mục: không đúng định dạng. Một số ô khác chưa đúng');
    expect(errorText(invalid({ request: ['INVALID_FORMAT'] }))).toBe('Vui lòng kiểm tra lại các ô đã nhập');
    expect(errorText(invalid({ Foo: ['WEIRD_CODE'] }))).not.toMatch(/Foo|WEIRD_CODE/);
  });
});
