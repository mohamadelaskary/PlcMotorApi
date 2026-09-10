using Microsoft.EntityFrameworkCore;
using PlcMotorApi.Data;

namespace PlcMotorApi.Services;

public class PlcMonitorService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IPlcService _plc;
    private readonly ISseNotifier _sse;
    private readonly ILogger<PlcMonitorService> _logger;
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);

    // آخر حالة معروفة للموتور، بيقرأها أي كنترولر تاني (زي عند فتح اتصال SSE جديد)
    // من غير ما يحتاج يعمل رحلة لقراءة الـ PLC بنفسه.
    public bool IsMotorRunning { get; private set; }

    public PlcMonitorService(
        IServiceScopeFactory scopeFactory,
        IPlcService plc,
        ISseNotifier sse,
        ILogger<PlcMonitorService> logger)
    {
        _scopeFactory = scopeFactory;
        _plc = plc;
        _sse = sse;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var (active, success, error) = await _plc.ReadFullStatusAsync();

                // لو حالة الموتور اتغيرت عن آخر مرة، ابعت تحديث لكل الأجهزة المتصلة فورًا
                if (active != IsMotorRunning)
                {
                    IsMotorRunning = active;
                    await _sse.BroadcastAsync(
                        $"{{\"type\":\"motorStatus\",\"isRunning\":{(active ? "true" : "false")}}}");
                }

                if (success || error)
                {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    var pendingOps = await db.Operations
                        .Where(o => o.Status == "Pending")
                        .ToListAsync(stoppingToken);

                    if (pendingOps.Count > 0)
                    {
                        string newStatus = success ? "Success" : "Error";
                        foreach (var op in pendingOps)
                            op.Status = newStatus;

                        await db.SaveChangesAsync(stoppingToken);

                        _logger.LogInformation(
                            "تم تحديث {Count} عملية إلى {Status} تلقائيًا",
                            pendingOps.Count, newStatus);

                        foreach (var op in pendingOps)
                        {
                            string json =
                                $"{{\"type\":\"operationStatus\",\"operationId\":\"{op.OperationId}\",\"status\":\"{newStatus}\"}}";
                            await _sse.BroadcastAsync(json);
                        }
                    }

                    await _plc.ClearFlagsAsync();
                }
            }
            catch (Exception ex)
            {
                // ما نوقفش الخدمة عشان خطأ عابر (زي انقطاع الشبكة لحظيًا)
                _logger.LogError(ex, "خطأ أثناء مراقبة الـ PLC");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }
}
