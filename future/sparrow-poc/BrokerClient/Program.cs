using BrokerServer;
using Grpc.Core;
using Grpc.Net.Client;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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

        private static AsyncDuplexStreamingCall<TaskRequest, TaskReply>[] _duplexStream = new AsyncDuplexStreamingCall<TaskRequest, TaskReply>[3];
        private static List<Task> responseTasks = new List<Task>();
        private static ConcurrentBag<TaskReply> replies = new ConcurrentBag<TaskReply>();

        private static void ConstructDuplexStream()
        {
            for (var i = 0; i < Channels.Length; i++)
            {
                var client = new Broker.BrokerClient(Channels[i]);
                _duplexStream[i] = client.Start();
                var duplexStream = _duplexStream[i];
                responseTasks.Add(Task.Run(async () => 
                    {
                        await foreach (var response in duplexStream.ResponseStream.ReadAllAsync())
                        {
                            Console.WriteLine(Logger.Info(response.Message));
                            replies.Add(response);
                        }
                    }));
            }
        }

        private static async void DestructDuplexStream()
        {
            foreach (var stream in _duplexStream)
            {
                await stream.RequestStream.CompleteAsync().ConfigureAwait(false);
                stream.Dispose();
            }
        }

        public static int GetRandomNum(int max)
        {
            Random rnd = new Random();
            return rnd.Next(0, max);
        }

        public static void Main(string[] args)
        {
            const int taskNum = 1000;
            const int estimateTaskExecuteTime = 1000;
            const int totalCoreNum = 10;
            Stopwatch stopwatch = Stopwatch.StartNew();
            ConstructDuplexStream();
            Console.WriteLine("Start to send requests");
            for (int i = 0; i < taskNum; i++)
            {
                int num = GetRandomNum(3);
                _duplexStream[num].RequestStream.WriteAsync(new TaskRequest { Id = i.ToString() });
            }
            Console.WriteLine("End to send requests");

            Task.WaitAll(responseTasks.ToArray());

            stopwatch.Stop();
            StreamWriter file = new StreamWriter(@".\TestFolder\result.txt", true);
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
            long elapsedTime = (taskNum / totalCoreNum) * estimateTaskExecuteTime;
            file.WriteLine("Actual elapsed time is " + actualTime + "ms");
            file.WriteLine("Supposed elapsed time is " + elapsedTime + "ms");
            file.WriteLine("Efficiency in this test is " + (((double)elapsedTime / actualTime) * 100) + "%");
            file.WriteLine("*******************End******************");
            file.Flush();
            file.Close();

            DestructDuplexStream();
        }
    }
}
