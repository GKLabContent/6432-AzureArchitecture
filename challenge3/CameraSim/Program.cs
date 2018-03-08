using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Auth;
using System.IO;

namespace CameraSim
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                string accountName = Properties.Settings.Default.VIDEO_STORAGE_ACCOUNT; //Environment.GetEnvironmentVariable("STORAGE_ACCOUNT");
                string containerName = "video";
                string sas = Properties.Settings.Default.VIDEO_SAS; //Environment.GetEnvironmentVariable("VIDEO_SAS");
                string endpoint = $"https://{accountName}.blob.core.windows.net/";
                var cred = new StorageCredentials(sas);
                CloudBlobClient client = new CloudBlobClient(new Uri(endpoint), cred);
                var container = client.GetContainerReference(containerName);
                container.CreateIfNotExists();
                for (int lcv = 0; lcv < 3; lcv++)
                {
                    string fileName = Path.GetTempFileName();
                    File.WriteAllText(fileName, "Sample file");
                    var blob = container.GetBlockBlobReference(Path.GetFileName(fileName));
                    blob.UploadFromFile(fileName);

                }
                Console.WriteLine($"You have successfully written 3 files to {endpoint}");
            } catch(Exception ex)
            {
                Console.WriteLine($"The camera simulation ran into an error: {ex.Message}");
            }
            Console.WriteLine("Press enter to close this window;");
            Console.ReadLine();

        }
    }
}
