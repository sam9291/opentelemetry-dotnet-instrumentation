// <copyright file="Program.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System.Diagnostics;
using System.Reflection;
using System.ServiceModel;
using System.ServiceModel.Channels;

namespace TestApplication.Wcf.Client.DotNet;

internal static class Program
{
    private static readonly ActivitySource Source = new(Assembly.GetExecutingAssembly().GetName().Name!, "1.0.0.0");

    public static async Task Main(string[] args)
    {
        var netTcpAddress = "net.tcp://127.0.0.1:9090/Telemetry";
        var httpAddress = "http://127.0.0.1:9009/Telemetry";

        using var parent = Source.StartActivity("Parent");
        try
        {
            Console.WriteLine("=============NetTcp===============");
            await CallService(netTcpAddress, new NetTcpBinding(SecurityMode.None)).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // netTcp fails on open when there is no endpoint
        }

        Console.WriteLine("=============Http===============");
        await CallService(httpAddress, new BasicHttpBinding()).ConfigureAwait(false);
        using var sibling = Source.StartActivity("Sibling");
    }

    private static async Task CallService(string address, Binding binding)
    {
        // Note: Best practice is to re-use your client/channel instances.
        // This code is not meant to illustrate best practices, only the
        // instrumentation.
        var client = new StatusServiceClient(binding, new EndpointAddress(new Uri(address)));
        await client.OpenAsync().ConfigureAwait(false);

        try
        {
            try
            {
                Console.WriteLine("Task-based Asynchronous Pattern call");
                var rq = new StatusRequest { Status = "1" };
                var response = await client.PingAsync(rq).ConfigureAwait(false);

                Console.WriteLine(
                    $"[{DateTimeOffset.UtcNow:o}] Request with status {rq.Status}. Server returned: {response?.ServerTime:o}");
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }
        finally
        {
            try
            {
                if (client.State == CommunicationState.Faulted)
                {
                    client.Abort();
                }
                else
                {
                    await client.CloseAsync().ConfigureAwait(false);
                }
            }
            catch
            {
            }
        }
    }
}
