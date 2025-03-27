using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using Prometheus;
using System.Diagnostics.Metrics;

var builder = WebApplication.CreateBuilder(args);

builder.AddBasicServiceDefaults();
builder.AddApplicationServices();

builder.Services.AddGrpc();

builder.Services.AddOpenTelemetry()
    .WithTracing(tracerProviderBuilder =>
    {
        tracerProviderBuilder
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("BasketAPI"))
            .AddAspNetCoreInstrumentation() 
            .AddGrpcClientInstrumentation() 
            .AddHttpClientInstrumentation() 
            .AddOtlpExporter(opts =>
            {
                opts.Endpoint = new Uri("http://localhost:4317");  
            }).AddSource("BasketAPI");
    });

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapGrpcService<BasketService>();

await app.RunAsync();
