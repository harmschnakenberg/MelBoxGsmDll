using System.Diagnostics;

namespace MelBoxGsm
{
    static class Log
    {
        /// <summary>
        /// Information in Windows-Ereignisprotokoll schreiben
        /// </summary>
        /// <param name="message">Text</param>
        /// <param name="id">eindeutige Nummer</param>
        internal static void Info(string message, int id)
        {
            using (EventLog eventLog = new EventLog("Application"))
            {
                eventLog.Source = System.IO.Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetExecutingAssembly().Location);
                eventLog.WriteEntry(message, EventLogEntryType.Information, id);
            }
        }

        /// <summary>
        /// Warnung in Windows-Ereignisprotokoll schreiben
        /// </summary>
        /// <param name="message">Text</param>
        /// <param name="id">eindeutige Nummer</param>
        internal static void Warning(string message, int id)
        {
            using (EventLog eventLog = new EventLog("Application"))
            {
                eventLog.Source = System.IO.Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetExecutingAssembly().Location);
                eventLog.WriteEntry(message, EventLogEntryType.Warning, id);
            }
        }

        /// <summary>
        /// Fehlermeldung in Windows-Ereignisprotokoll schreiben
        /// </summary>
        /// <param name="message">Text</param>
        /// <param name="id">eindeutige Nummer</param>
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
