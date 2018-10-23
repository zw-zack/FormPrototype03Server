using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Management;
using System.Runtime.Serialization.Formatters.Binary;
using System.Diagnostics;
using System.Threading;
using System.ServiceProcess;

namespace Server_Tutorial
{
    class Program
    {
        static void Main(string[] args)
        {
            //IPAddress ip = Dns.GetHostEntry("localhost").AddressList[0];
            IPAddress ipHost = Dns.GetHostAddresses("LA130029.global.avaya.com")[0];
            IPAddress ipSelf = Dns.GetHostAddresses("LA130029.global.avaya.com")[0];
            int sendPort = 8082;
            int listeningPort = 8080;

            TcpListener server = new TcpListener(ipHost, listeningPort);
            TcpClient client = default(TcpClient);
            List<string> systemInformationList = new List<string>();
            List<string> processInformationList = new List<string>();
            List<string> serviceInformationList = new List<string>();
            List<string> confirmationList = new List<string>();
            try
            {
                server.Start();
                Console.WriteLine("Server started...");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                Console.Read();
            }

            while (true)
            {
                client = server.AcceptTcpClient();
                byte[] receivedBuffer = new byte[100];
                NetworkStream stream = client.GetStream();
                stream.Read(receivedBuffer, 0, receivedBuffer.Length);

                StringBuilder msg = new StringBuilder();

                foreach (byte b in receivedBuffer)
                {
                    if (b.Equals(59))
                    {
                        break;
                    }
                    else
                    {
                        msg.Append(Convert.ToChar(b).ToString());
                    }
                }

                if (msg.ToString() == "Request Access")
                {
                    Console.WriteLine("Server Access Requested");

                    ApproveAccess();
                }

                if(msg.Length > 17)
                {
                    if (msg.ToString().Substring(0, 18) == "System Information")
                    {
                        Console.WriteLine("System Information Requested");
                        GetComponent("Win32_ComputerSystem", "Name");
                        GetComponent("Win32_ComputerSystem", "Model");
                        GetComponent("Win32_ComputerSystem", "UserName");
                        GetComponent("Win32_ComputerSystem", "SystemType");
                        GetComponent("Win32_ComputerSystem", "Domain");
                        GetComponent("Win32_Processor", "Name");
                        GetComponent("Win32_Processor", "ProcessorID");
                        GetComponent("Win32_VideoController", "Name");
                        GetComponent("Win32_OperatingSystem", "Name");
                        GetComponent("Win32_OperatingSystem", "SerialNumber");
                        GetComponent("Win32_OperatingSystem", "Version");
                        GetComponent("Win32_OperatingSystem", "FreePhysicalMemory");
                        GetComponent("Win32_OperatingSystem", "FreeSpaceInPagingFiles");
                        GetComponent("Win32_OperatingSystem", "TotalVirtualMemorySize");
                        GetComponent("Win32_OperatingSystem", "TotalVisibleMemorySize");
                        SendList();
                    }
                }

                

                if (msg.ToString() == "Process Information")
                {
                    Console.WriteLine("Process Information Requested");
                    SendProcessList();
                }

                if(msg.Length >= 12)
                {
                    if (msg.ToString().Substring(0, 12) == "Stop process")
                    {
                        Console.WriteLine("Stop process Requested");
                        string process = msg.ToString().Substring(13, msg.Length - 13);
                        StopProcess(process);
                    }

                    if (msg.ToString().Substring(0, 12) == "Stop service")
                    {
                        Console.WriteLine("Stop service Requested");

                        string service = msg.ToString().Substring(13, msg.Length - 13);
                        StopService(service);
                    }

                    if (msg.ToString().Substring(0, 13) == "Start service")
                    {
                        Console.WriteLine("Start service Requested");

                        string service = msg.ToString().Substring(14, msg.Length - 14);
                        StartService(service);
                    }
                }

                if (msg.ToString() == "Service Information")
                {
                    Console.WriteLine("Service Information Requested");
                    SendServiceList();
                }

            }
            void GetComponent(string hwclass, string syntax)
            {
                ManagementObjectSearcher mos = new ManagementObjectSearcher(
                    "root\\CIMV2",
                    "SELECT * FROM " +
                    hwclass);

                foreach (ManagementObject mj in mos.Get())
                {
                    systemInformationList.Add(hwclass.ToString()+" "+ Convert.ToString(syntax));
                }
                foreach (ManagementObject mj in mos.Get())
                {
                    systemInformationList.Add(Convert.ToString(mj[syntax]));
                }
            }

            void ApproveAccess()
            {
                try
                {
                    TcpClient clientResend = new TcpClient(ipHost.ToString(), sendPort);
                    int byteCount = Encoding.ASCII.GetByteCount(Convert.ToString("Approved") + 1);
                    byte[] sendData = new byte[byteCount];
                    sendData = Encoding.ASCII.GetBytes(Convert.ToString("Approved") + ";");
                    NetworkStream streamResend = clientResend.GetStream();

                    streamResend.Write(sendData, 0, sendData.Length);

                    streamResend.Close();
                    clientResend.Close();
                }
                catch { }
                
            }

            void SendList()
            {
                try
                {
                    TcpClient clientResend = new TcpClient(ipHost.ToString(), sendPort);
                    NetworkStream streamResend = clientResend.GetStream();
                    BinaryFormatter bf = new BinaryFormatter();
                    bf.Serialize(streamResend, systemInformationList);
                    streamResend.Close();
                    clientResend.Close();
                    systemInformationList = new List<string>();
                }
                catch { }
            }

            void SendProcessList()
            {
                try
                {
                    Process[] processList = Process.GetProcesses();
                    foreach (Process process in processList)
                    {
                        processInformationList.Add(process.ProcessName.ToString());
                        processInformationList.Add(process.Responding.ToString());
                    }
                    TcpClient clientResend = new TcpClient(ipHost.ToString(), sendPort);
                    NetworkStream streamResend = clientResend.GetStream();
                    BinaryFormatter bf = new BinaryFormatter();
                    bf.Serialize(streamResend, processInformationList);
                    streamResend.Close();
                    clientResend.Close();
                    processInformationList = new List<string>();
                }
                catch { }
                
            }


            void StopProcess(string processToStop)
            {
                try
                {
                    Process[] processList = Process.GetProcesses();
                    foreach (Process process in processList)
                    {
                        if (process.ProcessName == processToStop)
                        {
                            process.Kill();
                            Thread.Sleep(1000);

                            Process[] myProcess = Process.GetProcessesByName(process.ToString());
                            if (myProcess.Count() > 0)
                            {
                                SendProcessStopFailure();
                            }
                            else
                            {
                                SendProcessStopConfirmation();
                            }
                        }
                    }
                }
                catch { }
                
            }

            void SendProcessStopConfirmation()
            {
                try
                {
                    confirmationList.Add("Yes");
                    TcpClient clientResend = new TcpClient(ipHost.ToString(), sendPort);
                    NetworkStream streamResend = clientResend.GetStream();
                    BinaryFormatter bf = new BinaryFormatter();
                    bf.Serialize(streamResend, confirmationList);
                    streamResend.Close();
                    clientResend.Close();
                    confirmationList = new List<string>();
                }
                catch { }
                
            }

            void SendProcessStopFailure()
            {
                try
                {
                    confirmationList.Add("No");
                    TcpClient clientResend = new TcpClient(ipHost.ToString(), sendPort);
                    NetworkStream streamResend = clientResend.GetStream();
                    BinaryFormatter bf = new BinaryFormatter();
                    bf.Serialize(streamResend, confirmationList);
                    streamResend.Close();
                    clientResend.Close();
                    confirmationList = new List<string>();
                }
                catch { }
                
            }

            void SendServiceList()
            {
                try
                {
                    var scServices = ServiceController.GetServices();
                    foreach (var scTemp in scServices)
                    {
                        serviceInformationList.Add(scTemp.ServiceName.ToString());
                        serviceInformationList.Add(scTemp.Status.ToString());
                    }
                    TcpClient clientResend = new TcpClient(ipHost.ToString(), sendPort);
                    NetworkStream streamResend = clientResend.GetStream();
                    BinaryFormatter bf = new BinaryFormatter();
                    bf.Serialize(streamResend, serviceInformationList);
                    streamResend.Close();
                    clientResend.Close();
                    serviceInformationList = new List<string>();
                }
                catch { }
                
            }

            void StopService(string serviceToStop)
            {
                try
                {
                    var service = new ServiceController(serviceToStop);
                    service.Stop();
                    service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(10.0));
                    if (service.Status.ToString() == "Stopped")
                    {
                        SendServiceStopConfirmation();
                    }
                    else
                    {
                        SendServiceStopFailure();
                    }
                }
                catch
                {

                }
                
            }


            void SendServiceStopConfirmation()
            {
                confirmationList.Add("Yes");
                TcpClient clientResend = new TcpClient(ipHost.ToString(), sendPort);
                NetworkStream streamResend = clientResend.GetStream();
                BinaryFormatter bf = new BinaryFormatter();
                bf.Serialize(streamResend, confirmationList);
                streamResend.Close();
                clientResend.Close();
                confirmationList = new List<string>();
            }

            void SendServiceStopFailure()
            {
                confirmationList.Add("No");
                TcpClient clientResend = new TcpClient(ipHost.ToString(), sendPort);
                NetworkStream streamResend = clientResend.GetStream();
                BinaryFormatter bf = new BinaryFormatter();
                bf.Serialize(streamResend, confirmationList);
                streamResend.Close();
                clientResend.Close();
                confirmationList = new List<string>();
            }

            void StartService(string serviceToStart)
            {
                try
                {
                    var service = new ServiceController(serviceToStart);
                    service.Start();
                    service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10.0));
                    if (service.Status.ToString() == "Running")
                    {
                        SendServiceStartConfirmation();
                    }
                    else
                    {
                        SendServiceStartFailure();
                    }
                }
                catch { }

                
                
            }


            void SendServiceStartConfirmation()
            {
                confirmationList.Add("Yes");
                TcpClient clientResend = new TcpClient(ipHost.ToString(), sendPort);
                NetworkStream streamResend = clientResend.GetStream();
                BinaryFormatter bf = new BinaryFormatter();
                bf.Serialize(streamResend, confirmationList);
                streamResend.Close();
                clientResend.Close();
                confirmationList = new List<string>();
            }

            void SendServiceStartFailure()
            {
                confirmationList.Add("No");
                TcpClient clientResend = new TcpClient(ipHost.ToString(), sendPort);
                NetworkStream streamResend = clientResend.GetStream();
                BinaryFormatter bf = new BinaryFormatter();
                bf.Serialize(streamResend, confirmationList);
                streamResend.Close();
                clientResend.Close();
                confirmationList = new List<string>();
            }

        }

        
    }
}