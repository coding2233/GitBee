//using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wanderer
{
    public enum LogLevel { LOG_TRACE, LOG_DEBUG, LOG_INFO, LOG_WARN, LOG_ERROR, LOG_FATAL };

    public interface ILogger
    {
        void Log(int level, string file, int line, string log);
    }


    public static class Log
    {
        public static ILogger Logger { get; internal set; } = new DefaultLogger();
        private static StringBuilder _logStringBuilder = new StringBuilder();

        private static void Full(string log, LogLevel logLevel)
        {
            StackFrame stackFrame = null;

            if (logLevel == LogLevel.LOG_TRACE)
            {
                stackFrame = GetTraceLog(ref log);
            }
            else if (logLevel == LogLevel.LOG_DEBUG)
            {
#if DEBUG
                stackFrame = GetTraceLog(ref log);

#else
                stackFrame = GetStackFrame();
#endif
            }
            else
            {
                stackFrame = GetStackFrame();

            }

            if (Logger != null)
            {
                Logger.Log((int)logLevel, stackFrame == null ? "NULL" : stackFrame.GetFileName(), stackFrame == null ? -1 : stackFrame.GetFileLineNumber(), log);
            }
        }

        public static void Trace(string log)
        {
            Full(log, LogLevel.LOG_TRACE);
        }
        public static void Trace(string format, params object[] args)
        {
            Trace(string.Format(format, args));
        }

        public static void Debug(string log)
        {
            Full(log, LogLevel.LOG_DEBUG);
        }

        public static void Debug(string format, params object[] args)
        {
            Debug(string.Format(format, args));
        }

        public static void Info(string log)
        {
            Full(log, LogLevel.LOG_INFO);
        }

        public static void Info(string format, params object[] args)
        {
            Info(string.Format(format, args));
        }

        public static void Warn(string log)
        {
            Full(log, LogLevel.LOG_WARN);
        }

        public static void Warn(string format, params object[] args)
        {
            Warn(string.Format(format, args));
        }

        public static void Error(string log)
        {
            Full(log, LogLevel.LOG_ERROR);
        }

        public static void Error(string format, params object[] args)
        {
            Error(string.Format(format, args));
        }

        public static void Fatal(string log)
        {
            Full(log, LogLevel.LOG_FATAL);
        }

        public static void Fatal(string format, params object[] args)
        {
            Fatal(string.Format(format, args));
        }

        private static StackFrame GetTraceLog(ref string log)
        {
            StackTrace stackTrace = new StackTrace(true);
            var frames = stackTrace.GetFrames();
            if (frames != null)
            {
                if (_logStringBuilder == null)
                {
                    _logStringBuilder = new StringBuilder();
                }
                _logStringBuilder.Clear();
                _logStringBuilder.AppendLine(log);
                for (int i = 0; i < frames.Length; i++)
                {
                    var item = frames[i];
                    _logStringBuilder.Append($"{Path.GetFileName(item.GetFileName())}");
                    _logStringBuilder.Append("\t");
                    _logStringBuilder.Append($"{item.GetFileLineNumber()}");
                    _logStringBuilder.Append("\t");
                    _logStringBuilder.Append(item.GetMethod());

                    if (i < frames.Length - 1)
                    {
                        _logStringBuilder.Append("\n");
                    }
                }
                log = _logStringBuilder.ToString();
            }

            return GetStackFrame(stackTrace);
        }

        private static StackFrame GetStackFrame(StackTrace stackTrace = null)
        {
            if (stackTrace == null)
            {
                stackTrace = new StackTrace(true);
            }
            StackFrame stackFrame = null;
            var frames = stackTrace.GetFrames();
            if (frames != null)
            {
                foreach (var item in frames)
                {
                    if (item == null)
                        continue;
                    string fileName = item.GetFileName();
                    if (!string.IsNullOrEmpty(fileName) && !fileName.EndsWith("Log.cs"))
                    {
                        stackFrame = item;
                        break;
                    }
                }
            }
            return stackFrame;
        }
    }

    internal class DefaultLogger : ILogger
    {
        StringBuilder m_strBuilder = new StringBuilder();
        public void Log(int level, string file, int line, string log)
        {
            m_strBuilder.Clear();
            m_strBuilder.Append(DateTime.Now.ToString("HH:mm:ss"));
            m_strBuilder.Append(" | ");
            string strLevel = ((LogLevel)level).ToString();
            strLevel = strLevel.Replace("LOG_", "");
            m_strBuilder.Append(strLevel);
            if (strLevel.Length < 5)
            {
                m_strBuilder.Append(" ");
            }
            m_strBuilder.Append(" | ");
            file = Path.GetFileName(file);
            m_strBuilder.Append(file);
            m_strBuilder.Append(":");
            m_strBuilder.Append(line);
            m_strBuilder.Append(": ");
            m_strBuilder.Append(log);
            Console.WriteLine(m_strBuilder.ToString());
        }
    }
}
