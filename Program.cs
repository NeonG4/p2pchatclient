using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;

Console.Write("Your peer ID (e.g. alice): ");
var myId = Console.ReadLine()!.Trim();

Console.Write("Peer to chat with (e.g. bob): ");
var otherId = Console.ReadLine()!.Trim();

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

// Get peer endpoint
Console.WriteLine("Waiting to resolve peer endpoint...");
PeerInfo? peerInfo = null;
while (peerInfo == null)
{
    var resp = await http.GetAsync($"/peer/{otherId}");
    if (resp.IsSuccessStatusCode)
    {
        peerInfo = await resp.Content.ReadFromJsonAsync<PeerInfo>();
        break;
    }
    Console.WriteLine("Peer not registered yet, retrying in 2s...");
    await Task.Delay(2000);
}

Console.WriteLine($"Peer {otherId} at {peerInfo!.Ip}:{peerInfo.Port}");
var peerEndpoint = new IPEndPoint(IPAddress.Parse(peerInfo.Ip!), peerInfo.Port);

// Start receive loop
_ = Task.Run(async () =>
{
    while (true)
    {
        var result = await udp.ReceiveAsync();
        var msg = System.Text.Encoding.UTF8.GetString(result.Buffer);
        Console.WriteLine($"\n[{result.RemoteEndPoint}] {msg}");
        Console.Write("> ");
    }
});

// Simple “poke” to open NAT mapping
var helloBytes = System.Text.Encoding.UTF8.GetBytes($"HELLO from {myId}");
await udp.SendAsync(helloBytes, helloBytes.Length, peerEndpoint);

Console.WriteLine("Type messages and press Enter to send.");
while (true)
{
    Console.Write("> ");
    var line = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(line))
        continue;

    var bytes = System.Text.Encoding.UTF8.GetBytes(line);
    await udp.SendAsync(bytes, bytes.Length, peerEndpoint);
}

public class PeerInfo
{
    public string PeerId { get; set; } = default!;
    public string? Ip { get; set; }
    public int Port { get; set; }
}