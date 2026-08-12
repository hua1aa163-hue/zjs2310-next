using System.Globalization;
using System.Text;
using ZJS2310.Core.Domain;

namespace ZJS2310.Core.Services;

public sealed class ResultPacketParser
{
    public bool TryParse(string message, out ResultPacket? packet, out string error)
    {
        packet = null;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(message))
        {
            error = "结果消息为空。";
            return false;
        }

        var trimmed = message.Trim();
        var marker = trimmed.IndexOf("_Result", StringComparison.OrdinalIgnoreCase);
        if (marker < 2 || trimmed[0] is not ('t' or 'T') ||
            !int.TryParse(trimmed.AsSpan(1, marker - 1), NumberStyles.None, CultureInfo.InvariantCulture, out var code))
        {
            error = "结果消息缺少合法的 tN_Result 标识。";
            return false;
        }

        var colon = trimmed.IndexOf(':', marker);
        if (colon < 0)
        {
            error = "结果消息缺少冒号分隔符。";
            return false;
        }

        var payload = trimmed[(colon + 1)..];
        var endMarker = payload.IndexOf(",%", StringComparison.Ordinal);
        if (endMarker >= 0)
        {
            payload = payload[..endMarker];
        }

        var tokens = payload.Split([' ', '\t', '\r', '\n', ','], StringSplitOptions.RemoveEmptyEntries);
        var values = new List<double>(tokens.Length);
        foreach (var token in tokens)
        {
            if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                error = $"结果中包含非数字值“{token}”。";
                return false;
            }

            values.Add(value);
        }

        if (values.Count == 0)
        {
            error = "结果消息没有数值。";
            return false;
        }

        packet = new ResultPacket(code, values, trimmed);
        return true;
    }
}

public sealed class ProtocolMessageFramer
{
    private readonly StringBuilder _buffer = new();

    public IReadOnlyList<string> Append(string chunk)
    {
        if (!string.IsNullOrEmpty(chunk))
        {
            _buffer.Append(chunk);
        }

        var messages = new List<string>();
        while (_buffer.Length > 0)
        {
            var text = _buffer.ToString();
            var resultEnd = text.IndexOf(",%", StringComparison.Ordinal);
            var lineEnd = text.IndexOfAny(['\r', '\n']);
            int take;
            if (resultEnd >= 0 && (lineEnd < 0 || resultEnd < lineEnd))
            {
                take = resultEnd + 2;
            }
            else if (lineEnd >= 0)
            {
                take = lineEnd;
            }
            else
            {
                break;
            }

            var message = text[..take].Trim();
            _buffer.Remove(0, take);
            while (_buffer.Length > 0 && _buffer[0] is '\r' or '\n')
            {
                _buffer.Remove(0, 1);
            }

            if (message.Length > 0)
            {
                messages.Add(message);
            }
        }

        return messages;
    }
}
