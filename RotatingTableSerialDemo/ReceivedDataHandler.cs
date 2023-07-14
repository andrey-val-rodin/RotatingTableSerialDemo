namespace RotatingTableSerialDemo
{
    public delegate void ReceivedDataHandler(object sender, ReceivedDataEventArgs args);

    public class ReceivedDataEventArgs : EventArgs
    {
        public ReceivedDataEventArgs(string token)
        {
            Token = token;
        }

        public string Token { private set; get; }
    }
}
