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

        public static string AdminPhone { get; set; } = "+4916095285304";

        public static string CallForwardingNumber { get; set; } = "+4916095285304";

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


    }
}
