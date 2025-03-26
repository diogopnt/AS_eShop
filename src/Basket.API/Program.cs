using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using Prometheus;
using System.Diagnostics.Metrics;

var builder = WebApplication.CreateBuilder(args);

builder.AddBasicServiceDefaults();
builder.AddApplicationServices();

builder.Services.AddGrpc();

// Configurar OpenTelemetry para Trace

builder.Services.AddOpenTelemetry()
    .WithTracing(tracerProviderBuilder =>
    {
        tracerProviderBuilder
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("BasketAPI"))
            .AddAspNetCoreInstrumentation() // Instrumentação para HTTP
            .AddGrpcClientInstrumentation() // Instrumentação para gRPC
            .AddHttpClientInstrumentation() // Instrumentação para chamadas HTTP
            .AddOtlpExporter(opts =>
            {
                opts.Endpoint = new Uri("http://localhost:4317");  // Porta OTLP (gRPC)
            }).AddSource("BasketAPI");
    });

builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("BasketAPI"));
        metrics.AddAspNetCoreInstrumentation();  // Instrumentação de requests HTTP
        metrics.AddRuntimeInstrumentation();     // Métricas da runtime .NET
        metrics.AddPrometheusExporter();
    });

var app = builder.Build();

// Usar o endpoint para scraping de métricas para Prometheus
app.UseOpenTelemetryPrometheusScrapingEndpoint();

app.MapDefaultEndpoints();
app.MapGrpcService<BasketService>();

await app.RunAsync();
