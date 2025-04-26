using System.Diagnostics.Metrics;
using eShop.WebApp.Components;
using eShop.ServiceDefaults;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.AddApplicationServices();

var meter = new Meter("BasketAPI.Metrics");
builder.Services.AddSingleton(meter);

builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("BasketAPI"));
        metrics.AddAspNetCoreInstrumentation();
        metrics.AddRuntimeInstrumentation();
        metrics.AddPrometheusExporter();
        metrics.AddMeter("BasketAPI.Metrics");
    });

builder.Services.AddSingleton<BasketService>();

var app = builder.Build();

app.Use(async (context, next) =>
{
    var basketService = context.RequestServices.GetRequiredService<BasketService>();
    await basketService.HandleRequest(context, next);
});

app.UseOpenTelemetryPrometheusScrapingEndpoint();
app.MapDefaultEndpoints();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseAntiforgery();
app.UseHttpsRedirection();
app.UseStaticFiles();

app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.MapForwarder("/product-images/{id}", "http://catalog-api", "/api/catalog/items/{id}/pic");

app.Run();
