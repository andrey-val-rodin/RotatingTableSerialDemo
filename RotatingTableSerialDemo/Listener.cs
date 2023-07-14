using System.IO.Ports;

namespace RotatingTableSerialDemo
{
    internal class Listener
    {
        public event ReceivedDataHandler DataReceived;

        private SerialPort _port;

        public void BeginListening(SerialPort port)
        {
            _port = port ?? throw new ArgumentNullException(nameof(port));
            Task.Run(Listen);
        }

        private void Listen()
        {
            try
            {
                while (true)
                {
                    var token = _port.ReadLine();
                    DataReceived?.Invoke(this, new ReceivedDataEventArgs(token));
                }
            }
            catch (OperationCanceledException) { }
        }
    }
}
