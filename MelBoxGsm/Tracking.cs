using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace MelBoxGsm
{
    public static partial class Gsm
    {

        /// <summary>
        /// Liste gesendeter SMS zur Nachverfolgung / Warten auf Empfangsbestätigung
        /// </summary>
        private static readonly List<SmsOut> trackingList = new List<SmsOut>();

        /// <summary>
        /// Liest Statusreports (Empfangsbestätigungen) aus der Modemantwort
        /// </summary>
        /// <param name="answer">Modemantwort</param>
        private static void ParseStatusReports(string answer)
        {
            #region StatusReport

            //Statusreport:
            //+CMGL: < index > ,  < stat > ,  < fo > ,  < mr > , [ < ra > ], [ < tora > ],  < scts > ,  < dt > ,  < st >
            //[... ]
            //OK
            //z.B.: +CMGL: 1,"REC READ",6,34,,,"20/11/06,16:08:45+04","20/11/06,16:08:50+04",0

            //\+CMGL: (\d+),"(.+)",(\d+),(\d+),,,"(.+),(.+)([\+-]\d+)","(.+),(.+)([\+-]\d+)",(\d+)
            //getestet: @"\+CMGL: (\d+),(.+),(\d+),(\d+),,,(.+),(.+)([\+-].+),(.+),(.+)([\+-].+),(\d)"
            MatchCollection reports = Regex.Matches(answer, @"\+CMGL: (\d+),(.+),(\d+),(\d+),,,(.+),(.+)([\+-].+),(.+),(.+)([\+-].+),(\d)");

            if (reports.Count == 0) return;

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
                        ServiceCenterTimeUtc = SmsCenterTime.AddHours(-zoneCenter),
                        DischargeTimeUtc = DischargeTime.AddHours(-zoneDischarge),
                        DeliveryStatus = delviveryStatus
                    };

                    //Delivery-Status <st> - Quelle: https://en.wikipedia.org/wiki/GSM_03.40#Discharge_Time
                    //0-31:     delivered or, more generally, other transaction completed.
                    //32-63:    still trying to deliver the message.
                    //64-127:   not making any more delivery attempts.
                    Console.WriteLine($"Empfangsbestätigung erhalten für ID[{reference}]: StatusCode [{delviveryStatus }]: " + (delviveryStatus > 63 ? "Senden fehlgeschlagen" : delviveryStatus > 31 ? "senden im Gange" : "erfolgreich versendet"));
                    newReports.Add(report);

                }
#pragma warning disable CA1031 // Do not catch general exception types
                catch (Exception)
                {
#if DEBUG
                    throw;
#else
                    Log.Error("Fehler beim Abrufen von Statusreports aus GSM-Modem.", 42015);
#endif
                }
#pragma warning restore CA1031 // Do not catch general exception types
            }

            TrackSentSms(newReports);

            #endregion
        }

        /// <summary>
        /// Werte erhaltene Statusreports (Empfangsbestätigungen) aus. Nimmt erfolgreich versendete Nachrichten aus der TrackingList
        /// </summary>
        /// <param name="newReports"></param>
        private static void TrackSentSms(List<Report> newReports)
        {
            List<SmsOut> deleteFromTracking = new List<SmsOut>();

            foreach (Report report in newReports)
            {
                SmsReportEvent?.Invoke(null, report); // Tabelle 'Sent' aktualisieren 

                if (trackingList.Count == 0)
                    Log.Warning($"Es wurde eine Empfangsbestätigung für SMS-Referenz [{report.Reference}] empfangen, obwohl keine erwartet wurde. Dies kann nach einem Neustart passieren.", 913);
                else
                {                 
                    foreach (SmsOut tracking in trackingList)
                    {
                        if (tracking.Reference == report.Reference)                        
                            deleteFromTracking.Add(tracking);                                                   
                    }
                }
#if DEBUG
                Console.WriteLine($"StatusReports für Refernez [{report.Reference}] an Index [{report.Index}] wird aus dem Modem gelöscht. Sendestatus = " + report.DeliveryStatus);
#endif
                SmsDelete(report.Index);

            }

            foreach (SmsOut delete in deleteFromTracking) //lösche hier, denn Liste darf erste nach for..each Loop verändert werrden.
            {
                trackingList.Remove(delete);
            }
        }


        /// <summary>
        /// Wird aufgerugfen, wenn eine ausgehende SMS 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void CurrentSendSms_SmsSentTimeout(object sender, SmsOut e)
        {
            if (!trackingList.Contains(e)) return;

            trackingList.Remove(e);

            string timeoutText = $"Keine Empfangsbestätigung seit {e.SendTimeUtc.ToLocalTime()} ({ (DateTime.UtcNow - e.SendTimeUtc).TotalMinutes } min.), Sendeversuch Nr. {e.SendTryCounter} für SMS mit ID[{e.Reference}] an >{e.Phone}< >{e.Message}<.";
            Console.WriteLine(timeoutText);

            if (e.SendTryCounter < MaxSendTrysPerSms)
            {
                //Erneut versuchen zu Senden
                Log.Warning(timeoutText, 51708);
                sendList.Enqueue(e);
            }
            else if (e.SendTryCounter == MaxSendTrysPerSms)
            {
                //Senden entgültig fehlgeschlagen; Admin informieren! 
                Log.Error($"SMS-Nachricht >{e.Message}< nach {e.SendTryCounter} Versuchen an >{e.Phone}< unzustellbar. Wird deshalb an Admin >{AdminPhone}< gesendet: >{e.Message}<", 51704);

                e.Phone = AdminPhone;
                sendList.Enqueue(e);
            }
            else
            {
                //Auch Senden an Admin ist fehlgeschlagen
                Log.Error($"SMS-Nachricht >{e.Message}< konnte nach {e.SendTryCounter} Versuchen weder an Empfänger >{e.Phone}< noch an Admin gesendet werden. Die Nachricht wird verworfen!", 51705);
            }

            FailedSmsSendEvent?.Invoke(null, e);
        }
    }

}

