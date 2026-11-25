using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ObentoTensho.Api.Http;

// Enum trên query, đường dẫn đọc cùng dạng snake_case như JSON (vd "no_show", "in_store"); dạng viết liền cũ vẫn nhận.
// Bộ đọc mặc định chỉ hiểu tên viết liền nên giá trị có dấu gạch dưới bị trả 400. Số không được nhận (giống JSON).
public sealed class SnakeCaseEnumBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var type = Nullable.GetUnderlyingType(context.Metadata.ModelType) ?? context.Metadata.ModelType;
        var source = context.BindingInfo.BindingSource;
        return type.IsEnum && (source is null || source == BindingSource.Query || source == BindingSource.Path || source == BindingSource.ModelBinding)
            ? new SnakeCaseEnumBinder(type)
            : null;
    }
}
