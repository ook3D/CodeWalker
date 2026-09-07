using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeWalker.GameFiles
{
    public class ResourceBuilder
    {
        protected const int RESOURCE_IDENT = 0x37435352;
        protected const int BASE_SIZE = 0x2000;
        private const int SKIP_SIZE = 16;//512;//256;//64;
        private const int ALIGN_SIZE = 16;//512;//64;

        public class ResourceBuilderBlock
        {
            public IResourceBlock Block;
            public long Length;

            public ResourceBuilderBlock(IResourceBlock block)
            {
                Block = block;
                Length = block?.BlockLength ?? 0;
            }
        }
        public class ResourceBuilderBlockSet
        {
            public bool IsSystemSet = false;
            public ResourceBuilderBlock? RootBlock = null;
            public LinkedList<ResourceBuilderBlock> BlockList = new();
            public Dictionary<ResourceBuilderBlock, LinkedListNode<ResourceBuilderBlock>> BlockDict = new Dictionary<ResourceBuilderBlock, LinkedListNode<ResourceBuilderBlock>>();

            public int Count => BlockList.Count;

            public ResourceBuilderBlockSet(IList<IResourceBlock> blocks, bool sys)
            {
                IsSystemSet = sys;
                if (sys && (blocks.Count > 0))
                {
                    RootBlock = new ResourceBuilderBlock(blocks[0]);
                }
                var list = new List<ResourceBuilderBlock>();
                int start = sys ? 1 : 0;
                for (int i = start; i < blocks.Count; i++)
                {
                    var bb = new ResourceBuilderBlock(blocks[i]);
                    list.Add(bb);
                }
                list.Sort((a, b) => b.Length.CompareTo(a.Length));
                foreach (var bb in list)
                {
                    var ln = BlockList.AddLast(bb);
                    BlockDict[bb] = ln;
                }
            }

            public ResourceBuilderBlock? FindBestBlock(long maxSize)
            {
                var n = BlockList.First;
                while ((n != null) && (n.Value.Length > maxSize))
                {
                    n = n.Next;
                }
                return n?.Value;
            }

            public ResourceBuilderBlock? TakeBestBlock(long maxSize)
            {
                var r = FindBestBlock(maxSize);
                if (r != null)
                {
                    if (BlockDict.TryGetValue(r, out LinkedListNode<ResourceBuilderBlock>? ln))
                    {
                        BlockList.Remove(ln);
                        BlockDict.Remove(r);
                    }
                }
                return r;
            }

        }

        public static void GetBlocks(IResourceBlock rootBlock, out IList<IResourceBlock> sys, out IList<IResourceBlock> gfx)
        {
            var systemBlocks = new HashSet<IResourceBlock>();
            var graphicBlocks = new HashSet<IResourceBlock>();
            var processed = new HashSet<IResourceBlock>();


            void addBlock(IResourceBlock block)
            {
                if (block is IResourceSystemBlock)
                {
                    if (!systemBlocks.Contains(block)) systemBlocks.Add(block);
                }
                else if(block is IResourceGraphicsBlock)
                {
                    if (!graphicBlocks.Contains(block)) graphicBlocks.Add(block);
                }
            }
            void addChildren(IResourceBlock block)
            {
                if (block is IResourceSystemBlock sblock)
                {
                    var references = sblock.GetReferences();
                    foreach (var reference in references)
                    {
                        if (!processed.Contains(reference))
                        {
                            processed.Add(reference);
                            addBlock(reference);
                            addChildren(reference);
                        }
                    }
                    var parts = sblock.GetParts();
                    foreach (var part in parts)
                    {
                        addChildren(part.Item2);
                    }
                }
            }

            addBlock(rootBlock);
            addChildren(rootBlock);


            sys = new List<IResourceBlock>();
            foreach (var s in systemBlocks)
            {
                sys.Add(s);
            }
            gfx = new List<IResourceBlock>();
            foreach (var s in graphicBlocks)
            {
                gfx.Add(s);
            }
        }

        public static void AssignPositions(IList<IResourceBlock> blocks, uint basePosition, out RpfResourcePageFlags pageFlags, uint maxPageCount)
        {
            if ((blocks.Count > 0) && (blocks[0] is Meta))
            {
                //use naive packing strategy for Meta resources, due to crashes caused by the improved packing
                AssignPositionsForMeta(blocks, basePosition, out pageFlags);
                return;
            }

            var sys = (basePosition == 0x50000000);

            long pad(long p)
            {
                return ((ALIGN_SIZE - (p % ALIGN_SIZE)) % ALIGN_SIZE);
            }

            long largestBlockSize = 0; // find largest structure
            long startPageSize = BASE_SIZE;// 0x2000; // find starting page size
            long totalBlockSize = 0;
            foreach (var block in blocks)
            {
                var blockLength = block.BlockLength;
                totalBlockSize += blockLength;
                totalBlockSize += pad(totalBlockSize);
                if (largestBlockSize < blockLength)
                {
                    largestBlockSize = blockLength;
                }
            }
            while (startPageSize < largestBlockSize)
            {
                startPageSize *= 2;
            }


            pageFlags = new RpfResourcePageFlags();
            var pageSizeMult = 1;

            while (true)
            {
                if (blocks.Count == 0) break;

                var blockset = new ResourceBuilderBlockSet(blocks, sys);
                var rootblock = blockset.RootBlock;
                var currentPosition = 0L;
                var currentPageSize = startPageSize;
                var currentPageStart = 0L;
                var currentPageSpace = startPageSize;
                var currentRemainder = totalBlockSize;
                var pageCount = 1;
                var pageCounts = new uint[9];
                var pageCountIndex = 0;
                var targetPageSize = Math.Max(65536 * pageSizeMult, startPageSize >> (sys ? 5 : 2));
                var minPageSize = Math.Max(512 * pageSizeMult, Math.Min(targetPageSize, startPageSize) >> 4);
                var baseShift = 0u;
                var baseSize = 512;
                while (baseSize < minPageSize)
                {
                    baseShift++;
                    baseSize *= 2;
                    if (baseShift >= 0xF) break;
                }
                var baseSizeMax = baseSize << 8;
                var baseSizeMaxTest = startPageSize;
                while (baseSizeMaxTest < baseSizeMax)
                {
                    pageCountIndex++;
                    baseSizeMaxTest *= 2;
                }
                pageCounts[pageCountIndex] = 1;

                while (true)
                {
                    var isroot = sys && (currentPosition == 0);
                    var block = isroot ? rootblock : blockset.TakeBestBlock(currentPageSpace);
                    var blockLength = block?.Length ?? 0;
                    if (block != null)
                    {
                        //add this block to the current page.
                        block.Block.FilePosition = basePosition + currentPosition;
                        var opos = currentPosition;
                        currentPosition += blockLength;
                        currentPosition += pad(currentPosition);
                        var usedspace = currentPosition - opos;
                        currentPageSpace -= usedspace;
                        currentRemainder -= usedspace;//blockLength;// 

                    }
                    else if (blockset.Count > 0)
                    {
                        //allocate a new page
                        currentPageStart += currentPageSize;
                        currentPosition = currentPageStart;
                        block = blockset.FindBestBlock(long.MaxValue); //just find the biggest block
                        blockLength = block?.Length ?? 0;
                        while (blockLength <= (currentPageSize >> 1))//determine best new page size
                        {
                            if (currentPageSize <= minPageSize) break;
                            if (pageCountIndex >= 8) break;
                            if ((currentPageSize <= targetPageSize) && (currentRemainder >= (currentPageSize - minPageSize))) break;

                            currentPageSize = currentPageSize >> 1;
                            pageCountIndex++;
                        }
                        currentPageSpace = currentPageSize;
                        pageCounts[pageCountIndex]++;
                        pageCount++;
                    }
                    else
                    {
                        break;
                    }
                }


                pageFlags = new RpfResourcePageFlags(pageCounts, baseShift);

                if ((pageCount == pageFlags.Count) && (pageFlags.Size >= currentPosition) && (pageCount <= maxPageCount)) //make sure page counts fit in the flags value
                {
                    break;
                }

                startPageSize *= 2;
                pageSizeMult *= 2;
            }

        }

        public static void AssignPositionsForMeta(IList<IResourceBlock> blocks, uint basePosition, out RpfResourcePageFlags pageFlags)
        {
            // find largest structure
            long largestBlockSize = 0;
            foreach (var block in blocks)
            {
                if (largestBlockSize < block.BlockLength)
                    largestBlockSize = block.BlockLength;
            }

            // find minimum page size
            long currentPageSize = 0x2000;
            while (currentPageSize < largestBlockSize)
                currentPageSize *= 2;

            long currentPageCount;
            long currentPosition;
            while (true)
            {
                currentPageCount = 0;
                currentPosition = 0;

                // reset all positions
                foreach (var block in blocks)
                    block.FilePosition = -1;

                foreach (var block in blocks)
                {
                    if (block.FilePosition != -1)
                        throw new Exception("Block was already assigned a position!");

                    // check if new page is necessary...
                    // if yes, add a new page and align to it
                    long maxSpace = currentPageCount * currentPageSize - currentPosition;
                    if (maxSpace < (block.BlockLength + SKIP_SIZE))
                    {
                        currentPageCount++;
                        currentPosition = currentPageSize * (currentPageCount - 1);
                    }

                    // set position
                    block.FilePosition = basePosition + currentPosition;
                    currentPosition += block.BlockLength; // + SKIP_SIZE; //is padding everywhere really necessary??

                    // align...
                    if ((currentPosition % ALIGN_SIZE) != 0)
                        currentPosition += (ALIGN_SIZE - (currentPosition % ALIGN_SIZE));
                }

                // break if everything fits...
                if (currentPageCount < 128)
                    break;

                currentPageSize *= 2;
            }

            pageFlags = new RpfResourcePageFlags(RpfResourceFileEntry.GetFlagsFromBlocks((uint)currentPageCount, (uint)currentPageSize, 0));

        }


        public static void AssignPositions2(IList<IResourceBlock> blocks, uint basePosition, out RpfResourcePageFlags pageFlags, uint maxPageCount, bool gen9)
        {
            if ((blocks.Count > 0) && (blocks[0] is Meta))//TODO: try remove this?
            {
                //use naive packing strategy for Meta resources, due to crashes caused by the improved packing
                AssignPositionsForMeta(blocks, basePosition, out pageFlags);
                return;
            }

            //find optimal BaseShift value for the smallest block size
            //for small system blocks should be 0, but for large physical blocks can be much bigger
            //also, the largest block needs to fit into the largest page.
            //BaseSize = 0x2000 << BaseShift   (max BaseShift = 0xF)
            //then allocate page counts for the page sizes:
            //allows for 5 page sizes, each double the size of the previous, with max counts 0x7F, 0x3F, 0xF, 3, 1
            //also allows for 4 tail pages, each half the size of the previous, only one page of each size [TODO?] 

            var sys = (basePosition == 0x50000000);
            var maxPageSizeMult = 16L;//the biggest page is 16x the base page size.
            var maxPageSize = (0x2000 << 0xF) * maxPageSizeMult; //this is the size of the biggest possible page [4GB!]
            var maxBlockSize = 0L;
            var minBlockSize = (blocks.Count == 0) ? 0 : maxPageSize;
            if (gen9)
            {
                foreach (var block in blocks)
                {
                    if (block.BlockLength_Gen9 > maxBlockSize) maxBlockSize = block.BlockLength_Gen9;
                    if (block.BlockLength_Gen9 < minBlockSize) minBlockSize = block.BlockLength_Gen9;
                }
            }
            else
            {
                foreach (var block in blocks)
                {
                    if (block.BlockLength > maxBlockSize) maxBlockSize = block.BlockLength;
                    if (block.BlockLength < minBlockSize) minBlockSize = block.BlockLength;
                }
            }

            var baseShift = 0;//want to find the best value for this
            var baseSize = 0x2000L;//corresponding size for the baseShift value
            while (((baseSize < minBlockSize) || ((baseSize * maxPageSizeMult) < maxBlockSize)) && (baseShift < 0xF))
            {
                baseShift++;
                baseSize = 0x2000L << baseShift;
            }
            if ((baseSize * maxPageSizeMult) < maxBlockSize) throw new Exception("Unable to fit largest block!");



            var sortedBlocks = new List<IResourceBlock>();
            var rootBlock = (sys && (blocks.Count > 0)) ? blocks[0] : null;
            foreach (var block in blocks)
            {
                if (block == null) continue;
                if (block != rootBlock) sortedBlocks.Add(block);
            }
            if (gen9)
            {
                sortedBlocks.Sort((a, b) => b.BlockLength_Gen9.CompareTo(a.BlockLength_Gen9));
            }
            else
            {
                sortedBlocks.Sort((a, b) => b.BlockLength.CompareTo(a.BlockLength));
            }
            if (rootBlock != null) sortedBlocks.Insert(0, rootBlock);


            var pageCounts = new uint[5];
            var pageSizes = new List<long>?[5];
            var blockPages = new Dictionary<IResourceBlock, (int, int, long)>();//(pageSizeIndex, pageIndex, offset)
            while (true)
            {
                for (int i = 0; i < 5; i++)
                {
                    pageCounts[i] = 0;
                    pageSizes[i] = null;
                }

                var largestPageSizeI = 0;
                var largestPageSize = baseSize;
                while (largestPageSize < maxBlockSize)
                {
                    largestPageSizeI++;
                    largestPageSize *= 2;
                }

                for (int i = 0; i < sortedBlocks.Count; i++)
                {
                    var block = sortedBlocks[i];
                    var size = gen9 ? block.BlockLength_Gen9 : block.BlockLength;
                    if (i == 0)//first block should always go in the first page, it's either root block or largest
                    {
                        pageSizes[largestPageSizeI] = new List<long>() { size };//allocate the first new page
                        blockPages[block] = (largestPageSizeI, 0, 0);
                    }
                    else
                    {
                        var pageSizeIndex = 0;
                        var pageSize = baseSize;
                        while ((size > pageSize) && (pageSizeIndex < largestPageSizeI))//find the smallest page that will fit this block
                        {
                            pageSizeIndex++;
                            pageSize *= 2;
                        }
                        var found = false;//find an existing page of this size or larger which has space
                        var testPageSizeI = pageSizeIndex;
                        var testPageSize = pageSize;
                        while ((found == false) && (testPageSizeI <= largestPageSizeI))
                        {
                            var list = pageSizes[testPageSizeI];
                            if (list != null)
                            {
                                for (int p = 0; p < list.Count; p++)
                                {
                                    var s = list[p];
                                    s += ((ALIGN_SIZE - (s % ALIGN_SIZE)) % ALIGN_SIZE);
                                    var o = s;
                                    s += size;
                                    if (s <= testPageSize)
                                    {
                                        list[p] = s;
                                        found = true;
                                        blockPages[block] = (testPageSizeI, p, o);
                                        break;
                                    }
                                }
                            }
                            testPageSizeI++;
                            testPageSize *= 2;
                        }
                        if (found == false)//couldn't find an existing page for this block, so allocate a new page
                        {
                            var list = pageSizes[pageSizeIndex];
                            if (list == null)
                            {
                                list = new List<long>();
                                pageSizes[pageSizeIndex] = list;
                            }
                            var pageIndex = list.Count;
                            list.Add(size);
                            blockPages[block] = (pageSizeIndex, pageIndex, 0);
                        }
                    }
                }

                var testOk = true;
                var totalPageCount = 0u;
                for (int i = 0; i < 5; i++)
                {
                    var pc = (uint)(pageSizes[i]?.Count ?? 0);
                    pageCounts[i] = pc;
                    totalPageCount += pc;
                }
                if (totalPageCount > maxPageCount) testOk = false;
                if (pageCounts[0] > 0x7F) testOk = false;
                if (pageCounts[1] > 0x3F) testOk = false;
                if (pageCounts[2] > 0xF) testOk = false;
                if (pageCounts[3] > 0x3) testOk = false;
                if (pageCounts[4] > 0x1) testOk = false;
                if (testOk) break;//everything fits, so we're done here
                if (baseShift >= 0xF) throw new Exception("Unable to pack blocks with largest possible base!");
                baseShift++;
                baseSize = 0x2000 << baseShift;
            }


            
            var pageOffset = 0L;//pages are allocated, assign actual positions
            var pageOffsets = new long[5];//base offsets for each page size
            for (int i = 4; i >= 0; i--)
            {
                pageOffsets[i] = pageOffset;
                var pageSize = baseSize * (1 << i);
                var pageCount = pageCounts[i];
                pageOffset += (pageSize * pageCount);
            }
            foreach (var kvp in blockPages)
            {
                var block = kvp.Key;
                var pageSizeIndex = kvp.Value.Item1;
                var pageIndex = kvp.Value.Item2;
                var offset = kvp.Value.Item3;
                var pageSize = baseSize * (1 << pageSizeIndex);
                var blockPosition = pageOffsets[pageSizeIndex] + (pageSize * pageIndex) + offset;
                block.FilePosition = basePosition + blockPosition;
            }


            var v = (uint)baseShift & 0xF;
            v += (pageCounts[4] & 0x1) << 4;
            v += (pageCounts[3] & 0x3) << 5;
            v += (pageCounts[2] & 0xF) << 7;
            v += (pageCounts[1] & 0x3F) << 11;
            v += (pageCounts[0] & 0x7F) << 17;
            pageFlags = new RpfResourcePageFlags(v);


        }


        // Tail-aware page packer.
        // Generalises AssignPositions2 to all 9 page-size buckets the resource header can describe:
        //   index 0..4 = "head" pages of 16x/8x/4x/2x/1x the base chunk size (counts up to 1/3/15/63/127)
        //   index 5..8 = "tail" pages of 1/2/1/4/1/8/1/16 the base chunk size (one page each)
        // Unlike AssignPositions2 it does NOT force the base chunk to be >= the smallest block, so the
        // smallest blocks can land in fractional tail pages instead of wasting a full base page - which is
        // how the original RAGE buddy-allocated resources are laid out (HasTail2/4/8/16 in datResourceInfo).
        // Returns false if it cannot pack (caller should fall back to AssignPositions2).
        public static bool TryAssignPositionsWithTails(IList<IResourceBlock> blocks, uint basePosition, out RpfResourcePageFlags pageFlags, uint maxPageCount, bool gen9)
        {
            pageFlags = new RpfResourcePageFlags(0);

            if ((blocks.Count > 0) && (blocks[0] is Meta))
            {
                return false;//Meta uses the naive packer - let the caller fall back
            }
            if (blocks.Count == 0)
            {
                return true;//nothing to pack, zero flags are correct
            }

            long getLen(IResourceBlock b) => gen9 ? b.BlockLength_Gen9 : b.BlockLength;
            long pad(long p) => ((ALIGN_SIZE - (p % ALIGN_SIZE)) % ALIGN_SIZE);

            var sys = (basePosition == 0x50000000);

            long maxBlockSize = 0;
            foreach (var block in blocks)
            {
                var l = getLen(block);
                if (l > maxBlockSize) maxBlockSize = l;
            }

            //the root (system) block must end up at offset 0, so keep it first
            var rootBlock = (sys && (blocks.Count > 0)) ? blocks[0] : null;
            var sortedBlocks = new List<IResourceBlock>();
            foreach (var block in blocks)
            {
                if (block == null) continue;
                if (block != rootBlock) sortedBlocks.Add(block);
            }
            sortedBlocks.Sort((a, b) => getLen(b).CompareTo(getLen(a)));
            if (rootBlock != null) sortedBlocks.Insert(0, rootBlock);

            //bucket sizes are laid out largest-first to match RpfResourcePageFlags page ordering
            var maxCounts = new uint[] { 1, 3, 15, 63, 127, 1, 1, 1, 1 };

            for (int baseShift = 0; baseShift <= 0xF; baseShift++)
            {
                long chunkBase = 0x2000L << baseShift;//the 1x base chunk size (== RpfResourcePageFlags BaseSizes[4])
                if ((chunkBase * 16) < maxBlockSize) continue;//the largest block has to fit in the largest (16x) page

                var sizes = new long[9];
                for (int i = 0; i < 9; i++) sizes[i] = (chunkBase * 16) >> i;//i=0:16x ... i=4:1x ... i=8:1/16

                int largestBucket = 8;
                for (int j = 0; j < 9; j++) { if (sizes[j] >= maxBlockSize) { largestBucket = j; break; } }

                var pageUsed = new List<long>[9];//bytes used so far in each allocated page, per bucket
                var blockPages = new Dictionary<IResourceBlock, (int bucket, int page, long off)>();

                bool ok = true;
                for (int bi = 0; bi < sortedBlocks.Count; bi++)
                {
                    var block = sortedBlocks[bi];
                    var size = getLen(block);

                    if (bi == 0)//root (or largest) block starts the first largest-used page, at offset 0
                    {
                        pageUsed[largestBucket] = new List<long>() { size };
                        blockPages[block] = (largestBucket, 0, 0);
                        continue;
                    }

                    //smallest bucket (largest index) whose page is big enough for this block
                    int wantBucket = 0;
                    for (int j = 8; j >= 0; j--) { if (sizes[j] >= size) { wantBucket = j; break; } }

                    //try to fit into an existing page of that size or larger (smaller index)
                    bool placed = false;
                    for (int j = wantBucket; (j >= 0) && !placed; j--)
                    {
                        var list = pageUsed[j];
                        if (list == null) continue;
                        for (int p = 0; p < list.Count; p++)
                        {
                            var s = list[p];
                            s += pad(s);
                            var o = s;
                            s += size;
                            if (s <= sizes[j]) { list[p] = s; blockPages[block] = (j, p, o); placed = true; break; }
                        }
                    }
                    if (placed) continue;

                    //allocate a new page: prefer the smallest fitting bucket; if it's maxed out, step up to a larger one
                    for (int j = wantBucket; j >= 0; j--)
                    {
                        var list = pageUsed[j];
                        var cur = list?.Count ?? 0;
                        if (cur < maxCounts[j])
                        {
                            if (list == null) { list = new List<long>(); pageUsed[j] = list; }
                            var p = list.Count;
                            list.Add(size);
                            blockPages[block] = (j, p, 0);
                            placed = true;
                            break;
                        }
                    }
                    if (!placed) { ok = false; break; }//ran out of pages at this base size
                }
                if (!ok) continue;

                var counts = new uint[9];
                uint totalPages = 0;
                for (int i = 0; i < 9; i++) { var c = (uint)(pageUsed[i]?.Count ?? 0); counts[i] = c; totalPages += c; if (c > maxCounts[i]) ok = false; }
                if (!ok) continue;
                if (totalPages > maxPageCount) continue;//too many pages - try a larger base size

                //assign actual file positions; pages are laid out largest (index 0) first
                var pageBaseOffsets = new long[9];
                long pageOffset = 0;
                for (int i = 0; i < 9; i++) { pageBaseOffsets[i] = pageOffset; pageOffset += sizes[i] * counts[i]; }
                foreach (var kvp in blockPages)
                {
                    var bucket = kvp.Value.bucket;
                    var page = kvp.Value.page;
                    var off = kvp.Value.off;
                    var pos = pageBaseOffsets[bucket] + (sizes[bucket] * page) + off;
                    kvp.Key.FilePosition = basePosition + pos;
                }

                uint v = (uint)baseShift & 0xF;
                v += (counts[0] & 0x1) << 4;
                v += (counts[1] & 0x3) << 5;
                v += (counts[2] & 0xF) << 7;
                v += (counts[3] & 0x3F) << 11;
                v += (counts[4] & 0x7F) << 17;
                v += (counts[5] & 0x1) << 24;
                v += (counts[6] & 0x1) << 25;
                v += (counts[7] & 0x1) << 26;
                v += (counts[8] & 0x1) << 27;
                pageFlags = new RpfResourcePageFlags(v);
                return true;
            }

            return false;//couldn't pack even at the largest base size
        }

        // Validates that every block lies fully inside a single page and within the total mapped size
        // described by the page flags. Used as a safety net for TryAssignPositionsWithTails.
        public static bool ValidatePacking(IList<IResourceBlock> blocks, uint basePosition, RpfResourcePageFlags pageFlags, bool gen9)
        {
            if (blocks.Count == 0) return true;
            var pages = pageFlags.Pages;
            if (pages == null) return false;
            long total = pageFlags.Size;
            var extents = new List<(long pos, long end)>(blocks.Count);
            foreach (var block in blocks)
            {
                if (block == null) continue;
                long pos = (long)block.FilePosition - basePosition;
                long len = gen9 ? block.BlockLength_Gen9 : block.BlockLength;
                if (pos < 0) return false;
                if ((pos + len) > total) return false;
                bool inPage = false;
                foreach (var pg in pages)
                {
                    if ((pos >= pg.Offset) && ((pos + len) <= ((long)pg.Offset + pg.Size))) { inPage = true; break; }
                }
                if (!inPage) return false;
                extents.Add((pos, pos + len));
            }
            //ensure no two blocks overlap
            extents.Sort((a, b) => a.pos.CompareTo(b.pos));
            for (int i = 1; i < extents.Count; i++)
            {
                if (extents[i].pos < extents[i - 1].end) return false;
            }
            return true;
        }


        public static byte[] Build(ResourceFileBase fileBase, int version, bool compress = true, bool gen9 = false)
        {

            fileBase.FilePagesInfo = new ResourcePagesInfo();

            IList<IResourceBlock> systemBlocks;
            IList<IResourceBlock> graphicBlocks;
            GetBlocks(fileBase, out systemBlocks, out graphicBlocks);

            //AssignPositions(systemBlocks, 0x50000000, out var systemPageFlags, 128);
            //AssignPositions(graphicBlocks, 0x60000000, out var graphicsPageFlags, 128 - systemPageFlags.Count);

            //try the tail-aware packer first (matches the game's use of fractional tail pages),
            //falling back to AssignPositions2 if it can't pack or the result fails validation.
            if (!TryAssignPositionsWithTails(systemBlocks, 0x50000000, out var systemPageFlags, 128, gen9)
                || !ValidatePacking(systemBlocks, 0x50000000, systemPageFlags, gen9))
            {
                AssignPositions2(systemBlocks, 0x50000000, out systemPageFlags, 128, gen9);
            }
            if (!TryAssignPositionsWithTails(graphicBlocks, 0x60000000, out var graphicsPageFlags, 128 - systemPageFlags.Count, gen9)
                || !ValidatePacking(graphicBlocks, 0x60000000, graphicsPageFlags, gen9))
            {
                AssignPositions2(graphicBlocks, 0x60000000, out graphicsPageFlags, 128 - systemPageFlags.Count, gen9);
            }


            fileBase.FilePagesInfo.SystemPagesCount = (byte)systemPageFlags.Count;
            fileBase.FilePagesInfo.GraphicsPagesCount = (byte)graphicsPageFlags.Count;


            using var systemStream = new MemoryStream();
            using var graphicsStream = new MemoryStream();
            var resourceWriter = new ResourceDataWriter(systemStream, graphicsStream);
            resourceWriter.IsGen9 = gen9;

            resourceWriter.Position = 0x50000000;
            foreach (var block in systemBlocks)
            {
                resourceWriter.Position = block.FilePosition;

                var pos_before = resourceWriter.Position;
                block.Write(resourceWriter);
                var pos_after = resourceWriter.Position;
                var blen = resourceWriter.IsGen9 ? block.BlockLength_Gen9 : block.BlockLength;

                if ((pos_after - pos_before) != blen)
                {
                    throw new Exception("error in system length");
                }
            }

            resourceWriter.Position = 0x60000000;
            foreach (var block in graphicBlocks)
            {
                resourceWriter.Position = block.FilePosition;

                var pos_before = resourceWriter.Position;
                block.Write(resourceWriter);
                var pos_after = resourceWriter.Position;
                var blen = resourceWriter.IsGen9 ? block.BlockLength_Gen9 : block.BlockLength;

                if ((pos_after - pos_before) != blen)
                {
                    throw new Exception("error in graphics length");
                }
            }




            var sysDataSize = (int)systemPageFlags.Size;
            var sysData = new byte[sysDataSize];
            systemStream.Flush();
            systemStream.Position = 0;
            systemStream.Read(sysData, 0, Math.Min(sysDataSize, (int)systemStream.Length));


            var gfxDataSize = (int)graphicsPageFlags.Size;
            var gfxData = new byte[gfxDataSize];
            graphicsStream.Flush();
            graphicsStream.Position = 0;
            graphicsStream.Read(gfxData, 0, Math.Min(gfxDataSize, (int)graphicsStream.Length));



            uint uv = (uint)version;
            uint sv = (uv >> 4) & 0xF;
            uint gv = (uv >> 0) & 0xF;
            uint sf = systemPageFlags.Value + (sv << 28);
            uint gf = graphicsPageFlags.Value + (gv << 28);


            var tdatasize = sysDataSize + gfxDataSize;
            var tdata = new byte[tdatasize];
            Buffer.BlockCopy(sysData, 0, tdata, 0, sysDataSize);
            Buffer.BlockCopy(gfxData, 0, tdata, sysDataSize, gfxDataSize);


            var cdata = compress ? Compress(tdata) : tdata;


            var dataSize = 16 + cdata.Length;
            var data = new byte[dataSize];

            byte[] h1 = BitConverter.GetBytes((uint)0x37435352);
            byte[] h2 = BitConverter.GetBytes((int)version);
            byte[] h3 = BitConverter.GetBytes(sf);
            byte[] h4 = BitConverter.GetBytes(gf);
            Buffer.BlockCopy(h1, 0, data, 0, 4);
            Buffer.BlockCopy(h2, 0, data, 4, 4);
            Buffer.BlockCopy(h3, 0, data, 8, 4);
            Buffer.BlockCopy(h4, 0, data, 12, 4);
            Buffer.BlockCopy(cdata, 0, data, 16, cdata.Length);

            return data;
        }






        [return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull(nameof(data))]
        public static byte[]? AddResourceHeader(RpfResourceFileEntry entry, byte[]? data)
        {
            if (data == null) return null;
            byte[] newdata = new byte[data.Length + 16];
            byte[] h1 = BitConverter.GetBytes((uint)0x37435352);
            byte[] h2 = BitConverter.GetBytes(entry.Version);
            byte[] h3 = BitConverter.GetBytes(entry.SystemFlags);
            byte[] h4 = BitConverter.GetBytes(entry.GraphicsFlags);
            Buffer.BlockCopy(h1, 0, newdata, 0, 4);
            Buffer.BlockCopy(h2, 0, newdata, 4, 4);
            Buffer.BlockCopy(h3, 0, newdata, 8, 4);
            Buffer.BlockCopy(h4, 0, newdata, 12, 4);
            Buffer.BlockCopy(data, 0, newdata, 16, data.Length);
            return newdata;
        }


        public static byte[] Compress(byte[] data)
        {
            using var ms = new MemoryStream();
            using (var ds = new DeflateStream(ms, CompressionMode.Compress, leaveOpen: true))
            {
                ds.Write(data, 0, data.Length);
            }
            return ms.ToArray();
        }
        public static byte[] Decompress(byte[] data)
        {
            using var ms = new MemoryStream(data);
            using var ds = new DeflateStream(ms, CompressionMode.Decompress);
            using var outstr = new MemoryStream();
            ds.CopyTo(outstr);
            return outstr.ToArray();
        }

    }
}
