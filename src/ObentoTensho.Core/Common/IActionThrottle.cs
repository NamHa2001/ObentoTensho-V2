namespace ObentoTensho.Core.Common;

// Bộ đếm theo cửa sổ thời gian cho các giới hạn nghiệp vụ nhỏ (khóa tra cứu sau 5 lần sai, giới hạn "Hiện SĐT")
public interface IActionThrottle
{
    int Count(string key);

    // Tăng và trả giá trị mới; bộ đếm tự hết sau `window` kể từ lần tăng đầu
    int Increment(string key, TimeSpan window);
}
