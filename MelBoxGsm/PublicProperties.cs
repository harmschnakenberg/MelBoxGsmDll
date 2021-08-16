using System;
using System.Linq;

namespace MelBoxGsm
{
    public partial class Gsm
    {
        public static string SerialPortName { get; set; } = System.IO.Ports.SerialPort.GetPortNames().LastOrDefault();

        /// <summary>
        /// Zeit in der eine Sendebestätigung vom Mobilfunknetzbetreiber empfangen werden muss. Nach Auflauf dieser Zeit gilt die Zustellung als erfolglos.
        /// </summary>
        public static int TrackingTimeoutMinutes { get; set; } = 5;

        /// <summary>
        /// Anzahl der Sendeversuche. Bei Überschreiten geht die Nachricht an den Admin
        /// </summary>
        public static int MaxSendTrysPerSms { get; set; } = 2;

        /// <summary>
        /// Muss 
        /// </summary>
        public static string AdminPhone { get; set; }

        public static string CallForwardingNumber { get; set; }

        public static int SignalQuality { get; private set; }

        public static string NetworkRegistration { get; private set; }

        public static bool IsNetworkRegistrationNotificationActive { get; private set; }

        public static bool IsGsmTextMode { get; private set; }

        public static string ModemType { get; private set; }

        public static string OwnNumber { get; private set; }

        public static string OwnName { get; private set; }

        public static string ProviderName { get; private set; }

        public static string SmsServiceCenterAddress { get; private set; }

        public static int SmsStorageCapacity { get; private set; }

        public static int SmsStorageCapacityUsed { get; private set; }

        public static string SimPinStatus { get; private set; }

        public static bool CallForwardingActive { get; private set; }

        /// <summary>
        /// Der Zuletzt vom Modem gemeldete Fehler mit Zeit der Meldung
        /// </summary>
        public static Tuple<DateTime, string> LastError { get; private set; }
    }
}
