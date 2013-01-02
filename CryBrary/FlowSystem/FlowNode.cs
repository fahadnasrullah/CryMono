﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using CryEngine.Extensions;
using CryEngine.Initialization;
using CryEngine.Native;

using CryEngine.FlowSystem.Native;

namespace CryEngine.FlowSystem
{
    public abstract class FlowNode : CryScriptInstance
    {
        internal static void Register(string typeName)
        {
            NativeFlowNodeMethods.RegisterNode(typeName);
        }

        internal void InternalInitialize(NodeInfo nodeInfo)
        {
            NodeHandle = nodeInfo.nodePtr;
            NodeId = nodeInfo.nodeId;
            GraphId = nodeInfo.graphId;

            var registrationParams = (FlowNodeBaseRegistrationParams)Script.RegistrationParams;

            // create instances of OutputPort's.
            for(int i = 0; i < registrationParams.OutputMembers.Length; i++)
            {
                var outputMember = registrationParams.OutputMembers[i];

                Type type;
                if (outputMember.MemberType == MemberTypes.Field)
                    type = (outputMember as FieldInfo).FieldType;
                else
                    type = (outputMember as PropertyInfo).PropertyType;

                bool isGenericType = type.IsGenericType;
                Type genericType = isGenericType ? type.GetGenericArguments()[0] : typeof(void);

                object[] outputPortConstructorArgs = { NodeHandle, i };
                Type genericOutputPort = typeof(OutputPort<>);
                object outputPort = Activator.CreateInstance(isGenericType ? genericOutputPort.MakeGenericType(genericType) : type, outputPortConstructorArgs);

                if (outputMember.MemberType == MemberTypes.Field)
                    (outputMember as FieldInfo).SetValue(this, outputPort);
                else
                    (outputMember as PropertyInfo).SetValue(this, outputPort, null);
            }
        }

        internal virtual NodeConfig GetNodeConfig()
        {
            var registrationParams = (FlowNodeRegistrationParams)Script.RegistrationParams;

            return new NodeConfig(registrationParams.filter, registrationParams.description, (registrationParams.hasTargetEntity ? FlowNodeFlags.TargetEntity : 0), registrationParams.type, registrationParams.InputPorts, registrationParams.OutputPorts);
        }

        int GetInputPortId(MethodInfo method)
        {
            var registrationParams = (FlowNodeBaseRegistrationParams)Script.RegistrationParams;

            for (int i = 0; i < registrationParams.InputMethods.Length; i++)
            {
                if (registrationParams.InputMethods[i] == method)
                    return i;
            }

            throw new ArgumentException("Invalid input method specified");
        }

        #region Callbacks
        /// <summary>
        /// Called if one or more input ports have been activated.
        /// </summary>
        internal void OnPortActivated(int index, object value = null)
        {
            var registrationParams = (FlowNodeBaseRegistrationParams)Script.RegistrationParams;

            var method = registrationParams.InputMethods[index];

            if (value != null && method.GetParameters().Length > 0)
            {
                var parameters = new[] { value };
                method.Invoke(this, parameters);
            }
            else
                method.Invoke(this, null);
        }

        /// <summary>
        /// Called after level has been loaded, is not called on serialization.
        /// Note that this is called prior to GameRules.OnClientConnect and OnClientEnteredGame!
        /// </summary>
        protected virtual void OnInit() { }
        #endregion

        #region External methods
        /// <summary>
        /// Gets the int value of an flownode port.
        /// </summary>
        /// <param name="port"></param>
        /// <returns></returns>
        protected int GetPortInt(Action<int> port)
        {
            return NativeFlowNodeMethods.GetPortValueInt(NodeHandle, GetInputPortId(port.Method));
        }

        protected bool IsIntPortActive(Action<int> port)
        {
            return IsPortActive(GetInputPortId(port.Method));
        }

        protected T GetPortEnum<T>(Action<T> port) where T : struct
        {
#if !(RELEASE && RELEASE_DISABLE_CHECKS)
            if (!typeof(T).IsEnum)
                throw new ArgumentException("T must be an enumerated type");
#endif

            return (T)Enum.ToObject(typeof(T), NativeFlowNodeMethods.GetPortValueInt(NodeHandle, GetInputPortId(port.Method)));
        }

        protected bool IsEnumPortActive(Action<Enum> port)
        {
            return IsPortActive(GetInputPortId(port.Method));
        }

        /// <summary>
        /// Gets the float value of an flownode port.
        /// </summary>
        /// <param name="port"></param>
        /// <returns></returns>
        protected float GetPortFloat(Action<float> port)
        {
            return NativeFlowNodeMethods.GetPortValueFloat(NodeHandle, GetInputPortId(port.Method));
        }

        protected bool IsFloatPortActive(Action<float> port)
        {
            return IsPortActive(GetInputPortId(port.Method));
        }

        /// <summary>
        /// Gets the int value of an flownode port.
        /// </summary>
        /// <param name="port"></param>
        /// <returns></returns>
        protected Vec3 GetPortVec3(Action<Vec3> port)
        {
            return NativeFlowNodeMethods.GetPortValueVec3(NodeHandle, GetInputPortId(port.Method));
        }

        protected bool IsVec3PortActive(Action<Vec3> port)
        {
            return IsPortActive(GetInputPortId(port.Method));
        }

        /// <summary>
        /// Gets the string value of an flownode port.
        /// </summary>
        /// <param name="port"></param>
        /// <returns></returns>
        protected string GetPortString(Action<string> port)
        {
            return NativeFlowNodeMethods.GetPortValueString(NodeHandle, GetInputPortId(port.Method));
        }

        protected bool IsStringPortActive(Action<string> port)
        {
            return IsPortActive(GetInputPortId(port.Method));
        }

        /// <summary>
        /// Gets the bool value of an flownode port.
        /// </summary>
        /// <param name="port"></param>
        /// <returns></returns>
        protected bool GetPortBool(Action<bool> port)
        {
            return NativeFlowNodeMethods.GetPortValueBool(NodeHandle, GetInputPortId(port.Method));
        }

        protected bool IsBoolPortActive(Action<bool> port)
        {
            return IsPortActive(GetInputPortId(port.Method));
        }

        protected EntityId GetPortEntityId(Action<EntityId> port)
        {
            return new EntityId(NativeFlowNodeMethods.GetPortValueEntityId(NodeHandle, GetInputPortId(port.Method)));
        }

        protected bool IsEntityIdPortActive(Action<EntityId> port)
        {
            return IsPortActive(GetInputPortId(port.Method));
        }

        /// <summary>
        /// Used to check whether an input port is currently activated.
        /// </summary>
        /// <param name="port"></param>
        /// <returns></returns>
        private bool IsPortActive(int port) { return NativeFlowNodeMethods.IsPortActive(NodeHandle, port); }
        #endregion

        public virtual EntityBase TargetEntity
        {
            get
            {
                uint entId;
                var entPtr = NativeFlowNodeMethods.GetTargetEntity(NodeHandle, out entId);

                if (entPtr != IntPtr.Zero)
                    return Entity.CreateNativeEntity(new EntityId(entId), entPtr);

                return null;
            }
        }

        #region Overrides
        public override int GetHashCode()
        {
            unchecked // Overflow is fine, just wrap
            {
                int hash = 17;

                hash = hash * 29 + ScriptId.GetHashCode();
                hash = hash * 29 + NodeHandle.GetHashCode();
                hash = hash * 29 + NodeId.GetHashCode();
                hash = hash * 29 + GraphId.GetHashCode();

                return hash;
            }
        }
        #endregion

        public IntPtr NodeHandle { get; set; }

        public Int32 NodeId { get; set; }
        public Int64 GraphId { get; set; }
    }
}
