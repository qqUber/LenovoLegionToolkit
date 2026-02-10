using System;
using System.Linq;
using System.Management;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Utils;

// ReSharper disable InconsistentNaming
// ReSharper disable StringLiteralTypo

namespace LenovoLegionToolkit.Lib.System.Management;

public static partial class WMI
{
    public static class LenovoFanMethod
    {
        public static async Task<bool> ExistsAsync()
        {
            try
            {
                var query = $"SELECT * FROM LENOVO_FAN_METHOD";
                var mos = new ManagementObjectSearcher("root\\WMI", query);
                var managementObjects = await Task.Run(() => mos.Get()).ConfigureAwait(false);
                return managementObjects.Count > 0;
            }
            catch
            {
                return false;
            }
        }
        public static async Task FanSetTableAsync(byte[] fanTable)
        {
            try
            {
                await CallAsync("root\\WMI",
                    $"SELECT * FROM LENOVO_FAN_METHOD",
                    "Fan_Set_Table",
                    new() { { "FanTable", fanTable } }).ConfigureAwait(false);
                    
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"[FAN] Successfully set fan table. Length: {fanTable.Length}");
            }
            catch (ManagementException ex) when (ex.ErrorCode == ManagementStatus.AccessDenied)
            {
                Log.Instance.Trace($"[FAN] Access denied. Run as administrator or disable Vantage/Legion Zone.", ex);
                throw new InvalidOperationException("Access denied to WMI. Ensure Vantage/Legion Zone is disabled and app runs as administrator.", ex);
            }
            catch (ManagementException ex) when (ex.ErrorCode == ManagementStatus.NotFound)
            {
                Log.Instance.Trace($"[FAN] WMI class not found. Check if required drivers are installed.", ex);
                throw new InvalidOperationException("WMI interface not available. Install Lenovo Energy Management and Vantage Gaming Feature Driver.", ex);
            }
            catch (Exception ex)
            {
                Log.Instance.Trace($"[FAN] Failed to set fan table.", ex);
                throw;
            }
        }

        public static Task<bool> FanGetFullSpeedAsync() => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_FAN_METHOD",
            "Fan_Get_FullSpeed",
            [],
            pdc => (bool)pdc["Status"].Value);

        public static Task FanSetFullSpeedAsync(int status) => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_FAN_METHOD",
            "Fan_Set_FullSpeed",
            new() { { "Status", status } });

        public static Task<int> FanGetCurrentSensorTemperatureAsync(int sensorId) => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_FAN_METHOD",
            "Fan_GetCurrentSensorTemperature",
            new() { { "SensorID", sensorId } },
            pdc => Convert.ToInt32(pdc["CurrentSensorTemperature"].Value));

        public static Task<int> FanGetCurrentFanSpeedAsync(int fanId) => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_FAN_METHOD",
            "Fan_GetCurrentFanSpeed",
            new() { { "FanID", fanId } },
            pdc => Convert.ToInt32(pdc["CurrentFanSpeed"].Value));

        public static async Task<int> GetCurrentFanMaxSpeedAsync(int sensorId, int fanId)
        {
            var result = await ReadAsync("root\\WMI",
                $"SELECT * FROM LENOVO_FAN_TABLE_DATA WHERE Sensor_ID = {sensorId} AND Fan_Id = {fanId}",
                pdc => Convert.ToInt32(pdc["CurrentFanMaxSpeed"].Value)).ConfigureAwait(false);
            return result.Max();
        }

        public static async Task<int> GetDefaultFanMaxSpeedAsync(int sensorId, int fanID)
        {
            var result = await ReadAsync("root\\WMI",
                $"SELECT * FROM LENOVO_FAN_TABLE_DATA WHERE Sensor_ID = {sensorId} AND Fan_Id = {fanID}",
                pdc => Convert.ToInt32(pdc["DefaultFanMaxSpeed"].Value)).ConfigureAwait(false);
            return result.Max();
        }
    }
}
