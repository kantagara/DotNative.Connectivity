# DotNative.Connectivity

Reports active desktop network interfaces and emits state snapshots when the OS reports an address or availability change.

```csharp
builder.Services.AddConnectivity();
var connectivity = provider.Connectivity;
var current = connectivity.GetCurrent();
await foreach (var update in connectivity.WatchAsync(cancellationToken)) { /* update UI */ }
```

The API reports interface presence, kind, name, and link speed. `HasNetworkInterface` does not promise public Internet, DNS, or backend reachability. A short burst of OS events may be coalesced to the latest snapshot. Disposing the async enumerator unregisters both event handlers. Android/iOS target APIs have the same shape but are not implemented by this package yet.

Build locally: `dotnet build -p:DotNativeSourceRoot=../dotNative`. Interface snapshots ran on macOS, Windows 11 ARM64, and Linux ARM64 (Debian 12 container). Event watcher behavior remains unverified on all platforms.

## Service access

Import `DotNative.Connectivity` to access the plugin through `IServiceProvider`:

```csharp
using DotNative.Connectivity;

var plugin = services.Connectivity;
```

The getter calls `GetRequiredService<IConnectivity>()` on every access, preserving
DI lifetimes and the usual missing-registration error. Register the plugin with
`AddConnectivity(...)` before building the provider.

A `net10.0` application uses the property syntax with C# 14 or later. A
`net9.0` application uses only the method equivalent:

```csharp
var plugin = services.Connectivity();
```

The package contains separate `net9.0` and `net10.0` assemblies. NuGet selects
the assembly matching the application target framework. `NET10_0_OR_GREATER`
selects the property; the `#else` branch selects the method.

Build and pack both targets with .NET 10 SDK. A source build using .NET 9 SDK
builds only `net9.0`; it does not produce the .NET 10 assembly.
