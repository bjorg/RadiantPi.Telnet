# Release Notes

## v2.2 (2022-02-16)

### Features

* Added `AutoReconnect` property to control if Telnet client is using a hearbeat to automatically reconnect to the server.


## v2.1.1 (2022-02-16)

### Fixes

* Declare `Connected` on `ITelnet` interface.


## v2.1 (2022-02-16)

### Features

* Added `Connected` property to check if the Telnet client is currently connected.
* Upgraded `Microsoft.Extensions.Logging` package to 6.0.0

### Fixes

* Added mutex to avoid a race-condition between sending a command and sending a heartbeat.
* Close TCP client when `WaitForMessages()` exits.


## v2.0.1 (2022-02-15)

### Fixes

* Fixed an issue where reconnecting on lost network connection would not work as expected.
* Always show host IP and port in log messages.


## v2.0 (2022-02-15)

### BREAKING CHANGES

* Changed target framework to .NET 6.0.
* Changed `ITelnet.ConnectAsync()` to not return a boolean.

### Features

* Added auto-reconnect capability. Every 15s the client tries to reconnect when the connection was lost. Send operations during that time will fail with an exception.


## v1.1 (2021-12-07)

### Features

* Include host and port information in logging.


## v1.0 (2021-10-26)

### Features

* Initial release.
