﻿/*
 * 2012 - 2016 Ted Spence, http://tedspence.com
 * License: http://www.apache.org/licenses/LICENSE-2.0 
 * Home page: https://github.com/tspence/CRTG
 * 
 * This program uses icons from http://www.famfamfam.com/lab/icons/silk/
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Management;
using CRTG.Common;
using CRTG.Sensors.Toolkit;

namespace CRTG.Sensors.SensorLibrary
{
    [SensorUI(Category = "WMI", Tooltip = "Measure disk statistics via WMI.")]
    public class WmiDiskSensor : BaseSensor
    {
        public override string GetNormalIconPath()
        {
            return "Resources/bricks.png";
        }

        [AutoUI(Group = "Drive")]
        public string DriveLetter;

        [AutoUI(Group = "Drive")]
        public DiskMeasurement Measurement;

        #region Implementation

        public override decimal Collect()
        {
            var coll = WmiHelper.WmiQuery(Device, String.Format("SELECT * FROM Win32_LogicalDisk WHERE Name = '{0}:'", DriveLetter));

            // Count usage of each OS instance (should really be only one!)
            decimal total = 0;
            decimal free = 0;
            int count = 0;
            foreach (ManagementObject queryObj in coll) {
                object o = queryObj["FreeSpace"];
                count++;
                free += (decimal)Convert.ChangeType(o, typeof(decimal));
                o = queryObj["Size"];
                count++;
                total += (decimal)Convert.ChangeType(o, typeof(decimal));
            }

            // Average them out
            if (count > 0) {
                switch (Measurement) {
                    case DiskMeasurement.BytesFree:
                        return free;
                    case DiskMeasurement.BytesUsed:
                        return total - free;
                    case DiskMeasurement.MegabytesFree:
                        return free / 1024 / 1024;
                    case DiskMeasurement.MegabytesUsed:
                        return (total - free) / 1024 / 1024;
                    case DiskMeasurement.PercentFree:
                        return (free / total) * 100;
                    case DiskMeasurement.PercentUsed:
                        return 1 - (free / total) * 100;
                }
            }

            // Failed!
            throw new Exception("No disk query found!");
        }
        #endregion
    }
}
