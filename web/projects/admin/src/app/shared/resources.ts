import { Option, Resource } from './resource';

const SALE: Option[] = [{ value: 'available', label: 'Đang bán' }, { value: 'sold_out', label: 'Hết hàng' }, { value: 'hidden', label: 'Ẩn' }];
const ALLERGENS: Option[] = [
  { value: 'egg', label: 'Trứng' }, { value: 'milk', label: 'Sữa' }, { value: 'soy', label: 'Đậu nành' }, { value: 'wheat', label: 'Lúa mì' }, { value: 'shellfish', label: 'Hải sản có vỏ' },
  { value: 'fish', label: 'Cá' }, { value: 'peanut', label: 'Đậu phộng' }, { value: 'tree_nut', label: 'Các loại hạt' }, { value: 'sesame', label: 'Mè' },
];
const GALLERY: Option[] = [{ value: 'interior', label: 'Trong quán' }, { value: 'exterior', label: 'Bên ngoài' }, { value: 'private', label: 'Phòng riêng' }, { value: 'food', label: 'Món ăn' }, { value: 'event', label: 'Sự kiện' }];
const ROLES: Option[] = [{ value: 'super_admin', label: 'SuperAdmin' }, { value: 'manager', label: 'Quản lý' }, { value: 'reception', label: 'Lễ tân' }, { value: 'kitchen', label: 'Bếp' }, { value: 'cashier', label: 'Thu ngân' }, { value: 'content_editor', label: 'Biên tập' }];
const TIERS: Option[] = [{ value: 'bronze', label: 'Đồng' }, { value: 'silver', label: 'Bạc' }, { value: 'gold', label: 'Vàng' }, { value: 'diamond', label: 'Kim cương' }];
const DAYS: Option[] = [{ value: 1, label: 'Thứ hai' }, { value: 2, label: 'Thứ ba' }, { value: 3, label: 'Thứ tư' }, { value: 4, label: 'Thứ năm' }, { value: 5, label: 'Thứ sáu' }, { value: 6, label: 'Thứ bảy' }, { value: 0, label: 'Chủ nhật' }];
const label = (options: Option[], value: unknown) => options.find((o) => o.value === value)?.label ?? value;
const approval = { pending: 'Chờ duyệt', approved: 'Đã duyệt', rejected: 'Từ chối' } as Record<string, string>;

const EXPORT_KIND: Record<string, string> = { customers: 'Khách hàng', newsletter_subscribers: 'Người nhận bản tin' };

export const RESOURCES: Record<string, Resource> = {
  categories: {
    title: 'Danh mục món', path: 'menu/categories', reorder: true, canCreate: true, canEdit: true, canDelete: true,
    columns: [{ key: 'name', label: 'Tên' }, { key: 'slug', label: 'Đường dẫn' }, { key: 'itemCount', label: 'Số món' }, { key: 'isActive', label: 'Hiện' }],
    fields: [{ key: 'name', label: 'Tên', required: true }, { key: 'slug', label: 'Đường dẫn (để trống tự tạo)' }, { key: 'isActive', label: 'Hiện', type: 'bool', default: true }, { key: 'imageId', label: 'Ảnh', type: 'image' }],
  },
  items: {
    title: 'Món ăn', path: 'menu/items', paged: true, canCreate: true, canEdit: true, canDelete: true, detail: true,
    filters: [{ key: 'q', label: 'Tìm món' }, { key: 'categoryId', label: 'Mọi danh mục', optionsFrom: { path: 'menu/categories', value: 'id', label: 'name' } }, { key: 'status', label: 'Trạng thái', options: SALE }],
    tools: [{ label: 'Xuất Excel', kind: 'download', path: 'menu/items/export', file: 'thuc-don.xlsx' }, { label: 'Tải tệp mẫu', kind: 'download', path: 'menu/items/import-template', file: 'mau-thuc-don.xlsx' }, { label: 'Nhập Excel', kind: 'import', path: 'menu/items/import' }],
    columns: [{ key: 'nameVi', label: 'Tên' }, { key: 'price', label: 'Giá', map: (r) => `${new Intl.NumberFormat('vi-VN').format(r.price)} ₫` }, { key: 'status', label: 'Trạng thái', map: (r) => label(SALE, r.status) }],
    actions: [
      { label: 'Ảnh', link: (r) => ['/item-images', r.id], run: undefined as never },
      { label: 'Hết hàng', when: (r) => r.status === 'available', run: (a, r) => a.patch(`menu/items/${r.id}/status`, { status: 'sold_out', soldOutUntilUtc: null }, r.version) },
      { label: 'Mở bán', when: (r) => r.status !== 'available', run: (a, r) => a.patch(`menu/items/${r.id}/status`, { status: 'available', soldOutUntilUtc: null }, r.version) },
    ],
    fields: [
      { key: 'nameVi', label: 'Tên', required: true },
      { key: 'slug', label: 'Đường dẫn (để trống tự tạo)' },
      { key: 'categoryId', label: 'Danh mục', type: 'select', required: true, optionsFrom: { path: 'menu/categories', value: 'id', label: 'name' } },
      { key: 'price', label: 'Giá', type: 'number', required: true },
      { key: 'originalPrice', label: 'Giá gốc', type: 'number' },
      { key: 'shortDesc', label: 'Mô tả ngắn' },
      { key: 'fullDescHtml', label: 'Mô tả', type: 'rich' },
      { key: 'status', label: 'Trạng thái', type: 'select', required: true, options: SALE, default: 'available' },
      { key: 'allowTakeaway', label: 'Cho mang về', type: 'bool', default: true },
      { key: 'isNew', label: 'Món mới', type: 'bool', read: (r) => r.labels?.isNew },
      { key: 'isBestseller', label: 'Bán chạy', type: 'bool', read: (r) => r.labels?.isBestseller },
      { key: 'isFeatured', label: 'Nổi bật', type: 'bool', read: (r) => r.labels?.isFeatured },
      { key: 'isVegetarian', label: 'Món chay', type: 'bool', read: (r) => r.labels?.isVegetarian },
      { key: 'isSpicy', label: 'Cay', type: 'bool', read: (r) => r.labels?.isSpicy },
      { key: 'allergens', label: 'Dị ứng', type: 'multi', options: ALLERGENS },
      { key: 'optionGroupIds', label: 'Nhóm tùy chọn', type: 'multi', optionsFrom: { path: 'menu/option-groups', value: 'id', label: 'name' } },
    ],
    toBody: ({ isNew, isBestseller, isFeatured, isVegetarian, isSpicy, ...rest }) => ({ ...rest, labels: { isNew: !!isNew, isBestseller: !!isBestseller, isFeatured: !!isFeatured, isVegetarian: !!isVegetarian, isSpicy: !!isSpicy } }),
  },
  banners: {
    title: 'Banner', path: 'banners', reorder: true, canCreate: true, canEdit: true, canDelete: true,
    columns: [{ key: 'image', label: 'Ảnh', thumb: (r) => r.image?.variants?.at(-1)?.url }, { key: 'title', label: 'Tiêu đề' }, { key: 'showFromDate', label: 'Từ ngày' }, { key: 'showToDate', label: 'Đến ngày' }, { key: 'isActive', label: 'Hiện' }],
    fields: [
      { key: 'imageId', label: 'Ảnh máy tính: 1920 × 666 px (tỉ lệ 75:26), chữ và nút vẽ sẵn trong ảnh', type: 'image', required: true, read: (r) => r.image?.id },
      { key: 'imageMobileId', label: 'Ảnh điện thoại: 1080 × 1620 px (tỉ lệ 2:3)', type: 'image', read: (r) => r.imageMobile?.id },
      { key: 'title', label: 'Tên banner (đọc cho người khiếm thị và Google, không hiện ra)', required: true }, { key: 'subtitle', label: 'Ghi chú nội bộ' }, { key: 'ctaText', label: 'Chữ trên nút (không dùng)' }, { key: 'ctaLink', label: 'Bấm vào banner thì mở (vd /khuyen-mai)' },
      { key: 'showFromDate', label: 'Hiện từ ngày', type: 'date' }, { key: 'showToDate', label: 'Hiện đến ngày', type: 'date' }, { key: 'isActive', label: 'Hiện', type: 'bool', default: true },
    ],
  },
  gallery: {
    title: 'Không gian', path: 'gallery', reorder: true, canCreate: true, canEdit: true, canDelete: true,
    columns: [{ key: 'image', label: 'Ảnh', thumb: (r) => r.image?.variants?.at(-1)?.url }, { key: 'caption', label: 'Chú thích' }, { key: 'mediaType', label: 'Loại' }, { key: 'category', label: 'Nhóm', map: (r) => label(GALLERY, r.category) }, { key: 'isActive', label: 'Hiện' }],
    fields: [
      { key: 'mediaType', label: 'Loại', type: 'select', required: true, default: 'image', options: [{ value: 'image', label: 'Ảnh' }, { value: 'video', label: 'Video' }] },
      { key: 'imageId', label: 'Ảnh', type: 'image', read: (r) => r.image?.id }, { key: 'videoUrl', label: 'Địa chỉ video' },
      { key: 'category', label: 'Nhóm', type: 'select', required: true, default: 'interior', options: GALLERY }, { key: 'caption', label: 'Chú thích' }, { key: 'isActive', label: 'Hiện', type: 'bool', default: true },
    ],
  },
  news: {
    title: 'Tin tức', path: 'news', paged: true, canCreate: true, canEdit: true, canDelete: true, detail: true,
    filters: [{ key: 'q', label: 'Tìm bài' }],
    columns: [{ key: 'title', label: 'Tiêu đề' }, { key: 'status', label: 'Trạng thái' }, { key: 'publishedAtUtc', label: 'Đăng lúc' }],
    fields: [
      { key: 'title', label: 'Tiêu đề', required: true }, { key: 'slug', label: 'Đường dẫn (để trống tự tạo)' }, { key: 'thumbnailImageId', label: 'Ảnh đại diện', type: 'image', read: (r) => r.thumbnail?.id },
      { key: 'summary', label: 'Tóm tắt' }, { key: 'contentHtml', label: 'Nội dung', type: 'rich', required: true },
      { key: 'categoryId', label: 'Danh mục', type: 'select', optionsFrom: { path: 'news/categories', value: 'id', label: 'name' } }, { key: 'authorDisplayName', label: 'Tác giả' },
      { key: 'metaTitle', label: 'Tiêu đề SEO' }, { key: 'metaDescription', label: 'Mô tả SEO' }, { key: 'ogImageId', label: 'Ảnh chia sẻ', type: 'image', read: (r) => r.ogImage?.id },
      { key: 'status', label: 'Trạng thái', type: 'select', required: true, default: 'draft', options: [{ value: 'draft', label: 'Nháp' }, { value: 'published', label: 'Đăng' }, { value: 'hidden', label: 'Ẩn' }] },
      { key: 'publishedAtUtc', label: 'Đăng lúc', type: 'datetime' },
    ],
  },
  'news-categories': {
    title: 'Danh mục tin', path: 'news/categories', canCreate: true, canEdit: true, canDelete: true,
    columns: [{ key: 'name', label: 'Tên' }, { key: 'slug', label: 'Đường dẫn' }, { key: 'isActive', label: 'Hiện' }],
    fields: [{ key: 'name', label: 'Tên', required: true }, { key: 'slug', label: 'Đường dẫn' }, { key: 'isActive', label: 'Hiện', type: 'bool', default: true }],
  },
  pages: {
    title: 'Trang tĩnh', path: 'pages', id: 'slug', canEdit: true, detail: true,
    seedRows: [{ slug: 'about', title: 'Giới thiệu' }, { slug: 'contact', title: 'Liên hệ' }, { slug: 'privacy', title: 'Chính sách bảo mật' }, { slug: 'terms', title: 'Điều khoản' }, { slug: 'policy', title: 'Chính sách hủy và hoàn tiền' }],
    columns: [{ key: 'slug', label: 'Mã' }, { key: 'title', label: 'Tiêu đề' }, { key: 'updatedAtUtc', label: 'Cập nhật' }],
    fields: [{ key: 'title', label: 'Tiêu đề', required: true }, { key: 'contentHtml', label: 'Nội dung', type: 'rich', required: true }],
  },
  promotions: {
    title: 'Chương trình khuyến mãi', path: 'promotions', paged: true, canCreate: true, canEdit: true, canDelete: true,
    columns: [{ key: 'title', label: 'Tên' }, { key: 'startDate', label: 'Từ' }, { key: 'endDate', label: 'Đến' }, { key: 'isActive', label: 'Hiện' }],
    fields: [
      { key: 'title', label: 'Tên', required: true }, { key: 'slug', label: 'Đường dẫn' }, { key: 'descriptionHtml', label: 'Mô tả', type: 'rich' },
      { key: 'startDate', label: 'Từ ngày', type: 'date', required: true }, { key: 'endDate', label: 'Đến ngày', type: 'date', required: true }, { key: 'publicTeaser', label: 'Hiện trước khi bắt đầu', type: 'bool' }, { key: 'isActive', label: 'Hiện', type: 'bool', default: true }, { key: 'bannerImageId', label: 'Ảnh', type: 'image' },
    ],
  },
  vouchers: {
    title: 'Voucher', path: 'vouchers', paged: true, canCreate: true, canEdit: true, canDelete: true,
    tools: [
      { label: 'Xuất Excel', kind: 'download', path: 'vouchers/export', file: 'voucher.xlsx' },
      {
        label: 'Sinh hàng loạt', kind: 'form', path: 'vouchers/bulk-generate',
        fields: [
          { key: 'prefix', label: 'Tiền tố mã', required: true }, { key: 'quantity', label: 'Số lượng', type: 'number', required: true, default: 10 },
          { key: 'discountType', label: 'Kiểu giảm', type: 'select', required: true, default: 'percent', options: [{ value: 'percent', label: 'Phần trăm' }, { value: 'fixed', label: 'Số tiền' }] },
          { key: 'appliesTo', label: 'Áp dụng cho', type: 'select', required: true, default: 'items', options: [{ value: 'items', label: 'Tiền món' }, { value: 'shipping', label: 'Phí ship' }] },
          { key: 'discountValue', label: 'Giá trị', type: 'number', required: true }, { key: 'maxDiscountAmount', label: 'Giảm tối đa', type: 'number' }, { key: 'minOrderValue', label: 'Đơn tối thiểu', type: 'number', required: true, default: 0 },
          { key: 'startDate', label: 'Từ ngày', type: 'date' }, { key: 'endDate', label: 'Đến ngày', type: 'date' },
        ],
        toBody: ({ prefix, quantity, ...rest }) => ({ prefix, quantity, template: { code: `${prefix}-MAU`, maxUsageTotal: 1, maxUsagePerUser: 1, applicableTier: null, requireLogin: true, isActive: true, promotionId: null, ...rest } }),
      },
    ],
    filters: [{ key: 'q', label: 'Tìm mã' }, { key: 'approvalStatus', label: 'Duyệt', options: Object.entries(approval).map(([value, l]) => ({ value, label: l })) }],
    columns: [{ key: 'code', label: 'Mã' }, { key: 'discountType', label: 'Kiểu', map: (r) => (r.discountType === 'percent' ? 'Phần trăm' : 'Số tiền') + (r.appliesTo === 'shipping' ? ' phí ship' : '') }, { key: 'discountValue', label: 'Giá trị' }, { key: 'usedCount', label: 'Đã dùng' }, { key: 'approvalStatus', label: 'Duyệt', map: (r) => approval[r.approvalStatus] }, { key: 'isActive', label: 'Bật' }],
    actions: [
      { label: 'Duyệt', when: (r) => r.approvalStatus === 'pending', prompt: 'Ghi chú duyệt', run: (a, r, note) => a.post(`vouchers/${r.id}/approve`, { note }, r.version) },
      { label: 'Từ chối', when: (r) => r.approvalStatus === 'pending', prompt: 'Lý do từ chối', run: (a, r, note) => a.post(`vouchers/${r.id}/reject`, { note }, r.version) },
    ],
    fields: [
      { key: 'code', label: 'Mã', required: true }, { key: 'discountType', label: 'Kiểu giảm', type: 'select', required: true, default: 'percent', options: [{ value: 'percent', label: 'Phần trăm' }, { value: 'fixed', label: 'Số tiền' }] },
      { key: 'appliesTo', label: 'Áp dụng cho', type: 'select', required: true, default: 'items', options: [{ value: 'items', label: 'Tiền món' }, { value: 'shipping', label: 'Phí ship' }] },
      { key: 'discountValue', label: 'Giá trị', type: 'number', required: true }, { key: 'maxDiscountAmount', label: 'Giảm tối đa', type: 'number' }, { key: 'minOrderValue', label: 'Đơn tối thiểu', type: 'number', default: 0, required: true },
      { key: 'maxUsageTotal', label: 'Tổng lượt dùng', type: 'number' }, { key: 'maxUsagePerUser', label: 'Lượt mỗi khách', type: 'number', default: 1, required: true }, { key: 'applicableTier', label: 'Hạng áp dụng', type: 'select', options: TIERS },
      { key: 'requireLogin', label: 'Cần đăng nhập', type: 'bool', default: true }, { key: 'startDate', label: 'Từ ngày', type: 'date' }, { key: 'endDate', label: 'Đến ngày', type: 'date' }, { key: 'isActive', label: 'Bật', type: 'bool', default: true },
      { key: 'promotionId', label: 'Thuộc chương trình', type: 'select', optionsFrom: { path: 'promotions', value: 'id', label: 'title' } },
    ],
  },
  // Khách bấm "Đổi điểm" sẽ thấy đúng danh sách này; voucher đã đổi giữ nguyên điều kiện lúc đổi
  'redeem-rewards': {
    title: 'Phần thưởng đổi điểm', path: 'redeem-rewards', canCreate: true, canEdit: true, canDelete: true,
    columns: [
      { key: 'name', label: 'Tên' }, { key: 'pointsCost', label: 'Điểm cần' },
      { key: 'discountValue', label: 'Mức giảm', map: (r) => (r.discountType === 'percent' ? `${r.discountValue}%` : `${new Intl.NumberFormat('vi-VN').format(r.discountValue)} ₫`) },
      { key: 'minOrderValue', label: 'Đơn tối thiểu', map: (r) => `${new Intl.NumberFormat('vi-VN').format(r.minOrderValue)} ₫` }, { key: 'validDays', label: 'Hạn dùng (ngày)' }, { key: 'isActive', label: 'Bật' },
    ],
    fields: [
      { key: 'name', label: 'Tên phần thưởng (khách thấy)', required: true }, { key: 'description', label: 'Mô tả ngắn' },
      { key: 'pointsCost', label: 'Số điểm cần', type: 'number', required: true, default: 100 },
      { key: 'discountType', label: 'Kiểu giảm', type: 'select', required: true, default: 'fixed', options: [{ value: 'percent', label: 'Phần trăm' }, { value: 'fixed', label: 'Số tiền' }] },
      { key: 'discountValue', label: 'Giá trị giảm', type: 'number', required: true }, { key: 'maxDiscountAmount', label: 'Giảm tối đa (với phần trăm)', type: 'number' },
      { key: 'minOrderValue', label: 'Đơn tối thiểu', type: 'number', required: true, default: 0 }, { key: 'validDays', label: 'Voucher dùng trong (ngày)', type: 'number', required: true, default: 30 },
      { key: 'isActive', label: 'Bật', type: 'bool', default: true },
    ],
  },
  reviews: {
    title: 'Đánh giá', path: 'reviews', paged: true,
    filters: [{ key: 'status', label: 'Trạng thái', options: Object.entries(approval).map(([value, l]) => ({ value, label: l })) }],
    columns: [{ key: 'customerName', label: 'Khách' }, { key: 'itemName', label: 'Món' }, { key: 'rating', label: 'Sao' }, { key: 'comment', label: 'Nhận xét' }, { key: 'status', label: 'Trạng thái', map: (r) => approval[r.status] }],
    actions: [
      { label: 'Duyệt', when: (r) => r.status === 'pending', run: (a, r) => a.post(`reviews/${r.id}/approve`, {}, r.version) },
      { label: 'Từ chối', when: (r) => r.status === 'pending', confirm: 'Từ chối đánh giá này?', run: (a, r) => a.post(`reviews/${r.id}/reject`, {}, r.version) },
      { label: 'Trả lời', prompt: 'Nội dung trả lời', run: (a, r, note) => a.post(`reviews/${r.id}/reply`, { content: note }, r.version) },
    ],
  },
  'contact-messages': {
    title: 'Tin liên hệ', path: 'contact-messages', paged: true,
    columns: [{ key: 'fullName', label: 'Họ tên' }, { key: 'email', label: 'Email' }, { key: 'phoneNumber', label: 'SĐT' }, { key: 'subject', label: 'Tiêu đề' }, { key: 'message', label: 'Nội dung' }, { key: 'isRead', label: 'Đã đọc' }, { key: 'createdAtUtc', label: 'Lúc' }],
    actions: [{ label: 'Đã đọc', when: (r) => !r.isRead, run: (a, r) => a.post(`contact-messages/${r.id}/read`) }, { label: 'Đã trả lời', when: (r) => !r.repliedAtUtc, run: (a, r) => a.post(`contact-messages/${r.id}/replied`) }],
  },
  subscribers: { title: 'Người nhận bản tin', path: 'newsletter/subscribers', paged: true, tools: [{ label: 'Xuất danh sách', kind: 'reason', path: 'newsletter/subscribers/export', message: 'Lý do xuất danh sách' }], columns: [{ key: 'email', label: 'Email' }, { key: 'lang', label: 'Ngôn ngữ' }, { key: 'isActive', label: 'Đang nhận' }, { key: 'subscribedAtUtc', label: 'Đăng ký lúc' }] },
  campaigns: {
    title: 'Chiến dịch bản tin', path: 'newsletter/campaigns', paged: true, canCreate: true, canEdit: true, canDelete: true,
    columns: [{ key: 'subject', label: 'Tiêu đề' }, { key: 'status', label: 'Trạng thái' }, { key: 'sentCount', label: 'Đã gửi' }, { key: 'recipientCount', label: 'Người nhận' }],
    actions: [{ label: 'Gửi', when: (r) => r.status === 'draft', confirm: 'Gửi chiến dịch này?', run: (a, r) => a.post(`newsletter/campaigns/${r.id}/send`, {}, r.version) }],
    fields: [{ key: 'subject', label: 'Tiêu đề', required: true }, { key: 'contentHtml', label: 'Nội dung', type: 'rich', required: true }, { key: 'scheduledAtUtc', label: 'Hẹn gửi', type: 'datetime' }],
  },
  'loyalty-adjustments': {
    title: 'Duyệt điều chỉnh điểm', path: 'loyalty/adjustments',
    columns: [{ key: 'userId', label: 'Khách' }, { key: 'points', label: 'Điểm' }, { key: 'reason', label: 'Lý do' }, { key: 'status', label: 'Trạng thái', map: (r) => approval[r.status] }, { key: 'requestedAtUtc', label: 'Lúc' }],
    actions: [
      { label: 'Duyệt', when: (r) => r.status === 'pending', prompt: 'Ghi chú', run: (a, r, note) => a.post(`loyalty/adjustments/${r.id}/approve`, { note }) },
      { label: 'Từ chối', when: (r) => r.status === 'pending', prompt: 'Lý do', run: (a, r, note) => a.post(`loyalty/adjustments/${r.id}/reject`, { note }) },
    ],
  },
  'table-areas': {
    title: 'Khu vực', path: 'table-areas', canCreate: true, canEdit: true, canDelete: true,
    columns: [{ key: 'name', label: 'Tên' }, { key: 'isActive', label: 'Hoạt động' }],
    fields: [{ key: 'name', label: 'Tên', required: true }, { key: 'isActive', label: 'Hoạt động', type: 'bool', default: true }],
  },
  tables: {
    title: 'Bàn', path: 'tables', canCreate: true, canEdit: true, canDelete: true,
    columns: [{ key: 'name', label: 'Tên' }, { key: 'minCapacity', label: 'Ít nhất' }, { key: 'maxCapacity', label: 'Nhiều nhất' }, { key: 'status', label: 'Trạng thái' }, { key: 'isActive', label: 'Hoạt động' }],
    fields: [
      { key: 'areaId', label: 'Khu vực', type: 'select', required: true, optionsFrom: { path: 'table-areas', value: 'id', label: 'name' } }, { key: 'name', label: 'Tên', required: true },
      { key: 'minCapacity', label: 'Số khách ít nhất', type: 'number', required: true, default: 1 }, { key: 'maxCapacity', label: 'Số khách nhiều nhất', type: 'number', required: true, default: 4 }, { key: 'isActive', label: 'Hoạt động', type: 'bool', default: true },
    ],
    toBody: (b) => ({ ...b, layout: { x: 0, y: 0, width: 80, height: 80, rotation: 0 } }),
  },
  'service-slots': {
    title: 'Ca phục vụ', path: 'service-slots', canCreate: true, canEdit: true, canDelete: true,
    columns: [{ key: 'dayOfWeek', label: 'Thứ', map: (r) => (r.dayOfWeek === null ? 'Mọi ngày' : label(DAYS, r.dayOfWeek)) }, { key: 'start', label: 'Bắt đầu' }, { key: 'end', label: 'Kết thúc' }, { key: 'maxReservations', label: 'Tối đa lượt' }, { key: 'maxGuests', label: 'Tối đa khách' }, { key: 'isActive', label: 'Hoạt động' }],
    fields: [{ key: 'dayOfWeek', label: 'Thứ (trống = mọi ngày)', type: 'select', options: DAYS }, { key: 'start', label: 'Bắt đầu', type: 'time', required: true }, { key: 'end', label: 'Kết thúc', type: 'time', required: true }, { key: 'maxReservations', label: 'Tối đa lượt đặt', type: 'number', required: true, default: 10 }, { key: 'maxGuests', label: 'Tối đa khách', type: 'number' }, { key: 'isActive', label: 'Hoạt động', type: 'bool', default: true }],
  },
  'blocked-dates': {
    title: 'Ngày khóa', path: 'blocked-dates', canCreate: true, canDelete: true,
    columns: [{ key: 'date', label: 'Ngày' }, { key: 'appliesTo', label: 'Áp dụng' }, { key: 'start', label: 'Từ giờ' }, { key: 'end', label: 'Đến giờ' }, { key: 'reason', label: 'Lý do' }],
    fields: [{ key: 'date', label: 'Ngày', type: 'date', required: true }, { key: 'appliesTo', label: 'Áp dụng', type: 'select', required: true, default: 'both', options: [{ value: 'both', label: 'Cả hai' }, { value: 'reservation', label: 'Đặt bàn' }, { value: 'order', label: 'Đặt món' }] }, { key: 'start', label: 'Từ giờ (trống = cả ngày)', type: 'time' }, { key: 'end', label: 'Đến giờ', type: 'time' }, { key: 'reason', label: 'Lý do' }],
  },
  staff: {
    title: 'Nhân viên', path: 'staff', paged: true, canCreate: true, canEdit: true,
    filters: [{ key: 'q', label: 'Tìm' }],
    columns: [{ key: 'fullName', label: 'Họ tên' }, { key: 'email', label: 'Email' }, { key: 'role', label: 'Vai trò', map: (r) => label(ROLES, r.role) }, { key: 'isActive', label: 'Hoạt động' }, { key: 'totpEnabled', label: '2FA' }, { key: 'invitePending', label: 'Chờ nhận lời mời' }],
    actions: [
      { label: 'Khóa', when: (r) => r.isActive, confirm: 'Khóa nhân viên này?', run: (a, r) => a.patch(`staff/${r.id}/status`, { isActive: false }) },
      { label: 'Mở khóa', when: (r) => !r.isActive, run: (a, r) => a.patch(`staff/${r.id}/status`, { isActive: true }) },
      { label: 'Mời lại', when: (r) => r.invitePending, run: (a, r) => a.post(`staff/${r.id}/resend-invite`) },
      { label: 'Thu hồi phiên', confirm: 'Đăng xuất nhân viên này khỏi mọi thiết bị?', run: (a, r) => a.post(`staff/${r.id}/revoke-sessions`) },
      { label: 'Đặt lại 2FA', confirm: 'Đặt lại 2FA của nhân viên này?', run: (a, r) => a.post(`staff/${r.id}/reset-2fa`) },
    ],
    fields: [{ key: 'fullName', label: 'Họ tên', required: true }, { key: 'email', label: 'Email', type: 'email', required: true, createOnly: true }, { key: 'role', label: 'Vai trò', type: 'select', required: true, default: 'reception', options: ROLES }],
  },
  audit: {
    title: 'Nhật ký thao tác', path: 'audit-logs', paged: true,
    filters: [{ key: 'action', label: 'Hành động' }, { key: 'entityType', label: 'Đối tượng' }],
    columns: [{ key: 'createdAtUtc', label: 'Lúc' }, { key: 'actorName', label: 'Người làm' }, { key: 'action', label: 'Hành động' }, { key: 'entityType', label: 'Đối tượng' }, { key: 'entityId', label: 'Mã' }, { key: 'ipAddress', label: 'IP' }],
  },
  exports: { title: 'Tệp xuất', path: 'exports', columns: [{ key: 'kind', label: 'Loại', map: (r) => EXPORT_KIND[r.kind] ?? r.kind }, { key: 'status', label: 'Trạng thái' }, { key: 'rowCount', label: 'Số dòng' }, { key: 'createdAtUtc', label: 'Lúc' }, { key: 'expiresAtUtc', label: 'Hết hạn' }] },
};
