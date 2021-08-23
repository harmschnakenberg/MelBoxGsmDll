using System;
using System.Collections.Generic;

namespace MelBoxGsm
{
    public static partial class Gsm
    {



        /// <summary>
        /// Wird ausgelöst, wenn vom GSM-Modem eine Fehlermeldung zurückggegeben wird.
        /// </summary>
        public static event EventHandler<string> NewErrorEvent;

        /// <summary>
        /// Wird ausgelöst, wenn eine SMS vom GSM-Modem losgeschickt wurde.
        /// </summary>
        public static event EventHandler<SmsOut> SmsSentEvent;

        /// <summary>
        /// Wird ausgelöst, wenn eine SMS vom GSM-Modem empfangen wurde.
        /// </summary>
        public static event EventHandler<List<SmsIn>> SmsRecievedEvent;

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


        public static event EventHandler<string> NewCallRecieved;
    }
}
