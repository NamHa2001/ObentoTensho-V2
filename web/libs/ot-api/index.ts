import type { components } from './schema';

// Kiểu dữ liệu sinh từ OpenAPI (npm run api:types). Không sửa tay schema.d.ts
export type Schemas = components['schemas'];

export interface PageMeta {
  page: number;
  pageSize: number;
  total: number;
}
