using System;
using System.Globalization;
using System.Text;

namespace ExecutionFlow.Abstractions
{
    /// <summary>
    /// The format shared by every ID (custom IDs, recurring job IDs and the IDs given to query and control methods):
    /// Unicode letters, combining marks and decimal digits, the characters <c>_ - . + ` [ ] , =</c> and the space, not
    /// starting or ending with a space. Control characters, invisible format characters and line separators
    /// are rejected, so a validated ID can't forge or disguise log lines (specs/016-id-format-validation FR-001).
    /// </summary>
    internal static class IdFormat
    {
        internal const string AllowedDescription = "IDs can only contain letters, digits, spaces and _ - . + ` [ ] , =, and can't start or end with a space";

        /// <summary>Whether <paramref name="id"/> matches the ID format.</summary>
        public static bool IsValid(string id)
        {
            if (id == null || id.Length == 0 || id[0] == ' ' || id[id.Length - 1] == ' ')
                return false;
            for (var i = 0; i < id.Length; i++)
            {
                var c = id[i];

                if (IsAllowedSymbol(c))
                    continue;

                if (char.IsHighSurrogate(c))
                {
                    if (i + 1 >= id.Length || !char.IsLowSurrogate(id[i + 1]))
                        return false;
                    if (!IsAllowedCategory(CharUnicodeInfo.GetUnicodeCategory(id, i)))
                        return false;
                    i++;
                    continue;
                }

                if (!IsAllowedCategory(CharUnicodeInfo.GetUnicodeCategory(c)))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Throws when <paramref name="id"/> doesn't match the ID format: <see cref="ArgumentNullException"/> for <c>null</c>,
        /// otherwise <see cref="ArgumentException"/> with the ID escaped.
        /// </summary>
        public static void Validate(string id, string paramName)
        {
            if (id == null)
                throw new ArgumentNullException(paramName);
            if (!IsValid(id))
                throw new ArgumentException($"The ID '{Escape(id)}' is invalid. {AllowedDescription}.", paramName);
        }

        /// <summary>
        /// Throws <see cref="ArgumentException"/> naming <paramref name="handlerType"/> when the recurring job ID
        /// <paramref name="id"/> doesn't match the ID format.
        /// </summary>
        public static void ValidateRecurring(Type handlerType, string id, string paramName)
        {
            if (id == null)
                throw new ArgumentNullException(paramName);
            if (!IsValid(id))
                throw new ArgumentException(
                    $"The recurring job ID '{Escape(id)}' of handler '{handlerType?.FullName}' is invalid. {AllowedDescription}.", paramName);
        }

        /// <summary>
        /// Escapes control, format, line/paragraph separator and lone surrogate characters (<c>\r</c>, <c>\n</c>, <c>\t</c>,
        /// <c>\0</c>, <c>\\</c>, otherwise <c>\uXXXX</c>), so the text fits on one line and shows what was there.
        /// </summary>
        public static string Escape(string value)
        {
            if (value == null)
                return null;

            StringBuilder builder = null;
            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                var replacement = EscapeChar(value, i);
                if (replacement == null)
                {
                    builder?.Append(c);
                    continue;
                }

                if (builder == null)
                    builder = new StringBuilder(value.Length + 8).Append(value, 0, i);
                builder.Append(replacement);
            }

            return builder?.ToString() ?? value;
        }

        private static string EscapeChar(string value, int index)
        {
            var c = value[index];
            switch (c)
            {
                case '\r': return "\\r";
                case '\n': return "\\n";
                case '\t': return "\\t";
                case '\0': return "\\0";
                case '\\': return "\\\\";
            }

            if (char.IsHighSurrogate(c) && index + 1 < value.Length && char.IsLowSurrogate(value[index + 1]))
                return null;
            if (char.IsLowSurrogate(c) && index > 0 && char.IsHighSurrogate(value[index - 1]))
                return null;

            switch (CharUnicodeInfo.GetUnicodeCategory(c))
            {
                case UnicodeCategory.Control:
                case UnicodeCategory.Format:
                case UnicodeCategory.LineSeparator:
                case UnicodeCategory.ParagraphSeparator:
                case UnicodeCategory.Surrogate:
                    return "\\u" + ((int)c).ToString("X4", CultureInfo.InvariantCulture);
                default:
                    return null;
            }
        }

        private static bool IsAllowedSymbol(char c)
        {
            switch (c)
            {
                case '_':
                case '-':
                case '.':
                case '+':
                case '`':
                case '[':
                case ']':
                case ',':
                case '=':
                case ' ':
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsAllowedCategory(UnicodeCategory category)
        {
            switch (category)
            {
                case UnicodeCategory.UppercaseLetter:
                case UnicodeCategory.LowercaseLetter:
                case UnicodeCategory.TitlecaseLetter:
                case UnicodeCategory.ModifierLetter:
                case UnicodeCategory.OtherLetter:
                case UnicodeCategory.LetterNumber:
                case UnicodeCategory.NonSpacingMark:
                case UnicodeCategory.SpacingCombiningMark:
                case UnicodeCategory.DecimalDigitNumber:
                    return true;
                default:
                    return false;
            }
        }
    }
}
