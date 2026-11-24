using System;
using System.IO;
using Ps1Run.Core.Infrastructure;

namespace Ps1Run.Core.PowerShell
{
    /// <summary>
    /// 脚本编码判型（wiki 07 §4）。
    /// 只做判定，不做解码，因此不依赖代码页。
    /// </summary>
    public enum ScriptEncodingKind
    {
        Empty = 0,
        Utf8Bom = 1,
        Utf16Le = 2,
        Utf16Be = 3,
        Utf8NoBom = 4,
        Ansi = 5,
        Unknown = 6,
    }

    public sealed class ScriptEncodingDetector
    {
        /// <summary>超过该大小只做头部采样，避免异常大文件拖慢启动。</summary>
        public const int MaxFullScanBytes = 32 * 1024 * 1024;

        private readonly IFileSystem _fileSystem;

        public ScriptEncodingDetector(IFileSystem? fileSystem = null)
        {
            _fileSystem = fileSystem ?? PhysicalFileSystem.Instance;
        }

        public ScriptEncodingKind Detect(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return ScriptEncodingKind.Unknown;
            }

            try
            {
                using var stream = _fileSystem.OpenRead(path);
                return Detect(stream);
            }
            catch (IOException)
            {
                return ScriptEncodingKind.Unknown;
            }
            catch (UnauthorizedAccessException)
            {
                return ScriptEncodingKind.Unknown;
            }
        }

        public ScriptEncodingKind Detect(Stream stream)
        {
            // 注意：net48 无内置 Span<T>，这里用数组（wiki 07 §4.1）
            var head = new byte[4];
            int read = stream.Read(head, 0, head.Length);

            if (read == 0)
            {
                return ScriptEncodingKind.Empty;
            }

            if (read >= 3 && head[0] == 0xEF && head[1] == 0xBB && head[2] == 0xBF)
            {
                return ScriptEncodingKind.Utf8Bom;
            }

            if (read >= 2 && head[0] == 0xFF && head[1] == 0xFE)
            {
                // FF FE 00 00 是 UTF-32LE，极少见，交给兜底
                if (read >= 4 && head[2] == 0x00 && head[3] == 0x00)
                {
                    return ScriptEncodingKind.Unknown;
                }

                return ScriptEncodingKind.Utf16Le;
            }

            if (read >= 2 && head[0] == 0xFE && head[1] == 0xFF)
            {
                return ScriptEncodingKind.Utf16Be;
            }

            // 无 BOM：整文件严格 UTF-8 校验
            try
            {
                stream.Position = 0;
                using var reader = new StreamReader(
                    stream,
                    new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
                    detectEncodingFromByteOrderMarks: false,
                    bufferSize: 4096,
                    leaveOpen: true);

                var buffer = new char[4096];
                long total = 0;
                int n;
                while ((n = reader.Read(buffer, 0, buffer.Length)) > 0)
                {
                    total += n;
                    if (total > MaxFullScanBytes)
                    {
                        break;
                    }
                }

                return ScriptEncodingKind.Utf8NoBom;
            }
            catch (System.Text.DecoderFallbackException)
            {
                return ScriptEncodingKind.Ansi;
            }
            catch (IOException)
            {
                return ScriptEncodingKind.Unknown;
            }
        }
    }
}
