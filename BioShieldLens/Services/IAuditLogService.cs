using BioShieldLens.Models;

namespace BioShieldLens.Services;

public interface IAuditLogService
{
    Task LogActionAsync(string action, string? entityType = null, int? entityId = null, string? details = null, string performedBy = "System");
    Task<List<AuditLog>> GetRecentLogsAsync(int count = 100);
    Task<List<AuditLog>> GetLogsByEntityAsync(string entityType, int entityId);
}

