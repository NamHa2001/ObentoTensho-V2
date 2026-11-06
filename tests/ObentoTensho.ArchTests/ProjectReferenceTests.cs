using System.Xml.Linq;

namespace ObentoTensho.ArchTests;

// docs/srs/95 §3.1: luật phụ thuộc giữa các project, kiểm trực tiếp trên *.csproj.
public sealed class ProjectReferenceTests
{
    private const string Prefix = "ObentoTensho.";

    private static readonly Lazy<IReadOnlyDictionary<string, IReadOnlySet<string>>> Graph = new(LoadGraph);

    // AR-01
    [Fact]
    public void AR_01_Contracts_khong_tham_chieu_project_nao()
    {
        Assert.Empty(ReferencesOf("Contracts"));
    }

    // AR-02: Core và Gaming không tham chiếu nhau, không tham chiếu Infrastructure/Api
    [Fact]
    public void AR_02_Core_chi_tham_chieu_Contracts()
    {
        Assert.Subset(new HashSet<string>(StringComparer.Ordinal) { "Contracts" }, ReferencesOf("Core").ToHashSet(StringComparer.Ordinal));
    }

    [Fact]
    public void AR_02_Gaming_chi_tham_chieu_Contracts()
    {
        if (!Graph.Value.ContainsKey("Gaming"))
        {
            return;
        }

        Assert.Subset(new HashSet<string>(StringComparer.Ordinal) { "Contracts" }, ReferencesOf("Gaming").ToHashSet(StringComparer.Ordinal));
    }

    // AR-03: Infrastructure đứng dưới Api, không phụ thuộc ngược lên Api
    [Fact]
    public void AR_03_Infrastructure_khong_tham_chieu_Api()
    {
        Assert.DoesNotContain("Api", ReferencesOf("Infrastructure"));
    }

    [Fact]
    public void Project_trong_src_khong_tham_chieu_project_test()
    {
        foreach (var (name, references) in Graph.Value.Where(p => !p.Key.EndsWith("Tests", StringComparison.Ordinal)))
        {
            Assert.True(
                !references.Any(r => r.EndsWith("Tests", StringComparison.Ordinal)),
                $"{name} không được tham chiếu project test");
        }
    }

    private static IReadOnlySet<string> ReferencesOf(string project) => Graph.Value[project];

    private static Dictionary<string, IReadOnlySet<string>> LoadGraph()
    {
        var root = FindRepositoryRoot();
        var graph = new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal);

        foreach (var folder in new[] { "src", "tests" })
        {
            foreach (var file in Directory.EnumerateFiles(Path.Combine(root, folder), "*.csproj", SearchOption.AllDirectories))
            {
                var references = XDocument.Load(file)
                    .Descendants("ProjectReference")
                    .Select(e => Path.GetFileNameWithoutExtension(e.Attribute("Include")!.Value.Replace('\\', '/')))
                    .Select(Strip)
                    .ToHashSet(StringComparer.Ordinal);

                graph[Strip(Path.GetFileNameWithoutExtension(file))] = references;
            }
        }

        return graph;
    }

    private static string Strip(string projectName) =>
        projectName.StartsWith(Prefix, StringComparison.Ordinal) ? projectName[Prefix.Length..] : projectName;

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ObentoTensho.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Không tìm thấy ObentoTensho.sln");
    }
}
