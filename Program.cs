using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;

// Set console window title
Console.Title = "Chat";

// Auto-configuration
const string serverUrl = "https://p2psignalserver.onrender.com/";
const int localPort = 6000;
var currentInput = "";
var udp = new UdpClient(localPort);

Console.Write("Enter your username: ");
var myId = Console.ReadLine()!.Trim();

if (string.IsNullOrWhiteSpace(myId))
{
    Console.WriteLine("Username cannot be empty.");
    return;
}

Console.Write("Enter your password: ");
var password = "";
{
    ConsoleKeyInfo key;
    do
    {
        key = Console.ReadKey(intercept: true);
        
        if (key.Key == ConsoleKey.Backspace && password.Length > 0)
        {
            password = password[..^1];
            Console.Write("\b \b");
        }
        else if (!char.IsControl(key.KeyChar))
        {
            password += key.KeyChar;
            Console.Write("*");
        }
    } while (key.Key != ConsoleKey.Enter);
}
Console.WriteLine();

if (string.IsNullOrWhiteSpace(password))
{
    Console.WriteLine("Password cannot be empty.");
    return;
}

Console.WriteLine($"\n{DarkGrey("Connecting to chat server...")}\n");

// Register with signaling server
var http = new HttpClient { BaseAddress = new Uri(serverUrl) };
try
{
    var registerResponse = await http.PostAsJsonAsync("/register", new PeerInfo { PeerId = myId, Port = localPort, Password = password });
    
    if (!registerResponse.IsSuccessStatusCode)
    {
        if (registerResponse.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            Console.WriteLine($"{Red("Incorrect password. Please try again.")}");
        }
        else
        {
            Console.WriteLine($"{Red($"Failed to connect: {registerResponse.StatusCode}")}");
        }
        return;
    }
    
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
            var result = await udp.ReceiveAsync();
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
                    
                    foreach (var peer in result.Recipients)
                    {
                        try
                        {
                            var peerEndpoint = new IPEndPoint(IPAddress.Parse(peer.Ip!), peer.Port);
                            await udp.SendAsync(messageBytes, messageBytes.Length, peerEndpoint);
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
                
                foreach (var peer in result.Recipients)
                {
                    try
                    {
                        var peerEndpoint = new IPEndPoint(IPAddress.Parse(peer.Ip!), peer.Port);
                        await udp.SendAsync(messageBytes, messageBytes.Length, peerEndpoint);
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
    public string? Password { get; set; }
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
