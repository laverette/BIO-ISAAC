using BioShieldLens.Data;
using BioShieldLens.Models;
using Microsoft.EntityFrameworkCore;

namespace BioShieldLens.Services;

public class AuditLogService : IAuditLogService
{
    private readonly BioShieldDbContext _context;
    private readonly ILogger<AuditLogService> _logger;

    public AuditLogService(BioShieldDbContext context, ILogger<AuditLogService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task LogActionAsync(string action, string? entityType = null, int? entityId = null, string? details = null, string performedBy = "System")
    {
        try
        {
            var log = new AuditLog
            {
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                Details = details,
                PerformedBy = performedBy,
                Timestamp = DateTime.UtcNow
            };

            _context.AuditLogs.Add(log);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log audit action: {Action}", action);
        }
    }

    public async Task<List<AuditLog>> GetRecentLogsAsync(int count = 100)
    {
        return await _context.AuditLogs
            .OrderByDescending(l => l.Timestamp)
            .Take(count)
            .ToListAsync();
    }

    public async Task<List<AuditLog>> GetLogsByEntityAsync(string entityType, int entityId)
    {
        return await _context.AuditLogs
            .Where(l => l.EntityType == entityType && l.EntityId == entityId)
            .OrderByDescending(l => l.Timestamp)
            .ToListAsync();
    }
}

