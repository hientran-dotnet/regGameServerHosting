using Microsoft.Playwright;
using System.Net.WebSockets;
using System.Reflection.Metadata;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static System.Net.WebRequestMethods;

namespace regGameServerHosting
{
    public class Program
    {
        const string TEMP_MAIL = "https://10minutemail.net/?lang=vi";
        const string REGISTER_SPARKEDHOST = "https://billing.sparkedhost.com/register.php";
        const string SERVER_GEMLOGIN = "http://localhost:1010";
        static async Task Main(string[] args)
        {
            // CẤU HÌNH CONSOLE OUTPUT:
            Console.OutputEncoding = Encoding.UTF8;

            // Gọi api lấy số profile đang tồn tại
            int existingProfileCount = await RetrieveExistingProfileCountAsync();
            if(existingProfileCount == -9999999)
            {
                Console.WriteLine("Lỗi khi lấy số profile đang tồn tại. Kết thúc chương trình.");
                return;
            }
            Console.WriteLine($"Số profile đang tồn tại: {existingProfileCount}");
        }

        // HÀM TRẢ VỀ JSON CÁC PROFILE ĐANG TỒN TẠI
        static async Task<string> RetrieveExistingProfileAsync()
        {
            var httpCient = new HttpClient();

            using HttpResponseMessage response = await httpCient.GetAsync($"{SERVER_GEMLOGIN}/api/profiles");

            response.EnsureSuccessStatusCode();

            var jsonResponse = await response.Content.ReadAsStringAsync();
            return jsonResponse;
        }

        // HÀM TRẢ VỀ SỐ PROFILE ĐANG TỒN TẠI
        static async Task<int> RetrieveExistingProfileCountAsync()
        {
            // Biến lưu trữ số lần thử lại nếu không được
            int attempt = 5;

            string profilesJson = string.Empty;
            // Gọi hàm RetrieveExistingProfileAsync để lấy danh sách profile
            while (attempt > 0)
            {
                var response = await RetrieveExistingProfileAsync();
                if (!string.IsNullOrEmpty(response))
                {
                    profilesJson = response;
                    break;
                }
                --attempt;
                await Task.Delay(1000);
            }

            if(profilesJson != null)
            {
                using var doc = JsonDocument.Parse(profilesJson);
                if (doc.RootElement.TryGetProperty("data", out var dataEl) && dataEl.ValueKind == JsonValueKind.Array)
                {
                    int count = dataEl.GetArrayLength();
                    //Console.WriteLine($"Số profile: {count}");
                    return count;
                }
            }
            return -9999999;
        }
    }
}
