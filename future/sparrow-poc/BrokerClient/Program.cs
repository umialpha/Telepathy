using BrokerServer;
using Grpc.Core;
using Grpc.Net.Client;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using LogHelper;

namespace BrokerClient
{
    class Program
    {
        private static readonly GrpcChannel[] Channels = new GrpcChannel[]
        {
            GrpcChannel.ForAddress("https://localhost:50051"), GrpcChannel.ForAddress("https://localhost:50052"),
            GrpcChannel.ForAddress("https://localhost:50053")
        };

        private static List<AsyncDuplexStreamingCall<TaskRequest, TaskReply>> _duplexStreams =
            new List<AsyncDuplexStreamingCall<TaskRequest, TaskReply>>();

        private static List<Task> responseTasks = new List<Task>();
        private static List<Broker.BrokerClient> clients = new List<Broker.BrokerClient>();
        private static ConcurrentBag<TaskReply> replies = new ConcurrentBag<TaskReply>();
        private const int TaskNum = 5000;
        private const int EstimateTaskExecuteTime = 1000;
        private const int TotalCoreNum = 10;
        private static readonly string ClientGuid = Guid.NewGuid().ToString();

        private static bool InitializeBrokers()
        {
            bool result = true;
            for (var i = 0; i < Channels.Length; i++)
            {
                var client = new Broker.BrokerClient(Channels[i]);
                clients.Add(client);
                try
                {
                    Console.WriteLine($"Start to initialize broker {Channels[i]}");
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
            }
            return result;
        }

        private static void ConstructDuplexStream()
        {
            foreach (var client in clients)
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
        }

        private static async void DestructDuplexStream()
        {
            foreach (var stream in _duplexStreams)
            {
                await stream.RequestStream.CompleteAsync().ConfigureAwait(false);
                stream.Dispose();
            }

            foreach (var channel in Channels)
            {
                await channel.ShutdownAsync();
            }
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
