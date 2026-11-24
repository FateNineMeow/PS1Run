using System;
using System.IO;
using System.Text;
using Ps1Run.Core.PowerShell;

namespace Ps1Run.Core.Tests
{
    internal static class ScriptEncodingDetectorTests
    {
        public static void RunAll()
        {
            DetectUtf8Bom();
            DetectUtf16Le();
            DetectUtf16Be();
            DetectUtf8NoBom();
            DetectAnsi();
            DetectEmpty();
        }

        private static ScriptEncodingKind Detect(byte[] bytes)
        {
            const string path = @"C:\fake\script.ps1";
            var fs = new FakeFileSystem();
            fs.Add(path, bytes);
            return new ScriptEncodingDetector(fs).Detect(path);
        }

        private static void DetectUtf8Bom()
        {
            var body = new UTF8Encoding(false).GetBytes("Write-Host 'hi'");
            var bytes = new byte[3 + body.Length];
            bytes[0] = 0xEF; bytes[1] = 0xBB; bytes[2] = 0xBF;
            Array.Copy(body, 0, bytes, 3, body.Length);
            Assert.Equal(ScriptEncodingKind.Utf8Bom, Detect(bytes), "UTF-8 BOM");
        }

        private static void DetectUtf16Le()
        {
            var body = new UnicodeEncoding(false, false).GetBytes("Write-Host 'hi'");
            var bytes = new byte[2 + body.Length];
            bytes[0] = 0xFF; bytes[1] = 0xFE;
            Array.Copy(body, 0, bytes, 2, body.Length);
            Assert.Equal(ScriptEncodingKind.Utf16Le, Detect(bytes), "UTF-16 LE");
        }

        private static void DetectUtf16Be()
        {
            var body = new UnicodeEncoding(true, false).GetBytes("Write-Host 'hi'");
            var bytes = new byte[2 + body.Length];
            bytes[0] = 0xFE; bytes[1] = 0xFF;
            Array.Copy(body, 0, bytes, 2, body.Length);
            Assert.Equal(ScriptEncodingKind.Utf16Be, Detect(bytes), "UTF-16 BE");
        }

        private static void DetectUtf8NoBom()
        {
            var bytes = new UTF8Encoding(false).GetBytes("Write-Host '中文'");
            Assert.Equal(ScriptEncodingKind.Utf8NoBom, Detect(bytes), "UTF-8 无 BOM");
        }

        private static void DetectAnsi()
        {
            var bytes = Encoding.GetEncoding(936).GetBytes("Write-Host '中文'");
            Assert.Equal(ScriptEncodingKind.Ansi, Detect(bytes), "GBK/ANSI");
        }

        private static void DetectEmpty()
        {
            Assert.Equal(ScriptEncodingKind.Empty, Detect(Array.Empty<byte>()), "空文件");
        }
    }
}
