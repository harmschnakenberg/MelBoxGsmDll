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

        internal static void ParseErrorResponse(string answer)
        {
            MatchCollection mc = Regex.Matches(answer, @"\+CM[ES] ERROR: (.+)");
            if (mc.Count > 0)
                LastError = new Tuple<DateTime, string>(DateTime.Now, mc[0].Groups[1].Value.TrimEnd('\r'));

            NewErrorEvent?.Invoke(null, $"{LastError.Item1.ToShortTimeString()}: {LastError.Item2}");
        }
    }
}
