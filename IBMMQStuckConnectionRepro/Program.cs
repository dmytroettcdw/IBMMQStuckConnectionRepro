using System.Collections;
using CliWrap;
using IBM.WMQ;

if (!File.Exists("mqclient.ini"))
{
    throw new InvalidOperationException("mqclient.ini file not found in exe directory");
}

const string QUEUE_MANAGER_NAME = "OM_QMGR";
const string TEST_MESSAGE = "Hello World";

Hashtable properties = new()
{
    { MQC.TRANSPORT_PROPERTY, MQC.TRANSPORT_MQSERIES_MANAGED },
    /*
     * It seems the issues is specifically with MQCNO_RECONNECT option
     */
    { MQC.CONNECT_OPTIONS_PROPERTY, MQC.MQCNO_RECONNECT + MQC.MQCNO_HANDLE_SHARE_BLOCK },
    { MQC.HOST_NAME_PROPERTY, "localhost" },
    { MQC.PORT_PROPERTY, 11415 },
    { MQC.CHANNEL_PROPERTY, "DEV.APP.SVRCONN" }
};

Console.WriteLine("Starting MQ");

await Cli.Wrap("podman")
    .WithArguments("start QM_OMS")
    .ExecuteAsync();
    
await Task.Delay(TimeSpan.FromSeconds(3));

Console.WriteLine("Connecting to MQ");

using var queueManager = new MQQueueManager(QUEUE_MANAGER_NAME, properties);
using var queue = queueManager.AccessQueue("CDW.OMS.CREATEORDER.INBOUND.Q", MQC.MQOO_INPUT_SHARED + MQC.MQOO_OUTPUT);

Console.WriteLine("Stopping MQ");

await Cli.Wrap("podman")
    .WithArguments("stop QM_OMS")
    .ExecuteAsync();
    
// this has to be longer than MQReconnectTimeout
await Task.Delay(TimeSpan.FromSeconds(11));

MQPutMessageOptions putOpts = new()
{
    Options = MQC.MQPMO_NO_SYNCPOINT + MQC.MQPMO_SYNC_RESPONSE
};

MQMessage mqMessage = new()
{
    Format = MQC.MQFMT_STRING,
    Persistence = MQC.MQPER_PERSISTENT
};

mqMessage.WriteString(TEST_MESSAGE);

Console.WriteLine("Sending message to MQ");

try
{
    queue.Put(mqMessage, putOpts);
}
catch (Exception ex)
{
    Console.WriteLine(ex);
}

if (!queueManager.IsConnected)
{
    throw new InvalidOperationException("MQ object goes into stuck state when IsConnected is true. Run again to try to reproduce the issue");
}

Console.WriteLine("MQQueueManager.IsConnected says we're still connected to MQ (in reality we're not). Sending message from different thread");

try
{
    queue.Put(mqMessage, putOpts);
}
catch (Exception ex)
{
    Console.WriteLine(ex);
}

// if we're here, we almost certainly were able to successfully reproduce the issue
// switch the thread. TaskCreationOptions.LongRunning ensures we're running on new separate thread not shared with thread pool
// `new Thread().Start()` should also work here.

await Task.Factory.StartNew(() => SendMessage(queue, mqMessage, putOpts), TaskCreationOptions.LongRunning);

static void SendMessage(MQQueue mqQueue, MQMessage mqMessage, MQPutMessageOptions mqPutMessageOptions)
{
    Console.WriteLine("We're about to get stuck");

    try
    {
        mqQueue.Put(mqMessage, mqPutMessageOptions);
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex);
    }
    finally
    {
        Console.WriteLine("Nope, we're not stuck.");
    }
}



