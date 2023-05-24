using System.Collections;
using CliWrap;
using IBM.WMQ;

if (!File.Exists("mqclient.ini")) throw new InvalidOperationException("mqclient.ini file not found in exe directory");

Console.WriteLine("Starting MQ");

StartMq();

Console.WriteLine("Connecting to MQ");

ConnectAndOpenQueue(out var queueManager, out var queue);

Console.WriteLine("Stopping MQ");

StopMq();

// this has to be longer than MQReconnectTimeout
Thread.Sleep(TimeSpan.FromSeconds(11));


Console.WriteLine("Sending message to MQ");

try
{
    PutMessage(ref queueManager, ref queue);
}
catch (NullReferenceException)
{
    Console.WriteLine("NullReferenceException happened instead of MQException with MQRC_CONNECTION_BROKEN reason code");
    if (queueManager.IsConnected)
    {
        Console.WriteLine("MQQueueManager.IsConnected wasn't updated as per docs.");
        Console.WriteLine("Subsequent calls from different threads it will result in deadlock.");
    }
}

static void ConnectAndOpenQueue(out MQQueueManager queueManager, out MQQueue queue)
{
    const string QUEUE_MANAGER_NAME = "OM_QMGR";

    Hashtable properties = new()
    {
        { MQC.TRANSPORT_PROPERTY, MQC.TRANSPORT_MQSERIES_MANAGED },
        { MQC.CONNECT_OPTIONS_PROPERTY, MQC.MQCNO_RECONNECT + MQC.MQCNO_HANDLE_SHARE_BLOCK },
        { MQC.HOST_NAME_PROPERTY, "localhost" },
        { MQC.PORT_PROPERTY, 11415 },
        { MQC.CHANNEL_PROPERTY, "DEV.APP.SVRCONN" }
    };

    queueManager = new MQQueueManager(QUEUE_MANAGER_NAME, properties);
    queue = queueManager.AccessQueue("CDW.OMS.CREATEORDER.INBOUND.Q", MQC.MQOO_INPUT_SHARED + MQC.MQOO_OUTPUT);
}

static void PutMessage(ref MQQueueManager queueManager, ref MQQueue queue, int retryCount = 1)
{
    const string TEST_MESSAGE = "Hello World";

    try
    {
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
        queue.Put(mqMessage, putOpts);
    }
    catch (MQException ex) when (ex.Reason is MQC.MQRC_CONNECTION_BROKEN && retryCount-- > 0)
    {
        ConnectAndOpenQueue(out queueManager, out queue);
        PutMessage(ref queueManager, ref queue, retryCount);
    }
}

void StartMq()
{
    Cli.Wrap("podman")
        .WithArguments("start QM_OMS")
        .ExecuteAsync()
        .Task.Wait();

    Thread.Sleep(TimeSpan.FromSeconds(3));
}

void StopMq()
{
    Cli.Wrap("podman")
        .WithArguments("stop QM_OMS")
        .ExecuteAsync()
        .Task.Wait();
}