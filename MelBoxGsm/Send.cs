using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace MelBoxGsm
{
    public static partial class Gsm
    {

        //Frage: Senden in UCS2-Encoding?
        //siehe Quelle: https://www.smssolutions.net/tutorials/gsm/sendsmsat/


        const string ctrlz = "\u001a";

        private static readonly ReliableSerialPort Port = new ReliableSerialPort(SerialPortName);


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

            Match m = Regex.Match(answer, @"\+CMGS: (\d+)");

            currentSendSms.SendTimeUtc = DateTime.UtcNow;
            currentSendSms.SendTryCounter++;

            if (m.Success && int.TryParse(m.Groups[1].Value, out int reference))
            {
                currentSendSms.Reference = reference;

                trackingList.Add(currentSendSms);

#if DEBUG
                Console.WriteLine($"SMS-Referenz [{currentSendSms.Reference}] vergeben für gesendet >{currentSendSms.Phone}< >{currentSendSms.Message}<");
#endif
            }
            else
            {
                Log.Warning($"Der gesendeten SMS an >{currentSendSms.Phone}< >{currentSendSms.Message}< konnte keine Referenz zugeordnet werden. Es kann keine Empfangsbestätigung empfangen werden.", 815);
            }

            SmsSentEvent?.Invoke(null, currentSendSms);
            currentSendSms = null;

            SendList();
        }


    }
}
