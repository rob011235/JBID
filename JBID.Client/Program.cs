// Copyright (c) 2026 Robert Garner
// SPDX-License-Identifier: MIT

namespace JBID.Client
{
    using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

    /// <summary>
    /// Configures and runs the WebAssembly client.
    /// </summary>
    internal class Program
    {
        /// <summary>
        /// Starts the client and restores authentication state from the server.
        /// </summary>
        /// <param name="args">The command-line arguments.</param>
        /// <returns>A task representing the client lifetime.</returns>
        private static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);

            builder.Services.AddAuthorizationCore();
            builder.Services.AddCascadingAuthenticationState();
            builder.Services.AddAuthenticationStateDeserialization();

            await builder.Build().RunAsync();
        }
    }
}
