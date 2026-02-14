using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;

// Auto-configuration
const string serverUrl = "https://p2psignalserver.onrender.com/";
var localPort = 6000;
var currentInput = "";
var udp = new UdpClient(localPort);
var udpLock = new object();

Console.Write("Enter your username: ");
var myId = Console.ReadLine()!.Trim();

if (string.IsNullOrWhiteSpace(myId))
{
    Console.WriteLine("Username cannot be empty.");
    return;
}

Console.WriteLine($"\n{DarkGrey("Connecting to chat server...")}\n");

// Register with signaling server
var http = new HttpClient { BaseAddress = new Uri(serverUrl) };
try
{
    await http.PostAsJsonAsync("/register", new PeerInfo { PeerId = myId, Port = localPort });
    Console.WriteLine($"{Blue($"Connected as {Green(myId)}")}\n");
}
catch (Exception ex)
{
    Console.WriteLine($"{Red($"Failed to connect: {ex.Message}")}");
    return;
}

// Start receive loop
_ = Task.Run(async () =>
{
    while (true)
    {
        try
        {
            UdpClient currentUdp;
            lock (udpLock)
            {
                currentUdp = udp;
            }
            
            var result = await currentUdp.ReceiveAsync();
            var msg = System.Text.Encoding.UTF8.GetString(result.Buffer);
            
            // Save cursor position, clear current line
            var savedInput = currentInput;
            var clearLine = "\r" + new string(' ', Math.Min(Console.WindowWidth - 1, savedInput.Length + 10)) + "\r";
            
            // Display the incoming message
            Console.Write(clearLine);
            Console.WriteLine(msg);
            
            // Restore prompt and current input
            Console.Write($"{DarkGrey(">")} {savedInput}");
        }
        catch (ObjectDisposedException)
        {
            // UDP client was disposed during port change, wait a bit and continue
            await Task.Delay(100);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n{Red($"Error receiving message: {ex.Message}")}");
        }
    }
});





Console.WriteLine($"{DarkGrey("Type /help for commands")}");
Console.WriteLine();

while (true)
{
    Console.Write($"{DarkGrey(">")} ");
    currentInput = "";
    
    // Read input character by character to track current input
    while (true)
    {
        var key = Console.ReadKey(intercept: true);
        
        if (key.Key == ConsoleKey.Enter)
        {
            Console.WriteLine();
            break;
        }
        else if (key.Key == ConsoleKey.Backspace)
        {
            if (currentInput.Length > 0)
            {
                currentInput = currentInput[..^1];
                Console.Write("\b \b");
            }
        }
        else if (!char.IsControl(key.KeyChar))
        {
            currentInput += key.KeyChar;
            Console.Write(key.KeyChar);
        }
    }
    
    var line = currentInput;
    
    if (string.IsNullOrWhiteSpace(line))
        continue;

    if (line.Equals("/quit", StringComparison.OrdinalIgnoreCase))
    {
        await http.PostAsJsonAsync("/disconnect", new DisconnectRequest { PeerId = myId });
        Console.WriteLine($"\n{Blue("Disconnected from chat.")}\n");
        break;
    }

    if (line.StartsWith('/'))
    {
        var parts = line.Split(' ', 2);
        var command = parts[0];
        var body = parts.Length > 1 ? parts[1] : "";

        // Handle /say command locally (send as regular message)
        if (command.Equals("/say", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                Console.WriteLine($"{DarkGrey("[Usage: /say <message>]")}");
                continue;
            }
            
            // Treat as regular message
            var broadcastReq = new BroadcastRequest { FromPeerId = myId, Message = body };
            var broadcastResp = await http.PostAsJsonAsync("/broadcast", broadcastReq);
            
            if (broadcastResp.IsSuccessStatusCode)
            {
                var result = await broadcastResp.Content.ReadFromJsonAsync<BroadcastResponse>();
                if (result != null && result.Recipients.Count > 0)
                {
                    var messageBytes = System.Text.Encoding.UTF8.GetBytes($"{Green(myId)}: {body}");
                    
                    UdpClient currentUdp;
                    lock (udpLock)
                    {
                        currentUdp = udp;
                    }
                    
                    foreach (var peer in result.Recipients)
                    {
                        try
                        {
                            var peerEndpoint = new IPEndPoint(IPAddress.Parse(peer.Ip!), peer.Port);
                            await currentUdp.SendAsync(messageBytes, messageBytes.Length, peerEndpoint);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"{Red($"Failed to send to {peer.PeerId}: {ex.Message}")}");
                        }
                    }
                }
            }
            continue;
        }

        var cmdRequest = new CommandRequest { Command = command, Body = body, PeerId = myId };
        var response = await http.PostAsJsonAsync("/command", cmdRequest);
        
        if (response.IsSuccessStatusCode)
        {
            var cmdResponse = await response.Content.ReadFromJsonAsync<CommandResponse>();
            if (cmdResponse != null)
            {
                Console.WriteLine($"{DarkGrey(cmdResponse.Message)}");
                
                // Handle port change
                if (command.Equals("/ipconfig", StringComparison.OrdinalIgnoreCase) && 
                    body.StartsWith("setport", StringComparison.OrdinalIgnoreCase))
                {
                    var portParts = body.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                    if (portParts.Length >= 2 && int.TryParse(portParts[1], out var newPort))
                    {
                        try
                        {
                            // Create new UDP client on new port
                            var newUdp = new UdpClient(newPort);
                            
                            // Swap the UDP client
                            UdpClient oldUdp;
                            lock (udpLock)
                            {
                                oldUdp = udp;
                                udp = newUdp;
                                localPort = newPort;
                            }
                            
                            // Dispose old UDP client
                            oldUdp.Close();
                            oldUdp.Dispose();
                            
                            Console.WriteLine($"{DarkGrey($"Now listening on port {newPort}")}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"{Red($"Failed to change port: {ex.Message}")}");
                        }
                    }
                }
            }
        }
        else
        {
            Console.WriteLine($"{Red($"Error: Server returned {response.StatusCode}")}");
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
                var messageBytes = System.Text.Encoding.UTF8.GetBytes($"{Green(myId)}: {line}");
                
                UdpClient currentUdp;
                lock (udpLock)
                {
                    currentUdp = udp;
                }
                
                foreach (var peer in result.Recipients)
                {
                    try
                    {
                        var peerEndpoint = new IPEndPoint(IPAddress.Parse(peer.Ip!), peer.Port);
                        await currentUdp.SendAsync(messageBytes, messageBytes.Length, peerEndpoint);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"{Red($"Failed to send to {peer.PeerId}: {ex.Message}")}");
                    }
                }
            }
        }
    }
}

// Color helpers
static string Green(string text) => $"\x1b[32m{text}\x1b[0m";
static string Blue(string text) => $"\x1b[34m{text}\x1b[0m";
static string DarkGrey(string text) => $"\x1b[90m{text}\x1b[0m";
static string Red(string text) => $"\x1b[31m{text}\x1b[0m";

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
