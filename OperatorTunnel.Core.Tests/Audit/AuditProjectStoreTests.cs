using OperatorTunnel.Audit;

namespace OperatorTunnel.Core.Tests.Audit;

public sealed class AuditProjectStoreTests
{
    [Fact]
    public async Task SaveAndList_RoundTripsProjectMetadata()
    {
        var path = CreateTemporaryPath();
        try
        {
            var store = new JsonAuditProjectStore(path);
            var project = AuditProject.Create("Internal Review", "10.10.20.0/24", DateTimeOffset.UnixEpoch);

            await store.SaveAsync(project);
            var restored = await store.ListAsync();

            var item = Assert.Single(restored);
            Assert.Equal(project.Id, item.Id);
            Assert.Equal("Internal Review", item.Name);
            Assert.Equal("10.10.20.0/24", item.Scope);
        }
        finally
        {
            DeleteTemporaryStore(path);
        }
    }

    [Fact]
    public async Task SaveWithExistingId_UpdatesWithoutDuplicating()
    {
        var path = CreateTemporaryPath();
        try
        {
            var store = new JsonAuditProjectStore(path);
            var project = AuditProject.Create("Initial", "scope");

            await store.SaveAsync(project);
            await store.SaveAsync(project with { Name = "Updated" });

            var item = Assert.Single(await store.ListAsync());
            Assert.Equal("Updated", item.Name);
        }
        finally
        {
            DeleteTemporaryStore(path);
        }
    }

    [Fact]
    public async Task Delete_RemovesOnlyRequestedProject()
    {
        var path = CreateTemporaryPath();
        try
        {
            var store = new JsonAuditProjectStore(path);
            var first = AuditProject.Create("First", "scope");
            var second = AuditProject.Create("Second", "scope");
            await store.SaveAsync(first);
            await store.SaveAsync(second);

            await store.DeleteAsync(first.Id);

            var remaining = await store.ListAsync();
            Assert.Equal([second.Id], remaining.Select(item => item.Id));
        }
        finally
        {
            DeleteTemporaryStore(path);
        }
    }

    [Fact]
    public async Task InvalidHeader_FailsClosed()
    {
        var directory = Directory.CreateTempSubdirectory("operator-audit-store-");
        var path = Path.Combine(directory.FullName, "projects.json");
        try
        {
            await File.WriteAllTextAsync(path, "not-a-project-store\n{}");

            await Assert.ThrowsAsync<InvalidDataException>(() => new JsonAuditProjectStore(path).ListAsync());
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private static string CreateTemporaryPath()
    {
        var directory = Directory.CreateTempSubdirectory("operator-audit-store-");
        return Path.Combine(directory.FullName, "projects.json");
    }

    private static void DeleteTemporaryStore(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (directory is not null && Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
    }
}
