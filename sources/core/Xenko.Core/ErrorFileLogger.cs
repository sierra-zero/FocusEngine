using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace Xenko.Core
{
    /// <summary>
    /// Enables creating crash logs to file, so it is easier to report crashes
    /// </summary>
    public class ErrorFileLogger
    {
        private static string savePath = null, savePrefix = null;

        private static UnhandledExceptionEventHandler globalHandler = null;

        private static object locker = new object();

        /// <summary>
        /// Turn on file reporting of all exceptions, or change settings
        /// </summary>
        /// <param name="prefix">What to prefix the files?</param>
        /// <param name="path">Location to store the log files, null defaults to My Documents folder</param>
        public static void EnableGlobalExceptionLogger(string prefix = "FocusEngineCrashLog", string path = null)
        {
            savePath = (path ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)) + "/";
            savePrefix = prefix ?? "FocusEngineCrashLog";

            if (globalHandler == null)
                globalHandler = new UnhandledExceptionEventHandler(UnhandledException);

            // Add the event handler for handling non-UI thread exceptions to the event. 
            // remove it first, just to make sure we don't multiply it
            AppDomain.CurrentDomain.UnhandledException -= globalHandler;
            AppDomain.CurrentDomain.UnhandledException += globalHandler;
        }

        public static void Disable()
        {
            if (globalHandler != null)
                AppDomain.CurrentDomain.UnhandledException -= globalHandler;

            savePath = null;
            savePrefix = null;
        }

        public static void WriteLogToFile(string message)
        {
            if (savePrefix == null || savePath == null) return;

            string time = DateTime.Now.ToString("dd-MMM-yyyy-hh.mm-tt");
            string filename = savePath + savePrefix + time + ".txt";
            message = "\n--------------------- NEW ENTRY ------------------------\n" +
                      "Executable: " + System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName + "\n" +
                      "Thread: " + Thread.CurrentThread.Name + "\n" +
                      "Time: " + time + "\n" + 
                      "Message: " + message + "\n------------------------ END ENTRY -------------------\n";

            lock(locker)
            {
                System.IO.File.AppendAllText(filename, message);
            }
        }

        public static void WriteExceptionToFile(Exception ex, string additionalContext = "")
        {
            if (savePrefix == null || savePath == null || ex == null) return;

            try
            {
                // Get stack trace for the exception with source file information
                StackTrace st = new StackTrace(ex, true);
                // Get the top stack frame
                StackFrame frame = st.GetFrame(0);
                // Get the line number from the stack frame
                int line = frame.GetFileLineNumber();
                if (line != 0)
                {
                    // try to add it to the front of the text
                    additionalContext += "{line #" + line.ToString() + "}";
                }
            }
            catch (Exception e) { /* couldn't get line info */ }

            string error = "Message: " + ex.Message + "\n" +
                           "Context: " + additionalContext + "\n" +
                           "Stack Trace: " + ex.StackTrace + "\n" +
                           "Source: " + ex.Source + "\n" +
                           "Inner Exception: " + (ex.InnerException?.Message ?? "None") + "\n" +
                           "Inner Exception Trace: " + (ex.InnerException?.StackTrace ?? "None");
            WriteLogToFile(error);
        }

        private static void UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            WriteExceptionToFile(e.ExceptionObject as Exception);
        }
    }
}
