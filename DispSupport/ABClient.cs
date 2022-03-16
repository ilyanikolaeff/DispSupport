using System;
using System.Collections.Generic;
using System.Linq;
using AutomatedSolutions.Win.Comm;
using AutomatedSolutions.Win.Comm.AB.Logix;
using ABLogix = AutomatedSolutions.Win.Comm.AB.Logix;
using Item = AutomatedSolutions.Win.Comm.AB.Logix.Item;
using NLog;


namespace DispSupport
{
    // TO-DO дописать логирование если не удалось прочитать тег
    public class ABClient : IDisposable
    {
        private static Logger _logger = LogManager.GetCurrentClassLogger();

        private ABLogix.Net.Channel _channel;

        private ABLogix.Device _device;

        private ABLogix.Group _group;

        private Dictionary<string, Item> _items;

        public delegate void ABClientNotifyHandler(string message);

        public event ABClientNotifyHandler Notify;

        public delegate void ABClientReadDoneDelegate(List<OperationResult> results, bool exception);

        public event ABClientReadDoneDelegate ReadDone;

        private Result[] _results;

        private bool _asyncReadActive;

        private string _clientName;
        
        public ABClient(PLCConnectionSettings connectionSettings)
        {
            _clientName = connectionSettings.Name;
            _channel = new ABLogix.Net.Channel();
            _device = new ABLogix.Device(connectionSettings.IP + "," + connectionSettings.Backplane + "," + connectionSettings.CPU)
            {
                TimeoutConnect = 1000,
                TimeoutTransaction = 10000,
                Model = Model.ControlLogix
            };
            _group = new ABLogix.Group(false, 500);
            _channel.Devices.Add(_device);
            _device.Groups.Add(_group);
            _items = new Dictionary<string, Item>();
        }

        private bool TagExists(string tag)
        {
            return _items.ContainsKey(tag);      
        }
        private void AddItem(string tag)
        {
            if (!TagExists(tag))
            {
                var item = new Item(tag);
                _items.Add(tag, item);
                _group.Items.Add(item);
            }
        }
        private void AddItems(List<string> tags)
        {
            tags.ForEach(AddItem);
        }
        private void ClearItems()
        {
            _items.Clear();
            _group.Items.Clear();
        }
        public List<OperationResult> ReadSync(List<string> tags)
        {
            if (tags == null)
                return null;

            ClearItems();
            AddItems(tags);

            var itemsToRead = _items.Where(item => tags.Contains(item.Key)).Select(item => item.Value).ToArray();

            try
            {
                _device.Read(itemsToRead, out _results);
            }
            catch (Exception ex)
            {
                InvokeMessage("Unable to read: " + ex.Message);
                _logger.Debug($"[{_clientName}] Не удалось прочитать один или больше ПЛК тегов из-за ошибки: {ex}");
                return null;
            }

            if (_results.Any())
            {
                var listOperationResult = new List<OperationResult>();
                for (int i = 0; i < _results.Length; i++)
                {
                    listOperationResult.Add(new OperationResult(itemsToRead[i].HWTagName,
                        itemsToRead[i].Values.Last(),
                        _results[i].IsOK,
                        _results[i].EvArgs == null ? "" : _results[i].EvArgs.Message));
                }
                if (AppSettings.DEBUG_PLC)
                    PrintResultsDebug(listOperationResult, Operation.ReadSync);

                return listOperationResult;
            }

            return null;
        }
        public OperationResult ReadSync(string tag)
        {
            return ReadSync(new List<string>
            {
                tag
            }).First();
        }
        public bool ReadAsync(List<string> tags)
        {
            if (_asyncReadActive)
                return false;
            ClearItems();
            AddItems(tags);
            var itemsToRead = _items.Where(item => tags.Contains(item.Key)).Select(item => item.Value).ToArray();
            try
            {
                _device.BeginRead(itemsToRead, out _results, ReadCallback, itemsToRead);
                _asyncReadActive = true;
            }
            catch (Exception e)
            {
                InvokeMessage("Unable to read: " + e.Message);
                return false;
            }
            return true;
        }
        public bool ReadAsync(string tag)
        {
            return ReadAsync(new List<string> { tag });
        }
        private void ReadCallback(IAsyncResult ar)
        {
            _asyncReadActive = false;
            try
            {
                _device.EndRead(out _results, ar);
                var itemsToRead = (Item[])ar.AsyncState;
                InvokeReadDone(
                    _results.Select(
                        (t, i) =>
                        new OperationResult(itemsToRead[i].HWTagName, itemsToRead[i].Values.Last(), t.IsOK,
                                       t.EvArgs == null ? "" : t.EvArgs.Message)).ToList(), false);
            }
            catch (Exception e)
            {
                InvokeMessage("Unable to read: " + e.Message);
                InvokeReadDone(new List<OperationResult>(), true);
            }
        }
        public OperationResult WriteSync(string tag, object value)
        {
            ClearItems();
            AddItem(tag);
            Result result;
            try
            {
                _device.Write(_items[tag], value, out result);
            }
            catch (Exception ex)
            {
                InvokeMessage("Unable to write: " + ex.Message);
                _logger.Debug($"[{_clientName}] Не удалось записать один или больше тегов из-за ошибки: {ex}");
                return null;
            }
            var operResult = new OperationResult(_items[tag].HWTagName, value, result.IsOK, result.EvArgs == null ? "" : result.EvArgs.Message);
            if (AppSettings.DEBUG_PLC)
                PrintResultsDebug(new List<OperationResult>() { operResult }, Operation.WriteSync);

            return operResult;
        }
        public List<OperationResult> WriteSync(List<string> tags, object[] values)
        {
            ClearItems();
            AddItems(tags);

            var itemsToWrite = _items.Where(item => tags.Contains(item.Key)).Select(item => item.Value).ToArray();

            try
            {
                _device.Write(itemsToWrite, values, out _results);
            }
            catch (Exception ex)
            {
                InvokeMessage("Unable to write: " + ex.Message);
                _logger.Debug($"[{_clientName}] Не удалось записать один или больше тегов из-за ошибки: {ex}");
                return null;
            }

            if (_results.Any())
            {
                var listOperationResult = new List<OperationResult>();
                for (int i = 0; i < _results.Length; i++)
                {
                    listOperationResult.Add(new OperationResult(itemsToWrite[i].HWTagName,
                        values[i],
                        _results[i].IsOK,
                        _results[i].EvArgs == null ? "" : _results[i].EvArgs.Message));
                }
                if (AppSettings.DEBUG_PLC)
                    PrintResultsDebug(listOperationResult, Operation.WriteSync);

                return listOperationResult;
            }

            return null;
        }
        private void InvokeMessage(string message)
        {
            Notify?.Invoke(message);
        }
        private void InvokeReadDone(List<OperationResult> OperationResults, bool exception)
        {
            ReadDone?.Invoke(OperationResults, exception);
        }
        public void Dispose()
        {
            _group.Dispose();
            _device.Dispose();
            _channel.Dispose();
        }

        private void PrintResultsDebug(IEnumerable<OperationResult> results, Operation operation)
        {
            if (_results != null)
            {
                foreach (var result in results)
                    _logger.Debug($"[{_clientName}] PLC_{operation} - {result}");
            }
        }
    }

    enum Operation
    {
        ReadSync,
        ReadAsync,
        WriteSync,
        WriteAsync
    }

    public class OperationResult
    {
        public string Tag { get; set; }
        public object Result { get; set; }
        public bool IsOK { get; set; }
        public string Message { get; set; }
        public OperationResult(string tag, object result, bool isOK, string message)
        {
            Tag = tag;
            Result = result;
            IsOK = isOK;
            Message = message;
        }

        public override string ToString()
        {
            return $"Tag: {Tag}, Result: {Result}, IsOK = {IsOK}, Message: {Message}";
        }
    }
}
