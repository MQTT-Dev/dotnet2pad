using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using uPLibrary.Networking.M2Mqtt.Exceptions;
using uPLibrary.Networking.M2Mqtt.Messages;
// if .Net Micro Framework
#if (MF_FRAMEWORK_VERSION_V4_2 || MF_FRAMEWORK_VERSION_V4_3)
using Microsoft.SPOT;
#if SSL
using Microsoft.SPOT.Net.Security;
#endif
// else other frameworks (.Net, .Net Compact, Mono, Windows Phone) 
#else
using System.Collections.Generic;
#if (SSL && !WINDOWS_PHONE)
using System.Security.Authentication;
using System.Net.Security;
#endif
#endif

using System.Security.Cryptography.X509Certificates;
using System.Collections;

namespace uPLibrary.Networking.M2Mqtt
{
    /// <summary>
    /// MQTT Client
    /// </summary>
    public class MqttClient
    {
        /// <summary>
        /// Delagate that defines event handler for PUBLISH message received from broker
        /// </summary>
        public delegate void MqttMsgPublishEventHandler(object sender, MqttMsgPublishEventArgs e);

        /// <summary>
        /// Delagate that defines event handler for published message
        /// </summary>
        public delegate void MqttMsgPublishedEventHandler(object sender, MqttMsgPublishedEventArgs e);

        /// <summary>
        /// Delagate that defines event handler for subscribed topic
        /// </summary>
        public delegate void MqttMsgSubscribedEventHandler(object sender, MqttMsgSubscribedEventArgs e);

        /// <summary>
        /// Delagate that defines event handler for unsubscribed topic
        /// </summary>
        public delegate void MqttMsgUnsubscribedEventHandler(object sender, MqttMsgUnsubscribedEventArgs e);

        // CA certificate
        private X509Certificate caCert;

        // broker hostname, ip address and port
        private string brokerHostName;
        private IPAddress brokerIpAddress;
        private int brokerPort;
        // using SSL
        private bool secure;

        // thread for receiving incoming message from broker
        private Thread receiveThread;
        // thread for raising received message from broker event
        private Thread receiveEventThread;
        private bool isRunning;
        // event for raising received message from broker event
        private AutoResetEvent receiveEventWaitHandle;

        // thread for handling inflight messages queue asynchronously
        private Thread processInflightThread;
        // event for starting process inflight queue asynchronously
        private AutoResetEvent inflightWaitHandle;

        // event for signaling synchronous receive end from broker
        AutoResetEvent syncEndReceiving;
        // message received from broker
        MqttMsgBase msgReceived;

        // exeption thrown during receiving from broker
        Exception exReceiving;

        // keep alive period (in ms)
        private int keepAlivePeriod;
        // thread for sending keep alive message
        private Thread keepAliveThread;
        private AutoResetEvent keepAliveEvent;
        // keep alive timeout expired
        private bool isKeepAliveTimeout;
        // last communication time in ticks
        private long lastCommTime;

        // event for PUBLISH message received from broker
        public event MqttMsgPublishEventHandler MqttMsgPublishReceived;
        // event for published message
        public event MqttMsgPublishedEventHandler MqttMsgPublished;
        // event for subscribed topic
        public event MqttMsgSubscribedEventHandler MqttMsgSubscribed;
        // event for unsubscribed topic
        public event MqttMsgUnsubscribedEventHandler MqttMsgUnsubscribed;
        
        // channel to communicate over the network
        private IMqttNetworkChannel channel;

        // inflight messages queue
        private Queue inflightQueue;
        // internal queue for received messages about inflight messages
        private Queue internalQueue;
        // receive queue for received messages
        private Queue receiveQueue;

        // reference to avoid access to singleton via property
        private MqttSettings settings;

        // current message identifier generated
        private ushort messageIdCounter = 0;

        /// <summary>
        /// Connection status between client and broker
        /// </summary>
        public bool IsConnected { get; private set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="brokerIpAddress">Broker IP address</param>
        public MqttClient(IPAddress brokerIpAddress) :
            this(brokerIpAddress, MqttSettings.MQTT_BROKER_DEFAULT_PORT, false, null)
        {
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="brokerIpAddress">Broker IP address</param>
        /// <param name="brokerPort">Broker port</param>
        /// <param name="secure">Using secure connection</param>
        /// <param name="caCert">CA certificate for secure connection</param>
        public MqttClient(IPAddress brokerIpAddress, int brokerPort, bool secure, X509Certificate caCert)
        {
            this.Init(null, brokerIpAddress, brokerPort, secure, caCert);
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="brokerHostName">Broker Host Name</param>
        public MqttClient(string brokerHostName) :
            this(brokerHostName, MqttSettings.MQTT_BROKER_DEFAULT_PORT, false, null)
        {
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="brokerHostName">Broker Host Name</param>
        /// <param name="brokerPort">Broker port</param>
        /// <param name="secure">Using secure connection</param>
        /// <param name="caCert">CA certificate for secure connection</param>
        public MqttClient(string brokerHostName, int brokerPort, bool secure, X509Certificate caCert)
        {
            // throw exceptions to the caller
            IPHostEntry hostEntry = Dns.GetHostEntry(brokerHostName);

            if ((hostEntry != null) && (hostEntry.AddressList.Length > 0))
            {
                // check for the first address not null
                // it seems that with .Net Micro Framework, the IPV6 addresses aren't supported and return "null"
                int i = 0;
                while (hostEntry.AddressList[i] == null) i++;
                this.Init(brokerHostName, hostEntry.AddressList[i], brokerPort, secure, caCert);
            }
            else
                throw new ApplicationException("No address found for the broker");
        }

        /// <summary>
        /// MqttClient initialization
        /// </summary>
        /// <param name="brokerHostName">Broker host name</param>
        /// <param name="brokerIpAddress">Broker IP address</param>
        /// <param name="brokerPort">Broker port</param>
        /// <param name="secure">>Using secure connection</param>
        /// <param name="caCert">CA certificate for secure connection</param>
        private void Init(string brokerHostName, IPAddress brokerIpAddress, int brokerPort, bool secure, X509Certificate caCert)
        {
#if SSL
            // check security parameters
            if ((secure) && (caCert == null))
                throw new ArgumentException("Secure requested but CA certificate is null !");
#else
            if (secure)
                throw new ArgumentException("Library compiled without SSL support");
#endif

            this.brokerHostName = brokerHostName;
            // if broker hostname is null, set ip address
            if (this.brokerHostName == null)
                this.brokerHostName = brokerIpAddress.ToString();

            this.brokerIpAddress = brokerIpAddress;
            this.brokerPort = brokerPort;
            this.secure = secure;
            
#if SSL
            // if secure, load CA certificate
            if (this.secure)
            {
                this.caCert = caCert;
            }
#endif

            // reference to MQTT settings
            this.settings = MqttSettings.Instance;
            
            this.syncEndReceiving = new AutoResetEvent(false);
            this.keepAliveEvent = new AutoResetEvent(false);

            // queue for handling inflight messages (publishing and acknowledge)
            this.inflightWaitHandle = new AutoResetEvent(false);
            this.inflightQueue = new Queue();

            // queue for received message
            this.receiveEventWaitHandle = new AutoResetEvent(false);
            this.receiveQueue = new Queue();
            this.internalQueue = new Queue();
        }

        /// <summary>
        /// Connect to broker
        /// </summary>
        /// <param name="clientId">Client identifier</param>
        /// <returns>Return code of CONNACK message from broker</returns>
        public byte Connect(string clientId)
        {
            return this.Connect(clientId, null, null, false, MqttMsgConnect.QOS_LEVEL_AT_LEAST_ONCE, false, null, null, true, MqttMsgConnect.KEEP_ALIVE_PERIOD_DEFAULT);
        }
        
        /// <summary>
        /// Connect to broker
        /// </summary>
        /// <param name="clientId">Client identifier</param>
        /// <param name="username">Username</param>
        /// <param name="password">Password</param>
        /// <param name="willRetain">Will retain flag</param>
        /// <param name="willQosLevel">Will QOS level</param>
        /// <param name="willFlag">Will flag</param>
        /// <param name="willTopic">Will topic</param>
        /// <param name="willMessage">Will message</param>
        /// <param name="cleanSession">Clean sessione flag</param>
        /// <param name="keepAlivePeriod">Keep alive period</param>
        /// <returns>Return code of CONNACK message from broker</returns>
        public byte Connect(string clientId, 
            string username,
            string password,
            bool willRetain,
            byte willQosLevel,
            bool willFlag,
            string willTopic,
            string willMessage,
            bool cleanSession,
            ushort keepAlivePeriod)
        {
            // create CONNECT message
            MqttMsgConnect connect = new MqttMsgConnect(clientId,
                username,
                password,
                willRetain,
                willQosLevel,
                willFlag,
                willTopic,
                willMessage,
                cleanSession,
                keepAlivePeriod);

            try
            {
                // create network channel and connect to broker
#if WINDOWS_PHONE
                this.channel = new WPMqttNetworkChannel(this.brokerHostName, this.brokerIpAddress, this.brokerPort, this.secure, this.caCert);
#else
                this.channel = new MqttNetworkChannel(this.brokerHostName, this.brokerIpAddress, this.brokerPort, this.secure, this.caCert);
#endif
                this.channel.Connect();                
            }
            catch (Exception ex)
            {
                throw new MqttConnectionException("Exception connecting to the broker", ex);
            }

            this.lastCommTime = 0;
            this.isRunning = true;
            // start thread for receiving messages from broker
            this.receiveThread = new Thread(this.ReceiveThread);
            this.receiveThread.Start();
            
            MqttMsgConnack connack = (MqttMsgConnack)this.SendReceive(connect.GetBytes());
            // if connection accepted, start keep alive timer and 
            if (connack.ReturnCode == MqttMsgConnack.CONN_ACCEPTED)
            {
                this.keepAlivePeriod = keepAlivePeriod * 1000; // convert in ms
                
                // start thread for sending keep alive message to the broker
                this.keepAliveThread = new Thread(this.KeepAliveThread);
                this.keepAliveThread.Start();

                // start thread for raising received message event from broker
                this.receiveEventThread = new Thread(this.ReceiveEventThread);
                this.receiveEventThread.Start();

                // start thread for handling inflight messages queue to broker asynchronously (publish and acknowledge)
                this.processInflightThread = new Thread(this.ProcessInflightThread);
                this.processInflightThread.Start();

                this.IsConnected = true;
            }
            return connack.ReturnCode;
        }

        /// <summary>
        /// Disconnect from broker
        /// </summary>
        public void Disconnect()
        {
            MqttMsgDisconnect disconnect = new MqttMsgDisconnect();
            this.Send(disconnect.GetBytes());

            // close client
            this.Close();
        }

        /// <summary>
        /// Close client
        /// </summary>
        private void Close()
        {
            // stop receiving thread and keep alive thread
            this.isRunning = false;

            // wait end receive thread
            //if (this.receiveThread != null)
            //    this.receiveThread.Join();

            // wait end receive event thread
            if (this.receiveEventThread != null)
            {
                this.receiveEventWaitHandle.Set();
                // NOTE : no join because Close() could be called inside ReceiveEventThread
                //        so we have to avoid deadlock
                //this.receiveEventThread.Join();
            }

            // waint end process inflight thread
            if (this.processInflightThread != null)
            {
                this.inflightWaitHandle.Set();
                // NOTE : no join because Close() could be called inside ProcessInflightThread
                //        so we have to avoid deadlock
                //this.processInflightThread.Join();
            }

            // avoid dedalock if keep alive timeout expired
            if (!this.isKeepAliveTimeout)
            {
                // unlock keep alive thread and wait
                this.keepAliveEvent.Set();

                if (this.keepAliveThread != null)
                    this.keepAliveThread.Join();
            }

            // close network channel
            this.channel.Close();

            // keep alive thread will set it gracefully
            if (!this.isKeepAliveTimeout)
                this.IsConnected = false;
        }

        /// <summary>
        /// Execute ping to broker for keep alive
        /// </summary>
        /// <returns>PINGRESP message from broker</returns>
        private MqttMsgPingResp Ping()
        {
            MqttMsgPingReq pingreq = new MqttMsgPingReq();
            try
            {
                // broker must send PINGRESP within timeout equal to keep alive period
                return (MqttMsgPingResp)this.SendReceive(pingreq.GetBytes(), this.keepAlivePeriod);
            }
            catch (Exception)
            {
                this.isKeepAliveTimeout = true;
                // client must close connection
                this.Close();
                return null;
            }
        }

        /// <summary>
        /// Subscribe for message topics
        /// </summary>
        /// <param name="topics">List of topics to subscribe</param>
        /// <param name="qosLevels">QOS levels related to topics</param>
        /// <returns>Message Id related to SUBSCRIBE message</returns>
        public ushort Subscribe(string[] topics, byte[] qosLevels)
        {
            MqttMsgSubscribe subscribe =
                new MqttMsgSubscribe(topics, qosLevels);
            subscribe.MessageId = this.GetMessageId();

            // enqueue subscribe request into the inflight queue
            this.EnqueueInflight(subscribe, MqttMsgFlow.ToPublish);

            return subscribe.MessageId;
        }

        /// <summary>
        /// Unsubscribe for message topics
        /// </summary>
        /// <param name="topics">List of topics to unsubscribe</param>
        /// <returns>Message Id in UNSUBACK message from broker</returns>
        public ushort Unsubscribe(string[] topics)
        {
            MqttMsgUnsubscribe unsubscribe =
                new MqttMsgUnsubscribe(topics);
            unsubscribe.MessageId = this.GetMessageId();

            // enqueue unsubscribe request into the inflight queue
            this.EnqueueInflight(unsubscribe, MqttMsgFlow.ToPublish);

            return unsubscribe.MessageId;
        }

        /// <summary>
        /// Publish a message to the broker (asynchronously)
        /// </summary>
        /// <param name="topic">Message topic</param>
        /// <param name="message">Message data (payload)</param>
        /// <returns>Message Id related to PUBLISH message</returns>
        public ushort Publish(string topic, byte[] message)
        {
            return this.Publish(topic, message, MqttMsgBase.QOS_LEVEL_AT_MOST_ONCE, false);
        }

        /// <summary>
        /// Publish a message to the broker (asynchronously)
        /// </summary>
        /// <param name="topic">Message topic</param>
        /// <param name="message">Message data (payload)</param>
        /// <param name="qosLevel">QoS Level</param>
        /// <param name="retain">Retain flag</param>
        /// <returns>Message Id related to PUBLISH message</returns>
        public ushort Publish(string topic, byte[] message,
            byte qosLevel = MqttMsgBase.QOS_LEVEL_AT_MOST_ONCE,
            bool retain = false)
        {
            MqttMsgPublish publish =
                    new MqttMsgPublish(topic, message, false, qosLevel, retain);
            publish.MessageId = this.GetMessageId();

            // enqueue message to publish into the inflight queue
            this.EnqueueInflight(publish, MqttMsgFlow.ToPublish);           

            return publish.MessageId;
        }
        
        /// <summary>
        /// Wrapper method for raising message received event
        /// </summary>
        /// <param name="msg">Message received</param>
        private void OnMqttMsgReceived(MqttMsgBase msg)
        {
            lock (this.receiveQueue)
            {
                this.receiveQueue.Enqueue(msg);
            }
            
            this.receiveEventWaitHandle.Set();
        }

        /// <summary>
        /// Wrapper method for raising PUBLISH message received event
        /// </summary>
        /// <param name="publish">PUBLISH message received</param>
        private void OnMqttMsgPublishReceived(MqttMsgPublish publish)
        {
            if (this.MqttMsgPublishReceived != null)
            {
                this.MqttMsgPublishReceived(this, 
                    new MqttMsgPublishEventArgs(publish.Topic, publish.Message, publish.QosLevel, publish.Retain));
            }
        }

        /// <summary>
        /// Wrapper method for raising published message event
        /// </summary>
        /// <param name="messageId">Message identifier for published message</param>
        private void OnMqttMsgPublished(ushort messageId)
        {
            if (this.MqttMsgPublished != null)
            {
                this.MqttMsgPublished(this, 
                    new MqttMsgPublishedEventArgs(messageId));
            }
        }

        /// <summary>
        /// Wrapper method for raising subscribed topic event (SUBACK message)
        /// </summary>
        /// <param name="suback">SUBACK message received</param>
        private void OnMqttMsgSubscribed(MqttMsgSuback suback)
        {
            if (this.MqttMsgSubscribed != null)
            {
                this.MqttMsgSubscribed(this, 
                    new MqttMsgSubscribedEventArgs(suback.MessageId, suback.GrantedQoSLevels));
            }
        }

        /// <summary>
        /// Wrapper method for raising unsubscribed topic event
        /// </summary>
        /// <param name="messageId">Message identifier for unsubscribed topic</param>
        private void OnMqttMsgUnsubscribed(ushort messageId)
        {
            if (this.MqttMsgUnsubscribed != null)
            {
                this.MqttMsgUnsubscribed(this, 
                    new MqttMsgUnsubscribedEventArgs(messageId));
            }
        }
        
        /// <summary>
        /// Send a message to the broker
        /// </summary>
        /// <param name="msgBytes">Message bytes</param>
        private void Send(byte[] msgBytes)
        {
            try
            {
                // send message
                this.channel.Send(msgBytes);

                // update last message sent ticks
                this.lastCommTime = Environment.TickCount;
            }
            catch
            {
                throw new MqttCommunicationException();
            }
        }

        /// <summary>
        /// Send a message to the broker and wait answer
        /// </summary>
        /// <param name="msgBytes">Message bytes</param>
        /// <returns>MQTT message response</returns>
        private MqttMsgBase SendReceive(byte[] msgBytes)
        {
            return this.SendReceive(msgBytes, MqttSettings.MQTT_DEFAULT_TIMEOUT);
        }

        /// <summary>
        /// Send a message to the broker and wait answer
        /// </summary>
        /// <param name="msgBytes">Message bytes</param>
        /// <param name="timeout">Timeout for receiving answer</param>
        /// <returns>MQTT message response</returns>
        private MqttMsgBase SendReceive(byte[] msgBytes, int timeout)
        {
            // reset handle before sending
            this.syncEndReceiving.Reset();
            try
            {
                // send message
                this.channel.Send(msgBytes);

                // update last message sent ticks
                this.lastCommTime = Environment.TickCount;
            }
            catch (SocketException e)
            {
#if (!MF_FRAMEWORK_VERSION_V4_2 && !MF_FRAMEWORK_VERSION_V4_3 && !COMPACT_FRAMEWORK)
                // connection reset by broker
                if (e.SocketErrorCode == SocketError.ConnectionReset)
                    this.IsConnected = false;
#endif

                throw new MqttCommunicationException();
            }

#if (MF_FRAMEWORK_VERSION_V4_2 || MF_FRAMEWORK_VERSION_V4_3 || COMPACT_FRAMEWORK)
            // wait for answer from broker
            if (this.syncEndReceiving.WaitOne(timeout, false))
#else
            // wait for answer from broker
            if (this.syncEndReceiving.WaitOne(timeout))
#endif
            {
                // message received without exception
                if (this.exReceiving == null)
                    return this.msgReceived;
                // receiving thread catched exception
                else
                    throw this.exReceiving;
            }
            else
            {
                // throw timeout exception
                //throw new MqttTimeoutException();
                throw new MqttCommunicationException();
            }
        }

        /// <summary>
        /// Enqueue a message into the inflight queue
        /// </summary>
        /// <param name="msg">Message to enqueue</param>
        /// <param name="flow">Message flow (publish, acknowledge)</param>
        private void EnqueueInflight(MqttMsgBase msg, MqttMsgFlow flow)
        {
            // enqueue is needed (or not)
            bool enqueue = true;

            // if it is a PUBLISH message with QoS Level 2
            if ((msg.Type == MqttMsgBase.MQTT_MSG_PUBLISH_TYPE) &&
                (msg.QosLevel == MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE))
            {
                lock (this.inflightQueue)
                {
                    // if it is a PUBLISH message already received (it is in the inflight queue), the publisher
                    // re-sent it because it didn't received the PUBREC. In this case, we have to re-send PUBREC
                    MqttMsgContextFinder msgCtxFinder = new MqttMsgContextFinder(((MqttMsgPublish)msg).MessageId);
                    MqttMsgContext msgCtx = (MqttMsgContext)this.inflightQueue.Get(msgCtxFinder.Find);

                    // the PUBLISH message is alredy in the inflight queue, we don't need to re-enqueue but we need
                    // to change state to re-send PUBREC
                    if (msgCtx != null)
                    {
                        msgCtx.State = MqttMsgState.QueuedQos2;
                        msgCtx.Flow = MqttMsgFlow.ToAcknowledge;
                        enqueue = false;
                    }
                }
            }

            if (enqueue)
            {
                // set a default state
                MqttMsgState state = MqttMsgState.QueuedQos0;

                // based on QoS level, the messages flow between broker and client changes
                switch (msg.QosLevel)
                {
                    // QoS Level 0
                    case MqttMsgBase.QOS_LEVEL_AT_MOST_ONCE:

                        state = MqttMsgState.QueuedQos0;
                        break;

                    // QoS Level 1
                    case MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE:

                        state = MqttMsgState.QueuedQos1;
                        break;

                    // QoS Level 2
                    case MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE:

                        state = MqttMsgState.QueuedQos2;
                        break;
                }

                // queue message context
                MqttMsgContext msgContext = new MqttMsgContext()
                {
                    Message = msg,
                    State = state,
                    Flow = flow,
                    Attempt = 0
                };

                lock (this.inflightQueue)
                {
                    // enqueue message and unlock send thread
                    this.inflightQueue.Enqueue(msgContext);
                }
            }

            this.inflightWaitHandle.Set();
        }

        /// <summary>
        /// Enqueue a message into the internal queue
        /// </summary>
        /// <param name="msg">Message to enqueue</param>
        private void EnqueueInternal(MqttMsgBase msg)
        {
            // enqueue is needed (or not)
            bool enqueue = true;

            // if it is a PUBREL message (for QoS Level 2)
            if (msg.Type == MqttMsgBase.MQTT_MSG_PUBREL_TYPE)
            {
                lock (this.inflightQueue)
                {
                    // if it is a PUBREL but the corresponding PUBLISH isn't in the inflight queue,
                    // it means that we processed PUBLISH message and received PUBREL and we sent PUBCOMP
                    // but publisher didn't receive PUBCOMP so it re-sent PUBREL. We need only to re-send PUBCOMP.

                    MqttMsgContextFinder msgCtxFinder = new MqttMsgContextFinder(((MqttMsgPubrel)msg).MessageId);
                    MqttMsgContext msgCtx = (MqttMsgContext)this.inflightQueue.Get(msgCtxFinder.Find);
                    
                    // the PUBLISH message isn't in the inflight queue, it was already processed so
                    // we need to re-send PUBCOMP only
                    if (msgCtx == null)
                    {
                        MqttMsgPubcomp pubcomp = new MqttMsgPubcomp();
                        pubcomp.MessageId = ((MqttMsgPubrel)msg).MessageId;

                        this.Send(pubcomp.GetBytes());

                        enqueue = false;
                    }
                }
            }

            if (enqueue)
            {
                lock (this.internalQueue)
                {
                    this.internalQueue.Enqueue(msg);
                    this.inflightWaitHandle.Set();
                }
            }
        }

        /// <summary>
        /// Thread for receiving messages from broker
        /// </summary>
        private void ReceiveThread()
        {
            int readBytes = 0;
            byte[] fixedHeaderFirstByte = new byte[1];
            byte msgType;
                        
            while (this.isRunning)
            {
                try
                {
                    if (this.channel.DataAvailable)
                        // read first byte (fixed header)
                        readBytes = this.channel.Receive(fixedHeaderFirstByte);
                    else
                    {
                        // no bytes available, sleep before retry
                        readBytes = 0;
                        Thread.Sleep(10);
                    }

                    if (readBytes > 0)
                    {
                        // extract message type from received byte
                        msgType = (byte)((fixedHeaderFirstByte[0] & MqttMsgBase.MSG_TYPE_MASK) >> MqttMsgBase.MSG_TYPE_OFFSET);

                        switch (msgType)
                        {
                            // impossible, broker can't send CONNECT message
                            case MqttMsgBase.MQTT_MSG_CONNECT_TYPE:

                                throw new MqttClientException(MqttClientErrorCode.WrongBrokerMessage);
                                
                            // CONNACK message received from broker
                            case MqttMsgBase.MQTT_MSG_CONNACK_TYPE:

                                this.msgReceived = MqttMsgConnack.Parse(fixedHeaderFirstByte[0], this.channel);
                                this.syncEndReceiving.Set();
                                break;

                            // impossible, broker can't send PINGREQ message
                            case MqttMsgBase.MQTT_MSG_PINGREQ_TYPE:

                                throw new MqttClientException(MqttClientErrorCode.WrongBrokerMessage);

                            // PINGRESP message received from broker
                            case MqttMsgBase.MQTT_MSG_PINGRESP_TYPE:

                                this.msgReceived = MqttMsgPingResp.Parse(fixedHeaderFirstByte[0], this.channel);
                                this.syncEndReceiving.Set();
                                break;

                            // impossible, broker can't send SUBSCRIBE message
                            case MqttMsgBase.MQTT_MSG_SUBSCRIBE_TYPE:

                                throw new MqttClientException(MqttClientErrorCode.WrongBrokerMessage);

                            // SUBACK message received from broker
                            case MqttMsgBase.MQTT_MSG_SUBACK_TYPE:

                                // enqueue SUBACK message received (for QoS Level 1) into the internal queue
                                MqttMsgSuback suback = MqttMsgSuback.Parse(fixedHeaderFirstByte[0], this.channel);

                                // enqueue SUBACK message into the internal queue
                                this.EnqueueInternal(suback);

                                break;

                            // PUBLISH message received from broker
                            case MqttMsgBase.MQTT_MSG_PUBLISH_TYPE:

                                MqttMsgPublish publish = MqttMsgPublish.Parse(fixedHeaderFirstByte[0], this.channel);                               

                                // enqueue PUBLISH message to acknowledge into the inflight queue
                                this.EnqueueInflight(publish, MqttMsgFlow.ToAcknowledge);

                                break;

                            // PUBACK message received from broker
                            case MqttMsgBase.MQTT_MSG_PUBACK_TYPE:

                                // enqueue PUBACK message received (for QoS Level 1) into the internal queue
                                MqttMsgPuback puback = MqttMsgPuback.Parse(fixedHeaderFirstByte[0], this.channel);

                                // enqueue PUBACK message into the internal queue
                                this.EnqueueInternal(puback);

                                break;

                            // PUBREC message received from broker
                            case MqttMsgBase.MQTT_MSG_PUBREC_TYPE:

                                // enqueue PUBREC message received (for QoS Level 2) into the internal queue
                                MqttMsgPubrec pubrec = MqttMsgPubrec.Parse(fixedHeaderFirstByte[0], this.channel);

                                // enqueue PUBREC message into the internal queue
                                this.EnqueueInternal(pubrec);

                                break;

                            // PUBREL message received from broker
                            case MqttMsgBase.MQTT_MSG_PUBREL_TYPE:

                                // enqueue PUBREL message received (for QoS Level 2) into the internal queue
                                MqttMsgPubrel pubrel = MqttMsgPubrel.Parse(fixedHeaderFirstByte[0], this.channel);

                                // enqueue PUBREL message into the internal queue
                                this.EnqueueInternal(pubrel);

                                break;
                                
                            // PUBCOMP message received from broker
                            case MqttMsgBase.MQTT_MSG_PUBCOMP_TYPE:

                                // enqueue PUBCOMP message received (for QoS Level 2) into the internal queue
                                MqttMsgPubcomp pubcomp = MqttMsgPubcomp.Parse(fixedHeaderFirstByte[0], this.channel);

                                // enqueue PUBCOMP message into the internal queue
                                this.EnqueueInternal(pubcomp);

                                break;

                            // impossible, broker can't send UNSUBSCRIBE message
                            case MqttMsgBase.MQTT_MSG_UNSUBSCRIBE_TYPE:

                                throw new MqttClientException(MqttClientErrorCode.WrongBrokerMessage);

                            // UNSUBACK message received from broker
                            case MqttMsgBase.MQTT_MSG_UNSUBACK_TYPE:

                                // enqueue UNSUBACK message received (for QoS Level 1) into the internal queue
                                MqttMsgUnsuback unsuback = MqttMsgUnsuback.Parse(fixedHeaderFirstByte[0], this.channel);

                                // enqueue UNSUBACK message into the internal queue
                                this.EnqueueInternal(unsuback);

                                break;

                            default:

                                throw new MqttClientException(MqttClientErrorCode.WrongBrokerMessage);
                        }

                        this.exReceiving = null;
                    }

                }
                catch (Exception)
                {
                    this.exReceiving = new MqttCommunicationException();
                }
            }
        }

        /// <summary>
        /// Thread for sending keep alive message to broker
        /// </summary>
        private void KeepAliveThread()
        {
            long now = 0;
            int wait = this.keepAlivePeriod;
            this.isKeepAliveTimeout = false;

            while (this.isRunning)
            {
#if (MF_FRAMEWORK_VERSION_V4_2 || MF_FRAMEWORK_VERSION_V4_3 || COMPACT_FRAMEWORK)
                // waiting...
                this.keepAliveEvent.WaitOne(wait, false);
#else
                // waiting...
                this.keepAliveEvent.WaitOne(wait);
#endif

                if (this.isRunning)
                {
                    now = Environment.TickCount;
                    
                    // if timeout exceeded ... (keep alive period converted in ticks)
                    if ((now - this.lastCommTime) >= this.keepAlivePeriod) {
						// ... send keep alive
						this.Ping();
						wait = this.keepAlivePeriod;
					} else {
						// update waiting time
						wait = (int)(this.keepAlivePeriod - (now - this.lastCommTime));
					}
                }
            }

            if (this.isKeepAliveTimeout)
                this.IsConnected = false;
        }

        /// <summary>
        /// Thread for raising received message event
        /// </summary>
        private void ReceiveEventThread()
        {
            while (this.isRunning)
            {
                if (this.receiveQueue.Count == 0)
                    // wait on receiving message from client
                    this.receiveEventWaitHandle.WaitOne();

                // check if it is running or we are closing client
                if (this.isRunning)
                {
                    // get message from queue
                    MqttMsgBase msg = null;
                    lock (this.receiveQueue)
                    {
                        if (this.receiveQueue.Count > 0)
                            msg = (MqttMsgBase)this.receiveQueue.Dequeue();
                    }

                    if (msg != null)
                    {
                        switch (msg.Type)
                        {
                            // SUBACK message received from broker
                            case MqttMsgBase.MQTT_MSG_SUBACK_TYPE:

                                // raise subscribed topic event (SUBACK message received)
                                this.OnMqttMsgSubscribed((MqttMsgSuback)msg);
                                break;

                            // PUBLISH message received from broker
                            case MqttMsgBase.MQTT_MSG_PUBLISH_TYPE:

                                // raise PUBLISH message received event 
                                this.OnMqttMsgPublishReceived((MqttMsgPublish)msg);
                                break;

                            // PUBACK message received from broker
                            case MqttMsgBase.MQTT_MSG_PUBACK_TYPE:

                                // raise published message event
                                // (PUBACK received for QoS Level 1)
                                this.OnMqttMsgPublished(((MqttMsgPuback)msg).MessageId);
                                break;

                            // PUBREL message received from broker
                            case MqttMsgBase.MQTT_MSG_PUBREL_TYPE:

                                // raise message received event 
                                // (PUBREL received for QoS Level 2)
                                this.OnMqttMsgPublishReceived((MqttMsgPublish)msg);
                                break;

                            // PUBCOMP message received from broker
                            case MqttMsgBase.MQTT_MSG_PUBCOMP_TYPE:

                                // raise published message event
                                // (PUBCOMP received for QoS Level 2)
                                this.OnMqttMsgPublished(((MqttMsgPubcomp)msg).MessageId);
                                break;

                            // UNSUBACK message received from broker
                            case MqttMsgBase.MQTT_MSG_UNSUBACK_TYPE:

                                // raise unsubscribed topic event
                                this.OnMqttMsgUnsubscribed(((MqttMsgUnsuback)msg).MessageId);
                                break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Process inflight messages queue
        /// </summary>
        private void ProcessInflightThread()
        {
            MqttMsgContext msgContext = null;
            MqttMsgBase msgInflight = null;
            MqttMsgBase msgReceived = null;
            bool acknowledge = false;
            int timeout = Timeout.Infinite;

            try
            {
                while (this.isRunning)
                {
#if (MF_FRAMEWORK_VERSION_V4_2 || MF_FRAMEWORK_VERSION_V4_3 || COMPACT_FRAMEWORK)
                    // wait on message queueud to inflight
                    this.inflightWaitHandle.WaitOne(timeout, false);
#else
                    // wait on message queueud to inflight
                    this.inflightWaitHandle.WaitOne(timeout);
#endif

                    // it could be unblocked because Close() method is joining
                    if (this.isRunning)
                    {
                        lock (this.inflightQueue)
                        {
                            // set timeout tu MaxValue instead of Infinte (-1) to perform
                            // compare with calcultad current msgTimeout
                            timeout = Int32.MaxValue;

                            // a message inflight could be re-enqueued but we have to
                            // analyze it only just one time for cycle
                            int count = this.inflightQueue.Count;
                            // process all inflight queued messages
                            while (count > 0)
                            {
                                count--;
                                acknowledge = false;
                                msgReceived = null;

                                // dequeue message context from queue
                                msgContext = (MqttMsgContext)this.inflightQueue.Dequeue();

                                // get inflight message
                                msgInflight = (MqttMsgBase)msgContext.Message;

                                switch (msgContext.State)
                                {
                                    case MqttMsgState.QueuedQos0:

                                        // QoS 0, PUBLISH message to send to broker, no state change, no acknowledge
                                        if (msgContext.Flow == MqttMsgFlow.ToPublish)
                                        {
                                            this.Send(msgInflight.GetBytes());
                                        }
                                        // QoS 0, no need acknowledge
                                        else if (msgContext.Flow == MqttMsgFlow.ToAcknowledge)
                                        {
                                            // notify published message from broker (no need acknowledged)
                                            this.OnMqttMsgReceived(msgInflight);
                                        }
                                        break;

                                    case MqttMsgState.QueuedQos1:

                                        // QoS 1, PUBLISH or SUBSCRIBE/UNSUBSCRIBE message to send to broker, state change to wait PUBACK or SUBACK/UNSUBACK
                                        if (msgContext.Flow == MqttMsgFlow.ToPublish)
                                        {
                                            if (msgInflight.Type == MqttMsgBase.MQTT_MSG_PUBLISH_TYPE)
                                                // PUBLISH message to send, wait for PUBACK
                                                msgContext.State = MqttMsgState.WaitForPuback;
                                            else if (msgInflight.Type == MqttMsgBase.MQTT_MSG_SUBSCRIBE_TYPE)
                                                // SUBSCRIBE message to send, wait for SUBACK
                                                msgContext.State = MqttMsgState.WaitForSuback;
                                            else if (msgInflight.Type == MqttMsgBase.MQTT_MSG_UNSUBSCRIBE_TYPE)
                                                // UNSUBSCRIBE message to send, wait for UNSUBACK
                                                msgContext.State = MqttMsgState.WaitForUnsuback;

                                            msgContext.Timestamp = Environment.TickCount;
                                            msgContext.Attempt++;
                                            // retry ? set dup flag
                                            if (msgContext.Attempt > 1)
                                                msgInflight.DupFlag = true;

                                            this.Send(msgInflight.GetBytes());

                                            // update timeout
                                            int msgTimeout = (this.settings.DelayOnRetry - (Environment.TickCount - msgContext.Timestamp));
                                            timeout = (msgTimeout < timeout) ? msgTimeout : timeout;

                                            // re-enqueue message (I have to re-analyze for receiving PUBACK, SUBACK or UNSUBACK)
                                            this.inflightQueue.Enqueue(msgContext);
                                        }
                                        // QoS 1, PUBLISH message received from broker to acknowledge, send PUBACK
                                        else if (msgContext.Flow == MqttMsgFlow.ToAcknowledge)
                                        {
                                            MqttMsgPuback puback = new MqttMsgPuback();
                                            puback.MessageId = ((MqttMsgPublish)msgInflight).MessageId;

                                            this.Send(puback.GetBytes());

                                            // notify published message from broker and acknowledged
                                            this.OnMqttMsgReceived(msgInflight);
                                        }
                                        break;

                                    case MqttMsgState.QueuedQos2:

                                        // QoS 2, PUBLISH message to send to broker, state change to wait PUBREC
                                        if (msgContext.Flow == MqttMsgFlow.ToPublish)
                                        {
                                            msgContext.State = MqttMsgState.WaitForPubrec;
                                            msgContext.Timestamp = Environment.TickCount;
                                            msgContext.Attempt++;
                                            // retry ? set dup flag
                                            if (msgContext.Attempt > 1)
                                                msgInflight.DupFlag = true;

                                            this.Send(msgInflight.GetBytes());

                                            // update timeout
                                            int msgTimeout = (this.settings.DelayOnRetry - (Environment.TickCount - msgContext.Timestamp));
                                            timeout = (msgTimeout < timeout) ? msgTimeout : timeout;

                                            // re-enqueue message (I have to re-analyze for receiving PUBREC)
                                            this.inflightQueue.Enqueue(msgContext);
                                        }
                                        // QoS 2, PUBLISH message received from broker to acknowledge, send PUBREC, state change to wait PUBREL
                                        else if (msgContext.Flow == MqttMsgFlow.ToAcknowledge)
                                        {
                                            MqttMsgPubrec pubrec = new MqttMsgPubrec();
                                            pubrec.MessageId = ((MqttMsgPublish)msgInflight).MessageId;

                                            msgContext.State = MqttMsgState.WaitForPubrel;

                                            this.Send(pubrec.GetBytes());

                                            // re-enqueue message (I have to re-analyze for receiving PUBREL)
                                            this.inflightQueue.Enqueue(msgContext);
                                        }
                                        break;

                                    case MqttMsgState.WaitForPuback:
                                    case MqttMsgState.WaitForSuback:
                                    case MqttMsgState.WaitForUnsuback:

                                        // QoS 1, waiting for PUBACK of a PUBLISH message sent or
                                        //        waiting for SUBACK of a SUBSCRIBE message sent or
                                        //        waiting for UNSUBACK of a UNSUBSCRIBE message sent or
                                        if (msgContext.Flow == MqttMsgFlow.ToPublish)
                                        {
                                            acknowledge = false;
                                            lock (this.internalQueue)
                                            {
                                                if (this.internalQueue.Count > 0)
                                                    msgReceived = (MqttMsgBase)this.internalQueue.Peek();
                                            }

                                            // it is a PUBACK message or a SUBACK/UNSUBACK message
                                            if ((msgReceived != null) && ((msgReceived.Type == MqttMsgBase.MQTT_MSG_PUBACK_TYPE) ||
                                                                          (msgReceived.Type == MqttMsgBase.MQTT_MSG_SUBACK_TYPE) ||
                                                                          (msgReceived.Type == MqttMsgBase.MQTT_MSG_UNSUBACK_TYPE)))
                                            {
                                                // PUBACK message or SUBACK message for the current message
                                                if (((msgInflight.Type == MqttMsgBase.MQTT_MSG_PUBLISH_TYPE) && (((MqttMsgPuback)msgReceived).MessageId == ((MqttMsgPublish)msgInflight).MessageId)) ||
                                                    ((msgInflight.Type == MqttMsgBase.MQTT_MSG_SUBSCRIBE_TYPE) && (((MqttMsgSuback)msgReceived).MessageId == ((MqttMsgSubscribe)msgInflight).MessageId)) ||
                                                    ((msgInflight.Type == MqttMsgBase.MQTT_MSG_UNSUBSCRIBE_TYPE) && (((MqttMsgUnsuback)msgReceived).MessageId == ((MqttMsgUnsubscribe)msgInflight).MessageId)))
                                                {
                                                    lock (this.internalQueue)
                                                    {
                                                        // received message processed
                                                        this.internalQueue.Dequeue();
                                                        acknowledge = true;
                                                    }

                                                    // notify received acknowledge from broker of a published message or subscribe/unsubscribe message
                                                    this.OnMqttMsgReceived(msgReceived);
                                                }
                                            }

                                            // current message not acknowledged, no PUBACK or SUBACK/UNSUBACK or not equal messageid 
                                            if (!acknowledge)
                                            {
                                                // check timeout for receiving PUBACK since PUBLISH was sent or
                                                // for receiving SUBACK since SUBSCRIBE was sent or
                                                // for receiving UNSUBACK since UNSUBSCRIBE was sent
                                                if ((Environment.TickCount - msgContext.Timestamp) >= this.settings.DelayOnRetry)
                                                {
                                                    // max retry not reached, resend
                                                    if (msgContext.Attempt <= this.settings.AttemptsOnRetry)
                                                    {
                                                        msgContext.State = MqttMsgState.QueuedQos1;

                                                        // re-enqueue message
                                                        this.inflightQueue.Enqueue(msgContext);

                                                        // update timeout (0 -> reanalyze queue immediately)
                                                        timeout = 0;
                                                    }
                                                }
                                                else
                                                {
                                                    // re-enqueue message (I have to re-analyze for receiving PUBACK, SUBACK or UNSUBACK)
                                                    this.inflightQueue.Enqueue(msgContext);

                                                    // update timeout
                                                    int msgTimeout = (this.settings.DelayOnRetry - (Environment.TickCount - msgContext.Timestamp));
                                                    timeout = (msgTimeout < timeout) ? msgTimeout : timeout;
                                                }
                                            }
                                        }
                                        break;

                                    case MqttMsgState.WaitForPubrec:

                                        // QoS 2, waiting for PUBREC of a PUBLISH message sent
                                        if (msgContext.Flow == MqttMsgFlow.ToPublish)
                                        {
                                            acknowledge = false;
                                            lock (this.internalQueue)
                                            {
                                                if (this.internalQueue.Count > 0)
                                                    msgReceived = (MqttMsgBase)this.internalQueue.Peek();
                                            }

                                            // it is a PUBREC message
                                            if ((msgReceived != null) && (msgReceived.Type == MqttMsgBase.MQTT_MSG_PUBREC_TYPE))
                                            {
                                                // PUBREC message for the current PUBLISH message, send PUBREL, wait for PUBCOMP
                                                if (((MqttMsgPubrec)msgReceived).MessageId == ((MqttMsgPublish)msgInflight).MessageId)
                                                {
                                                    lock (this.internalQueue)
                                                    {
                                                        // received message processed
                                                        this.internalQueue.Dequeue();
                                                        acknowledge = true;
                                                    }

                                                    MqttMsgPubrel pubrel = new MqttMsgPubrel();
                                                    pubrel.MessageId = ((MqttMsgPublish)msgInflight).MessageId;

                                                    msgContext.State = MqttMsgState.WaitForPubcomp;
                                                    msgContext.Timestamp = Environment.TickCount;
                                                    msgContext.Attempt = 1;

                                                    this.Send(pubrel.GetBytes());

                                                    // update timeout
                                                    int msgTimeout = (this.settings.DelayOnRetry - (Environment.TickCount - msgContext.Timestamp));
                                                    timeout = (msgTimeout < timeout) ? msgTimeout : timeout;

                                                    // re-enqueue message
                                                    this.inflightQueue.Enqueue(msgContext);
                                                }
                                            }

                                            // current message not acknowledged
                                            if (!acknowledge)
                                            {
                                                // check timeout for receiving PUBREC since PUBLISH was sent
                                                if ((Environment.TickCount - msgContext.Timestamp) >= this.settings.DelayOnRetry)
                                                {
                                                    // max retry not reached, resend
                                                    if (msgContext.Attempt <= this.settings.AttemptsOnRetry)
                                                    {
                                                        msgContext.State = MqttMsgState.QueuedQos2;
                                                        
                                                        // re-enqueue message
                                                        this.inflightQueue.Enqueue(msgContext);

                                                        // update timeout (0 -> reanalyze queue immediately)
                                                        timeout = 0;
                                                    }
                                                }
                                                else
                                                {
                                                    // re-enqueue message
                                                    this.inflightQueue.Enqueue(msgContext);

                                                    // update timeout
                                                    int msgTimeout = (this.settings.DelayOnRetry - (Environment.TickCount - msgContext.Timestamp));
                                                    timeout = (msgTimeout < timeout) ? msgTimeout : timeout;
                                                }
                                            }
                                        }
                                        break;

                                    case MqttMsgState.WaitForPubrel:

                                        // QoS 2, waiting for PUBREL of a PUBREC message sent
                                        if (msgContext.Flow == MqttMsgFlow.ToAcknowledge)
                                        {
                                            lock (this.internalQueue)
                                            {
                                                if (this.internalQueue.Count > 0)
                                                    msgReceived = (MqttMsgBase)this.internalQueue.Peek();
                                            }

                                            // it is a PUBREL message
                                            if ((msgReceived != null) && (msgReceived.Type == MqttMsgBase.MQTT_MSG_PUBREL_TYPE))
                                            {
                                                // PUBREL message for the current message, send PUBCOMP
                                                if (((MqttMsgPubrel)msgReceived).MessageId == ((MqttMsgPublish)msgInflight).MessageId)
                                                {
                                                    lock (this.internalQueue)
                                                    {
                                                        // received message processed
                                                        this.internalQueue.Dequeue();
                                                    }

                                                    MqttMsgPubcomp pubcomp = new MqttMsgPubcomp();
                                                    pubcomp.MessageId = ((MqttMsgPublish)msgInflight).MessageId;

                                                    this.Send(pubcomp.GetBytes());

                                                    // notify published message from broker and acknowledged
                                                    this.OnMqttMsgReceived(msgInflight);
                                                }
                                                else
                                                {
                                                    // re-enqueue message
                                                    this.inflightQueue.Enqueue(msgContext);
                                                }
                                            }
                                            else
                                            {
                                                // re-enqueue message
                                                this.inflightQueue.Enqueue(msgContext);
                                            }
                                        }
                                        break;

                                    case MqttMsgState.WaitForPubcomp:

                                        // QoS 2, waiting for PUBCOMP of a PUBREL message sent
                                        if (msgContext.Flow == MqttMsgFlow.ToPublish)
                                        {
                                            acknowledge = false;
                                            lock (this.internalQueue)
                                            {
                                                if (this.internalQueue.Count > 0)
                                                    msgReceived = (MqttMsgBase)this.internalQueue.Peek();
                                            }

                                            // it is a PUBCOMP message
                                            if ((msgReceived != null) && (msgReceived.Type == MqttMsgBase.MQTT_MSG_PUBCOMP_TYPE))
                                            {
                                                // PUBCOMP message for the current message
                                                if (((MqttMsgPubcomp)msgReceived).MessageId == ((MqttMsgPublish)msgInflight).MessageId)
                                                {
                                                    lock (this.internalQueue)
                                                    {
                                                        // received message processed
                                                        this.internalQueue.Dequeue();
                                                        acknowledge = true;
                                                    }

                                                    // notify received acknowledge from broker of a published message
                                                    this.OnMqttMsgReceived(msgReceived);
                                                }
                                            }

                                            // current message not acknowledged
                                            if (!acknowledge)
                                            {
                                                // check timeout for receiving PUBCOMP since PUBREL was sent
                                                if ((Environment.TickCount - msgContext.Timestamp) >= this.settings.DelayOnRetry)
                                                {
                                                    // max retry not reached, resend
                                                    if (msgContext.Attempt < this.settings.AttemptsOnRetry)
                                                    {
                                                        msgContext.State = MqttMsgState.SendPubrel;
                                                        
                                                        // re-enqueue message
                                                        this.inflightQueue.Enqueue(msgContext);

                                                        // update timeout (0 -> reanalyze queue immediately)
                                                        timeout = 0;
                                                    }
                                                }
                                                else
                                                {
                                                    // re-enqueue message
                                                    this.inflightQueue.Enqueue(msgContext);

                                                    // update timeout
                                                    int msgTimeout = (this.settings.DelayOnRetry - (Environment.TickCount - msgContext.Timestamp));
                                                    timeout = (msgTimeout < timeout) ? msgTimeout : timeout;
                                                }
                                            }
                                        }
                                        break;

                                    case MqttMsgState.SendPubrec:

                                        // TODO : impossible ? --> QueuedQos2 ToAcknowledge
                                        break;

                                    case MqttMsgState.SendPubrel:

                                        // QoS 2, PUBREL message to send to broker, state change to wait PUBCOMP
                                        if (msgContext.Flow == MqttMsgFlow.ToPublish)
                                        {
                                            MqttMsgPubrel pubrel = new MqttMsgPubrel();
                                            pubrel.MessageId = ((MqttMsgPublish)msgInflight).MessageId;
                                          
                                            msgContext.State = MqttMsgState.WaitForPubcomp;
                                            msgContext.Timestamp = Environment.TickCount;
                                            msgContext.Attempt++;
                                            // retry ? set dup flag
                                            if (msgContext.Attempt > 1)
                                                pubrel.DupFlag = true;

                                            this.Send(pubrel.GetBytes());

                                            // update timeout
                                            int msgTimeout = (this.settings.DelayOnRetry - (Environment.TickCount - msgContext.Timestamp));
                                            timeout = (msgTimeout < timeout) ? msgTimeout : timeout;

                                            // re-enqueue message
                                            this.inflightQueue.Enqueue(msgContext);
                                        }
                                        break;

                                    case MqttMsgState.SendPubcomp:
                                        // TODO : impossible ?
                                        break;
                                    case MqttMsgState.SendPuback:
                                        // TODO : impossible ? --> QueuedQos1 ToAcknowledge
                                        break;
                                    default:
                                        break;
                                }
                            }

                            // if calculated timeout is MaxValue, it means that must be Infinite (-1)
                            if (timeout == Int32.MaxValue)
                                timeout = Timeout.Infinite;
                        }
                    }
                }
            }
            catch (MqttCommunicationException)
            {
                this.Close();
            }
        }

        /// <summary>
        /// Generate the next message identifier
        /// </summary>
        /// <returns>Message identifier</returns>
        private ushort GetMessageId()
        {
            if (this.messageIdCounter == 0)
                this.messageIdCounter++;
            else
                this.messageIdCounter = ((this.messageIdCounter % UInt16.MaxValue) != 0) ? (ushort)(this.messageIdCounter + 1) : (ushort)0;
            return this.messageIdCounter;
        }

        /// <summary>
        /// Finder class for PUBLISH message inside a queue
        /// </summary>
        internal class MqttMsgContextFinder
        {
            // PUBLISH message id
            internal ushort MessageId { get; set; }

            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="messageId">Message Id</param>
            internal MqttMsgContextFinder(ushort messageId)
            {
                this.MessageId = messageId;
            }

            internal bool Find(object item)
            {
                MqttMsgContext msgCtx = (MqttMsgContext)item;
                return ((msgCtx.Message.Type == MqttMsgBase.MQTT_MSG_PUBLISH_TYPE) &&
                        (((MqttMsgPublish)msgCtx.Message).MessageId == this.MessageId));
                    
            }
        }
    }
}
