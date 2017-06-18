﻿using System;
using Orleans.Runtime;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Orleans.Streams.RabbitMq
{
    internal class RabbitMqConsumer : IRabbitMqConsumer
    {
        private readonly RabbitMqConnector _connection;

        public RabbitMqConsumer(RabbitMqConnector connection)
        {
            _connection = connection;
        }

        public void Dispose()
        {
            _connection.Dispose();
        }

        public void Ack(ulong deliveryTag)
        {
            _connection.Channel.BasicAck(deliveryTag, false);
        }

        public void Nack(ulong deliveryTag)
        {
            _connection.Channel.BasicNack(deliveryTag, false, true);
        }

        public BasicGetResult Receive()
        {
            return _connection.Channel.BasicGet(_connection.QueueName, false);
        }
    }

    internal class RabbitMqProducer : IRabbitMqProducer
    {
        private readonly RabbitMqConnector _connection;

        public RabbitMqProducer(RabbitMqConnector connection)
        {
            _connection = connection;
        }

        public void Dispose()
        {
            _connection.Dispose();
        }

        public void Send(byte[] message)
        {
            var basicProperties = _connection.Channel.CreateBasicProperties();
            basicProperties.MessageId = Guid.NewGuid().ToString();
            basicProperties.DeliveryMode = 2;   // persistent

            _connection.Channel.BasicPublish(string.Empty, _connection.QueueName, true, basicProperties, message);

            _connection.Channel.WaitForConfirmsOrDie(TimeSpan.FromSeconds(10));
        }
    }

    internal class RabbitMqConnector : IDisposable
    {
        public readonly string QueueName;

        private readonly RabbitMqStreamProviderOptions _options;
        private readonly Logger _logger;
        private IConnection _connection;
        private IModel _channel;

        public IModel Channel
        {
            get
            {
                EnsureConnection();
                return _channel;
            }
        }

        public RabbitMqConnector(RabbitMqStreamProviderOptions options, QueueId queueId, Logger logger)
        {
            _options = options;
            QueueName = queueId.ToString();
            _logger = logger;
        }

        private void EnsureConnection()
        {
            if (_connection?.IsOpen != true)
            {
                _logger.Verbose("Opening a new RMQ connection...");
                var factory = new ConnectionFactory
                {
                    HostName = _options.HostName,
                    VirtualHost = _options.VirtualHost,
                    Port = _options.Port,
                    UserName = _options.UserName,
                    Password = _options.Password,
                    UseBackgroundThreadsForIO = false,
                    AutomaticRecoveryEnabled = true,
                    NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
                };

                _connection = factory.CreateConnection();
                _logger.Verbose("Connection created.");
                _connection.ConnectionShutdown += OnConnectionShutdown;
                _connection.ConnectionBlocked += OnConnectionBlocked;
                _connection.ConnectionUnblocked += OnConnectionUnblocked;
            }

            if (_channel?.IsOpen != true)
            {
                _logger.Verbose("Creating a model.");
                _channel = _connection.CreateModel();
                _channel.QueueDeclare(QueueName, durable: true, exclusive: false, autoDelete: false, arguments: null);
                _channel.ConfirmSelect();   // manual (N)ACK
                _logger.Verbose("Model created.");
            }
        }

        public void Dispose()
        {
            try
            {
                if (_channel?.IsClosed == false)
                {
                    _channel.Close();
                }
                _connection?.Close();
            }
            catch (Exception ex)
            {
                _logger.Error(0, "Error during RMQ connection disposal.", ex);
            }
        }

        private void OnConnectionShutdown(object connection, ShutdownEventArgs reason)
        {
            _logger.Error(0, $"Connection was shut down: [{reason.ReplyText}]");
        }

        private void OnConnectionBlocked(object connection, ConnectionBlockedEventArgs reason)
        {
            _logger.Error(0, $"Connection is blocked: [{reason.Reason}]");
        }

        private void OnConnectionUnblocked(object connection, EventArgs args)
        {
            _logger.Error(0, "Connection is not blocked any more.");
        }
    }
}