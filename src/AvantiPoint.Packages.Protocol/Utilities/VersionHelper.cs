using System.Text.RegularExpressions;

namespace AvantiPoint.Packages.Protocol.Utilities
{
    public static class VersionHelper
    {
        private const string VersionRegex = @"(\d+)(.\d+)?(.\d+)?(.\d+)?(-[A-Za-z0-9]+)?";

        public static string GetFormattedVersionConstraint(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            string format = "invalid";
            if (IsMinimumVersionInclusive(input))
            {
                format = $">= {GetVersion(input)}";
            }
            else if (IsMinimumVersionExclusive(input))
            {
                format = $"> {GetVersion(input)}";
            }
            else if (IsMaximumVersionInclusive(input))
            {
                format = $"<= {GetVersion(input)}";
            }
            else if (IsMaximumVersionExclusive(input))
            {
                format = $"< {GetVersion(input)}";
            }
            else if (IsExactRangeInclusive(input))
            {
                var temp = input.Split(',');
                format = $"{GetVersion(temp[0])} <= x <= {GetVersion(temp[1])}";
            }
            else if (IsExactRangeExclusive(input))
            {
                var temp = input.Split(',');
                format = $"{GetVersion(temp[0])} < x < {GetVersion(temp[1])}";
            }
            else if (IsMixed(input))
            {
                var temp = input.Split(',');
                format = $"{GetVersion(temp[0])} <= x < {GetVersion(temp[1])}";
            }
            else if (IsExactVersion(input))
            {
                format = $"== {GetVersion(input)}";
            }

            return format;
        }

        public static string GetVersion(string input)
        {
            var match = Regex.Match(input, VersionRegex);
            return match.Groups[0].Value;
        }
        public static bool IsMinimumVersionInclusive(string input)
        {
            var match = Regex.Match(input.Trim(), $@"^{VersionRegex}$");
            if (!match.Success)
            {
                match = Regex.Match(input.Trim(), $@"^\[( )?((\d+)(.\d+)?(.\d+)?(.\d+)?(-[A-Za-z0-9]+)?)( )?, \)$");
            }
            return match.Success;
        }

        public static bool IsMinimumVersionExclusive(string input)
        {
            var match = Regex.Match(input.Trim(), $@"^\((\w+)?({VersionRegex})(\w+)?,(\w+)?\)$");
            return match.Success;
        }

        public static bool IsExactVersion(string input)
        {
            var match = Regex.Match(input.Trim(), $@"^\[(\w+)?({VersionRegex})(\w+)?\]$");
            return match.Success;
        }

        public static bool IsMaximumVersionInclusive(string input)
        {
            var match = Regex.Match(input.Trim(), $@"^\((\w+)?,(\w+)?({VersionRegex})(\w+)?\]$");
            return match.Success;
        }

        public static bool IsMaximumVersionExclusive(string input)
        {
            var match = Regex.Match(input.Trim(), $@"^\((\w+)?,(\w+)?({VersionRegex})(\w+)?\)$");
            return match.Success;
        }

        public static bool IsExactRangeInclusive(string input)
        {
            var match = Regex.Match(input.Trim(), $@"^\[(\w+)?({VersionRegex})(\w+)?,(\w+)?({VersionRegex})(\w+)?\]$");
            return match.Success;
        }

        public static bool IsExactRangeExclusive(string input)
        {
            var match = Regex.Match(input.Trim(), $@"^\((\w+)?({VersionRegex})(\w+)?,(\w+)?({VersionRegex})(\w+)?\)$");
            return match.Success;
        }

        public static bool IsMixed(string input)
        {
            var match = Regex.Match(input.Trim(), $@"^\[(\w+)?({VersionRegex})(\w+)?,(\w+)?({VersionRegex})(\w+)?\)$");
            return match.Success;
        }
    }
}
