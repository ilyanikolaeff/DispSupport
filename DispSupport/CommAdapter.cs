using NLog;
using System;
using System.Threading;

namespace DispSupport
{
    class CommAdapter
    {
        public OPCClient OpcClient { get; set; }
        public ABClient PlcClient { get; set; }

        private static Logger _logger = LogManager.GetCurrentClassLogger();

        public CommAdapter()
        {
        }
        public void Initialize(OPCDAConnectionSettings opcConn, PLCConnectionSettings plcConn)
        {
            OpcClient = new OPCClient(opcConn);
            PlcClient = new ABClient(plcConn);
        }
    }
}
