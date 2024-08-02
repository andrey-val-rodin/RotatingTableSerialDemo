using System.IO.Ports;

namespace RotatingTableSerialDemo
{
    internal class Terminal
    {
        private readonly SerialPort _port = new();
        private Listener _listener;
        // Keeps response after sending command
        private string _response;
        // Response timeout after sending command
        private readonly AutoResetEvent _listeningEvent = new(false);
        // This timer is used to check that table sends messages periodically during rotation
        private readonly System.Timers.Timer _listeningTimer = new(3000) { AutoReset = false };

        public Terminal()
        {
            _listeningTimer.Elapsed += (s, e) =>
            {
                // This message may appear after a sudden power outage of the table during rotation
                Console.WriteLine("Table stopped sending messages in the process of rotation");
            };
        }

        public async Task ProcessAsync()
        {
            if (!OpenPort())
                return;

            PrintAvailableCommands();

            try
            {
                while (true)
                {
                    var command = Console.ReadLine();
                    if (string.IsNullOrEmpty(command))
                        return;

                    var response = await SendCommandAndGetResponseAsync(command);
                    var color = ConsoleColor.Yellow;
                    if (response == "ERR")
                        color = ConsoleColor.Red;
                    WriteLine(response, color);

                    if (command.StartsWith("FM ") && response == "OK")
                    {
                        // Start listening
                        _listener.DataReceived += RotateHandler;
                        _listeningTimer.Start();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                Console.WriteLine("Oops! Something goes wrong...");
            }
            finally
            {
                _port.Close();
            }
        }

        private static void WriteLine(string response, ConsoleColor color = ConsoleColor.Yellow, params object[] arg)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(response, arg);
            Console.ForegroundColor = ConsoleColor.White;
        }

        private bool OpenPort()
        {
            try
            {
                // Allow the user to enter port name
                _port.PortName = SetPortName(_port.PortName);
                _port.BaudRate = 115200;
                _port.ReadTimeout = SerialPort.InfiniteTimeout;
                _port.WriteTimeout = 100;
                _port.Open();
                _listener = new();
                _listener.BeginListening(_port);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Oops! Something goes wrong...");
                Console.WriteLine(ex.ToString());
                return false;
            }
        }

        private static string SetPortName(string defaultPortName)
        {
            // Display Port values and prompt user to enter a port
            string portName;

            Console.WriteLine("Available Ports:");
            foreach (string s in SerialPort.GetPortNames())
            {
                WriteLine("   {0}", ConsoleColor.Yellow, s);
            }

            Console.Write("Enter COM port value (Default: {0}): ", defaultPortName);
            portName = Console.ReadLine();

            if (portName == "" || !(portName.ToLower()).StartsWith("com"))
            {
                portName = defaultPortName;
            }

            return portName;
        }

        private static void PrintAvailableCommands()
        {
            Console.WriteLine("Available commands:");
            WriteLine("   STATUS");
            WriteLine("   SET ACC X");
            WriteLine("   GET ACC");
            WriteLine("   FM X");
            WriteLine("   STOP");
            WriteLine("   SOFTSTOP");
        }

        private async Task<string> SendCommandAndGetResponseAsync(string command)
        {
            System.Diagnostics.Debug.WriteLine($"Command: {command}");
            _response = null;
            _listener.DataReceived += CommandHandler;
            try
            {
                _listeningEvent.Reset();
                _port.WriteLine(command);

#if DEBUG
                var stopwatch = new System.Diagnostics.Stopwatch();
                stopwatch.Start();
#endif
                await Task.Run(() => _listeningEvent.WaitOne(500));
#if DEBUG
                stopwatch.Stop();
                System.Diagnostics.Debug.WriteLine(
                    $"[{command}] Response time: {stopwatch.ElapsedMilliseconds} ms");
#endif

                var result = _response;
                if (result == null)
                {
                    var error = $"Table not responding to command. Command: {command}";
                    System.Diagnostics.Debug.WriteLine(error);
                    throw new InvalidOperationException(error);
                }
                
                return result;
            }
            catch
            {
                throw;
            }
            finally
            {
                _listener.DataReceived -= CommandHandler;
            }
        }

        private void CommandHandler(object sender, ReceivedDataEventArgs args)
        {
            // Take only first accepted token
            if (!string.IsNullOrEmpty(_response))
                return;

            // Number is acceptable as a response to command "GET ACC"
            if (int.TryParse(args.Token, out int _))
            {
                _response = args.Token;
                _listeningEvent.Set();
                return;
            }

            switch (args.Token)
            {
                case "OK":
                case "ERR":
                case "READY":
                case "BUSY":
                case "UNKNOWN":
                    _response = args.Token;
                    _listeningEvent.Set();
                    break;
                default:
                    break; // continue
            }
        }

        private void RotateHandler(object sender, ReceivedDataEventArgs args)
        {
            // Reset timer
            _listeningTimer.Stop();
            _listeningTimer.Start();

            if (args.Token.StartsWith("POS "))
            {
                System.Diagnostics.Debug.WriteLine(args.Token);
            }
            else if (args.Token == "END")
            {
                // Table has finished rotating
                System.Diagnostics.Debug.WriteLine(args.Token);
                _listener.DataReceived -= RotateHandler;
                _listeningTimer.Stop();
            }
            else if (args.Token == "MOVERR")
            {
                WriteLine(args.Token, ConsoleColor.Red);
            }
        }
    }
}
