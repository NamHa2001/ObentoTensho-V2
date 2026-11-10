import { Schemas } from '@ot/api';

export type RedeemReward = Schemas['RedeemRewardDto'];
export type VoucherOption = Schemas['VoucherOptionDto'];

interface Discount {
  discountType: 'percent' | 'fixed';
  discountValue: number;
  maxDiscountAmount: number | null;
  minOrderValue: number;
}

const THOUSAND = 1000;
const money = new Intl.NumberFormat('vi-VN');

// Cuống vé ghi gọn: "20%" hoặc "30K"
export function discountShort(v: Discount): string {
  if (v.discountType === 'percent') {
    return `${v.discountValue}%`;
  }

  return v.discountValue % THOUSAND === 0 ? `${v.discountValue / THOUSAND}K` : `${money.format(v.discountValue)}₫`;
}

// Một dòng điều kiện đọc được ngay: "Giảm tối đa 50.000₫ · Đơn từ 100.000₫"
export function discountTerms(v: Discount): string {
  const parts: string[] = [];
  if (v.discountType === 'percent' && v.maxDiscountAmount) {
    parts.push(`Giảm tối đa ${money.format(v.maxDiscountAmount)}₫`);
  }

  parts.push(v.minOrderValue > 0 ? `Đơn từ ${money.format(v.minOrderValue)}₫` : 'Mọi đơn hàng');
  return parts.join(' · ');
}

export const VOUCHER_SOURCE: Record<string, string> = { redeem: 'Đổi điểm', birthday: 'Quà sinh nhật', admin: 'Quán tặng', promotion: 'Khuyến mãi' };

// Lý do mã chưa dùng được, theo mã lỗi của API
export const VOUCHER_REASON: Record<string, string> = {
  VOUCHER_MIN_ORDER: 'Chưa đủ giá trị đơn tối thiểu',
  VOUCHER_TIER_MISMATCH: 'Không áp dụng cho hạng thành viên của bạn',
  VOUCHER_LOGIN_REQUIRED: 'Cần đăng nhập để dùng mã',
};
