using System;
using System.Linq;

namespace MelBoxGsm
{
    public static partial class Gsm
    {
        private const string def = "-unbekannt-";

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
        /// Muss eine gültige Telefonnummer sein
        /// </summary>
        public static string AdminPhone { get; set; } = def;

        /// <summary>
        /// Telefonnummer an die Sprachanrufe weitergeleitet werden sollen.
        /// </summary>
        public static string CallForwardingNumber { get; set; } = def;

        /// <summary>
        /// Mobilfunknetzqualität in %
        /// </summary>
        public static int SignalQuality { get; private set; }

        public enum Registration
        {
            NotRegistered = 0,
            Registerd = 1,
            Searching = 2,
            Denied = 3,
            Unknown = 4,
            Roaming = 5
        }

        /// <summary>
        /// Registrierungsstatus im Mobilfunknetz
        /// </summary>
        public static Registration NetworkRegistration { get; private set; } = Registration.Unknown;

        public static string RegToString(this Registration reg )
        {
            switch ((int) reg)
            {
                case 0:
                    return "nicht registriert";
                case 1:
                    return "registriert";
                case 2:
                    return "suche Netz";
                case 3:
                    return "Verbindung verweigert";
                case 4:
                    return "unbekannt";
                case 5:
                    return "Roaming";
                default:
                    return "-ungültig-";
            }
        }

        /// <summary>
        /// true = Änderungen im Registrierungsstatus werden unaufgefordert vom Modem angezeigt.
        /// </summary>
        public static bool IsNetworkRegistrationNotificationActive { get; private set; }

        public static bool IsGsmTextMode { get; private set; }

        public static string ModemType { get; private set; } = def;

        public static string OwnNumber { get; private set; } = def;

        public static string OwnName { get; private set; } = def;

        public static string ProviderName { get; private set; } = def;

        public static string SmsServiceCenterAddress { get; private set; } = def;

        public static int SmsStorageCapacity { get; private set; }

        public static int SmsStorageCapacityUsed { get; private set; }

        public static string SimPinStatus { get; private set; } = def;

        public static string SimPin { get; set; } = "0000";

        public static bool CallForwardingActive { get; private set; } = false;

        public static int RingSecondsBeforeCallForwarding { get; set; } = 5;

        public static string GsmCharacterSet { get; set; } = "GSM";

        /// <summary>
        /// Der Zuletzt vom Modem gemeldete Fehler mit Zeit der Meldung
        /// </summary>
        public static Tuple<DateTime, string> LastError { get; private set; } = new Tuple<DateTime, string>(DateTime.Now, "seit Neustart kein Fehler");
    }
}
