import { HTTP_TRANSFER_CACHE_ORIGIN_MAP } from '@angular/common/http';
import { mergeApplicationConfig, ApplicationConfig, inject, REQUEST } from '@angular/core';
import { provideServerRendering, withRoutes } from '@angular/ssr';
import { appConfig } from './app.config';
import { API_ORIGIN } from './core/api';
import { serverRoutes } from './app.routes.server';

const serverConfig: ApplicationConfig = {
  providers: [
    provideServerRendering(withRoutes(serverRoutes)),
    { provide: API_ORIGIN, useValue: process.env['API_ORIGIN'] ?? 'http://localhost:5212' },
    // Dữ liệu server đã tải được trình duyệt dùng lại, không gọi API lần hai và không nháy khung chờ
    {
      provide: HTTP_TRANSFER_CACHE_ORIGIN_MAP,
      useFactory: () => {
        const request = inject(REQUEST, { optional: true });
        return request ? { [inject(API_ORIGIN)]: new URL(request.url).origin } : {};
      },
    },
  ]
};

export const config = mergeApplicationConfig(appConfig, serverConfig);
