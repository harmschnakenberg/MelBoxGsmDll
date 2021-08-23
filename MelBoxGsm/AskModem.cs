using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Timers;

namespace MelBoxGsm
{
    public static partial class Gsm
    {

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

                if (SignalQuality != quality)
                {
                    SignalQuality = quality;
                    return true;
                }
            }

            return false;
        }

        private static bool HasNetworkRegistrationChanged()
        {
            bool result = false;
            string answer = Port.Ask("AT+CREG?");
            MatchCollection mc = Regex.Matches(answer, @"\+CREG: (\d),(\d)");

            if (mc.Count == 0) return result;

            if (int.TryParse(mc[0].Groups[1].Value, out int mode))
            {
                if (IsNetworkRegistrationNotificationActive != (mode > 0))
                {
                    IsNetworkRegistrationNotificationActive = (mode > 0);

                    result = true;
                }
            }

            string networkRegistration = string.Empty;

            if (int.TryParse(mc[0].Groups[2].Value, out int status))
            {
                switch (status)
                {
                    case 0:
                        networkRegistration = "nicht registriert";
                        break;
                    case 1:
                        networkRegistration = "registriert";
                        break;
                    case 2:
                        networkRegistration = "suche Netz";
                        break;
                    case 3:
                        networkRegistration = "verweigert";
                        break;
                    case 4:
                        networkRegistration = "unbekannt";
                        break;
                    case 5:
                        networkRegistration = "Roaming";
                        break;
                }
            }

            if (NetworkRegistration != networkRegistration)
            {
                NetworkRegistration = networkRegistration;
                result = true;
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

                    NewErrorEvent?.Invoke(null, $"{LastError.Item1.ToShortTimeString()}: {LastError.Item2}");
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
            string answer = Port.Ask("AT+CGMM");
            MatchCollection mc = Regex.Matches(answer, @"(\w+)\r\n\r\nOK");

            if (mc.Count > 0)
                ModemType = mc[0].Groups[1].Value;
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
                OwnName = mc[0].Groups[1].Value.Trim('"');
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
                SmsServiceCenterAddress = mc[0].Groups[1].Value.Trim('"');
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
                Port.Ask("AT+CPIN=" + simPin);
                GetSimPinStatus(simPin);
            }
        }

        private static bool IsCallRelayActive()
        {
            string answer = Port.Ask("AT+CCFC=0,2");
            MatchCollection mc = Regex.Matches(answer, @"\+CCFC: (\d),(\d),(.+),(\d+)");

            foreach (Match m in mc)
            {
                if (int.TryParse(m.Groups[2].Value, out int c) && int.TryParse(m.Groups[1].Value, out int a) && (c % 2 == 1)) //Sprachanrufe
                {
                    CallForwardingActive = a == 1;
                    CallForwardingNumber = mc[0].Groups[3].Value.Trim('"');
                }
            }

            return CallForwardingActive;
        }

        private static void SetCallRelay(string phone)
        {
            MatchCollection mc = Regex.Matches(phone, @"\+(\d+)");

            if (mc.Count > 0)
            _ = Port.Ask("AT+CCFC=0,3,\"" + phone + "\", 145");

            _ = IsCallRelayActive();
        }

        private static void SetIncomingCallNotification(bool active = true)
        {
            _ = Port.Ask($"AT+CLIP={(active ? 1 : 0)}");
        }

        private static void SetSimTrayNotification(bool notify = true)
        {
            _ = Port.Ask($"AT^SCKS={(notify ? 1 : 0)}");
        }

        private static void SetNewSmsRecNotification(bool sms = true, bool statusreport = true)
        {
            //Setze Parameter für SMS Textmode. Siehe Seite 375f (Kap. 13.16)
            //Quelle: https://www.codeproject.com/questions/271002/delivery-reports-in-at-commands
            //Quelle: https://www.smssolutions.net/tutorials/gsm/sendsmsat/
            //AT+CSMP=<fo> [,  <vp> / <scts> [,  <pid> [,  <dcs> ]]]
            // <fo> First Octet:
            // <vp> Validity-Period: 0 - 143 (vp+1 x 5min), 144 - 167 (12 Hours + ((VP-143) x 30 minutes)), [...]
            _ = Port.Ask("AT+CSMP=49,1,0,0");

            //Sendeempfangsbestätigungen abonieren. Siehe Seite 367ff (Kap. 13.11)
            //AT+CNMI= [ <mode> ][,  <mt> ][,  <bm> ][,  <ds> ][,  <bfr> ]
            int mt = sms ? 1 : 0;
            int ds = statusreport ? 2 : 0;

            _ = Port.Ask($"AT+CNMI=2,{mt},2,{ds},1");
        }

        /// <summary>
        /// Ereignisse (ungetriggerte Zeichenfolge) von Modem 
        /// </summary>
        /// <param name="recLine">empfang</param>
        internal static void UnsolicatedEvent(string recLine)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(recLine);
            Console.ForegroundColor = ConsoleColor.Gray;

            #region SIM-Schubfach
            Match m1 = Regex.Match(recLine, @"^SCKS: (\d)");

            if (m1.Success && int.TryParse(m1.Groups[1].Value, out int SimTrayStatus))
            {
                if (SimTrayStatus == 1) 
                    SetupModem(CallForwardingNumber);
                else
                {
                    LastError = new Tuple<DateTime, string>(DateTime.Now, "Keine SIM-Karte detektiert.");

                    NewErrorEvent?.Invoke(null, $"{LastError.Item1.ToShortTimeString()}: {LastError.Item2}");
                }
            }

            #endregion

            #region neue SMS oder Statusreport empfangen
            Match m2 = Regex.Match(recLine, @"\+C(?:MT|DS)I: ");

            if (m2.Success)
            {
                _ = SmsRead();
            }

            #endregion

            #region Sprachanruf empfangen (Ereignis wird beim Klingeln erzeugt)
            Match m3 = Regex.Match(recLine, @"\+CLIP: (.+),(?:\d+),,,,(?:\d+)");

            if (m3.Success)
            {
                string callFrom = m3.Groups[1].Value.Trim('"');

                if (callFrom != lastIncomingCallNumber)
                {
                    lastIncomingCallNumber = callFrom;
                    Log.Info($"Sprachanruf von Anrufer >{callFrom}<", 31016);
                    NewCallRecieved?.Invoke(null, $"Sprachanruf von >{callFrom}<");

                    Timer callNotifcationTimer = new Timer(20000); //Anrufe von diese Nummer für 20 sec. nicht signalisieren
                    callNotifcationTimer.Elapsed += CallNotifcationTimer_Elapsed;
                    callNotifcationTimer.AutoReset = false;
                    callNotifcationTimer.Start();
                }
            }
            #endregion
        }

        private static string lastIncomingCallNumber = "";

        private static void CallNotifcationTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            lastIncomingCallNumber = "";
        }


        public static string DecodeUcs2(string ucs2)
        {
            //UCS2 ist Fallback-Encode, wenn Standard GSM-Encode nicht ausreicht.
            ucs2 = ucs2.Trim();
            System.Collections.Generic.List<byte> bytes = new System.Collections.Generic.List<byte>();

            for (int i = 0; i < ucs2.Length; i += 2)
            {
                string str = ucs2.Substring(i, 2);
                bytes.Add(byte.Parse(str, System.Globalization.NumberStyles.HexNumber));
#if DEBUG
                Console.Write(str + " ");
#endif
            }
            //#if DEBUG
            //            Console.WriteLine();

            //            string result = "Unicode: " + System.Text.Encoding.Unicode.GetString(bytes.ToArray());
            //            result += Environment.NewLine + "UTF8:      " + System.Text.Encoding.UTF8.GetString(bytes.ToArray());
            //            result += Environment.NewLine + "Default:   " + System.Text.Encoding.Default.GetString(bytes.ToArray());
            //            result += Environment.NewLine + "UTF7:      " + System.Text.Encoding.UTF7.GetString(bytes.ToArray());
            //            result += Environment.NewLine + "ASCII:     " + System.Text.Encoding.ASCII.GetString(bytes.ToArray());
            //            result += Environment.NewLine + "BigEndian: " + System.Text.Encoding.BigEndianUnicode.GetString(bytes.ToArray());
            //#endif
            return System.Text.Encoding.BigEndianUnicode.GetString(bytes.ToArray());
        }

        private static string DecodeUmlaute(string input)
        {
            return input.Replace('[', 'Ä').Replace('\\', 'Ö').Replace('^', 'Ü').Replace('{', 'ä').Replace('|', 'ö').Replace('~', 'ü');
        }

    }
}
