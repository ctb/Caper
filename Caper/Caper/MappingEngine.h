#pragma once

#include <sstream>
#include <iostream>
#include "stdafx.h"
#include "Sequence.h"
#include "Typedefs.h"
#include "MappingCache.h"

class MappingEngine
{
  static const int IndexIncrement = 1000;
  static const char Tab = '\t';
  static const char NewLine = '\n';

private:
	Sequences * ReferenceGenome;

  MappingCache* CacheA;
  MappingCache* CacheB;
	
	long mEndOfFilePosition;
	string mPath;
  bool mDOSDelimiter;

	map<string, vector<long>> mMappingIndexes;
	map<string, int> mNumberOfWindows;
	map<string, pair<long,long>> mContigBorders;
	vector<string> mSortedContigIdents;	

private:
	void PopulateMappingIndex();	
	void PopulateContigBorders();
	void PopulateNumberOfWindows();
	void PopulateSortedContigIdents();
	void PopulateReadInformation();
  
	MappingCache * GetCorrectCache( string lContigIdent, int aLeft, int aRight );
	void RebuildCaches( string lContigIdent, int aLeft );
	MappingCache * RebuildCache( string aContigIdent, int aLeft );
	MappingCache * BuildEmptyCache( string aContigIdent, int aLeft, int aRight );
	MappingCache * BuildCache( char * aBlock, string aContigIdent, int aLeft, int aRight );

	virtual string GetSequence( string & aLine ) = 0;
	virtual int GetIndex( string & aLine ) = 0;
	virtual string GetContigIdent( string & aLine ) = 0;

public:
	map<string, int> NumberOfReads;
  int ReadLength;
	
public:
	MappingEngine( string & aPath, Sequences & aReferenceGenome );
  Mappings * GetReads(string lContigIdent, int aLeft, int aRight );

  void Initialize();
};