using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

public class MultipartParser
{
    public string? FileName { get; private set; }
    public byte[]? FileContents { get; private set; }

    public MultipartParser(Stream stream, string boundary)
    {
        var boundaryBytes = Encoding.UTF8.GetBytes("--" + boundary);
        var endBoundaryBytes = Encoding.UTF8.GetBytes("--" + boundary + "--");

        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        byte[] data = ms.ToArray();

        int start = IndexOf(data, boundaryBytes, 0);
        int end = IndexOf(data, endBoundaryBytes, start + boundaryBytes.Length);

        if (start < 0 || end < 0) return;

        // Recherche le bloc de l'en-tête
        int headersEnd = IndexOf(data, Encoding.UTF8.GetBytes("\r\n\r\n"), start) + 4;
        if (headersEnd < 4) return;

        string headers = Encoding.UTF8.GetString(data, start, headersEnd - start);
        var fileNameMatch = Regex.Match(headers, @"filename=""([^""]+)""");

        if (!fileNameMatch.Success) return;

        FileName = fileNameMatch.Groups[1].Value;

        int contentStart = headersEnd;
        int contentEnd = end - 2; // en général, il y a \r\n avant le boundary final

        if (contentEnd <= contentStart) return;

        int length = contentEnd - contentStart;
        FileContents = new byte[length];
        Array.Copy(data, contentStart, FileContents, 0, length);
    }

    private static int IndexOf(byte[] buffer, byte[] pattern, int startIndex)
    {
        for (int i = startIndex; i <= buffer.Length - pattern.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < pattern.Length; j++)
            {
                if (buffer[i + j] != pattern[j])
                {
                    match = false;
                    break;
                }
            }
            if (match) return i;
        }
        return -1;
    }
}


