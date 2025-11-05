using System.Collections.Generic;
using ASRS.ModbusGateway.Models;

namespace ASRS.ModbusGateway.Registry
{
    public static class ModbusTagRegistry
    {
        public static List<ModbusTag> Tags = new()
        {
            // ===== WMS → PLC =====
            new ModbusTag { Name = "Operation_Type", Register = 10, Direction = "WMS_TO_PLC", FieldType = "String" },
            new ModbusTag { Name = "Source_X", Register = 11, Direction = "WMS_TO_PLC", FieldType = "Int" },
            new ModbusTag { Name = "Source_Y", Register = 12, Direction = "WMS_TO_PLC", FieldType = "Int" },
            new ModbusTag { Name = "Source_Z", Register = 13, Direction = "WMS_TO_PLC", FieldType = "Int" },
            new ModbusTag { Name = "Target_X", Register = 14, Direction = "WMS_TO_PLC", FieldType = "Int" },
            new ModbusTag { Name = "Target_Y", Register = 15, Direction = "WMS_TO_PLC", FieldType = "Int" },
        /*    new ModbusTag { Name = "Target_Z", Register = 16, Direction = "WMS_TO_PLC", FieldType = "Int" },
            new ModbusTag { Name = "Start_CMD", Register = 17, Direction = "WMS_TO_PLC", FieldType = "Int" },*/

            // ===== PLC → WMS =====
            new ModbusTag { Name = "Crane_Status", Register = 0, Direction = "PLC_TO_WMS", FieldType = "Int" },
            new ModbusTag { Name = "Crane_Pos_X", Register = 1, Direction = "PLC_TO_WMS", FieldType = "Int" },
            new ModbusTag { Name = "Crane_Pos_Y", Register = 2, Direction = "PLC_TO_WMS", FieldType = "Int" },
            new ModbusTag { Name = "Crane_Pos_Z", Register = 3, Direction = "PLC_TO_WMS", FieldType = "Int" },
            new ModbusTag { Name = "Operation_Type_PLC", Register = 3, Direction = "PLC_TO_WMS", FieldType = "String" },
            new ModbusTag { Name = "Source_X_PLC", Register = 4, Direction = "PLC_TO_WMS", FieldType = "Int" },
            new ModbusTag { Name = "Source_Y_PLC", Register = 5, Direction = "PLC_TO_WMS", FieldType = "Int" },
            new ModbusTag { Name = "Source_Z_PLC", Register = 6, Direction = "PLC_TO_WMS", FieldType = "Int" },
            new ModbusTag { Name = "Target_X_PLC", Register = 7, Direction = "PLC_TO_WMS", FieldType = "Int" },
            new ModbusTag { Name = "Target_Y_PLC", Register = 8, Direction = "PLC_TO_WMS", FieldType = "Int" },
            new ModbusTag { Name = "Target_Z_PLC", Register = 9, Direction = "PLC_TO_WMS", FieldType = "Int" }
        };
    }
}
