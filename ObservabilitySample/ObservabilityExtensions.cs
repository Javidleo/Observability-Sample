using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace ObservabilitySample;

public static class ObservabilityExtensions 
{
    public static IServiceCollection AddObservabilityDefaults(this IServiceCollection services, IWebHostEnvironment env, string serviceName = "test-service")
    {
        services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddRuntimeInstrumentation()
                       .AddProcessInstrumentation()
                       .AddBuiltInMeters()
                       .AddConsoleExporter()
                       .AddPrometheusExporter(); // this extension method set up /metrics endpoint and expose all metrics on that for service
                                                 // as prometheus is a pull mode service(it fetchs data from the host)
                                                 // you can find this service configurations on prometheus configs in devops folder.
            })
            .WithTracing(tracing =>
            {
                if (env.IsDevelopment())
                {
                    // We want to view all traces in development
                    tracing.SetSampler(new AlwaysOnSampler());
                }
                // adding your event provider service activity source name.
                // create an activity 
                tracing.AddSource("Rabbit-MQ");
                tracing.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(serviceName, serviceVersion: "1.0.0"));

                tracing.AddAspNetCoreInstrumentation(i =>
                {
                    i.Filter = (httpContext) =>
                    {
                        var path = httpContext.Request.Path.Value;

                        List<string> defaultIgnoreCases = ["/hc", "/metrics"];
                        foreach (var ignoreCase in defaultIgnoreCases)
                        {
                            if (path!.StartsWith(ignoreCase, StringComparison.OrdinalIgnoreCase))
                                return false;
                        }

                        return true;
                    };
                })
                .AddHttpClientInstrumentation()
                .AddOtlpExporter(i =>
                {
                    // only working with docker compose.
                    i.Endpoint = new Uri("http://collector:4317");
                });
            });

        return services;
    }
    public static WebApplication MapObservabilityEndpoints(this WebApplication app)
    {
        app.MapPrometheusScrapingEndpoint();
        return app;
    }
    public static IEndpointRouteBuilder MapObservabilityEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPrometheusScrapingEndpoint();

        return app;
    }

    private static MeterProviderBuilder AddBuiltInMeters(this MeterProviderBuilder meterProviderBuilder) =>
        meterProviderBuilder.AddMeter(
            "Microsoft.AspNetCore.Hosting",
            "Microsoft.AspNetCore.Server.Kestrel",
            "System.Net.Http");
}
