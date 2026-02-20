# Color Customization Feature

## ✅ **FEATURE COMPLETE!**

Users can now fully customize the colors of their chat interface with the `/color` command.

---

## **Features**

### **Customizable Elements**
- **Username** - Color of usernames in chat messages
- **Message** - Color of message text
- **Command** - Color of command prompts and system messages  
- **Error** - Color of error messages and warnings

### **Available Colors**
- `black`
- `red`
- `green`
- `yellow`
- `blue`
- `magenta`
- `cyan`
- `white`
- `darkgrey`

### **Default Colors**
- Username: `green`
- Message: `white`
- Command: `darkgrey`
- Error: `red`

---

## **Usage Examples**

### **View Current Colors**
```
> /color
Current colors:
Username: green
Message: white
Command: darkgrey
Error: red

Usage: /color <element> <color>
Elements: username, message, command, error
Colors: black, red, green, yellow, blue, magenta, cyan, white, darkgrey
```

### **Change Username Color**
```
> /color username cyan
Changed username color to cyan.
```

Now all usernames appear in cyan!

### **Change Message Color**
```
> /color message yellow
Changed message color to yellow.
```

Now all messages appear in yellow!

### **Change Command Prompt Color**
```
> /color command blue
Changed command color to blue.
```

Now the `>` prompt and system messages appear in blue!

### **Change Error Color**
```
> /color error magenta
Changed error color to magenta.
```

Now error messages appear in magenta!

---

## **Technical Implementation**

### **Server Side**

#### **ColorPreferences Class**
```csharp
class ColorPreferences
{
    public string Username { get; set; } = "green";
    public string Message { get; set; } = "white";
    public string Command { get; set; } = "darkgrey";
    public string Error { get; set; } = "red";
}
```

#### **User Model Update**
```csharp
class User
{
    // ... existing properties
    public ColorPreferences Colors { get; set; } = new();
}
```

#### **Registration Response**
```csharp
return Results.Ok(new { 
    Username = user.Username, 
    MessageCount = user.MessageCount, 
    Colors = user.Colors  // Send colors to client
});
```

#### **/color Command Handler**
```csharp
case "/color":
    if (string.IsNullOrWhiteSpace(body))
    {
        // Show current colors
        return new CommandResponse { Message = "Current colors: ..." };
    }

    var colorParts = body.Split(' ', 2);
    var element = colorParts[0].ToLower();  // username, message, command, error
    var color = colorParts[1].ToLower();     // red, green, blue, etc.

    // Validate color
    var validColors = new[] { "black", "red", "green", ... };
    if (!validColors.Contains(color)) { ... }

    // Update user's color preference
    switch (element)
    {
        case "username": colorUser.Colors.Username = color; break;
        case "message": colorUser.Colors.Message = color; break;
        case "command": colorUser.Colors.Command = color; break;
        case "error": colorUser.Colors.Error = color; break;
    }

    // Notify client of color change
    if (messageQueues.TryGetValue(peerId, out var queue))
    {
        queue.Enqueue($"COLOR_UPDATE:{JsonSerializer.Serialize(colorUser.Colors)}");
    }
```

---

### **Client Side**

#### **Color Storage**
```csharp
// Color preferences
var usernameColor = "green";
var messageColor = "white";
var commandColor = "darkgrey";
var errorColor = "red";
```

#### **Load Colors on Registration**
```csharp
var registerJson = JsonDocument.Parse(registerContent);
if (registerJson.RootElement.TryGetProperty("Colors", out var colors))
{
    if (colors.TryGetProperty("Username", out var un)) usernameColor = un.GetString() ?? "green";
    if (colors.TryGetProperty("Message", out var msg)) messageColor = msg.GetString() ?? "white";
    if (colors.TryGetProperty("Command", out var cmd)) commandColor = cmd.GetString() ?? "darkgrey";
    if (colors.TryGetProperty("Error", out var err)) errorColor = err.GetString() ?? "red";
}
```

#### **Handle COLOR_UPDATE Messages**
```csharp
if (message.StartsWith("COLOR_UPDATE:"))
{
    var colorJson = message.Substring(13);
    var colors = JsonDocument.Parse(colorJson);
    if (colors.RootElement.TryGetProperty("Username", out var un)) usernameColor = un.GetString() ?? "green";
    if (colors.RootElement.TryGetProperty("Message", out var msg)) messageColor = msg.GetString() ?? "white";
    if (colors.RootElement.TryGetProperty("Command", out var cmd)) commandColor = cmd.GetString() ?? "darkgrey";
    if (colors.RootElement.TryGetProperty("Error", out var err)) errorColor = err.GetString() ?? "red";
}
```

#### **Dynamic Color Function**
```csharp
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
```

#### **Apply Colors Dynamically**
```csharp
// Username
Console.WriteLine($"{GetColor(usernameColor)(username)}: ...");

// Message
Console.WriteLine($"... {GetColor(messageColor)(messageText)}");

// Command prompt
Console.Write($"{GetColor(commandColor)(">")} ");

// Error
Console.WriteLine($"{GetColor(errorColor)("Error: ...")}");
```

---

## **Real-Time Updates**

When you change a color with `/color`, the change takes effect **immediately**:

1. Server updates your color preferences
2. Server sends `COLOR_UPDATE` message via SSE
3. Client receives update and changes colors
4. Next message uses new colors automatically!

**No restart required!** ✨

---

## **Persistence**

- ✅ **Stored per user** - Each user has their own color preferences
- ✅ **Survives logout** - Colors persist when you reconnect (in-memory, resets on server restart)
- ✅ **Independent** - Your color choices don't affect other users

---

## **ANSI Color Codes**

The client uses ANSI escape sequences for terminal colors:

| Color      | Code      | ANSI Sequence |
|------------|-----------|---------------|
| Black      | `30`      | `\x1b[30m`    |
| Red        | `31`      | `\x1b[31m`    |
| Green      | `32`      | `\x1b[32m`    |
| Yellow     | `33`      | `\x1b[33m`    |
| Blue       | `34`      | `\x1b[34m`    |
| Magenta    | `35`      | `\x1b[35m`    |
| Cyan       | `36`      | `\x1b[36m`    |
| White      | `37`      | `\x1b[37m`    |
| Dark Grey  | `90`      | `\x1b[90m`    |
| Reset      | `0`       | `\x1b[0m`     |

---

## **Complete Workflow**

### **First Login**
```
Enter your username: alice
Enter your email: alice@example.com
Enter your password: ******

Connected as alice    <- green (default)

Type /help for commands    <- darkgrey (default)

> Hello!    <- darkgrey prompt (default)
alice: Hello!    <- green username, white message (defaults)
```

### **Customize Colors**
```
> /color username cyan
Changed username color to cyan.

> /color message yellow  
Changed message color to yellow.

> /color command blue
Changed command color to blue.

> Hello again!    <- blue prompt
alice: Hello again!    <- cyan username, yellow message
```

### **Next Login**
```
Enter your username: alice
Enter your email: alice@example.com  
Enter your password: ******

Connected as alice    <- cyan (remembered!)

Type /help for commands    <- blue (remembered!)

> Welcome back!    <- blue prompt
alice: Welcome back!    <- cyan username, yellow message
```

Your colors are automatically loaded! 🎨

---

## **Error Handling**

### **Invalid Color**
```
> /color username purple
Invalid color. Valid colors: black, red, green, yellow, blue, magenta, cyan, white, darkgrey
```

### **Invalid Element**
```
> /color background red
Invalid element. Valid elements: username, message, command, error
```

### **Missing Parameters**
```
> /color username
Usage: /color <element> <color>
```

---

## **Benefits**

✅ **Personalization** - Make the chat look exactly how you want  
✅ **Accessibility** - Choose colors that work best for your vision  
✅ **Fun** - Express yourself with unique color schemes  
✅ **Persistent** - Colors saved with your account  
✅ **Real-time** - Changes apply immediately  
✅ **Easy** - Simple command syntax  
✅ **Safe** - Validated input, can't break anything  

---

## **Popular Color Schemes**

### **Matrix Theme**
```
/color username green
/color message green
/color command green
/color error red
```

### **Ocean Theme**
```
/color username cyan
/color message blue
/color command darkgrey
/color error magenta
```

### **Fire Theme**
```
/color username yellow
/color message red
/color command red
/color error magenta
```

### **Monochrome**
```
/color username white
/color message white
/color command darkgrey
/color error white
```

---

## **Updated Command List**

| Command | Description |
|---------|-------------|
| `/help` | Show available commands |
| `/list` | List all connected users |
| `/count` | Show number of users |
| `/stats [username]` | Show user statistics |
| `/color <element> <color>` | **NEW** - Customize colors |
| `/say <message>` | Send message to all users |
| `/quit` | Exit the chat |

**Admin commands:**
- `/kick <username>`
- `/ban <username>`
- `/unban <username>`
- `/promote <username>`
- `/demote <username>`
- `/adminlist`

---

## **Server Logging**

```
[COLOR] alice changed username color to cyan
[COLOR] alice changed message color to yellow
[COLOR] bob changed command color to blue
```

All color changes are logged on the server for debugging and auditing.

---

**Both projects build successfully!** 🎉  
**Color customization is fully functional and ready to use!** 🎨
