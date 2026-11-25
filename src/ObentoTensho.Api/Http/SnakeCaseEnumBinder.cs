using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ObentoTensho.Api.Http;

// Đọc một giá trị enum dạng snake_case hoặc viết liền; số và tên lạ báo lỗi ô (trả 400 VALIDATION_FAILED)
public sealed class SnakeCaseEnumBinder(Type enumType) : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);
        var value = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
        if (value == ValueProviderResult.None)
        {
            return Task.CompletedTask;
        }

        bindingContext.ModelState.SetModelValue(bindingContext.ModelName, value);
        var text = value.FirstValue;
        if (string.IsNullOrEmpty(text))
        {
            return Task.CompletedTask;
        }

        if (TryParse(enumType, text, out var parsed))
        {
            bindingContext.Result = ModelBindingResult.Success(parsed);
        }
        else
        {
            bindingContext.ModelState.TryAddModelError(bindingContext.ModelName, $"Giá trị '{text}' không hợp lệ.");
        }

        return Task.CompletedTask;
    }

    public static bool TryParse(Type enumType, string text, out object? value)
    {
        value = null;
        var name = text.Replace("_", string.Empty, StringComparison.Ordinal);
        if (name.Length == 0 || !char.IsLetter(name[0]) || !Enum.TryParse(enumType, name, ignoreCase: true, out var parsed) || !Enum.IsDefined(enumType, parsed))
        {
            return false;
        }

        value = parsed;
        return true;
    }
}
