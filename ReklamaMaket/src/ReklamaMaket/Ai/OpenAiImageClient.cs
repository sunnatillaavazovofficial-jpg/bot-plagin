using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ReklamaMaket.Core;

namespace ReklamaMaket.Ai
{
    /// <summary>
    /// OpenAI Images API (ChatGPT rasm modeli) bilan ishlash. Har bir foydalanuvchi o'z API kalitidan foydalanadi.
    /// </summary>
    public sealed class OpenAiImageClient : IDisposable
    {
        /// <summary>API kalit olish sahifasi.</summary>
        public const string ApiKeysUrl = "https://platform.openai.com/api-keys";

        /// <summary>Tashkilotni tasdiqlash sahifasi (rasm modellari uchun talab qilinishi mumkin).</summary>
        public const string VerificationUrl = "https://platform.openai.com/settings/organization/general";

        public static readonly string[] Models = { "gpt-image-2", "gpt-image-2.5-flare", "gpt-image-2.5-sunburst", "chatgpt-image-latest", "gpt-image-1.5" };
        public static readonly string[] Qualities = { "low", "medium", "high" };
        public static readonly string[] Sizes = { "auto", "1536x1024", "1024x1536", "1024x1024" };

        private readonly HttpClient http;

        static OpenAiImageClient()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        }

        public OpenAiImageClient(string apiKey, string baseUrl = "https://api.openai.com/v1/")
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new UserException("ChatGPT API kaliti kiritilmagan. Kalitni platform.openai.com/api-keys sahifasidan oling.");

            http = new HttpClient
            {
                BaseAddress = new Uri(baseUrl),
                Timeout = TimeSpan.FromMinutes(6)
            };
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());
        }

        /// <summary>Kalit ishlashini tekshiradi (xato bo'lsa UserException).</summary>
        public async Task CheckKeyAsync(CancellationToken cancel)
        {
            using (HttpResponseMessage response = await SendAsync(() => http.GetAsync("models", cancel), cancel))
            {
                if (!response.IsSuccessStatusCode)
                    throw new UserException(DescribeError(response.StatusCode, await response.Content.ReadAsStringAsync()));
            }
        }

        /// <summary>Rasm(lar) asosida yangi rasm yaratadi va PNG baytlarini qaytaradi.</summary>
        public async Task<byte[]> EditImageAsync(IList<string> images, string prompt, string model, string size, string quality, CancellationToken cancel)
        {
            using (var form = new MultipartFormDataContent())
            {
                AddField(form, "model", model);
                AddField(form, "prompt", prompt);
                AddField(form, "quality", quality);
                if (!string.IsNullOrEmpty(size) && size != "auto")
                    AddField(form, "size", size);
                if (model.StartsWith("gpt-image-1.5", StringComparison.OrdinalIgnoreCase))
                    AddField(form, "input_fidelity", "high"); // logoni aniq saqlash uchun (faqat shu modelda bor)

                string field = images.Count > 1 ? "image[]" : "image";
                foreach (string file in images)
                    AddFile(form, field, file);

                using (HttpResponseMessage response = await SendAsync(() => http.PostAsync("images/edits", form, cancel), cancel))
                {
                    string body = await response.Content.ReadAsStringAsync();
                    if (!response.IsSuccessStatusCode)
                        throw new UserException(DescribeError(response.StatusCode, body));
                    return ReadImage(body);
                }
            }
        }

        public void Dispose() => http.Dispose();

        private static async Task<HttpResponseMessage> SendAsync(Func<Task<HttpResponseMessage>> send, CancellationToken cancel)
        {
            try
            {
                return await send();
            }
            catch (TaskCanceledException ex) when (!cancel.IsCancellationRequested)
            {
                throw new UserException("ChatGPT javob bermadi (vaqt tugadi). Keyinroq qayta urinib ko'ring.", ex);
            }
            catch (HttpRequestException ex)
            {
                throw new UserException("OpenAI serveriga ulanib bo'lmadi. Internetni tekshiring.", ex);
            }
        }

        private static void AddField(MultipartFormDataContent form, string name, string value)
        {
            var content = new StringContent(value ?? string.Empty);
            content.Headers.ContentType = null;
            content.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data") { Name = Quote(name) };
            form.Add(content);
        }

        private static void AddFile(MultipartFormDataContent form, string name, string path)
        {
            var content = new ByteArrayContent(File.ReadAllBytes(path));
            content.Headers.ContentType = new MediaTypeHeaderValue(MimeType(path));
            content.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
            {
                Name = Quote(name),
                FileName = Quote(Path.GetFileName(path))
            };
            form.Add(content);
        }

        private static string Quote(string value) => "\"" + value.Replace("\"", string.Empty) + "\"";

        private static string MimeType(string path)
        {
            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".jpg":
                case ".jpeg":
                    return "image/jpeg";
                case ".webp":
                    return "image/webp";
                default:
                    return "image/png";
            }
        }

        private static byte[] ReadImage(string body)
        {
            ImagesResponse response = Parse<ImagesResponse>(body);
            if (response?.Data != null)
            {
                foreach (ImageData item in response.Data)
                {
                    if (!string.IsNullOrEmpty(item?.B64Json))
                        return Convert.FromBase64String(item.B64Json);
                }
            }
            throw new UserException("ChatGPT rasm qaytarmadi. So'rovni o'zgartirib qayta urinib ko'ring.");
        }

        private static T Parse<T>(string json) where T : class
        {
            try
            {
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                    return new DataContractJsonSerializer(typeof(T)).ReadObject(stream) as T;
            }
            catch (SerializationException)
            {
                return null;
            }
        }

        private static string DescribeError(HttpStatusCode status, string body)
        {
            string message = Parse<ErrorResponse>(body)?.Error?.Message;
            message = string.IsNullOrWhiteSpace(message) ? body : message;
            switch ((int)status)
            {
                case 401:
                    return "API kalit noto'g'ri yoki bekor qilingan. Yangi kalit oling: " + ApiKeysUrl;
                case 403:
                    if (message != null && message.IndexOf("verif", StringComparison.OrdinalIgnoreCase) >= 0)
                        return "Rasm modelidan foydalanish uchun OpenAI tashkilotingizni tasdiqlash kerak: " + VerificationUrl;
                    return "Ruxsat yo'q: " + message;
                case 429:
                    return "Limit tugagan yoki hisobda mablag' yo'q (OpenAI → Billing). " + message;
                default:
                    return "ChatGPT xatosi (" + (int)status + "): " + message;
            }
        }
    }
    [DataContract]
    internal sealed class ImagesResponse
    {
        [DataMember(Name = "data")] public List<ImageData> Data { get; set; }
    }

    [DataContract]
    internal sealed class ImageData
    {
        [DataMember(Name = "b64_json")] public string B64Json { get; set; }
    }

    [DataContract]
    internal sealed class ErrorResponse
    {
        [DataMember(Name = "error")] public ApiError Error { get; set; }
    }

    [DataContract]
    internal sealed class ApiError
    {
        [DataMember(Name = "message")] public string Message { get; set; }
    }
}
