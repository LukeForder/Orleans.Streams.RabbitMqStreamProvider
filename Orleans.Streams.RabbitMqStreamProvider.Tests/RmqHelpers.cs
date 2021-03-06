﻿using RabbitMQ.Client;

namespace RabbitMqStreamTests
{
    public enum RmqSerializer
    {
        Default,
        ProtoBuf
    }

    public static class RmqHelpers
    {
        public static void EnsureEmptyQueue()
        {
            var factory = new ConnectionFactory
            {
                HostName = "localhost",
                VirtualHost = "/",
                Port = 5672,
                UserName = "guest",
                Password = "guest"
            };

            using (var connection = factory.CreateConnection())
            using (var channel = connection.CreateModel())
            {
                channel.QueuePurge(Globals.StreamNameSpaceDefault);
                channel.QueuePurge(Globals.StreamNameSpaceProtoBuf);
            }
        }
    }
}
