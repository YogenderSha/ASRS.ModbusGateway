using System;
using System.Threading;
using System.Threading.Tasks;
using ASRS.ModbusGateway.Models;
using Microsoft.Extensions.Hosting;
using EasyModbus;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ASRS.ModbusGateway
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly PLCSettings _plcSettings;
        private ModbusClient _modbusClient;

        public Worker(ILogger<Worker> logger, IOptions<PLCSettings> plcSettings)
        {
            _logger = logger;
            _plcSettings = plcSettings.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (_modbusClient == null || !_modbusClient.Connected)
                    {
                        ConnectToPLC();
                    }

                    if (_modbusClient.Connected)
                    {
                        // Example: read 10 holding registers starting at address 0
                        int[] values = _modbusClient.ReadHoldingRegisters(0, 10);
                        _logger.LogInformation($"[{DateTime.Now}] Registers: {string.Join(", ", values)}");
                    }

                    await Task.Delay(_plcSettings.PollingIntervalMs, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"PLC communication error: {ex.Message}");
                    await Task.Delay(_plcSettings.ReconnectDelayMs, stoppingToken);
                }
            }

            _modbusClient?.Disconnect();
        }

        private void ConnectToPLC()
        {
            try
            {
                _logger.LogInformation($"Connecting to PLC at {_plcSettings.IpAddress}:{_plcSettings.Port}...");
                _modbusClient = new ModbusClient(_plcSettings.IpAddress, _plcSettings.Port);
                _modbusClient.Connect();
                _logger.LogInformation("? Connected to PLC successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"? Failed to connect to PLC: {ex.Message}");
            }
        }
    }
}
