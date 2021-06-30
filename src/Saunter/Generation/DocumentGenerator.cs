using Namotion.Reflection;
using NJsonSchema.Generation;
using Saunter.AsyncApiSchema.v2;
using Saunter.Attributes;
using Saunter.Generation.Filters;
using Saunter.Generation.SchemaGeneration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NJsonSchema;
using Saunter.AsyncApiSchema.v2.Bindings;
using Saunter.Utils;

namespace Saunter.Generation
{
    public class DocumentGenerator : IDocumentGenerator
    {
        public DocumentGenerator()
        {
        }

        public AsyncApiSchema.v2.AsyncApiDocument GenerateDocument(TypeInfo[] asyncApiTypes, AsyncApiOptions options)
        {
            // todo: clone the global document so each call generates a new document
            var asyncApiSchema = new AsyncApiDocument();
            asyncApiSchema.Info = options.AsyncApi.Info;
            asyncApiSchema.Id = options.AsyncApi.Id;
            asyncApiSchema.DefaultContentType = options.AsyncApi.DefaultContentType;
            asyncApiSchema.Channels = options.AsyncApi.Channels.ToDictionary(p => p.Key, p => p.Value);
            asyncApiSchema.Servers = options.AsyncApi.Servers.ToDictionary(p => p.Key, p => p.Value);
            foreach (var tag in options.AsyncApi.Tags)
            {
                asyncApiSchema.Tags.Add(tag);
            }
            asyncApiSchema.ExternalDocs = options.AsyncApi.ExternalDocs;
            asyncApiSchema.Components.Schemas = options.AsyncApi.Components.Schemas.ToDictionary(p => p.Key, p => p.Value);
            asyncApiSchema.Components.Messages = options.AsyncApi.Components.Messages.ToDictionary(p => p.Key, p => p.Value);
            asyncApiSchema.Components.SecuritySchemes = options.AsyncApi.Components.SecuritySchemes.ToDictionary(p => p.Key, p => p.Value);
            asyncApiSchema.Components.Parameters = options.AsyncApi.Components.Parameters.ToDictionary(p => p.Key, p => p.Value);
            asyncApiSchema.Components.CorrelationIds = options.AsyncApi.Components.CorrelationIds.ToDictionary(p => p.Key, p => p.Value);
            asyncApiSchema.Components.ServerBindings = options.AsyncApi.Components.ServerBindings.ToDictionary(p => p.Key, p => p.Value);
            asyncApiSchema.Components.ChannelBindings = options.AsyncApi.Components.ChannelBindings.ToDictionary(p => p.Key, p => p.Value);
            asyncApiSchema.Components.OperationBindings = options.AsyncApi.Components.OperationBindings.ToDictionary(p => p.Key, p => p.Value);
            asyncApiSchema.Components.MessageBindings = options.AsyncApi.Components.MessageBindings.ToDictionary(p => p.Key, p => p.Value);
            asyncApiSchema.Components.OperationTraits = options.AsyncApi.Components.OperationTraits.ToDictionary(p => p.Key, p => p.Value);
            asyncApiSchema.Components.MessageTraits = options.AsyncApi.Components.MessageTraits.ToDictionary(p => p.Key, p => p.Value);

            var schemaResolver = new AsyncApiSchemaResolver(asyncApiSchema, options.JsonSchemaGeneratorSettings);

            var generator = new JsonSchemaGenerator(options.JsonSchemaGeneratorSettings);
            asyncApiSchema.Channels = GenerateChannels(asyncApiTypes, schemaResolver, options, generator);
            
            var filterContext = new DocumentFilterContext(asyncApiTypes, schemaResolver, generator);
            foreach (var filter in options.DocumentFilters)
            {
                filter.Apply(asyncApiSchema, filterContext);
            }

            return asyncApiSchema;
        }

        /// <summary>
        /// Generate the Channels section of an AsyncApi schema.
        /// </summary>
        private IDictionary<string, ChannelItem> GenerateChannels(TypeInfo[] asyncApiTypes, AsyncApiSchemaResolver schemaResolver, AsyncApiOptions options, JsonSchemaGenerator jsonSchemaGenerator)
        {
            var channels = new Dictionary<string, ChannelItem>();
            
            channels.AddRange(GenerateChannelsFromMethods(asyncApiTypes, schemaResolver, options, jsonSchemaGenerator));
            channels.AddRange(GenerateChannelsFromClasses(asyncApiTypes, schemaResolver, options, jsonSchemaGenerator));
            return channels;
        }

        /// <summary>
        /// Generate the Channels section of the AsyncApi schema from the
        /// <see cref="ChannelAttribute"/> on methods.
        /// </summary>
        private IDictionary<string, ChannelItem> GenerateChannelsFromMethods(IEnumerable<TypeInfo> asyncApiTypes, AsyncApiSchemaResolver schemaResolver, AsyncApiOptions options, JsonSchemaGenerator jsonSchemaGenerator)
        {
            var channels = new Dictionary<string, ChannelItem>();

            var methodsWithChannelAttribute = asyncApiTypes
                .SelectMany(type => type.DeclaredMethods)
                .Select(method => new
                {
                    Channel = method.GetCustomAttribute<ChannelAttribute>(),
                    Method = method,
                })
                .Where(mc => mc.Channel != null);

            foreach (var mc in methodsWithChannelAttribute)
            {
                var channelItem = new ChannelItem
                {
                    Description = mc.Channel.Description,
                    Parameters = GetChannelParametersFromAttributes(mc.Method, schemaResolver, jsonSchemaGenerator),
                    Publish = GenerateOperationFromMethod(mc.Method, schemaResolver, OperationType.Publish, options, jsonSchemaGenerator),
                    Subscribe = GenerateOperationFromMethod(mc.Method, schemaResolver, OperationType.Subscribe, options, jsonSchemaGenerator),
                    Bindings = mc.Channel.BindingsRef != null ? new ChannelBindingsReference(mc.Channel.BindingsRef) : null,
                }; 
                channels.Add(mc.Channel.Name, channelItem);
                
                var context = new ChannelItemFilterContext(mc.Method, schemaResolver, jsonSchemaGenerator, mc.Channel);
                foreach (var filter in options.ChannelItemFilters)
                {
                    filter.Apply(channelItem, context);
                }
            }

            return channels;
        }

        /// <summary>
        /// Generate the Channels section of the AsyncApi schema from the
        /// <see cref="ChannelAttribute"/> on classes.
        /// </summary>
        private IDictionary<string, ChannelItem> GenerateChannelsFromClasses(IEnumerable<TypeInfo> asyncApiTypes, AsyncApiSchemaResolver schemaResolver, AsyncApiOptions options, JsonSchemaGenerator jsonSchemaGenerator)
        {
            var channels = new Dictionary<string, ChannelItem>();

            var classesWithChannelAttribute = asyncApiTypes
                .Select(type => new
                {
                    Channel = type.GetCustomAttribute<ChannelAttribute>(),
                    Type = type,
                })
                .Where(cc => cc.Channel != null);

            foreach (var cc in classesWithChannelAttribute)
            {
                var channelItem = new ChannelItem
                {
                    Description = cc.Channel.Description,
                    Parameters = GetChannelParametersFromAttributes(cc.Type, schemaResolver, jsonSchemaGenerator),
                    Publish = GenerateOperationFromClass(cc.Type, schemaResolver, OperationType.Publish, jsonSchemaGenerator),
                    Subscribe = GenerateOperationFromClass(cc.Type, schemaResolver, OperationType.Subscribe, jsonSchemaGenerator),
                    Bindings = cc.Channel.BindingsRef != null ? new ChannelBindingsReference(cc.Channel.BindingsRef) : null,
                };
                
                channels.AddOrAppend(cc.Channel.Name, channelItem);
                
                var context = new ChannelItemFilterContext(cc.Type, schemaResolver, jsonSchemaGenerator, cc.Channel);
                foreach (var filter in options.ChannelItemFilters)
                {
                    filter.Apply(channelItem, context);
                }
            }

            return channels;
        }

        /// <summary>
        /// Generate the an operation of an AsyncApi Channel for the given method.
        /// </summary>
        private Operation GenerateOperationFromMethod(MethodInfo method, AsyncApiSchemaResolver schemaResolver, OperationType operationType, AsyncApiOptions options, JsonSchemaGenerator jsonSchemaGenerator)
        {
            var operationAttribute = GetOperationAttribute(method, operationType);
            if (operationAttribute == null)
            {
                return null;
            }

            IEnumerable<MessageAttribute> messageAttributes = method.GetCustomAttributes<MessageAttribute>();
            var message = messageAttributes.Any()
                ? GenerateMessageFromAttributes(messageAttributes, schemaResolver, jsonSchemaGenerator)
                : GenerateMessageFromType(operationAttribute.MessagePayloadType, schemaResolver, jsonSchemaGenerator);
            
            var operation = new Operation
            {
                OperationId = operationAttribute.OperationId ?? method.Name,
                Summary = operationAttribute.Summary ?? method.GetXmlDocsSummary(),
                Description = operationAttribute.Description ?? (method.GetXmlDocsRemarks() != "" ? method.GetXmlDocsRemarks() : null),
                Message = message,
                Bindings = operationAttribute.BindingsRef != null ? new OperationBindingsReference(operationAttribute.BindingsRef) : null,
            };

            var filterContext = new OperationFilterContext(method, schemaResolver, jsonSchemaGenerator, operationAttribute);
            foreach (var filter in options.OperationFilters)
            {
                filter.Apply(operation, filterContext);
            }

            return operation;
        }

        /// <summary>
        /// Generate the an operation of an AsyncApi Channel for the given class.
        /// </summary>
        private Operation GenerateOperationFromClass(TypeInfo type, AsyncApiSchemaResolver schemaResolver, OperationType operationType, JsonSchemaGenerator jsonSchemaGenerator)
        {
            var operationAttribute = GetOperationAttribute(type, operationType);
            if (operationAttribute == null)
            {
                return null;
            }

            var messages = new Messages();
            var operation = new Operation
            {
                OperationId = operationAttribute.OperationId ?? type.Name,
                Summary = operationAttribute.Summary ?? type.GetXmlDocsSummary(),
                Description = operationAttribute.Description ?? (type.GetXmlDocsRemarks() != "" ? type.GetXmlDocsRemarks() : null),
                Message = messages,
                Bindings = operationAttribute.BindingsRef != null ? new OperationBindingsReference(operationAttribute.BindingsRef) : null,
            };

            var methodsWithMessageAttribute = type.DeclaredMethods
                .Select(method => new
                {
                    MessageAttributes = method.GetCustomAttributes<MessageAttribute>(),
                    Method = method,
                })
                .Where(mm => mm.MessageAttributes.Any());

            foreach (MessageAttribute messageAttribute in methodsWithMessageAttribute.SelectMany(x => x.MessageAttributes))
            {
                var message = GenerateMessageFromAttribute(messageAttribute, schemaResolver, jsonSchemaGenerator);
                if (message != null)
                {
                    messages.OneOf.Add(message);
                }
            }

            if (messages.OneOf.Count == 1)
            {
                operation.Message = messages.OneOf.First();
            }

            return operation;
        }

        private static OperationAttribute GetOperationAttribute(MemberInfo typeOrMethod, OperationType operationType)
        {
            switch (operationType)
            {
                case OperationType.Publish:
                    var publishOperationAttribute = typeOrMethod.GetCustomAttribute<PublishOperationAttribute>();
                    return (OperationAttribute) publishOperationAttribute;

                case OperationType.Subscribe:
                    var subscribeOperationAttribute = typeOrMethod.GetCustomAttribute<SubscribeOperationAttribute>();
                    return (OperationAttribute) subscribeOperationAttribute;

                default:
                    return null;
            }
        }

        private IMessage GenerateMessageFromAttributes(IEnumerable<MessageAttribute> messageAttributes, AsyncApiSchemaResolver schemaResolver, JsonSchemaGenerator jsonSchemaGenerator)
        {
            if (messageAttributes.Count() == 1)
            {
                return GenerateMessageFromAttribute(messageAttributes.First(), schemaResolver, jsonSchemaGenerator);
            }

            var messages = new Messages();
            foreach (MessageAttribute messageAttribute in messageAttributes)
            {
                var message = GenerateMessageFromAttribute(messageAttribute, schemaResolver, jsonSchemaGenerator);
                if (message != null)
                {
                    messages.OneOf.Add(message);
                }
            }

            if (messages.OneOf.Count == 1)
            {
                return messages.OneOf.First();
            }

            return messages;
        }

        private IMessage GenerateMessageFromAttribute(MessageAttribute messageAttribute, AsyncApiSchemaResolver schemaResolver, JsonSchemaGenerator jsonSchemaGenerator)
        {
            if (messageAttribute?.PayloadType == null)
            {
                return null;
            }

            var message = new Message
            {
                Payload = jsonSchemaGenerator.Generate(messageAttribute.PayloadType, schemaResolver),
                Title = messageAttribute.Title,
                Summary = messageAttribute.Summary,
                Description = messageAttribute.Description,
                Bindings = messageAttribute.BindingsRef != null ? new MessageBindingsReference(messageAttribute.BindingsRef) : null,
            };
            message.Name = messageAttribute.Name ?? message.Payload.ActualSchema.Id;

            return schemaResolver.GetMessageOrReference(message);
        }
        

        private IMessage GenerateMessageFromType(Type payloadType, AsyncApiSchemaResolver schemaResolver, JsonSchemaGenerator jsonSchemaGenerator)
        {
            if (payloadType == null)
            {
                return null;
            }

            var message = new Message
            {
                Payload = jsonSchemaGenerator.Generate(payloadType, schemaResolver),
            };
            message.Name = message.Payload.Id;

            return schemaResolver.GetMessageOrReference(message);
        }

        private IDictionary<string,IParameter> GetChannelParametersFromAttributes(MemberInfo memberInfo, AsyncApiSchemaResolver schemaResolver, JsonSchemaGenerator jsonSchemaGenerator)
        {
            IEnumerable<ChannelParameterAttribute> attributes = memberInfo.GetCustomAttributes<ChannelParameterAttribute>();
            var parameters = new Dictionary<string, IParameter>();
            if (attributes.Any())
            {
                foreach (ChannelParameterAttribute attribute in attributes)
                {
                    var parameter = schemaResolver.GetParameterOrReference(new Parameter
                    {
                        Description = attribute.Description,
                        Name = attribute.Name,
                        Schema = jsonSchemaGenerator.Generate(attribute.Type, schemaResolver),
                        Location = attribute.Location,
                    });
                    
                    parameters.Add(attribute.Name, parameter);
                }
            }

            return parameters;
        }
    }
}