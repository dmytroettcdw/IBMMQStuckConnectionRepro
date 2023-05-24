# IBM MQ Deadlock issue demo
This project demonstrates how deadlock can occur due to MQ library reconnect feature issue. Keep in mind, that repro steps are not always consistent, so you might want to run it couple of times before you'll be able to successfully reproduce it.

Repro steps:
1. Start MQ server
2. Open a connection to MQ (create new MQQueueManager object)
3. Stop MQ server
4. Wait for MQReconnectTimeout interval
5. Execute any MQ request
6. **Expected** result:
   * request fails with `MQException` with `MQRC_CONNECTION_BROKEN` reason code.
   * Subsequent calls to this queue manager object fail with `MQException` with `MQRC_CONNECTION_BROKEN` reason code.
   * `MQQueueManager.IsConnected == false`
7. **Actual** result:
   * request fails with NullReferenceException
   * `MQQueueManager.IsConnected == true`, while according to docs it should be updated to `false`
   * Subsequent calls to this queue manager object from the same thread as per will result in `NullReferenceException`
   * Subsequent calls to this queue manager object from different thread will result in deadlock.

My understanding this is caused by combination of multiple factors:
* Reconnect logic didn't properly dispose of used resources. 
* MQPUT is missing `finally` block to exit an obtained lock.

As a result, when unexpected exception happens (NullReferenceException in this demo) inner lock is never exited which results in a deadlock state once thread is switched.

## How to run
* Install [Podman](https://podman.io/getting-started/installation)
* Execute mqserver/build.ps1 script to build and run MQ server.