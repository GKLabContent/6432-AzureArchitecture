#r "Microsoft.ServiceBus"
#r "Microsoft.WindowsAzure.Storage"
using Microsoft.ServiceBus.Messaging;
using Microsoft.WindowsAzure.Storage.Blob;
using System.IO;
public static void Run(CloudBlockBlob myBlob, TraceWriter log, out BrokeredMessage outputSbMsg)
{
    log.Info($"C# Blob trigger function Processed blob\n {myBlob.Name}");
    
    outputSbMsg = new BrokeredMessage(myBlob.Snapshot​Qualified​Uri.AbsolutePath);
    

}
