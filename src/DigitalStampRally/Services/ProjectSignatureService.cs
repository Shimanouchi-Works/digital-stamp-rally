using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Encodings.Web;

namespace DigitalStampRally.Services
{
    /// <summary>
    /// project.json 用 HMAC署名ユーティリティ
    /// </summary>
    public static class ProjectSignatureService
    {
        /// <summary>
        /// 署名用に固定する JsonSerializerOptions
        /// ※ 必ず保存・検証で同じ設定を使うこと
        /// </summary>
        public static readonly JsonSerializerOptions SignJsonOptions = new()
        {
            WriteIndented = false,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            PropertyNamingPolicy = null
        };

        /// <summary>
        /// HMAC-SHA256 を base64url 形式で生成
        /// </summary>
        public static string ComputeHmacBase64Url(string secret, string payload)
        {
            using var h = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var bytes = h.ComputeHash(Encoding.UTF8.GetBytes(payload));
            return Base64UrlEncode(bytes);
        }

        /// <summary>
        /// オブジェクトをJSON化してHMAC生成
        /// </summary>
        public static string ComputeForObject(string secret, object obj)
        {
            var json = JsonSerializer.Serialize(obj, SignJsonOptions);
            return ComputeHmacBase64Url(secret, json);
        }

        /// <summary>
        /// base64url エンコード
        /// </summary>
        private static string Base64UrlEncode(byte[] bytes)
        {
            return Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        /// <summary>
        /// 固定時間比較（改ざんチェック用）
        /// </summary>
        public static bool SecureEquals(string a, string b)
        {
            var aBytes = Encoding.UTF8.GetBytes(a);
            var bBytes = Encoding.UTF8.GetBytes(b);

            return CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
        }
    }
}
