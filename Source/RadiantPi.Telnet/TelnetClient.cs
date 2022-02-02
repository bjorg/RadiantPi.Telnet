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

using System.IO;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace RadiantPi.Telnet {
    using Timer = System.Timers.Timer;

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
        private readonly Timer _reconnectTimer;
        private CancellationTokenSource? _internalCancellation;
        private TcpClient? _tcpClient;
        private StreamWriter? _streamWriter;
        private bool _disposed = false;

        //--- Constructors ---
        public TelnetClient(string host, int port, ILogger<TelnetClient>? logger = null) {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _port = port;
            Logger = logger;

            // initialize reconnection timer
            _reconnectTimer = new(TimeSpan.FromSeconds(15).TotalMilliseconds);
            _reconnectTimer.Elapsed += OnCheckConnectionTimer;
        }

        //--- Events ---
        public event EventHandler<TelnetMessageReceivedEventArgs>? MessageReceived;

        //--- Properties ---
        public TelnetConnectionHandshakeAsync? ValidateConnectionAsync { get; set; }
        private ILogger? Logger { get; }
        private StreamWriter StreamWriter => _streamWriter ?? throw new InvalidOperationException("Connection is not connected");

        //--- Methods ---
        public async Task ConnectAsync() {
            if(_disposed) {
                throw new ObjectDisposedException(nameof(TelnetClient));
            }

            // attempt to connect
            Logger?.LogInformation($"Connecting to telnet socket [{_host}:{_port}]");
            await ReconnectAsync().ConfigureAwait(false);

            // enable reconneciton timer
            _reconnectTimer.Enabled = true;
        }

        public async Task SendAsync(string message) {
            if(_disposed) {
                throw new ObjectDisposedException(nameof(TelnetClient));
            }

            // ensure the client is connected
            if(!(_tcpClient?.Connected ?? false)) {
                throw new InvalidOperationException("Client is not connected");
            }

            // Send command
            Logger?.LogTrace($"Sending [{_host}:{_port}]: '{Escape(message)}'");
            await StreamWriter.WriteLineAsync(message).ConfigureAwait(false);
        }

        public void Disconnect() {
            if(_disposed) {
                throw new ObjectDisposedException(nameof(TelnetClient));
            }
            Logger?.LogInformation($"Disconnecting telnet socket [{_host}:{_port}]");
            ResetConnectionState();
        }

        public void Dispose() {

            // NOTE (2022-02-01, bjorg): the `Dispose()` method must be idempotent
            if(_disposed) {
                return;
            }

            // dispose object indicating `Dispose()` was called explicitly
            Dispose(disposing: true);
        }

        private async Task WaitForMessages(
            TcpClient tcpClient,
            StreamReader streamReader,
            CancellationTokenSource cancellationToken
        ) {
            try {
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
                            Logger?.LogTrace($"Received [{_host}:{_port}]: '{Escape(message)}'");
                            MessageReceived?.Invoke(this, new(message));
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
                streamReader.Close();
            }
        }

        private void Dispose(bool disposing) {

            // has this instance been disposed before
            if(_disposed) {
                return;
            }
            _disposed = true;

            // close connection and release resources
            if(disposing) {
                Disconnect();
                _reconnectTimer.Dispose();
            }
        }

        private void OnCheckConnectionTimer(object? source, System.Timers.ElapsedEventArgs args) {

            // check timer to minimize race condition between a reconnection attempt and an intentional disconnection
            if(!_reconnectTimer.Enabled) {
                return;
            }

            // attempt to connect again
            try {
                ReconnectAsync().GetAwaiter().GetResult();
            } catch(Exception e) {
                Logger?.LogWarning(e, "Reconnect attempt failed");
            }
        }

        private async Task ReconnectAsync() {

            // check if TCP client is already connected
            if(_tcpClient?.Connected ?? false) {
                return;
            }

            // reset all connection resources
            ResetConnectionState();

            // initialize a new TCP client
            TcpClient tcpClient;
            try {
                tcpClient = new();
                await tcpClient.ConnectAsync(_host, _port).ConfigureAwait(false);
            } catch(SocketException) {

                // unable to connect to telnet server
                return;
            } catch(Exception e) {
                Logger?.LogError(e, "Unable to connect");
                return;
            }
            _tcpClient = tcpClient;

            // initialize reader/writer streams
            StreamReader streamReader = new(_tcpClient.GetStream());
            _streamWriter = new(_tcpClient.GetStream()) {
                AutoFlush = true
            };

            // validate new connection
            if(ValidateConnectionAsync != null) {
                try {
                    await ValidateConnectionAsync(this, streamReader, _streamWriter).ConfigureAwait(false);
                } catch(Exception e) {
                    Logger?.LogError(e, "Unable to validate connection");
                    ResetConnectionState();
                    throw;
                }
            }

            // wait for messages to arrive
            _internalCancellation = new();
            _ = WaitForMessages(
                _tcpClient,
                streamReader,
                _internalCancellation
            );
        }

        private void ResetConnectionState() {

            // disable timer to prevent reconnections from happening
            _reconnectTimer.Enabled = false;

            // cancel socket read operations
            try {
                _internalCancellation?.Cancel();
                _internalCancellation = null;
            } catch(Exception e) {
                Logger?.LogWarning(e, "Error while resetting cancellation token");
            }

            // close write stream
            try {
                _streamWriter?.Close();
                _streamWriter = null;
            } catch(Exception e) {
                Logger?.LogWarning(e, "Error while closing write stream");
            }

            // close TCP client
            try {
                _tcpClient?.Close();
                _tcpClient?.Dispose();
                _tcpClient = null;
            } catch(Exception e) {
                Logger?.LogWarning(e, "Error while disposing TCP client");
            }
        }
   }
}