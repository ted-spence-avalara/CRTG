﻿using ClosedXML.Excel;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

namespace CRTG.Notification
{
    public class ExcelAttachmentHelper
    {
        /// <summary>
        /// Construct an excel file from a datatable
        /// </summary>
        public static Attachment BuildAttachment(DataTable dt, string attachment_filename, string worksheet_name)
        {    
            // Construct a memory stream for this excel file
            var ms = new MemoryStream();
            try {

                // Okay, let's put it into an XLSX file
                using (var wb = new XLWorkbook()) {

                    // Populate a table within this file
                    AddWorksheetTable(wb, dt, worksheet_name);

                    // Save to disk
                    wb.SaveAs(ms);
                    ms.Position = 0;
                }

                // Here's your attachment
                Attachment a = new Attachment(ms, new System.Net.Mime.ContentType("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"));
                a.Name = attachment_filename;
                return a;

            // Failed to build an excel file, log it
            } catch (Exception ex) {
                SensorProject.LogException("Build Excel File", ex);
            }

            // Something failed
            return null;
        }


        /// <summary>
        /// Add a simple list of name-value pairs as a worksheet
        /// </summary>
        /// <param name="workbook"></param>
        /// <param name="sheets"></param>
        /// <param name="obj"></param>
        /// <param name="worksheet_title"></param>
        private static void AddWorksheetTable(XLWorkbook wb, DataTable dt, string worksheet_name)
        {
            // Set up this sheet
            var ws = wb.Worksheets.Add(worksheet_name ?? "Sheet1");

            // Set up header row with column names
            for (int col = 0; col < dt.Columns.Count; col++) {
                var c = ws.Cell(1, col + 1);
                HeaderCell(c, dt.Columns[col].ColumnName);
            }

            // Set up data rows
            string lastcell = "B1";
            for (int row = 0; row < dt.Rows.Count; row++) {
                var dr = dt.Rows[row];
                for (int col = 0; col < dt.Columns.Count; col++) {
                    var c = ws.Cell(row + 2, col + 1);
                    c.Value = dr[col].ToString();
                    lastcell = c.Address.ToString();
                }
            }

            // Set this up as a table
            ws.Columns().AdjustToContents();
            var range = ws.Range("A1:" + lastcell);
            range.CreateTable();
        }

        /// <summary>
        /// Convenience for setting up headers
        /// </summary>
        /// <param name="c"></param>
        /// <param name="text"></param>
        private static void HeaderCell(IXLCell c, string text)
        {
            c.Value = text;
            var s = c.Style;
            s.Fill.SetBackgroundColor(XLColor.Orange);
            var f = s.Font;
            f.SetFontColor(XLColor.White);
            f.SetBold();
        }
    }
}
