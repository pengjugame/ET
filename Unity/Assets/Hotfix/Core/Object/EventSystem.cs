﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using ET;

namespace ETHotfix
{
    public sealed class EventSystem
    {
        private static EventSystem instance;

        public static EventSystem Instance
        {
            get
            {
                return instance ??= new EventSystem();
            }
        }

        private readonly Dictionary<long, Entity> allComponents = new Dictionary<long, Entity>();

        private readonly UnOrderMultiMapSet<Type, Type> types = new UnOrderMultiMapSet<Type, Type>();

        private readonly Dictionary<Type, List<IEvent>> allEvents = new Dictionary<Type, List<IEvent>>();

        private readonly UnOrderMultiMap<Type, IAwakeSystem> awakeSystems = new UnOrderMultiMap<Type, IAwakeSystem>();

        private readonly UnOrderMultiMap<Type, IStartSystem> startSystems = new UnOrderMultiMap<Type, IStartSystem>();

        private readonly UnOrderMultiMap<Type, IDestroySystem> destroySystems = new UnOrderMultiMap<Type, IDestroySystem>();

        private readonly UnOrderMultiMap<Type, ILoadSystem> loadSystems = new UnOrderMultiMap<Type, ILoadSystem>();

        private readonly UnOrderMultiMap<Type, IUpdateSystem> updateSystems = new UnOrderMultiMap<Type, IUpdateSystem>();

        private readonly UnOrderMultiMap<Type, ILateUpdateSystem> lateUpdateSystems = new UnOrderMultiMap<Type, ILateUpdateSystem>();

        private readonly UnOrderMultiMap<Type, IChangeSystem> changeSystems = new UnOrderMultiMap<Type, IChangeSystem>();

        private readonly UnOrderMultiMap<Type, IDeserializeSystem> deserializeSystems = new UnOrderMultiMap<Type, IDeserializeSystem>();

        private Queue<long> updates = new Queue<long>();
        private Queue<long> updates2 = new Queue<long>();

        private readonly Queue<long> starts = new Queue<long>();

        private Queue<long> loaders = new Queue<long>();
        private Queue<long> loaders2 = new Queue<long>();

        private Queue<long> lateUpdates = new Queue<long>();
        private Queue<long> lateUpdates2 = new Queue<long>();

        /// <summary>
        /// 用于注册热更层Attribute
        /// </summary>
        /// <param name="attribute"></param>
        public void RegisterAttribute<T>() where T : BaseAttribute
        {
            List<Type> hotfixTypes = ET.Game.Hotfix.GetHotfixTypes();
            //List<Type> hotfixViewTypes = ET.Game.Hotfix.GetHotfixViewTypes();

            void RegisterAttributeInternal(List<Type> types)
            {
                foreach (Type type in types)
                {
                    if (type.IsAbstract)
                    {
                        continue;
                    }

                    object[] targetAttribute = type.GetCustomAttributes(typeof(T), false);

                    if (targetAttribute.Length > 0)
                    {
                        foreach (BaseAttribute objectSystem in targetAttribute)
                        {
                            this.types.Add(objectSystem.AttributeType, type);
                        }
                    }
                }
            }

            RegisterAttributeInternal(hotfixTypes);
            //RegisterAttributeInternal(hotfixViewTypes);
        }

        public void Add(List<Type> hotfixTypes)
        {
            foreach (Type type in hotfixTypes)
            {
                if (type.IsAbstract)
                {
                    continue;
                }

                object[] objects_ObjectSystemAttribute = type.GetCustomAttributes(typeof (ObjectSystemAttribute), false);

                if (objects_ObjectSystemAttribute.Length > 0)
                {
                    this.types.Add(typeof (ObjectSystemAttribute), type);
                }

                object[] objects_EventAttribute = type.GetCustomAttributes(typeof (EventAttribute), false);

                if (objects_EventAttribute.Length > 0)
                {
                    this.types.Add(typeof (EventAttribute), type);
                }
            }

            this.awakeSystems.Clear();
            this.lateUpdateSystems.Clear();
            this.updateSystems.Clear();
            this.startSystems.Clear();
            this.loadSystems.Clear();
            this.changeSystems.Clear();
            this.destroySystems.Clear();
            this.deserializeSystems.Clear();
            
            foreach (Type type in this.GetTypes(typeof (ObjectSystemAttribute)))
            {
                object obj = Activator.CreateInstance(type);
                switch (obj)
                {
                    case IAwakeSystem objectSystem:
                        this.awakeSystems.Add(objectSystem.Type(), objectSystem);
                        break;
                    case IUpdateSystem updateSystem:
                        this.updateSystems.Add(updateSystem.Type(), updateSystem);
                        break;
                    case ILateUpdateSystem lateUpdateSystem:
                        this.lateUpdateSystems.Add(lateUpdateSystem.Type(), lateUpdateSystem);
                        break;
                    case IStartSystem startSystem:
                        this.startSystems.Add(startSystem.Type(), startSystem);
                        break;
                    case IDestroySystem destroySystem:
                        this.destroySystems.Add(destroySystem.Type(), destroySystem);
                        break;
                    case ILoadSystem loadSystem:
                        this.loadSystems.Add(loadSystem.Type(), loadSystem);
                        break;
                    case IChangeSystem changeSystem:
                        this.changeSystems.Add(changeSystem.Type(), changeSystem);
                        break;
                    case IDeserializeSystem deserializeSystem:
                        this.deserializeSystems.Add(deserializeSystem.Type(), deserializeSystem);
                        break;
                }
            }

            allEvents.Clear();
            foreach (Type type in this.GetTypes(typeof (EventAttribute)))
            {
                IEvent obj = Activator.CreateInstance(type) as IEvent;
                if (obj == null)
                {
                    Log.Error($"type: {type} not is AEvent! ");
                    continue;
                }

                Type eventType = obj.GetEventType();
                RegisterEvent(eventType, obj);

                //hotfix可以订阅model层的事件
                if (!ET.Game.Hotfix.GetHotfixTypes().Contains(eventType))
                {
                    Func<object, ETTask> action = obj.GetEventTask();
                    ET.Game.EventSystem.RegisterEvent(eventType, new EventProxy(eventType, action));
                }
            }

            this.Load();
        }

        public void RegisterEvent(Type eventType, IEvent e)
        {
            if (!this.allEvents.ContainsKey(eventType))
            {
                this.allEvents.Add(eventType, new List<IEvent>());
            }

            this.allEvents[eventType].Add(e);
        }

        public HashSet<Type> GetTypes(Type systemAttributeType)
        {
            if (!this.types.ContainsKey(systemAttributeType))
            {
                return new HashSet<Type>();
            }

            return this.types[systemAttributeType];
        }

        public void RegisterSystem(Entity component, bool isRegister = true)
        {
            if (!isRegister)
            {
                this.Remove(component.InstanceId);
                return;
            }

            this.allComponents.Add(component.InstanceId, component);

            Type type = component.GetType();

            if (this.loadSystems.ContainsKey(type))
            {
                this.loaders.Enqueue(component.InstanceId);
            }

            if (this.updateSystems.ContainsKey(type))
            {
                this.updates.Enqueue(component.InstanceId);
            }

            if (this.startSystems.ContainsKey(type))
            {
                this.starts.Enqueue(component.InstanceId);
            }

            if (this.lateUpdateSystems.ContainsKey(type))
            {
                this.lateUpdates.Enqueue(component.InstanceId);
            }
        }

        public void Remove(long instanceId)
        {
            this.allComponents.Remove(instanceId);
        }

        public Entity Get(long instanceId)
        {
            Entity component = null;
            this.allComponents.TryGetValue(instanceId, out component);
            return component;
        }

        public bool IsRegister(long instanceId)
        {
            return this.allComponents.ContainsKey(instanceId);
        }

        public void Deserialize(Entity component)
        {
            List<IDeserializeSystem> iDeserializeSystems = this.deserializeSystems[component.GetType()];
            if (iDeserializeSystems == null)
            {
                return;
            }

            foreach (IDeserializeSystem deserializeSystem in iDeserializeSystems)
            {
                if (deserializeSystem == null)
                {
                    continue;
                }

                try
                {
                    deserializeSystem.Run(component);
                }
                catch (Exception e)
                {
                    Log.Error(e);
                }
            }
        }

        public void Awake(Entity component)
        {
            List<IAwakeSystem> iAwakeSystems = this.awakeSystems[component.GetType()];
            if (iAwakeSystems == null)
            {
                return;
            }

            foreach (IAwakeSystem aAwakeSystem in iAwakeSystems)
            {
                if (aAwakeSystem == null)
                {
                    continue;
                }

                IAwake iAwake = aAwakeSystem as IAwake;
                if (iAwake == null)
                {
                    continue;
                }

                try
                {
                    iAwake.Run(component);
                }
                catch (Exception e)
                {
                    Log.Error(e);
                }
            }
        }

        public void Awake<P1>(Entity component, P1 p1)
        {
            List<IAwakeSystem> iAwakeSystems = this.awakeSystems[component.GetType()];
            if (iAwakeSystems == null)
            {
                return;
            }

            foreach (IAwakeSystem aAwakeSystem in iAwakeSystems)
            {
                if (aAwakeSystem == null)
                {
                    continue;
                }

                IAwake<P1> iAwake = aAwakeSystem as IAwake<P1>;
                if (iAwake == null)
                {
                    continue;
                }

                try
                {
                    iAwake.Run(component, p1);
                }
                catch (Exception e)
                {
                    Log.Error(e);
                }
            }
        }

        public void Awake<P1, P2>(Entity component, P1 p1, P2 p2)
        {
            List<IAwakeSystem> iAwakeSystems = this.awakeSystems[component.GetType()];
            if (iAwakeSystems == null)
            {
                return;
            }

            foreach (IAwakeSystem aAwakeSystem in iAwakeSystems)
            {
                if (aAwakeSystem == null)
                {
                    continue;
                }

                IAwake<P1, P2> iAwake = aAwakeSystem as IAwake<P1, P2>;
                if (iAwake == null)
                {
                    continue;
                }

                try
                {
                    iAwake.Run(component, p1, p2);
                }
                catch (Exception e)
                {
                    Log.Error(e);
                }
            }
        }

        public void Awake<P1, P2, P3>(Entity component, P1 p1, P2 p2, P3 p3)
        {
            List<IAwakeSystem> iAwakeSystems = this.awakeSystems[component.GetType()];
            if (iAwakeSystems == null)
            {
                return;
            }

            foreach (IAwakeSystem aAwakeSystem in iAwakeSystems)
            {
                if (aAwakeSystem == null)
                {
                    continue;
                }

                IAwake<P1, P2, P3> iAwake = aAwakeSystem as IAwake<P1, P2, P3>;
                if (iAwake == null)
                {
                    continue;
                }

                try
                {
                    iAwake.Run(component, p1, p2, p3);
                }
                catch (Exception e)
                {
                    Log.Error(e);
                }
            }
        }

        public void Awake<P1, P2, P3, P4>(Entity component, P1 p1, P2 p2, P3 p3, P4 p4)
        {
            List<IAwakeSystem> iAwakeSystems = this.awakeSystems[component.GetType()];
            if (iAwakeSystems == null)
            {
                return;
            }

            foreach (IAwakeSystem aAwakeSystem in iAwakeSystems)
            {
                if (aAwakeSystem == null)
                {
                    continue;
                }

                IAwake<P1, P2, P3, P4> iAwake = aAwakeSystem as IAwake<P1, P2, P3, P4>;
                if (iAwake == null)
                {
                    continue;
                }

                try
                {
                    iAwake.Run(component, p1, p2, p3, p4);
                }
                catch (Exception e)
                {
                    Log.Error(e);
                }
            }
        }

        public void Change(Entity component)
        {
            List<IChangeSystem> iChangeSystems = this.changeSystems[component.GetType()];
            if (iChangeSystems == null)
            {
                return;
            }

            foreach (IChangeSystem iChangeSystem in iChangeSystems)
            {
                if (iChangeSystem == null)
                {
                    continue;
                }

                try
                {
                    iChangeSystem.Run(component);
                }
                catch (Exception e)
                {
                    Log.Error(e);
                }
            }
        }

        public void Load()
        {
            while (this.loaders.Count > 0)
            {
                long instanceId = this.loaders.Dequeue();
                Entity component;
                if (!this.allComponents.TryGetValue(instanceId, out component))
                {
                    continue;
                }

                if (component.IsDisposed)
                {
                    continue;
                }

                List<ILoadSystem> iLoadSystems = this.loadSystems[component.GetType()];
                if (iLoadSystems == null)
                {
                    continue;
                }

                this.loaders2.Enqueue(instanceId);

                foreach (ILoadSystem iLoadSystem in iLoadSystems)
                {
                    try
                    {
                        iLoadSystem.Run(component);
                    }
                    catch (Exception e)
                    {
                        Log.Error(e);
                    }
                }
            }

            ObjectHelper.Swap(ref this.loaders, ref this.loaders2);
        }

        private void Start()
        {
            while (this.starts.Count > 0)
            {
                long instanceId = this.starts.Dequeue();
                Entity component;
                if (!this.allComponents.TryGetValue(instanceId, out component))
                {
                    continue;
                }

                List<IStartSystem> iStartSystems = this.startSystems[component.GetType()];
                if (iStartSystems == null)
                {
                    continue;
                }

                foreach (IStartSystem iStartSystem in iStartSystems)
                {
                    try
                    {
                        iStartSystem.Run(component);
                    }
                    catch (Exception e)
                    {
                        Log.Error(e);
                    }
                }
            }
        }

        public void Destroy(Entity component)
        {
            List<IDestroySystem> iDestroySystems = this.destroySystems[component.GetType()];
            if (iDestroySystems == null)
            {
                return;
            }

            foreach (IDestroySystem iDestroySystem in iDestroySystems)
            {
                if (iDestroySystem == null)
                {
                    continue;
                }

                try
                {
                    iDestroySystem.Run(component);
                }
                catch (Exception e)
                {
                    Log.Error(e);
                }
            }
        }

        public void Update()
        {
            this.Start();

            while (this.updates.Count > 0)
            {
                long instanceId = this.updates.Dequeue();
                Entity component;
                if (!this.allComponents.TryGetValue(instanceId, out component))
                {
                    continue;
                }

                if (component.IsDisposed)
                {
                    continue;
                }

                List<IUpdateSystem> iUpdateSystems = this.updateSystems[component.GetType()];
                if (iUpdateSystems == null)
                {
                    continue;
                }

                this.updates2.Enqueue(instanceId);

                foreach (IUpdateSystem iUpdateSystem in iUpdateSystems)
                {
                    try
                    {
                        iUpdateSystem.Run(component);
                    }
                    catch (Exception e)
                    {
                        Log.Error(e);
                    }
                }
            }

            ObjectHelper.Swap(ref this.updates, ref this.updates2);
        }

        public void LateUpdate()
        {
            while (this.lateUpdates.Count > 0)
            {
                long instanceId = this.lateUpdates.Dequeue();
                Entity component;
                if (!this.allComponents.TryGetValue(instanceId, out component))
                {
                    continue;
                }

                if (component.IsDisposed)
                {
                    continue;
                }

                List<ILateUpdateSystem> iLateUpdateSystems = this.lateUpdateSystems[component.GetType()];
                if (iLateUpdateSystems == null)
                {
                    continue;
                }

                this.lateUpdates2.Enqueue(instanceId);

                foreach (ILateUpdateSystem iLateUpdateSystem in iLateUpdateSystems)
                {
                    try
                    {
                        iLateUpdateSystem.Run(component);
                    }
                    catch (Exception e)
                    {
                        Log.Error(e);
                    }
                }
            }

            ObjectHelper.Swap(ref this.lateUpdates, ref this.lateUpdates2);
        }

        public async ETTask Publish<T>(T a) where T : struct
        {
            List<IEvent> iEvents;
            if (!this.allEvents.TryGetValue(typeof (T), out iEvents))
            {
                return;
            }

            using var list = ListComponent<ETTask>.Create();

            foreach (IEvent obj in iEvents)
            {
                if ((obj is AEvent<T> aEvent))
                {
                    list.List.Add(aEvent.Handle(a));
                }
                else
                {
                    Log.Error($"event error: {obj.GetType()}");
                }
            }

            try
            {
                await ETTaskHelper.WaitAll(list.List);
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            HashSet<Type> noParent = new HashSet<Type>();
            Dictionary<Type, int> typeCount = new Dictionary<Type, int>();

            HashSet<Type> noDomain = new HashSet<Type>();

            foreach (var kv in this.allComponents)
            {
                Type type = kv.Value.GetType();
                if (kv.Value.Parent == null)
                {
                    noParent.Add(type);
                }

                if (kv.Value.Domain == null)
                {
                    noDomain.Add(type);
                }

                if (typeCount.ContainsKey(type))
                {
                    typeCount[type]++;
                }
                else
                {
                    typeCount[type] = 1;
                }
            }

            sb.AppendLine("not set parent type: ");
            foreach (Type type in noParent)
            {
                sb.AppendLine($"\t{type.Name}");
            }

            sb.AppendLine("not set domain type: ");
            foreach (Type type in noDomain)
            {
                sb.AppendLine($"\t{type.Name}");
            }

            IOrderedEnumerable<KeyValuePair<Type, int>> orderByDescending = typeCount.OrderByDescending(s => s.Value);

            sb.AppendLine("Entity Count: ");
            foreach (var kv in orderByDescending)
            {
                if (kv.Value == 1)
                {
                    continue;
                }

                sb.AppendLine($"\t{kv.Key.Name}: {kv.Value}");
            }

            return sb.ToString();
        }

        public void Dispose()
        {
            instance = null;
        }
    }
}