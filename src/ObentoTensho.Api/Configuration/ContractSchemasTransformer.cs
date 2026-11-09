using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using ObentoTensho.Contracts.Dtos.Account;

namespace ObentoTensho.Api.Configuration;

// Controller trả IActionResult nên OpenAPI không tự biết kiểu dữ liệu trả về: đưa mọi DTO và enum của Contracts
// vào tài liệu để client TypeScript dùng đúng tên trường (FR-ARC-03)
internal sealed class ContractSchemasTransformer : IOpenApiDocumentTransformer
{
    public async Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.Schemas ??= new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal);
        var types = typeof(ProfileDto).Assembly.GetExportedTypes()
            .Where(t => !t.IsGenericTypeDefinition && !t.IsAbstract && (t.IsEnum || t.IsClass))
            .Where(t => t.Namespace is { } ns && (ns.StartsWith("ObentoTensho.Contracts.Dtos", StringComparison.Ordinal) || ns.StartsWith("ObentoTensho.Contracts.Enums", StringComparison.Ordinal)));
        foreach (var type in types)
        {
            if (!document.Components.Schemas.ContainsKey(type.Name))
            {
                document.Components.Schemas[type.Name] = await context.GetOrCreateSchemaAsync(type, parameterDescription: null, cancellationToken);
            }
        }
    }
}
