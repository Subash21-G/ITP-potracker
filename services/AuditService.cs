using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using POTracker.Data;
using POTracker.Models;

namespace POTracker.Services
{
    public interface IAuditService
    {
        Task LogAsync(
            string action,
            int? materialId = null,
            string? materialNo = null,
            string? poNumber = null,
            string? processStatus = null,
            string? dispatchStatus = null,
            string? owner = null,
            string? details = null,
            CancellationToken cancellationToken = default);
    }

    public sealed class AuditService : IAuditService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<AuditService> _logger;

        public AuditService(
            ApplicationDbContext context,
            IHttpContextAccessor httpContextAccessor,
            ILogger<AuditService> logger)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public async Task LogAsync(
            string action,
            int? materialId = null,
            string? materialNo = null,
            string? poNumber = null,
            string? processStatus = null,
            string? dispatchStatus = null,
            string? owner = null,
            string? details = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var userName =
                    _httpContextAccessor.HttpContext?.Session.GetString("UserName") ??
                    _httpContextAccessor.HttpContext?.User?.Identity?.Name ??
                    "System";

                var audit = new AuditLog
                {
                    Action = action,
                    MaterialId = materialId,
                    MaterialNo = materialNo,
                    PONumber = poNumber,
                    ProcessStatus = processStatus,
                    DispatchStatus = dispatchStatus,
                    Owner = owner,
                    UserName = userName,
                    Details = details,
                    CreatedDate = DateTime.Now
                };

                _context.AuditLogs.Add(audit);
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to persist audit log entry: {Action}", action);
            }
        }
    }
}

