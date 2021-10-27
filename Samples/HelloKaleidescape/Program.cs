using System;
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