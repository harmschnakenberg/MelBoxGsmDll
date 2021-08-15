using MelBoxGsm;
using System;
using System.Collections.Generic;

namespace TestMelBoxGsmDll
{
    class Program
    {
        static void Main()
        {
            Gsm.AdminPhone = "+4916095285304";
            Gsm.CallForwardingNumber = "+4916095285304";

            Gsm.SetupModem();

            List<Gsm.SmsIn> smsIn = Gsm.SmsRead();

            foreach (Gsm.SmsIn sms in smsIn)
            {
                Console.WriteLine(sms.Message);
            }

        }
    }
}
