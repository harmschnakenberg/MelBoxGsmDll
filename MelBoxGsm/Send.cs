using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Timers;

namespace MelBoxGsm
{
    public partial class Gsm
    {
        const string ctrlz = "\u001a";

        private static readonly ReliableSerialPort Port = null;

        /// <summary>
        /// Timer für zyklische Modemabfrage
        /// </summary>
        static readonly Timer askingTimer = new Timer(19000);

        /// <summary>
        /// Zum Senden anstehende SMS
        /// </summary>
        private static readonly Queue<SmsOut> sendList = new Queue<SmsOut>();

        /// <summary>
        /// Zurzeit zum Senden bearbeitete SMS
        /// </summary>
        private static SmsOut currentSendSms = null;

        /// <summary>
        /// Arbeitet die Liste der zu sendenden SMSen ab.
        /// </summary>
        private static void SendList()
        {
            if (currentSendSms != null || sendList.Count == 0) return;

            currentSendSms = sendList.Dequeue();

            _ = Port.Ask("AT+CMGS=\"" + currentSendSms.Phone + "\"\r");
            string answer = Port.Ask(currentSendSms.Message + ctrlz, 5000);

            MatchCollection mc = Regex.Matches(answer, @"\+CMGS: (\d+)");

            if (mc.Count > 0 && int.TryParse(mc[0].Groups[1].Value, out int reference))
            {
                currentSendSms.Reference = reference;
                currentSendSms.SendTimeUtc = DateTime.UtcNow;
                currentSendSms.SendTryCounter++;

                trackingList.Add(currentSendSms);

                SmsSentEvent?.Invoke(null, currentSendSms);
                
#if DEBUG
                Console.WriteLine($"SMS-Refernez [{currentSendSms.Reference}] vergeben für gesendet >{currentSendSms.Phone}< >{currentSendSms.Message}<");
#endif
                currentSendSms = null;
            }

            SendList();
        }


    }
}
