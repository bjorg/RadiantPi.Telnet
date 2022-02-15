# Release Notes


# v2.0.1 (2022-02-15)

## Fixes

* Fixed an issue where reconnecting on lost network connection would not work as expected.
* Always show host IP and port in log messages.

# v2.0 (2022-02-15)

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
