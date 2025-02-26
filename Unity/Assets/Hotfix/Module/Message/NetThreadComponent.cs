﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using ET;

namespace ETHotfix
{
    public class NetThreadComponent: Entity
    {
        public static NetThreadComponent Instance;
        
        public const int checkInteral = 2000;
        public const int recvMaxIdleTime = 60000;
        public const int sendMaxIdleTime = 60000;

        public ThreadSynchronizationContext ThreadSynchronizationContext;
        
        public HashSet<AService> Services = new HashSet<AService>();

        
        public Random Random = new Random(Guid.NewGuid().GetHashCode());
    }
}