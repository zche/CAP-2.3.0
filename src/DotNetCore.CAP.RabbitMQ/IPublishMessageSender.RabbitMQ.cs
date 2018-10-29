// Copyright (c) .NET Core Community. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using DotNetCore.CAP.Internal;
using DotNetCore.CAP.Processor.States;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace DotNetCore.CAP.RabbitMQ
{
    internal sealed class RabbitMQPublishMessageSender : BasePublishMessageSender
    {
        private readonly IConnectionChannelPool _connectionChannelPool;
        private readonly ILogger _logger;
        private readonly string _exchange;
        private readonly RabbitMQOptions _rabbitMQOptions;

        public RabbitMQPublishMessageSender(ILogger<RabbitMQPublishMessageSender> logger, CapOptions options,
            IStorageConnection connection, IConnectionChannelPool connectionChannelPool, IStateChanger stateChanger,, RabbitMQOptions mqOptions)
            : base(logger, options, connection, stateChanger)
        {
            _logger = logger;
            _connectionChannelPool = connectionChannelPool;
            _exchange = _connectionChannelPool.Exchange;
            ServersAddress = _connectionChannelPool.HostAddress;
            _rabbitMQOptions = mqOptions;
        }

        public override Task<OperateResult> PublishAsync(string keyName, string content)
        {
            #region 个人添加的
            var items = keyName.Split(new char[] { '-' }, StringSplitOptions.RemoveEmptyEntries);
            if (items.Length != 2)
            {
                throw new Exception("传入的keyName必须包含routingKey和queueName，并且以短杠隔开！");
            }
            var routingKey = items[0];
            var queueName = items[1];
            #endregion
            var channel = _connectionChannelPool.Rent();
            try
            {
                var body = Encoding.UTF8.GetBytes(content);
                channel.ExchangeDeclare(_exchange, RabbitMQOptions.ExchangeType, true);

                #region 个人添加的
                var arguments = new Dictionary<string, object>
            {
                {"x-message-ttl", _rabbitMQOptions.QueueMessageExpires}
            };
                channel.QueueDeclare(queueName, true, false, false, arguments);
                channel.QueueBind(queueName, _exchange, routingKey);

                #endregion

                channel.BasicPublish(_exchange, routingKey, null, body);

                _logger.LogDebug($"RabbitMQ topic message [{routingKey}] has been published.");

                return Task.FromResult(OperateResult.Success);
            }
            catch (Exception ex)
            {
                var wapperEx = new PublisherSentFailedException(ex.Message, ex);
                var errors = new OperateError
                {
                    Code = ex.HResult.ToString(),
                    Description = ex.Message
                };

                return Task.FromResult(OperateResult.Failed(wapperEx, errors));
            }
            finally
            {
                var returned = _connectionChannelPool.Return(channel);
                if (!returned)
                {
                    channel.Dispose();
                }
            }
        }
    }
}