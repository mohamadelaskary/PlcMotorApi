using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlcMotorApi.Data;
using PlcMotorApi.Models;
using PlcMotorApi.Services;

namespace PlcMotorApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OperationsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IPlcService _plc;
    private readonly ISseNotifier _sse;
    private readonly PlcMonitorService _monitor;

    public OperationsController(AppDbContext db, IPlcService plc, ISseNotifier sse, PlcMonitorService monitor)
    {
        _db = db;
        _plc = plc;
        _sse = sse;
        _monitor = monitor;
    }

    // الموبايل بيفتح الاتصال ده مرة واحدة ويسيبه مفتوح، وأي تحديث حالة
    // (Success/Error/motorStatus) هيوصله فورًا من غير ما يعمل أي طلب تاني.
    [HttpGet("stream")]
    public async Task Stream()
    {
        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");

        var (clientId, reader) = _sse.Subscribe();
        var token = HttpContext.RequestAborted;

        try
        {
            await Response.WriteAsync(":connected\n\n", token);

            // نبعت حالة الموتور الحالية فورًا للعميل الجديد، عشان يعرف
            // الوضع من أول لحظة يفتح فيها الاتصال، مش يستنى تغيير يحصل.
            string initial =
                $"{{\"type\":\"motorStatus\",\"isRunning\":{(_monitor.IsMotorRunning ? "true" : "false")}}}";
            await Response.WriteAsync($"data: {initial}\n\n", token);
            await Response.Body.FlushAsync(token);

            await foreach (var message in reader.ReadAllAsync(token))
            {
                await Response.WriteAsync($"data: {message}\n\n", token);
                await Response.Body.FlushAsync(token);
            }
        }
        catch (OperationCanceledException)
        {
            // الموبايل قفل الاتصال أو الأبلكيشن اتقفل - ده طبيعي ومش خطأ
        }
        finally
        {
            _sse.Unsubscribe(clientId);
        }
    }

    public record CreateOperationRequest(int Qty);

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOperationRequest request)
    {
        if (request.Qty <= 0)
            return BadRequest(new { message = "qty لازم يكون أكبر من صفر" });

        // تأكيد لحظي من الـ PLC نفسه (مش بس من القيمة المخزّنة) قبل السماح ببدء عملية جديدة
        var (active, _, _) = await _plc.ReadFullStatusAsync();
        if (active)
        {
            return Conflict(new
            {
                message = "الموتور يعمل حاليًا بالفعل، يرجى الانتظار حتى انتهاء العملية الجارية."
            });
        }

        var operation = new Operation
        {
            OperationId = Guid.NewGuid().ToString("N"),
            Qty = request.Qty,
            Status = "Pending"
        };

        _db.Operations.Add(operation);
        await _db.SaveChangesAsync();

        try
        {
            await _plc.StartOperationAsync(request.Qty);
        }
        catch (Exception ex)
        {
            operation.Status = "Error";
            await _db.SaveChangesAsync();
            return StatusCode(502, new { message = "فشل الاتصال بالـ PLC", detail = ex.Message });
        }

        return Ok(new { message = "بدأت العملية، انتظر الإشعار", operationId = operation.OperationId });
    }

    [HttpGet("{operationId}/status")]
    public async Task<IActionResult> GetStatus(string operationId)
    {
        var operation = await _db.Operations.FirstOrDefaultAsync(o => o.OperationId == operationId);
        if (operation is null)
            return NotFound(new { message = "operationId مش موجود" });

        return Ok(new { operation.OperationId, operation.Status });
    }
}
