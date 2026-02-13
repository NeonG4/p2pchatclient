using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;

Console.Write("Your peer ID (e.g. alice): ");
var myId = Console.ReadLine()!.Trim();

Console.Write("Signaling server URL (default http://localhost:5000): ");
var serverUrl = Console.ReadLine();
if (string.IsNullOrWhiteSpace(serverUrl))
    serverUrl = "http://localhost:5000";

Console.Write("Local UDP port to listen on (e.g. 6000): ");
var portStr = Console.ReadLine();
var localPort = int.Parse(portStr!);

// Start UDP listener
var udp = new UdpClient(localPort);
Console.WriteLine($"Listening on UDP {localPort}...");

// Register with signaling server
var http = new HttpClient { BaseAddress = new Uri(serverUrl) };
await http.PostAsJsonAsync("/register", new PeerInfo { PeerId = myId, Port = localPort });

Console.WriteLine($"Registered as {myId}. Connected to group chat!");

// Start receive loop
_ = Task.Run(async () =>
{
    while (true)
    {
        var result = await udp.ReceiveAsync();
        var msg = System.Text.Encoding.UTF8.GetString(result.Buffer);
        Console.WriteLine($"\n[UDP: {result.RemoteEndPoint}] {msg}");
        Console.Write("> ");
    }
});





Console.WriteLine("Group chat ready! Type messages to broadcast, or use commands:");
Console.WriteLine("  /help - Show server commands");
Console.WriteLine("  /list - List all users");
Console.WriteLine("  /quit - Exit chat");
while (true)
{
    Console.Write("> ");
    var line = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(line))
        continue;

    if (line.Equals("/quit", StringComparison.OrdinalIgnoreCase))
    {
        await http.PostAsJsonAsync("/disconnect", new DisconnectRequest { PeerId = myId });
        Console.WriteLine("Disconnected from chat.");
        break;
    }

    if (line.StartsWith('/'))
    {
        var parts = line.Split(' ', 2);
        var command = parts[0];
        var body = parts.Length > 1 ? parts[1] : "";

        var cmdRequest = new CommandRequest { Command = command, Body = body, PeerId = myId };
        var response = await http.PostAsJsonAsync("/command", cmdRequest);
        
        if (response.IsSuccessStatusCode)
        {
            var cmdResponse = await response.Content.ReadFromJsonAsync<CommandResponse>();
            if (cmdResponse != null)
            {
                Console.WriteLine($"[Server{(string.IsNullOrEmpty(cmdResponse.Person) ? "" : $"/{cmdResponse.Person}")}] {cmdResponse.Message}");
            }
        }
        else
        {
            Console.WriteLine($"[Error] Server returned {response.StatusCode}");
        }
    }
    else
    {
        // Broadcast to all peers
        var broadcastReq = new BroadcastRequest { FromPeerId = myId, Message = line };
        var broadcastResp = await http.PostAsJsonAsync("/broadcast", broadcastReq);
        
        if (broadcastResp.IsSuccessStatusCode)
        {
            var result = await broadcastResp.Content.ReadFromJsonAsync<BroadcastResponse>();
            if (result != null && result.Recipients.Count > 0)
            {
                var messageBytes = System.Text.Encoding.UTF8.GetBytes($"[{myId}] {line}");
                foreach (var peer in result.Recipients)
                {
                    var peerEndpoint = new IPEndPoint(IPAddress.Parse(peer.Ip!), peer.Port);
                    await udp.SendAsync(messageBytes, messageBytes.Length, peerEndpoint);
                }
                Console.WriteLine($"[Sent to {result.Recipients.Count} user(s)]");
            }
            else
            {
                Console.WriteLine("[No other users online]");
            }
        }
    }
}

public class PeerInfo
{
    public string PeerId { get; set; } = default!;
    public string? Ip { get; set; }
    public int Port { get; set; }
}

public class CommandRequest
{
    public string Command { get; set; } = default!;
    public string Body { get; set; } = default!;
    public string PeerId { get; set; } = default!;
}

public class CommandResponse
{
    public string Message { get; set; } = default!;
    public string? Person { get; set; }
}

public class BroadcastRequest
{
    public string FromPeerId { get; set; } = default!;
    public string Message { get; set; } = default!;
}

public class BroadcastResponse
{
    public List<PeerInfo> Recipients { get; set; } = new();
}

public class DisconnectRequest
{
    public string PeerId { get; set; } = default!;
}
