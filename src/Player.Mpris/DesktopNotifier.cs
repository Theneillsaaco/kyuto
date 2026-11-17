using Player.Core;
using Tmds.DBus.Protocol;

namespace Player.Mpris;

/// <summary>Desktop notifications over the session bus. Never throws: a missing daemon only means no notification.</summary>
public sealed class DesktopNotifier : IDisposable
{
    public async Task NotifyAsync(string summary, string body, string imagePath = "")
    {
        await _gate.WaitAsync();
        try
        {
            if (_connection is null)
            {
                var address = DBusAddress.Session ?? throw new InvalidOperationException("no session bus");
                var connection = new DBusConnection(address);
                await connection.ConnectAsync();
                _connection = connection;
            }

            Dictionary<string, VariantValue> hints = [];
            if (imagePath.Length > 0)
                hints["image-path"] = imagePath;
            
            MessageBuffer messageBuffer;
            using (var writer = _connection.GetMessageWriter())
            {
                writer.WriteMethodCallHeader(Service, Path, Service, "Notify", "susssasa{sv}i");
                writer.WriteString("Player");
                writer.WriteUInt32(_lastId); // replaces the previous one
                writer.WriteString("io.github.Theneillsaaco.MediaPlayer");
                writer.WriteString(summary);
                writer.WriteString(body);
                writer.WriteArray(Array.Empty<string>());
                writer.WriteDictionary(hints);
                writer.WriteInt32(5000);

                messageBuffer = writer.CreateMessage();
            }
            
            _lastId = await _connection.CallMethodAsync(
                messageBuffer,
                static (Message message, object? _) => message.GetBodyReader().ReadUInt32(),
                null
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEBUG NOTIFY EXCEPTION]: {ex}");
            Log.Warn("notify", ex.Message);
            _connection?.Dispose();
            _connection = null;
        }
        finally
        {
            _gate.Release();
        }
    }
    
    public void Dispose()
    {
        _connection?.Dispose();
        _connection = null;
    }

    #region Fields

    private const string Service = "org.freedesktop.Notifications";
    private const string Path = "/org/freedesktop/Notifications";
    
    private readonly SemaphoreSlim _gate = new(1,1);
    private DBusConnection? _connection;
    private uint _lastId;

    #endregion
}