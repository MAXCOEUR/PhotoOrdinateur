using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace PhotoOrdinateur
{
    public class PhotoServer
    {
        private readonly int port;
        private readonly string baseFolder;
        private readonly Action<BitmapImage> qrCodeCallback;

        public PhotoServer(int port, string baseFolder, Action<BitmapImage> qrCodeCallback)
        {
            this.port = port;
            this.baseFolder = baseFolder;
            this.qrCodeCallback = qrCodeCallback;
        }

        public async void Start()
        {
            try
            {
                string ip = GetLocalIPAddress();
                string url = $"http://{ip}:{port}/upload/";

                string qrContent = System.Text.Json.JsonSerializer.Serialize(new { ip, port });
                var image = QrCodeService.Generate(qrContent);
                qrCodeCallback?.Invoke(image);

                await RunHttpListener(url);
            }
            catch (Exception ex)
            {
                LogErreur.print("Erreur sur le serveur", ex);
            }
        }

        private async Task RunHttpListener(string prefix)
        {
            HttpListener listener = new HttpListener();
            listener.Prefixes.Add(prefix);
            listener.Start();
            Console.WriteLine($"Listening on {prefix}");

            while (true)
            {
                var context = await listener.GetContextAsync();
                if (context.Request.HttpMethod == "POST")
                {
                    await HandleUpload(context);
                }
                else if (context.Request.HttpMethod == "GET")
                {
                    context.Response.StatusCode = 200; // Method Not Allowed
                    context.Response.Close();
                }
                else
                {
                    context.Response.StatusCode = 405; // Method Not Allowed
                    context.Response.Close();
                }
            }
        }

        private async Task HandleUpload(HttpListenerContext context)
        {
            var contentType = context.Request.ContentType ?? "";

            string deviceName;
            DateTime fileDate;
            string fileName;
            byte[] fileContents;

            var query = context.Request.QueryString;
            deviceName = string.IsNullOrWhiteSpace(query["DeviceName"]) ? "UnknownDevice" : query["DeviceName"];

            if (!DateTime.TryParse(query["DateTime"], out fileDate))
            {
                context.Response.StatusCode = 400;
                await context.Response.OutputStream.WriteAsync(Encoding.UTF8.GetBytes("Invalid or missing DateTime"));
                context.Response.Close();
                return;
            }

            if (contentType.StartsWith("multipart/form-data"))
            {
                // MULTIPART
                var boundary = contentType.Split("boundary=")[1];
                var parser = new MultipartParser(context.Request.InputStream, boundary);

                if (parser.FileContents == null || parser.FileName == null)
                {
                    context.Response.StatusCode = 400;
                    await context.Response.OutputStream.WriteAsync(Encoding.UTF8.GetBytes("Invalid multipart data"));
                    context.Response.Close();
                    return;
                }

                // Extraire l'extension du fichier original
                string originalExtension = Path.GetExtension(parser.FileName);
                if (string.IsNullOrEmpty(originalExtension))
                    originalExtension = ".jpg"; // fallback si pas d'extension

                // Récupérer le titre original, si possible (par exemple depuis le nom du fichier ou les données envoyées)
                string originalTitle = Path.GetFileNameWithoutExtension(parser.FileName);

                // Si le titre est trop long, on peut le tronquer si nécessaire pour ne pas dépasser la longueur du nom de fichier
                if (originalTitle.Length > 50) // longueur limite arbitraire, ajuster selon les besoins
                    originalTitle = originalTitle.Substring(0, 50);

                // Générer le nom de fichier basé sur la date et ajouter le titre entre parenthèses
                fileName = $"{deviceName}_{fileDate:yyyy-MM-dd_HHmmss} ({originalTitle}){originalExtension}";

                fileContents = parser.FileContents;
            }
            else
            {
                context.Response.StatusCode = 400;
                await context.Response.OutputStream.WriteAsync(Encoding.UTF8.GetBytes("Invalid or missing multipart/form-data"));
                context.Response.Close();
                return;
            }

            // Crée le chemin final
            var folder = Path.Combine(baseFolder, deviceName, fileDate.Year.ToString(), fileDate.Month.ToString("D2"), fileDate.Day.ToString("D2"));
            Directory.CreateDirectory(folder);

            string filePath = Path.Combine(folder, fileName);

            if (!File.Exists(filePath) || !FilesAreEqual(fileContents, filePath))
            {
                await File.WriteAllBytesAsync(filePath, fileContents);
                Console.WriteLine($"Saved {filePath}");
            }
            else
            {
                Console.WriteLine($"Skipped duplicate {filePath}");
            }

            context.Response.StatusCode = 200;
            context.Response.Close();
        }

        private bool FilesAreEqual(byte[] fileData, string path)
        {
            using var md5 = MD5.Create();
            byte[] existingHash;
            using (var fs = File.OpenRead(path))
                existingHash = md5.ComputeHash(fs);
            var incomingHash = md5.ComputeHash(fileData);
            return StructuralComparisons.StructuralEqualityComparer.Equals(existingHash, incomingHash);
        }

        private string GetLocalIPAddress()
        {
            foreach (var ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
            {
                if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    return ip.ToString();
            }
            return "127.0.0.1";
        }
    }

}
