using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASRS.ModbusGateway.Models
{
    public class ModbusTag
    {
        public string Name { get; set; }
        public int Register { get; set; }
        public string Direction { get; set; } // "WMS_TO_PLC" or "PLC_TO_WMS"
        public string FieldType { get; set; } // Int, String, etc.
    }

}
