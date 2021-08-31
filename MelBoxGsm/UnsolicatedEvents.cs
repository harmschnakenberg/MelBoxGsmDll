using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Timers;

namespace MelBoxGsm
{
    public static partial class Gsm
    {

        public static void CeckUnsolicatedIndicators(string recLine)
        {
            bool show = false;

            #region SIM-Schubfach
            Match m1 = Regex.Match(recLine, @"^SCKS: (\d)");

            if (m1.Success)
            {
                SimTrayIndicator(m1.Groups[1].Value);
                show = true;
            }
            #endregion

            #region neue SMS oder Statusreport empfangen
            Match m2 = Regex.Match(recLine, @"\+C(?:MT|DS)I: ");

            if (m2.Success)
            {
                foreach (SmsIn sms in SmsRead())
                    Console.WriteLine($"Empfangene Sms: {sms.Index} - {sms.Phone}:\t{sms.Message}");

                show = true;
            }
            #endregion

            #region Sprachanruf empfangen (Ereignis wird beim Klingeln erzeugt)
            Match m3 = Regex.Match(recLine, @"\+CLIP: (.+),(?:\d+),,,,(?:\d+)");

            if (m3.Success)
            {
                NewVoiceCallIndicator(m3.Groups[1].Value.Trim('"'));
                show = true;
            }
            #endregion

            Match m4 = Regex.Match(recLine, @"\+CREG: (\d)\r\n");

            if (m4.Success && int.TryParse(m1.Groups[1].Value, out int status))
            {
                NetworkRegistration = (Registration)status;
                show = true;
            }

            #region Anzeige in Konsole
            if (show && ReliableSerialPort.Debug.HasFlag(ReliableSerialPort.GsmDebug.UnsolicatedResult))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(recLine);
                Console.ForegroundColor = ConsoleColor.Gray;
            }
            #endregion
        }

        /// <summary>
        /// Der SIM-Karten-Einschub oder die Verbidnung zur SIM-Karte haben sich geändert. 
        /// </summary>
        /// <param name="val"></param>
        private static void SimTrayIndicator(string val)
        {
            if (int.TryParse(val, out int SimTrayStatus))
            {
                if ((Registration)SimTrayStatus == Registration.Registerd)
                    SetupModem();
                else
                {
                    LastError = new Tuple<DateTime, string>(DateTime.Now, "Keine SIM-Karte detektiert.");

                    NewErrorEvent?.Invoke(null, $"{LastError.Item1:yyyy-MM-dd HH:mm:ss}: {LastError.Item2}");
                }
            }
        }

        /// <summary>
        /// EIn eingehedner Sprachanruf wurde erfasst
        /// </summary>
        /// <param name="callFrom"></param>
        private static void NewVoiceCallIndicator(string callFrom)
        {
            if (callFrom != lastIncomingCallNumber)
            {
                lastIncomingCallNumber = callFrom;
                Log.Info($"Sprachanruf von Anrufer >{callFrom}<", 31016);
                NewCallRecieved?.Invoke(null, callFrom);

                Timer callNotifcationTimer = new Timer(20000); //Anrufe von diese Nummer für 20 sec. nicht signalisieren
                callNotifcationTimer.Elapsed += CallNotifcationTimer_Elapsed;
                callNotifcationTimer.AutoReset = false;
                callNotifcationTimer.Start();
            }
        }

        /// <summary>
        /// Hilfsvariable, damit eingehende Sprachanrufe nur einmal und nicht bei jedem Klingeln gemeldet werden. 
        /// </summary>
        private static string lastIncomingCallNumber = "";

        /// <summary>
        /// Hilfstimer, damit eingehende Sprachanrufe nur einmal und nicht bei jedem Klingeln gemeldet werden. 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void CallNotifcationTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            lastIncomingCallNumber = "";
        }
    }
}
