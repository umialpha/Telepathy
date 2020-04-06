// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf.Collections;
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
            GrpcChannel.ForAddress("https://10.240.0.8:50051", new GrpcChannelOptions
            {
                HttpClient = CreateHttpClient()
            }),
            GrpcChannel.ForAddress("https://10.240.0.9:50051", new GrpcChannelOptions
            {
                HttpClient = CreateHttpClient()
            }),
            GrpcChannel.ForAddress("https://10.240.0.11:50051", new GrpcChannelOptions
            {
                HttpClient = CreateHttpClient()
            }),
            GrpcChannel.ForAddress("https://10.240.0.12:50051", new GrpcChannelOptions
            {
                HttpClient = CreateHttpClient()
            }),
            GrpcChannel.ForAddress("https://10.240.0.13:50051", new GrpcChannelOptions
            {
                HttpClient = CreateHttpClient()
            })
        };

        private static HttpClient CreateHttpClient()
        {
            var handler = new HttpClientHandler();
            handler.ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            var cert = new X509Certificate2("client.pfx", "1234");
            handler.ClientCertificates.Add(cert);
            return new HttpClient(handler);
        }


        private static readonly string BrokerGuid = Guid.NewGuid().ToString();

        private static readonly SemaphoreSlim WorkerWriterSemaphoreSlim = new SemaphoreSlim(1, 1);

        private static readonly SemaphoreSlim ClientWriterSemaphoreSlim = new SemaphoreSlim(1, 1);

        private static List<AsyncDuplexStreamingCall<ActionMessage, ResponseMessage>> _duplexStreams = new List<AsyncDuplexStreamingCall<ActionMessage, ResponseMessage>>();

        private static ConcurrentQueue<TaskRequest> _tasks = new ConcurrentQueue<TaskRequest>();

        private static IServerStreamWriter<TaskReply> _clientStreamWriter;

        private readonly ILogger _logger;

        public BrokerService(ILogger<BrokerService> logger)
        {
            _logger = logger;
        }

        public override Task<InitializeResponse> Initialize(InitializeRequest request, ServerCallContext context)
        {
            try
            {
                // Call complete may not invoke RPC cancel exception, then the previous duplex streams can't be destruct
                // Call DestrcutDuplexStreams here
                /*if (_duplexStreams.Count > 0)
                {
                    DestructDuplexStreams();
                }*/

                _duplexStreams = new List<AsyncDuplexStreamingCall<ActionMessage, ResponseMessage>>();
                _logger.LogError(Logger.Format("Start to construct duplex streams"));
                ConstructDuplexStream(Channels, context.CancellationToken);
                return Task.FromResult(new InitializeResponse { Result = "success" });
            }
            catch (Exception e)
            {
                _logger.LogError(Logger.Format($"duplexStreams count is {_duplexStreams.Count}: {e.Message}"));
                return Task.FromResult(new InitializeResponse { Result = $"{e.Message}" });
            }
        }

        // SendRequest service to receive client request
        public override async Task Start(IAsyncStreamReader<TaskRequest> requestStream, IServerStreamWriter<TaskReply> responseStream, ServerCallContext context)
        {
            _logger.LogInformation(Logger.Format($"Connection established, current Broker is {BrokerGuid}"));
            _clientStreamWriter = responseStream;
            try
            {
                await foreach (var request in requestStream.ReadAllAsync(context.CancellationToken))
                {
                    _tasks.Enqueue(request);
                    _logger.LogInformation(
                        Logger.Format(
                            $"{BrokerGuid} receives task {request.Id}, current task length is {_tasks.Count}"));
                    await SendProbesAsync(request.Id);
                }

                context.CancellationToken.Register(async () =>
                {
                    _logger.LogWarning(Logger.Format($"The operation is cancelled, wait to destruct duplex streams"));
                    await DestructDuplexStreams();
                });

            }
            catch (OperationCanceledException e)
            {
                _logger.LogWarning(Logger.Format($"OperationCanceledException: {e.Message}"));
                await DestructDuplexStreams();
            }
            catch (Exception e)
            {
                _logger.LogError(Logger.Format($"Exception when read request: {e.Message}"));
            }

        }

        public async Task SendProbesAsync(string requestId)
        {
            SendProbe probeRequest = new SendProbe
            {
                Id = BrokerGuid,
                Timestamp = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds().ToString(),
                Broker = BrokerGuid
            };

            foreach (var duplexStream in _duplexStreams)
            {
                await duplexStream.RequestStream.WriteAsync(new ActionMessage { Probe = probeRequest });
            }
        }

        private void ConstructDuplexStream(GrpcChannel[] channels, CancellationToken token)
        {
            foreach (var channel in Channels)
            {
                var client = new Worker.WorkerClient(channel);
                var duplexStream = client.Subscribe();
                _duplexStreams.Add(duplexStream);
                Task.Run(async () =>
                {
                    await HandleResponsesAsync(duplexStream, token);
                }, token);
            }
          
            _logger.LogInformation(Logger.Format($"duplex streams constructed, the number is {_duplexStreams.Count}"));
        }

        private async Task DestructDuplexStreams()
        {
            _logger.LogWarning(Logger.Format($"Start to destruct duplex streams"));
            foreach (var stream in _duplexStreams)
            {
                await stream.RequestStream.CompleteAsync().ConfigureAwait(false);
                stream.Dispose();
            }

            while (_duplexStreams.Count > 0)
            {
                _duplexStreams.RemoveAt(0);
            }
            _logger.LogWarning(Logger.Format($"End to destruct duplex streams, the _duplexStreams length is {_duplexStreams.Count}"));
        }

        private async Task HandleResponsesAsync(AsyncDuplexStreamingCall<ActionMessage, ResponseMessage> duplexStream, CancellationToken token)
        {
            try
            {
                await foreach (var response in duplexStream.ResponseStream.ReadAllAsync(token))
                {
                    switch (response.ResponseCase)
                    {
                        case ResponseMessage.ResponseOneofCase.None:
                            _logger.LogWarning(Logger.Format("No Action specified in HandleResponsesAsync."));
                            _logger.LogWarning(Logger.Format($"{response}"));
                            break;
                        case ResponseMessage.ResponseOneofCase.Probe:
                            _logger.LogInformation(
                                Logger.Format($"Receive Probe response, should send {response.Probe.CoreNumber} to Node {response.Probe.Node}"));
                            await SendRequestAsync(duplexStream.RequestStream, response);
                            break;
                        case ResponseMessage.ResponseOneofCase.Request:
                            _logger.LogInformation(Logger.Format(
                                $"Receive request {response.Request.Id} response from Node {response.Request.Node}."));
                            await SendRequestAsync(duplexStream.RequestStream, response);
                            _logger.LogInformation(
                                Logger.Format($"Send request {response.Request.Id} result to client."));
                            await SendResponseToClient(response);
                            break;
                        default:
                            _logger.LogWarning(Logger.Format($"Unknown Action '{response.ResponseCase}'."));
                            break;
                    }
                }
            }
            catch (OperationCanceledException e)
            {
                _logger.LogError(Logger.Format($"Cancelled in HandleResponseAsync from Worker Response: {e.ToString()}"));
            }
            catch (Exception e)
            {
                _logger.LogError(Logger.Format($"Error in HandleResponseAsync from Worker Response: {e.ToString()}"));
            }
        }

        private async Task SendRequestAsync(IClientStreamWriter<ActionMessage> requestStreamWriter, ResponseMessage response)
        {
            var node = response.ResponseCase == ResponseMessage.ResponseOneofCase.Probe
                    ? response.Probe.Node
                    : response.Request.Node;
            var num = response.ResponseCase == ResponseMessage.ResponseOneofCase.Probe ? response.Probe.CoreNumber : 1;
            RepeatedField<ClientRequest> requests = new RepeatedField<ClientRequest>();
            try
            {
                await WorkerWriterSemaphoreSlim.WaitAsync();
                while (num > 0)
                {
                    if (_tasks.TryDequeue(out TaskRequest request))
                    {
                        // Send worker request to handle
                        _logger.LogInformation(Logger.Format($"Get request {request.Id} to Worker {node}"));
                        requests.Add(new ClientRequest { Id = request.Id, Request = request.Request });
                    }
                    else
                    {
                        // send empty request to disconnect
                        _logger.LogInformation(Logger.Format($"Start to send empty signal to Worker {node}, request number is {num}"));
                        await requestStreamWriter.WriteAsync(new ActionMessage
                        {
                            Empty = new Empty
                            {
                                Id = BrokerGuid,
                                Timestamp = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds().ToString(),
                                Broker = BrokerGuid.ToString(),
                                CoreNumber = num
                            }
                        });
                        break;
                    }

                    num--;
                }

                if (requests.Count > 0)
                {
                    _logger.LogInformation(Logger.Format($"Send {requests.Count} requests to Worker {node}"));
                    await requestStreamWriter.WriteAsync(new ActionMessage
                    {
                        Request = new SendRequest
                        { Request = { requests }, Number = requests.Count, Broker = BrokerGuid }
                    });
                }
            }
            catch (Exception e)
            {
                _logger.LogError(Logger.Format($"Error in SendRequestAsync: {e.ToString()}"));
            }
            finally
            {
                WorkerWriterSemaphoreSlim.Release();
            }

        }

        private async Task SendResponseToClient(ResponseMessage response)
        {
            try
            {
                await ClientWriterSemaphoreSlim.WaitAsync();
                await _clientStreamWriter.WriteAsync(
                    new TaskReply
                    {
                        Id = response.Request.Id,
                        Node = response.Request.Node,
                        Message = response.Request.Response
                    });
            }
            catch (Exception e)
            {
                _logger.LogError($"Error message when send reply to client: {e}");
            }
            finally
            {
                ClientWriterSemaphoreSlim.Release();
            }
        }
    }
}