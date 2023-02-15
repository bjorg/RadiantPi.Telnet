/*
 * RadiantPi.Telnet - Client for Telnet protocol
 * Copyright (C) 2020-2023 - Steve G. Bjorg
 *
 * This program is free software: you can redistribute it and/or modify it
 * under the terms of the GNU Affero General Public License as published by the
 * Free Software Foundation, either version 3 of the License, or (at your option)
 * any later version.
 *
 * This program is distributed in the hope that it will be useful, but WITHOUT
 * ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS
 * FOR A PARTICULAR PURPOSE. See the GNU Affero General Public License for more
 * details.
 *
 * You should have received a copy of the GNU Affero General Public License along
 * with this program. If not, see <https://www.gnu.org/licenses/>.
 */

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

// hook-up event handler
client.MessageReceived += delegate (object? sender, TelnetMessageReceivedEventArgs args) {
    Console.WriteLine($"Received: {args.Message}");
};

// connect to device
Console.WriteLine("Open connection");
await client.ConnectAsync();

Console.WriteLine("Press ENTER to exit.");
Console.ReadLine();