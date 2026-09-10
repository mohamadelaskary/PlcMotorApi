namespace PlcMotorApi.Services;

public interface IPlcService
{
    Task StartOperationAsync(int qty);
    Task<(bool active, bool success, bool error)> ReadFullStatusAsync();
    Task ClearFlagsAsync();
}
