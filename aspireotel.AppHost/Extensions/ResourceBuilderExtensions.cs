using System;
using Microsoft.Extensions.Configuration;

namespace aspireotel.AppHost.Extensions;

public static class ResourceBuilderExtensions
{
    private const string DashboardOtlpUrlVariableName = "DOTNET_DASHBOARD_OTLP_ENDPOINT_URL";

    // Adds the dashboard OTLP endpoint URL to the environment variables of the resource with the specified name.
    public static IResourceBuilder<T> WithDashboardEndpoint<T>(this IResourceBuilder<T> builder, string name)
        where T : IResourceWithEnvironment
    {
        var configuration = builder.ApplicationBuilder.Configuration;

        return builder.WithEnvironment(context =>
        {
            var t = context.ExecutionContext;
            if (context.ExecutionContext.IsPublishMode)
            {
                // Runtime only
                return;
            }

            var url = configuration[DashboardOtlpUrlVariableName];
            context.EnvironmentVariables[name] = builder.Resource is ContainerResource
                ? ReplaceLocalhostWithContainerHost(url, configuration)
                : url;
        });
    }

    private static string ReplaceLocalhostWithContainerHost(string value, ConfigurationManager configuration )
    {
        // https://stackoverflow.com/a/43541732/45091

        // This configuration value is a workaround for the fact that host.docker.internal is not available on Linux by default.
        var hostName = configuration["AppHost:ContainerHostname"] ?? /*_dcpInfo?.Containers?.ContainerHostName ??*/ "host.docker.internal";;

        return value.Replace("localhost", hostName, StringComparison.OrdinalIgnoreCase)
                    .Replace("127.0.0.1", hostName)
                    .Replace("[::1]", hostName);
    }    
}