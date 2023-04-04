FROM icr.io/ibm-messaging/mq:9.3.2.0-r1
ENV LICENSE=accept
ENV MQ_QMGR_NAME=OM_QMGR
USER 1001
COPY 20-config.mqsc /etc/mqm/