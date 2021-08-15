using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MelBoxGsm
{
    public partial class Gsm
    {

        /// <summary>
        /// Fragt die Mobilnetzempfangsqualität ab. 
        /// </summary>
        /// <returns>WAHR, wenn sich die Signalqualität zur vorigen Abfrage verändert hat.</returns>
        private static bool HasSignalQualityChanged()
        {
            string answer = Port.Ask("AT+CSQ");
            MatchCollection mc = Regex.Matches(answer, @"\+CSQ: (\d+),(\d+)");

            if (mc.Count > 0 && int.TryParse(mc[0].Groups[1].Value, out int quality))
            {
                if (SignalQuality != quality)
                {
                    SignalQuality = quality;
                    return true;
                }
            }

            return false;
        }

        private static bool HasNetworkRegistrationChanged() //TESTEN
        {
            bool result = false;
            string answer = Port.Ask("AT+CREG?");
            MatchCollection mc = Regex.Matches(answer, @"\+CREG: (\d),(\d)");

            if (mc.Count == 0) return result;

            if (int.TryParse(mc[0].Groups[1].Value, out int mode))
            {
                if (IsNetworkRegistrationNotificationActive != (mode > 0))
                {
                    IsNetworkRegistrationNotificationActive = (mode > 0);

                    result = true;
                }
            }

            string networkRegistration = string.Empty;

            if (int.TryParse(mc[0].Groups[2].Value, out int status))
            {
                switch (status)
                {
                    case 0:
                        networkRegistration = "nicht registriert";
                        break;
                    case 1:
                        networkRegistration = "registriert";
                        break;
                    case 2:
                        networkRegistration = "suche Netz";
                        break;
                    case 3:
                        networkRegistration = "verweigert";
                        break;
                    case 4:
                        networkRegistration = "unbekannt";
                        break;
                    case 5:
                        networkRegistration = "Roaming";
                        break;
                }
            }

            if (NetworkRegistration != networkRegistration)
            {
                NetworkRegistration = networkRegistration;
                result = true;
            }

            return result;
        }

    }
}
