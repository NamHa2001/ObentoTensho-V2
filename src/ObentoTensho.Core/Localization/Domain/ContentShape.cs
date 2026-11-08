using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Core.Localization.Domain;

// Trường nào dịch được và độ dài tối đa — theo cột của bản Việt (docs/srs/08)
public sealed record ContentShape(int TitleMax, int? SummaryMax, bool HasBody, bool RichBody, int? CtaMax, bool HasMeta)
{
    public const int BodyMax = 200_000;
    public const int MetaTitleMax = 200;
    public const int MetaDescriptionMax = 500;

    public static ContentShape For(TranslatableContent kind) => kind switch
    {
        TranslatableContent.Promotion => new(200, null, HasBody: true, RichBody: false, null, HasMeta: false),
        TranslatableContent.Banner => new(200, 300, HasBody: false, RichBody: false, 100, HasMeta: false),
        TranslatableContent.Gallery => new(300, null, HasBody: false, RichBody: false, null, HasMeta: false),
        TranslatableContent.News => new(300, 500, HasBody: true, RichBody: true, null, HasMeta: true),
        _ => new(200, null, HasBody: true, RichBody: true, null, HasMeta: false),
    };
}
