using Arona.Datas.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Arona.Services;

public sealed class GroupBlacklistService
{
    private const string ConfigKey = "GroupBlacklist";
    private readonly IServiceProvider _services;
    private readonly ILogger<GroupBlacklistService> _logger;
    private readonly HashSet<long> _blacklist = [];
    private readonly object _lock = new();

    public GroupBlacklistService(IServiceProvider services, ILogger<GroupBlacklistService> logger)
    {
        _services = services;
        _logger = logger;
    }

    public void Load()
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MainDbContext>();
        var cfg = db.Configs.Find(ConfigKey);
        var ids = ParseList(cfg?.Value);
        lock (_lock)
        {
            _blacklist.Clear();
            _blacklist.UnionWith(ids);
        }
        _logger.LogInformation("加载群黑名单：{Count} 个群", ids.Length);
    }

    public bool IsBlacklisted(long groupId)
    {
        lock (_lock)
            return _blacklist.Contains(groupId);
    }

    public long[] GetAll()
    {
        lock (_lock)
            return _blacklist.ToArray();
    }

    public async Task<bool> AddAsync(long groupId, CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (!_blacklist.Add(groupId))
                return false;
        }
        await SaveAsync(ct);
        _logger.LogInformation("群 {GroupId} 已加入黑名单", groupId);
        return true;
    }

    public async Task<bool> RemoveAsync(long groupId, CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (!_blacklist.Remove(groupId))
                return false;
        }
        await SaveAsync(ct);
        _logger.LogInformation("群 {GroupId} 已移出黑名单", groupId);
        return true;
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MainDbContext>();
        var cfg = await db.Configs.FindAsync([ConfigKey], ct);
        var value = string.Join(",", GetAll());
        if (cfg is null)
            db.Configs.Add(new StringConfig { Id = ConfigKey, Value = value });
        else
            cfg.Value = value;
        await db.SaveChangesAsync(ct);
    }

    private static long[] ParseList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return [];
        return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => long.TryParse(s, out var id) ? id : -1)
            .Where(id => id > 0)
            .Distinct()
            .ToArray();
    }
}