using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MelBoxGsm
{
    public partial class Gsm
    {

        /// <summary>
        /// Versendet eine SMS. Nach Absenden wird das Ereignis 'SmsSentEvent' ausgelöst
        /// </summary>
        /// <param name="phone">Telefonnummer in internationaler Schreibweise z.B. +49160...</param>
        /// <param name="message">Inhalt der SMS</param>
        public static void SendSms(string phone, string message)
        {
            SmsOut sms = new SmsOut
            {
                Phone = phone,
                Message = message
            };

            sendList.Enqueue(sms);

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

            #region StatusReport

            //Statusreport:
            //+CMGL: < index > ,  < stat > ,  < fo > ,  < mr > , [ < ra > ], [ < tora > ],  < scts > ,  < dt > ,  < st >
            //[... ]
            //OK
            //z.B.: +CMGL: 1,"REC READ",6,34,,,"20/11/06,16:08:45+04","20/11/06,16:08:50+04",0

            //\+CMGL: (\d+),"(.+)",(\d+),(\d+),,,"(.+),(.+)([\+-]\d+)","(.+),(.+)([\+-]\d+)",(\d+)
            //getestet: @"\+CMGL: (\d+),(.+),(\d+),(\d+),,,(.+),(.+)([\+-].+),(.+),(.+)([\+-].+),(\d)"
            MatchCollection reports = Regex.Matches(answer, @"\+CMGL: (\d+),(.+),(\d+),(\d+),,,(.+),(.+)([\+-].+),(.+),(.+)([\+-].+),(\d)");

            List<Report> newReports = new List<Report>();

            foreach (Match m in reports)
            {
                try
                {
                    int index = int.Parse(m.Groups[1].Value);
                    string status = m.Groups[2].Value.Trim('"');
                    int firstOctet = int.Parse(m.Groups[3].Value); // Bedeutung?
                    int reference = int.Parse(m.Groups[4].Value);
                    string dateCenter = "20" + m.Groups[5].Value.Trim('"').Replace('/', '-');
                    string timeCenter = m.Groups[6].Value;
                    int zoneCenter = int.Parse(m.Groups[7].Value.Trim('"')) / 4;
                    string dateDischarge = "20" + m.Groups[8].Value.Trim('"').Replace('/', '-');
                    string timeDischarge = m.Groups[9].Value;
                    int zoneDischarge = int.Parse(m.Groups[10].Value.Trim('"')) / 4;
                    int delviveryStatus = int.Parse(m.Groups[11].Value);

                    _ = DateTime.TryParse(dateCenter + " " + timeCenter, out DateTime SmsCenterTime);
                    _ = DateTime.TryParse(dateDischarge + " " + timeDischarge, out DateTime DischargeTime);

                    Report report = new Report
                    {
                        Index = index,
                        Status = status,
                        Reference = reference,
                        ServiceCenterTimeUtc = SmsCenterTime.AddHours(zoneCenter),
                        DischargeTimeUtc = DischargeTime.AddHours(zoneDischarge),
                        DeliveryStatus = delviveryStatus
                    };

                    Console.WriteLine($"Empfangsbestätigung erhalten für ID[{reference}]: " + (delviveryStatus > 63 ? "Senden fehlgeschlagen" : delviveryStatus > 31 ? "senden im Gange" : "erfolgreich versendet"));
                    newReports.Add(report);
                }
                catch (Exception)
                {
#if DEBUG
                    throw;
#else
                    Log.Error("Fehler beim Abrufen von Statusreports aus GSM-Modem.", 2108142015);
#endif
                }
            }

            TrackSentSms(newReports);
           
            #endregion

            #region Neue SMSen

            //Nachricht:
            //+CMGL: <index> ,  <stat> ,  <oa> / <da> , [ <alpha> ], [ <scts> ][,  <tooa> / <toda> ,  <length> ]
            //<data>
            //[... ]
            //OK

            //+CMGL: 9,"REC READ","+4917681371522",,"20/11/08,13:47:10+04"
            //Ein Test 08.11.2020 13:46 PS sms38.de

            //\+CMGL: (\d+),"(.+)","(.+)",(.*),"(.+),(.+)([\+-].{2})"\n(.+\n)+
            MatchCollection mc = Regex.Matches(answer, "\\+CMGL: (\\d+),\"(.+)\",\"(.+)\",(.*),\"(.+),(.+)([\\+-].{ 2})\"\\r\\n(.+\\r\\n)+");

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

                    string message = m.Groups[8].Value;

                    _ = DateTime.TryParse($"{dateStr} {timeStr}", out DateTime time);

                    Console.WriteLine($"SMS empfangen: {time}\r\n{status}\r\n{phone}\r\n{message}");

                    SmsIn sms = new SmsIn
                    {
                        Index = index,
                        Status = status,
                        Phone = phone,
                        TimeUtc = time.AddHours(timeZone),
                        Message = message
                    };

                    newSms.Add(sms);
                }
                catch (Exception)
                {
#if DEBUG
                    throw;
#else
                    Log.Error("Fehler beim Abrufen von SMS aus GSM-Modem.", 2108142017);
#endif
                }
            }

            #endregion

            return newSms;
        }


        public static void CheckNetworkConnection()
        {
            if (HasSignalQualityChanged() || HasNetworkRegistrationChanged())
                NetworkStatusEvent?.Invoke(null, EventArgs.Empty);


        }

    }
}
