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
using System.IO;
using CRTG.Sensors.Helpers;
using CRTG.Common;

namespace CRTG.Sensors.SensorLibrary
{
    [SensorUI(Category = "File System", Tooltip = "Find a specific file and report on facts about that file.")]
    public class FileSensor : BaseSensor
    {
        [AutoUI(Group = "File")]
        public FileMeasurement Measurement;

        [AutoUI(Group = "File", BrowseFile=true)]
        public string Path;

        #region Implementation
        public override decimal Collect()
        {
            FileInfo fi = null;

            // Is this a UNC path?
            if (Path.StartsWith("\\\\")) {
                using (UNCAccessWithCredentials unc = new UNCAccessWithCredentials()) {

                    // Detect problems connecting
                    if (!unc.NetUseWithCredentials(Path, Device.Username, Device.WindowsDomain, Device.Password)) {
                        throw new Exception("Unable to connect to file server with supplied credentials.");
                    }

                    // Retrieve information about this file
                    fi = new FileInfo(Path);
                }

            // Okay, this is local, just connect to the file directly
            } else {
                fi = new FileInfo(Path);
            }

            // Retrieve information for this file
            TimeSpan ts;
            switch (Measurement) {
                case FileMeasurement.FileSizeBytes:
                    return (decimal)fi.Length;
                case FileMeasurement.MinutesSinceChanged:
                    ts = DateTime.UtcNow - fi.LastWriteTimeUtc;
                    return (decimal)ts.TotalMinutes;
                case FileMeasurement.MinutesSinceCreated:
                    ts = DateTime.UtcNow - fi.CreationTimeUtc;
                    return (decimal)ts.TotalMinutes;
            }

            // Failed
            return 0m;
        }
        #endregion
    }
}
