﻿using System.Collections.Generic;

namespace ETHotfix
{
	/// <summary>
	/// 管理Scene上的UI
	/// </summary>
	public class UIComponent: Entity
	{
		public Dictionary<string, UI> UIs = new Dictionary<string, UI>();
	}
}