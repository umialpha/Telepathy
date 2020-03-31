// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Net.Client;
using LogHelper;
using Microsoft.Extensions.Logging;
using WorkerServer;

namespace BrokerServer.Services
{
    public class BrokerService : Broker.BrokerBase
    {
        private static readonly GrpcChannel[] Channels = new GrpcChannel[]
        {
            GrpcChannel.ForAddress("https://localhost:50054"), GrpcChannel.ForAddress("https://localhost:50055"),
            GrpcChannel.ForAddress("https://localhost:50056"), GrpcChannel.ForAddress("https://localhost:50057"),
            GrpcChannel.ForAddress("https://localhost:50058")
        };

        private readonly Guid _brokerGuid = Guid.NewGuid();

        private readonly List<AsyncDuplexStreamingCall<ActionMessage, ResponseMessage>> _duplexStreams = new List<AsyncDuplexStreamingCall<ActionMessage, ResponseMessage>>();

        private static ConcurrentQueue<TaskRequest> _tasks = new ConcurrentQueue<TaskRequest>();

        private IServerStreamWriter<TaskReply> clientStreamWriter = null;

        // SendRequest service to receive client request
        public override async Task Start(IAsyncStreamReader<TaskRequest> requestStream , IServerStreamWriter<TaskReply> responseStream, ServerCallContext context)
        {
            clientStreamWriter = responseStream;

            if (_duplexStreams.Count == 0)
            {
                await ConstructDuplexStream(Channels);
            }

            await foreach (var request in requestStream.ReadAllAsync(context.CancellationToken))
            {

                Console.WriteLine(Logger.Info($"{_brokerGuid} received task " + request.Id));
                _tasks.Enqueue(new TaskRequest
                {
                    Id = request.Id
                });

                await SendProbesAsync(request.Id);
            }
        }

        public async Task SendProbesAsync(string requestId)
        {
            SendProbe probeRequest = new SendProbe
            {
                Id = _brokerGuid.ToString(),
                Timestamp = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds().ToString()
            };

            foreach (var duplexStream in _duplexStreams)
            {
                await duplexStream.RequestStream.WriteAsync(new ActionMessage { Probe = probeRequest });
            }

        }

        private async Task ConstructDuplexStream(GrpcChannel[] channels)
        {
            for (var i = 0; i < channels.Length; i++)
            {
                var client = new Worker.WorkerClient(Channels[i]);
                var duplexStream = client.Subscribe();
                _duplexStreams.Add(duplexStream);
                await Task.Run(async () =>
                {
                    await HandleResponsesAsync(duplexStream);
                });
            }
        }

        private async Task DestructDuplexStream(AsyncDuplexStreamingCall<ActionMessage, ResponseMessage> duplexStream)
        {
            await duplexStream.RequestStream.CompleteAsync().ConfigureAwait(false);
            duplexStream.Dispose();
        }

        private async Task HandleResponsesAsync(AsyncDuplexStreamingCall<ActionMessage, ResponseMessage> duplexStream)
        {
            await foreach (var response in duplexStream.ResponseStream.ReadAllAsync())
            {
                switch (response.ResponseCase)
                {
                    case ResponseMessage.ResponseOneofCase.None:
                        Console.WriteLine(Logger.Info("No Action specified."));
                        break;
                    case ResponseMessage.ResponseOneofCase.Probe:
                        Console.WriteLine(Logger.Info("Receive Probe response to send request to WorkerNode."));
                        await SendRequestAsync(duplexStream.RequestStream);
                        break;
                    case ResponseMessage.ResponseOneofCase.Request:
                        Console.WriteLine(Logger.Info("Receive request response."));
                        await SendRequestAsync(duplexStream.RequestStream);
                        await clientStreamWriter.WriteAsync(new TaskReply { Id = response.Request.Id, Node = response.Request.Node, Message = response.Request.Response });
                        break;
                    case ResponseMessage.ResponseOneofCase.Disconnect:
                        Console.WriteLine(Logger.Info($"Node {response.Disconnect.Node} want to disconnect due to empty request"));
                        await DestructDuplexStream(duplexStream);
                        break;
                    default:
                        Console.WriteLine(Logger.Info($"Unknown Action '{response.ResponseCase}'."));
                        break;
                }
            }
        }

        private async Task SendRequestAsync(IClientStreamWriter<ActionMessage> requestStreamWriter)
        {
            if (_tasks.TryDequeue(out TaskRequest request))
            {
                // Send worker request to handle
                await requestStreamWriter.WriteAsync(new ActionMessage { Request = {Id = request.Id, Request = request.Request}});
            }
            else
            {  // send empty request to disconnect
                await requestStreamWriter.WriteAsync(new ActionMessage {Empty = { Id = _brokerGuid.ToString(), Timestamp = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds().ToString()}});
            }
        }
    }
}