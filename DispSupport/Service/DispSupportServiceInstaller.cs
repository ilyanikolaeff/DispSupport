using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration.Install;
using System.Linq;
using System.ServiceProcess;
using System.Threading.Tasks;

namespace DispSupport
{
    [RunInstaller(true)]
    public partial class DispSupportServiceInstaller : Installer
    {
        public DispSupportServiceInstaller()
        {
            InitializeComponent();

            var spi = new ServiceProcessInstaller();
            var si = new ServiceInstaller();

            spi.Account = ServiceAccount.LocalSystem;
            spi.Username = null;
            spi.Password = null;

            si.DisplayName = DispSupportService.WindowsServiceName;
            si.ServiceName = DispSupportService.WindowsServiceName;
            si.StartType = ServiceStartMode.Automatic;

            Installers.Add(spi);
            Installers.Add(si);
        }
    }
}
