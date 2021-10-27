using System;
using System.IO;
using RadiantPi.Telnet;

// initialize client
using var client = new TelnetClient("192.168.1.180", 44100);

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

client.MessageReceived += delegate(object? sender, TelnetMessageReceivedEventArgs args) {
    Console.WriteLine($"Received: {args.Message}");
};

Console.WriteLine("Open connection");
await client.ConnectAsync();

Console.WriteLine("Press ENTER to exit.");
Console.ReadLine();