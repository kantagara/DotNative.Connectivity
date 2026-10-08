using System;
using Microsoft.Extensions.DependencyInjection;

namespace DotNative.Connectivity;

public static class ConnectivityServiceProviderExtensions
{
#if NET10_0_OR_GREATER
    extension(IServiceProvider services)
    {
        /// <summary>Resolves the registered plugin using the provider's DI lifetime.</summary>
        public IConnectivity Connectivity => services.GetRequiredService<IConnectivity>();
    }
#else
    /// <summary>Resolves the registered plugin using the provider's DI lifetime.</summary>
    public static IConnectivity Connectivity(this IServiceProvider services) =>
        services.GetRequiredService<IConnectivity>();
#endif
}
