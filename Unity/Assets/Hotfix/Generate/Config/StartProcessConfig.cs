using System;
using System.Collections.Generic;
using ET;
using MongoDB.Bson.Serialization.Attributes;
using ProtoBuf;

namespace ETHotfix
{
    [ProtoContract]
    [Config]
    public partial class StartProcessConfigCategory : ProtoObject
    {
        public static StartProcessConfigCategory Instance;
		
        [ProtoIgnore]
        [BsonIgnore]
        private Dictionary<int, StartProcessConfig> dict = new Dictionary<int, StartProcessConfig>();
		
        [BsonElement]
        [ProtoMember(1)]
        private List<StartProcessConfig> list = new List<StartProcessConfig>();
		
        public StartProcessConfigCategory()
        {
            Instance = this;
        }
		
        public void AfterDeserialization()
        {
            foreach (StartProcessConfig config in list)
            {
                this.dict.Add(config.Id, config);
            }
            list = null;
            this.EndInit();
        }
		
        public StartProcessConfig Get(int id)
        {
            this.dict.TryGetValue(id, out StartProcessConfig item);

            if (item == null)
            {
                throw new Exception($"配置找不到，配置表名: {nameof (StartProcessConfig)}，配置id: {id}");
            }

            return item;
        }
		
        public bool Contain(int id)
        {
            return this.dict.ContainsKey(id);
        }

        public Dictionary<int, StartProcessConfig> GetAll()
        {
            return this.dict;
        }

        public StartProcessConfig GetOne()
        {
            if (this.dict == null || this.dict.Count <= 0)
            {
                return null;
            }
            return this.dict.Values.GetEnumerator().Current;
        }
    }

    [ProtoContract]
	public partial class StartProcessConfig: ProtoObject, IConfig
	{
		[ProtoMember(1, IsRequired  = true)]
		public int Id { get; set; }
		[ProtoMember(2, IsRequired  = true)]
		public int MachineId { get; set; }
		[ProtoMember(3, IsRequired  = true)]
		public int InnerPort { get; set; }



        public void AfterDeserialization()
        {
            this.EndInit();
        }
	}
}
