using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASRS.ModbusGateway.Models
{
    public class PLCSettings
    {
        public string IpAddress { get; set; } = "192.168.0.10";
        public int Port { get; set; } = 502;
        public int PollingIntervalMs { get; set; } = 1000;
        public int ReconnectDelayMs { get; set; } = 5000;
    }
}
