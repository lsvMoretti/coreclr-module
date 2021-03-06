using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Threading.Tasks;
using AltV.Net.NetworkingEntity.Elements.Args;
using AltV.Net.NetworkingEntity.Elements.Pools;
using Entity;

namespace AltV.Net.NetworkingEntity.Elements.Entities
{
    public class NetworkingEntity : INetworkingEntity, IInternalNetworkingEntity
    {
        public Entity.Entity StreamedEntity { get; }

        private IEntityStreamer entityStreamer;

        public EntityDataSnapshot Snapshot { get; private set; }

        public INetworkingClient MainStreamer { get; }

        public StreamingType StreamingType { get; }

        public HashSet<INetworkingClient> StreamedInClients { get; } = new HashSet<INetworkingClient>();

        public ulong Id => StreamedEntity?.Id ?? 0;

        public int Dimension
        {
            get => StreamedEntity.Dimension;
            set
            {
                StreamedEntity.Dimension = value;
                entityStreamer.UpdateEntityDimension(StreamedEntity, value);
            }
        }

        public Position Position
        {
            get => StreamedEntity.Position;
            set
            {
                StreamedEntity.Position = value;
                entityStreamer.UpdateEntityPosition(StreamedEntity, value);
            }
        }

        public float Range
        {
            get => StreamedEntity.Range;
            set
            {
                StreamedEntity.Range = value;
                entityStreamer.UpdateEntityRange(StreamedEntity, value);
            }
        }

        public bool Exists { get; set; }

        public NetworkingEntity(IEntityStreamer entityStreamer, Entity.Entity streamedEntity,
            StreamingType streamingType = StreamingType.Default)
        {
            this.entityStreamer = entityStreamer;
            StreamedEntity = streamedEntity;
            StreamingType = streamingType;
            Exists = true;
            Snapshot = new EntityDataSnapshot(Id);
        }

        public NetworkingEntity(Position position, int dimension, float range, Dictionary<string, object> data,
            StreamingType streamingType = StreamingType.Default)
        {
            var entity = new Entity.Entity {Position = position, Dimension = dimension, Range = range};
            foreach (var (key, value) in data)
            {
                entity.Data[key] = MValueUtils.ToMValue(value);
            }

            StreamingType = streamingType;
            StreamedEntity = entity;
            Exists = false;
        }

        public void Init(ulong id, IEntityStreamer streamer)
        {
            StreamedEntity.Id = id;
            entityStreamer = streamer;
            Snapshot = new EntityDataSnapshot(Id);
            if (StreamingType == StreamingType.DataStreaming)
            {
                foreach (var (key, value) in StreamedEntity.Data)
                {
                    SetData(key, value);
                }

                this.StreamedEntity.Data.Clear();
            }

            Exists = true;
        }

        public void SetData(string key, bool value)
        {
            UpdateData(key, new MValue {BoolValue = value});
        }

        public void SetData(string key, double value)
        {
            UpdateData(key, new MValue {DoubleValue = value});
        }

        public void SetData(string key, string value)
        {
            UpdateData(key, new MValue {StringValue = value});
        }

        public void SetData(string key, long value)
        {
            UpdateData(key, new MValue {IntValue = value});
        }

        public void SetData(string key, ulong value)
        {
            UpdateData(key, new MValue {UintValue = value});
        }

        public void SetDataNull(string key)
        {
            UpdateData(key, new MValue {NullValue = true});
        }

        public void SetData(string key, IDictionary<string, object> value)
        {
            var dictionaryMValue = new DictionaryMValue();
            foreach (var (currKey, dictValue) in value)
            {
                dictionaryMValue.Value[currKey] = MValueUtils.ToMValue(dictValue);
            }

            var mValue = new MValue {DictionaryValue = dictionaryMValue};
            UpdateData(key, mValue);
        }

        public void SetData(string key, IEnumerable<object> value)
        {
            var listMValue = new ListMValue();
            foreach (var curr in value)
            {
                listMValue.Value.Add(MValueUtils.ToMValue(curr));
            }

            var mValue = new MValue {ListValue = listMValue};
            UpdateData(key, mValue);
        }

        public void SetData(string key, object value)
        {
            UpdateData(key, MValueUtils.ToMValue(value));
        }

        public bool GetData(string key, out bool value)
        {
            if (StreamedEntity.Data.TryGetValue(key, out var mValue) &&
                mValue.MValueCase == MValue.MValueOneofCase.BoolValue)
            {
                value = mValue.BoolValue;
                return true;
            }

            value = default;
            return false;
        }

        public bool GetData(string key, out double value)
        {
            if (StreamedEntity.Data.TryGetValue(key, out var mValue) &&
                mValue.MValueCase == MValue.MValueOneofCase.DoubleValue)
            {
                value = mValue.DoubleValue;
                return true;
            }

            value = default;
            return false;
        }

        public bool GetData(string key, out string value)
        {
            if (StreamedEntity.Data.TryGetValue(key, out var mValue) &&
                mValue.MValueCase == MValue.MValueOneofCase.StringValue)
            {
                value = mValue.StringValue;
                return true;
            }

            value = default;
            return false;
        }

        public bool GetData(string key, out long value)
        {
            if (StreamedEntity.Data.TryGetValue(key, out var mValue) &&
                mValue.MValueCase == MValue.MValueOneofCase.IntValue)
            {
                value = mValue.IntValue;
                return true;
            }

            value = default;
            return false;
        }

        public bool GetData(string key, out ulong value)
        {
            if (StreamedEntity.Data.TryGetValue(key, out var mValue) &&
                mValue.MValueCase == MValue.MValueOneofCase.UintValue)
            {
                value = mValue.UintValue;
                return true;
            }

            value = default;
            return false;
        }

        public bool GetData<T>(string key, out T value)
        {
            if (StreamedEntity.Data.TryGetValue(key, out var mValue))
            {
                var obj = MValueUtils.FromMValue(mValue);
                if (obj is T expectedObject)
                {
                    value = expectedObject;
                    return true;
                }
            }

            value = default;
            return false;
        }

        public bool IsDataNull(string key)
        {
            return StreamedEntity.Data.TryGetValue(key, out var mValue) &&
                   mValue.MValueCase == MValue.MValueOneofCase.NullValue;
        }

        private void UpdateData(string key, MValue value)
        {
            StreamedEntity.Data[key] = value;
            Snapshot.Update(key);
            entityStreamer.UpdateEntityData(this, key, value);
        }

        public Task Remove()
        {
            return AltNetworking.RemoveEntity(this);
        }

        // Internal

        public void ClientStreamedIn(INetworkingClient client)
        {
            lock (StreamedInClients)
            {
                StreamedInClients.Add(client);
            }
        }

        public bool ClientStreamedOut(INetworkingClient client)
        {
            lock (StreamedInClients)
            {
                return StreamedInClients.Remove(client);
            }
        }
    }
}