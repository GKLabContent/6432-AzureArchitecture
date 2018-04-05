using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.ServiceBus.Messaging;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using System.IO;
using Microsoft.Azure.Batch.Conventions.Files;
using Microsoft.Azure.Batch;
using Microsoft.Azure.Batch.Auth;
using System.Threading;

namespace videoProcessor
{
    class Program
    {
        static void Main(string[] args)
        {
            /*
             Variables set by environment variables.  If the environment variable does not exist an alternate value is defined under 
             videoProcessor/Properties/Settings.settings.  Double click that item in Solution Explorer if you want to put in persistent values.
             */
            var sbConnection = getEnvironmentVariable("SB_CONNECT", Properties.Settings.Default.SB_CONNECT);
            var videoSAS = getEnvironmentVariable("VIDEO_STORAGE_SAS", Properties.Settings.Default.VIDEO_STORAGE_SAS);
            var videoAccountName = getEnvironmentVariable("VIDEO_STORAGE_ACCOUNT", Properties.Settings.Default.VIDEO_STORAGE_ACCOUNT);
            var batchStorageAccount = getEnvironmentVariable("BATCH_STORAGE_ACCOUNT", Properties.Settings.Default.BATCH_STORAGE_ACCOUNT);
            var batchStorageKey = getEnvironmentVariable("BATCH_STORAGE_KEY", Properties.Settings.Default.BATCH_STORAGE_KEY);
            var batchURI = getEnvironmentVariable("BATCH_URI", Properties.Settings.Default.BATCH_URI);
            var batchName = getEnvironmentVariable("BATCH_ACCOUNT_NAME", Properties.Settings.Default.BATCH_ACCOUNT_NAME);
            var batchKey = getEnvironmentVariable("BATCH_KEY", Properties.Settings.Default.BATCH_KEY);

            var development = getEnvironmentBoolean("DEVELOPMENT", Properties.Settings.Default.DEVELOPMENT);

            //This data does not come from user defined environment variables.
            var taskOutputFolder = getEnvironmentVariable("AZ_BATCH_TASK_DIR", "d:\\dump");
            var jobId = getEnvironmentVariable("AZ_BATCH_JOB_ID", "DEMOJOB");
            var taskId = getEnvironmentVariable("AZ_BATCH_TASK_ID", "DEMOTASK");
            var taskOutputFile = taskOutputFolder + @"\stdout.log";
            var errOutputFile = taskOutputFolder + @"\stderr.log";

            if (development)
            {
                if (!Directory.Exists(taskOutputFolder))
                {
                    Directory.CreateDirectory(taskOutputFolder);
                }
            }

            //Redirect console output to the stdOut and stddError files
            using (var taskOutput = new StreamWriter(taskOutputFile))
            {
                var output = Console.Out;
                if (!development)
                {
                    Console.SetOut(taskOutput);
                }
                using (var taskErrors = new StreamWriter(errOutputFile))
                {
                    var errorOutput = Console.Error;
                    if (!development)
                    {
                        Console.SetError(taskErrors);
                    }
                    //wrap the rest of the operation in try/catch and log errors to batch log output
                    try
                    {
                        //Get Service bus queue.  Raise an error if the queue does not exist.  
                        var queueName = "videoprocess";
                        var sbEnv = Microsoft.ServiceBus.NamespaceManager.CreateFromConnectionString(sbConnection);
                        if (!sbEnv.QueueExists(queueName))
                        {
                            throw new ArgumentException("The \"videoprocess\" queue does not exist.");
                        }
                        var sbClient = QueueClient.CreateFromConnectionString(sbConnection, queueName);

                        //Set up the output container in the same storage account as the video
                        var resultsContainerName = "results";
                        sbClient.PrefetchCount = 5;
                        var videoCred = new StorageCredentials(videoSAS);
                        string endpoint = $"https://{videoAccountName}.blob.core.windows.net/";
                        CloudBlobClient blobClient = new CloudBlobClient(new Uri(endpoint), videoCred);
                        var container = blobClient.GetContainerReference(resultsContainerName);
                        container.CreateIfNotExists();


                        //Process the messages in the videoprocess queue.  Create a dummy file in the output container for each message.
                        var emptyCount = 0;
                        var messageWait = new TimeSpan(0, 0, 2);
                        var message = sbClient.Receive(messageWait);
                        var cleanRun = true;
                        //This processes two empty receives.  This should get around any timing issues.
                        while ((message != null) || (emptyCount < 2))
                        {
                            if (message != null)
                            {
                                try
                                {
                                    var fileName = message.GetBody<string>();
                                    Console.WriteLine($"Message: {fileName}");
                                    var blob = container.GetBlockBlobReference(fileName);
                                    blob.UploadText("File successfully processed");
                                    message.Complete();
                                }
                                catch (Exception exPerMessage)
                                {
                                    Console.Error.WriteLine($"Error processing message {message.MessageId}: {exPerMessage.Message}");
                                    cleanRun = false;
                                }
                            }
                            else
                            {
                                emptyCount++;
                            }
                            message = sbClient.Receive(messageWait);

                        }
                        if (cleanRun)
                        {
                            Console.WriteLine("Successfully processed all messages");
                        }
                        else
                        {
                            Console.WriteLine("There were errors writing the files");
                        }

                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"An error occurred processing the messages: {ex.Message}");
                    }
                    if (!development)
                    {
                        Console.SetError(errorOutput);
                    }
                    taskErrors.Flush();
                    taskErrors.Close();
                }
                if (!development)
                {
                    Console.SetOut(output);
                }
                taskOutput.Flush();
                taskOutput.Close();
            }

            //Try to write the output to the batch output using the library.  If that doesn't work (testing) then write to the console
            if (!development)
            {
                try
                {
                    var linkedStorageAccount = new CloudStorageAccount(new StorageCredentials(batchStorageAccount, batchStorageKey), true);

                    var taskOutputStorage = new TaskOutputStorage(linkedStorageAccount, jobId, taskId);
                    //This needs a task because TaskOutputStorage is completely async
                    var t = Task.Run(async () =>
                    {


                        //Set up output storage
                        BatchSharedKeyCredentials cred = new BatchSharedKeyCredentials(batchURI,
                                                                       batchName,
                                                                       batchKey);

                        using (BatchClient batchClient = BatchClient.Open(cred))
                        {
                            // Create the blob storage container for the outputs.
                            await batchClient.JobOperations.GetJob(jobId).PrepareOutputStorageAsync(linkedStorageAccount);
                        }
                        await taskOutputStorage.SaveAsync(TaskOutputKind.TaskLog, taskOutputFile, Path.GetFileName(taskOutputFile));
                        await taskOutputStorage.SaveAsync(TaskOutputKind.TaskLog, errOutputFile, Path.GetFileName(errOutputFile));
                    });
                    t.Wait();

                }
                catch (AggregateException ae)
                {
                    var errorText = new StringBuilder();
                    foreach (Exception ex in ae.InnerExceptions)
                    {
                        errorText.AppendLine(ex.Message);
                    }
                    Console.Error.Write(errorText.ToString());
                    Console.WriteLine("Could not write output to batch storage. Check the standard error output for details.  Processing information: \n {0}", File.ReadAllText(taskOutputFile));
                    
                }
            }
            else
            {
                Console.WriteLine("Done. Press any key to continue");
                Console.ReadLine();
            }


        }

        private static string getEnvironmentVariable(string envVariable, string defaultValue)
        {
            return Environment.GetEnvironmentVariable(envVariable) != null ? Environment.GetEnvironmentVariable(envVariable) : defaultValue;
        }
        private static bool getEnvironmentBoolean(string envVariable, bool defaultValue)
        {
            bool value = defaultValue;
            if (Environment.GetEnvironmentVariable(envVariable) != null)
            {
                bool.TryParse(Environment.GetEnvironmentVariable(envVariable), out value);
            }
            return value;
        }
    }
}
