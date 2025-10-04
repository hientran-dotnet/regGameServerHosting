using Bogus.DataSets;
using Microsoft.Playwright;
using System.Net.WebSockets;
using System.Reflection.Metadata;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using static System.Net.WebRequestMethods;

namespace regGameServerHosting
{
    public record GemLoginResponseData(
        bool success,
        string message,
        GemLoginProfileData data
    );

    public record GemLoginProfileData(
        bool success,
        int profile_id,
        string browser_location,
        string remote_debugging_address,
        string driver_path
    );

    public class RegAccountInfo
    {
        public string firstName { get; set; }
        public string lastName { get; set; }
        public string phonenumber { get; set; }
        public string companyName { get; set; }
        public string streetAddress { get; set; }
        public string streetAddress2 { get; set; }
        public string city { get; set; }
        public string state { get; set; }
        public string postCode { get; set; }
        public string password { get; set; }
    }

    public class Program
    {
        const string TEMP_MAIL = "https://10minutemail.net/?lang=vi";
        const string REGISTER_SPARKEDHOST = "https://billing.sparkedhost.com/register.php";
        const string SERVER_GEMLOGIN = "http://localhost:1010";
        const int MAX_PROFILES = 5;
        static async Task Main(string[] args)
        {
            // CẤU HÌNH CONSOLE OUTPUT:
            Console.OutputEncoding = Encoding.UTF8;

            int profileCount = await RetrieveExistingProfileCountAsync();
            Console.WriteLine($"Tổng số profile hiện có: {profileCount}");

            // Giới hạn số profile


            //// Nếu đạt max -> yêu cầu xóa thủ công
            //if (profileCount == MAX_PROFILES)
            //{
            //    Console.WriteLine($"Đã đạt tối đa {MAX_PROFILES} profile. Hãy xóa thủ công 1 hoặc toàn bộ profile hiện tại!");
            //    Console.WriteLine("Nhấn Enter để tiếp tục (sau khi đã xóa bớt profile)");
            //    Console.ReadLine();

            //    // Lấy lại dữ liệu mới nhất sau khi người dùng xử lý
            //    profileCount = await RetrieveExistingProfileCountAsync();
            //}

            //// Nếu không còn profile nào, tạo mới
            //if (profileCount == 0)
            //{
            //    if (await CreateNewProfileAsync())
            //        Console.WriteLine("Tạo mới profile thành công !!");
            //}
            //// Nếu còn nhưng chưa max, vẫn có thể tạo thêm
            //else if (profileCount < MAX_PROFILES)
            //{
            //    if (await CreateNewProfileAsync())
            //        Console.WriteLine("Tạo mới profile thành công !!");
            //}

            //profileCount = await RetrieveExistingProfileCountAsync();
            //Console.WriteLine($"Tổng số profile hiện tại: {profileCount}");

            // BẮT ĐẦU TỰ ĐỘNG HÓA VỚI PLAYWRIGHT
            var runNewProfileId = profileCount; // Lấy profile mới tạo (có ID = tổng số profile hiện tại)
            var newProfile = await StartProfileAsync(runNewProfileId);
            Console.WriteLine(newProfile);

            string wsUrl = await GetWebSocketDebuggerUrlAsync(newProfile.data.remote_debugging_address);

            using var playwright = await Playwright.CreateAsync();
            var browser = await playwright.Chromium.ConnectOverCDPAsync(wsUrl);

            var context = browser.Contexts.FirstOrDefault() ?? await browser.NewContextAsync();
            var tempMail_page = context.Pages.FirstOrDefault() ?? await context.NewPageAsync();

            // MỞ 10MINUTEMAIL            
            await tempMail_page.GotoAsync(TEMP_MAIL);

            // Kiểm tra popup nếu có
            var consentButton = tempMail_page.Locator("button.fc-cta-consent");

            // Nếu thấy thì click
            if (await consentButton.IsVisibleAsync())
            {
                Console.WriteLine("Consent dialog xuất hiện, đang click...");
                await consentButton.ClickAsync();
            }
            else
            {
                Console.WriteLine("Không có dialog consent, tiếp tục...");
            }

            // LẤY EMAIL TẠM
            var email = await tempMail_page.Locator("#fe_text").InputValueAsync();

            Console.WriteLine($"Email tạm thời: {email}");

            var regSparkedHost = await context.NewPageAsync();
            await regSparkedHost.GotoAsync(REGISTER_SPARKEDHOST);

            // LẤY THÔNG TIN ĐĂNG KÝ
            var regInfo = await GenerateAccountInfoAsync();

            Console.WriteLine(JsonSerializer.Serialize(regInfo, new JsonSerializerOptions 
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));

            // Tìm các trường và điền thông tin vào
            try
            {
                // Nhập thông tin cá nhân
                try
                {
                    // Fill first name
                    await regSparkedHost.Locator("#inputFirstName").FillAsync(regInfo.firstName);

                    // Fill last name
                    await regSparkedHost.Locator("#inputLastName").FillAsync(regInfo.lastName);

                    // Fill email
                    await regSparkedHost.Locator("#inputEmail").FillAsync(email);

                    // Chọn mã điện thoại Việt Nam (+84)
                    // Mở dropdown mã quốc gia
                    await regSparkedHost.Locator(".selected-flag").ClickAsync();

                    // Bước 2: Chờ dropdown hiện ra
                    await regSparkedHost.WaitForSelectorAsync(".country-list:not(.hide)");

                    // Bước 3: Tìm và click vào Vietnam
                    // Cách 1: Dùng data-country-code
                    await regSparkedHost.Locator("li.country[data-country-code='vn']").ClickAsync();

                    // Fill phone number
                    await regSparkedHost.Locator("#inputPhone").FillAsync(regInfo.phonenumber);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Lỗi khi điền thông tin: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi tìm trường nhập liệu: {ex.Message}");
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

                if (profilesJson != null)
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

            // HÀM TẠO MỚI PROFILE
            static async Task<bool> CreateNewProfileAsync()
            {
                var httpClient = new HttpClient();
                using HttpResponseMessage responseMessage = await httpClient.PostAsync($"{SERVER_GEMLOGIN}/api/profiles/create", null);

                responseMessage.EnsureSuccessStatusCode();

                var jsonResponse = await responseMessage.Content.ReadAsStringAsync();
                return true;
            }

            static async Task<GemLoginResponseData?> StartProfileAsync(int profileId)
            {
                using var http = new HttpClient();
                var url = $"{SERVER_GEMLOGIN}/api/profiles/start/{profileId}";
                var response = await http.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<GemLoginResponseData>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return result;
            }

            static async Task<string> GetWebSocketDebuggerUrlAsync(string remoteDebuggingAddress)
            {
                using var http = new HttpClient();
                var response = await http.GetAsync($"http://{remoteDebuggingAddress}/json/version");
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();

                using var doc = JsonDocument.Parse(json);
                return doc.RootElement.GetProperty("webSocketDebuggerUrl").GetString();
            }

            static async Task<RegAccountInfo> GenerateAccountInfoAsync()
            {
                var faker = new Bogus.Faker("vi");
                var firstName = faker.Name.FirstName();
                var lastName = faker.Name.LastName();
                var phoneNumber = faker.Phone.PhoneNumber("0#########");
                var companyName = faker.Company.CompanyName();
                var streetAddress = faker.Address.StreetAddress();
                var streetAddress2 = faker.Address.SecondaryAddress();
                var city = faker.Address.City();
                var state = faker.Address.State();
                var postCode = faker.Address.ZipCode("######");
                var password = "Abc@123456"; // Mặc định
                return new RegAccountInfo
                {
                    firstName = firstName,
                    lastName = lastName,
                    phonenumber = phoneNumber,
                    companyName = companyName,
                    streetAddress = streetAddress,
                    streetAddress2 = streetAddress2,
                    city = city,
                    state = state,
                    postCode = postCode,
                    password = password
                };
            }
        }   
    } 
}
