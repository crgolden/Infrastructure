#pragma warning disable SA1200
using System.Diagnostics;
using Infrastructure.Extensions;
using Infrastructure.Hubs;
using Serilog;
#pragma warning restore SA1200

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    if (builder.Environment.IsDevelopment())
    {
        builder.Configuration.AddUserSecrets("aspnet-Infrastructure-3f7a2c1b-8e4d-4b9f-a6c3-2d1e5f8b9a0c");
    }

    var tokenCredential = await builder.Configuration.ToTokenCredentialAsync();
    var secretClient = builder.Configuration.ToSecretClient(tokenCredential);
    await builder.AddMonitoringServicesAsync(secretClient);
    await builder.AddObservabilityAsync(secretClient);
    builder.AddDataProtection(tokenCredential);
    builder.Services.AddControllers();
    builder.Services.AddRazorPages();

    var app = builder.Build();
    app.UseSerilogRequestLogging(options =>
    {
        options.EnrichDiagnosticContext = (diagnosticContext, _) =>
        {
            var activity = Activity.Current;
            if (activity is null)
            {
                return;
            }

            diagnosticContext.Set(nameof(Activity.TraceId), activity.TraceId.ToString());
            diagnosticContext.Set(nameof(Activity.SpanId), activity.SpanId.ToString());
        };
    });
    if (app.Environment.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
    }
    else
    {
        app.UseHsts();
    }

    app.UseHttpsRedirection();
    app.MapHealthChecks("/health").DisableHttpMetrics();
    app.MapGet("/ping", () => Results.Ok()).DisableHttpMetrics();
    app.MapStaticAssets();
    app.MapControllers();
    app.MapRazorPages();
    app.MapHub<HealthHub>("/hubs/health");

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}
