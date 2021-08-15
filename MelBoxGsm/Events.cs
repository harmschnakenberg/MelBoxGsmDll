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
        /// Der Zuletzt vom Modem gemeldete Fehler mit Zeit der Meldung
        /// </summary>
        public static Tuple<DateTime, string> LastError { get; private set; }

        /// <summary>
        /// Wird ausgelöst, wenn vom GSM-Modem eine Fehlermeldung zurückggegeben wird.
        /// </summary>
        public static event EventHandler<string> NewErrorEvent;

        /// <summary>
        /// Wird ausgelöst, wenn eine SMS vom GSM-Modem losgeschickt wurde.
        /// </summary>
        public static event EventHandler<SmsOut> SmsSentEvent;

        /// <summary>
        /// Wird ausgelöst, wenn ein Statusreport (Empfangsbestätigung) einer gesendeten SMS empfangen wird.
        /// </summary>
        public static event EventHandler<Report> SmsReportEvent;

        /// <summary>
        /// Wird ausgelöst, wenn sich der Status für Signalstärke oder Anmeldestatus beim Mobilfunknetz ändert.
        /// </summary>
        public static event EventHandler NetworkStatusEvent;


        /// <summary>
        /// Wird ausgelöst, wenn für eine gesendete SMS keine Empfangsbestätigung bis zum Timeout empfangen wird.
        /// </summary>
        public static event EventHandler<SmsOut> FailedSmsSendEvent;


    }
}
