# IBM MQ Deadlock issue demo
This project demonstrates how deadlock can occur due to MQ library reconnect feature issue. Keep in mind, that repro steps are not always consistent, so you might want to run it couple of times before you'll be able to successfully reproduce it.

Repro steps:
* Start MQ server
* Open a connection to MQ (create new MQQueueManager object)
* Stop MQ server
* Wait for MQReconnectTimeout interval
* Execute any MQ request (it should fail)
* At this point MQQueueManager.IsConnected should return true.
* Switch the thread (either via await Task.Yield() or just run continuation in separate thread)
* execute any MQ request

My understanding this is caused by combination of multiple factors. Reconnect logic didn't properly dispose of used resources. As a result, subsequent call failed (in this demo it consistently fails with NullReferenceException) and didn't properly exit obtained lock. Thus once thread is switched it enters into a deadlock state.

## How to run
* Install [Podman](https://podman.io/getting-started/installation)
* Execute build.ps1 script to build and run MQ server.
* Run the project in Debug mode (this is important for output purposes)