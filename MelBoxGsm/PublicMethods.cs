using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Timers;

namespace MelBoxGsm
{
    public static partial class Gsm
    {

        /// <summary>
        /// Timer für zyklische Modemabfrage
        /// </summary>
        static readonly Timer askingTimer = new Timer(19000);// wenn unsolicated SMS-Delivery +CMTI: / StatusReport +CDSI: funktioniert ca 1 Minute, sonst 10-30 sec?


        /// <summary>
        /// Gibt eine Reihe von Anweisungen an das GSM-Modem, bevor andere Operationen ausgeführt werden.
        /// </summary>
        public static void SetupModem()
        {
            Log.Info($"Modem wird an {SerialPortName} initialisiert.", 102);

            //Modem
            GetModemType();
            SetErrorFormat();
            SetSimTrayNotification();

            //SIM-Karte
            GetSimPinStatus(SimPin);
            GetOwnNumber();
            GetProviderName();
            GetSmsServiceCenterAddress();
            SetGsmMemory();
            CheckNetworkConnection(null, null);          
            SetNewSmsRecNotification();
            SetCallForewarding(CallForwardingNumber);

            #region Regelmäßige Anfragen an das Modem
            askingTimer.Elapsed += new ElapsedEventHandler(CheckNetworkConnection);
            askingTimer.AutoReset = true;
            askingTimer.Start();
            #endregion

        }


        /// <summary>
        /// Versendet eine SMS. Nach Absenden wird das Ereignis 'SmsSentEvent' ausgelöst
        /// </summary>
        /// <param name="phone">Telefonnummer in internationaler Schreibweise z.B. +49160...</param>
        /// <param name="message">Inhalt der SMS</param>
        public static void SmsSend(string phone, string message)
        {
            MatchCollection mc = Regex.Matches(phone, @"\+(\d+)");
            if (mc.Count == 0)
                Log.Warning($">{phone}< ist keine gültige Telefonnummer. Es wird keine SMS versand! >{message}<", 52050);
            else
            {
                SmsOut sms = new SmsOut
                {
                    Phone = phone,
                    Message = message
                };

                sendList.Enqueue(sms);
            }
            
            SendList();
        }


        /// <summary>
        /// Liest die im GSM-Modem gespeicherten SMS und StatusReports (Empfangsbestätigungen) aus.
        /// </summary>
        /// <param name="filter">Mögliche Werte: 'REC UNREAD', 'REC READ', 'ALL'</param>
        /// <returns></returns>
        public static List<SmsIn> SmsRead(string filter = "REC UNREAD")
        {
            string answer = Port.Ask($"AT+CMGL=\"{filter}\"");

            ParseStatusReports(answer);

            #region Neue SMSen

            //Nachricht:
            //+CMGL: <index> ,  <stat> ,  <oa> / <da> , [ <alpha> ], [ <scts> ][,  <tooa> / <toda> ,  <length> ]
            //<data>
            //[... ]
            //OK

            //+CMGL: 9,"REC READ","+4917681371522",,"20/11/08,13:47:10+04"
            //Ein Test 08.11.2020 13:46 PS sms38.de

            //\+CMGL: (\d+),"(.+)","(.+)",(.*),"(.+),(.+)([\+-].{2})"\n(.+\n)+
            //     MatchCollection mc = Regex.Matches(answer, "\\+CMGL: (\\d+),\"(.+)\",\"(.+)\",(.*),\"(.+),(.+)([\\+-].{2})\"\\n(.+)");

            MatchCollection mc = Regex.Matches(answer, @"\+CMGL: (\d+),""(.+)"",""(.+)"",(.*),""(.+),(.+)([\+-].{2})""\r\n(.+)");

            List<SmsIn> newSms = new List<SmsIn>();

            foreach (Match m in mc)
            {
                try
                {
                    int index = int.Parse(m.Groups[1].Value);
                    string status = m.Groups[2].Value.Trim('"');
                    string phone = m.Groups[3].Value.Trim('"');
                    string phonebookentry = m.Groups[4].Value.Trim('"');

                    string dateStr = "20" + m.Groups[5].Value.Trim('"').Replace('/', '-');
                    string timeStr = m.Groups[6].Value;
                    int timeZone = int.Parse(m.Groups[7].Value.Trim('"')) / 4;

                    string message = m.Groups[8].Value.TrimEnd('\r');

                    if (message.StartsWith("00")) //UCS-Codiert?
                        message = DecodeUcs2(message);

                    message = DecodeUmlaute(message); //in GSM-Encoding korrigieren Umlaute

                    _ = DateTime.TryParse($"{dateStr} {timeStr}", out DateTime time);

                    Console.WriteLine($"SMS empfangen: {time}\r\n{status}\r\n{phone}\r\n{message}");

                    SmsIn sms = new SmsIn
                    {
                        Index = index,
                        Status = status,
                        Phone = phone,
                        TimeUtc = time.AddHours(-timeZone),
                        Message = message
                    };

                    newSms.Add(sms);
                }
#pragma warning disable CA1031 // Do not catch general exception types
                catch (Exception)
                {
#if DEBUG
                    throw;
#else
                    Log.Error("Fehler beim Abrufen von SMS aus GSM-Modem.", 42017);
#endif
                }
#pragma warning restore CA1031 // Do not catch general exception types
            }

            #endregion

            if(newSms.Count > 0)
                SmsRecievedEvent?.Invoke(null, newSms);
            
            return newSms;
        }


        /// <summary>
        /// Löscht die im Modem unter 'index' geführte SMS.
        /// </summary>
        /// <param name="index">Speicherplatz einer SMS in Modem oder SIM-Karte</param>
        public static void SmsDelete(int index)
        {
            if (index > 0) // Simulierte SMS haben Index 0
            _ = Port.Ask("AT+CMGD=" + index);
        }

        /// <summary>
        /// Regelmäßige Abfrage an Modem nach Signalqualität, Mobilfunkanmeldestatus, Rufumleitung und SMS-Nachrichten im Modemspeicher
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void CheckNetworkConnection(object sender, ElapsedEventArgs e)
        {
#if DEBUG
            if (e != null) Console.WriteLine(e.SignalTime ); //Als Lebenszeichen an Console
#endif
            bool networkStatus = false;
            networkStatus &= HasNetworkRegistrationChanged();
            networkStatus &= HasSignalQualityChanged();

            if ( networkStatus )
                NetworkStatusEvent?.Invoke(null, (NetworkRegistration != "registriert" ? 0 : SignalQuality) );
            
            if (!CallForwardingActive && !IsCallForewardingActive())
                SetCallForewarding(CallForwardingNumber);

            List<SmsIn> smsIn = SmsRead("ALL");

            foreach (SmsIn sms in smsIn)
            {
                Console.WriteLine($"Empfangene Sms: {sms.Index} - {sms.Phone}:\t{sms.Message}");
            }
        }

        public static void ModemShutdown()
        {
            Port.Close();
        }
    }
}
