namespace PlcMotorApi.Models;

public class Operation
{
    public int Id { get; set; }
    public string OperationId { get; set; } = string.Empty;
    public int Qty { get; set; }

    // Pending, Success, Error
    public string Status { get; set; } = "Pending";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
