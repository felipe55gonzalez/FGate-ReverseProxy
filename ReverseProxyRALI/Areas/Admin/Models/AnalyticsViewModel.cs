using System.Text.Json;

namespace FGate.Areas.Admin.Models
{
    public class AnalyticsViewModel
    {
        public string HourlyTrafficChartJson { get; set; } = "{}";
        public string HttpMethodsChartJson { get; set; } = "{}";
        public string ErrorsChartJson { get; set; } = "{}";
        public string LatencyChartJson { get; set; } = "{}";
        public string SlowestEndpointsChartJson { get; set; } = "{}";
        public string TrafficByGroupChartJson { get; set; } = "{}";

        public static string SerializeEChartData(object data)
        {
            return JsonSerializer.Serialize(data, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }
    }
}