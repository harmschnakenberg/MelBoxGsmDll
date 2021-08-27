using System;
using System.Timers;

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

        #region Sendungsverfolgung
        public int SendTryCounter { get; private set; } = 0;

        public event EventHandler<SmsOut> SmsSentTimeout;

        public void StartTimeout(int minutes)
        {
            SendTryCounter++;

            Timer SendTimeout = new Timer();
            SendTimeout.Interval = minutes * 60000;
            SendTimeout.AutoReset = false;
            SendTimeout.Elapsed += SendTimeout_Elapsed;
            SendTimeout.Start();
        }

        private void SendTimeout_Elapsed(object sender, ElapsedEventArgs e)
        {
            SmsSentTimeout?.Invoke(this, this);
        }

        #endregion
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

    //public class Property
    //{
    //    public Property(string name, object value)
    //    {
    //        Name = name;
    //        Value = value;
    //    }
    //    public string Name { get; set; }

    //    public object Value { get; set; }
    //}


}
