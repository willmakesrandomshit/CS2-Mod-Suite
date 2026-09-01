using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace DiscordRPC
{
    internal sealed class PresenceSnapshot
    {
        public string Mode = "Main Menu";
        public string Activity = "In the Main Menu";
        public string City = string.Empty;
        public string Population = string.Empty;
        public string PopulationLine = string.Empty;
        public string Money = string.Empty;
        public string MoneyLine = string.Empty;
        public string Date = string.Empty;
        public string Season = string.Empty;
        public string Weather = string.Empty;
        public string Temperature = string.Empty;
        public string WeatherLine = string.Empty;
        public string Traffic = string.Empty;
        public string TrafficLine = string.Empty;
        public string Source = "Cities: Skylines II";
        public string LargeImage = "milestone0";
        public string LargeText = "Cities: Skylines II";
        public string SmallImage = null;
        public string SmallText = null;
        public DateTime SessionStart = DateTime.Now;
    }

    internal static class PresenceFormatting
    {
        private static readonly Regex s_Separators = new Regex(@"\s*([·•|])\s*([·•|]|$)", RegexOptions.Compiled);
        private static readonly Regex s_Whitespace = new Regex(@"\s{2,}", RegexOptions.Compiled);

        public static string Render(string template, PresenceSnapshot s, string fallback)
        {
            if (string.IsNullOrWhiteSpace(template)) template = fallback;
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["activity"] = s.Activity, ["city"] = s.City, ["mode"] = s.Mode,
                ["population"] = s.Population, ["population_line"] = s.PopulationLine,
                ["money"] = s.Money, ["money_line"] = s.MoneyLine,
                ["date"] = s.Date, ["season"] = s.Season, ["weather"] = s.Weather,
                ["temperature"] = s.Temperature, ["weather_line"] = s.WeatherLine,
                ["traffic"] = s.Traffic, ["traffic_line"] = s.TrafficLine,
                ["source"] = s.Source
            };
            string value = Regex.Replace(template, @"\{([a-z_]+)\}", m => values.TryGetValue(m.Groups[1].Value, out string v) ? v ?? string.Empty : m.Value, RegexOptions.IgnoreCase);
            value = s_Separators.Replace(value, string.Empty);
            value = s_Whitespace.Replace(value, " ").Trim(' ', '·', '•', '|', '-', ',');
            if (value.Length > 128) value = value.Substring(0, 125) + "...";
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        public static string FormatPopulation(int value, PopulationPrivacy privacy)
        {
            if (privacy == PopulationPrivacy.Hidden) return string.Empty;
            if (privacy == PopulationPrivacy.Exact) return value.ToString("N0", CultureInfo.InvariantCulture);
            if (value < 1000) return (Math.Max(0, value / 100) * 100).ToString("N0", CultureInfo.InvariantCulture);
            if (value < 100000) return (Math.Round(value / 1000d) * 1000).ToString("N0", CultureInfo.InvariantCulture);
            return (Math.Round(value / 10000d) * 10000).ToString("N0", CultureInfo.InvariantCulture);
        }

        public static string FormatMoney(long value, MoneyPrivacy privacy)
        {
            if (privacy == MoneyPrivacy.Hidden) return string.Empty;
            if (privacy == MoneyPrivacy.Exact) return value.ToString("N0", CultureInfo.InvariantCulture) + "¢";
            double abs = Math.Abs((double)value);
            if (abs >= 1000000000) return (value / 1000000000d).ToString("0.#", CultureInfo.InvariantCulture) + "B¢";
            if (abs >= 1000000) return (value / 1000000d).ToString("0.#", CultureInfo.InvariantCulture) + "M¢";
            if (abs >= 1000) return (value / 1000d).ToString("0.#", CultureInfo.InvariantCulture) + "K¢";
            return value.ToString("N0", CultureInfo.InvariantCulture) + "¢";
        }
    }
}
