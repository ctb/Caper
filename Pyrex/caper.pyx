cdef extern from "Caper.h":
    # Sequence class
    # declared as c_sequence

    ctypedef char * (*ToStringP)()
    ctypedef int (*OrientationI)()
    ctypedef struct c_sequence "Sequence":
        ToStringP ToStringP
        OrientationI OrientationI

    # Mapping class
    # declared as c_mapping

    ctypedef struct c_mapping "Mapping":
        c_sequence * mSequence
        ToStringP NameP
        int Index
        OrientationI GetOrientation

    void del_mapping "delete" (c_mapping * mapping)

    # ReadsAtIndex class
    # declared as c_reads_at_index (vector of Mapping*)

    ctypedef int (*size)()
    ctypedef c_mapping * (*at)(int)
    ctypedef void (*destroy)()
    ctypedef struct c_reads_at_index "ReadsAtIndex":
        size size
        at at
        destroy Destroy

    void del_reads_at_index "delete" (c_reads_at_index * reads_at_index)

    # vector<ReadsAtIndex*> class
    # declared as c_reads_at_indexes (vector of ReadsAtIndex*)

    ctypedef c_reads_at_index * (*at_reads)(int)
    ctypedef struct c_reads_at_indexes "IndexedMappingsFlat":
        size size
        at_reads at

    void del_reads_at_indexes "delete" (c_reads_at_indexes * reads_at_indexes)

    # MappingEngine::iterator class
    # declared as c_mapping_engine_iterator

    ctypedef void (*next)()
    ctypedef void (*previous)()
    ctypedef void (*end)()
    ctypedef c_reads_at_index * (*get_reads)()
    ctypedef c_reads_at_indexes * (*get_reads_indexed_flat)()
    ctypedef int (*get_index)()
    ctypedef c_reads_at_indexes * (*intersect_flat)()
    ctypedef struct c_mapping_engine_iterator "MappingEngine::iterator":
        next Next
        previous Previous
        end End
        get_index GetIndex
        get_reads GetReads
        intersect_flat IntersectFlat
        get_reads_indexed_flat GetReadsIndexedFlat

    void del_mapping_engine_iterator "delete" (c_mapping_engine_iterator * iterator)

    # MappingEngine class
    # declared as c_mapping_engine (only the bundle version :/)

    ctypedef void (*Initialize)()
#    ctypedef c_mapping_engine_iterator * (*begin)(char *)
    ctypedef c_mapping_engine_iterator * (*at_iter)(char *, int)
#    ctypedef c_mapping_engine_iterator * (*end_iter)(char *)
    ctypedef c_reads_at_index * (*GetReads)(char *, int)
    ctypedef c_reads_at_indexes * (*GetIntersectionFlat)(char *, int, int)
    ctypedef int (*GetReadLength)()

    ctypedef struct c_mapping_engine "MappingEngine":
        Initialize Initialize
#        begin Begin
        at_iter AtPtr
#        end_iter End
        GetReads GetReads
        GetIntersectionFlat GetIntersectionFlat
        GetReadLength GetReadLength

    # create c_mapping_engine from MappingEngine objects
    c_mapping_engine *new_mapping_engine "new MappingEngine" (char * bundle_path)
    void del_mapping_engine "delete" (c_mapping_engine * engine)

    # MappingIndexer class
    # declared as c_mapping_indexer

    ctypedef enum c_mapping_file_format:
        DEFAULT
        MAPVIEW
        BOWTIE
        SAM

    ctypedef void (*CreateIndex)()
    ctypedef struct c_mapping_indexer "MappingIndexer":
        CreateIndex CreateIndex

    c_mapping_indexer *new_mapping_indexer "new MappingIndexer" (char * source_path, c_mapping_file_format format,  char * save_path, short bundle)
    void del_mappings_indexer "delete" (c_mapping_indexer * indexer)

### END Pyrex Definitions
#############################

###### BEGIN PYTHON CLASSES ######

## @RCK
## no idea what would be useful in this context.
## perhaps something that deals gracefully with the iterator?

cdef class mappings:
    """List of mappings at a particular index"""
    cdef c_reads_at_index * mappings ## a simple collection of mappings, but they identify themselves by their index.
    cdef public char * seqname ## the name of the contig
    cdef public int index ## the index that I am

    cdef int current ## hey, I'm the current index. :P

    def __cinit__(self, seqname, index): #, c_reads_at_index * reads_at_index): ## __cinit__ can only take python parameters. :(
        self.seqname = seqname
        self.index = index

    ## no specific deallocator is required. It takes care of itself... I think?

    def __repr__(self): ## what does this do?
        return "mappings('%s', %d)" % (self.seqname, self.index)

    def __len__(self): ## this probably doesn't work.
        """Return number of overlapping mappings at this point."""
        return self.mappings.size() ##

    def __getitem__(self, i):
        """Return (start, sequence) of overlapping mapping."""
        if i < 0 or i >= self.mappings.size():
            raise IndexError

        cdef c_mapping * item
        item = self.mappings.at(i)

        name = item.NameP()
        index = item.Index
        seq = item.mSequence.ToStringP()
        orientation = item.GetOrientation()
        return index, name, seq, orientation # we don't know anything about slicing in this context.

cdef class mappingsinterval:
    """Two-dimensional dictionary of mappings (the collection of mappings at those indexes)"""
    cdef c_reads_at_indexes * mappings ## the set of mappings that overlap the given index
    cdef public char * seqname ## name of the contig
    cdef public int slice_start, slice_stop ## beginning and end of the interval (umm, does this make sense?)

    cdef int current ## hey, I'm the current index. :P

    def __cinit__(self, seqname, slice_start, slice_stop): #, indexed_mappings): ## populating the info stuff
        self.seqname = seqname
        self.slice_start = slice_start
        self.slice_stop = slice_stop

    def __dealloc__(self):
        cdef c_reads_at_indexes * mappings
        mappings = self.mappings
        del_reads_at_indexes(mappings) ## this should work, since it should call the right destructor... maybe.

    def __repr__(self):
        return "mappingsinterval('%s', %d, %d)" % (self.seqname, self.slice_start, self.slice_stop)

    def __len__(self): ## this probably doesn't work.
        """Return number of overlapping mappings at this point."""
        return self.mappings.size() ##this isn't correct. :(

    def __getitem__(self, i):  ## thinking about making this a slightly smarter iterator so that it returns the reads not the read index container
        """Return (align_start, align_stop, slice_start, slice_stop, and the set of reads) at this spot."""
        if i < 0 or i >= self.mappings.size():
            raise IndexError

        cdef c_reads_at_index * reads_at_index
        reads_at_index = self.mappings.at(i)


        cdef mappings reads
        cdef c_mapping * thingy
        thingy = reads_at_index.at(0)
        thingy2 = thingy.Index
        reads = mappings(self.seqname, reads_at_index.at(0).Index)
        reads.mappings = reads_at_index

        cdef c_mapping * read
        read = reads_at_index.at(0) ## get the zeroth one
        ## all the reads to be returned have the same info.

        seq = read.mSequence.ToStringP()
        seq_len = len(seq)
        seq_start = read.Index
        seq_stop = seq_start + seq_len

        slice_start = 0;
        align_start = seq_start
        if seq_start < self.slice_start:
            slice_start = self.slice_start - seq_start
            align_start = self.slice_start

        slice_stop = seq_len
        align_stop = seq_stop
        if seq_stop > self.slice_stop:
            slice_stop = seq_len - (seq_stop - self.slice_stop)
            align_stop = self.slice_stop

        return align_start, align_stop, slice_start, slice_stop, reads

cdef class iterator:
    """Iterates through the genome, one index at a time."""
    cdef c_mapping_engine_iterator * thisiterator
    cdef public char * seqname
    cdef public int start, current

    def __cinit__(self, seqname, start):
        self.start = start
        self.seqname = seqname
        self.current = start

    def __iter__(self):
        return self

    def __repr__(self):
        return "iterator('%s', %d, %d)" % (self.seqname, self.start, self.current)

    # Conform to Pyrex's iterator protocol, which asks for a __next__ on
    # iterator objects.
    # @CTB can we add current to mappings()?
    # @CTB note, 'next()' is reserved by Pyrex
    def __next__(self):
        self.thisiterator.Next()
        self.current = self.thisiterator.GetIndex()
        if self.current == -1:
            raise StopIteration
        return (self.current, self.get_reads())

    def get_reads(self):
        cdef c_reads_at_index * reads
        reads = self.thisiterator.GetReads()
        cdef mappings x
        x = mappings(self.seqname, self.current)
        x.mappings = reads
        return x

    def get_intersection(self):
        cdef c_reads_at_indexes * reads
        reads = self.thisiterator.IntersectFlat()
        cdef mappingsinterval x
        x = mappingsinterval(self.seqname, self.current, self.current)
        x.mappings = reads
        return x

cdef class slice_iterator:
    """Iterates through a slice of the genome."""
    cdef c_mapping_engine_iterator * thisiterator
    cdef public char * seqname
    cdef public int start, current

    def __cinit__(self, seqname, start):
        self.start = start
        self.current = start
        self.seqname = seqname

    def __iter__(self):
        return self

    def __repr__(self):
        return "slice_iterator('%s', %d, %d)" % (self.seqname, self.start, self.current)

    def __next__(self):
        self.thisiterator.Next()
        self.current = self.thisiterator.GetIndex()
        if self.current == -1:
            raise StopIteration
        return (self.current, self.get_reads())

    def get_reads(self):
        cdef c_reads_at_indexes * reads
        if self.current == self.start:
            reads = self.thisiterator.IntersectFlat()
        else:
            reads = self.thisiterator.GetReadsIndexedFlat()
        cdef mappingsinterval x
        x = mappingsinterval(self.seqname, self.current, self.current)
        x.mappings = reads
        return x

cdef class mapping_container:
    cdef c_mapping_engine *thismap

    def __cinit__(self, bundle_path):
        self.thismap = NULL
        self.thismap = new_mapping_engine(bundle_path)
        self.thismap.Initialize()

    def __dealloc__(self):
        del_mapping_engine(self.thismap)

    def get_read_length(self):
        length = self.thismap.GetReadLength()
        return length

    def get_iterator(self, seqname, start):
        cdef iterator x
        x = iterator(seqname, start)
        x.thisiterator = self.thismap.AtPtr(seqname, start);
        return x

    def get_slice_iterator(self, seqname, start):
        cdef slice_iterator x
        x = slice_iterator(seqname, start)
        x.thisiterator = self.thismap.AtPtr(seqname, start)
        return x

    def get_intersect(self, seqname, index):
        cdef mappingsinterval reads
        reads = mappingsinterval(seqname, index, index)
        reads.mappings = self.thismap.GetIntersectionFlat(seqname, index, index)
        return reads

    def get_slice(self, seqname, left, right ):
        cdef mappingsinterval reads
        reads = mappingsinterval(seqname, left, right)
        reads.mappings = self.thismap.GetIntersectionFlat(seqname, left, right)
        return reads

    def get_reads(self, seqname, index):
        cdef mappings reads
        reads = mappings(seqname, index)
        reads.mappings = self.thismap.GetReads(seqname, index)
        return reads

####### END PYTHON CLASSES
