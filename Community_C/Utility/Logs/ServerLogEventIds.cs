namespace Community_C.Utility.Logs;

internal static class ServerLogEventIds
{
    internal static class Http
    {
        public const int RequestCompleted = 1000;
        public const int RequestRejected = 1001;
        public const int RequestFailed = 1002;
        public const int UnhandledException = 1003;
        public const int RequestCanceled = 1004;
    }

    internal static class Database
    {
        public const int Ready = 1100;
        public const int Unavailable = 1101;
        public const int CheckFailed = 1102;
        public const int KeepAliveSucceeded = 1103;
        public const int KeepAliveFailed = 1104;
    }

    internal static class OAuth
    {
        public const int RemoteRequestFailed = 1200;
        public const int ResponseInvalid = 1201;
    }

    internal static class Application
    {
        public const int BoardListLoadFailed = 1300;
        public const int LocalUserRegistered = 1301;
    }
}
