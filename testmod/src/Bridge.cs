using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Godot;

namespace P3TestMod;

/// <summary>
/// 导演接口：本机 HTTP（只监听 127.0.0.1，默认端口 47021，环境变量 P3_BRIDGE_PORT 可改，P3_BRIDGE=0 关闭）。
/// 外部脚本 / MCP 通过它驱动游戏录制演示：查状态、往手牌加卡、用真实鼠标拖牌出牌、点结束回合、点界面按钮……
/// 请求在后台线程收发，命令本身全部排队到主线程（每帧 Pump 一次）执行，命令可以跨多帧（拖牌动画），做完才回包。
/// 参数：POST 的 JSON 正文，或 URL 查询参数（?card=xxx&amp;target=0），两种都行。
/// 回包：{"ok":true, ...} 或 {"ok":false,"error":"..."}。
/// </summary>
public static class Bridge
{
	public const int DefaultPort = 47021;
	private static readonly ConcurrentQueue<Action> _main = new();
	private static readonly JsonSerializerOptions _json = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
	private static bool _started;

	public sealed class Fail : Exception
	{
		public Fail(string message) : base(message) { }
	}

	public static void Start()
	{
		if (_started || System.Environment.GetEnvironmentVariable("P3_BRIDGE") == "0")
		{
			return;
		}
		_started = true;
		int port = int.TryParse(System.Environment.GetEnvironmentVariable("P3_BRIDGE_PORT"), out int p) ? p : DefaultPort;
		try
		{
			TcpListener listener = new(IPAddress.Loopback, port);
			listener.Start();
			Task.Run(() => AcceptLoop(listener));
			GD.Print($"{TestEntry.Tag} director bridge listening on 127.0.0.1:{port}");
		}
		catch (Exception e)
		{
			GD.PrintErr($"{TestEntry.Tag} director bridge failed to start on port {port}: {e.Message}");
		}
	}

	/// <summary>主线程每帧调用：执行排队的命令。</summary>
	public static void Pump()
	{
		int n = 0;
		while (n++ < 16 && _main.TryDequeue(out Action? job))
		{
			job();
		}
	}

	private static async Task AcceptLoop(TcpListener listener)
	{
		while (true)
		{
			TcpClient client;
			try
			{
				client = await listener.AcceptTcpClientAsync();
			}
			catch (Exception e)
			{
				GD.PrintErr($"{TestEntry.Tag} bridge accept failed: {e.Message}");
				await Task.Delay(500);
				continue;
			}
			_ = Task.Run(() => Handle(client));
		}
	}

	private static async Task Handle(TcpClient client)
	{
		using (client)
		{
			try
			{
				NetworkStream stream = client.GetStream();
				List<byte> buf = new();
				byte[] chunk = new byte[8192];
				int headerEnd = -1;
				while (headerEnd < 0)
				{
					int n = await stream.ReadAsync(chunk);
					if (n <= 0 || buf.Count > (1 << 20)) return;
					buf.AddRange(new ArraySegment<byte>(chunk, 0, n));
					headerEnd = IndexOf(buf, "\r\n\r\n"u8.ToArray());
				}
				string head = Encoding.UTF8.GetString(buf.GetRange(0, headerEnd).ToArray());
				string[] lines = head.Split("\r\n");
				string[] first = lines[0].Split(' ');
				string target = first.Length > 1 ? first[1] : "/";
				int length = 0;
				foreach (string line in lines)
				{
					int colon = line.IndexOf(':');
					if (colon > 0 && line[..colon].Trim().Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
					{
						int.TryParse(line[(colon + 1)..].Trim(), out length);
					}
				}
				int bodyStart = headerEnd + 4;
				while (buf.Count - bodyStart < length)
				{
					int n = await stream.ReadAsync(chunk);
					if (n <= 0) break;
					buf.AddRange(new ArraySegment<byte>(chunk, 0, n));
				}
				string body = buf.Count > bodyStart ? Encoding.UTF8.GetString(buf.GetRange(bodyStart, Math.Min(length, buf.Count - bodyStart)).ToArray()) : "";

				string path = target;
				JsonObject args = new();
				int q = target.IndexOf('?');
				if (q >= 0)
				{
					path = target[..q];
					foreach (string kv in target[(q + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries))
					{
						int eq = kv.IndexOf('=');
						string k = Uri.UnescapeDataString(eq >= 0 ? kv[..eq] : kv);
						string v = eq >= 0 ? Uri.UnescapeDataString(kv[(eq + 1)..].Replace('+', ' ')) : "true";
						args[k] = v;
					}
				}
				if (body.Trim().Length > 0 && JsonNode.Parse(body) is JsonObject json)
				{
					foreach (KeyValuePair<string, JsonNode?> kv in json)
					{
						args[kv.Key] = kv.Value?.DeepClone();
					}
				}

				int code = 200;
				object payload;
				try
				{
					Runtime.RecordingTopUp = true;                 // 录制脚本要连续出牌：单人局里能量 / 辉星保持 99
					object? result = await OnMain(() => Director.Dispatch(path.TrimEnd('/'), args));
					payload = result ?? new { ok = true };
				}
				catch (Fail f)
				{
					code = 400;
					payload = new { ok = false, error = f.Message };
				}
				catch (Exception e)
				{
					code = 500;
					payload = new { ok = false, error = e.GetType().Name + ": " + e.Message };
					GD.PrintErr($"{TestEntry.Tag} bridge {path} failed: {e}");
				}
				byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, _json));
				string header = $"HTTP/1.1 {code} {(code == 200 ? "OK" : "Error")}\r\nContent-Type: application/json; charset=utf-8\r\nContent-Length: {bytes.Length}\r\nConnection: close\r\n\r\n";
				await stream.WriteAsync(Encoding.ASCII.GetBytes(header));
				await stream.WriteAsync(bytes);
			}
			catch (Exception e)
			{
				GD.PrintErr($"{TestEntry.Tag} bridge connection failed: {e.Message}");
			}
		}
	}

	private static Task<object?> OnMain(Func<Task<object?>> work)
	{
		TaskCompletionSource<object?> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
		_main.Enqueue(async () =>
		{
			try
			{
				tcs.SetResult(await work());
			}
			catch (Exception e)
			{
				tcs.SetException(e);
			}
		});
		return tcs.Task;
	}

	private static int IndexOf(List<byte> data, byte[] pattern)
	{
		for (int i = 0; i + pattern.Length <= data.Count; i++)
		{
			int j = 0;
			while (j < pattern.Length && data[i + j] == pattern[j]) j++;
			if (j == pattern.Length) return i;
		}
		return -1;
	}

	// ── 参数读取（JSON 数字 / 字符串都接受）──

	public static string? Str(JsonObject a, string key)
	{
		if (a[key] is not JsonValue v) return null;
		if (v.TryGetValue(out string? s)) return s;
		return v.ToJsonString();
	}

	public static double Num(JsonObject a, string key, double fallback)
	{
		if (a[key] is not JsonValue v) return fallback;
		if (v.TryGetValue(out double d)) return d;
		return double.TryParse(Str(a, key), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out d) ? d : fallback;
	}

	public static int Int(JsonObject a, string key, int fallback) => (int)Math.Round(Num(a, key, fallback));

	public static bool Bool(JsonObject a, string key, bool fallback)
	{
		if (a[key] is not JsonValue v) return fallback;
		if (v.TryGetValue(out bool b)) return b;
		string? s = Str(a, key)?.Trim().ToLowerInvariant();
		return s switch { "1" or "true" or "yes" or "on" => true, "0" or "false" or "no" or "off" => false, _ => fallback };
	}

	public static List<string> List(JsonObject a, string key)
	{
		List<string> list = new();
		if (a[key] is JsonArray arr)
		{
			foreach (JsonNode? n in arr)
			{
				if (n is JsonValue v) list.Add(v.TryGetValue(out string? s) ? s : v.ToJsonString());
			}
		}
		else if (Str(a, key) is string one)
		{
			list.AddRange(one.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
		}
		return list;
	}
}
