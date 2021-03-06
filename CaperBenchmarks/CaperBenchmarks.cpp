#include "CaperBenchmarks.h"

void CaperBenchmarks::Start(int argc, char * const argv[] )
{
  string lUsageString = "CaperBenchmarks v0.1\nUsage: CaperBenchmarks [-s SaveIndexesToPath] [-i SavedMappingIndexFile] [-f SavedReferenceGenomeIndexFile] <-g|-G> <referencegenome.fa> <-m|-M|-b|-B> <mappingsfile>";

  string lCommandString = "You can type: \"<contig ident>:<X>:<Y>\", or \"<contig ident>:<X>:<Y>:p\" (for pretty mode)";

  try
  {
    Arguments lArgs;
    if ( !lArgs.ProcessArguments( argc, argv ) )
    {
      cout << lUsageString << endl;
      return;
    }

    cout << "Reading Genome \"" << lArgs.GenomePath << "\"... ";
    cout.flush();

    time_t lStartSeconds = time(NULL);
    time_t lInitStartSeconds = time(NULL);

    SequenceEngine * lSequenceEngine = new FASequenceEngine( lArgs.GenomePath );
    if ( lArgs.LoadReferenceGenomeIndex )
      lSequenceEngine->Initialize( lArgs.ReferenceGenomeIndexPath );    
    else
      lSequenceEngine->Initialize();
    
    if ( lArgs.SaveIndexes )
      lSequenceEngine->SaveIndex( lArgs.SavePath );
    
    time_t lEndSeconds = time(NULL);
    cout << "Done! - " << lEndSeconds - lStartSeconds << "s" << endl;

    if ( !lArgs.LoadMappings )
    {
      return;
    }

    cout << "Preparing Mappings \"" << lArgs.MappingPath << "\"... " << endl;
    cout.flush();

    lStartSeconds = time(NULL);
    MappingEngine * lMappingEngine;
    if ( lArgs.MappingStyle == lArgs.BOWTIE )
    {
      if ( !lArgs.MappingsSorted )
      {
        BowtieMappingsPreparer * lPrep = new BowtieMappingsPreparer( lArgs.MappingPath );
        string lNewPath = lPrep->PrepareMappings();
        delete lPrep;

        lMappingEngine = new BowtieMappingEngine( lNewPath, lSequenceEngine->mSequences );
      }
      else
        lMappingEngine = new BowtieMappingEngine( lArgs.MappingPath, lSequenceEngine->mSequences );
    }
    else if ( lArgs.MappingStyle == lArgs.MAPVIEW )
    {
      if ( !lArgs.MappingsSorted )
      {
        MapviewMappingsPreparer * lPrep = new MapviewMappingsPreparer( lArgs.MappingPath );
        string lNewPath = lPrep->PrepareMappings();
        delete lPrep;

        lMappingEngine = new MapviewMappingEngine( lNewPath, lSequenceEngine->mSequences );
      }
      else
        lMappingEngine = new MapviewMappingEngine( lArgs.MappingPath, lSequenceEngine->mSequences );
    }
    lEndSeconds = time(NULL);

    cout << "Done! - " << lEndSeconds - lStartSeconds << "s" << endl;

    cout << "Initializing Mapping Engine... " << endl;

    lStartSeconds = time(NULL);
    if ( lArgs.LoadMappingIndex )
      lMappingEngine->Initialize( lArgs.IndexPath );
    else
      lMappingEngine->Initialize();
    
    if ( lArgs.SaveIndexes )
      lMappingEngine->SaveMappingIndex( lArgs.SavePath );

    lEndSeconds = time(NULL);    
    time_t lInitEndSeconds = time(NULL);
	

    cout << "Done! - " << lEndSeconds - lStartSeconds << "s" << endl;
    cout << "Total Init Time: " << lInitEndSeconds - lInitStartSeconds << "s" << endl;

	// done init.

    if ( !lArgs.InteractiveMode )
      return;

    cout << "> ";

    string lInput = "";
    Commands lCommands;
    while ( cin >> lInput )
    {
      if ( !lCommands.ProcessArguments( lInput, lSequenceEngine->mSequences, lMappingEngine ) )
      {
        cout << "Invalid Input: " << lCommandString << endl << "> " ;
        continue;
      }

      if ( lCommands.Action == lCommands.GETREADS )
      {
        Mappings * lMappings = lMappingEngine->GetReads(lCommands.ContigIdent, lCommands.Left, lCommands.Right );
        if ( lCommands.PrettyMode ) // engage pretty mode
        {    
          cout << PadLeft( lMappingEngine->ReadLength ) << lCommands.Left + lMappingEngine->ReadLength << endl;        
          cout << PadLeft( lMappingEngine->ReadLength ) << PadLeft( lCommands.Right - lCommands.Left, "*") << endl;

          string lGenome = "";

          Sequence * lContig = (*lSequenceEngine->mSequences)[ lCommands.ContigIdent ];

          int lTargetGenomeWidth = lCommands.Right - lCommands.Left + 1 + lMappingEngine->ReadLength;
          if ( lTargetGenomeWidth < lContig->Length )
            lGenome = lContig->Substring( lCommands.Left, lTargetGenomeWidth );
          else
            lGenome = lContig->Substring( lCommands.Left );

          cout << lGenome << endl; 

          int lGenomeLength = lGenome.length();
          for ( int i = 0 ; i < lMappings->size(); i++ ) 
          {
            string lHighlightedString = lMappings->at(i)->mSequence->ToString();
            for ( int j = 0; j < lHighlightedString.length(); j++ )
            {
              int lTargetLocalIndexOnGenome = lMappings->at(i)->Index - lCommands.Left + j;
              if ( lTargetLocalIndexOnGenome < lGenome.length() && 
                lHighlightedString[j] != lGenome[ lTargetLocalIndexOnGenome ] )
              {
                if ( islower(lHighlightedString[j]) )
                  lHighlightedString[j] = toupper( lHighlightedString[j] );
                else
                  lHighlightedString[j] = tolower( lHighlightedString[j] );
              }
            }

            cout << PadLeft( lMappings->at(i)->Index - lCommands.Left ) << lHighlightedString << "\n"; 
          }
        }
        else
        {
	clock_t lStart, lEnd;
	lStart = clock();
          for ( int i = 0 ; i < lMappings->size(); i++ )
          {
            cout << "~Index " << lMappings->at(i)->Index << ": " << lMappings->at(i)->Name << " - " << lMappings->at(i)->mSequence->ToString() << "\n";
          }
        lEnd = clock();
        cout << "Elapsed: " <<  ( lEnd - lStart ) / ( CLOCKS_PER_SEC / 1000 ) << "ms" << endl;

        }

        delete lMappings;
      }

      cout << "> ";
    }
  }
  catch( string lException )
  {
    cerr << "ERROR: " << lException << endl;
    return;
  }
}


string CaperBenchmarks::PadLeft( int aCount )
{
  return PadLeft( aCount, " " );
}

string CaperBenchmarks::PadLeft( int aCount, string aPad )
{
  string lThing = "";

  for (int i = 0; i < aCount; i++ )
    lThing.append(aPad);

  return lThing;
}


