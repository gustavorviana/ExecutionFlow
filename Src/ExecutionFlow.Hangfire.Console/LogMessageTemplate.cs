using System;
using System.Globalization;
using System.Text;

namespace ExecutionFlow.Hangfire.Console
{
    /// <summary>
    /// Formats messages the way <c>Microsoft.Extensions.Logging</c> does: each <c>{placeholder}</c>, named or numeric, with
    /// optional <c>,alignment</c> and <c>:format</c>, takes the argument in the same position (order of appearance).
    /// <c>{{</c> and <c>}}</c> are literal braces. The same message then reads the same in the console and in <c>ILogger</c>.
    /// </summary>
    internal static class LogMessageTemplate
    {
        public static string Format(string message, object[] args)
        {
            if (string.IsNullOrEmpty(message) || args == null || args.Length == 0)
                return message;

            var result = new StringBuilder(message.Length);
            var argumentIndex = 0;
            var i = 0;

            while (i < message.Length)
            {
                var c = message[i];

                if (c == '{' && i + 1 < message.Length && message[i + 1] == '{')
                {
                    result.Append('{');
                    i += 2;
                    continue;
                }

                if (c == '}' && i + 1 < message.Length && message[i + 1] == '}')
                {
                    result.Append('}');
                    i += 2;
                    continue;
                }

                if (c == '{')
                {
                    var close = message.IndexOf('}', i + 1);
                    if (close < 0)
                    {
                        // Unclosed brace: write the rest as is.
                        result.Append(message, i, message.Length - i);
                        break;
                    }

                    var placeholder = message.Substring(i + 1, close - i - 1);
                    if (argumentIndex < args.Length)
                        result.Append(FormatArgument(args[argumentIndex], placeholder));
                    else
                        result.Append(message, i, close - i + 1);   // no argument left: keep the placeholder as written

                    argumentIndex++;
                    i = close + 1;
                    continue;
                }

                result.Append(c);
                i++;
            }

            return result.ToString();
        }

        /// <summary>Applies the placeholder's optional alignment (<c>,-10</c>) and format (<c>:N2</c>).</summary>
        private static string FormatArgument(object argument, string placeholder)
        {
            string format = null;
            var alignment = 0;

            var formatStart = placeholder.IndexOf(':');
            if (formatStart >= 0)
            {
                format = placeholder.Substring(formatStart + 1);
                placeholder = placeholder.Substring(0, formatStart);
            }

            var alignmentStart = placeholder.IndexOf(',');
            if (alignmentStart >= 0)
                int.TryParse(placeholder.Substring(alignmentStart + 1).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out alignment);

            var text = argument is IFormattable formattable && format != null
                ? formattable.ToString(format, CultureInfo.CurrentCulture)
                : Convert.ToString(argument, CultureInfo.CurrentCulture) ?? string.Empty;

            if (alignment > 0)
                return text.PadLeft(alignment);
            if (alignment < 0)
                return text.PadRight(-alignment);
            return text;
        }
    }
}
