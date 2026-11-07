using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Menu.Domain;
using ObentoTensho.Core.Menu.Services;
using ObentoTensho.IntegrationTests.Fixtures;

namespace ObentoTensho.IntegrationTests.Orders;

// Thực đơn riêng cho mỗi test đặt món, ghi thẳng vào CSDL test (tên/slug ngẫu nhiên để không đụng test khác):
// Rice (65.000, bắt buộc chọn Size: Small +0 / Large +15.000, có bản dịch tiếng Anh đã duyệt),
// Tea (5.000, không tùy chọn), SoldOut (hết hàng), DineInOnly (không bán mang về),
// Topping (nhóm tùy chọn của Tea: Egg +5.000, Cheese hết hàng)
public sealed record OrderTestMenu(int Rice, int Small, int Large, int Tea, int Egg, int Cheese, int SoldOut, int DineInOnly)
{
    public static async Task<OrderTestMenu> CreateAsync(SqlServerFixture fixture)
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var now = DateTime.UtcNow;
        await using var db = fixture.CreateContext();

        var category = new MenuCategory { Name = $"Đặt món {tag}", Slug = $"dat-mon-{tag}", CreatedAtUtc = now };
        db.MenuCategories.Add(category);
        await db.SaveChangesAsync();

        MenuItem Item(string name, decimal price, SaleStatus status = SaleStatus.Available, bool takeaway = true) => new()
        {
            CategoryId = category.Id, NameVi = $"{name} {tag}", Slug = $"{name.ToLowerInvariant()}-{tag}", NameSearch = name,
            Price = price, Status = status, AllowTakeaway = takeaway, CreatedAtUtc = now,
        };

        var rice = Item("Rice", 65_000);
        var tea = Item("Tea", 5_000);
        var soldOut = Item("SoldOut", 40_000, SaleStatus.SoldOut);
        var dineIn = Item("DineInOnly", 90_000, takeaway: false);
        db.MenuItems.AddRange(rice, tea, soldOut, dineIn);

        var size = new OptionGroup { Name = "Size", SelectionType = OptionSelectionType.One, IsRequired = true, MinSelect = 1, MaxSelect = 1 };
        var topping = new OptionGroup { Name = "Topping", SelectionType = OptionSelectionType.Many, MinSelect = 0, MaxSelect = 2 };
        db.OptionGroups.AddRange(size, topping);
        await db.SaveChangesAsync();

        var small = new MenuOption { GroupId = size.Id, Name = "Nhỏ", PriceDelta = 0, IsDefault = true };
        var large = new MenuOption { GroupId = size.Id, Name = "Lớn", PriceDelta = 15_000 };
        var egg = new MenuOption { GroupId = topping.Id, Name = "Trứng", PriceDelta = 5_000 };
        var cheese = new MenuOption { GroupId = topping.Id, Name = "Phô mai", PriceDelta = 10_000, Status = SaleStatus.SoldOut };
        db.Options.AddRange(small, large, egg, cheese);
        await db.SaveChangesAsync();

        db.MenuItemOptionGroups.AddRange(
            new MenuItemOptionGroup { MenuItemId = rice.Id, GroupId = size.Id },
            new MenuItemOptionGroup { MenuItemId = tea.Id, GroupId = topping.Id });
        db.MenuItemTranslations.Add(new MenuItemTranslation
        {
            EntityId = rice.Id, Lang = "en", Name = "Chicken rice", SourceHash = MenuSnapshot.ItemHash(rice),
            ReviewStatus = TranslationStatus.Approved, UpdatedAtUtc = now,
        });
        db.OptionTranslations.Add(new OptionTranslation
        {
            EntityId = large.Id, Lang = "en", Name = "Large", SourceHash = MenuRules.SourceHash(large.Name),
            ReviewStatus = TranslationStatus.Approved, UpdatedAtUtc = now,
        });
        await db.SaveChangesAsync();

        return new OrderTestMenu(rice.Id, small.Id, large.Id, tea.Id, egg.Id, cheese.Id, soldOut.Id, dineIn.Id);
    }
}
