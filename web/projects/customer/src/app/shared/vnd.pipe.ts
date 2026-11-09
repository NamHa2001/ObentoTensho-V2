import { Pipe, PipeTransform } from '@angular/core';

@Pipe({ name: 'vnd' })
export class VndPipe implements PipeTransform {
  transform(value: number): string {
    return `${new Intl.NumberFormat('vi-VN').format(value)}\u00a0₫`;
  }
}
