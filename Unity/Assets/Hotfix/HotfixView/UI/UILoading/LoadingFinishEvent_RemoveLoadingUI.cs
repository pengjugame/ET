﻿using ET;

namespace ETHotfix
{

    [Event]    
    public class LoadingFinishEvent_RemoveLoadingUI : AEvent<HotfixEventType.LoadingFinish>
    {
        protected override async ETTask Run(HotfixEventType.LoadingFinish args)
        {
            await UIHelper.Create(args.Scene, UIType.UILoading);
        }
    }
}
