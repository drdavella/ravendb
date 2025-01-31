using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using Sparrow;
using Sparrow.Server;
using Voron.Data.Tables;
using Voron.Exceptions;
using Voron.Impl;
using Constants = Voron.Global.Constants;

namespace Voron.Data.RawData
{
    /// <summary>
    ///     Handles small values (lt 2Kb) by packing them into pages
    ///     It will allocate 512 pages (2MB in using 4KB pages) and work with them.
    ///     It can grow up to 2,000 pages (7.8 MB in size using 4KB pages), the section size
    ///     is dependent on the size of the database file.
    ///     All attempts are made to reduce the number of times that we need to move data, even
    ///     at the cost of fragmentation.
    /// </summary>
    public sealed unsafe class ActiveRawDataSmallSection(Transaction tx, long pageNumber) : RawDataSection(tx.LowLevelTransaction, pageNumber)
    {
        /// <summary>
        ///     Try allocating some space in the section, defrag if needed (including moving other valid entries)
        ///     Once a section returned false for try allocation, it should be retired as an actively allocating
        ///     section, and a new one will be generated for new values.
        /// </summary>
        public bool TryAllocate(int size, out long id)
        {
            var allocatedSize = (short)size;
            size += sizeof(RawDataEntrySizes);

            // we need to have the size value here, so we add that
            if (allocatedSize <= 0)
                throw new ArgumentException($"Size must be greater than zero, but was {allocatedSize}");

            if (size > MaxItemSize || size > short.MaxValue)
                throw new ArgumentException($"Cannot allocate an item of {size} bytes in a small data section. Maximum is: {MaxItemSize}");

            //  start reading from the last used page, to skip full pages
            for (var i = _sectionHeader->LastUsedPage; i < _sectionHeader->NumberOfPages; i++)
            {
                if (AvailableSpace[i] < size)
                    continue;

                var pageHeader = PageHeaderFor(_llt, _sectionHeader->PageNumber + i + 1);
                if (pageHeader->NextAllocation + size > Constants.Storage.PageSize)
                    continue;

                // best case, we have enough space, and we don't need to defrag
                pageHeader = ModifyPage(pageHeader);
                id = (pageHeader->PageNumber) * Constants.Storage.PageSize + pageHeader->NextAllocation;
                var sizes = (RawDataEntrySizes*)((byte*)pageHeader + pageHeader->NextAllocation);
                sizes->AllocatedSize = allocatedSize;
                sizes->UsedSize = 0;
                pageHeader->NextAllocation += (ushort)size;
                pageHeader->NumberOfEntries++;
                EnsureHeaderModified();
                AvailableSpace[i] -= (ushort)size;
                _sectionHeader->NumberOfEntries++;
                _sectionHeader->LastUsedPage = i;
                _sectionHeader->AllocatedSize += size;
                return true;
            }

            // we don't have any pages that are free enough, we need to check if we 
            // need to fragment, so we will scan from the start, see if we have anything
            // worth doing, and defrag if needed
            for (ushort i = 0; i < _sectionHeader->NumberOfPages; i++)
            {
                if (AvailableSpace[i] < size)
                    continue;
                // we have space, but we need to defrag
                var pageHeader = PageHeaderFor(_llt, _sectionHeader->PageNumber + i + 1);
                pageHeader = DefragPage(pageHeader);

                id = (pageHeader->PageNumber) * Constants.Storage.PageSize + pageHeader->NextAllocation;
                ((short*)((byte*)pageHeader + pageHeader->NextAllocation))[0] = allocatedSize;
                pageHeader->NextAllocation += (ushort)size;
                pageHeader->NumberOfEntries++;
                EnsureHeaderModified();
                _sectionHeader->NumberOfEntries++;
                _sectionHeader->LastUsedPage = i;
                _sectionHeader->AllocatedSize += size;
                AvailableSpace[i] = (ushort)(Constants.Storage.PageSize - pageHeader->NextAllocation);

                return true;
            }

            // we don't have space, caller need to allocate new small section?
            id = -1;
            return false;
        }

        public string DebugDump(RawDataSmallPageHeader* pageHeader)
        {
            var sb =
                new StringBuilder(
                    $"Page {pageHeader->PageNumber}, {pageHeader->NumberOfEntries} entries, next allocation: {pageHeader->NextAllocation}")
                    .AppendLine();

            for (int i = sizeof(RawDataSmallPageHeader); i < pageHeader->NextAllocation;)
            {
                var oldSize = (RawDataEntrySizes*)((byte*)pageHeader + i);
                sb.Append($"{i} - {oldSize->AllocatedSize} / {oldSize->UsedSize} - ");

                if (oldSize->IsFreed == false)
                {
                    var tvr = new TableValueReader((byte*)pageHeader + i + sizeof(RawDataEntrySizes),
                        oldSize->UsedSize);

                    sb.Append(tvr.Count);
                }

                sb.AppendLine();
                i += oldSize->AllocatedSize + sizeof(RawDataEntrySizes);
            }

            return sb.ToString();

        }

        private RawDataSmallPageHeader* DefragPage(RawDataSmallPageHeader* pageHeader)
        {
            pageHeader = ModifyPage(pageHeader);

            if (pageHeader->NumberOfEntries == 0)
            {
                pageHeader->NextAllocation = (ushort)sizeof(RawDataSmallPageHeader);
                Memory.Set((byte*)pageHeader + pageHeader->NextAllocation, 0,
                    Constants.Storage.PageSize - pageHeader->NextAllocation);

                return pageHeader;
            }

            using (_llt.Allocator.Allocate(Constants.Storage.PageSize, out ByteString tmp))
            {
                tmp.Clear();
                var maxUsedPos = pageHeader->NextAllocation;
                var tmpPtr = tmp.Ptr;
                Debug.Assert(_llt.IsDirty(pageHeader->PageNumber));
                Memory.Copy(tmpPtr, (byte*)pageHeader, Constants.Storage.PageSize);

                pageHeader->NextAllocation = (ushort)sizeof(RawDataSmallPageHeader);
                Memory.Set((byte*)pageHeader + pageHeader->NextAllocation, 0,
                    Constants.Storage.PageSize - pageHeader->NextAllocation);

                pageHeader->NumberOfEntries = 0;
                var pos = pageHeader->NextAllocation;

                while (pos < maxUsedPos)
                {
                    var oldSize = (RawDataEntrySizes*)(tmpPtr + pos);

                    if (oldSize->AllocatedSize <= 0)
                        VoronUnrecoverableErrorException.Raise(_llt, $"Allocated size cannot be zero or negative, but was {oldSize->AllocatedSize} in page {pageHeader->PageNumber}");

                    if (oldSize->IsFreed)
                    {
                        pos += (ushort)(oldSize->AllocatedSize + sizeof(RawDataEntrySizes));
                        continue; // this was freed
                    }

                    var prevId = (pageHeader->PageNumber) * Constants.Storage.PageSize + pos;
                    var newId = (pageHeader->PageNumber) * Constants.Storage.PageSize + pageHeader->NextAllocation;
                    byte* entryPos = tmpPtr + pos + sizeof(RawDataEntrySizes);
                    if (prevId != newId)
                    {
                        var size = oldSize->UsedSize;
                        if (oldSize->IsCompressed)
                        {
                            using var __ = Table.DecompressValue(tx, entryPos, size, out var buffer);
                            OnDataMoved(prevId, newId, buffer.Ptr, buffer.Length, compressed: true);
                        }
                        else
                        {
                            OnDataMoved(prevId, newId, entryPos, oldSize->UsedSize, compressed: false);
                        }
                    }

                    var newSize = (RawDataEntrySizes*)(((byte*)pageHeader) + pageHeader->NextAllocation);
                    newSize->AllocatedSize = oldSize->AllocatedSize;
                    newSize->UsedSize_Buffer = oldSize->UsedSize_Buffer;
                    pageHeader->NextAllocation += (ushort)sizeof(RawDataEntrySizes);
                    pageHeader->NumberOfEntries++;
                    Memory.Copy(((byte*)pageHeader) + pageHeader->NextAllocation, entryPos,
                        oldSize->UsedSize);

                    pageHeader->NextAllocation += (ushort)oldSize->AllocatedSize;
                    pos += (ushort)(oldSize->AllocatedSize + sizeof(RawDataEntrySizes));
                }
            }
            return pageHeader;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static ActiveRawDataSmallSection Create(Transaction tx, string owner, byte tableType, ushort? sizeInPages = null)
        {
            Slice.From(tx.Allocator, owner, ByteStringType.Immutable, out Slice ownerSlice);
            return Create(tx, ownerSlice, tableType, sizeInPages);
        }

        public static ActiveRawDataSmallSection Create(Transaction transaction, Slice owner, byte tableType, ushort? sizeInPages)
        {
            var llt = transaction.LowLevelTransaction;
            var dbPagesInSmallSection = GetNumberOfPagesInSmallSection(llt);
            var numberOfPagesInSmallSection = Math.Min(sizeInPages ?? dbPagesInSmallSection, dbPagesInSmallSection);
            Debug.Assert((numberOfPagesInSmallSection * 2) + ReservedHeaderSpace <= Constants.Storage.PageSize);

            var sectionStart = llt.AllocatePage(numberOfPagesInSmallSection);
            numberOfPagesInSmallSection--; // we take one page for the active section header
            Debug.Assert(numberOfPagesInSmallSection > 0);
            llt.BreakLargeAllocationToSeparatePages(sectionStart.PageNumber);

            var sectionHeader = (RawDataSmallSectionPageHeader*)sectionStart.Pointer;
            sectionHeader->RawDataFlags = RawDataPageFlags.Header;
            sectionHeader->Flags = PageFlags.RawData | PageFlags.Single;
            sectionHeader->NumberOfEntries = 0;
            sectionHeader->NumberOfPages = numberOfPagesInSmallSection;
            sectionHeader->LastUsedPage = 0;
            sectionHeader->SectionOwnerHash = Hashing.XXHash64.Calculate(owner.Content.Ptr, (ulong)owner.Content.Length);
            sectionHeader->TableType = tableType;

            var availableSpace = (ushort*)((byte*)sectionHeader + ReservedHeaderSpace);

            for (ushort i = 0; i < numberOfPagesInSmallSection; i++)
            {
                var pageHeader = (RawDataSmallPageHeader*)(sectionStart.Pointer + (i + 1) * Constants.Storage.PageSize);
                Debug.Assert(pageHeader->PageNumber == sectionStart.PageNumber + i + 1);
                pageHeader->NumberOfEntries = 0;
                pageHeader->PageNumberInSection = i;
                pageHeader->RawDataFlags = RawDataPageFlags.Small;
                pageHeader->Flags = PageFlags.RawData | PageFlags.Single;
                pageHeader->NextAllocation = (ushort)sizeof(RawDataSmallPageHeader);
                pageHeader->SectionOwnerHash = sectionHeader->SectionOwnerHash;
                pageHeader->TableType = tableType;
                availableSpace[i] = (ushort)(Constants.Storage.PageSize - sizeof(RawDataSmallPageHeader));
            }

            return new ActiveRawDataSmallSection(transaction, sectionStart.PageNumber);
        }

        /// <summary>
        /// We choose the length of the section based on the overall db size.
        /// The idea is that we want to avoid pre-allocating a lot of data all at once when we are small, which
        /// can blow up our file size
        /// </summary>
        private static ushort GetNumberOfPagesInSmallSection(LowLevelTransaction tx)
        {
            return tx.DataPager.NumberOfAllocatedPages switch
            {
                // all sizes are with 8 Kb page size
                // 256 MB 
                > 1024 * 32 => (Constants.Storage.PageSize - ReservedHeaderSpace) / 2,
                // 64 MB
                > 1024 * 16 => 1024,
                // 32 MB
                > 1024 * 8 => 512,
                // 16 MB
                > 1024 * 4 => 128,
                // we are less than 16 MB
                // 512 KB
                _ => 32
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsOwned(long id)
        {
            var posInPage = (int)(id % Constants.Storage.PageSize);
            var pageNumberInSection = (id - posInPage) / Constants.Storage.PageSize;

            // same section, obviously owned
            if (pageNumberInSection > _sectionHeader->PageNumber &&
                pageNumberInSection <= _sectionHeader->PageNumber + _sectionHeader->NumberOfPages)
                return true;

            var pageHeader = PageHeaderFor(_llt, pageNumberInSection);
            var sectionPageNumber = pageHeader->PageNumber - pageHeader->PageNumberInSection - 1;
            var idSectionHeader = (RawDataSmallSectionPageHeader*)_llt.GetPage(sectionPageNumber).Pointer;

            return idSectionHeader->SectionOwnerHash == _sectionHeader->SectionOwnerHash;
        }
    }
}
