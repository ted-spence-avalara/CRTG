﻿/*
 * 2012 - 2015 Ted Spence, http://tedspence.com
 * License: http://www.apache.org/licenses/LICENSE-2.0 
 * Home page: https://github.com/tspence/CRTG
 * 
 * This program uses icons from http://www.famfamfam.com/lab/icons/silk/
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Threading;
using System.Xml.Serialization;
using System.IO;
using System.Net.Mail;
using Microsoft.Win32;
using CRTG.Sensors;
using log4net;
using CRTG.Sensors.Devices;
using CRTG.Common;
using CRTG.Sensors.Data;
using Newtonsoft.Json;

namespace CRTG
{
    [Serializable]
    public class SensorProject
    {
        /// <summary>
        /// All the sensors in a project
        /// </summary>
        [AutoUI(Skip=true)]
        public List<DeviceContext> Devices { get; set; }

        /// <summary>
        /// Dependency injection for notifications
        /// </summary>
        [AutoUI(Skip = true), XmlIgnore]
        public BaseNotificationSystem Notifications { get; set; }

        /// <summary>
        /// The hostname or IP address of the SMTP server
        /// </summary>
        [AutoUI(Group = "SMTP")]
        public string SmtpHost { get; set; }

        /// <summary>
        /// username to provide to the SMTP server
        /// </summary>
        [AutoUI(Group = "SMTP")]
        public string SmtpUsername { get; set; }

        /// <summary>
        /// username to provide to the SMTP server
        /// </summary>
        [AutoUI(Group = "SMTP", PasswordField = true)]
        public string SmtpPassword { get; set; }

        /// <summary>
        /// Does the user want local time or GMT?
        /// </summary>
        [AutoUI(Group = "Preferences")]
        public DateTimePreference TimeZonePreference { get; set; }

        /// <summary>
        /// Does the user want flat CSV files, or full OpenXML?
        /// </summary>
        [AutoUI(Group = "Preferences")]
        public ReportFileFormat ReportFileFormatPreference { get; set; }

        /// <summary>
        /// The "from" name to use for the email message
        /// </summary>
        [AutoUI(Group = "SMTP")]
        public string MessageFrom { get; set; }

        /// <summary>
        /// The subject line to use for notifications
        /// </summary>
        [AutoUI(Group = "Email Notifications")]
        public string SubjectLineTemplate { get; set; }

        /// <summary>
        /// The message body to use for notifications
        /// </summary>
        [AutoUI(Group = "Email Notifications", MultiLine=20)]
        public string MessageBodyTemplate { get; set; }

        [AutoUI(Label = "Klipfolio Username", Group = "Klipfolio")]
        public string KlipfolioUsername { get; set; }

        [AutoUI(Label = "Klipfolio Password", Group = "Klipfolio", PasswordField = true)]
        public string KlipfolioPassword { get; set; }

        /// <summary>
        /// Indicates the method we use to store data
        /// </summary>
        [AutoUI(Skip=true)]
        public IDataStore DataStore { get; set; }


        #region Logging
        private static ILog _logger = null;

        /// <summary>
        /// The log to use for outputting debug information
        /// </summary>
        [AutoUI(Skip = true)]
        private static ILog Log
        {
            get
            {
                // Make sure we have a logging object we can use
                if (_logger == null) {
                    log4net.Config.XmlConfigurator.Configure();
                    _logger = (ILog)log4net.LogManager.GetLogger(typeof(SensorProject));
                }

                // Here's the logger
                return _logger;
            }
        }
        #endregion


        #region Managing the collection of sensors
        [AutoUI(Skip=true)]
        public int NextSensorNum = 1;

        /// <summary>
        /// Add a sensor to the project
        /// </summary>
        /// <param name="dc"></param>
        /// <param name="s"></param>
        public void AddSensor(IDevice dc, BaseSensor s)
        {
            s.Identity = NextSensorNum++;
            s.Device = dc;
            dc.Sensors.Add(s);
        }
        #endregion


        #region Managing the collection of devices
        [AutoUI(Skip = true)]
        public int NextDeviceNum = 1;

        /// <summary>
        /// Add a device to the project
        /// </summary>
        /// <param name="dc"></param>
        /// <param name="s"></param>
        public void AddDevice(DeviceContext dc)
        {
            dc.Identity = NextDeviceNum++;
            Devices.Add(dc);
        }
        #endregion


        #region Multithreaded data gathering from the sensors
        protected Thread _collection_thread = null;
        protected bool _keep_running = false;

        /// <summary>
        /// Start collection of data for this project
        /// </summary>
        public void Start()
        {
            if (_keep_running == false) {
                _keep_running = true;
                ParameterizedThreadStart ts = new ParameterizedThreadStart(CollectionThread);
                _collection_thread = new Thread(ts);
                _collection_thread.Start();
            }
        }

        /// <summary>
        /// Stop collection of data for this project (after current sensors have fired)
        /// </summary>
        public void Stop()
        {
            if (_collection_thread != null) {
                _keep_running = false;
            }
        }

        /// <summary>
        /// This is the background thread for collecting data
        /// </summary>
        public void CollectionThread(object o)
        {
            // Reset thread pool to run up to 16 concurrent requests - no idea why, I just picked this, so let's go with it
            ThreadPool.SetMaxThreads(16, 16);

            // Okay, let's enter the loop
            while (_keep_running) {
                int collect_count = 0;
                DateTime next_collect_time = DateTime.MaxValue;

                // Be safe about this - we don't want this thread to blow up!  It's the only one we've got
                try {

                    // Loop through sensors, and spawn a work item for them
                    for (int i = 0; i < Devices.Count; i++) {
                        DeviceContext dc = Devices[i];
                        for (int j = 0; j < dc.Sensors.Count; j++) {

                            // Allow us to kick out
                            if (!_keep_running) return;

                            // Okay, let's work on this sensor
                            ISensor s = dc.Sensors[j];
                            if (s.Enabled && !s.InFlight) {

                                // Spawn a work item in the thread pool to do this collection task
                                if (s.NextCollectTime <= DateTime.UtcNow) {
                                    s.InFlight = true;
                                    ThreadPool.QueueUserWorkItem(delegate { s.OuterCollect(); });
                                    collect_count++;

                                    // If it's not time yet, use this to factor when next to wake up
                                } else {
                                    if (s.NextCollectTime < next_collect_time) {
                                        next_collect_time = s.NextCollectTime;
                                    }
                                }
                            }
                        }
                    }

                // Failsafe
                } catch (Exception ex) {
                    Console.WriteLine(ex.ToString());
                }

                // Sleep until next collection time, but allow ourselves to kick out
                if (!_keep_running) return;
                TimeSpan time_to_sleep = next_collect_time - DateTime.UtcNow;
                int clean_sleep_time = Math.Max(1, Math.Min((int)time_to_sleep.TotalMilliseconds, 1000));
                System.Threading.Thread.Sleep(clean_sleep_time);
            }
        }
        #endregion


        #region Serialization and Singleton
        [AutoUI(Skip = true)]
        public static SensorProject Current = null;

        public SensorProject()
        {
            Devices = new List<DeviceContext>();
            string path = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().CodeBase);
            if (path.StartsWith("file:\\", StringComparison.CurrentCultureIgnoreCase)) {
                path = path.Substring(6);
            }
            DataStore = new CSVSensorDataStore(Path.Combine(path, "sensors"));
        }

        /// <summary>
        /// Read a sensor back from a node from an XML file
        /// </summary>
        /// <param name="r"></param>
        /// <returns></returns>
        public static SensorProject Deserialize(string filename)
        {
            SensorProject sp = null;

            // Read in the text
            try {
                var settings = new JsonSerializerSettings();
                settings.TypeNameHandling = TypeNameHandling.Objects;
                settings.Formatting = Newtonsoft.Json.Formatting.Indented;
                string s = File.ReadAllText(filename);
                sp = JsonConvert.DeserializeObject<SensorProject>(s, settings);

            // Failed to load
            } catch (Exception ex) {
                SensorProject.LogException("Error loading sensor XML file", ex);
                sp = new SensorProject();
                sp.Devices = new List<DeviceContext>();
            }

            // Now make all the sensors read their data
            Current = sp;
            List<int> sensor_id_list = new List<int>();
            foreach (DeviceContext dc in sp.Devices) {
                foreach (BaseSensor bs in dc.Sensors) {

                    // Make sure each sensor is uniquely identified!  If any have duplicate IDs, uniqueify them
                    if (sensor_id_list.Contains(bs.Identity)) {
                        bs.Identity = sp.NextSensorNum++;
                    }
                    sensor_id_list.Add(bs.Identity);
                    bs.Device = (IDevice)dc;

                    // Read in each sensor's data, and write it back out to disk 
                    // (this ensures that all files have the same fields in the same order - permits appending via AppendText later
                    bs.DataRead();
                }
            }

            // Ensure that the log folder exists
            if (!Directory.Exists("logs")) Directory.CreateDirectory("Logs");

            // Save this as the current project
            return sp;
        }

        static void deserializer_UnreferencedObject(object sender, UnreferencedObjectEventArgs e)
        {
            Log.DebugFormat("Unknown object type [{0}]", e.UnreferencedId);
        }

        static void deserializer_UnknownAttribute(object sender, XmlAttributeEventArgs e)
        {
            Log.DebugFormat("Unknown attribute [{0}={1}]", e.Attr.Name, e.Attr.Value);
        }

        static void deserializer_UnknownElement(object sender, XmlElementEventArgs e)
        {
            Log.DebugFormat("Unknown element [{0}]", e.Element.Name);
        }

        static void deserializer_UnknownNode(object sender, XmlNodeEventArgs e)
        {
            Log.DebugFormat("Unknown node [{0}]", e.Name);
        }

        /// <summary>
        /// Write this sensor out to a configuration file
        /// </summary>
        /// <param name="r"></param>
        /// <returns></returns>
        public void Serialize(string filename)
        {
            try {
                var settings = new JsonSerializerSettings();
                settings.TypeNameHandling = TypeNameHandling.Objects;
                settings.Formatting = Newtonsoft.Json.Formatting.Indented;
                var s = JsonConvert.SerializeObject(SensorProject.Current, settings);
                File.WriteAllText(filename, s);
            } catch (Exception ex) {
                SensorProject.LogException("Error saving sensor project", ex);
            }
        }
        #endregion


        #region Logging
        public static void LogException(string Location, Exception ex)
        {
            string message = String.Format("CRTG caught an internal exception in [{0}]: {1}", Location, ex.ToString());
            Log.Error(message);
            System.Diagnostics.Debug.WriteLine(message);
        }

        public static void LogMessage(string message)
        {
            Log.Debug(message);
        }
        #endregion
    }
}
