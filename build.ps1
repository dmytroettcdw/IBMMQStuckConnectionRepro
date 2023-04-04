podman build -t docker.io/oms-mq-image .
podman run --publish 11415:1414 --publish 9444:9443 --detach --name QM_OMS docker.io/oms-mq-image