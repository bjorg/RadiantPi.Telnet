# RadiantPi.Telnet

`TelnetClient` simplifies working with Telnet connections. The library is platform agnostic and works on Windows or Linux, including on a Raspberry Pi.

Run the `dotnet` command from your project folder to add the `RadiantPi.Telnet` assembly:
```
dotnet add package RadiantPi.Telnet
```

Find a description of the latest changes in the [release notes](ReleaseNotes.md).

## Sample: Hello Trinnov

Use `TelnetClient` to connect to an Trinnov Altitude processor.

```csharp
using System;
using RadiantPi.Telnet;

// initialize client
using TelnetClient client = new("192.168.1.180", 44100);

// register server connection validation
client.ValidateConnectionAsync = async (client, reader, writer) => {
    var handshake = await reader.ReadLineAsync() ?? "";

    // the Trinnov Altitude sends a welcome text to identify itself
    if(!handshake.StartsWith("Welcome on Trinnov Optimizer (", StringComparison.Ordinal)) {
        throw new NotSupportedException("Unrecognized device");
    }

    // announce client
    await writer.WriteLineAsync("id radiant_pi_telnet");
};

client.MessageReceived += delegate (object? sender, TelnetMessageReceivedEventArgs args) {
    Console.WriteLine($"Received: {args.Message}");
};

Console.WriteLine("Open connection");
await client.ConnectAsync();

Console.WriteLine("Press ENTER to exit.");
Console.ReadLine();
```

## License

This application is distributed under the GNU Affero General Public License v3.0 or later.

Copyright (C) 2020-2023 - Steve G. Bjorg