/// <reference path="../../../../typings/tsd.d.ts"/>
import jsonUtil = require("common/jsonUtil");

class configurationItem {

    static readonly PerDatabaseIndexingConfigurationOptions: Array<string> = [
        "Indexing.Analyzers.Default",
        "Indexing.Analyzers.Exact.Default",
        "Indexing.Analyzers.NGram.MaxGram",
        "Indexing.Analyzers.NGram.MinGram",
        "Indexing.Analyzers.Search.Default",
        "Indexing.Encrypted.TransactionSizeLimitInMb",
        "Indexing.IndexEmptyEntries",
        "Indexing.IndexMissingFieldsAsNull",
        "Indexing.LargeSegmentSizeToMergeInMb",
        "Indexing.ManagedAllocationsBatchSizeLimitInMb",
        "Indexing.MapBatchSize",
        "Indexing.MapTimeoutAfterEtagReachedInMin",
        "Indexing.MapTimeoutInSec",
        "Indexing.MaximumSizePerSegmentInMb",
        "Indexing.MaxStepsForScript",
        "Indexing.MaxTimeForDocumentTransactionToRemainOpenInSec",
        "Indexing.MaxTimeForMergesToKeepRunningInSec",
        "Indexing.MergeFactor",
        "Indexing.Metrics.Enabled",
        "Indexing.MinNumberOfMapAttemptsAfterWhichBatchWillBeCanceledIfRunningLowOnMemory",
        "Indexing.NumberOfConcurrentStoppedBatchesIfRunningLowOnMemory",
        "Indexing.NumberOfLargeSegmentsToMergeInSingleBatch",
        "Indexing.ScratchSpaceLimitInMb",
        "Indexing.Throttling.TimeIntervalInMs",
        "Indexing.TimeSinceLastQueryAfterWhichDeepCleanupCanBeExecutedInMin",
        "Indexing.TransactionSizeLimitInMb"
    ];
    
    key = ko.observable<string>();
    value = ko.observable<string>();

    unknownKey: KnockoutComputed<boolean>;

    validationGroup: KnockoutObservable<any>;
    dirtyFlag: () => DirtyFlag;

    constructor(key: string, value: string) {
        this.key(key);
        this.value(value);

        this.unknownKey = ko.pureComputed(() => {
            const key = this.key();
            if (!key) {
                return false;
            }
            return configurationItem.PerDatabaseIndexingConfigurationOptions.indexOf(key) === -1;
        });
        
        this.initValidation();

        this.dirtyFlag = new ko.DirtyFlag([
            this.key, 
            this.value,
        ], false, jsonUtil.newLineNormalizingHashFunction);
    }

    private initValidation() {
        this.key.extend({
            required: true
        });

        this.value.extend({
            required: true
        });

        this.validationGroup = ko.validatedObservable({
            key: this.key,
            value: this.value
        });
    }

    static empty() {
        return new configurationItem("", "");
    }
}

export = configurationItem;
