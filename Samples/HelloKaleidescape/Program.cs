/*
 * RadiantPi.Telnet - Client for Telnet protocol
 * Copyright (C) 2020-2021 - Steve G. Bjorg
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
using Spectre.Console;

// check if there is an environment variable for Kaleidescape player serial number or prompt for it
var deviceId = Environment.GetEnvironmentVariable("KPLAYER_SERIAL_NUMBER");
if(string.IsNullOrEmpty(deviceId)) {
    deviceId = AnsiConsole.Ask<string>("Enter Kaleidescape Player Serial Number:");
}

// initialize client
using TelnetClient client = new("192.168.1.147", 10000);

// register server connection validation
client.ValidateConnectionAsync = async (client, reader, writer) => {

    // subscribe to events
    await writer.WriteLineAsync($"01/1/ENABLE_EVENTS:#{deviceId}:");
};

// hook-up event handler
client.MessageReceived += delegate (object? sender, TelnetMessageReceivedEventArgs args) {
    Console.WriteLine($"Received: {args.Message}");
};

// connect to device
Console.WriteLine("Open connection");
await client.ConnectAsync();

// wait for exit
Console.WriteLine("Press ENTER to exit.");
Console.ReadLine();