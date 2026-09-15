using System;
using System.Collections.Generic;
using System.Linq;

namespace AuroraClock.Services
{
    public sealed record CityEntry(string Name, string Id, string Group);

    /// <summary>A curated catalogue of world cities mapped to Windows time-zone ids.</summary>
    public static class TimeZoneCatalog
    {
        public static IReadOnlyList<CityEntry> All { get; } = new List<CityEntry>
        {
            new("本地 Local", "", "★"),
            new("北京 Beijing", "China Standard Time", "亚洲"),
            new("上海 Shanghai", "China Standard Time", "亚洲"),
            new("香港 Hong Kong", "China Standard Time", "亚洲"),
            new("台北 Taipei", "Taipei Standard Time", "亚洲"),
            new("东京 Tokyo", "Tokyo Standard Time", "亚洲"),
            new("大阪 Osaka", "Tokyo Standard Time", "亚洲"),
            new("首尔 Seoul", "Korea Standard Time", "亚洲"),
            new("新加坡 Singapore", "Singapore Standard Time", "亚洲"),
            new("吉隆坡 Kuala Lumpur", "Singapore Standard Time", "亚洲"),
            new("曼谷 Bangkok", "SE Asia Standard Time", "亚洲"),
            new("雅加达 Jakarta", "SE Asia Standard Time", "亚洲"),
            new("河内 Hanoi", "SE Asia Standard Time", "亚洲"),
            new("马尼拉 Manila", "Singapore Standard Time", "亚洲"),
            new("孟买 Mumbai", "India Standard Time", "亚洲"),
            new("新德里 New Delhi", "India Standard Time", "亚洲"),
            new("达卡 Dhaka", "Bangladesh Standard Time", "亚洲"),
            new("迪拜 Dubai", "Arabian Standard Time", "中东"),
            new("阿布扎比 Abu Dhabi", "Arabian Standard Time", "中东"),
            new("利雅得 Riyadh", "Arab Standard Time", "中东"),
            new("多哈 Doha", "Arab Standard Time", "中东"),
            new("德黑兰 Tehran", "Iran Standard Time", "中东"),
            new("伊斯坦布尔 Istanbul", "Turkey Standard Time", "欧洲"),
            new("莫斯科 Moscow", "Russian Standard Time", "欧洲"),
            new("圣彼得堡 St. Petersburg", "Russian Standard Time", "欧洲"),
            new("基辅 Kyiv", "FLE Standard Time", "欧洲"),
            new("赫尔辛基 Helsinki", "FLE Standard Time", "欧洲"),
            new("华沙 Warsaw", "Central European Standard Time", "欧洲"),
            new("柏林 Berlin", "W. Europe Standard Time", "欧洲"),
            new("慕尼黑 Munich", "W. Europe Standard Time", "欧洲"),
            new("巴黎 Paris", "Romance Standard Time", "欧洲"),
            new("马德里 Madrid", "Romance Standard Time", "欧洲"),
            new("罗马 Rome", "W. Europe Standard Time", "欧洲"),
            new("阿姆斯特丹 Amsterdam", "W. Europe Standard Time", "欧洲"),
            new("苏黎世 Zurich", "W. Europe Standard Time", "欧洲"),
            new("维也纳 Vienna", "W. Europe Standard Time", "欧洲"),
            new("斯德哥尔摩 Stockholm", "W. Europe Standard Time", "欧洲"),
            new("奥斯陆 Oslo", "W. Europe Standard Time", "欧洲"),
            new("哥本哈根 Copenhagen", "W. Europe Standard Time", "欧洲"),
            new("布拉格 Prague", "Central Europe Standard Time", "欧洲"),
            new("布达佩斯 Budapest", "Central Europe Standard Time", "欧洲"),
            new("雅典 Athens", "GTB Standard Time", "欧洲"),
            new("里斯本 Lisbon", "GMT Standard Time", "欧洲"),
            new("伦敦 London", "GMT Standard Time", "欧洲"),
            new("都柏林 Dublin", "GMT Standard Time", "欧洲"),
            new("爱丁堡 Edinburgh", "GMT Standard Time", "欧洲"),
            new("雷克雅未克 Reykjavik", "Greenwich Standard Time", "欧洲"),
            new("纽约 New York", "Eastern Standard Time", "美洲"),
            new("华盛顿 Washington", "Eastern Standard Time", "美洲"),
            new("波士顿 Boston", "Eastern Standard Time", "美洲"),
            new("多伦多 Toronto", "Eastern Standard Time", "美洲"),
            new("迈阿密 Miami", "Eastern Standard Time", "美洲"),
            new("芝加哥 Chicago", "Central Standard Time", "美洲"),
            new("达拉斯 Dallas", "Central Standard Time", "美洲"),
            new("墨西哥城 Mexico City", "Central Standard Time (Mexico)", "美洲"),
            new("丹佛 Denver", "Mountain Standard Time", "美洲"),
            new("凤凰城 Phoenix", "US Mountain Standard Time", "美洲"),
            new("洛杉矶 Los Angeles", "Pacific Standard Time", "美洲"),
            new("旧金山 San Francisco", "Pacific Standard Time", "美洲"),
            new("西雅图 Seattle", "Pacific Standard Time", "美洲"),
            new("拉斯维加斯 Las Vegas", "Pacific Standard Time", "美洲"),
            new("温哥华 Vancouver", "Pacific Standard Time", "美洲"),
            new("安克雷奇 Anchorage", "Alaskan Standard Time", "美洲"),
            new("檀香山 Honolulu", "Hawaiian Standard Time", "美洲"),
            new("圣保罗 São Paulo", "E. South America Standard Time", "美洲"),
            new("里约热内卢 Rio de Janeiro", "E. South America Standard Time", "美洲"),
            new("布宜诺斯艾利斯 Buenos Aires", "Argentina Standard Time", "美洲"),
            new("圣地亚哥 Santiago", "Pacific SA Standard Time", "美洲"),
            new("利马 Lima", "SA Pacific Standard Time", "美洲"),
            new("波哥大 Bogotá", "SA Pacific Standard Time", "美洲"),
            new("开罗 Cairo", "Egypt Standard Time", "非洲"),
            new("约翰内斯堡 Johannesburg", "South Africa Standard Time", "非洲"),
            new("开普敦 Cape Town", "South Africa Standard Time", "非洲"),
            new("内罗毕 Nairobi", "E. Africa Standard Time", "非洲"),
            new("拉各斯 Lagos", "W. Central Africa Standard Time", "非洲"),
            new("卡萨布兰卡 Casablanca", "Morocco Standard Time", "非洲"),
            new("悉尼 Sydney", "AUS Eastern Standard Time", "大洋洲"),
            new("墨尔本 Melbourne", "AUS Eastern Standard Time", "大洋洲"),
            new("布里斯班 Brisbane", "E. Australia Standard Time", "大洋洲"),
            new("珀斯 Perth", "W. Australia Standard Time", "大洋洲"),
            new("阿德莱德 Adelaide", "Cen. Australia Standard Time", "大洋洲"),
            new("奥克兰 Auckland", "New Zealand Standard Time", "大洋洲"),
            new("惠灵顿 Wellington", "New Zealand Standard Time", "大洋洲"),
            new("UTC · 协调世界时", "UTC", "其他"),
        };

        public static IEnumerable<CityEntry> Search(string? query)
        {
            if (string.IsNullOrWhiteSpace(query)) return All;
            var q = query.Trim();
            return All.Where(c =>
                c.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                c.Id.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                c.Group.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        public static TimeZoneInfo Resolve(string? id)
        {
            if (string.IsNullOrWhiteSpace(id)) return TimeZoneInfo.Local;
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch { return TimeZoneInfo.Local; }
        }

        public static string OffsetText(string? id)
        {
            var tz = Resolve(id);
            var off = tz.GetUtcOffset(DateTime.UtcNow);
            var sign = off < TimeSpan.Zero ? "-" : "+";
            var a = off.Duration();
            return $"UTC{sign}{a.Hours:00}:{a.Minutes:00}";
        }
    }
}
