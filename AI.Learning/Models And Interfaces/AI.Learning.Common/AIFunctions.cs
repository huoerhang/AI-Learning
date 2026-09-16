using System.ComponentModel;

namespace AI.Learning.Common
{
    public static class AIFunctions
    {
        [Description("获取指定地点的天气。")]
       public static string GetWeather([Description("地点名称")] string location)
        {
            return $"{location} 今天多云，最高 15°C";
        }

        [Description("根据邮政编码获取当地的天气情况。")]
        public static string GetWeaterByPostalCode([Description("邮政编码")] string postalCode)
        {
            return $"{postalCode} 今天多云，最高 15°C";
        }

        [Description("获取指定城市的编码。")]
        public static string GetPostalCode([Description("城市名称")]string location)
        {
            return $"{location}的邮政编码是：718400";
        }

    }
}
