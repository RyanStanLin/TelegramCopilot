using System.Runtime.InteropServices;

namespace TelegramCopilot.Infrastructure.Telegram;

internal static partial class TdNative
{
    [LibraryImport("tdjson", EntryPoint = "td_create_client_id")]
    internal static partial int Create();
    [LibraryImport("tdjson", EntryPoint = "td_send", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial void Send(int clientId, string request);
    [LibraryImport("tdjson", EntryPoint = "td_receive")]
    internal static partial nint Receive(double timeout);
    [LibraryImport("tdjson", EntryPoint = "td_execute", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial nint Execute(string request);
}
