using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Timers;

namespace MelBoxGsm
{
    public static partial class Gsm
    {

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
            bool networkStatusChange = false;
            networkStatusChange &= HasNetworkRegistrationChanged();
            networkStatusChange &= HasSignalQualityChanged();

            if (networkStatusChange)
                NetworkStatusEvent?.Invoke(null, (NetworkRegistration != Registration.Registered ? 0 : SignalQuality));

            if (!CallForwardingActive && !IsCallForewardingActive())
                SetCallForewarding(CallForwardingNumber);

            List<SmsIn> smsIn = SmsRead("ALL");

            foreach (SmsIn sms in smsIn)
            {
                Console.WriteLine($"Empfangene Sms: {sms.Index} - {sms.Phone}:\t{sms.Message}");
            }
        }

        /// <summary>
        /// Fragt die Mobilnetzempfangsqualität ab. 
        /// </summary>
        /// <returns>WAHR, wenn sich die Signalqualität zur vorigen Abfrage verändert hat.</returns>
        private static bool HasSignalQualityChanged()
        {
            string answer = Port.Ask("AT+CSQ");
            MatchCollection mc = Regex.Matches(answer, @"\+CSQ: (\d+),(\d+)");

            if (mc.Count > 0 && int.TryParse(mc[0].Groups[1].Value, out int quality))
            {
                quality = (int)(quality / 0.32); //in %

                if (quality > 100) quality = 0;

                if (SignalQuality != quality)
                {
                    SignalQuality = quality;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Legt fest, ob Änderungen in der Mobilfunknetzregistrierung unaufgefordert gemeldet werden sollen.
        /// Siehe 'AT+CREG' Kap. 8.4 (Seite 190)
        /// </summary>
        /// <param name="isActive"></param>
        private static void SetNetworkRegistrationChangeNotification(bool isActive = true)
        {
            _ = Port.Ask("AT+CREG=" + (isActive ? "1" : "0"));

            //Änderungen werden unaufgefordert mit '+CREG: <stat>' angezeigt
        }

        /// <summary>
        /// Fragt ab, ob das Modem im Mobilfunnetz registriert ist.
        /// </summary>
        /// <returns>true = online</returns>
        private static bool HasNetworkRegistrationChanged()
        {
            bool result = false;
            string answer = Port.Ask("AT+CREG?");
            MatchCollection mc = Regex.Matches(answer, @"\+CREG: (\d),(\d)");

            if (mc.Count == 0) return result;

            if (int.TryParse(mc[0].Groups[1].Value, out int mode))
            {
                if (IsNetworkRegistrationNotificationActive != (mode > 0)) // nur Änderungen verarbeiten
                {
                    IsNetworkRegistrationNotificationActive = (mode > 0);

                    result = true;
                }
            }

            if (int.TryParse(mc[0].Groups[2].Value, out int status))
            {                
                if (NetworkRegistration != (Registration)status)
                {
                    NetworkRegistration = (Registration)status;
                    result = true;
                }
            }

            return result;
        }

        /// <summary>
        /// Setzt das Format mit dem Fehler auf der Modemseite angezeigt werden
        /// </summary>
        /// <param name="format">0 = Fehler, 1 = Fehlernummer, 2= Fehlertext</param>
        private static void SetErrorFormat(int format = 2)
        {
            /*            
            AT+CMEE=?
            AT+CMEE=?
            +CMEE: (0-2)
            OK
            AT+CMEE?
            AT+CMEE?
            +CMEE: 0
            OK
            AT+CMEE=2
            AT+CMEE=2
            OK
            */
            _ = Port.Ask($"AT+CMEE={format}");
        }

        /// <summary>
        /// Speichert einem vom Modem ausgegebenen Fehler und löst das Ereignis 'NewErrorEvent' aus.
        /// </summary>
        /// <param name="answer">zu prüfende Ausgabe von Modem</param>
        internal static void ParseErrorResponse(string answer)
        {
            Match m = Regex.Match(answer, @"\+CM[ES] ERROR: (.+)");
            if (m.Success)
            {
                var currentError = m.Groups[1].Value.TrimEnd('\r');

                if (currentError != LastError.Item2) //Gleichen Fehler nur einmal melden
                {
                    LastError = new Tuple<DateTime, string>(DateTime.Now, m.Groups[1].Value.TrimEnd('\r'));

                    NewErrorEvent?.Invoke(null, $"{LastError.Item1:yyyy-MM-dd HH:mm:ss}: {LastError.Item2}");
                }
                else
                {
                    LastError = new Tuple<DateTime, string>(DateTime.Now, LastError.Item2); //nur Zeit aktualisieren
                }
            }
        }

        /// <summary>
        /// Prüft, ob sich das GSM-Modem im Text-Modus befindet. Setzt ggf. den TextModus im GSM-Modem.
        /// </summary>
        private static void CheckOrSetGsmTextMode()
        {
            string answer = Port.Ask("AT+CMGF?");
            MatchCollection mc = Regex.Matches(answer, @"\+CMGF: (\d)");

            if (mc.Count > 0 && int.TryParse(mc[0].Groups[1].Value, out int textmode))
            {
                IsGsmTextMode = textmode == 1;

                if (textmode == 0 && Port.Ask("AT+CMGF=1").Contains("OK"))
                {
                    /*
                    Write Command
                    AT+CMGF=<mode>
                    Response(s)
                    OK
                    */

                    CheckOrSetGsmTextMode();
                }
            }
        }

        /// <summary>
        /// Liest die Type des Modems aus.
        /// </summary>
        private static void GetModemType()
        {       
            string answer1 = Port.Ask("ATI");
            Match m1 = Regex.Match(answer1, @"ATI\r\r\n(.+)\r\n(.+)\r\n(.+)\r\n\r\nOK\r\n");
            if (!m1.Success) return;
            
            string manufacturer = m1.Groups[1].Value;
            string type = m1.Groups[2].Value;
            string revision = m1.Groups[3].Value;

            ModemType = $"{manufacturer} {type} {revision}";
        }

        /// <summary>
        /// Liest die Eigene Nummer und den eigenen Telefonbucheintrag für die SIM-Karte im GSM-Modem aus.
        /// </summary>
        private static void GetOwnNumber()
        {
            string answer = Port.Ask("AT+CNUM");
            MatchCollection mc = Regex.Matches(answer, @"\+CNUM: (.+),(.+),(\d+)");

            if (mc.Count > 0)
            {
                OwnName = mc[0].Groups[1].Value.Trim('"').DecodeUcs2();
                OwnNumber = mc[0].Groups[2].Value.Trim('"');
            }
        }

        /// <summary>
        /// Liest den aktuellen Mobilfunknetzbetreiber aus.
        /// </summary>
        private static void GetProviderName()
        {
            string answer = Port.Ask("AT+COPS?");
            MatchCollection mc = Regex.Matches(answer, @"\+COPS: (\d),(\d),(.+)\r");

            if (mc.Count > 0)
                ProviderName = mc[0].Groups[3].Value.Trim('"');
        }

        /// <summary>
        /// Liest die Nummer des SMS-ServiceCenters aus.
        /// </summary>
        private static void GetSmsServiceCenterAddress()
        {
            string answer = Port.Ask("AT+CSCA?");
            MatchCollection mc = Regex.Matches(answer, @"\+CSCA: (.+),(\d)");

            if (mc.Count > 0)
                SmsServiceCenterAddress = mc[0].Groups[1].Value.Trim('"').DecodeUcs2();
        }

        /// <summary>
        /// Setzt den Speicherort für SMS.
        /// </summary>
        /// <param name="sim"></param>
        /// <param name="device"></param>
        private static void SetGsmMemory(bool sim = true, bool device = true)
        {
            /*  
            Write Command
            AT+CPMS=<mem1> [,  <mem2> [,  <mem3> ]]
            Response(s)
            +CPMS: <used1> ,  <total1> ,  <used2> ,  <total2> ,  <used3> ,  <total3>
            OK
            ERROR
            ERROR
            +CMS ERROR
            */

            string mem;

            if (sim && !device) mem = "SM";
            else if (!sim && device) mem = "ME";
            else mem = "MT";

            string answer = Port.Ask($"AT+CPMS=\"{mem}\",\"{mem}\",\"{mem}\"");
            MatchCollection mc = Regex.Matches(answer, @"\+CPMS: (\d+),(\d+),(\d+),(\d+),(\d+),(\d+)");

            if (mc.Count > 0)
            {
                int readmem_used = int.Parse(mc[0].Groups[1].Value);
                int readmem_max = int.Parse(mc[0].Groups[2].Value);
                int writemem_used = int.Parse(mc[0].Groups[3].Value);
                int writemem_max = int.Parse(mc[0].Groups[4].Value);
                int recmem_used = int.Parse(mc[0].Groups[5].Value);
                int recmem_max = int.Parse(mc[0].Groups[6].Value);

                SmsStorageCapacity = Math.Max(Math.Max(readmem_max, writemem_max), recmem_max);
                SmsStorageCapacityUsed = (readmem_used + writemem_used + recmem_used) / (readmem_max  + writemem_max + recmem_max);
            }
        }

        /// <summary>
        /// Prüft, ob die SIM-Karte eine PIN-EIngabe erfordert und gibt ggf. den PIN ein.
        /// </summary>
        /// <param name="simPin">PIN für die SIM-Karte. Beliebig, wenn keine PIN-Abfrage an SIM</param>
        private static void GetSimPinStatus(string simPin)
        {
            string answer = Port.Ask("AT+CPIN?");
            MatchCollection mc = Regex.Matches(answer, @"\+CPIN: (.+)\r");

            if (mc.Count > 0)
                SimPinStatus = mc[0].Groups[1].Value;

            if (SimPinStatus == "SIM PIN") //PIN-Eingabe erforderlich
            {
                Console.WriteLine("PIN-Eingabe erforderlich.");
                Port.Ask("AT+CPIN=" + simPin);
                GetSimPinStatus(simPin);
            }
        }

        /// <summary>
        /// Fragt ab, ob die Rufumleitung bei Nicht-Erreichbarkeit eingeschaltet ist.
        /// </summary>
        /// <returns>true = Sprachanrufe werden umgeleitet.</returns>
        private static bool IsCallForewardingActive()
        {
            string answer = Port.Ask("AT+CCFC=2,2");
            //+CCFC: <status> ,  <class> [,  <number> ,  <type> ]
            MatchCollection mc = Regex.Matches(answer, @"\+CCFC: (\d),(\d),""(.+)"",(?:\d+),(\d+)");

            foreach (Match m in mc)
            {
                if (int.TryParse(m.Groups[1].Value, out int _status) && int.TryParse(m.Groups[2].Value, out int _class) && (_class % 2 == 1)) //Sprachanrufe
                {
                    bool isActive = _status == 1;

                    if (isActive != CallForwardingActive) // nur bei Statusänderung
                    {
                        Log.Info($"Die Anrufweiterleitung an >{CallForwardingNumber}< ist {(isActive ? "" : "de")}aktiviert", 104);
                        if (isActive) SetIncomingCallNotification();
                    }

                    CallForwardingActive = isActive;
                    CallForwardingNumber = mc[0].Groups[3].Value.Trim('"');
                }
            }

            return CallForwardingActive;
        }

        /// <summary>
        /// Aktiviert die Rufumleitung auf die Angegeben Telefonnummer
        /// </summary>
        /// <param name="phone">Nummer zu der Sprachanrufe umgeleitet werdne sollen.</param>
        public static void SetCallForewarding(string phone)
        {
            MatchCollection mc = Regex.Matches(phone, @"\+(\d+)");

            if (RingSecondsBeforeCallForwarding == 0) RingSecondsBeforeCallForwarding = 5;

            if (mc.Count > 0)
                _ = Port.Ask($"AT+CCFC=2,3,\"{phone}\",145,1,{RingSecondsBeforeCallForwarding}", 5000);

            _ = IsCallForewardingActive();
        }

        /// <summary>
        /// Legt fest, ob bei eingehendem Anruf ein Ereignis ausgelöst werden soll. Voraussetzung für Protokollierung der Rufumleitung.
        /// </summary>
        /// <param name="active">true = Zeigt Nummer des Anrufenden an bei jedem '+RING: '-Ereignis von Modem.</param>
        private static void SetIncomingCallNotification(bool active = true)
        {
            _ = Port.Ask($"AT+CLIP={(active ? 1 : 0)}");
        }

        /// <summary>
        /// Legt fest, ob ein Ereignis ausgelöst werden soll wenn die SIM-Karte (nicht mehr) gefunden wird.
        /// </summary>
        /// <param name="notify">true = Ereignis bei Statusänderung SIM-Schubfach</param>
        private static void SetSimTrayNotification(bool notify = true)
        {
            _ = Port.Ask($"AT^SCKS={(notify ? 1 : 0)}");
        }

        /// <summary>
        /// Setzt die Codierung des GSM-Modems
        /// </summary>
        /// <param name="chset">Für MC75: 'GSM' Standard, 'UCS2' 16-bit hexadecimal</param>
        private static void SetCharacterset(string chset = "GSM")
        {
            //Setze Codierung in GSM Modem. Siehe Seite 53 (Kap. 2.136)

            _ = Port.Ask($"AT+CSCS=\"{chset}\"");

            string answer = Port.Ask("AT+CSCS?");

            Match m = Regex.Match(answer, @"\+CSCS: ""(.+)""");

            if (m.Success)            
                GsmCharacterSet = m.Groups[1].Value;            
        }


        /// <summary>
        /// Legt fest, ob ein Ereignis ausgelöst werden soll, wenn das Modem eine neue Nachricht empfangen hat.
        /// </summary>
        /// <param name="sms">true = Ereignis bei eingehener SMS</param>
        /// <param name="statusreport">true = Ereignis bei eingehendem Stausreport</param>
        private static void SetNewSmsRecNotification(bool sms = true, bool statusreport = true)
        {
            bool unicode = GsmCharacterSet != "GSM";

            //Validity-Period https://de.wammu.eu/docs/devel/internals/gammu-message_8h_source.html Zeile 233ff
            int vp_min = 60;
            int vp = (vp_min / 5) - 1; // bis vp_min = 720 min. (12 h) // vp: 11 = 1h, 71 = 6h, 167  = 1d, 255 = max_time

            //Setze Parameter für SMS Textmode. Siehe Seite 375f (Kap. 13.16)
            //Quelle: https://www.codeproject.com/questions/271002/delivery-reports-in-at-commands
            //Quelle: https://www.smssolutions.net/tutorials/gsm/sendsmsat/
            //AT+CSMP=<fo> [,  <vp> / <scts> [,  <pid> [,  <dcs> ]]]
            // <fo> First Octet: 1 = SMS senden 2 = ?, 4 = Reject Duplicates, do not return a message ID when a message with the same destination and ID is still pending, 8 = ? , 16 = has validity period, 32 = Request delivery report),  
            // <vp> Validity-Period: 0 - 143 (vp+1 x 5min), 144 - 167 (12 Hours + ((VP-143) x 30 minutes)), [...]
            // <dcs> DataCodingScheme: 0 = GSM, 8 = Unicode 
            _ = Port.Ask($"AT+CSMP=49,{vp},0,{(unicode ? 8 : 0)}");

            //Sendeempfangsbestätigungen abonieren. Siehe Seite 367ff (Kap. 13.11)
            //AT+CNMI= [ <mode> ][,  <mt> ][,  <bm> ][,  <ds> ][,  <bfr> ]
            int mt = sms ? 1 : 0;
            int ds = statusreport ? 2 : 0;

            _ = Port.Ask($"AT+CNMI=2,{mt},2,{ds},1");
        }



    }
}
