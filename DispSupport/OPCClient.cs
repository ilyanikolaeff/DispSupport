using NLog;
using Opc;
using Opc.Da;
using System;
using System.Collections.Generic;
using System.Timers;

namespace DispSupport
{
    class OPCClient
    {
        private Opc.Da.Server OpcDaServer { get; set; }
        private List<Subscription> SubscriptionGroups { get; set; }
        private string _clientName { get; set; }

        private static Logger _logger = LogManager.GetCurrentClassLogger();

        public delegate void OnItemsValuesChangedDelegate(IDictionary<string, object> itemsValuesResults);
        private OnItemsValuesChangedDelegate _itemValueChangedDelegate;

        private readonly OPCDAConnectionSettings _connectionSettings;

        private Timer _checkServerStateTimer;

        public OPCClient(OPCDAConnectionSettings connectionSettings)
        {
            _clientName = connectionSettings.Name;
            _connectionSettings = connectionSettings;

            if (_connectionSettings.IP == "localhost" || _connectionSettings.IP == "127.0.0.1")
                _connectionSettings.IP = string.Empty;

            var opcUrl = new URL("opcda://" + _connectionSettings.IP + "/" + _connectionSettings.ServerName);
            OpcDaServer = new Opc.Da.Server(new OpcCom.Factory(), opcUrl);
            OpcDaServer.Connect();
            SubscriptionGroups = new List<Subscription>();

            if (_connectionSettings.ServerName.ToLower() != "elesy.dualsource")
                StartTimerForCheckServerState();
        }

        public void Disconnect()
        {
            if (OpcDaServer != null)
            {
                OpcDaServer.Disconnect();

                if (_checkServerStateTimer != null && _checkServerStateTimer.Enabled)
                {
                    _checkServerStateTimer.Stop();
                    _checkServerStateTimer.Dispose();
                    _logger.Debug($"[{_clientName}] [{OpcDaServer.Url}] Таймер для проверки состояния OPC сервера остановлен");
                }
            }

        }
        public void Subscribe(IEnumerable<string> subscribeTags,
            string groupName,
            int updateRate,
            DataChangedEventHandler dataChangedEventHandler,
            OnItemsValuesChangedDelegate onItemValueChangedDelegate)
        {
            if (OpcDaServer.IsConnected)
            {
                // Group
                SubscriptionState subscriptionState = new SubscriptionState();
                subscriptionState.Name = groupName;
                subscriptionState.UpdateRate = updateRate;
                subscriptionState.Active = true;
                Subscription subscriptionGroup = (Subscription)OpcDaServer.CreateSubscription(subscriptionState);

                // Add items
                var opcDaItemsCollection = new List<Item>();
                foreach (var tag in subscribeTags)
                {
                    opcDaItemsCollection.Add(new Item(new ItemIdentifier(tag)));

                    if (AppSettings.DEBUG_OPC)
                        _logger.Debug($"Subscribing on -> {tag}");
                }

                var opcDaItemsResultsArray = subscriptionGroup.AddItems(opcDaItemsCollection.ToArray());
                subscriptionGroup.DataChanged += dataChangedEventHandler;
                _itemValueChangedDelegate = onItemValueChangedDelegate;

                SubscriptionGroups.Add(subscriptionGroup);
            }
        }

        public void Unsubscribe()
        {
            if (SubscriptionGroups.Count > 0)
                foreach (var subGroup in SubscriptionGroups)
                    OpcDaServer.CancelSubscription(subGroup);
        }

        public void OnItemValueChanged(object subscriptionHandle, object requestHandle, ItemValueResult[] itemsValuesResults)
        {
            var tempDictionary = new Dictionary<string, object>();
            foreach (var itemValueResult in itemsValuesResults)
            {
                if (AppSettings.DEBUG_OPC)
                    _logger.Debug($"[{_clientName}] Changed value -> {itemValueResult.ItemName} : {itemValueResult.Value}");

                tempDictionary.Add(itemValueResult.ItemName, itemValueResult.Value);
            }

            _itemValueChangedDelegate?.Invoke(tempDictionary);
        }

        public IdentifiedResult[] WriteData(Dictionary<string, object> tagsValues)
        {
            ItemValue[] itemValueArray = new ItemValue[tagsValues.Count];
            int i = 0;
            foreach (var tagValue in tagsValues)
            {
                itemValueArray[i] = new ItemValue(new Opc.ItemIdentifier(tagValue.Key));
                itemValueArray[i].Value = tagValue.Value;
                itemValueArray[i].Timestamp = DateTime.Now;
                itemValueArray[i].Quality = Quality.Good;
                i++;
            }
            IdentifiedResult[] identifiedResultsArray = OpcDaServer.Write(itemValueArray);

            if (AppSettings.DEBUG_OPC)
            {
                for (int j = 0; j < identifiedResultsArray.Length; j++)
                    _logger.Debug($"[{_clientName}] Result of writing -> {identifiedResultsArray[j].ItemName} " +
                        $": {tagsValues[identifiedResultsArray[j].ItemName]} " +
                        $"= {identifiedResultsArray[j].ResultID}");
            }
            return identifiedResultsArray;
        }

        public IdentifiedResult[] WriteDataByGroup(Dictionary<string, object> tagsValues)
        {
            var subState = new SubscriptionState();
            subState.Active = true;
            subState.Deadband = 10000;
            subState.UpdateRate = 50;
            subState.Name = "WriteDataGroup (New)";
            var writeGroup = (Subscription)OpcDaServer.CreateSubscription(subState);

            var items = new Item[tagsValues.Count];
            int index = 0;
            foreach (var tagValue in tagsValues)
            {
                items[index] = new Item(new ItemIdentifier(tagValue.Key));
                items[index].ItemName = tagValue.Key;
                index++;
            }
            writeGroup.AddItems(items);


            ItemValue[] itemValueArray = new ItemValue[tagsValues.Count];
            int i = 0;
            foreach (var tagValue in tagsValues)
            {
                itemValueArray[i] = new ItemValue(new Opc.ItemIdentifier(tagValue.Key));
                itemValueArray[i].Value = tagValue.Value;
                itemValueArray[i].Timestamp = DateTime.Now;
                itemValueArray[i].Quality = Quality.Good;
                itemValueArray[i].ServerHandle = writeGroup.Items[i].ServerHandle;
                i++;
            }

            IdentifiedResult[] identifiedResultsArray = writeGroup.Write(itemValueArray);

            if (AppSettings.DEBUG_OPC)
            {
                for (int j = 0; j < identifiedResultsArray.Length; j++)
                    _logger.Debug($"[{_clientName}] Result of writing -> {identifiedResultsArray[j].ItemName} " +
                        $": {tagsValues[identifiedResultsArray[j].ItemName]} " +
                        $"= {identifiedResultsArray[j].ResultID}");
            }
            return identifiedResultsArray;
        }

        public ItemValueResult[] ReadData(IEnumerable<string> tags)
        {
            var items = new List<Item>();
            foreach (var tag in tags)
            {
                items.Add(new Item(new ItemIdentifier(tag)));
            }
            var itemValueResults = OpcDaServer.Read(items.ToArray());

            if (AppSettings.DEBUG_OPC)
            {
                foreach (var itemValueResult in itemValueResults)
                    _logger.Debug($"[{_clientName}] Result of reading -> {itemValueResult.ItemName} : {itemValueResult.Value} = {itemValueResult.ResultID}");
            }


            return itemValueResults;
        }

        private void StartTimerForCheckServerState()
        {
            _checkServerStateTimer = new Timer(AppSettings.CHECK_OPC_TIMEOUT);
            _checkServerStateTimer.AutoReset = true;
            _checkServerStateTimer.Elapsed += CheckServerStateTimer_Elapsed;
            _checkServerStateTimer.Start();
            _logger.Debug($"[{_clientName}] [{OpcDaServer.Url}] Таймер для проверки состояния подключения к OPC серверу запущен");
        }

        private void CheckServerStateTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (OpcDaServer != null)
            {
                if (OpcDaServer.IsConnected)
                {
                    _logger.Debug($"[{_clientName}] [{OpcDaServer.Url}] Подключение к серверу в порядке. Состояние сервера: {OpcDaServer.GetStatus().ServerState}");
                }
                else
                {
                    try
                    {
                        OpcDaServer.Connect();
                        _logger.Debug($"[{_clientName}] [{OpcDaServer.Url}] Переподключен к серверу. Состояние сервера: {OpcDaServer.GetStatus().ServerState}");
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"[{ _clientName}] [{ OpcDaServer.Url}] Переподключение к серверу не удалось из-за ошибки: {ex}");
                    }
                }
            }
        }
    }
}
