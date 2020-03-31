// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Net;
using System.Runtime.CompilerServices;
using LogHelper;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using Grpc.Core;
using System.Threading;
using WorkerServer;

namespace WorkerServer.Services
{
    internal struct BrokerRecord
    {
        public SendProbe Probe { get; }

        public IServerStreamWriter<ResponseMessage> Writer { get; }

        public BrokerRecord(SendProbe request, IServerStreamWriter<ResponseMessage> writer)
        {
            Probe = request;
            Writer = writer;
        }
    }
    public class WorkerService : Worker.WorkerBase
    {
        // Can parallel run 2 requests at one time
        private static int _availableCoreNum = 2;
        private static ConcurrentDictionary<string, BrokerRecord> _brokers = new ConcurrentDictionary<string, BrokerRecord>();
        private readonly Guid _nodeGuid = Guid.NewGuid();


        public override async Task Subscribe(IAsyncStreamReader<ActionMessage> requestStream, IServerStreamWriter<ResponseMessage> responseStream, ServerCallContext context)
        {
            await HandleActions(requestStream, responseStream);
        }

        private async Task HandleActions(IAsyncStreamReader<ActionMessage> requestStream, IServerStreamWriter<ResponseMessage> responseStream)
        {
            await foreach (var action in requestStream.ReadAllAsync())
            {
                switch (action.ActionCase)
                {
                    case ActionMessage.ActionOneofCase.None:
                        Console.WriteLine(Logger.Info("No Action specified."));
                        break;
                    case ActionMessage.ActionOneofCase.Empty:
                        Console.WriteLine(Logger.Info("No more request, empty action"));
                        await ChangeBrokerAsync(action, responseStream);
                        break;
                    case ActionMessage.ActionOneofCase.Probe:
                        Console.WriteLine(Logger.Info("Receive probe action."));
                        await MakeReservationAsync(action, responseStream);
                        break;
                    case ActionMessage.ActionOneofCase.Request:
                        Console.WriteLine(Logger.Info("Receive request action."));
                        await ExecuteTaskAsync(action, responseStream);
                        break;
                    default:
                        Console.WriteLine($"Unknown Action '{action.ActionCase}'.");
                        break;
                }
            }
        }

        public async Task MakeReservationAsync(ActionMessage request, IServerStreamWriter<ResponseMessage> responseStream)
        {
            BrokerRecord updateRecord = new BrokerRecord(request.Probe, responseStream);
            // use broker guid as probe id  
            _brokers.AddOrUpdate(request.Probe.Id, updateRecord, (k, v) => updateRecord);

            if (_availableCoreNum > 0)
            {
                var temp = _availableCoreNum;
                _availableCoreNum = 0;

                for (var i = 0; i < temp; i++)
                {
                    // Send probe response to get task request
                    await Task.Run(async () => await SendResponseAsync(request, responseStream));
                }
            }
        }

        private async Task ExecuteTaskAsync(ActionMessage request, IServerStreamWriter<ResponseMessage> responseStream)
        {
            await Task.Delay(1000);
            await SendResponseAsync(request, responseStream);
        }

        private async Task ChangeBrokerAsync(ActionMessage action, IServerStreamWriter<ResponseMessage> responseStream)
        {
            if (action.ActionCase == ActionMessage.ActionOneofCase.Empty)
            {
                // Empty.id is equal to Probe.id which are all broker guid value
                if (_brokers.TryGetValue(action.Empty.Id, out BrokerRecord brokerRecord))
                {
                    long recordTime = Convert.ToInt64(brokerRecord.Probe.Timestamp);
                    long queryTime = Convert.ToInt64(action.Empty.Timestamp);

                    // only the time record in dics smaller than the query task time, which means that no more probe arrives in during query time,
                    // then remove empty brokers from dics
                    if (recordTime < queryTime)
                    {
                        _brokers.TryRemove(action.Empty.Id, out var record);
                        await record.Writer.WriteAsync(new ResponseMessage { Disconnect = new DisconnectResponse { Node = _nodeGuid.ToString() } });
                    }
                }

                // try to change broker to acquire tasks
                if (_brokers.Count > 0)
                {
                    var broker = _brokers.Take(1).Select(item => item.Value).First();
                    await SendResponseAsync(new ActionMessage { Probe = broker.Probe }, broker.Writer);
                }
                else
                {
                    // return core only no more task can be acquired from any scheduler
                    _availableCoreNum++;
                }
            }
            else
            {
                throw new Exception("Action type error in change broker");
            }


        }

        private async Task SendResponseAsync(ActionMessage action, IServerStreamWriter<ResponseMessage> responseStream)
        {
            switch (action.ActionCase)
            {
                case ActionMessage.ActionOneofCase.None:
                    Console.WriteLine("No Action specified.");
                    break;
                case ActionMessage.ActionOneofCase.Probe:
                    // Probe response to get task request
                    Console.WriteLine("Send probe response.");
                    await responseStream.WriteAsync(new ResponseMessage { Probe = new ProbeResponse { Id = action.Probe.Id, Node = _nodeGuid.ToString() } });
                    break;
                case ActionMessage.ActionOneofCase.Request:
                    // Send response with message
                    Console.WriteLine("Send task request response.");
                    await responseStream.WriteAsync(new ResponseMessage { Request = new RequestResponse { Id = action.Request.Id, Node = _nodeGuid.ToString(), Response = $"Task {action.Request.Id} executed in node {_nodeGuid}" } });
                    break;
                default:
                    Console.WriteLine($"Unknown Action '{action.ActionCase}'.");
                    break;
            }
        }
    }
}