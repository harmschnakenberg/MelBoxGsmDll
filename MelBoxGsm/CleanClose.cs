using System.Runtime.InteropServices;

namespace MelBoxGsm
{
    public static class CleanClose
    {
        //Quelle: https://stackoverflow.com/questions/474679/capture-console-exit-c-sharp

        [DllImport("Kernel32")]
        public static extern bool SetConsoleCtrlHandler(EventHandler handler, bool add);

        public delegate bool EventHandler(CtrlType sig);
        public static EventHandler CloseConsoleHandler;

        public enum CtrlType
        {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT = 1,
            CTRL_CLOSE_EVENT = 2,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT = 6
        }

        public static bool Handler(CtrlType sig)
        {
            switch (sig)
            {
                case CtrlType.CTRL_C_EVENT:
                case CtrlType.CTRL_LOGOFF_EVENT:
                case CtrlType.CTRL_SHUTDOWN_EVENT:
                case CtrlType.CTRL_CLOSE_EVENT:
                    Log.Info("Programmende erzwungen z.B. Konsole-Fenster mit x geschlossen.", 1011);
                    Gsm.ModemShutdown();                    
                    return true;
                default:
                    return false;
            }
        }


//        static void Main(string[] args)
//        {
//            // Some biolerplate to react to close window event
//            CloseConsoleHandler += new EventHandler(Handler);
//            SetConsoleCtrlHandler(CloseConsoleHandler, true);
//            ...
//}
    }

}
