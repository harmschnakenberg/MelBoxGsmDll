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
        static readonly Timer askingTimer = new Timer(19000); // 10-30 sec?


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
            SetCharacterset(GsmCharacterSet);

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
        /// Schließt den Seriellen Port.
        /// </summary>
        public static void ModemShutdown()
        {
            Port.Close();
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

                    string message = m.Groups[8].Value.TrimEnd('\r').DecodeUcs2();

                    _ = DateTime.TryParse($"{dateStr} {timeStr}", out DateTime time);

                    Console.WriteLine($"SMS empfangen: {time}\r\n{status}\r\n{phone}\r\n{message}");

                    SmsIn sms = new SmsIn
                    {
                        Index = index,
                        Status = status,
                        Phone = phone,
                        TimeUtc = time.AddHours(-timeZone),
                        Message = message.DecodeUmlaute() //korrigiere Umlaut bei GSM-Encoding
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
        /// Geht davon aus, dass ein String der mit '00' beginnt in hexadezimalschreibweise (UCS2-Characterset) vorliegt und wandelt das HEX-Format in Unicode um.
        /// Beginnt der String nicht mit '00' wird er unverändert zurückgegeben.
        /// </summary>
        /// <param name="ucs2">SMS-Inhalt im GSM- oder UCS2-Format</param>
        /// <returns>EIngabe-String als Unicode</returns>
        public static string DecodeUcs2(this string ucs2)
        {
            if (!ucs2.StartsWith("00"))
                return ucs2;

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

            return System.Text.Encoding.BigEndianUnicode.GetString(bytes.ToArray());
        }

        /// <summary>
        /// Erstellt eine hexadezimale bytereihe des übergebenen String.
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string EncodeUcs2(this string str)
        {
            if (GsmCharacterSet == "GSM")
            {
                return str;
            }
            else
            {
                byte[] bytes = System.Text.Encoding.BigEndianUnicode.GetBytes(str);

                return BitConverter.ToString(bytes).Replace("-", string.Empty);
            }
        }

        /// <summary>
        /// Bei GSM-Encoding werden Umlaute als andere Zeichen interpretiert. 
        /// NOCH TESTEN: Werden die ersetzten Sonderzeichne in unseren SMSen verwendet?
        /// </summary>
        /// <param name="input">SMS-Inhalt im GSM-Encoding mit 'verbogenen' Umlauten</param>
        /// <returns>Inhalt mit Umlauten</returns>
        private static string DecodeUmlaute(this string input)
        {
            if (GsmCharacterSet == "GSM")
                return input.Replace('[', 'Ä').Replace('\\', 'Ö').Replace('^', 'Ü').Replace('{', 'ä').Replace('|', 'ö').Replace('~', 'ü');
            else
                return input;
        }


        /// <summary>
        /// Der Empfangsstatus wird im Status-report als Byte ausgegeben. Diese Funktion interpretiert das Byte und gibt aus
        /// - einen generellen Status 
        /// - eine detaillierte Erklärung
        /// - einen Icon-Namen für die Darstellung in der Weboberfläche (siehe Google Material icons https://fonts.google.com/icons?selected=Material+Icons+Outlined)
        /// </summary>
        /// <param name="TP_ST">Statusbyte aus STATUS-REPORT von Modem</param>
        /// <param name="detailedStatus">genauer status von Servicecenter</param>
        /// <param name="icon">Icon-Name zur Darstellung in der Weboberfläche</param>
        /// <returns>genereller Status des Sendeerfolgs</returns>
        public static string GetDeliveryStatus(int TP_ST, out string detailedStatus, out string icon)
        {

            #region Detaillierter Sendestatus
            switch (TP_ST)
            {
                case 0x00: //0
                    detailedStatus = "SMS wurde empfangen"; // "SMS empfangen received by the Empänger"; //SMS SessionManagement
                    break;
                case 0x01: //1
                    detailedStatus = "SMS vom ServiceCenter an Empfänger weitergeleitet, aber Empfang konnte nicht bestätigt werden"; // "SMS forwarded by the ServiceCenter to the Empänger but the ServiceCenter is unable to confirm delivery";
                    break;
                case 0x02: //2
                    detailedStatus = "SMS wurde vom ServiceCenter ersetzt"; // "SMS replaced by the ServiceCenter";
                    break;
                case 0x20: //32
                    detailedStatus = "Überlastung";// "Congestion";
                    break;
                case 0x21: //33
                    detailedStatus = "Empänger beschäftigt";
                    break;
                case 0x22: //34
                    detailedStatus = "Keine Antwort vom Empänger";
                    break;
                case 0x23: //35
                    detailedStatus = "Service abgelehnt";
                    break;
                case 0x24: //36
                    detailedStatus = "Servicequalität nicht ausreichend"; // "Quality of service not available";
                    break;
                case 0x25: //37
                    detailedStatus = "Fehler beim  Empänger";
                    break;
                case 0x40: //64
                    detailedStatus = "Verarbeitungsfehler auf der Gegenseite"; // "Remote procedure error";
                    break;
                case 0x41: //65
                    detailedStatus = "Ziel ist inkompatibel"; //"Incompatible destination";
                    break;
                case 0x42: //66
                    detailedStatus = "Verbindung durch Empänger abgelehnt";
                    break;
                case 0x43: //67
                    detailedStatus = "Nicht erreichbar"; // "Not obtainable";
                    break;
                case 0x44: //68
                    detailedStatus = "Servicequalität nicht ausreichend"; //"Quality of service not available";
                    break;
                case 0x45: //69
                    detailedStatus = "Übertragungsnetz nicht verfügbar"; // "No internetworking available";
                    break;
                case 0x46: //70
                    detailedStatus = "Gültigkeitszeitraum der SMS überschritten"; // "SMS Validity Period Expired";
                    break; 
                case 0x47: //71
                    detailedStatus = "SMS gelöscht durch Ursprungsempänger"; // "SMS deleted by originating Empänger";
                    break;
                case 0x48: //72
                    detailedStatus = "SMS gelöscht durch ServiceCenter Administration"; // "SMS Deleted by ServiceCenter Administration";
                    break;
                case 0x49: //73
                    detailedStatus = "SMS existiert nicht"; // "SMS does not exist";
                    break;
                case 0x60: //96
                    detailedStatus = "Überlastung"; // "Congestion";
                    break;
                case 0x61: //97
                    detailedStatus = "Empänger beschäftigt";
                    break;
                case 0x62: //98
                    detailedStatus = "Keine Antwort vom Empänger";
                    break;
                case 0x63: //99
                    detailedStatus = "Service abgelehnt";
                    break;
                case 0x64: //100
                    detailedStatus = "Servicequalität nicht ausreichend"; //"Quality of service not available";
                    break;
                case 0x65: //101
                    detailedStatus = "Fehler beim Empänger";
                    break;
                case 256: //Selbst hinzugefügt!
                    detailedStatus = "SMS an Modem zum Senden geleitet";
                    break;
                case 512: //Selbst hinzugefügt!
                    detailedStatus = "erneuter Sendeversuch";
                    break;
                default:
                    detailedStatus = $"Sendestatus ist reserviert oder ServiceCenter-spezifisch: {TP_ST}";
                    break;
            }
            /* See GSM 03.40 section 9.2.3.15 (TP-Status) Seite 51*/

            string generalStatus;
            #endregion

            #region genereller Sendeerfolg

            /// 0-31:     delivered or, more generally, other transaction completed.
            /// 32-63:    still trying to deliver the message.
            /// 64-127:   not making any more delivery attempts.
            
            if (TP_ST < 3) // 0x03
            {
                generalStatus = "Nachricht ausgeliefert";
                icon = "check_circle_outline";
            }
            else if (TP_ST.IsBitSet(6)) // 64 = 0x40            
            {
                generalStatus = "Senden fehlgeschlagen";
                icon = "highlight_off";
            }
            else if (TP_ST.IsBitSet(5)) // 32 = 0x20             
            {
                generalStatus = "Warte auf Antwort";
                icon = "timer"; //"pending";
            }
            else
            {
                generalStatus = "Unbekannt";
                icon = "help_outline";
            }

            return generalStatus;
            #endregion

        }

        public enum DeliveryStatus
        {            
            Success = 0,
            ServiceDenied = 35,
            QueuedToSend = 256,
            SendRetry = 512,
            Simulated = 1024
        }


    }
}
