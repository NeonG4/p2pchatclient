# P2P UDP Group Chat Client

A clean, colorful terminal-based group chat client with UDP peer-to-peer messaging.

## Features

? **Streamlined Experience**
- Just enter your username and start chatting
- Auto-connects to `https://p2psignalserver.onrender.com/`
- No need to configure ports or IPs

?? **Smart Message Handling**
- Messages won't interrupt what you're typing
- Your input is preserved when receiving messages
- Clean message display without technical details

?? **Color-Coded Messages**
- **Green** - Usernames
- **Blue** - Server messages
- **Dark Grey** - Commands and prompts
- **White** - Message text

## Quick Start

```bash
dotnet run
```

Then just enter your username and start chatting!

## Commands

- `/help` - Show available commands
- `/list` - List all connected users
- `/quit` - Exit the chat

## Usage Example

```
Enter your username: alice
Connecting to chat server...
Connected as alice

Available commands: /help, /list, /quit

> Hello everyone!
> /list
[Server] Connected UDP users (3): alice, bob, charlie
> /quit

Disconnected from chat.
```

## How It Works

1. **Connect**: Client registers with the signaling server
2. **Send**: Messages are broadcast to all connected users via UDP
3. **Receive**: UDP listener displays incoming messages in real-time
4. **Commands**: Special commands starting with `/` are sent to the server

## Technical Details

- **Protocol**: UDP for peer-to-peer messaging, HTTP for server commands
- **Port**: Auto-configured to use port 6000
- **Server**: Connects to production server at `p2psignalserver.onrender.com`
- **Colors**: ANSI escape codes for terminal colors

## Notes

- Requires .NET 10
- Works on Windows, macOS, and Linux terminals that support ANSI colors
- All messages are sent directly peer-to-peer after server coordination
- Server only facilitates peer discovery and commands
