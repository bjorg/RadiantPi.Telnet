# Release Notes


# v2.0 (TBD)

### BREAKING CHANGES

* Changed target framework to .NET 6.0.
* Changed `ITelnet.ConnectAsync()` to not return a boolean.

### Features

* Added auto-reconnect capability. Eveny 15s the client tries to reconnect when the connection was lost. Send operations during that time will fail with an exception.


## v1.1 (2021-12-07)

### Features

* Include host and port information in logging.


## v1.0 (2021-10-26)

### Features

* Initial release.
