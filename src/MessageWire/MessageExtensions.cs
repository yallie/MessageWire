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

using NetMQ;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MessageWire
{
	internal static class MessageExtensions
	{
		public static List<string> ConvertToStrings(this Message msg, bool convertEncodingErrorToNull = true)
		{
			return msg.ConvertToStrings(Encoding.UTF8, convertEncodingErrorToNull);
		}

		public static List<string> ConvertToStrings(this Message msg, Encoding encoding, bool convertEncodingErrorToNull = true)
		{
			var list = new List<string>();
			if (msg.Frames == null || msg.Frames.Count == 0) return list;
			foreach (var frame in msg.Frames)
			{
				try
				{
					if (frame == null)
						list.Add((string)null);
					else
						list.Add(encoding.GetString(frame));
				}
				catch (Exception e)
				{
					if (convertEncodingErrorToNull)
						list.Add((string)null);
					else
						list.Add($"##EncodingError-Bytes({frame.Length})-{e.Message}-##");
				}
			}
			return list;
		}

		public static Message ToMessageWithoutClientFrame(this NetMQMessage msg, Guid clientId)
		{
			if (msg == null || msg.FrameCount == 0) return null;
			List<byte[]> frames = new List<byte[]>();
			if (msg.FrameCount > 0)
			{
				frames = (from n in msg where !n.IsEmpty select n.Buffer).ToList();
			}
			return new Message
			{
				ClientId = clientId,
				Frames = frames
			};
		}

		public static Message ToMessageWithClientFrame(this NetMQMessage msg)
		{
			if (msg == null || msg.FrameCount == 0) return null;
			if (msg[0].BufferSize != 16) return null; //must have a Guid id
			var clientId = new Guid(msg[0].Buffer);
			List<byte[]> frames = new List<byte[]>();
			if (msg.FrameCount > 1)
			{
				frames = (from n in msg where !n.IsEmpty select n.Buffer).Skip(1).ToList();
			}
			return new Message
			{
				ClientId = clientId,
				Frames = frames
			};
		}

		public static NetMQMessage ToNetMQMessage(this Message msg)
		{
			var message = new NetMQMessage();
			message.Append(msg.ClientId.ToByteArray());
			message.AppendEmptyFrame();
			if (null != msg.Frames)
			{
				foreach (var frame in msg.Frames)
				{
					message.Append(frame);
				}
			}
			else
			{
				message.AppendEmptyFrame();
			}
			return message;
		}
	}

	public class ProtocolFailureEventArgs : EventArgs
	{
		public string Message { get; set; }
	}

	public class MessageEventArgs : EventArgs
	{
		public Message Message { get; set; }
	}

	public class MessageEventFailureArgs : EventArgs
	{
		public MessageFailure Failure { get; set; }
	}

	public class MessageFailure
	{
		public string ErrorMessage { get; set; }
		public string ErrorCode { get; set; }
		public Message Message { get; set; }
	}
}
