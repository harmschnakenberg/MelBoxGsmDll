using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MelBoxGsm
{
    public partial class Gsm
    {

        /// <summary>
        /// Liste gesendeter SMS zur Nachverfolgung / Warten auf Empfangsbestätigung
        /// </summary>
        private static readonly List<SmsOut> trackingList = new List<SmsOut>();

        private static void TrackSentSms(List<Report> newReports)
        {
            List<SmsOut> deleteFromTracking = new List<SmsOut>();

            foreach (Report report in newReports)
            {
                foreach (SmsOut tracking in trackingList)
                {
                    if (tracking.Reference == report.Reference)
                    {           
                        SmsReportEvent?.Invoke(null, report);
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
                            Log.Warning(timeoutText, 2108151708);

                            tracking.SendTryCounter++;
                            sendList.Enqueue(tracking);
                        }
                        else if (MaxSendTrysPerSms == tracking.SendTryCounter)
                        {
                            //Senden entgültig fehlgeschlagen; Admin informieren! 
                            Log.Error($"SMS-Nachricht nach {tracking.SendTryCounter} Versuchen an >{tracking.Phone}< unzustellbar. Wird deshalb an Admin >{AdminPhone}< gesendet: >{tracking.Message}<", 2108151704);

                            tracking.Phone = AdminPhone;
                            tracking.SendTryCounter++;
                            sendList.Enqueue(tracking);
                        }
                        else
                        {
                            //Auch Senden an Admin ist fehlgeschlagen
                            Log.Error($"SMS-Nachricht konnte nach {tracking.SendTryCounter} Versuchen weder an Empfänger noch an Admin gesendet werden. Die Nachricht wird verworfen!", 2108151705);
                        }

                        deleteFromTracking.Add(tracking);
                    }
                }
            }

            foreach (SmsOut delete in deleteFromTracking) //lösche hier, denn Liste darf erste nach for..each Loop verändert werrden.
            {
                trackingList.Remove(delete);
            }
        }

    }

}

