﻿using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace CaperSharp
{
  abstract class MappingEngine
  {
    public int ReadLength;

    public string Path { get; private set; }
    private Sequences ReferenceGenome { get; set; }

    const int IndexIncrement = 1000;
    const int StartingJumpIncrement = 10000;
    private const char Tab = '\t';
    private const char NewLine = '\n';
    private int mLineLengthIsh;
    private long mEndOfFilePos;

    private MappingCache CacheA;
    private MappingCache CacheB;

    private DecoupledStreamReader mStream;

    private Dictionary<string, List<long>> mFilePositions;
    private Dictionary<string, int> mNumberOfWindows;
    private Dictionary<string, Pair<long>> mContigBorders;
    private Dictionary<string, int> mSortedContigIdents;

    private enum Direction
    {
      JumpForward,
      JumpBackward
    }

    public MappingEngine( string aPath, Sequences aReferenceGenome )
    {
      ReferenceGenome = aReferenceGenome;
      Path = aPath;

      mNumberOfWindows = new Dictionary<string, int>();
      mFilePositions = new Dictionary<string, List<long>>();
      mContigBorders = new Dictionary<string, Pair<long>>();
      mSortedContigIdents = new Dictionary<string, int>();

      mLineLengthIsh = 0;
      mEndOfFilePos = 0;
    }

    /// <summary>
    /// This assumes that all the mappings are sorted by contig ident and by index.
    /// </summary>
    private void PrepareFilePositions()
    {
      int i = 0;
      foreach ( string lContigIdent in ReferenceGenome.Keys )
        mSortedContigIdents.Add( lContigIdent, i++ );

      long lLastPos = -1;
      foreach ( string lContigIdent in ReferenceGenome.Keys )
      {
        long lBeginning = lLastPos + 1; // one over to the beginning of the next one.
        lLastPos = FindEndOfContig( mStream, lContigIdent, IndexIncrement );
        mContigBorders.Add( lContigIdent, new Pair<long>() { FirstItem = lBeginning, SecondItem = lLastPos } );
      }

      foreach ( string lContigIdent in ReferenceGenome.Keys )
      {
        mFilePositions.Add( lContigIdent, new List<long>() );
        mFilePositions[ lContigIdent ].Add( mContigBorders[ lContigIdent ].FirstItem ); // the first one is always here. I suppose I could figure out how to make it populate from 0, but why?
        for ( int j = 1; j < mNumberOfWindows[ lContigIdent ]; j++ )
        {
          mFilePositions[ lContigIdent].Add( FindIndex( mStream, j * IndexIncrement, IndexIncrement, mContigBorders[ lContigIdent ].FirstItem, mContigBorders[ lContigIdent ].SecondItem ) );
        }
      }
    }

    private long FindIndex( DecoupledStreamReader aStream, int aTargetIndex, int aIncrement, long aStart, long aEnd )
    {
      long lPreJumpPosition = aStream.Position;

      SafeSeek( aStream, aStream.Position + aIncrement, aStart, aEnd );

      AdvanceToStartOfLine( aStream );// clean up the queue so we can get a clean read.

      long lPreReadPosition = aStream.Position;
      string lLine = aStream.ReadLine();

      int lIndex = -1;
      if ( lLine != null )
        lIndex = GetIndex( lLine ); // read the contig ident.


      if ( lPreJumpPosition == aStream.Position ) // we are bouncing off the wall, this is it
        return aStream.Position - 1; // this is the edge. you're done.

      if ( lPreReadPosition == aEnd || lIndex >= aTargetIndex ) // TOO FAR. undo the jump and shorten it up.
      {
        aStream.Position = lPreJumpPosition; // back up.
        if ( aIncrement > mLineLengthIsh * 2 ) // do we jump again?
          return FindIndex( aStream, aTargetIndex, aIncrement / 10, aStart, aEnd );
        else // or plough forward?
        {
          AdvanceToStartOfLine( aStream );// clean the queue. 
          while ( aStream.Position < mEndOfFilePos )
          {
            long lLinePosition = aStream.Position;

            int lThisIndex = GetIndex( aStream.ReadLine() );

            if ( lThisIndex > aTargetIndex ) // we've crossed over!
            {
              return lLinePosition - 1;
            }
          }
          // we ploughed forward, but we never crossed over the index we were looking for.
          return aStream.Position - 1;
        }
      }
      else // otherwise, keep going!
      {
        return FindIndex( aStream, aTargetIndex, aIncrement, aStart, aEnd ); // NOTE, this could keep jumping forward in ever smaller increments. should put some control on this.
      }
      throw new Exception( "WHAT!?!?!" );
    }


    private long FindEndOfContig( DecoupledStreamReader aStream, string aTargetContigIdent, int aIncrement )
    {
      long lPreJumpPosition = aStream.Position;

      SafeSeek( aStream, aStream.Position + aIncrement );

      AdvanceToStartOfLine( aStream );// clean up the queue so we can get a clean read.

      string lReadContigIdent = GetContigIdent( aStream.ReadLine() ); // read the contig ident.

      if ( lPreJumpPosition == aStream.Position ) // we are bouncing off the wall, this is it
        return aStream.Position - 1; // this is the edge. you're done.

      if ( mSortedContigIdents[ lReadContigIdent ] > mSortedContigIdents[ aTargetContigIdent ] ) // TOO FAR. undo the jump and shorten it up.
      {
        aStream.Position = lPreJumpPosition; // back up.
        if ( aIncrement > mLineLengthIsh * 2 ) // do we jump again?
          return FindEndOfContig( aStream, aTargetContigIdent, aIncrement / 10 );
        else // or plough forward?
        {
          AdvanceToStartOfLine( aStream );// clean the queue. 
          while ( aStream.Position < mEndOfFilePos )
          {
            long lLinePosition = aStream.Position;

            string lThisContigIdent = GetContigIdent( aStream.ReadLine() );

            if ( lThisContigIdent != aTargetContigIdent ) // we've crossed over!
            {
              return lLinePosition - 1;
            }
          }
        }
      }
      else // otherwise, keep going!
      {
        return FindEndOfContig( aStream, aTargetContigIdent, aIncrement ); // NOTE, this could keep jumping forward in ever smaller increments. should put some control on this.
      }
      throw new Exception( "WHAT!?!?!" );
    }

    private void AdvanceToStartOfLine( DecoupledStreamReader aStream )
    {
      if ( aStream.Position == 0 )
        return;

      aStream.BaseStream.Position -= 1; // back up.
      if ( ( ( char ) aStream.BaseStream.ReadByte() ) == NewLine ) // read the next byte.
        return;
      else
        aStream.ReadLine();
    }

    private void GenerateNumberOfWindows()
    {
      foreach ( string lContigIdent in ReferenceGenome.Keys )
      {
        mNumberOfWindows.Add( lContigIdent,
         ( int ) Math.Ceiling( ( double ) ReferenceGenome[ lContigIdent ].Length / IndexIncrement ) );
      }
    }

    internal virtual bool Initialize()
    {
      mStream = new DecoupledStreamReader( Path );
      InitFileInformation( mStream );
      GenerateNumberOfWindows();
      PrepareFilePositions();

      return true; // test whether the thing finished or not.
    }

    private void InitFileInformation( DecoupledStreamReader lStream )
    {
      string lLine = lStream.ReadLine();
      mLineLengthIsh = lLine.Length;
      ReadLength = GetSequence( lLine ).Length;

      lStream.BaseStream.Seek( 0, SeekOrigin.End );
      mEndOfFilePos = lStream.Position;
      lStream.Position = 0;
    }

    private void SafeSeek( DecoupledStreamReader aStream, long aTargetSeek )
    {
      SafeSeek( aStream, aTargetSeek, 0, mEndOfFilePos );
    }

    private void SafeSeek( DecoupledStreamReader aStream, long aTargetSeek, long aBeginning, long aEnd )
    {
      if ( aTargetSeek >= aEnd ) // at the end, back it up by a line and a half.
        aStream.Position = aEnd - ( long ) ( mLineLengthIsh * 1.5 );
      else if ( aTargetSeek < aBeginning )
        aStream.Position = aBeginning;
      else
        aStream.Position = aTargetSeek; // do the seek as requested.
    }

    internal virtual ICollection<Mapping> GetReads( string lContigIdent, int aLeft, int aRight )
    {
      List<Mapping> lResult = new List<Mapping>();

      int lStartingCache = aLeft / IndexIncrement;
      int lEndingCache = aRight / IndexIncrement;
      int lCachesRequired = lEndingCache - lStartingCache + 1; // this many caches.
      int lLeftPosition = aLeft; // starting position

      int lCurrentCachePosition = lStartingCache;
      for ( int i = 0; i < lCachesRequired; i++ ) // loop through the total number of caches required.
      {
        int lRightPosition = aRight < ( lCurrentCachePosition + 1 ) * IndexIncrement ?
          aRight : ( ( lCurrentCachePosition + 1 ) * IndexIncrement ) - 1;

        MappingCache lAppropriateCache = GetCorrectCache( lContigIdent, lLeftPosition, lRightPosition );
        lResult.AddRange( lAppropriateCache.GetReads( lLeftPosition, lRightPosition ) );

        // these only matter if we do end up going to the next cache.
        lCurrentCachePosition++;
        lLeftPosition = lCurrentCachePosition * IndexIncrement; // move the left pointer over to the beginning of the next one.
      }

      return lResult;
    }

    internal MappingCache GetCorrectCache( string aContigIdent, int aLeft, int aRight )
    {
      if ( CacheA != null && CacheA.ContigIdent == aContigIdent && CacheA.LeftIndex <= aLeft && CacheA.RightIndex >= aLeft )
        return CacheA;
      else if ( CacheB != null && CacheB.ContigIdent == aContigIdent && CacheB.LeftIndex <= aLeft && CacheB.RightIndex >= aLeft )
        return CacheB;
      else
      {
        RebuildCaches( aContigIdent, aLeft ); // neither of them have it.
        return GetCorrectCache( aContigIdent, aLeft, aRight ); // check again.
      }
    }

    private void RebuildCaches( string aContigIdent, int aLeft ) // these define the left edge of the problem.
    {
      int lStartingIndex = ( aLeft / IndexIncrement );

      CacheA = RebuildCache( aContigIdent, lStartingIndex );

      if ( lStartingIndex + 1 < mFilePositions.Count )
        CacheB = RebuildCache( aContigIdent, lStartingIndex + 1 );
      else
        CacheB = null;

    }

    private MappingCache RebuildCache( string aContigIdent, int lStartingIndex )
    {
      long lStartingPos = mFilePositions[ aContigIdent ][ lStartingIndex ];

      long lCount;
      if ( lStartingIndex + 1 < mFilePositions[ aContigIdent ].Count )
        lCount = mFilePositions[ aContigIdent ][ lStartingIndex + 1 ] - lStartingPos;
      else
        lCount = mEndOfFilePos - lStartingPos;

      byte[] lBlock = new byte[ lCount ];
      mStream.Position = lStartingPos;
      mStream.BaseStream.Read( lBlock, 0, ( int ) lCount );

      MappingCache lCache = BuildCache( lBlock, aContigIdent, lStartingIndex * IndexIncrement, ( ( lStartingIndex + 1 ) * IndexIncrement ) - 1 );

      return lCache;
    }

    public virtual MappingCache BuildCache( byte[] lBlock, string aContigIdent, int aLeft, int aRight )
    {
      List<List<Mapping>> lMappings = new List<List<Mapping>>();
      for ( int i = aLeft; i <= aRight; i++ )
      {
        lMappings.Add( null );
      }

      StreamReader lStream = new StreamReader( new MemoryStream( lBlock ) );

      while ( lStream.Peek() >= 0 )
      {
        string lLine = lStream.ReadLine();

        int lIndex = GetIndex( lLine );
        int lPrivateIndex = lIndex - aLeft;

        if ( lMappings[ lPrivateIndex ] == null )
          lMappings[ lPrivateIndex ] = new List<Mapping>();

        lMappings[ lPrivateIndex ].Add( new Mapping( lIndex, new Sequence( GetSequence( lLine ) ) ) );
      }

      return new MappingCache( lMappings, aContigIdent, aLeft, aRight );

    }

    public abstract string GetSequence( string aLine );

    public abstract int GetIndex( string aLine );

    public abstract string GetContigIdent( string aLine );

    public static string[] GetLinePieces( string aLine )
    {
      return aLine.Split( Tab );
    }


  }
}
