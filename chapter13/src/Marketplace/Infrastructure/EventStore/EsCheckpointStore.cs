using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using Newtonsoft.Json;
using ExpectedVersion = EventStore.ClientAPI.ExpectedVersion;

namespace Marketplace.Infrastructure.EventStore
{
    public class EsCheckpointStore : ICheckpointStore
    {
        private readonly IEventStoreConnection _connection;
        private readonly string _streamName;

        private const string CheckpointStreamPrefix = "checkpoint:";

        public EsCheckpointStore(IEventStoreConnection connection, 
            string subscriptionName)
        {
            _connection = connection;
            _streamName = CheckpointStreamPrefix + subscriptionName;
        }

        public async Task<Position?> GetCheckpoint()
        {
            var slice = await _connection
                .ReadStreamEventsBackwardAsync(_streamName, -1, 1, false);
            
            var eventData = slice.Events.FirstOrDefault();

            if (eventData.Equals(default(ResolvedEvent)))
            {
                await StoreCheckpoint(AllCheckpoint.AllStart);
                await SetStreamMaxCount();
                return AllCheckpoint.AllStart;
            }

            return eventData.Deserialze<Checkpoint>()?.Position;
        }

        public Task StoreCheckpoint(Position? checkpoint)
        {
            var @event = new Checkpoint{ Position = checkpoint};
            var preparedEvent =
                new EventData(
                    Guid.NewGuid(),
                    "$checkpoint",
                    true,
                    Encoding.UTF8.GetBytes(
                        JsonConvert.SerializeObject(@event)),
                    null
                );
            
            return _connection.AppendToStreamAsync(
                _streamName,
                ExpectedVersion.Any,
                preparedEvent);
        }

        private async Task SetStreamMaxCount()
        {
            var metadata = await _connection.GetStreamMetadataAsync(_streamName);

            if (!metadata.StreamMetadata.MaxCount.HasValue)
                await _connection.SetStreamMetadataAsync(_streamName, ExpectedVersion.Any,
                    StreamMetadata.Create(1));
        }

        private class Checkpoint
        {
            public Position? Position { get; set; }
        }
    }
}
