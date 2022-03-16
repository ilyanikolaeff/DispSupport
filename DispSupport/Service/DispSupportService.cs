using System.ServiceProcess;

namespace DispSupport
{
        public class DispSupportService : ServiceBase
        {
            public const string WindowsServiceName = "DispSupportService";

            public DispSupportService()
            {
                ServiceName = WindowsServiceName;
            }

            protected override void OnStart(string[] args)
            {
                Program.Start(args);
            }

            protected override void OnStop()
            {
                Program.Stop();
            }
        }
    }