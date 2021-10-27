# RadiantPi.Telnet - HelloKaleidescape

Connect to a Kaleidescape player.

## Code
```csharp
using System;
using System.IO;
using RadiantPi.Telnet;

// initialize client
using var client = new TelnetClient("192.168.1.147", 10000);

// register server connection validation
client.ValidateConnectionAsync = async (client, reader, writer) => {

    // TODO: replace with your player serial number
    const string playerSerialNumber = "123";

    // subscribe to events
    await writer.WriteLineAsync($"01/1/ENABLE_EVENTS:#{playerSerialNumber}:");
};

client.MessageReceived += delegate(object? sender, TelnetMessageReceivedEventArgs args) {
    Console.WriteLine($"Received: {args.Message}");
};

Console.WriteLine("Open connection");
await client.ConnectAsync();

Console.WriteLine("Press ENTER to exit.");
Console.ReadLine();
```

## Output

Response from Kaleidescape player after the connection is established and user interacts with player.

```
Open connection
Received: 01/1/000:/89
Press ENTER to exit.
Received: #021700001340/!/000:HIGHLIGHTED_SELECTION:26-0.0-S_c4432d36:/18
Received: #021700001340/!/000:HIGHLIGHTED_SELECTION:26-0.0-S_c442bd48:/68
Received: #021700001340/!/000:HIGHLIGHTED_SELECTION:26-0.0-S_c44c8eeb:/67
Received: #021700001340/!/000:HIGHLIGHTED_SELECTION:26-0.0-S_c446a231:/13
Received: #021700001340/!/000:HIGHLIGHTED_SELECTION:26-0.0-S_c44c8eeb:/67
Received: #021700001340/!/000:HIGHLIGHTED_SELECTION:26-0.0-S_c442bd48:/68
```