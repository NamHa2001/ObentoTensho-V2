using System.Diagnostics;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Api.Logging;

// SEC-53: log có cấu trúc (một dòng JSON mỗi bản ghi, kèm traceId để nối với ProblemDetails) và mọi giá trị đều qua LogRedactor
public sealed class RedactingJsonConsoleFormatter(IClock clock) : ConsoleFormatter(FormatterName)
{
    public const string FormatterName = "redacted-json";

    private static readonly JsonWriterOptions WriterOptions = new() { Encoder = JavaScriptEncoder.Create(UnicodeRanges.All) };

    public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider? scopeProvider, TextWriter textWriter)
    {
        ArgumentNullException.ThrowIfNull(textWriter);
        var message = logEntry.Formatter?.Invoke(logEntry.State, logEntry.Exception);
        if (message is null && logEntry.Exception is null)
        {
            return;
        }

        using var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer, WriterOptions))
        {
            json.WriteStartObject();
            json.WriteString("timestamp", clock.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            json.WriteString("level", logEntry.LogLevel.ToString());
            json.WriteString("category", logEntry.Category);
            json.WriteNumber("eventId", logEntry.EventId.Id);
            json.WriteString("message", LogRedactor.Scrub(message ?? string.Empty));
            if (Activity.Current is { } activity)
            {
                json.WriteString("traceId", activity.TraceId.ToHexString());
                json.WriteString("spanId", activity.SpanId.ToHexString());
            }

            if (logEntry.State is IReadOnlyList<KeyValuePair<string, object?>> state)
            {
                WriteState(json, state);
            }

            if (logEntry.Exception is { } exception)
            {
                json.WriteString("exception", LogRedactor.Scrub(exception.ToString().ReplaceLineEndings(" | ")));
            }

            json.WriteEndObject();
        }

        textWriter.WriteLine(System.Text.Encoding.UTF8.GetString(buffer.ToArray()));
    }

    private static void WriteState(Utf8JsonWriter json, IReadOnlyList<KeyValuePair<string, object?>> state)
    {
        var wrote = false;
        foreach (var (key, value) in state)
        {
            // {OriginalFormat} là khuôn thông điệp, không phải dữ liệu
            if (string.Equals(key, "{OriginalFormat}", StringComparison.Ordinal))
            {
                continue;
            }

            if (!wrote)
            {
                json.WriteStartObject("state");
                wrote = true;
            }

            json.WriteString(key, LogRedactor.Value(key, value));
        }

        if (wrote)
        {
            json.WriteEndObject();
        }
    }
}
