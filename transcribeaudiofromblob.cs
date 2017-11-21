#r "Microsoft.WindowsAzure.Storage"
#r "Newtonsoft.Json"
using Newtonsoft.Json;
using System.Net;
using Microsoft.CognitiveServices.SpeechRecognition;
using System.Text;


//aca voy a serializar el JSON que voy a retornas
public class Transcription
{
    public string title;
    public string transcription;
}

//ahora recibo un string no un Report o como sea
public static async Task<HttpResponseMessage> Run(HttpRequestMessage req, TraceWriter log)
{
    // parsear la query
    string blob = req.GetQueryNameValuePairs()
        .FirstOrDefault(q => string.Compare(q.Key, "blob", true) == 0)
        .Value;
    // obtener el body
    dynamic data = await req.Content.ReadAsAsync<object>();
    //guardar el nombre del blob en una variable
    blob = blob ?? data?.blob;
    //si es que es null retorno error, si no proceso
    if(blob==null) {
        return new HttpResponseMessage(HttpStatusCode.BadRequest);
    }
    else{
        log.Info("Intentaremos procesar el blob: "+blob);
        var storageAccount = Microsoft.WindowsAzure.Storage.CloudStorageAccount.Parse(AzureStorageAccount.ConnectionString);
        var blobClient = storageAccount.CreateCloudBlobClient();
        var container = blobClient.GetContainerReference(AzureStorageAccount.ContainerName);
        var audioHandler = new AudioHandler();
        Transcription trans = new Transcription();
        trans.transcription = await audioHandler.ProcessBlob(container, blob, log);   
        log.Info($"[{blob}] transcribed: {trans.transcription}");
        trans.title = blob;
        string output = JsonConvert.SerializeObject(trans);
        return req.CreateResponse(HttpStatusCode.OK, output);
    }
}
//clase con las variables de conexion a la cuenta de storage
public abstract class AzureStorageAccount
{
    public static string ConnectionString = "DefaultEndpointsProtocol=https;AccountName=accentureaudio;AccountKey=i6aOeII3RWz8AEEB7qJ0K25JwokSLoCbRvheEskdj8u23IPcL9cFgHefhZ3jh632kNvKzJhp0jUtXw1eZBHY4w==";
    public static string ContainerName = "audiocontainer";
}


public class AudioHandler {

TaskCompletionSource<string> _tcs;
static DataRecognitionClient _dataClient;

static AudioHandler()
{
     _dataClient = SpeechRecognitionServiceFactory.CreateDataClient(
                SpeechRecognitionMode.LongDictation,
                "es-MX",
                "2b7a0e7605b748a597de8c9d7fb9080d");
}

public AudioHandler()
{
  _dataClient.OnResponseReceived += responseHandler;
}

private void responseHandler(object sender, SpeechResponseEventArgs args){

      
        if (args.PhraseResponse.Results.Length == 0)
            _tcs.SetResult("ERROR: Bad audio");
        else
            _tcs.SetResult(args.PhraseResponse.Results[0].DisplayText);
        var client = sender as DataRecognitionClient;
        client.OnResponseReceived -= responseHandler;

}

public   Task<string>  ProcessBlob(Microsoft.WindowsAzure.Storage.Blob.CloudBlobContainer container, string blobName, TraceWriter log)
{
     _tcs = new TaskCompletionSource<string>();
    var mem = new System.IO.MemoryStream();
    log.Info("Ready to read blob");
    var blockBlob = container.GetBlockBlobReference(blobName);
    blockBlob.DownloadToStream(mem);
    log.Info("Blob read - size=" + mem.Length);
    mem.Position = 0;

    int bytesRead = 0;
    byte[] buffer = new byte[1024];

    try
    {
        do
        {           
            bytesRead = mem.Read(buffer, 0, buffer.Length);
            
            _dataClient.SendAudio(buffer, bytesRead);
        }
        while (bytesRead > 0);
         log.Info("Done Reading bytes");
      
    }
    finally
    {
       
        _dataClient.EndAudio();
         log.Info("Finished");
         
    }
     log.Info("Returning");
    return _tcs.Task;
}
}
