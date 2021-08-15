using System;

namespace MelBoxGsm
{
    public partial class Gsm
    {

        public class SmsIn
        {
            public int Index { get; set; }
            public string Status { get; set; }
            public DateTime TimeUtc { get; set; }
            public string Phone { get; set; }
            public string Message { get; set; }
        }

        public class SmsOut
        {
            public int Reference { get; set; }
            public DateTime SendTimeUtc { get; set; }
            public string Phone { get; set; }
            public string Message { get; set; }
            public int SendTryCounter { get; set; } = 0;
        }

        public class Report
        {
            public int Index { get; set; }
            public string Status { get; set; }
            public int Reference { get; set; }
            public DateTime ServiceCenterTimeUtc { get; set; }
            public DateTime DischargeTimeUtc { get; set; }
            public int DeliveryStatus { get; set; }
        }

        public class Property
        {
            public Property(string name, object value)
            {
                Name = name;
                Value = value;
            }
            public string Name { get; set; }

            public object Value { get; set; }
        }

    }
}
