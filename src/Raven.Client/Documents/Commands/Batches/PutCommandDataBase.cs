﻿using System;
using Raven.Client.Documents.Conventions;
using Raven.Client.Documents.Session;
using Sparrow.Json;
using Sparrow.Json.Parsing;

namespace Raven.Client.Documents.Commands.Batches
{
    internal sealed class PutCommandDataWithBlittableJson : PutCommandDataBase<BlittableJsonReaderObject>
    {
        public PutCommandDataWithBlittableJson(string id, string changeVector, string originalChangeVector, BlittableJsonReaderObject document)
            : base(id, changeVector, originalChangeVector, document)
        {
        }
        
        public PutCommandDataWithBlittableJson(string id, string changeVector, string originalChangeVector, BlittableJsonReaderObject document, ForceRevisionStrategy strategy)
            : base(id, changeVector, originalChangeVector, document, strategy)
        {
        }

        public override void OnBeforeSaveChanges(InMemoryDocumentSessionOperations session)
        {
        }
    }

    public sealed class PutCommandData : PutCommandDataBase<DynamicJsonValue>
    {
        public PutCommandData(string id, string changeVector,  DynamicJsonValue document)
            :base(id, changeVector, changeVector, document)
        {
            
        }
        public PutCommandData(string id, string changeVector, string originalChangeVector, DynamicJsonValue document)
            : base(id, changeVector, originalChangeVector, document)
        {
        }

        public PutCommandData(string id, string changeVector, DynamicJsonValue document, ForceRevisionStrategy strategy)
            :this(id, changeVector, changeVector, document, strategy)
        {
        }
        
        public PutCommandData(string id, string changeVector, string originalChangeVector, DynamicJsonValue document, ForceRevisionStrategy strategy)
            : base(id, changeVector, originalChangeVector, document, strategy)
        {
        }

        public override void OnBeforeSaveChanges(InMemoryDocumentSessionOperations session)
        {
        }
    }

    public abstract class PutCommandDataBase<T> : ICommandData
    {
        protected PutCommandDataBase(string id, string changeVector, string originalChangeVector, T document, ForceRevisionStrategy strategy = ForceRevisionStrategy.None)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            Id = id;
            ChangeVector = changeVector;
            OriginalChangeVector = originalChangeVector;
            Document = document;
            ForceRevisionCreationStrategy = strategy;
        }

        public string Id { get; }
        public string Name { get; } = null;
        public string ChangeVector { get; }
        
        public string OriginalChangeVector { get; }
        public T Document { get; }
        public CommandType Type { get; } = CommandType.PUT;
        public ForceRevisionStrategy ForceRevisionCreationStrategy { get; }

        public DynamicJsonValue ToJson(DocumentConventions conventions, JsonOperationContext context)
        {
            var json = new DynamicJsonValue
            {
                [nameof(Id)] = Id,
                [nameof(ChangeVector)] = ChangeVector,
                [nameof(Document)] = Document,
                [nameof(Type)] = Type.ToString()
            };
            if (OriginalChangeVector != null)
            {
                json[nameof(OriginalChangeVector)] = OriginalChangeVector;
            }
            
            if (ForceRevisionCreationStrategy != ForceRevisionStrategy.None)
            {
                json[nameof(ForceRevisionCreationStrategy)] = ForceRevisionCreationStrategy;
            }

            return json;
        }

        public abstract void OnBeforeSaveChanges(InMemoryDocumentSessionOperations session);
    }
}
