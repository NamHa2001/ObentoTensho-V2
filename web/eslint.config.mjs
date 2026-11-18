// @ts-check
import angular from 'angular-eslint';
import { defineConfig } from 'eslint/config';
import tseslint from 'typescript-eslint';

// Luật riêng của dự án: mỗi lỗi từng gặp thành một luật máy tự bắt, kèm hướng sửa đúng.
// Không còn chỗ vi phạm nào được miễn. Thêm luật mới mà mã cũ chưa đạt thì ghi tạm bằng `npx eslint projects libs --suppress-all`
// (tạo eslint-suppressions.json), dọn dần rồi chạy `--prune-suppressions`; mã mới không được vi phạm thêm.
export default defineConfig(
  {
    ignores: ['dist/', '.angular/', 'node_modules/', 'libs/ot-api/schema.d.ts'],
  },
  {
    files: ['**/*.ts'],
    extends: [tseslint.configs.recommended, angular.configs.tsRecommended],
    processor: angular.processInlineTemplates,
    rules: {
      '@angular-eslint/component-selector': 'off',
      '@angular-eslint/directive-selector': 'off',
      '@typescript-eslint/no-explicit-any': ['error', { fixToUnknown: false }],
      '@typescript-eslint/no-unused-vars': ['error', { argsIgnorePattern: '^_', varsIgnorePattern: '^_' }],
      'no-restricted-imports': [
        'error',
        {
          paths: [
            {
              name: '@angular/common/http',
              importNames: ['HttpClient'],
              message: 'Gọi API của quán qua service Api (core/api.ts): Api lo địa chỉ API, render phía server, token và xử lý lỗi chung.',
            },
          ],
        },
      ],
      'no-restricted-globals': [
        'error',
        {
          name: 'fetch',
          message: 'Gọi API của quán qua service Api. Dịch vụ bên ngoài (bản đồ…) thì ghi ngoại lệ kèm lý do: // eslint-disable-next-line no-restricted-globals -- <lý do>',
        },
      ],
      'no-restricted-syntax': [
        'error',
        {
          selector: "CallExpression[callee.name='setTimeout'][arguments.length<2]",
          message: 'setTimeout không có thời gian chờ thường là chữa cháy thứ tự chạy. Chờ giao diện vẽ xong thì dùng afterNextRender; chờ dữ liệu thì dùng effect/computed.',
        },
        {
          selector: "CallExpression[callee.name='setTimeout'] > Literal.arguments:nth-child(2)[value<1000]",
          message: 'setTimeout chờ dưới 1 giây thường là đoán mò thời điểm giao diện sẵn sàng. Dùng afterNextRender hoặc ResizeObserver; hẹn giờ thật (ẩn thông báo, thử lại) thì đặt hằng số có tên, vd TOAST_MS.',
        },
      ],
    },
  },
  {
    files: ['**/core/api.ts'],
    rules: { 'no-restricted-imports': 'off' },
  },
  {
    files: ['**/*.spec.ts'],
    rules: { '@typescript-eslint/no-explicit-any': 'off', 'no-restricted-imports': 'off', 'no-restricted-globals': 'off' },
  },
  {
    files: ['**/*.html'],
    extends: [angular.configs.templateRecommended, angular.configs.templateAccessibility],
  },
);
