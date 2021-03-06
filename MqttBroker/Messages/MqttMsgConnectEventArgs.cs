﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using uPLibrary.Networking.M2Mqtt.Messages;

namespace uPLibrary.Networking.M2Mqtt.Messages
{
    /// <summary>
    /// Event Args class for CONNECT message received from client
    /// </summary>
    public class MqttMsgConnectEventArgs : EventArgs
    {
        /// <summary>
        /// Message received from client
        /// </summary>
        public MqttMsgConnect Message { get; private set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="msg">CONNECT message received from client</param>
        public MqttMsgConnectEventArgs(MqttMsgConnect connect)
        {
            this.Message = connect;
        }
    }
} 
