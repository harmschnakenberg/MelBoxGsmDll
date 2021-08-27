using System;
using System.IO.Ports;
using System.Threading;

namespace MelBoxGsm
{
    /// <summary>
    /// Klasse bietet grundlegende Verbindung, Schreib- und Lesevorgänge über COM-Port.
    /// </summary>
    public class ReliableSerialPort : SerialPort
    {
        #region Connection
        public ReliableSerialPort(string portName = "", int baudRate = 38400, Parity parity = Parity.None, int dataBits = 8, StopBits stopBits = StopBits.One)
        {
            #region COM-Port verifizieren
            string[] ports = System.IO.Ports.SerialPort.GetPortNames();

            if (ports.Length == 0)
            {
                throw new Exception("Es sind keine COM-Ports vorhanden.");
            }

            if (!Array.Exists(ports, x => x == portName))
            {
                int pos = ports.Length - 1;
                portName = ports[pos];
            }
            #endregion

            PortName = portName;
            BaudRate = baudRate;
            DataBits = dataBits;
            Parity = parity;
            StopBits = stopBits;
            Handshake = Handshake.None;
            DtrEnable = true;
            NewLine = Environment.NewLine;
            ReceivedBytesThreshold = 1024;
            WriteTimeout = 300;
            ReadTimeout = 500;

        }

        public static GsmDebug Debug { get; set; } = GsmDebug.UnsolicatedResult;

        [Flags]
        public enum GsmDebug : byte
        {
            AnswerGsm = 1,
            RequestGsm = 2,
            UnsolicatedResult = 4
        }

        new public void Open()
        {
            int Try = 10;

            do
            {
                try
                {
                    base.Open();
                    ContinuousRead();
                }
#pragma warning disable CA1031 // Do not catch general exception types
                catch
                {
                    Console.WriteLine(base.PortName + " verbleibende Verbindungsversuche: " + Try);
                    System.Threading.Thread.Sleep(2000);
                }
#pragma warning restore CA1031 // Do not catch general exception types
            } while (!base.IsOpen && --Try > 0);

            if (!base.IsOpen)
            {
                string errorText = $"Der COM-Port {base.PortName} ist nicht bereit. Das Programm wird beendet.";
                Console.WriteLine(errorText);
                base.Close();
                Log.Error(errorText, 102);                
                Environment.Exit(102);
            }
        }

        #endregion

        #region Read

        private void ContinuousRead()
        {
            ThreadPool.QueueUserWorkItem(async delegate (object unused)
            {
                byte[] buffer = new byte[4096];

                while (true)
                {
                    try
                    {
                        if (!base.IsOpen) return;
                        int count = await BaseStream.ReadAsync(buffer, 0, buffer.Length);
                        byte[] dst = new byte[count];
                        Buffer.BlockCopy(buffer, 0, dst, 0, count);
                        OnDataReceived(dst);
                    }
#pragma warning disable CA1031 // Do not catch general exception types
                    catch (Exception ex)
                    {
                        Console.WriteLine("ContinuousRead(): Lesefehler an COM-Port:\r\n" +
                        ex.GetType() + Environment.NewLine +
                        ex.Message + Environment.NewLine +
                        ex.InnerException + Environment.NewLine +
                        ex.Source + Environment.NewLine +
                        ex.StackTrace);

                        Log.Error($"Lesefehler an COM-Port: {ex.Message}", 41917);
                    }
#pragma warning restore CA1031 // Do not catch general exception types
                }
            });
        }

        public const string Terminator = "\r\nOK\r\n";
        static string recLine = string.Empty;

        /// <summary>
        /// Sammelt die vom Modem empfangenen Daten für die Weiterleitung
        /// </summary>
        /// <param name="data"></param>
        public virtual void OnDataReceived(byte[] data)
        {
            recLine += System.Text.Encoding.UTF8.GetString(data);

            if (recLine.Contains("ERROR"))
            {
                Gsm.ParseErrorResponse(recLine);
                _wait.Set();
            }

            //Melde empfangne Daten, wenn...
            if (recLine.Contains(Terminator))
                _wait.Set();
            //if (_wait.Set()) Console.WriteLine("Set");

            if (recLine.Contains("^SCKS: ") || recLine.Contains("+CMTI: ") || recLine.Contains("+CDSI: ") || recLine.Contains("+CLIP: "))
                UnsolicatedEvent();
        }

        #endregion

        #region Write

        private readonly AutoResetEvent _wait = new AutoResetEvent(false);

        public string Ask(string request, int timeout = 3000)
        {
            if (!base.IsOpen) Open();
            if (!base.IsOpen) return recLine;

            base.WriteLine(request);

            _wait.Reset();

            if (Debug.HasFlag(GsmDebug.RequestGsm))
            {
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine(request);
                Console.ForegroundColor = ConsoleColor.Gray;
            }

            if (!_wait.WaitOne(timeout))
            {
#if DEBUG
                Console.WriteLine("Timeout");
#endif
            }

            if (Debug.HasFlag(GsmDebug.AnswerGsm))
            {
                Console.ForegroundColor = ConsoleColor.DarkGreen;
                Console.WriteLine(recLine);
                Console.ForegroundColor = ConsoleColor.Gray;
            }

            string x = recLine;
            recLine = string.Empty;
            return x;
        }

        #endregion

        private void UnsolicatedEvent()
        {
            if (Debug.HasFlag(GsmDebug.UnsolicatedResult))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(recLine);
                Console.ForegroundColor = ConsoleColor.Gray;
            }

            Gsm.UnsolicatedEvent(recLine);
        }
    }
}
