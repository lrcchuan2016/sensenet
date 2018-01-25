﻿using System.Text;

namespace SenseNet.Packaging.Tests.Implementations
{
    public class PackagingTestLogger : IPackagingLogger
    {
        public LogLevel AcceptedLevel => LogLevel.File;
        private readonly StringBuilder _sb;
        public PackagingTestLogger(StringBuilder sb)
        {
            _sb = sb;
        }
        public string LogFilePath => "[in memory]";
        public void Initialize(LogLevel level, string logFilePath) { }
        public void WriteTitle(string title)
        {
            _sb.AppendLine("================================");
            _sb.AppendLine(title);
            _sb.AppendLine("================================");
        }
        public void WriteMessage(string message)
        {
            _sb.AppendLine(message);
        }
    }
}
