using System.Collections.Concurrent;
using System.Threading.Channels;

namespace PlcMotorApi.Services;

// بيحتفظ بقايمة كل الموبايلات المتصلة حاليًا (كل واحد له قناة/Channel خاصة بيه)
// ولما تحصل عملية Broadcast، بنكتب نفس الرسالة لكل القنوات مرة واحدة.
public class SseNotifier : ISseNotifier
{
    private readonly ConcurrentDictionary<Guid, Channel<string>> _clients = new();

    public (Guid ClientId, ChannelReader<string> Reader) Subscribe()
    {
        var id = Guid.NewGuid();
        var channel = Channel.CreateUnbounded<string>();
        _clients[id] = channel;
        return (id, channel.Reader);
    }

    public void Unsubscribe(Guid clientId)
    {
        if (_clients.TryRemove(clientId, out var channel))
            channel.Writer.TryComplete();
    }

    public async Task BroadcastAsync(string message)
    {
        foreach (var channel in _clients.Values)
        {
            await channel.Writer.WriteAsync(message);
        }
    }
}
