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
        public ReliableSerialPort(string portName, int baudRate, Parity parity, int dataBits, StopBits stopBits)
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
                catch
                {
                    Console.WriteLine(base.PortName + " verbleibende Verbindungsversuche: " + Try);
                    System.Threading.Thread.Sleep(2000);
                }
            } while (!base.IsOpen && --Try > 0);
        }

        #endregion

        #region Read
        private void ContinuousRead()
        {
            try
            {
                byte[] buffer = new byte[4096];
                Action kickoffRead = null;
                kickoffRead = (Action)(() => BaseStream.BeginRead(buffer, 0, buffer.Length, delegate (IAsyncResult ar)
                {
                    try
                    {
                        if (!IsOpen) return; //Wenn beim Lesen die Verbindung abbricht.

                        int count = BaseStream.EndRead(ar);
                        byte[] dst = new byte[count];
                        Buffer.BlockCopy(buffer, 0, dst, 0, count);
                        OnDataReceived(dst);
                    }
                    catch (System.OperationCanceledException)
                    {
                        // nichts unternehmen
                    }
                    catch (Exception exception)
                    {
                        Console.WriteLine("ContinuousRead(): Lesefehler Bitstream von COM-Port:\r\n" +
                            ">" + System.Text.Encoding.UTF8.GetString(buffer) + "<" + Environment.NewLine +
                            exception.GetType() + Environment.NewLine +
                            exception.Message + Environment.NewLine +
                            exception.InnerException + Environment.NewLine +
                            exception.Source + Environment.NewLine +
                            exception.StackTrace);

                        Log.Error($"Lesefehler COM-Port in Bitstream bei >{buffer}<", 2108141918);
                    }

                    kickoffRead();
                }, null)); kickoffRead();
            }
            catch (Exception exception)
            {
                Console.WriteLine("ContinuousRead(): Lesefehler bei Beginn. COM-Port:\r\n" +
                    exception.GetType() + Environment.NewLine +
                    exception.Message + Environment.NewLine +
                    exception.InnerException + Environment.NewLine +
                    exception.Source + Environment.NewLine +
                    exception.StackTrace);

                Log.Error($"Lesefehler an COM-Port: {exception.Message}", 2108141919);
            }
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
                if (_wait.Set()) Console.WriteLine("Set");

        }

        #endregion

        #region Write

        private readonly AutoResetEvent _wait = new AutoResetEvent(false);

        public string Ask(string request, int timeout = 3000)
        {
            if (!IsOpen) Open();

            base.DiscardOutBuffer();
            base.DiscardInBuffer();

            base.WriteLine(request);

            _wait.Reset();
#if DEBUG
            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.WriteLine(request);
            Console.ForegroundColor = ConsoleColor.Gray;
#endif


            if (!_wait.WaitOne(timeout))
            {
#if DEBUG
                Console.WriteLine("Timeout");
#endif
            }
#if DEBUG
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.WriteLine(recLine);
            Console.ForegroundColor = ConsoleColor.Gray;
#endif
            string x = recLine;
            recLine = string.Empty;
            return x;
        }

        #endregion

    }
}
