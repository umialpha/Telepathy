using BrokerServer;
using Grpc.Core;
using Grpc.Net.Client;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using LogHelper;

namespace BrokerClient
{
    class Program
    {
        private static readonly GrpcChannel Channel = GrpcChannel.ForAddress("https://10.3.0.19:50051",
            new GrpcChannelOptions
            {
                HttpClient = CreateHttpClient()
            });

        private static HttpClient CreateHttpClient()
        {
            var handler = new HttpClientHandler();
            handler.ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            var cert = new X509Certificate2("C:\\Users\\jingjli\\github\\Telepathy\\future\\sparrow-poc\\certs-test\\client.pfx", "1234");
            handler.ClientCertificates.Add(cert);
            return new HttpClient(handler);
        }

        private static List<AsyncDuplexStreamingCall<TaskRequest, TaskReply>> _duplexStreams =
            new List<AsyncDuplexStreamingCall<TaskRequest, TaskReply>>();

        private static List<Task> responseTasks = new List<Task>();
        private static Broker.BrokerClient client = null;
        private static ConcurrentBag<TaskReply> replies = new ConcurrentBag<TaskReply>();
        private const int TaskNum = 5000;
        private const int EstimateTaskExecuteTime = 1000;
        private const int TotalCoreNum = 10;
        private static readonly string ClientGuid = Guid.NewGuid().ToString();

        private static bool InitializeBrokers()
        {
            bool result = true;
            client = new Broker.BrokerClient(Channel);

            try
            {
                Console.WriteLine($"Start to initialize broker {Channel}");
                var initializeResult = client.Initialize(new InitializeRequest { ClientId = ClientGuid });
                if (!string.Equals(initializeResult.Result, "success"))
                {
                    Console.WriteLine($"Failed in initialize broker: {initializeResult.Result}");
                    result = false;
                }
                else
                {
                    Console.WriteLine($"Succeed in initialize broker.");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error in initialize broker: {e.Message}");
                result = false;
            }

            return result;
        }

        private static void ConstructDuplexStream()
        {

            var duplexStream = client.Start();
            _duplexStreams.Add(duplexStream);
            responseTasks.Add(Task.Run(async () =>
            {
                try
                {
                    await foreach (var response in duplexStream.ResponseStream.ReadAllAsync())
                    {
                        replies.Add(response);
                        Console.WriteLine(Logger.Format($"{response.Message}, current replies count is {replies.Count}"));
                        if (replies.Count == TaskNum)
                        {
                            Console.WriteLine(Logger.Format($"Wait for request stream complete..."));
                            DestructDuplexStream();
                        }
                    }
                }
                catch (RpcException e)
                {
                    Console.WriteLine(Logger.Format($"RpcError in HandleResponseStream from Brokers {e.Message}"));
                }
                catch (OperationCanceledException e)
                {
                    Console.WriteLine(Logger.Format($"OperationCanceledException in HandleResponseStream from Brokers {e.Message}"));
                }
            }));

        }

        private static async void DestructDuplexStream()
        {
            foreach (var stream in _duplexStreams)
            {
                await stream.RequestStream.CompleteAsync().ConfigureAwait(false);
                stream.Dispose();
            }
            await Channel.ShutdownAsync();
        }

        public static int GetRandomNum(int max)
        {
            Random rnd = new Random();
            return rnd.Next(0, max);
        }

        public static async Task Main(string[] args)
        {
            if (InitializeBrokers())
            {
                ConstructDuplexStream();
                Console.WriteLine(Logger.Format("Start to send requests"));
                Stopwatch stopwatch = Stopwatch.StartNew();
                for (int i = 0; i < TaskNum; i++)
                {

                    int num = GetRandomNum(3);
                    try
                    {
                        Console.WriteLine(Logger.Format($"Send request {i} to DuplexStream {num}"));
                        await _duplexStreams[num].RequestStream.WriteAsync(new TaskRequest
                        { Id = i.ToString(), Request = $"request content for request {i}" });
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                    }

                }
                Console.WriteLine(Logger.Format("End to send requests"));
                await Task.WhenAll(responseTasks).ContinueWith(task =>
                {
                    Console.WriteLine(Logger.Format("All requests finish !"));
                    stopwatch.Stop();
                    StreamWriter file =
                        new StreamWriter(@"C:\Users\jingjli\github\Telepathy\future\sparrow-poc\TestFolder\result-5000.txt",
                            true);
                    file.WriteLine("*******************Start******************");
                    Dictionary<string, int> result = new Dictionary<string, int>();

                    while (!replies.IsEmpty)
                    {
                        if (replies.TryTake(out TaskReply reply))
                        {
                            if (result.ContainsKey(reply.Node))
                            {
                                result[reply.Node]++;
                            }
                            else
                            {
                                result.Add(reply.Node, 1);
                            }
                        }
                    }

                    foreach (KeyValuePair<string, int> item in result)
                    {
                        file.WriteLine(item.Key + " executed " + item.Value + " tasks.");
                    }

                    long actualTime = stopwatch.ElapsedMilliseconds;
                    long elapsedTime = (TaskNum / TotalCoreNum) * EstimateTaskExecuteTime;
                    file.WriteLine("Actual elapsed time is " + actualTime + "ms");
                    file.WriteLine("Supposed elapsed time is " + elapsedTime + "ms");
                    file.WriteLine("Efficiency in this test is " + (((double)elapsedTime / actualTime) * 100) + "%");
                    file.WriteLine("*******************End******************");
                    file.Flush();
                    file.Close();

                    DestructDuplexStream();
                });
            }
            else
            {
                Console.WriteLine("Failed to initialize Broker clients.");
            }
        }
    }
}
