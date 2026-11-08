namespace ObentoTensho.Core.Common;

// TIME-08, docs/srs/20 §4: việc chạy mỗi ngày một lần từ giờ VN cố định. Ngày đã chạy lưu trong CSDL nên khởi động lại
// không chạy lặp; máy tắt đúng giờ thì chạy bù khi bật lại. Interval là khoảng chờ thử lại khi lượt trước lỗi
public interface IDailyJob : IRecurringJob
{
    TimeOnly RunAtLocal { get; }
}
