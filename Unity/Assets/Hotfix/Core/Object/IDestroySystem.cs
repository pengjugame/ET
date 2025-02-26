﻿using System;
using ET;

namespace ETHotfix
{
	public interface IDestroySystem
	{
		Type Type();
		void Run(object o);
	}

	[ObjectSystem]
	public abstract class DestroySystem<T> : IDestroySystem
	{
		public void Run(object o)
		{
			this.Destroy((T)o);
		}

		public Type Type()
		{
			return typeof(T);
		}

		public abstract void Destroy(T self);
	}
}
