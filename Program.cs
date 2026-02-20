using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;

// Set console window title
Console.Title = "Chat";

// Auto-configuration
const string serverUrl = "https://p2psignalserver.onrender.com/";
const int localPort = 6000;
var currentInput = "";

// Color preferences
var usernameColor = "green";
var messageColor = "white";
var commandColor = "darkgrey";
var errorColor = "red";

Console.Write("Enter your username: ");
var myId = Console.ReadLine()!.Trim();

if (string.IsNullOrWhiteSpace(myId))
{
    Console.WriteLine("Username cannot be empty.");
    return;
}

Console.Write("Enter your email: ");
var email = Console.ReadLine()!.Trim();

if (string.IsNullOrWhiteSpace(email))
{
    Console.WriteLine("Email cannot be empty.");
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
    var registerResponse = await http.PostAsJsonAsync("/register", new PeerInfo { PeerId = myId, Port = localPort, Password = password, Email = email });
    
    if (!registerResponse.IsSuccessStatusCode)
    {
        if (registerResponse.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            Console.WriteLine($"{GetColor(errorColor)("Incorrect password. Please try again.")}");
        }
        else if (registerResponse.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            var errorContent = await registerResponse.Content.ReadAsStringAsync();
            try
            {
                var errorJson = System.Text.Json.JsonDocument.Parse(errorContent);
                if (errorJson.RootElement.TryGetProperty("Error", out var errorMsg))
                {
                    Console.WriteLine($"{GetColor(errorColor)(errorMsg.GetString()!)}");
                }
                else
                {
                    Console.WriteLine($"{GetColor(errorColor)("Your account has been banned.")}");
                }
            }
            catch
            {
                Console.WriteLine($"{GetColor(errorColor)("Your account has been banned.")}");
            }
        }
        else if ((int)registerResponse.StatusCode == 429)
        {
            var errorContent = await registerResponse.Content.ReadAsStringAsync();
            try
            {
                var errorJson = System.Text.Json.JsonDocument.Parse(errorContent);
                if (errorJson.RootElement.TryGetProperty("Error", out var errorMsg))
                {
                    Console.WriteLine($"{GetColor(errorColor)(errorMsg.GetString()!)}");
                }
                else
                {
                    Console.WriteLine($"{GetColor(errorColor)("Too many account creations. Please try again later.")}");
                }
            }
            catch
            {
                Console.WriteLine($"{GetColor(errorColor)("Too many account creations. Please try again later.")}");
            }
        }
        else
        {
            Console.WriteLine($"{GetColor(errorColor)($"Failed to connect: {registerResponse.StatusCode}")}");
        }
        return;
    }

    // Parse color preferences from server
    var registerContent = await registerResponse.Content.ReadAsStringAsync();
    try
    {
        var registerJson = System.Text.Json.JsonDocument.Parse(registerContent);
        if (registerJson.RootElement.TryGetProperty("Colors", out var colors))
        {
            if (colors.TryGetProperty("Username", out var un)) usernameColor = un.GetString() ?? "green";
            if (colors.TryGetProperty("Message", out var msg)) messageColor = msg.GetString() ?? "white";
            if (colors.TryGetProperty("Command", out var cmd)) commandColor = cmd.GetString() ?? "darkgrey";
            if (colors.TryGetProperty("Error", out var err)) errorColor = err.GetString() ?? "red";
        }
    }
    catch { }

    Console.WriteLine($"{Blue($"Connected as {GetColor(usernameColor)(myId)}")}\n");

    // Start listening for server-relayed messages
    _ = Task.Run(async () =>
    {
        try
        {
            using var relayClient = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
            var stream = await relayClient.GetStreamAsync($"{serverUrl}messages/{myId}");
            using var reader = new StreamReader(stream);

            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (line?.StartsWith("data: ") == true)
                {
                    var message = line.Substring(6); // Remove "data: " prefix

                    // Check if this is a color update
                    if (message.StartsWith("COLOR_UPDATE:"))
                    {
                        try
                        {
                            var colorJson = message.Substring(13);
                            var colors = System.Text.Json.JsonDocument.Parse(colorJson);
                            if (colors.RootElement.TryGetProperty("Username", out var un)) usernameColor = un.GetString() ?? "green";
                            if (colors.RootElement.TryGetProperty("Message", out var msg)) messageColor = msg.GetString() ?? "white";
                            if (colors.RootElement.TryGetProperty("Command", out var cmd)) commandColor = cmd.GetString() ?? "darkgrey";
                            if (colors.RootElement.TryGetProperty("Error", out var err)) errorColor = err.GetString() ?? "red";
                        }
                        catch { }
                        continue;
                    }

                    // Check if this is a kick message
                    if (message.StartsWith("KICKED:"))
                    {
                        var kickReason = message.Substring(7);
                        Console.WriteLine($"\n{GetColor(errorColor)(kickReason)}");
                        Console.WriteLine($"{GetColor(errorColor)("Connection terminated. Press any key to exit...")}");
                        Environment.Exit(0);
                    }

                    // Clear current line, display message, redisplay prompt
                    var savedInput = currentInput;
                    var clearLine = "\r" + new string(' ', Math.Min(Console.WindowWidth - 1, savedInput.Length + 10)) + "\r";

                    Console.Write(clearLine);
                    var msgParts = message.Split(':', 2);
                    if (msgParts.Length == 2)
                    {
                        Console.WriteLine($"{GetColor(usernameColor)(msgParts[0])}: {GetColor(messageColor)(msgParts[1].TrimStart())}");
                    }
                    else
                    {
                        Console.WriteLine(message);
                    }
                    Console.Write($"{GetColor(commandColor)(">")} {savedInput}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n{Red($"Message relay error: {ex.Message}")}");
        }
    });
}
catch (Exception ex)
{
    Console.WriteLine($"{Red($"Failed to connect: {ex.Message}")}");
    return;
}






Console.WriteLine($"{GetColor(commandColor)("Type /help for commands")}");
Console.WriteLine();

while (true)
{
    Console.Write($"{GetColor(commandColor)(">")} ");
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
                Console.WriteLine($"{GetColor(commandColor)("[Usage: /say <message>]")}");
                continue;
            }

            // Send message to server for relay
            var broadcastReq = new BroadcastRequest { FromPeerId = myId, Message = body };
            await http.PostAsJsonAsync("/broadcast", broadcastReq);
            continue;
        }

        var cmdRequest = new CommandRequest { Command = command, Body = body, PeerId = myId };
        var response = await http.PostAsJsonAsync("/command", cmdRequest);
        
        if (response.IsSuccessStatusCode)
        {
            var cmdResponse = await response.Content.ReadFromJsonAsync<CommandResponse>();
            if (cmdResponse != null)
            {
                Console.WriteLine($"{GetColor(commandColor)(cmdResponse.Message)}");
            }
        }
        else
        {
            Console.WriteLine($"{GetColor(errorColor)($"Error: Server returned {response.StatusCode}")}");
        }
    }
    else
    {
        // Send message to server for relay
        var broadcastReq = new BroadcastRequest { FromPeerId = myId, Message = line };
        await http.PostAsJsonAsync("/broadcast", broadcastReq);
    }
}

// Color helpers
static string Green(string text) => $"\x1b[32m{text}\x1b[0m";
static string Blue(string text) => $"\x1b[34m{text}\x1b[0m";
static string DarkGrey(string text) => $"\x1b[90m{text}\x1b[0m";
static string Red(string text) => $"\x1b[31m{text}\x1b[0m";

static Func<string, string> GetColor(string colorName)
{
    return colorName.ToLower() switch
    {
        "black" => text => $"\x1b[30m{text}\x1b[0m",
        "red" => text => $"\x1b[31m{text}\x1b[0m",
        "green" => text => $"\x1b[32m{text}\x1b[0m",
        "yellow" => text => $"\x1b[33m{text}\x1b[0m",
        "blue" => text => $"\x1b[34m{text}\x1b[0m",
        "magenta" => text => $"\x1b[35m{text}\x1b[0m",
        "cyan" => text => $"\x1b[36m{text}\x1b[0m",
        "white" => text => $"\x1b[37m{text}\x1b[0m",
        "darkgrey" => text => $"\x1b[90m{text}\x1b[0m",
        _ => text => text
    };
}

public class PeerInfo
{
    public string PeerId { get; set; } = default!;
    public string? Ip { get; set; }
    public int Port { get; set; }
    public string? Password { get; set; }
    public string? Email { get; set; }
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
