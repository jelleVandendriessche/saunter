﻿using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Saunter.AsyncApiSchema.v2;

namespace Saunter.Serialization
{
    public class NewtonsoftAsyncApiDocumentSerializer : IAsyncApiDocumentSerializer
    {
        private readonly IOptions<AsyncApiOptions> _options;

        public NewtonsoftAsyncApiDocumentSerializer(IOptions<AsyncApiOptions> options)
        {
            _options = options;
        }

        public string ContentType => "application/json";
        public string Serialize(AsyncApiDocument document)
        {
            return JsonConvert.SerializeObject(document, _options.Value.JsonSchemaGeneratorSettings.ActualSerializerSettings);
        }
    }
}