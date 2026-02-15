using ClawSharp.Core.Memory;
using FluentAssertions;

namespace ClawSharp.Memory.Tests;

public class SqliteMemoryStoreTests : IAsyncDisposable
{
    private readonly SqliteMemoryStore _store;

    public SqliteMemoryStoreTests()
    {
        _store = new SqliteMemoryStore(":memory:");
    }

    [Fact]
    public async Task StoreAsync_NewKey_CreatesEntry()
    {
        await _store.StoreAsync("test-key", "test content", MemoryCategory.Core);
        var entry = await _store.GetAsync("test-key");
        entry.Should().NotBeNull();
        entry!.Content.Should().Be("test content");
        entry.Category.Should().Be(MemoryCategory.Core);
    }

    [Fact]
    public async Task StoreAsync_ExistingKey_UpdatesContent()
    {
        await _store.StoreAsync("key", "old");
        await _store.StoreAsync("key", "new");
        var entry = await _store.GetAsync("key");
        entry!.Content.Should().Be("new");
    }

    [Fact]
    public async Task StoreAsync_DefaultCategory_IsCore()
    {
        await _store.StoreAsync("key", "content");
        var entry = await _store.GetAsync("key");
        entry.Should().NotBeNull();
        entry!.Category.Should().Be(MemoryCategory.Core);
    }

    [Fact]
    public async Task DeleteAsync_ExistingKey_RemovesEntry()
    {
        await _store.StoreAsync("key", "content");
        var deleted = await _store.DeleteAsync("key");
        deleted.Should().BeTrue();
        var entry = await _store.GetAsync("key");
        entry.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_NonExistentKey_ReturnsFalse()
    {
        var deleted = await _store.DeleteAsync("nonexistent");
        deleted.Should().BeFalse();
    }

    [Fact]
    public async Task GetAsync_NonExistentKey_ReturnsNull()
    {
        var entry = await _store.GetAsync("nonexistent");
        entry.Should().BeNull();
    }

    [Fact]
    public async Task SearchAsync_FindsByContent()
    {
        await _store.StoreAsync("weather", "It's sunny in Tokyo today");
        await _store.StoreAsync("food", "I like sushi and ramen");
        var results = await _store.SearchAsync("Tokyo sunny");
        results.Should().ContainSingle(r => r.Key == "weather");
    }

    [Fact]
    public async Task SearchAsync_ReturnsResultsWithScore()
    {
        await _store.StoreAsync("weather", "It's sunny in Tokyo today");
        var results = await _store.SearchAsync("Tokyo sunny");
        results.Should().NotBeEmpty();
        results[0].Score.Should().NotBeNull();
    }

    [Fact]
    public async Task SearchAsync_NoResults_ReturnsEmptyList()
    {
        await _store.StoreAsync("test", "some content");
        var results = await _store.SearchAsync("completely unrelated query xyz");
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task ListAsync_FiltersByCategory()
    {
        await _store.StoreAsync("core1", "c1", MemoryCategory.Core);
        await _store.StoreAsync("daily1", "d1", MemoryCategory.Daily);
        var results = await _store.ListAsync(MemoryCategory.Core);
        results.Should().OnlyContain(r => r.Category == MemoryCategory.Core);
    }

    [Fact]
    public async Task ListAsync_WithoutFilter_ReturnsAll()
    {
        await _store.StoreAsync("core1", "c1", MemoryCategory.Core);
        await _store.StoreAsync("daily1", "d1", MemoryCategory.Daily);
        var results = await _store.ListAsync();
        results.Should().HaveCount(2);
    }

    [Fact]
    public async Task ListAsync_RespectsLimit()
    {
        for (int i = 0; i < 10; i++)
            await _store.StoreAsync($"key{i}", $"value{i}");
        var results = await _store.ListAsync(limit: 3);
        results.Should().HaveCount(3);
    }

    [Fact]
    public async Task ListAsync_OrdersByTimestamp()
    {
        await _store.StoreAsync("first", "first content");
        await Task.Delay(10); // ensure different timestamp
        await _store.StoreAsync("second", "second content");
        var results = await _store.ListAsync();
        results[0].Key.Should().Be("second");
        results[1].Key.Should().Be("first");
    }

    [Fact]
    public async Task CountAsync_ReturnsCorrectCount()
    {
        await _store.StoreAsync("k1", "v1");
        await _store.StoreAsync("k2", "v2");
        var count = await _store.CountAsync();
        count.Should().Be(2);
    }

    [Fact]
    public async Task CountAsync_AfterDelete_ReturnsReducedCount()
    {
        await _store.StoreAsync("k1", "v1");
        await _store.StoreAsync("k2", "v2");
        await _store.DeleteAsync("k1");
        var count = await _store.CountAsync();
        count.Should().Be(1);
    }

    [Fact]
    public async Task VectorSearchAsync_NotImplemented_ReturnsEmpty()
    {
        // Vector search is a stub for now
        await _store.StoreAsync("test", "test content");
        var results = await _store.VectorSearchAsync("test");
        results.Should().BeEmpty();
    }

    public async ValueTask DisposeAsync()
    {
        await _store.DisposeAsync();
    }
}
