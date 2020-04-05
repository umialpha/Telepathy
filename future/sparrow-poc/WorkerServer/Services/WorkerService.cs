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
        private static readonly string NodeGuid = Guid.NewGuid().ToString();
        private readonly ILogger _logger;
        private static readonly SemaphoreSlim CurrentWriterSemaphoreSlim = new SemaphoreSlim(1, 1);
        private static readonly SemaphoreSlim NewWriterSemaphoreSlim = new SemaphoreSlim(1, 1);

        public WorkerService(ILogger<WorkerService> logger)
        {
            _logger = logger;
        }

        public override async Task Subscribe(IAsyncStreamReader<ActionMessage> requestStream, IServerStreamWriter<ResponseMessage> responseStream, ServerCallContext context)
        {
            _logger.LogInformation(Logger.Format($"Connection established with Broker, current Worker is {NodeGuid}"));
            _availableCoreNum = 2;
            await HandleActions(requestStream, responseStream, context.CancellationToken);
        }

        private async Task HandleActions(IAsyncStreamReader<ActionMessage> requestStream, IServerStreamWriter<ResponseMessage> responseStream, CancellationToken token)
        {
            try
            {
                await foreach (var action in requestStream.ReadAllAsync(token))
                {
                    switch (action.ActionCase)
                    {
                        case ActionMessage.ActionOneofCase.None:
                            _logger.LogWarning(Logger.Format("No Action specified when read request from Brokers."));
                            break;
                        case ActionMessage.ActionOneofCase.Empty:
                            _logger.LogInformation(
                                Logger.Format($"No more request, empty action from Broker {action.Empty.Id}"));
                            await ChangeBrokerAsync(action);
                            break;
                        case ActionMessage.ActionOneofCase.Probe:
                            _logger.LogInformation(Logger.Format($"Receive probe action from {action.Probe.Broker}."));
                            await MakeReservation(action, responseStream);
                            break;
                        case ActionMessage.ActionOneofCase.Request:
                            _logger.LogInformation(
                                Logger.Format(
                                    $"Receive {action.Request.Number} request from {action.Request.Broker}."));
                            ExecuteTaskAsync(action, responseStream);
                            break;
                        default:
                            _logger.LogWarning($"Unknown Action '{action.ActionCase}'.");
                            break;
                    }
                }
            }
            catch (OperationCanceledException e)
            {
                _logger.LogWarning(Logger.Format($"OperationCanceledException: {e.Message}"));
                // Reset worker configurations
                _logger.LogError(Logger.Format($"Start to reset worker Info."));
                _brokers.Clear();
                _availableCoreNum = 2;
            }
            catch (Exception e)
            {
                _logger.LogError(Logger.Format($"Error in HandleAction when read requests: {e.Message}"));
            }
        }

        public async Task MakeReservation(ActionMessage request, IServerStreamWriter<ResponseMessage> responseStream)
        {
            _logger.LogInformation(Logger.Format($"Make reservation for Broker {request.Probe.Broker}, current available core number is {_availableCoreNum}"));
            BrokerRecord updateRecord = new BrokerRecord(request.Probe, responseStream);
            // use broker guid as probe id  
            _brokers.AddOrUpdate(request.Probe.Id, updateRecord, (k, v) => updateRecord);

            if (_availableCoreNum > 0)
            {
                var temp = _availableCoreNum;
                _availableCoreNum = 0;

                /*for (var i = 0; i < temp; i++)
                {
                    // Send probe response to get task request
                    Task.Run(async () => await SendResponseAsync(request, responseStream));
                }*/
                _logger.LogInformation(Logger.Format($"Start to send probe response to broker {request.Probe.Broker}, request number is {temp}."));
                await CurrentWriterSemaphoreSlim.WaitAsync();
                try
                {
                    await responseStream.WriteAsync(new ResponseMessage
                    { Probe = new ProbeResponse { Id = request.Probe.Id, Node = NodeGuid, CoreNumber = temp } });
                }
                catch (Exception e)
                {
                    _logger.LogError(e.Message);
                }
                finally
                {
                    CurrentWriterSemaphoreSlim.Release();
                }

            }
        }

        private void ExecuteTaskAsync(ActionMessage requests, IServerStreamWriter<ResponseMessage> responseStream)
        {
            foreach (var request in requests.Request.Request)
            {
                _logger.LogInformation(Logger.Format($"Start to execute request {request.Id}."));
                Task.Run(async () =>
                {
                    await Task.Delay(1000);
                    _logger.LogInformation(Logger.Format($"Start to send request {request.Id} response to broker."));
                    await CurrentWriterSemaphoreSlim.WaitAsync();
                    try
                    {
                        await responseStream.WriteAsync(new ResponseMessage
                        {
                            Request = new RequestResponse
                            {
                                Id = request.Id,
                                Node = NodeGuid,
                                Response = $"Task {request.Id} executed in node {NodeGuid}"
                            }
                        });
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                        throw;
                    }
                    finally
                    {
                        CurrentWriterSemaphoreSlim.Release();
                    }
                });
            }
        }

        private async Task ChangeBrokerAsync(ActionMessage action)
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
                    }
                }

                // try to change broker to acquire requests
                if (_brokers.Count > 0)
                {
                    var broker = _brokers.Take(1).Select(item => item.Value).First();
                    _logger.LogWarning($"Change Broker to {broker.Probe.Broker}, request number is {action.Empty.CoreNumber}");
                    //Change broker and get the max number of requests from new Broker.
                    await NewWriterSemaphoreSlim.WaitAsync();
                    try
                    {
                        await broker.Writer.WriteAsync(new ResponseMessage
                        {
                            Probe = new ProbeResponse
                            { Id = broker.Probe.Id, Node = NodeGuid, CoreNumber = action.Empty.CoreNumber }
                        });
                    }
                    catch (Exception e)
                    {
                        _logger.LogError($"Error message when change broker: {e.Message}");
                        throw;
                    }
                    finally
                    {
                        NewWriterSemaphoreSlim.Release();
                    }
                }
                else
                {
                    // return core only no more task can be acquired from any scheduler
                    _availableCoreNum += action.Empty.CoreNumber;
                }
            }
            else
            {
                _logger.LogError(Logger.Format("Action type error in change broker"));
            }
        }
    }
}