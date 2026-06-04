using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ScapeSwitcher.Agent;
using ScapeSwitcher.Agent.Audio;
using ScapeSwitcher.Agent.Hid;

var builder = Host.CreateApplicationBuilder(args);

// Enables running as a Windows Service while still allowing console execution for local debugging.
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "ScapeSwitcher Agent";
});

builder.Services.AddSingleton<AudioDeviceSwitcher>();
builder.Services.AddSingleton<FractalDongleMonitor>();
builder.Services.AddHostedService<ScapeMonitorWorker>();

using var host = builder.Build();
await host.RunAsync();
