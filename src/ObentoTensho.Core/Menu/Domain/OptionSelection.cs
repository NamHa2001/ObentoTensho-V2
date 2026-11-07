using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;

namespace ObentoTensho.Core.Menu.Domain;

// BR-MNU-20/21: server kiểm lựa chọn của khách và tự tính tiền tùy chọn từ CSDL (bỏ mọi giá client gửi — SEC-70)
public static class OptionSelection
{
    public sealed record GroupOptions(OptionGroup Group, IReadOnlyList<MenuOption> Options);

    public sealed record Outcome(string? ErrorCode, IReadOnlyList<string> Problems, decimal PriceDelta, IReadOnlyList<MenuOption> Selected)
    {
        public bool IsValid => ErrorCode is null;
    }

    public static Outcome Check(IReadOnlyList<GroupOptions> groups, IReadOnlyCollection<int> selectedOptionIds)
    {
        var problems = new List<string>();
        if (selectedOptionIds.Distinct().Count() != selectedOptionIds.Count)
        {
            problems.Add("duplicate_option");
        }

        var byId = groups.Where(g => g.Group.IsActive)
            .SelectMany(g => g.Options.Select(o => (g.Group, Option: o)))
            .ToDictionary(x => x.Option.Id);
        var chosen = selectedOptionIds.Distinct().Select(id => byId.TryGetValue(id, out var hit) ? hit : default).ToArray();

        // Tùy chọn không thuộc nhóm nào của món này (hoặc nhóm đã tắt)
        if (chosen.Any(c => c.Option is null))
        {
            problems.Add("option_not_in_item");
        }

        var valid = chosen.Where(c => c.Option is not null).ToArray();
        foreach (var group in groups.Where(g => g.Group.IsActive).Select(g => g.Group))
        {
            var count = valid.Count(c => c.Group.Id == group.Id);
            var min = group.IsRequired ? Math.Max((int)group.MinSelect, 1) : group.MinSelect;
            var max = group.SelectionType == OptionSelectionType.One ? 1 : group.MaxSelect;
            if (count < min || count > max)
            {
                problems.Add($"group_{group.Id}_count");
            }
        }

        if (problems.Count > 0)
        {
            return new Outcome(ErrorCodes.OptionInvalid, problems, 0, []);
        }

        // Hết/ẩn: lỗi riêng để giao diện báo khách chọn lại (BR-MNU-21)
        var unavailable = valid.Where(c => c.Option.Status != SaleStatus.Available).Select(c => $"option_{c.Option.Id}_unavailable").ToArray();
        return unavailable.Length > 0
            ? new Outcome(ErrorCodes.OptionUnavailable, unavailable, 0, [])
            : new Outcome(null, [], valid.Sum(c => c.Option.PriceDelta), valid.Select(c => c.Option).ToArray());
    }
}
