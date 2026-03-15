using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Hpoll.Core.Configuration;
using Hpoll.Data;

namespace Hpoll.Admin.Services;

public class SendTimeDisplayService
{
    private readonly HpollDbContext _db;
    private readonly EmailSettings _emailSettings;

    public SendTimeDisplayService(HpollDbContext db, IOptions<EmailSettings> emailSettings)
    {
        _db = db;
        _emailSettings = emailSettings.Value;
    }

    public async Task<List<string>> GetEffectiveDefaultSendTimesUtcAsync()
    {
        var entry = await _db.SystemInfo
            .FirstOrDefaultAsync(e => e.Key == "email.send_times_utc");
        if (entry != null && !string.IsNullOrWhiteSpace(entry.Value))
        {
            var parsed = entry.Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(t => TimeSpan.TryParse(t, out _))
                .ToList();
            if (parsed.Count > 0)
                return parsed;
        }
        return _emailSettings.SendTimesUtc;
    }

    public async Task<string> GetDefaultSendTimesDisplayAsync()
    {
        var defaults = await GetEffectiveDefaultSendTimesUtcAsync();
        return string.Join(", ", defaults) + " UTC";
    }
}
