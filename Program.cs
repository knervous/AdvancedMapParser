using System;
using System.Security.Cryptography.X509Certificates;
using System.Reflection;
using System.Linq;
using System.IO;
using WebSocketSharp;
using WebSocketSharp.Server;
using System.Dynamic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
using System.Text;
using AdvancedMapParser;
using System.Net.Http;
using System.Threading;

namespace AdvancedMapTool
{

    class AdvancedMapTool
    {

        private static readonly string version = "0.2.0";
        private static readonly List<EQSocket> socketConnections = new List<EQSocket>();
        private static readonly Dictionary<string, ParseInfo> parseInfoDict = new Dictionary<string, ParseInfo>();
        static void ExceptionHandler(object sender, UnhandledExceptionEventArgs args)
        {
            Exception e = (Exception)args.ExceptionObject;
            Console.WriteLine("Fatal Exception handler caught : " + e.Message);
            Console.WriteLine("Runtime terminating: {0}", args.IsTerminating);
            File.WriteAllText("AdvancedMapErrors.txt", e.Message);
        }

        static void Main(string[] args)
        {
            AppDomain currentDomain = AppDomain.CurrentDomain;
            currentDomain.UnhandledException += new UnhandledExceptionEventHandler(ExceptionHandler);
            var assembly = Assembly.GetExecutingAssembly();
            string certPfx = assembly.GetManifestResourceNames().Single(str => str.EndsWith("localhost.pfx"));
            X509Certificate2 cert = null;
            using (var stream = assembly.GetManifestResourceStream(certPfx))
            {
                byte[] buffer = new byte[stream.Length];
                stream.Read(buffer, 0, buffer.Length);
                cert = new X509Certificate2(buffer, "123");
            }

            using (X509Store store = new X509Store(StoreName.Root, StoreLocation.CurrentUser))
            {
                store.Open(OpenFlags.ReadWrite);
                if (!store.Certificates.Contains(cert))
                    store.Add(cert);
            }

            int port = -1;
            bool isHost = false;
            string localIP = string.Empty;
            string remoteIp = string.Empty;
            string hostIp = string.Empty;

            Console.WriteLine($"Launching temp0 Advanced Map Parser v{version}");
            Console.WriteLine($"Visit https://eqmap.vercel.app to connect maps.");
            Console.WriteLine($"Also visit https://eqrequiem.com to check out EQ Requiem.");

            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
            {
                socket.Connect("8.8.8.8", 65530);
                IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
                localIP = endPoint.Address.ToString();
            }

            try
            {
                foreach (var line in File.ReadAllLines("config.ini"))
                {
                    if (line.StartsWith("HOST="))
                    {
                        isHost = line.ToLower().EndsWith("true");
                    }

                    if (line.StartsWith("HOST_IP="))
                    {
                        remoteIp = line.Split('=')?[1] ?? string.Empty;
                    }

                    if (line.StartsWith("PORT="))
                    {
                        port = int.Parse(line.Split('=')?[1]);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Error reading config.ini file.");
                Console.WriteLine(e.ToString());
            }


            var wssv = new WebSocketServer(IPAddress.Any, port, true)
            {
                SslConfiguration = {
                    ServerCertificate = cert,
                    ClientCertificateRequired = false,
                    CheckCertificateRevocation = false,
                    ClientCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true,
                    EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12 |
                    System.Security.Authentication.SslProtocols.Tls |  System.Security.Authentication.SslProtocols.Tls11 |  System.Security.Authentication.SslProtocols.Tls12 |  System.Security.Authentication.SslProtocols.Ssl2 |  System.Security.Authentication.SslProtocols.Ssl3
                },
            };
            var httpsv = new HttpServer(IPAddress.Any, port, true)
            {
                SslConfiguration = {
                    ServerCertificate = cert,
                    ClientCertificateRequired = false,
                    CheckCertificateRevocation = false,
                    ClientCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true,
                    EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12 |
                    System.Security.Authentication.SslProtocols.Tls |  System.Security.Authentication.SslProtocols.Tls11 |  System.Security.Authentication.SslProtocols.Tls12 |  System.Security.Authentication.SslProtocols.Ssl2 |  System.Security.Authentication.SslProtocols.Ssl3
                },
            };

            httpsv.AddWebSocketService<EQSocket>("/maps", f =>
            {
                socketConnections.Add(f);
                new Thread(() =>
                {
                    Thread.Sleep(1);
                    foreach (var p in parseInfoDict)
                    {

                        f.Emit(new Message { Type = "parseInfo", Payload = p.Value });
                    }
                }).Start();

                f.OnDisconnectHandler = () =>
                {
                    socketConnections.Remove(f);
                };
            });
            httpsv.OnGet += (e, a) =>
            {
                a.Response.StatusCode = 200;
                a.Response.AddHeader("Access-Control-Allow-Origin", "*");
                a.Response.WriteContent(Encoding.ASCII.GetBytes("Successfully connected!"));
                a.Response.Close();
            };

            using (var httpClient = new HttpClient())
            {
                hostIp = httpClient.GetAsync("https://api.ipify.org").Result.Content.ReadAsStringAsync().Result;
            }

            if (isHost)
            {
                httpsv.OnPost += (e, a) =>
                   {
                       try
                       {
                           if (a.Request.HasEntityBody)
                           {
                               var str = new StreamReader(a.Request.InputStream).ReadToEnd();
                               var parseInfo = JsonConvert.DeserializeObject<ParseInfo>(str);
                               if (parseInfoDict.ContainsKey(parseInfo.displayedName))
                               {
                                   parseInfoDict[parseInfo.displayedName] = parseInfo;

                               }
                               else
                               {
                                   parseInfoDict.Add(parseInfo.displayedName, parseInfo);

                               }
                               foreach (var sock in socketConnections)
                               {
                                   sock.Emit(new Message { Type = "parseInfo", Payload = parseInfo });
                               }
                           }
                       }
                       catch (Exception err)
                       {
                       }
                       a.Response.StatusCode = 200;
                       a.Response.AddHeader("Access-Control-Allow-Origin", "*");
                       a.Response.Close();
                   };
            }


            httpsv.Start();

            Console.WriteLine("");
            Console.WriteLine("------------------------------------------------------------------------------");
            Console.WriteLine("|  Server is listening and can be accessed on the following addresses:");
            Console.WriteLine($"|  This local machine: wss://127.0.0.1:{port}");
            if (!string.IsNullOrEmpty(localIP))
            {
                Console.WriteLine($"|  On this machine's network: wss://{localIP}:{port}");
            }
            Console.WriteLine($"|  Or if configured as a host, wss://{hostIp}:{port}");
            if (isHost)
            {
                Console.WriteLine("|  You are configured as a host and will accept incoming log parses from other AdvancedMapParser clients.");
            }
            else
            {
                Console.WriteLine("|  You are not configured as a host.");
            }


            var handler = new HttpClientHandler();
            handler.ClientCertificateOptions = ClientCertificateOption.Manual;
            handler.ServerCertificateCustomValidationCallback =
                (httpRequestMessage, cert, cetChain, policyErrors) =>
            {
                return true;
            };

            var client = new HttpClient(handler);
            if (remoteIp != string.Empty)
            {
                client.BaseAddress = new Uri($"https://{remoteIp}");
                client.Timeout = TimeSpan.FromSeconds(2);
                Console.WriteLine($"|  You are configured to forward all log updates to {remoteIp}.");
                try
                {
                    if (client.GetAsync("")?.Result?.StatusCode == HttpStatusCode.OK)
                    {
                        Console.WriteLine("|  Successfully able to connect to host");
                    }
                    else
                    {
                        Console.WriteLine($"|  Could not connect to host! Are you sure they are accepting connections at {remoteIp}?");
                    }

                }
                catch (Exception excep)
                {
                    Console.WriteLine($"|  Could not connect to host! Are you sure they are accepting connections at {remoteIp}?");
                }

            }

            Console.WriteLine("------------------------------------------------------------------------------");
            Console.WriteLine("");



            var fileWatch = new FileWatcher();

            fileWatch.OnParseInfoCallback = (p) =>
            {
                if (parseInfoDict.ContainsKey(p.displayedName))
                {
                    parseInfoDict[p.displayedName] = p;

                }
                else
                {
                    parseInfoDict.Add(p.displayedName, p);

                }
                foreach (var sock in socketConnections)
                {
                    sock.Emit(new Message { Type = "parseInfo", Payload = p });
                }

                if (remoteIp != string.Empty)
                {
                    var ser = JsonConvert.SerializeObject(p);
                    var parseInfo = new StringContent(
                    ser,
                    Encoding.UTF8,
                    "application/json");

                    try
                    {
                        var httpResponse =
                         client.PostAsync("", parseInfo);
                    }
                    catch (Exception parseExcep)
                    {
                        Console.WriteLine($"Could not post to {remoteIp}");
                    }

                }
            };

            while (true)
            {
                System.Console.ReadKey();
            };

        }
    }

    public class Message
    {
        public string Type { get; set; }
        public string CallbackId { get; set; }
        public object Payload { get; set; }
        public Message()
        {
        }
        public Message(string data)
        {
            IDictionary<string, object> expandoDict = JsonConvert.DeserializeObject<ExpandoObject>(data, new ExpandoObjectConverter());
            Type = (string)expandoDict["type"];
            if (expandoDict.ContainsKey("callbackId"))
            {
                CallbackId = (string)expandoDict["callbackId"];
            }
            if (expandoDict.ContainsKey("payload"))
            {
                Payload = expandoDict["payload"];
            }

        }

    }

    public class EQSocket : WebSocketBehavior
    {

        delegate void ActionHandler<T1, T2>(T1 str, T2 optional = default(T2));
        private IDictionary<string, ActionHandler<object, Action<object>>> TaskMap { get; set; } = new Dictionary<string, ActionHandler<object, Action<object>>>();

        //  Emit(new Message { Type = "parseInfo", Payload = parseInfo });

        public delegate void OnDisconnect();
        public OnDisconnect OnDisconnectHandler { get; set; } = null;

        protected override void OnOpen()
        {
            base.OnOpen();
            TaskMap.Clear();

            Console.WriteLine($"Client connected {Context.UserEndPoint}");
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            var msg = new Message(e.Data);
            if (TaskMap.ContainsKey(msg.Type) && TaskMap[msg.Type] != null)
            {
                var callback = TaskMap[msg.Type];
                callback.Invoke(msg.Payload, callbackArg =>
                {
                    if (!string.IsNullOrEmpty(msg.CallbackId))
                    {
                        Emit(callbackArg);
                    }
                });
            };
        }

        public void Emit(object m)
        {
            try { Send(JsonConvert.SerializeObject(m)); } catch { }
        }

        protected override void OnError(WebSocketSharp.ErrorEventArgs e)
        {
            base.OnError(e);
        }

        protected override void OnClose(CloseEventArgs e)
        {
            if (OnDisconnectHandler != null)
            {
                OnDisconnectHandler();
            }
            base.OnClose(e);
        }
    }
}
