using ASRS.ModbusGateway;
using ASRS.ModbusGateway.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

// Bind configuration from appsettings.json
builder.Services.Configure<PLCSettings>(builder.Configuration.GetSection("PLCSettings"));

// Add worker
builder.Services.AddHostedService<Worker>();

// Add logging
builder.Logging.AddConsole();

IHost host = builder.Build();
host.Run();
