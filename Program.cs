﻿/*   Copyright 2016 Cinegy GmbH

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
*/


using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.ServiceModel.Web;
using System.Threading;
using CommandLine;
using static System.String;

namespace NmosAnalyser
{
    internal class Program
    {
        private static bool _receiving;
        private static bool _suppressConsoleOutput;
        private static DateTime _startTime = DateTime.UtcNow;
        private static string _logFile;
        private static bool _pendingExit;
        private static ServiceHost _serviceHost;
        private static NmosAnalyserApi _nmosAnalyserApi;
        private static UdpClient _udpClient = new UdpClient { ExclusiveAddressUse = false };
        private static object _logfileWriteLock = new object();
        private static StreamWriter _logFileStream = null;
        private static NetworkMetric _networkMetric = new NetworkMetric();
        private static RtpMetric _rtpMetric = new RtpMetric();
        private static NmosRtpMetric _nmosMetric = new NmosRtpMetric();

        static void Main(string[] args)
        {
            var options = new Options();

            Console.CancelKeyPress += Console_CancelKeyPress;

            Console.WriteLine("Cinegy Simple RTP and NMOS monitoring tool v1.0.0 ({0})\n",
                File.GetCreationTime(Assembly.GetExecutingAssembly().Location));

            try
            {
                Console.SetWindowSize(100, 40);
            }
            catch
            {
                Console.WriteLine("Failed to increase console size - probably screen resolution is low");
            }

            if (!Parser.Default.ParseArguments(args, options))
            {
                //ask the user interactively for an address and group
                Console.WriteLine(
                    "\nSince parameters were not passed at the start, you can now enter the two most important (or just hit enter to quit)");
                Console.Write("\nPlease enter the multicast address to listen to (e.g. 239.1.1.1): ");
                var address = Console.ReadLine();

                if (IsNullOrWhiteSpace(address)) return;

                options.MulticastAddress = address;

                Console.Write("Please enter the multicast group port (e.g. 1234): ");
                var port = Console.ReadLine();
                if (IsNullOrWhiteSpace(port))
                {
                    Console.WriteLine("Not a valid group port number - press enter to exit.");
                    Console.ReadLine();
                    return;
                }
                options.MulticastGroup = int.Parse(port);

            }

            WorkLoop(options);
        }

        private static void WorkLoop(Options options)
        {
            Console.Clear();

            if (!_receiving)
            {
                _receiving = true;
                _logFile = options.LogFile;
                _suppressConsoleOutput = options.SuppressOutput;

                if (!IsNullOrWhiteSpace(_logFile))
                {
                    PrintToConsole("Logging events to file {0}", _logFile);
                }
                LogMessage("Logging started.");

                if (options.EnableWebServices)
                {
                    var httpThreadStart = new ThreadStart(delegate
                    {
                        StartHttpService(options.ServiceUrl);
                    });

                    var httpThread = new Thread(httpThreadStart) { Priority = ThreadPriority.Normal };

                    httpThread.Start();
                }

                SetupMetrics();
                StartListeningToNetwork(options.MulticastAddress, options.MulticastGroup, options.AdapterAddress);
            }

            Console.Clear();

            while (!_pendingExit)
            {
                var runningTime = DateTime.UtcNow.Subtract(_startTime);

                //causes occasional total refresh to erase glitches that build up
                if (runningTime.Milliseconds < 20)
                {
                    Console.Clear();
                }

                if (!_suppressConsoleOutput)

                {
                    Console.SetCursorPosition(0, 0);

                    PrintToConsole($"Cinegy NMOS Analyser");
                    if(options.EnableWebServices & (_serviceHost?.State == CommunicationState.Opened))
                    {
                        PrintToConsole($"Web Service Enabled - browse to {options.ServiceUrl}/");
                    }
                    PrintToConsole("Monitoring: rtp://@{0}:{1}\tRunning time: {2:hh\\:mm\\:ss}\t\t\n", options.MulticastAddress,
                        options.MulticastGroup, runningTime);
                    PrintToConsole(
                        "Network Details\n----------------\nTotal Packets Rcvd: {0} \tBuffer Usage: {1:0.00}%\t\t\nTotal Data (MB): {2}\t\tPackets per sec:{3}",
                        _networkMetric.TotalPackets, _networkMetric.NetworkBufferUsage, _networkMetric.TotalData / 1048576,
                        _networkMetric.PacketsPerSecond);
                    PrintToConsole("Time Between Packets (ms): {0} \tShortest/Longest: {1}/{2}",
                        _networkMetric.TimeBetweenLastPacket, _networkMetric.ShortestTimeBetweenPackets,
                        _networkMetric.LongestTimeBetweenPackets);
                    PrintToConsole("Bitrates (Mbps): {0:0.00}/{1:0.00}/{2:0.00}/{3:0.00} (Current/Avg/Peak/Low)\t\t\t",
                        (_networkMetric.CurrentBitrate / 131072.0), _networkMetric.AverageBitrate / 131072.0,
                        (_networkMetric.HighestBitrate / 131072.0), (_networkMetric.LowestBitrate / 131072.0));
                    PrintToConsole(
                    "\nRTP Details\n----------------\nSeq Num: {0}\tMin Lost Pkts: {1}\nTimestamp: {2}\tSSRC: {3}\t",
                    _rtpMetric.LastSequenceNumber, _rtpMetric.MinLostPackets, _rtpMetric.LastTimestamp, _rtpMetric.Ssrc);
                    PrintToConsole(
                    "\nNMOS Details\n----------------\nHeader Count: {0}\tExtension Length: {1}",
                   _nmosMetric.TotalNmosHeaders, _nmosMetric.LastNmosHeader.ExtensionLength);

                }

                Thread.Sleep(20);
            }

            LogMessage("Logging stopped.");
        }

        private static void StartListeningToNetwork(string multicastAddress, int multicastGroup,
            string listenAdapter = "")
        {

            var listenAddress = IsNullOrEmpty(listenAdapter) ? IPAddress.Any : IPAddress.Parse(listenAdapter);

            var localEp = new IPEndPoint(listenAddress, multicastGroup);

            _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _udpClient.Client.ReceiveBufferSize = 1024*1024*10;
            _udpClient.ExclusiveAddressUse = false;
            _udpClient.Client.Bind(localEp);
            _networkMetric.UdpClient = _udpClient;

            var parsedMcastAddr = IPAddress.Parse(multicastAddress);
            _udpClient.JoinMulticastGroup(parsedMcastAddr, listenAddress);

            var ts = new ThreadStart(delegate
            {
                ReceivingNetworkWorkerThread(_udpClient, localEp);
            });

            var receiverThread = new Thread(ts) {Priority = ThreadPriority.Highest};

            receiverThread.Start();
        }
        
        private static void ReceivingNetworkWorkerThread(UdpClient client, IPEndPoint localEp)
        {
            while (_receiving)
            {
                var data = client.Receive(ref localEp);
                if (data == null) continue;
                try
                {
                    _networkMetric.AddPacket(data);
                    _rtpMetric.AddPacket(data);

                    if (_rtpMetric.HasExtension)
                    {
                        _nmosMetric.AddPacket(data);
                    }

                }
                catch (Exception ex)
                {
                    LogMessage($@"Unhandled exception within network receiver: {ex.Message}");
                }
            }
        }
        
        private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            if (_pendingExit) return; //already trying to exit - allow normal behaviour on subsequent presses
            _pendingExit = true;
            e.Cancel = true;
        }
        
        private static void RtpMetric_SequenceDiscontinuityDetected(object sender, EventArgs e)
        {
            LogMessage("Discontinuity in RTP sequence.");
        }

        private static void NetworkMetric_BufferOverflow(object sender, EventArgs e)
        {
            LogMessage("Network buffer > 99% - probably loss of data from overflow.");
        }

        private static void PrintToConsole(string message, params object[] arguments)
        {
            if (_suppressConsoleOutput) return;
            
            Console.WriteLine(message, arguments);
        }
        
        private static void LogMessage(string message)
        {
            ThreadPool.QueueUserWorkItem(WriteToFile, message);
        }

        public static void WriteToFile(object msg)
        {
            lock (_logfileWriteLock)
            {
                try
                {
                    if (_logFileStream == null || _logFileStream.BaseStream.CanWrite != true)
                    {
                        if (IsNullOrWhiteSpace(_logFile)) return;

                        var fs = new FileStream(_logFile, FileMode.Append, FileAccess.Write);

                        _logFileStream = new StreamWriter(fs);
                        _logFileStream.AutoFlush = true;
                    }

                    _logFileStream.WriteLine("{0} - {1}", DateTime.Now, msg);
                }
                catch (Exception)
                {
                    Debug.WriteLine("Concurrency error writing to log file...");
                    _logFileStream.Close();
                    _logFileStream.Dispose();
                }
            }
        }

  

        private static void StartHttpService(string serviceAddress)
        {
            var baseAddress = new Uri(serviceAddress);

            _serviceHost?.Close();

            _nmosAnalyserApi = new NmosAnalyserApi
            {
                NetworkMetric = _networkMetric,
                RtpMetric = _rtpMetric
            };

            _nmosAnalyserApi.StreamCommand += _tsAnalyserApi_StreamCommand;
            
            _serviceHost = new ServiceHost(_nmosAnalyserApi, baseAddress);
            var webBinding = new WebHttpBinding();

            var serviceEndpoint = new ServiceEndpoint(ContractDescription.GetContract(typeof (INmosAnalyserApi)))
            {
                Binding = webBinding, 
                Address = new EndpointAddress(baseAddress)
            };

            _serviceHost.AddServiceEndpoint(serviceEndpoint);

            var webBehavior = new WebHttpBehavior
            {
                AutomaticFormatSelectionEnabled = true,
                DefaultOutgoingRequestFormat = WebMessageFormat.Json,
                HelpEnabled = true
            };
            
            serviceEndpoint.Behaviors.Add(webBehavior);
            
            //Metadata Exchange
            var serviceBehavior = new ServiceMetadataBehavior {HttpGetEnabled = true};
            _serviceHost.Description.Behaviors.Add(serviceBehavior);

            try
            {
                _serviceHost.Open();
            }
            catch (Exception ex)
            {
                var msg =
                    "Failed to start local web API for player - either something is already using the requested URL, the tool is not running as local administrator, or netsh url reservations have not been made " +
                    "to allow non-admin users to host services.\n\n" +
                    "To make a URL reservation, permitting non-admin execution, run:\n" +
                    "netsh http add urlacl http://+:8124/analyser user=BUILTIN\\Users\n\n" +
                    "This is the details of the exception thrown:" +
                    ex.Message +
                    "\n\nHit enter to continue without services.\n\n";

                Console.WriteLine(msg);

                Console.ReadLine();

                LogMessage(msg);
            }
        }

        private static void SetupMetrics()
        {
            //currently does nothing... will be extended once NMOS parsing is sorted           
        }

        private static void _tsAnalyserApi_StreamCommand(object sender, StreamCommandEventArgs e)
        {
            switch (e.Command)
            {
                case (StreamCommandType.ResetMetrics):
                    SetupMetrics();

                    _nmosAnalyserApi.NetworkMetric = _networkMetric;
                    _nmosAnalyserApi.RtpMetric = _rtpMetric;

                    break;
                case (StreamCommandType.StopStream):
                    //todo: implement
                    break;
                case (StreamCommandType.StartStream):
                    //todo: implement
                    break;
            }
        }
    }

}

