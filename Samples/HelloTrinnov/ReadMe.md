# RadiantPi.Telnet - HelloTrinnov

Connect to a Trinnov Altitude.

## Code
```csharp
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
```

## Output

Response from Trinnov Altitude after the connection is established.

```
Open connection
Press ENTER to exit.
Received: OK
Received: SOURCES_CHANGED
Received: OPTSOURCE 0 Source 1
Received: OK
...
Received: LABELS_CLEAR
Received: LABEL 0: Builtin
Received: LABEL 1: Settings 48 (flat)
Received: LABEL 2: Settings 48 (calibrated 2021-03-14)
Received: LABEL 3: Settings 48 (Cineramax)
Received: LABEL 4: Settings 48 (3D Remapping)
Received: LABEL 5: Settings 48 (2021-08-21) - Pre-calibration
Received: LABEL 6: Settings 48 (2021-09-09) - WIP (Calibrated)
Received: LABEL 7: Settings 48 (2021-09-07) - Music (Calibrated)
Received: LABEL 8: Settings 48 (Hybrid+EQ+Full Left+Right)
Received: LABEL 9: Settings 48 (2021-08-21) - Calibrated
Received: LABEL 10: Settings 48 (2021-10-14) - Experimental (Calibrated)
Received: LABEL 11: Settings 48 (2021-10-14) - Wide Stage (Calibrated)
Received: LABEL 12: Settings 48 (2021-10-14) - DTS:X (Calibrated)
Received: OK
Received: SRATE 48000
Received: AUDIOSYNC_STATUS 1
Received: DECODER NONAUDIO 0 PLAYABLE 1 DECODER PCM UPMIXER none
Received: AUDIOSYNC Slave
```