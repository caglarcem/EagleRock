﻿using EagleRock.Services.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using StackExchange.Redis;

namespace EagleRock.Services
{
    public class TrafficDataService : ITrafficDataService
    {
        private readonly IRedisService _redisService;
        private readonly ILogger<TrafficDataService> _logger;

        private const string EAGLEBOT_REDIS_KEY_PREFIX = "EagleBot_";

        public TrafficDataService(IRedisService redisService, ILogger<TrafficDataService> logger)
        {
            _redisService = redisService;
            _logger = logger;
        }

        public async Task<IEnumerable<TrafficDataModel>> GetAllTrafficData()
        {
            var currentValue = string.Empty;
            try
            {
                // Add wildcard to get EagleBot related data only in case there are other entries in cache.
                var values = await _redisService.GetValuesByKeyPattern($"{EAGLEBOT_REDIS_KEY_PREFIX}*");

                if(!(values?.Any() == true))
                {
                    return Enumerable.Empty<TrafficDataModel>();
                }

                var trafficDataModels = new List<TrafficDataModel>();

                // Key here is EagleBot_{EagleBotId}_{Timestamp}
                foreach (var rawTrafficData in values)
                {
                    currentValue = rawTrafficData;

                    var trafficData = JsonConvert.DeserializeObject<TrafficDataModel>(rawTrafficData);

                    if (trafficData != null)
                    {
                        trafficDataModels.Add(trafficData);
                    }
                }

                return trafficDataModels;
            }
            catch (Exception ex) when (ex is RedisConnectionException || ex is RedisTimeoutException)
            {
                _logger.LogError(ex, $"Cannot connect to Redis database.");
                throw;
            }
            catch (JsonSerializationException jex)
            {
                _logger.LogError(jex, $" Redis value {currentValue} cannot be deserialized to {nameof(TrafficDataModel)}.");
                throw;
            }
        }

        public async Task<string> SaveTrafficData(TrafficDataModel trafficData)
        {
            try
            {
                if (trafficData == null) return string.Empty    ;

                trafficData.Timestamp = DateTime.Now;

                var rawTrafficData = JsonConvert.SerializeObject(trafficData);

                if (rawTrafficData != null)
                {
                    // Make the cache key unique, by using the total microseconds elapsed since min datetime value
                    var totalMicroseconds = (trafficData.Timestamp - DateTime.MinValue).TotalMilliseconds * 1000;
                    var key = $"{EAGLEBOT_REDIS_KEY_PREFIX}{trafficData.EagleBotId}_{totalMicroseconds}";

                    await _redisService.SetValue(key, rawTrafficData);

                    return key;
                }

                return string.Empty;
            }
            catch (Exception ex) when (ex is RedisConnectionException || ex is RedisTimeoutException)
            {
                _logger.LogError(ex, $"Cannot connect to Redis database.");
                throw;
            }
            catch (JsonSerializationException jex)
            {
                _logger.LogError(jex, $" Redis value cannot be serialized.");
                throw;
            }
        }
    }
}