namespace PlcMotorApi.Services;

public interface ISseNotifier
{
    (Guid ClientId, System.Threading.Channels.ChannelReader<string> Reader) Subscribe();
    void Unsubscribe(Guid clientId);
    Task BroadcastAsync(string message);
}
