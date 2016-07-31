﻿using CRTG.Common;
/*
 * 2012 - 2016 Ted Spence, http://tedspence.com
 * License: http://www.apache.org/licenses/LICENSE-2.0 
 * Home page: https://github.com/tspence/CRTG
 * 
 * This program uses icons from http://www.famfamfam.com/lab/icons/silk/
 */
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRTG.Sensors
{
    public class BaseNotificationSystem
    {
        public virtual void Notify(BaseSensor baseSensor, NotificationState ns, DateTime timestamp, decimal value, string s)
        {
            throw new NotImplementedException();
        }

        public virtual void SendReport(string[] recipients, string subject, string message, DataTable report_data, ReportFileFormat format, string attachment_filename)
        {
            throw new NotImplementedException();
        }

        public virtual bool UploadReport<T>(List<T> list, bool include_header_row, string url, HttpVerb verb, string username, string password)
        {
            throw new NotImplementedException();
        }

        public virtual bool UploadReport(DataTable dt, bool include_header_row, string url, HttpVerb verb, string username, string password)
        {
            throw new NotImplementedException();
        }
    }
}
