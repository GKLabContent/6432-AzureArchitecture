using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Driver;
using MongoDB.Bson;
using System.Security.Authentication;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;

namespace cosmosDBLoad
{
    class Program
    {
        static void Main(string[] args)
        {
            //MongoDB_Load();
            var t = Task.Run(new Action(() =>
            {
                DocumentDB_Load();
            }));
            t.Wait();
            Console.WriteLine("Hopefully, you have some data.\nPress any key to close.");
            Console.Read();
        }

        private static async void DocumentDB_Load()
        {
            string serverName = "tsw6342c44tzojpypwqlsm";
            string cosmosDBKey = "SJbpitSnIw0pFQWm5TqpAVk8Cceu9tdWtz2ENgSnXdfkJAKLHZoYgQ1H3IP86ptkMTu1TYPPfeyGiPY8xuS44A==";
           string DatabaseName = "products";
            string CollectionName = "productlist";

            string endpointUrl = $"https://{serverName}.documents.azure.com";
 
            ConnectionPolicy connectionPolicy = new ConnectionPolicy();
            DocumentClient client = new DocumentClient(new Uri(endpointUrl), cosmosDBKey, connectionPolicy);
            client.CreateDatabaseIfNotExistsAsync(new Database { Id = DatabaseName }).Wait();
            DocumentCollection simpleCollection = await client.CreateDocumentCollectionIfNotExistsAsync(
     UriFactory.CreateDatabaseUri(DatabaseName),
     new DocumentCollection { Id = CollectionName },
     new RequestOptions { OfferThroughput = 400 });

            var collectionURI = UriFactory.CreateDocumentCollectionUri(DatabaseName, CollectionName);
            await client.CreateDocumentAsync(collectionURI, new Product { id = "1", name = "Edsel" });
            await client.CreateDocumentAsync(collectionURI, new Product { id = "2", name = "Yugo" });
            await client.CreateDocumentAsync(collectionURI, new Product { id = "3", name = "Eggs" });




        }

        private static void MongoDB_Load()
        {
            string connectionString =
  @"mongodb://tsw6342c44tzojpypwqlsm:8cKL1LHtMGzWiitBm7Uv4oGsVDX1Ro7leezr4x6twJzkz8p2V6NWBPTpnKhJEbeTe9t1ZbR2cCvvZoG0Gj2mMQ==@tsw6342c44tzojpypwqlsm.documents.azure.com:10255/?ssl=true&replicaSet=globaldb";
            string dbName = "Tasks";
            string collectionName = "TasksList";

            MongoClientSettings settings = MongoClientSettings.FromUrl(
                  new MongoUrl(connectionString)
                );
            settings.SslSettings =
              new SslSettings() { EnabledSslProtocols = SslProtocols.Tls12 };
            var client = new MongoClient(settings);
            var database = client.GetDatabase(dbName);
            var collection = database.GetCollection<Product>(collectionName);
            collection.InsertOne(new Product { id = "1", name = "Firetruck" });
            collection.InsertOne(new Product { id = "2", name = "School bus" });
            collection.InsertOne(new Product { id = "3", name = "eggs" });

            Console.WriteLine("Done.  Press enter asshole");
            Console.Read();
        }
    }

    class ProcessMongo
    {
        private string userName = "FILLME";
        private string host = "FILLME";
        private string password = "FILLME";

        // This sample uses a database named "Tasks" and a 
        //collection named "TasksList".  The database and collection 
        //will be automatically created if they don't already exist.
        private string dbName = "Tasks";
        private string collectionName = "TasksList";
        private IMongoCollection<Product> GetTasksCollection()
        {
            string connectionString =
              @"mongodb://tsw6342c44tzojpypwqlsm:8cKL1LHtMGzWiitBm7Uv4oGsVDX1Ro7leezr4x6twJzkz8p2V6NWBPTpnKhJEbeTe9t1ZbR2cCvvZoG0Gj2mMQ==@tsw6342c44tzojpypwqlsm.documents.azure.com:10255/?ssl=true&replicaSet=globaldb";
            MongoClientSettings settings = MongoClientSettings.FromUrl(
              new MongoUrl(connectionString)
            );
            settings.SslSettings =
              new SslSettings() { EnabledSslProtocols = SslProtocols.Tls12 };
            var client = new MongoClient(settings);
            var database = client.GetDatabase(dbName);
            var todoTaskCollection = database.GetCollection<Product>(collectionName);
            return todoTaskCollection;

        }
    }
}
