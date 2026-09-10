using Sharp7;

namespace PlcMotorApi.Services;

// DB_Operation layout expected in TIA Portal (DB number set below).
// Declare the tags in exactly this order so TIA packs them this way:
// Offset 0.0 : Qty     (Int, 2 bytes)
// Offset 2.0 : Active  (Bool - bit 0 of byte 2)
// Offset 2.1 : Success (Bool - bit 1 of byte 2)
// Offset 2.2 : Error   (Bool - bit 2 of byte 2)
public class PlcService : IPlcService, IDisposable
{
    private readonly S7Client _client = new();
    private readonly object _lock = new();
    private readonly string _ip;
    private readonly int _rack;
    private readonly int _slot;
    private readonly int _dbNumber;

    public PlcService(IConfiguration config)
    {
        _ip = config["Plc:IpAddress"] ?? "192.168.0.1";
        _rack = int.Parse(config["Plc:Rack"] ?? "0");
        _slot = int.Parse(config["Plc:Slot"] ?? "1");
        _dbNumber = int.Parse(config["Plc:DbNumber"] ?? "1");
    }

    private void EnsureConnected()
    {
        if (_client.Connected) return;

        int result = _client.ConnectTo(_ip, _rack, _slot);
        if (result != 0)
            throw new InvalidOperationException($"فشل الاتصال بالـ PLC: {_client.ErrorText(result)}");
    }

    public Task StartOperationAsync(int qty)
    {
        lock (_lock)
        {
            EnsureConnected();

            byte[] qtyBuffer = new byte[2];
            // Sharp7 expects a short (Int16) for SetIntAt
            S7.SetIntAt(qtyBuffer, 0, (short)qty);
            int rc = _client.DBWrite(_dbNumber, 0, 2, qtyBuffer);
            if (rc != 0)
                throw new InvalidOperationException($"فشل كتابة Qty: {_client.ErrorText(rc)}");

            byte[] statusByte = new byte[1];
            // SetBitAt(buffer, byteIndex, bit, value)
            S7.SetBitAt(ref statusByte, 0, 0, true); // bit 0 = Active
            rc = _client.DBWrite(_dbNumber, 2, 1, statusByte);
            if (rc != 0)
                throw new InvalidOperationException($"فشل كتابة Active: {_client.ErrorText(rc)}");
        }

        return Task.CompletedTask;
    }

    public Task<(bool active, bool success, bool error)> ReadFullStatusAsync()
    {
        lock (_lock)
        {
            EnsureConnected();

            byte[] buffer = new byte[1];
            int rc = _client.DBRead(_dbNumber, 2, 1, buffer);
            if (rc != 0)
                throw new InvalidOperationException($"فشل قراءة الحالة: {_client.ErrorText(rc)}");

            // GetBitAt(buffer, byteIndex, bit)
            bool active = S7.GetBitAt(buffer, 0, 0);
            bool success = S7.GetBitAt(buffer, 0, 1);
            bool error = S7.GetBitAt(buffer, 0, 2);
            return Task.FromResult((active, success, error));
        }
    }

    public Task ClearFlagsAsync()
    {
        lock (_lock)
        {
            EnsureConnected();
            byte[] clear = new byte[1];
            int rc = _client.DBWrite(_dbNumber, 2, 1, clear);
            if (rc != 0)
                throw new InvalidOperationException($"فشل تصفير الحالة: {_client.ErrorText(rc)}");
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_client.Connected)
            _client.Disconnect();
    }
}
