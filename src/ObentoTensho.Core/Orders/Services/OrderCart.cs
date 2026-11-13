using ObentoTensho.Contracts.Dtos.Orders;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Menu.Abstractions;
using ObentoTensho.Core.Menu.Domain;
using ObentoTensho.Core.Menu.Services;
using ObentoTensho.Core.Orders.Domain;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Core.Orders.Services;

// BR-ORD-01/02/22/23: dựng dòng đơn từ CSDL — giá, tình trạng món và tùy chọn đều đọc lại lúc này;
// client chỉ gửi mã món, số lượng, mã tùy chọn. Tên đã dịch lấy từ ảnh chụp thực đơn (chỉ để hiển thị).
public sealed class OrderCart(IMenuRepository menu, PublicMenuService publicMenu, IClock clock)
{
    public const int MaxQuantity = 99;
    public const int NoteMaxLength = 200;

    public sealed record Line(int Index, OrderItem? Item, string? ErrorCode);

    // Tại bàn: đợt gọi và lúc gửi bếp; món không bán mang về vẫn gọi được khi ăn tại quán
    public sealed record DineInRound(int Round, DateTime SentAtUtc);

    public sealed record Priced(IReadOnlyList<Line> Lines)
    {
        public bool IsValid => Lines.Count > 0 && Lines.All(l => l.ErrorCode is null);

        public IReadOnlyList<OrderItem> Items => Lines.Select(l => l.Item).OfType<OrderItem>().ToArray();

        public decimal SubTotal => OrderPricing.SubTotal(Items.Select(i => (i.ItemPrice, i.OptionsPrice, i.Quantity)));
    }

    public async Task<Priced> PriceAsync(IReadOnlyList<OrderLineRequest> requests, string language, CancellationToken cancellationToken, DineInRound? dineIn = null)
    {
        ArgumentNullException.ThrowIfNull(requests);
        var data = await menu.LoadOrderableAsync(requests.Select(r => r.MenuItemId).Distinct().ToArray(), cancellationToken);
        var snapshot = await publicMenu.SnapshotAsync(cancellationToken);
        var context = new BuildContext(data, snapshot, language, clock.UtcNow, dineIn);
        return new Priced(requests.Select((r, i) => Build(i, r, context)).ToArray());
    }

    private static Line Build(int index, OrderLineRequest request, BuildContext context)
    {
        if (request.Quantity is < 1 or > MaxQuantity || request.Note is { Length: > NoteMaxLength })
        {
            return new Line(index, null, ErrorCodes.ValidationFailed);
        }

        if (context.Items.GetValueOrDefault(request.MenuItemId) is not { } item || (context.DineIn is null && !item.AllowTakeaway)
            || MenuRules.Effective(item.Status, item.SoldOutUntilUtc, context.NowUtc) != SaleStatus.Available)
        {
            return new Line(index, null, ErrorCodes.ItemUnavailable);
        }

        var groups = context.GroupsOf(item.Id);
        var outcome = OptionSelection.Check(groups, request.OptionIds ?? []);
        if (!outcome.IsValid)
        {
            return new Line(index, null, outcome.ErrorCode);
        }

        // Giữ thứ tự hiển thị nhóm của món rồi thứ tự tùy chọn trong nhóm
        var selected = groups
            .SelectMany(g => g.Options.Where(o => outcome.Selected.Contains(o)).Select(o => (g.Group, Option: o)))
            .Select(x => new OrderItemOption
            {
                OptionId = x.Option.Id,
                GroupName = x.Group.Name,
                OptionName = x.Option.Name,
                GroupNameLocalized = context.Name<OptionGroupTranslation>(x.Group.Id, x.Group.Name),
                OptionNameLocalized = context.Name<OptionTranslation>(x.Option.Id, x.Option.Name),
                PriceDelta = x.Option.PriceDelta,
            })
            .ToList();

        return new Line(index, new OrderItem
        {
            MenuItemId = item.Id,
            ItemName = item.NameVi,
            ItemNameLocalized = context.Name<MenuItemTranslation>(item.Id, item.NameVi),
            ItemPrice = item.Price,
            OptionsPrice = outcome.PriceDelta,
            Quantity = request.Quantity,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            Options = selected,
            Round = context.DineIn?.Round ?? 0,
            SentAtUtc = context.DineIn?.SentAtUtc,
        }, null);
    }

    private sealed class BuildContext(OrderableMenu data, MenuSnapshot snapshot, string language, DateTime nowUtc, DineInRound? dineIn)
    {
        public DineInRound? DineIn { get; } = dineIn;

        private readonly Dictionary<int, OptionGroup> _groups = data.Groups.ToDictionary(g => g.Id);
        private readonly ILookup<int, MenuOption> _options = data.Options.OrderBy(o => o.DisplayOrder).ThenBy(o => o.Id).ToLookup(o => o.GroupId);
        private readonly ILookup<int, MenuItemOptionGroup> _links = data.Links.OrderBy(l => l.DisplayOrder).ToLookup(l => l.MenuItemId);

        public Dictionary<int, MenuItem> Items { get; } = data.Items.ToDictionary(i => i.Id);

        public DateTime NowUtc { get; } = nowUtc;

        public OptionSelection.GroupOptions[] GroupsOf(int itemId) =>
            _links[itemId]
                .Select(l => _groups.GetValueOrDefault(l.GroupId))
                .OfType<OptionGroup>()
                .Select(g => new OptionSelection.GroupOptions(g, _options[g.Id].ToArray()))
                .ToArray();

        // Tên theo ngôn ngữ khách khi có bản dịch đã duyệt và khác bản Việt; không thì null (dùng bản Việt)
        public string? Name<T>(int id, string vietnamese)
            where T : MenuTranslation =>
            snapshot.Translation<T>(id, language)?.Name is { } name && !string.Equals(name, vietnamese, StringComparison.Ordinal) ? name : null;
    }
}
