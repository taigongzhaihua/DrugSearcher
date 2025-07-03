using DrugSearcher.Common.Enums;

namespace DrugSearcher.Models
{
    public record ThemeConfig(ThemeMode Mode, ThemeColor Color)
    {
        public override string ToString()
        {
            return $"{Mode}-{Color}";
        }

        public static ThemeConfig FromString(string value)
        {
            try
            {
                var parts = value.Split('-');
                if (parts.Length == 2 &&
                    Enum.TryParse<ThemeMode>(parts[0], out var mode) &&
                    Enum.TryParse<ThemeColor>(parts[1], out var color))
                {
                    return new ThemeConfig(mode, color);
                }
            }
            catch
            {
                // 解析失败时返回默认值
            }

            return new ThemeConfig(ThemeMode.Light, ThemeColor.Blue);
        }
    }
}