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
        /// Zeit in der eine Sendebestätigung vom Mobilfunknetzbetreiber empfangen werden muss. Nach Auflauf dieser Zeit gilt die Zustellung als erfolglos.
        /// </summary>
        public static int TrackingTimeoutMinutes { get; set; } = 5;

        /// <summary>
        /// Anzahl der Sendeversuche. Bei Überschreiten geht die Nachricht an den Admin
        /// </summary>
        public static int MaxSendTrysPerSms { get; set; } = 2;

        public static string AdminPhone { get; set; } = "+4916095285304";

        public static string RelayCallsToPhone { get; set; } = "+4916095285304";

        public static int SignalQuality { get; private set; }

        public static string NetworkRegistration { get; private set; }

        public static bool IsNetworkRegistrationNotificationActive { get; private set; }




    }
}
