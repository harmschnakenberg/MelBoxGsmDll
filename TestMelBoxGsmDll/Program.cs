using MelBoxGsm;
using System;
using System.Collections.Generic;

namespace TestMelBoxGsmDll
{
    class Program
    {
        static void Main()
        {
            Gsm.AdminPhone = "+49...";

            Gsm.SetupModem();

            List<SmsIn> smsIn = Gsm.SmsRead();

            foreach (SmsIn sms in smsIn)
            {
                Console.WriteLine(sms.Message);
            }

        }
    }
}
