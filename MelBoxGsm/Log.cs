using System.Diagnostics;

namespace MelBoxGsm
{
    static class Log
    {

        internal static void Info(string message, int id)
        {
            using (EventLog eventLog = new EventLog("Application"))
            {
                eventLog.Source = System.IO.Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetExecutingAssembly().Location);
                eventLog.WriteEntry(message, EventLogEntryType.Information, id);
            }
        }

        internal static void Warning(string message, int id)
        {
            using (EventLog eventLog = new EventLog("Application"))
            {
                eventLog.Source = System.IO.Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetExecutingAssembly().Location);
                eventLog.WriteEntry(message, EventLogEntryType.Warning, id);
            }
        }

        internal static void Error(string message, int id)
        {
            using (EventLog eventLog = new EventLog("Application"))
            {
                eventLog.Source = System.IO.Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetExecutingAssembly().Location);
                eventLog.WriteEntry(message, EventLogEntryType.Error, id);
            }
        }

    }
}
