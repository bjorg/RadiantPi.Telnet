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

namespace RadiantPi.Telnet;

using System.Linq;
using System.IO;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

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
    private readonly SemaphoreSlim _mutex = new SemaphoreSlim(1, 1);
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
    public bool Connected => _tcpClient?.Connected ?? false;
    public bool AutoReconnect { get; set; } = true;
    private ILogger? Logger { get; }
    private StreamWriter StreamWriter => _streamWriter ?? throw new InvalidOperationException("Connection is not connected");

    //--- Methods ---
    public async Task ConnectAsync() {
        if(_disposed) {
            throw new ObjectDisposedException(nameof(TelnetClient));
        }

        // attempt to connect
        await ReconnectAsync().ConfigureAwait(false);
    }

    public async Task SendAsync(string message) {
        if(_disposed) {
            throw new ObjectDisposedException(nameof(TelnetClient));
        }

        // ensure the client is connected
        if(!Connected) {
            throw new InvalidOperationException("Client is not connected");
        }

        // Send command
        await _mutex.WaitAsync().ConfigureAwait(false);
        try {
            Logger?.LogTrace($"Sending [{_host}:{_port}]: '{Escape(message)}'");
            await StreamWriter.WriteLineAsync(message).ConfigureAwait(false);
        } finally {
            _mutex.Release();
        }
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
                    if(message is null) {

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

            // close read stream
            try {
                streamReader.Close();
            } catch { }

            // close TCP client
            try {
                tcpClient?.Close();
            } catch { }
        }
    }

    private void Dispose(bool disposing) {

        // has this instance been disposed before
        if(_disposed) {
            return;
        }

        // close connection and release resources
        if(disposing) {
            Disconnect();
            _reconnectTimer.Dispose();
        }
        _disposed = true;
    }

    private async void OnCheckConnectionTimer(object? source, System.Timers.ElapsedEventArgs args) {

        // check timer to minimize race condition between a reconnection attempt and an intentional disconnection
        if(!_reconnectTimer.Enabled) {
            return;
        }

        // check if connection is still open
        var connected = false;
        var streamWriter = _streamWriter;
        if(streamWriter is not null) {
            try {

                // send heartbeat to telnet server
                await _mutex.WaitAsync().ConfigureAwait(false);
                try {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                    await streamWriter.WriteLineAsync(new char[0], cts.Token).ConfigureAwait(false);
                    connected = true;
                } finally {
                    _mutex.Release();
                }
                Logger?.LogTrace($"HeartBeat [{_host}:{_port}]: SUCCESS");
            } catch(TaskCanceledException) {

                // heartbeat timeout
                Logger?.LogTrace($"HeartBeat [{_host}:{_port}]: TaskCanceledException");
            } catch(OperationCanceledException) {

                // heartbeat timeout
                Logger?.LogTrace($"HeartBeat [{_host}:{_port}]: OperationCanceledException");
            } catch(SocketException) {
                Logger?.LogTrace($"HeartBeat [{_host}:{_port}]: SocketException");
            } catch(ObjectDisposedException) {

                // stream has been closed
                Logger?.LogTrace($"HeartBeat [{_host}:{_port}]: ObjectDisposedException");
            } catch(Exception e) {
                Logger?.LogError(e, $"HeartBeat [{_host}:{_port}]: Unable to send hearhbeat");
            }
        }
        if(!connected) {
            try {
                await ReconnectAsync().ConfigureAwait(false);
            } catch(Exception e) {
                Logger?.LogError(e, $"HeartBeat [{_host}:{_port}]: Unable to reconnect");
            }
        }
    }

    private async Task ReconnectAsync() {

        // reset all connection resources
        ResetConnectionState();

        // enable reconnection timer
        _reconnectTimer.Enabled = AutoReconnect;

        // initialize a new TCP client
        TcpClient tcpClient;
        try {
            tcpClient = new();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            Logger?.LogInformation($"Connecting to telnet socket [{_host}:{_port}]");
            await tcpClient.ConnectAsync(_host, _port, cts.Token).ConfigureAwait(false);
            Logger?.LogTrace($"Connecting [{_host}:{_port}]: SUCCESS");
        } catch(TaskCanceledException) {

            // connection timeout
            Logger?.LogTrace($"Connecting [{_host}:{_port}]: TaskCanceledException");
            return;
        } catch(OperationCanceledException) {

            // connection timeout
            Logger?.LogTrace($"Connecting [{_host}:{_port}]: OperationCanceledException");
            return;
        } catch(SocketException) {
            Logger?.LogTrace($"Connecting [{_host}:{_port}]: SocketException");

            // unable to connect to telnet server
            return;
        } catch(Exception e) {
            Logger?.LogError(e, $"Connecting [{_host}:{_port}]: Unable to connect");
            return;
        }
        _tcpClient = tcpClient;

        // initialize reader/writer streams
        StreamReader streamReader = new(_tcpClient.GetStream());
        _streamWriter = new(_tcpClient.GetStream()) {
            AutoFlush = true
        };

        // validate new connection
        if(ValidateConnectionAsync is not null) {
            try {
                await ValidateConnectionAsync(this, streamReader, _streamWriter).ConfigureAwait(false);
            } catch(Exception e) {
                Logger?.LogError(e, $"Connecting [{_host}:{_port}]: Unable to validate connection");
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
            Logger?.LogWarning(e, $"Reseting [{_host}:{_port}]: Error while resetting cancellation token");
        }

        // close write stream
        try {
            _streamWriter?.Close();
            _streamWriter = null;
        } catch(Exception e) {
            Logger?.LogWarning(e, $"Reseting [{_host}:{_port}]: Error while closing write stream");
        }

        // close TCP client
        try {
            _tcpClient?.Close();
            _tcpClient?.Dispose();
            _tcpClient = null;
        } catch(Exception e) {
            Logger?.LogWarning(e, $"Reseting [{_host}:{_port}]: Error while disposing TCP client");
        }
    }
}
