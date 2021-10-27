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
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace RadiantPi.Telnet {

    public sealed class TelnetClient : ITelnet {

        //--- Class Methods ---
        private static string Escape(string text)
            => string.Join("", text.Select(c => c switch {
                >= (char)32 and < (char)127 => ((char)c).ToString(),
                '\n' => "\\n",
                '\r' => "\\r",
                _ => $"\\u{(int)c:X4}"
            }));


        //--- Fields ---
        private readonly int _port;
        private readonly string _host;
        private CancellationTokenSource? _internalCancellation;
        private TcpClient? _tcpClient;
        private StreamWriter? _streamWriter;
        private bool _disposed = false;
        private bool _sendReady;

        //--- Constructors ---
        public TelnetClient(string host, int port, ILogger<TelnetClient>? logger = null) {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _port = port;
            Logger = logger;
        }

        //--- Events ---
        public event EventHandler<TelnetMessageReceivedEventArgs>? MessageReceived;

        //--- Properties ---
        public TelnetConnectionHandshakeAsync? ConfirmConnectionAsync { get; set; }
        private ILogger? Logger { get; }
        private StreamWriter StreamWriter => _streamWriter ?? throw new InvalidOperationException("Connection is not open");

        //--- Methods ---
        public async Task SendAsync(string message) {
            if(_disposed) {
                throw new ObjectDisposedException("TelnetClient");
            }

            // open connection
            await ConnectAsync().ConfigureAwait(false);
            if(!_sendReady) {
                throw new InvalidOperationException("Client is not ready to send messages");
            }

            // Send command + params
            Logger?.LogTrace($"Sending: '{Escape(message)}'");
            await StreamWriter.WriteLineAsync(message).ConfigureAwait(false);
        }

        public void Disconnect() {
            if(_tcpClient == null) {

                // nothing to do
                return;
            }
            Logger?.LogInformation($"Disconnecting telnet socket");

            // cancel socket read operations
            try {
                _internalCancellation?.Cancel();
                _internalCancellation = null;
            } catch {

                // nothing to do
            }

            // close write stream
            try {
                _streamWriter?.Close();
                _streamWriter = null;
            } catch {

                // nothing to do
            }

            // close socket
            try {
                _tcpClient?.Close();
                _tcpClient = null;
            } catch {

                // nothing to do
            }
        }

        public async Task<bool> ConnectAsync() {

            // check if socket is already connected
            if(_tcpClient?.Connected ?? false) {
                return false;
            }
            Logger?.LogInformation($"Connecting telnet socket");

            // cancel any previous listener
            _internalCancellation?.Cancel();
            _internalCancellation = new();

            // initialize a new client
            _tcpClient = new TcpClient();
            await _tcpClient.ConnectAsync(_host, _port).ConfigureAwait(false);

            // initialize reader/writer streams
            _streamWriter = new(_tcpClient.GetStream()) {
                AutoFlush = true
            };

            // notify that a connection is opening
            StreamReader streamReader = new(_tcpClient.GetStream());
            if(ConfirmConnectionAsync != null) {
                await ConfirmConnectionAsync(this, streamReader, _streamWriter).ConfigureAwait(false);
            }

            // wait for messages to arrive
            _ = WaitForMessages(
                _tcpClient,
                streamReader,
                _internalCancellation
            );
            return true;
        }

        public void Dispose() => Dispose(true);

        private async Task WaitForMessages(
            TcpClient tcpClient,
            StreamReader streamReader,
            CancellationTokenSource cancellationToken
        ) {
            try {
                _sendReady = true;
                while(true) {

                    // check if cancelation token is set
                    if(cancellationToken.IsCancellationRequested) {

                        // operation was canceled
                        break;
                    }

                    // attempt to read from socket
                    try {
                        if(!tcpClient.Connected) {

                            // client is no longer connected
                            break;
                        }
                        var message = await streamReader.ReadLineAsync().ConfigureAwait(false);
                        if(message == null) {

                            // found end of stream
                            break;
                        }

                        // ignore empty messages
                        if(!string.IsNullOrWhiteSpace(message)) {
                            Logger?.LogTrace($"Received: '{Escape(message)}'");
                            MessageReceived?.Invoke(this, new TelnetMessageReceivedEventArgs(message));
                        }
                    } catch(ObjectDisposedException) {

                        // nothing to do: underlying stream was closed and disposed
                        break;
                    } catch(IOException) {

                        // nothing to do: underlying stream was disconnected
                        break;
                    } catch {

                        // TODO: add mechanism for reporting asynchronous exceptions
                        break;
                    }
                }
            } finally {
                _sendReady = false;
                streamReader.Close();
            }
        }

        private void Dispose(bool disposing) {
            if(_disposed) {
                return;
            }
            if(disposing) {
                Disconnect();
            }
            _disposed = true;
        }
    }
}