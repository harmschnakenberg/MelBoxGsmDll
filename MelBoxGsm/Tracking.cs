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

                    Console.WriteLine($"Empfangsbestätigung erhalten für ID[{reference}]: " + (delviveryStatus > 63 ? "Senden fehlgeschlagen" : delviveryStatus > 31 ? "senden im Gange" : "erfolgreich versendet"));
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


        private static void TrackSentSms(List<Report> newReports)
        {
            List<SmsOut> deleteFromTracking = new List<SmsOut>();

            foreach (Report report in newReports)
            {
                if (trackingList.Count == 0)
                {
                    SmsReportEvent?.Invoke(null, report); //Trotzdem versuchen in Datenbank zu aktualisieren.
                    Log.Warning($"Es wurde eine Empfangsbestätigung für SMS-Referenz [{report.Reference}] empfangen, obwohl keine erwartet wurde. Dies kann nach einem Neustart passieren.", 913);
                }
                else
                {
                    foreach (SmsOut tracking in trackingList)
                    {

                        SmsReportEvent?.Invoke(null, report);

                        if (tracking.Reference == report.Reference)
                        {
                            deleteFromTracking.Add(tracking);
                        }

                        if (tracking.SendTimeUtc.CompareTo(System.DateTime.UtcNow.AddMinutes(-TrackingTimeoutMinutes)) < 0) //TESTEN!
                        {
                            string timeoutText = $"Keine Empfangsbestätigung seit über {TrackingTimeoutMinutes} Minuten. Sendeversuch Nr. {tracking.SendTryCounter} für SMS mit ID[{tracking.Reference}] an >{tracking.Phone}< >{tracking.Message}<.";

                            Console.WriteLine(timeoutText);

                            FailedSmsSendEvent?.Invoke(null, tracking);

                            if (MaxSendTrysPerSms < tracking.SendTryCounter)
                            {
                                //Erneut versuchen zu Senden
                                Log.Warning(timeoutText, 51708);

                                tracking.SendTryCounter++;
                                sendList.Enqueue(tracking);
                            }
                            else if (MaxSendTrysPerSms == tracking.SendTryCounter)
                            {
                                //Senden entgültig fehlgeschlagen; Admin informieren! 
                                Log.Error($"SMS-Nachricht nach {tracking.SendTryCounter} Versuchen an >{tracking.Phone}< unzustellbar. Wird deshalb an Admin >{AdminPhone}< gesendet: >{tracking.Message}<", 51704);

                                tracking.Phone = AdminPhone;
                                tracking.SendTryCounter++;
                                sendList.Enqueue(tracking);
                            }
                            else
                            {
                                //Auch Senden an Admin ist fehlgeschlagen
                                Log.Error($"SMS-Nachricht konnte nach {tracking.SendTryCounter} Versuchen weder an Empfänger noch an Admin gesendet werden. Die Nachricht wird verworfen!", 51705);
                            }

                            deleteFromTracking.Add(tracking);
                        }

                    }
                }
#if DEBUG
                Console.WriteLine($"StatusReports für Refernez [{report.Reference}] an Index [{report.Index}] wird aus dem Modem gelöscht.");
#endif
                SmsDelete(report.Index);

            }

            foreach (SmsOut delete in deleteFromTracking) //lösche hier, denn Liste darf erste nach for..each Loop verändert werrden.
            {
                trackingList.Remove(delete);
            }
        }


    }

}

