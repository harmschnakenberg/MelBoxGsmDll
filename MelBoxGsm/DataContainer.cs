using System;

namespace MelBoxGsm
{

    public class SmsIn
    {

        public int Index { get; set; }
        public string Status { get; set; }
        public DateTime TimeUtc { get; set; }
       
        private string _Phone = "";
        public string Phone
        {
            get { return (_Phone.Length > 0 ? _Phone : "OHNE NUMMER"); }
            set { _Phone = value; }
        }

        private string _Message = "";
        public string Message
        {
            get { return (_Message.Length > 0 ? _Message : "KEIN TEXT"); }
            set { _Message = value; }
        }

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
