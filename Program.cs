using System;
using System.Threading;
using System.Security.Cryptography.X509Certificates;
using System.Reflection;
using System.Linq;
using System.IO;
using WebSocketSharp;
using WebSocketSharp.Server;
using System.Threading.Tasks;
using System.Dynamic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
using System.Text.RegularExpressions;
using System.Text;

namespace AdvancedMapTool
{

    class AdvancedMapTool
    {

        private static readonly string version = "0.1.0";
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
            int userEnteredPort;
            var port = (args.Length > 0 && int.TryParse(args[0], out userEnteredPort)) ? userEnteredPort : 9004;

            Console.WriteLine($"Launching temp0 Advanced Map Parser v{version}");
            Console.WriteLine($"Visit https://eqmap.vercel.app to connect maps.");

            string localIP;
            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
            {
                socket.Connect("8.8.8.8", 65530);
                IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
                localIP = endPoint.Address.ToString();
            }

            var wssv = new WebSocketServer(System.Net.IPAddress.Any, port, true)
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
            var httpsv = new HttpServer(System.Net.IPAddress.Any, port, true)
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

            httpsv.AddWebSocketService<SocketIo>("/maps");
            httpsv.OnGet += (e, a) =>
            {
                a.Response.StatusCode = 200;
                a.Response.AddHeader("Access-Control-Allow-Origin", "*");
                a.Response.WriteContent(Encoding.ASCII.GetBytes("Successfully connected!"));
                a.Response.Close();
            };
            httpsv.Start();

            Console.WriteLine("");
            Console.WriteLine("------------------------------------------------------------------------------");
            Console.WriteLine("|  Server is listening and can be accessed on the following addresses:       |");
            Console.WriteLine($"|  This local machine: wss://127.0.0.1:{port}                                  |");
            if (!string.IsNullOrEmpty(localIP))
            {
                Console.WriteLine($"|  On this machine's network: wss://{localIP}:{port}                       |");
            }
            Console.WriteLine("------------------------------------------------------------------------------");
            Console.WriteLine("");



            while (true)
            {
                System.Console.ReadKey();
            };

        }
    }

    public class SocketIo : WebSocketBehavior
    {
        protected internal class Message
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
        delegate void ActionHandler<T1, T2>(T1 str, T2 optional = default(T2));
        private IDictionary<string, ActionHandler<object, Action<object>>> TaskMap { get; set; } = new Dictionary<string, ActionHandler<object, Action<object>>>();
        private readonly string locPattern = "Your Location is (.*), (.*), (.*)";
        private readonly string zonePattern = "There are \\d+ players in (.*)\\.";
        private readonly string enterZonePattern = "You have entered (.*)\\.";
        private readonly int maxLoc = 20;
        private FileSystemWatcher watcher = new FileSystemWatcher(Directory.GetCurrentDirectory());
        private ParseInfo parseInfo = new ParseInfo();
        private void On(string message, ActionHandler<object, Action<object>?> callback)
        {
            TaskMap[message] = callback;
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            var msg = new Message(e.Data);
            if (TaskMap[msg.Type] != null)
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

        private void Emit(object m)
        {
            try { Send(JsonConvert.SerializeObject(m)); } catch { }
        }

        protected override void OnOpen()
        {
            base.OnOpen();
            TaskMap.Clear();

            Console.WriteLine("Client connected. Listening for log file changes...");
            var cwd = Directory.GetCurrentDirectory();
            var files = Directory.GetFiles(cwd, "eqlog_*");

            watcher.NotifyFilter = NotifyFilters.Attributes
                                 | NotifyFilters.CreationTime
                                 | NotifyFilters.DirectoryName
                                 | NotifyFilters.FileName
                                 | NotifyFilters.LastAccess
                                 | NotifyFilters.LastWrite
                                 | NotifyFilters.Security
                                 | NotifyFilters.Size;

            watcher.Filter = "eqlog_*";
            watcher.IncludeSubdirectories = true;
            watcher.EnableRaisingEvents = true;

            On("startParse", (payload, callback) =>
            {
                watcher.Changed += OnChanged;
                watcher.Error += OnError;

            });

            On("stopParse", (payload, callback) =>
            {
                watcher.Changed -= OnChanged;
                watcher.Error -= OnError;
            });
        }

        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            if (e.ChangeType != WatcherChangeTypes.Changed)
            {
                return;
            }
            var name = e.Name.Split('_')[1];
            var lineReader = new ReverseLineReader(e.FullPath);
            parseInfo.locations.Clear();
            parseInfo.displayedName = name;
            bool zoneFound = false;
            bool enterZoneFound = false;
            foreach (var line in lineReader)
            {

                if (parseInfo.locations.Count >= maxLoc && zoneFound)
                {
                    break;
                }
                var locMatches = Regex.Matches(line, locPattern, RegexOptions.IgnoreCase);
                if (!enterZoneFound && locMatches.Count > 0 && parseInfo.locations.Count < maxLoc)
                {
                    Loc loc = new Loc
                    {
                        y = float.Parse(locMatches[0].Groups[1].Value),
                        x = float.Parse(locMatches[0].Groups[2].Value),
                        z = float.Parse(locMatches[0].Groups[3].Value)
                    };
                    if (parseInfo.locations.Any(l =>
                        l.x == loc.x && l.y == loc.y && l.z == loc.z
                    ))
                    {
                        continue;
                    }
                    parseInfo.locations.Add(loc);
                }
                if (line.Contains(name) && (line.Contains("ANONYMOUS") || Regex.IsMatch(line, $"[\\d+ \\w+] {name}")))
                {
                    parseInfo.displayedName = line.Substring(27).Trim();
                }
                var enterZoneMatch = Regex.Match(line, enterZonePattern);
                if (!zoneFound && enterZoneMatch.Success)
                {
                    parseInfo.zoneName = enterZoneMatch.Groups[1].Value;
                    zoneFound = true;
                    enterZoneFound = true;
                }
                var zoneMatch = Regex.Match(line, zonePattern);
                if (!zoneFound && zoneMatch.Success)
                {
                    parseInfo.zoneName = zoneMatch.Groups[1].Value;
                    zoneFound = true;
                }
            }
            Emit(new Message { Type = "parseInfo", Payload = parseInfo });
        }

        private void OnError(object sender, System.IO.ErrorEventArgs e) =>
            PrintException(e.GetException());

        private void PrintException(Exception? ex)
        {
            if (ex != null)
            {
                Console.WriteLine($"Message: {ex.Message}");
                Console.WriteLine("Stacktrace:");
                Console.WriteLine(ex.StackTrace);
                Console.WriteLine();
                PrintException(ex.InnerException);
            }
        }

        protected override void OnError(WebSocketSharp.ErrorEventArgs e)
        {
            base.OnError(e);
        }

        protected override void OnClose(CloseEventArgs e)
        {
            base.OnClose(e);
        }
    }
}
