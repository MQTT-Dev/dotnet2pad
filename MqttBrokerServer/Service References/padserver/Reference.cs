﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.1
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace MqttBrokerServer.padserver {
    using System.Runtime.Serialization;
    using System;
    
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.Runtime.Serialization", "4.0.0.0")]
    [System.Runtime.Serialization.DataContractAttribute(Name="Aid_SpecialTopicInfo", Namespace="http://schemas.datacontract.org/2004/07/Model")]
    [System.SerializableAttribute()]
    [System.Runtime.Serialization.KnownTypeAttribute(typeof(MqttBrokerServer.padserver.SpecialTopicInfo))]
    public partial class Aid_SpecialTopicInfo : object, System.Runtime.Serialization.IExtensibleDataObject, System.ComponentModel.INotifyPropertyChanged {
        
        [System.NonSerializedAttribute()]
        private System.Runtime.Serialization.ExtensionDataObject extensionDataField;
        
        private MqttBrokerServer.padserver.Aid_TopicAttachment[] AttachmentListField;
        
        private string MainPicUrlField;
        
        private int SendFromField;
        
        private System.DateTime SendTimeField;
        
        private string[] SendToListField;
        
        private string TopicInfoField;
        
        private int TopicInfoIdField;
        
        private int TopicTypeField;
        
        [global::System.ComponentModel.BrowsableAttribute(false)]
        public System.Runtime.Serialization.ExtensionDataObject ExtensionData {
            get {
                return this.extensionDataField;
            }
            set {
                this.extensionDataField = value;
            }
        }
        
        [System.Runtime.Serialization.DataMemberAttribute(IsRequired=true)]
        public MqttBrokerServer.padserver.Aid_TopicAttachment[] AttachmentList {
            get {
                return this.AttachmentListField;
            }
            set {
                if ((object.ReferenceEquals(this.AttachmentListField, value) != true)) {
                    this.AttachmentListField = value;
                    this.RaisePropertyChanged("AttachmentList");
                }
            }
        }
        
        [System.Runtime.Serialization.DataMemberAttribute(IsRequired=true)]
        public string MainPicUrl {
            get {
                return this.MainPicUrlField;
            }
            set {
                if ((object.ReferenceEquals(this.MainPicUrlField, value) != true)) {
                    this.MainPicUrlField = value;
                    this.RaisePropertyChanged("MainPicUrl");
                }
            }
        }
        
        [System.Runtime.Serialization.DataMemberAttribute(IsRequired=true)]
        public int SendFrom {
            get {
                return this.SendFromField;
            }
            set {
                if ((this.SendFromField.Equals(value) != true)) {
                    this.SendFromField = value;
                    this.RaisePropertyChanged("SendFrom");
                }
            }
        }
        
        [System.Runtime.Serialization.DataMemberAttribute(IsRequired=true)]
        public System.DateTime SendTime {
            get {
                return this.SendTimeField;
            }
            set {
                if ((this.SendTimeField.Equals(value) != true)) {
                    this.SendTimeField = value;
                    this.RaisePropertyChanged("SendTime");
                }
            }
        }
        
        [System.Runtime.Serialization.DataMemberAttribute(IsRequired=true)]
        public string[] SendToList {
            get {
                return this.SendToListField;
            }
            set {
                if ((object.ReferenceEquals(this.SendToListField, value) != true)) {
                    this.SendToListField = value;
                    this.RaisePropertyChanged("SendToList");
                }
            }
        }
        
        [System.Runtime.Serialization.DataMemberAttribute(IsRequired=true)]
        public string TopicInfo {
            get {
                return this.TopicInfoField;
            }
            set {
                if ((object.ReferenceEquals(this.TopicInfoField, value) != true)) {
                    this.TopicInfoField = value;
                    this.RaisePropertyChanged("TopicInfo");
                }
            }
        }
        
        [System.Runtime.Serialization.DataMemberAttribute(IsRequired=true)]
        public int TopicInfoId {
            get {
                return this.TopicInfoIdField;
            }
            set {
                if ((this.TopicInfoIdField.Equals(value) != true)) {
                    this.TopicInfoIdField = value;
                    this.RaisePropertyChanged("TopicInfoId");
                }
            }
        }
        
        [System.Runtime.Serialization.DataMemberAttribute(IsRequired=true)]
        public int TopicType {
            get {
                return this.TopicTypeField;
            }
            set {
                if ((this.TopicTypeField.Equals(value) != true)) {
                    this.TopicTypeField = value;
                    this.RaisePropertyChanged("TopicType");
                }
            }
        }
        
        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        
        protected void RaisePropertyChanged(string propertyName) {
            System.ComponentModel.PropertyChangedEventHandler propertyChanged = this.PropertyChanged;
            if ((propertyChanged != null)) {
                propertyChanged(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
            }
        }
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.Runtime.Serialization", "4.0.0.0")]
    [System.Runtime.Serialization.DataContractAttribute(Name="SpecialTopicInfo", Namespace="http://schemas.datacontract.org/2004/07/TerminalInfoReleaseWCF")]
    [System.SerializableAttribute()]
    public partial class SpecialTopicInfo : MqttBrokerServer.padserver.Aid_SpecialTopicInfo {
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.Runtime.Serialization", "4.0.0.0")]
    [System.Runtime.Serialization.DataContractAttribute(Name="Aid_TopicAttachment", Namespace="http://schemas.datacontract.org/2004/07/Model")]
    [System.SerializableAttribute()]
    public partial class Aid_TopicAttachment : object, System.Runtime.Serialization.IExtensibleDataObject, System.ComponentModel.INotifyPropertyChanged {
        
        [System.NonSerializedAttribute()]
        private System.Runtime.Serialization.ExtensionDataObject extensionDataField;
        
        private byte[] AttachmentField;
        
        private string AttachmentNameField;
        
        private string AttachmentTypeField;
        
        private string AttachmentUrlField;
        
        [global::System.ComponentModel.BrowsableAttribute(false)]
        public System.Runtime.Serialization.ExtensionDataObject ExtensionData {
            get {
                return this.extensionDataField;
            }
            set {
                this.extensionDataField = value;
            }
        }
        
        [System.Runtime.Serialization.DataMemberAttribute(IsRequired=true)]
        public byte[] Attachment {
            get {
                return this.AttachmentField;
            }
            set {
                if ((object.ReferenceEquals(this.AttachmentField, value) != true)) {
                    this.AttachmentField = value;
                    this.RaisePropertyChanged("Attachment");
                }
            }
        }
        
        [System.Runtime.Serialization.DataMemberAttribute(IsRequired=true)]
        public string AttachmentName {
            get {
                return this.AttachmentNameField;
            }
            set {
                if ((object.ReferenceEquals(this.AttachmentNameField, value) != true)) {
                    this.AttachmentNameField = value;
                    this.RaisePropertyChanged("AttachmentName");
                }
            }
        }
        
        [System.Runtime.Serialization.DataMemberAttribute(IsRequired=true)]
        public string AttachmentType {
            get {
                return this.AttachmentTypeField;
            }
            set {
                if ((object.ReferenceEquals(this.AttachmentTypeField, value) != true)) {
                    this.AttachmentTypeField = value;
                    this.RaisePropertyChanged("AttachmentType");
                }
            }
        }
        
        [System.Runtime.Serialization.DataMemberAttribute(IsRequired=true)]
        public string AttachmentUrl {
            get {
                return this.AttachmentUrlField;
            }
            set {
                if ((object.ReferenceEquals(this.AttachmentUrlField, value) != true)) {
                    this.AttachmentUrlField = value;
                    this.RaisePropertyChanged("AttachmentUrl");
                }
            }
        }
        
        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        
        protected void RaisePropertyChanged(string propertyName) {
            System.ComponentModel.PropertyChangedEventHandler propertyChanged = this.PropertyChanged;
            if ((propertyChanged != null)) {
                propertyChanged(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
            }
        }
    }
    
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    [System.ServiceModel.ServiceContractAttribute(ConfigurationName="padserver.IServiceOfSepcialInfo", CallbackContract=typeof(MqttBrokerServer.padserver.IServiceOfSepcialInfoCallback))]
    public interface IServiceOfSepcialInfo {
        
        [System.ServiceModel.OperationContractAttribute(Action="http://tempuri.org/IServiceOfSepcialInfo/RegisterClient", ReplyAction="http://tempuri.org/IServiceOfSepcialInfo/RegisterClientResponse")]
        void RegisterClient(string clientId, string userName, int clientType);
        
        [System.ServiceModel.OperationContractAttribute(Action="http://tempuri.org/IServiceOfSepcialInfo/SendTopicInfo", ReplyAction="http://tempuri.org/IServiceOfSepcialInfo/SendTopicInfoResponse")]
        void SendTopicInfo(MqttBrokerServer.padserver.SpecialTopicInfo topicInfo);
        
        [System.ServiceModel.OperationContractAttribute(Action="http://tempuri.org/IServiceOfSepcialInfo/ReceiveTopicInfo", ReplyAction="http://tempuri.org/IServiceOfSepcialInfo/ReceiveTopicInfoResponse")]
        MqttBrokerServer.padserver.SpecialTopicInfo ReceiveTopicInfo(int topicId);
        
        [System.ServiceModel.OperationContractAttribute(Action="http://tempuri.org/IServiceOfSepcialInfo/PadOnline", ReplyAction="http://tempuri.org/IServiceOfSepcialInfo/PadOnlineResponse")]
        void PadOnline(string ClientID, string UserName, int ClientType);
    }
    
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    public interface IServiceOfSepcialInfoCallback {
        
        [System.ServiceModel.OperationContractAttribute(IsOneWay=true, Action="http://tempuri.org/IServiceOfSepcialInfo/SendOnLine")]
        void SendOnLine(string ClientID, string UserName, int ClientType);
    }
    
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    public interface IServiceOfSepcialInfoChannel : MqttBrokerServer.padserver.IServiceOfSepcialInfo, System.ServiceModel.IClientChannel {
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    public partial class ServiceOfSepcialInfoClient : System.ServiceModel.DuplexClientBase<MqttBrokerServer.padserver.IServiceOfSepcialInfo>, MqttBrokerServer.padserver.IServiceOfSepcialInfo {
        
        public ServiceOfSepcialInfoClient(System.ServiceModel.InstanceContext callbackInstance) : 
                base(callbackInstance) {
        }
        
        public ServiceOfSepcialInfoClient(System.ServiceModel.InstanceContext callbackInstance, string endpointConfigurationName) : 
                base(callbackInstance, endpointConfigurationName) {
        }
        
        public ServiceOfSepcialInfoClient(System.ServiceModel.InstanceContext callbackInstance, string endpointConfigurationName, string remoteAddress) : 
                base(callbackInstance, endpointConfigurationName, remoteAddress) {
        }
        
        public ServiceOfSepcialInfoClient(System.ServiceModel.InstanceContext callbackInstance, string endpointConfigurationName, System.ServiceModel.EndpointAddress remoteAddress) : 
                base(callbackInstance, endpointConfigurationName, remoteAddress) {
        }
        
        public ServiceOfSepcialInfoClient(System.ServiceModel.InstanceContext callbackInstance, System.ServiceModel.Channels.Binding binding, System.ServiceModel.EndpointAddress remoteAddress) : 
                base(callbackInstance, binding, remoteAddress) {
        }
        
        public void RegisterClient(string clientId, string userName, int clientType) {
            base.Channel.RegisterClient(clientId, userName, clientType);
        }
        
        public void SendTopicInfo(MqttBrokerServer.padserver.SpecialTopicInfo topicInfo) {
            base.Channel.SendTopicInfo(topicInfo);
        }
        
        public MqttBrokerServer.padserver.SpecialTopicInfo ReceiveTopicInfo(int topicId) {
            return base.Channel.ReceiveTopicInfo(topicId);
        }
        
        public void PadOnline(string ClientID, string UserName, int ClientType) {
            base.Channel.PadOnline(ClientID, UserName, ClientType);
        }
    }
}