using ASRS.ModbusGateway;
using ASRS.ModbusGateway.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

// ✅ Load configuration from appsettings.json
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

// ✅ Bind configuration to your strongly typed settings
builder.Services.Configure<PLCSettings>(builder.Configuration.GetSection("PLCSettings"));
builder.Services.Configure<MqttSettings>(builder.Configuration.GetSection("MqttSettings"));

// ✅ Add your background worker
builder.Services.AddHostedService<Worker>();

// ✅ Add console logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// ✅ Build and run the host
IHost host = builder.Build();
host.Run();
