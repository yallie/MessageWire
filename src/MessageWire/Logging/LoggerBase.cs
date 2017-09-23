﻿/* * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * *
 *  MessageWire - https://github.com/tylerjensen/MessageWire
 *
 * The MIT License (MIT)
 * Copyright (C) 2016-2017 Tyler Jensen
 * Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated
 * documentation files (the "Software"), to deal in the Software without restriction, including without limitation
 * the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software,
 * and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED
 * TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL
 * THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF
 * CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
 * DEALINGS IN THE SOFTWARE.
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;

namespace MessageWire.Logging
{
    public abstract class LoggerBase
    {
        protected object _syncRoot = new object();
        protected string _logDirectory = null;
        protected string _logFilePrefix = null;
        protected string _logFileExtension = null;
        protected bool _useUtcTimeStamp = false;
        protected ConcurrentQueue<string[]> _logQueue = new ConcurrentQueue<string[]>();
        protected LogOptions _options = LogOptions.LogOnlyToFile;
        protected LogRollOptions _rollOptions = LogRollOptions.Daily;
        protected string _logFileNameFormat = null;
        protected int _messageBufferSize = 32;
        protected int _rollMaxMegaBytes = 1024;
        protected Exception _lastError = null;

        public LogOptions LogOptions
        {
            get { return _options; }
            set
            {
                _options = value;
                if (_options != LogOptions.LogOnlyToConsole)
                {
                    if (!_logFileExtension.StartsWith(".")) _logFileExtension = "." + _logFileExtension;
                    _logFileNameFormat = Path.Combine(_logDirectory, _logFilePrefix + "{0}" + _logFileExtension);
                }
            }
        }

        public virtual void FlushLog()
        {
            WriteBuffer(int.MaxValue);
        }

        protected const string TimeStampPattern = "yyyy-MM-ddTHH:mm:ss.fff";
        protected string GetTimeStamp()
        {
            return _useUtcTimeStamp
                ? DateTime.UtcNow.ToString(TimeStampPattern)
                : DateTime.Now.ToString(TimeStampPattern);
        }

        protected string GetTimeStamp(DateTime dt)
        {
            return _useUtcTimeStamp
                ? dt.ToUniversalTime().ToString(TimeStampPattern)
                : dt.ToString(TimeStampPattern);
        }

        protected void WriteBuffer(int count)
        {
            try
            {
                var list = new List<string>();
                for (int i = 0; i < count; i++)
                {
                    string[] msg;
                    _logQueue.TryDequeue(out msg);
                    if (null == msg) break;
                    list.AddRange(msg);
                }
                if (list.Count == 0) return; //nothing to log
                var lines = list.ToArray();
                if (_options == LogOptions.LogOnlyToConsole || _options == LogOptions.LogToBoth)
                {
                    Console.Write(lines);
                }
                if (_options == LogOptions.LogOnlyToFile || _options == LogOptions.LogToBoth)
                {
                    WriteToFile(lines);
                }
            }
            catch (Exception e)
            {
                _lastError = e;
            }
        }

        private void WriteToFile(string[] lines)
        {
            lock (_syncRoot)
            {
                try
                {
                    var fileName = GetFileName();
#if (!NET35)
                    File.AppendAllLines(fileName, lines);
#else
                    File.AppendAllText(fileName, string.Join("\r\n", lines));
#endif
                }
                catch
                {
                    //todo ?
                }
            }
        }

        private string _currentFileName = null;
        private int _currentFileOffset = -1;
        private int _currentWritesCount = 0;

        private string GetFileName()
        {
            //by size
            if (_rollOptions == LogRollOptions.Size)
            {
                if (_currentFileOffset < 0) _currentFileOffset = GetCurrentFileOffset();
                if (null == _currentFileName)
                {
                    _currentFileName = GetSizeFileName(_currentFileOffset);
                    return _currentFileName;
                }
                //should we check size?
                if (_currentWritesCount * _messageBufferSize >= 3200) //100 writes at 32 per
                {
                    _currentWritesCount = 0; //reset
                    var fi = new FileInfo(_currentFileName);
                    if (fi.Length > _rollMaxMegaBytes * 1024 * 1024)
                    {
                        _currentFileOffset++;
                        _currentFileName = GetSizeFileName(_currentFileOffset);
                    }
                }
                return _currentFileName;
            }

            //based on roll options
            if (_rollOptions == LogRollOptions.Hourly)
            {
                if (_useUtcTimeStamp) return string.Format(_logFileNameFormat, DateTime.UtcNow.ToString(ToDateHourPattern));
                return string.Format(_logFileNameFormat, DateTime.Now.ToString(ToDateHourPattern));
            }
            if (_useUtcTimeStamp) return string.Format(_logFileNameFormat, DateTime.UtcNow.ToString(ToDateOnlyPattern));
            return string.Format(_logFileNameFormat, DateTime.Now.ToString(ToDateOnlyPattern));
        }

        private const string ToDateOnlyPattern = "yyyyMMdd";
        private const string ToDateHourPattern = "yyyyMMdd-HH";

        private int GetCurrentFileOffset()
        {
            return Directory.GetFiles(_logDirectory, _logFilePrefix + "*").Length;
        }

        private const string FileNameFormatSizePattern = "00000000";
        private string GetSizeFileName(int offset)
        {
            return string.Format(_logFileNameFormat, offset.ToString(FileNameFormatSizePattern));
        }
    }
}