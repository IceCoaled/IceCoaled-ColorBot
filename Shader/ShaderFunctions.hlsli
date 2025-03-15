#include "ShaderDefines.hlsli"


///---------Utility-Functions--------///


// These functions are used to check a thread id, group id or local( group thread ) id
// Against a target, this is done to block out threads, groups or group threads
// They are all defaulted to 0,0 for ease, but can be changed to any value
inline bool IsTargetThread( const uint2 threadId, const uint2 target )
{
	return all( threadId == target );
}

inline bool IsTargetGroup( const uint2 groupId, const uint2 target )
{
	return all( groupId == target );
}

inline bool IsTargetGroupThread( const uint2 localId, const uint2 target )
{
	return all( localId == target );
}

// This function is used to check if a pixel thread is outside the texture
inline bool IsOutsideTexture( const uint2 threadId )
{
	return all( threadId > uint2( WINDOW_SIZE_X, WINDOW_SIZE_Y ) );
}

inline bool IsInHudBlock( const uint threadIdY )
{
	return threadIdY > BOTTOM_HUD_BLOCK;
}

// A helper function to check if a pixel is within 3 pixels 
// Of a set of min and max positions
inline bool IsWithin3PixelBorder( const uint2 pos, const uint2 minPos, const uint2 maxPos )
{
    // Inside the bounding box:
	bool result = false;

	if ( ( ( pos.x >= minPos.x ) & ( pos.x <= maxPos.x ) ) && 
    ( ( pos.y >= minPos.y ) & ( pos.y <= maxPos.y ) ) )
	{
        // Near left or right edge                                              
		if ( ( ( pos.x - minPos.x ) <= 2 | ( maxPos.x - pos.x ) <= 2 ) || 
        ( ( ( pos.y - minPos.y ) <= 2 ) | ( maxPos.y - pos.y ) <= 2 ) )  //< Near top or bottom edge
		{
			result = true;
		}
	}
	return result;
}


// Verifies if the pixel is within the scan box
inline bool IsWithinScanBox( const uint2 threadId )
{
	return all( threadId >= SCAN_BOX_MIN ) && all( threadId <= SCAN_BOX_MAX );
}

inline bool IsGroupOverScanBox( const uint2 groupId )
{
	return !( groupId.x * X_THREADGROUP + X_THREADGROUP <= SCAN_BOX_MIN.x ||
      groupId.x * X_THREADGROUP >= SCAN_BOX_MAX.x ||
      groupId.y * Y_THREADGROUP + Y_THREADGROUP <= SCAN_BOX_MIN.y ||
      groupId.y * Y_THREADGROUP >= SCAN_BOX_MAX.y );
}

// Checks to see if pixel is empty using a threshold
// That is the default functionality but you can change the epsilon
// and use to check the pixel is under a certain value for each swizzle
// This is specifically checking each swizzle as i ran into a problem previously
// Trying to use all / any for checking if the pixel read from the texture was successful
// I.E out of bounds was causing massive issues with false positives
inline bool IsZero( float4 color, float epsilon = 0.00001 )
{
    return abs( color.x ) < epsilon &&
           abs( color.y ) < epsilon &&
           abs( color.z ) < epsilon &&
           abs( color.w ) < epsilon;
}

inline bool IsZero( uint4 target, float epsilon = 0.00001 )
{
    return int( target.x ) < epsilon &&
           int( target.y ) < epsilon &&
           int( target.z ) < epsilon &&
           int( target.w ) < epsilon;
}


// Calculates the distance between two points and checks if it's within a threshold
inline bool DetectGrouping( const uint2 pos1, const uint2 pos2, const float epsilon )
{
    return abs( distance( float2( pos1 ), float2( pos2 ) ) ) <= epsilon;
}

// These functions are for creating a uint4 vector and then checking the resulting vector
// this is done to reduce branching
inline bool OutlinesCmpHelper( const uint4 name )
{
    return IsZero( name - COLOR_NAME_OUTLNZ );
}

inline bool HairCmpHelper( const uint4 name )
{
    return IsZero( name - COLOR_NAME_HAIR );
}


// Gets the pixel classification for group class matrix from range name
uint GetPixelClassFromName( const uint4 name )
{
    uint result = NO_MATCHING_NAME;
    if ( OutlinesCmpHelper( name ) )
    {
        result = PX_OUTLINE;
    }
    else if ( HairCmpHelper( name ) )
    {
        result = PX_HAIR;
    }
    return result;
}

inline bool CompareNames( const uint4 name1, const uint4 name2 )
{
    bool result = false;
    if ( IsZero( name1 - name2 ) )
    {
        result = true;
    }
    return result;
}

///--------------Group-Clusters-Buffer-Functions----------------///


inline uint GetGroupStatus( const uint2 localId, const uint groupDataIndex )
{
	const int bitIndex = ( localId.x & ( GROUP_DATA_PER_UINT - 1 ) ) << GROUP_DATA_PER_BYTE;
	const uint pxData = GroupDataBuffer [ groupDataIndex ].statusPxlType [ localId.x >> GROUP_DATA_PER_BYTE ] [ localId.y ];
	return ( pxData >> bitIndex ) & BITS2_MASK;
}

inline uint GetGroupPixelType( const uint2 localId, const uint groupDataIndex )
{
	const int bitIndex = ( ( localId.x & ( GROUP_DATA_PER_UINT - 1 ) ) << GROUP_DATA_PER_BYTE ) + GROUP_DATA_PER_BYTE;
	uint pxData = GroupDataBuffer [ groupDataIndex ].statusPxlType [ localId.x >> GROUP_DATA_PER_BYTE ] [ localId.y ];
	return ( pxData >> bitIndex ) & BITS2_MASK;
}

inline void SetGroupStatus( const uint status, const uint2 localId, const uint groupDataIndex )
{
	const int bitIndex = ( gmGroupDataIndex & ( GROUP_DATA_PER_UINT - 1 ) ) << GROUP_DATA_PER_BYTE;
	InterlockedAnd( GroupDataBuffer [ groupDataIndex ].statusPxlType [ localId.x >> GROUP_DATA_PER_BYTE ] [ localId.y ], ~( BITS2_MASK << bitIndex, DUMMY_UINT ) );
	InterlockedOr( GroupDataBuffer [ groupDataIndex ].statusPxlType [ localId.x >> GROUP_DATA_PER_BYTE ] [ localId.y ], ( status & BITS2_MASK ) << bitIndex, DUMMY_UINT );
}

inline void SetGroupPixelType( const uint pixelType, const uint2 localId, const uint groupDataIndex )
{
	const int bitIndex = ( ( localId.x & ( GROUP_DATA_PER_UINT - 1 ) ) << GROUP_DATA_PER_BYTE ) + GROUP_DATA_PER_BYTE;
	InterlockedAnd( GroupDataBuffer [ groupDataIndex ].statusPxlType [ localId.x >> GROUP_DATA_PER_BYTE ] [ localId.y ], ~( BITS2_MASK << bitIndex, DUMMY_UINT ) );
	InterlockedOr( GroupDataBuffer [ groupDataIndex ].statusPxlType [ localId.x >> GROUP_DATA_PER_BYTE ] [ localId.y ], ( pixelType & BITS2_MASK ) << bitIndex, DUMMY_UINT );
}

inline int GetGroupDataIndex( const uint2 groupId )
{
	int2 groupPos;	
	groupPos.x = ( groupId.x * X_THREADGROUP ) - FIRST_GROUP_OVERLAP.x;
	groupPos.y = ( groupId.y * Y_THREADGROUP ) - FIRST_GROUP_OVERLAP.y;
	return ( groupPos.y * SCAN_GROUPS_X ) + groupPos.x;
}

inline void SetCluster( const uint groupDataIndex )
{
	const uint allowance = uint( lerp( 5.0, 20.0, saturate( float( ( gmHairCluster.outlinePos.z - gmHairCluster.outlinePos.x ) - MIN_HEAD_SIZE / ( MAX_HEAD_SIZE - MIN_HEAD_SIZE ) ) ) ) );
	InterlockedExchange( GroupDataBuffer [ groupDataIndex ].hairCentroid.allowance, allowance, DUMMY_UINT );
	InterlockedExchange( GroupDataBuffer [ groupDataIndex ].hasCluster, CLUSTER_AVAILABLE, DUMMY_UINT );
	InterlockedExchange( GroupDataBuffer [ groupDataIndex ].hairCentroid.outlinePos.x, gmHairCluster.outlinePos.x, DUMMY_UINT );
	InterlockedExchange( GroupDataBuffer [ groupDataIndex ].hairCentroid.outlinePos.y, gmHairCluster.outlinePos.y, DUMMY_UINT );
	InterlockedExchange( GroupDataBuffer [ groupDataIndex ].hairCentroid.outlinePos.z, gmHairCluster.outlinePos.z, DUMMY_UINT );
	InterlockedExchange( GroupDataBuffer [ groupDataIndex ].hairCentroid.outlinePos.w, gmHairCluster.outlinePos.w, DUMMY_UINT );
	InterlockedExchange( GroupDataBuffer [ groupDataIndex ].hairCentroid.clusterSize, gmHairCluster.clusterSize, DUMMY_UINT );
}


///---------------Scan-Box-Buffer-Functions----------------///


// Helper function to set the scan box byte value to 0
inline void ClearScanBoxByte( const int byteIndex )
{
	if ( byteIndex >= 0 && byteIndex < MAX_SCAN_BOX_BUFFER_INDEX )
	{
		// Sets all bits in the byte to 0
		ScanBoxBuffer.InterlockedAnd( byteIndex, ~( MAX_BYTE ), DUMMY_UINT );
	}
}


// This function is used to get the index data for the pixel in the scan box data buffer
inline void GetScanBoxIndexing( const uint2 threadId, inout int pixelIndex, inout int bitIndex, inout int byteIndex )
{
	pixelIndex = ( ( threadId.y - SCAN_BOX_MIN.y ) * SCAN_FOV.x ) + ( threadId.x - SCAN_BOX_MIN.x );
	bitIndex = ( ( pixelIndex & ( CLASSIFICATIONS_PER_UINT - 1 ) ) << 1 );
    
	if ( pixelIndex < CLASSIFICATIONS_PER_UINT )
	{
		byteIndex = bitIndex >> 2;
	}
	else
	{
		byteIndex = ( pixelIndex >> 4 ) << 2;
	}    
}

// Read classification from scan box matrix
inline uint ReadScanBoxClass( const int bitPos, const int byteIndex )
{
	uint result = MAX_UINT;
	if ( byteIndex >= 0 && byteIndex < MAX_SCAN_BOX_BUFFER_INDEX )
	{
		result = ( ScanBoxBuffer.Load( byteIndex ) >> bitPos ) & BITS2_MASK;
	}		
	return result;
}


// Write classification to scan box matrix
// this is a trick to write the classification to the scan box
// Without needing to use store or sync operations
// We clear the bits at the position we want to write to
// Then we set the bits to the classification we want to write
inline void WriteScanBoxClass( const uint classification, const int bitPos, const int byteIndex )
{
	if ( byteIndex >= 0 && byteIndex < MAX_SCAN_BOX_BUFFER_INDEX )
	{
		ScanBoxBuffer.InterlockedAnd( byteIndex, ~( BITS2_MASK << bitPos ), DUMMY_UINT );
		ScanBoxBuffer.InterlockedOr( byteIndex, ( classification & BITS2_MASK ) << bitPos, DUMMY_UINT );
	}
}

// Helper to verify if a label is the same as the pixel type
inline bool VerifyScanBoxPixel( const int bitPos, const int byteIndex, const uint pixelType )
{
	return ReadScanBoxClass( bitPos, byteIndex ) == pixelType;
}


// Overload of the above function that takes a pixel index
// this is optimized for loops that scan the scan box
inline bool VerifyScanBoxPixel( const uint2 threadId, inout int pixelIndex, inout int bitIndex, inout int byteIndex, const uint pixelType )
{  
	pixelIndex = 0;
    bitIndex = 0;
	byteIndex = 0;	
	GetScanBoxIndexing( threadId, pixelIndex, bitIndex, byteIndex );
	return ReadScanBoxClass( bitIndex, byteIndex ) == pixelType;
}

///-----Detected-Players-Buffer-Functions------///

// This function initalizes the global scanbox bounding box
// For the detected players to the values needed to properly
// Min / Max for the bounding box
// Always set the max values to the minimum possible value
// And the min values to the maximum possible value
inline void InitializeDPMaxMin()
{
	InterlockedExchange( PlayerPositionBuffer [ 0 ].scanBoxBB.bbMax.x, MIN_UINT, DUMMY_UINT );
	InterlockedExchange( PlayerPositionBuffer [ 0 ].scanBoxBB.bbMax.y, MIN_UINT, DUMMY_UINT );
	InterlockedExchange( PlayerPositionBuffer [ 0 ].scanBoxBB.bbMin.x, MAX_UINT, DUMMY_UINT );
	InterlockedExchange( PlayerPositionBuffer [ 0 ].scanBoxBB.bbMin.y, MAX_UINT, DUMMY_UINT );
}

inline uint CheckMinMax()
{
	uint2 min = PlayerPositionBuffer [ 0 ].scanBoxBB.bbMin;
	const uint2 max = PlayerPositionBuffer [ 0 ].scanBoxBB.bbMax;
	if ( !IsWithinScanBox( min ) || !IsWithinScanBox( max ) )
	{
		min.x = STATUS_NO_MIN_MAX;
	}
	else
	{
		min.x = STATUS_OK;
	}
	return min.x;
}

///---------------Main-Shader-Functions----------------///


// This is a helper function if no hair centroids are detected
// We just assume there is 1 player and set the player details
// It will only be ran by thread 0,0
void NoPlayersDetected( const uint2 bbMin, const uint2 bbMax )
{
	// Create actual bounding box
	// Manually initialise so we know we are setting the correct values
	uint4x2 bb;
	bb [ 0 ] = bbMin;
	bb [ 1 ] = uint2( bbMax.x, bbMin.y );
	bb [ 2 ] = uint2( bbMin.x, bbMax.y );
	bb [ 3 ] = bbMax;
	
	// Calculate the offset from top of boundning box to head
	uint headOffset = uint( floor( lerp( 1, 25, ( float( bbMax.x - bbMin.x ) / MAX_BODY_WIDTH ) ) ) );
	
	// Calculate head and torso positions
	uint2 headPos = uint2( ( bbMin.x + bbMax.x ) / 2, ( bb [ 0 ].y + headOffset ) );
	uint2 torsoPos = ( ( bbMin + bbMax ) / 2 );
		
	// Set the player details
	InterlockedExchange( PlayerPositionBuffer [ 0 ].players [ 0 ].headPosition.x, headPos.x, DUMMY_UINT );
	InterlockedExchange( PlayerPositionBuffer [ 0 ].players [ 0 ].headPosition.y, headPos.y, DUMMY_UINT );
	InterlockedExchange( PlayerPositionBuffer [ 0 ].players [ 0 ].bodyPosition.x, torsoPos.x, DUMMY_UINT );
	InterlockedExchange( PlayerPositionBuffer [ 0 ].players [ 0 ].bodyPosition.y, torsoPos.y, DUMMY_UINT );
	InterlockedExchange( PlayerPositionBuffer [ 0 ].players [ 0 ].boundingBox [ 0 ].x, bbMin.x, DUMMY_UINT );
	InterlockedExchange( PlayerPositionBuffer [ 0 ].players [ 0 ].boundingBox [ 0 ].y, bbMin.y, DUMMY_UINT );
	InterlockedExchange( PlayerPositionBuffer [ 0 ].players [ 0 ].boundingBox [ 0 ].x, bbMax.x, DUMMY_UINT );
	InterlockedExchange( PlayerPositionBuffer [ 0 ].players [ 0 ].boundingBox [ 0 ].y, bbMin.y, DUMMY_UINT );
	InterlockedExchange( PlayerPositionBuffer [ 0 ].players [ 0 ].boundingBox [ 0 ].x, bbMin.x, DUMMY_UINT );
	InterlockedExchange( PlayerPositionBuffer [ 0 ].players [ 0 ].boundingBox [ 0 ].y, bbMax.y, DUMMY_UINT );
	InterlockedExchange( PlayerPositionBuffer [ 0 ].players [ 0 ].boundingBox [ 0 ].x, bbMax.x, DUMMY_UINT );
	InterlockedExchange( PlayerPositionBuffer [ 0 ].players [ 0 ].boundingBox [ 0 ].y, bbMax.y, DUMMY_UINT );
	InterlockedAdd( PlayerPositionBuffer [ 0 ].playerCount, 1 );
}

// This is a helper function if only 1 hair centroid is detected
// This will only be ran by thread 0,0
void SinglePlayerDetected( const uint2 bbMin, const uint2 bbMax )
{
	// Create actual bounding box
	// Manually initialise so we know we are setting the correct values
	uint4x2 bb;
	bb [ 0 ] = bbMin;
	bb [ 1 ] = uint2( bbMax.x, bbMin.y );
	bb [ 2 ] = uint2( bbMin.x, bbMax.y );
	bb [ 3 ] = bbMax;
	
	// Calculate torso position
	uint2 torsoPos = ( ( bbMin + bbMax ) / 2 );
	
	// Set the player details
	InterlockedExchange( PlayerPositionBuffer [ 0 ].players [ 0 ].bodyPosition.x, torsoPos.x, DUMMY_UINT );
	InterlockedExchange( PlayerPositionBuffer [ 0 ].players [ 0 ].bodyPosition.y, torsoPos.y, DUMMY_UINT );
	InterlockedExchange( PlayerPositionBuffer [ 0 ].players [ 0 ].boundingBox [ 0 ].x, bbMin.x, DUMMY_UINT );
	InterlockedExchange( PlayerPositionBuffer [ 0 ].players [ 0 ].boundingBox [ 0 ].y, bbMin.y, DUMMY_UINT );
	InterlockedExchange( PlayerPositionBuffer [ 0 ].players [ 0 ].boundingBox [ 0 ].x, bbMax.x, DUMMY_UINT );
	InterlockedExchange( PlayerPositionBuffer [ 0 ].players [ 0 ].boundingBox [ 0 ].y, bbMin.y, DUMMY_UINT );
	InterlockedExchange( PlayerPositionBuffer [ 0 ].players [ 0 ].boundingBox [ 0 ].x, bbMin.x, DUMMY_UINT );
	InterlockedExchange( PlayerPositionBuffer [ 0 ].players [ 0 ].boundingBox [ 0 ].y, bbMax.y, DUMMY_UINT );
	InterlockedExchange( PlayerPositionBuffer [ 0 ].players [ 0 ].boundingBox [ 0 ].x, bbMax.x, DUMMY_UINT );
	InterlockedExchange( PlayerPositionBuffer [ 0 ].players [ 0 ].boundingBox [ 0 ].y, bbMax.y, DUMMY_UINT );
}




// This uses trig to get the left lowest point based off right lowest point
void GetHairToFootAngle( const uint2 headPos, const uint2 threadId, const uint index )
{
    // Get degress of hair centroid to right lowest outline pixel
    // We have to get the degrees in order to get the opposite angle from the head
    // Atan2 returns radians
    float hcToPixelDegree = degrees( atan2( float( headPos.y - threadId.y ), float( headPos.x - threadId.x ) ) );
    
    // Set our angle
	InterlockedExchange( PlayerPositionBuffer [ 0 ].globals.DegHairToRightLow, int( hcToPixelDegree ), DUMMY_UINT );
    
    // Set opposite angle
	InterlockedExchange( PlayerPositionBuffer [ 0 ].globals.DegHairToLeftLow, int( -hcToPixelDegree ), DUMMY_UINT );

    // Set distance
	InterlockedExchange( PlayerPositionBuffer [ 0 ].globals.distance, int( floor( distance( float2( headPos ), float2( threadId ) ) ) ), DUMMY_UINT );
    
    // Set search plane for left side
	InterlockedExchange( PlayerPositionBuffer [ 0 ].globals.ySearchPlaneLane.x, threadId.y - 4, DUMMY_UINT );
	InterlockedExchange( PlayerPositionBuffer [ 0 ].globals.ySearchPlaneLane.y, threadId.y + 4, DUMMY_UINT );
}

// This loops through hair centroid data to get the most right
// Hair centroid, as we work through them we cancel them out
uint3 GetFarRightPlayer( const int previousWorked [ MAX_PLAYERS ], bool first = true )
{
    // Current centroid
    uint2 centroidPos = uint2( MIN_UINT, MIN_UINT );
    
    // Player index
    int index = 0;
    
    // previous worked flag
    bool skip = false;
    
    // Loop through player centroids
    [allow_uav_condition]
    for ( int i = 0; i < int( MAX_PLAYERS ); ++i, skip = false )
    {
        if ( !first )
        {
            for ( uint n = 0; n < MAX_PLAYERS; ++n )
            {
                if ( i == previousWorked [ n ] )
                {
                    skip = true;
                    break;
                }
            }
            if ( skip )
            {
                continue;
            }
        } 
        uint2 hairCentroid = PlayerPositionBuffer[ 0 ].players [ i ].headPosition;
        centroidPos = max( centroidPos, hairCentroid );
        index = max( index, i );
    }
    
    return uint3( centroidPos, index );
}



// Gets and sets detected player info
// We use pixel type for thread blocking this way we know all threads 
// Will be running this function so we can directly incorporate
// Memory barriers
void GetSetPlayerDetails( const uint2 threadId, const uint pixelType, const uint2 min, const uint2 max, const uint playerCount )
{

	
    // Setup array for previous pplayer index's we already 
    // Set the data for
	int previousWorked [ MAX_PLAYERS ];
	for ( uint j = 0; j < MAX_PLAYERS; ++j )
	{
		previousWorked [ j ] = -1;
	}
        
        
    // Loop through each hair centroid / player
    [allow_uav_condition]
	for ( uint i = 0; i < MAX_PLAYERS; ++i )
	{
		if ( i >= playerCount )
		{
			break;
		}
            
        // Get furthest right hair centroid in global bounding box
		uint3 player = GetFarRightPlayer( previousWorked, ( i == 0 ) );
            
		if ( pixelType == PX_OUTLINE && threadId.x > player.x && threadId.x <= max.x &&
            threadId.y > player.y && threadId.y <= max.y )
		{
            // If this threads pixel cords is between hair centroid
            // And global bounding box max
            // Set its coords into global variable
            // For other threads to use max operation 
            // So we truly get the very right bottom of the target outline   
			InterlockedMax( PlayerPositionBuffer [ 0 ].globals.rightLowestPoint.x, threadId.x );
			InterlockedMax( PlayerPositionBuffer [ 0 ].globals.rightLowestPoint.y, threadId.y );
		}
            
        // Sync for buffer writes
		AllMemoryBarrier();
                
		if ( pixelType == PX_OUTLINE )
		{
            // If we are the thread that is the right bottom of the outline                                                                                                                                                                                                                      
            // We setup some of the bounding box details
			const uint2 maxRightLow = PlayerPositionBuffer [ 0 ].globals.rightLowestPoint;
			if ( DetectGrouping( maxRightLow, threadId.xy, 10.0 ) )
			{
				InterlockedExchange( PlayerPositionBuffer [ 0 ].players [ player.z ].boundingBox [ 3 ].x, max.x, DUMMY_UINT );
				InterlockedExchange( PlayerPositionBuffer [ 0 ].players [ player.z ].boundingBox [ 3 ].y, max.y, DUMMY_UINT );
				InterlockedExchange( PlayerPositionBuffer [ 0 ].players [ player.z ].boundingBox [ 1 ].x, max.x, DUMMY_UINT );
                // Then we get the left bottom of the target outline
				GetHairToFootAngle( player.xy, threadId.xy, player.z );
			}
		}
           
		const int hcToPixelDegree = int( floor( degrees( atan2( float( player.y - threadId.y ), float( player.x - threadId.x ) ) ) ) );
		const uint2 searchPlane = PlayerPositionBuffer [ 0 ].globals.ySearchPlaneLane;
		if ( pixelType == PX_OUTLINE && threadId.y >= searchPlane.x && threadId.y <= searchPlane.y )
		{
            // If we are in y plan range and within 5 degrees of set angle                             
			const int hcToPixeldistance = int( floor( distance( float2( player.xy ), float2( threadId ) ) ) );
			const int rightDistance = PlayerPositionBuffer [ 0 ].globals.distance;
			const int degToMatch = PlayerPositionBuffer [ 0 ].globals.DegHairToLeftLow;
			if ( ( abs( degToMatch ) - abs( hcToPixelDegree ) <= 5 ) && ( hcToPixeldistance <= ( rightDistance - 10 ) )
            && ( hcToPixeldistance >= ( rightDistance + 10 ) ) )
			{
                // degree reduction to get closest pixel
				InterlockedMin( PlayerPositionBuffer [ 0 ].globals.leftReductionDegree, hcToPixelDegree );
			}
		}

        // Sync for buffer writes
		AllMemoryBarrier();
        
		int setDegree = PlayerPositionBuffer [ 0 ].globals.leftReductionDegree;
		if ( pixelType == PX_OUTLINE && ( abs( setDegree - hcToPixelDegree ) >= 1 ) && ( abs( setDegree - hcToPixelDegree ) <= 10 ) )
		{
            // If we are the left bottom of the target outline              
            // Set our pixel coord info, really just for debug
			InterlockedExchange( PlayerPositionBuffer [ 0 ].globals.leftLowestPoint.x, threadId.x, DUMMY_UINT );
			InterlockedExchange( PlayerPositionBuffer [ 0 ].globals.leftLowestPoint.y, threadId.y, DUMMY_UINT );
                 
            // Set bounding box details
			InterlockedExchange( PlayerPositionBuffer [ 0 ].players [ player.z ].boundingBox [ 2 ].x, threadId.x, DUMMY_UINT );
			InterlockedExchange( PlayerPositionBuffer [ 0 ].players [ player.z ].boundingBox [ 2 ].y, threadId.y, DUMMY_UINT );
			InterlockedExchange( PlayerPositionBuffer [ 0 ].players [ player.z ].boundingBox [ 0 ].x, threadId.x, DUMMY_UINT );
			uint headOffset = uint( floor( lerp( 1, 25, ( float( threadId.x - PlayerPositionBuffer [ 0 ].globals.rightLowestPoint.x ) / MAX_BODY_WIDTH ) ) ) );
			InterlockedExchange( PlayerPositionBuffer [ 0 ].players [ player.z ].boundingBox [ 1 ].y, ( player.y + headOffset ), DUMMY_UINT );
			InterlockedExchange( PlayerPositionBuffer [ 0 ].players [ player.z ].boundingBox [ 0 ].y, ( player.y + headOffset ), DUMMY_UINT );
            
			uint4x2 bb = PlayerPositionBuffer [ 0 ].players [ player.z ].boundingBox;
			uint2 torso = ( bb._m00_m01 + bb._m30_m31 ) / 2;
			InterlockedExchange( PlayerPositionBuffer [ 0 ].players [ player.z ].bodyPosition.x, torso.x, DUMMY_UINT );
			InterlockedExchange( PlayerPositionBuffer [ 0 ].players [ player.z ].bodyPosition.y, torso.y, DUMMY_UINT );
                
            // Reduce global bounding box to exclude detected target
			InterlockedMin( PlayerPositionBuffer [ 0 ].scanBoxBB.bbMax.x, threadId.x - 4 );
		}

        // Add hair centroid index to worked array
		previousWorked [ player.z ] = player.z;
	}
	
}



// Create a bounding box around all outline pixels detected
void CreateBoundingBox( const uint2 threadId, const uint2 localId )
{
    
    // Flag for final merge
	bool threadUsed = false;

    // Single initialisation of variables
    // For loops
	int sbPixelIndex;
	int sbBitPos;
	int sbByteIndex;
    
    // Top or bottom flag
	uint topBottom = ( ( threadId.y > SCAN_BOX_MAX.y - ( SCAN_FOV.y / 2 ) ) && ( threadId.y <= SCAN_BOX_MAX.y ) && ( threadId.x == SCAN_BOX_MAX.x ) ) ? BOTTOM_BOX :
	( ( threadId.y >= SCAN_BOX_MIN.y ) && ( threadId.y <= SCAN_BOX_MAX.y - ( SCAN_FOV.y / 2 ) ) && ( threadId.x == SCAN_BOX_MIN.x ) ) ? TOP_BOX : 0;
    
    // Loop indices
	int start = ( topBottom == BOTTOM_BOX ) ? SCAN_FOV.x - 1 : 0;
	int end = ( topBottom == BOTTOM_BOX ) ? 0 : SCAN_FOV.x;
	int step = ( topBottom == BOTTOM_BOX ) ? -1 : 1;
	
	// Each thread's intermediate min/max for their row
	uint2 intermediateMM = ( topBottom  == BOTTOM_BOX ) ? uint2( MIN_UINT, threadId.y ) : uint2( MAX_UINT, threadId.y );
    
	if (  topBottom == TOP_BOX || topBottom == BOTTOM_BOX )
	{
		// Set our y position
		intermediateMM.y = threadId.y;		
		// Loop through the scan box
        // Top half goes left to right
        // Bottom half goes right to left
        [allow_uav_condition]
		for ( int x = start; ( topBottom == BOTTOM_BOX ) ? x >= end : x < end; x += step )
		{
			if ( VerifyScanBoxPixel( uint2( threadId.x + ( ( topBottom == BOTTOM_BOX ) ? -x : x ), threadId.y ), sbPixelIndex, sbBitPos, sbByteIndex, PX_OUTLINE ) )
			{
				threadUsed = true;	
				intermediateMM.x = ( topBottom == BOTTOM_BOX ) ? 
				max( intermediateMM.x, ( threadId.x - ( start - x ) ) ) :
				min( intermediateMM.x, ( threadId.x + x ) );
			}
		}
	}
    
    // Final min max merge of all threads
	if ( threadUsed && ( intermediateMM.x != MAX_UINT || intermediateMM.x != MIN_UINT ) )
	{
		start = ( topBottom == BOTTOM_BOX ) ? ( SCAN_BOX_MAX.y - ( SCAN_FOV.y / 2 ) ) : SCAN_BOX_MIN.y;
		end = ( topBottom == BOTTOM_BOX ) ? SCAN_BOX_MAX.y : ( SCAN_BOX_MIN.y + ( ( SCAN_FOV.y / 2 ) ) );
		step = 1;
        
        [allow_uav_condition]
		for ( int y = start; y < end; y += step )
		{
			if ( threadId.y == ( ( uint ) y ) )
			{
				if ( topBottom == BOTTOM_BOX )
				{
					InterlockedMax( PlayerPositionBuffer [ 0 ].scanBoxBB.bbMax.y, intermediateMM.y );
					InterlockedMax( PlayerPositionBuffer [ 0 ].scanBoxBB.bbMax.x, intermediateMM.x );
				}
				else
				{
					InterlockedMin( PlayerPositionBuffer [ 0 ].scanBoxBB.bbMin.y, intermediateMM.y );
					InterlockedMin( PlayerPositionBuffer [ 0 ].scanBoxBB.bbMin.x, intermediateMM.x );
				}
			}
		}
	}
}


// This function is used to merge all the hair centroids In group memory
// It will be ran by thread 0,0
inline void FinalMerge()
{
    // Player position buffer write index
	uint writeIdx = 0;
    // Value to track added players
	uint addedPlayers = 0;
     
    [allow_uav_condition]
	for ( uint i = 0; i < MAX_PLAYERS; ++i )
	{
		if ( i >= gmValidCount )
		{
			break;
		}
        
        
        [allow_uav_condition]   
		for ( uint j = i + 1; j < MAX_PLAYERS; ++j )
		{
			if ( j >= gmValidCount )
			{
				break;
			}
                    
            // If we find non mergeable hair centroid
            // We assume its a different player
			if ( !gmMergedClusters [ i ].CheckForMerge( gmMergedClusters [ j ].outlinePos ) )
			{
                // Add the current centroid to the player position buffer
				uint2 headPos = uint2(
                ( gmMergedClusters [ i ].outlinePos.x + gmMergedClusters [ i ].outlinePos.z ) / 2,
                ( gmMergedClusters [ i ].outlinePos.w + gmMergedClusters [ i ].outlinePos.y ) / 2 );
				
                if ( all( headPos ) && addedPlayers < ( MAX_PLAYERS - 1 ) )
				{
					InterlockedAdd( PlayerPositionBuffer [ 0 ].playerCount, 1, writeIdx );
					InterlockedExchange( PlayerPositionBuffer [ 0 ].players [ writeIdx ].headPosition.x, headPos.x, DUMMY_UINT );
					InterlockedExchange( PlayerPositionBuffer [ 0 ].players [ writeIdx ].headPosition.y, headPos.y, DUMMY_UINT );
					++addedPlayers;
				}
                
                // Push outter index to new cluster
                // Minus 1 because it will increment at the end of the loop
				i = j - 1;
				break;
			}
		}
	}
}


// This function is used to merge all the hair centroids
void MergeGroupHairCentroids( const uint2 localId )
{
    // Local variables
	HairCentroid current;
    // Flags for target checks
	bool3 targetChecks = bool3( false, false, false );
    // Write index for player position buffer
	uint writeIdx = 0;
	// Inner loop index
	uint j = 0;
    	

    // Calculate indices range
	uint indicesPerThread = ( ACTUAL_SCAN_GROUPS + THREAD_GROUP_SIZE - 1 ) / THREAD_GROUP_SIZE;
	uint threadIndex = localId.x + localId.y * X_THREADGROUP;
	// Calculate start and end indices
    uint startIdx = threadIndex * indicesPerThread;
	uint endIdx = ( threadIndex == THREAD_GROUP_SIZE - 1 ) ? ACTUAL_SCAN_GROUPS : min( startIdx + indicesPerThread, ACTUAL_SCAN_GROUPS );
    
    
    [allow_uav_condition]
	for ( uint i = startIdx; i < endIdx; ++i )
	{        
		if ( GroupDataBuffer [ i ].CheckForClusters() )
		{
			current = GroupDataBuffer [ i ].hairCentroid;
			if ( current.VerifyCentroid() )
			{
				targetChecks.y = true;
					
				[allow_uav_condition]
				for ( j = i + 1; j < endIdx; ++j )
				{
                 
					if ( !GroupDataBuffer [ j ].CheckForClusters() ||
                    !GroupDataBuffer [ j ].hairCentroid.VerifyCentroid() )
					{
						continue;
					}
					else if ( !current.CheckForMerge( GroupDataBuffer [ j ].hairCentroid.outlinePos ) )
					{
						targetChecks.x = true;
                        // Move index to new cluster
                        // We minus 1 to account for the increment at the end of the loop
						i = j - 1;
						break;
					}
						
				}
                
                // If we get to the end of the buffer chunk
                // With no matches we add the current centroid to the group
                // This also means we are done as there are no more centroids to merge
				if ( j >= endIdx )
				{
					targetChecks.z = true;
					targetChecks.x = true;
				}
			}
            
			if ( all( targetChecks.xy == bool2( true, true ) ) && gmValidCount < ( MAX_PLAYERS - 1 ) )
			{
				InterlockedAdd( gmValidCount, 1, writeIdx );
				if ( writeIdx < MAX_PLAYERS )
				{
					InterlockedExchange( gmMergedClusters [ writeIdx ].outlinePos.x, current.outlinePos.x, DUMMY_UINT );
					InterlockedExchange( gmMergedClusters [ writeIdx ].outlinePos.y, current.outlinePos.y, DUMMY_UINT );
					InterlockedExchange( gmMergedClusters [ writeIdx ].outlinePos.z, current.outlinePos.z, DUMMY_UINT );
					InterlockedExchange( gmMergedClusters [ writeIdx ].outlinePos.w, current.outlinePos.w, DUMMY_UINT );
					InterlockedExchange( gmMergedClusters [ writeIdx ].clusterSize, current.clusterSize, DUMMY_UINT );
					InterlockedExchange( gmMergedClusters [ writeIdx ].allowance, current.allowance, DUMMY_UINT );
				}
                
                // Break out of main loop
                // If the early exit flag is set
				if ( targetChecks.z )
				{
					break;
				}
                // Else we reset the target checks
                // And continue the loop
				targetChecks = bool3( false, false, false );
			}
		}
	}
}




// This function is used by hair threads to search for outline pixels
// If we find all 3 outline pixels we increment the cluster size
// Add our position to the group cluster info
// And set the min max outline locations
void OutlineSearch( const uint2 threadId, const uint2 localId )
{
    // For checking if we have all 3 outline pixels
	uint result = 0;
    
    // Single initialisation of variables
    // Used in the loops
	int sbPixelIndex;
	int sbBitPos;
	int sbByteIndex;
    
    // Outline locations
    // Used to get the width and top of the outline( for the head )
	uint3 outlineLocs = uint3( 0, 0, 0 );
    
    // Scan left
    [allow_uav_condition]
	for ( int i = 0; i < ( ( int ) MAX_SCAN_BOX_SIDE ); ++i )
	{
		if ( i >= int( threadId.x - SCAN_BOX_MIN.x ) )
		{
			break;
		}
        
            
		if ( VerifyScanBoxPixel( uint2( threadId.x - i, threadId.y ), sbPixelIndex, sbBitPos, sbByteIndex, PX_OUTLINE ) )
		{
			++result;
			outlineLocs.x = threadId.x - i;
			break;
		}
	}
    
	if ( result == 0 )
	{
		return;
	}

    
    // Scan right
    [allow_uav_condition]
	for ( int j = 0; j < ( ( int ) MAX_SCAN_BOX_SIDE ); ++j )
	{
		if ( j >= int( SCAN_BOX_MAX.x - threadId.x ) )
		{
			break;
		}
		
		if ( VerifyScanBoxPixel( uint2( threadId.x + j, threadId.y ), sbPixelIndex, sbBitPos, sbByteIndex, PX_OUTLINE ) )
		{
			++result;
			outlineLocs.z = threadId.x + j;
			break;
		}
	}
    
	if ( result < 2 )
	{
		return;
	}
    
    // Scan up
    [allow_uav_condition]
	for ( int k = 0; k < ( ( int ) MAX_SCAN_BOX_SIDE ); ++k )
	{
		if ( k >= int( threadId.y - SCAN_BOX_MIN.y ) )
		{
			break;
		}
        
		if ( VerifyScanBoxPixel( uint2( threadId.x, threadId.y - k ), sbPixelIndex, sbBitPos, sbByteIndex, PX_OUTLINE ) )
		{
			++result;
			outlineLocs.y = threadId.y - k;
			break;
		}
	}
    

    // If we have all 3 outline pixels
	if ( result == 3 && ( threadId.y - outlineLocs.y ) < ( MAX_HEAD_SIZE << 1 ) )
	{
		InterlockedAdd( gmHairCluster.clusterSize, 1 );
        // Min max outline locations
		InterlockedMin( gmHairCluster.outlinePos.y, outlineLocs.y );
		InterlockedMin( gmHairCluster.outlinePos.x, outlineLocs.x );
		InterlockedMax( gmHairCluster.outlinePos.z, outlineLocs.z );
		InterlockedMax( gmHairCluster.outlinePos.w, threadId.y );
	}
}





// Check if the pixel is within the range of any of the color ranges
// If it is we set the swap color and set the pixel classification
// Has a alignment check to make sure the buffer is aligned
// If not we abort the shader
uint CheckPixelColor( const uint2 localId, const unorm float4 pixelColor, const uint4 rangeName, inout unorm float4 swapColor )
{
    // Pixel classification
    uint pixelType = 0;
        
    // Range loop variable
    volatile uint colorIndex = 0;
    
    // Color tolerance loop variable 
    volatile uint rangeIndex = 0;
    
    [allow_uav_condition]
    while ( colorIndex < NUM_COLOR_RANGES )
    {
		const ColorRanges color = ColorRangeBuffer.Load( colorIndex );
        if ( color.CheckNull() )
        {
            pixelType = NULL_RANGE_ERROR;
        } else if ( !color.CheckAlignment() )
        {
			pixelType = ALIGNMENT_ERROR;
		}        
        // Make sure we are accessing the correct range
        else if ( CompareNames( color.colorName, rangeName ) )
        {
            // Make sure there is tolerances
            if ( !color.HasRanges())
            {
                pixelType = NO_RANGE_ERROR;
            }
            else
            {    
                [allow_uav_condition]
				while ( rangeIndex <  MAX_RANGE_SIZE )
                {
                    if ( rangeIndex >= color.numOfRanges )
                    {
                        break;
                    }
                    
                    if ( color.IsInRange( pixelColor, rangeIndex ) )
                    {
                        // Set output swap color to the skin swap color
                        swapColor = color.swapColor;
                         
                        // Set local shared matrix to desired pixel type                        
                        switch ( GetPixelClassFromName( color.colorName ) )
                        {
                            case PX_HAIR:
                                pixelType = PX_HAIR;
                                break;
                            case PX_OUTLINE:
                                pixelType = PX_OUTLINE;
                                break;
                            default:
                                pixelType = NO_MATCHING_NAME;
                                break;
                        }
                    }
                    
                    if ( pixelType != 0 )
                    {
                        break;
                    }
                    ++rangeIndex;
                }
            }
        }
        if ( pixelType != 0 )
        {
            break;
        }
        ++colorIndex;
    }
    
    if ( pixelType != 0 )
    {
        return pixelType;
    }
    
    return PX_BACKGROUND;
}


 
//--------------Group-Memory-Funcitons-----------------//

inline void InitGMCentroids( const uint2 localId )
{
	
	InterlockedExchange( gmMergedClusters [ localId.x ].outlinePos.x, 0, DUMMY_UINT );
	InterlockedExchange( gmMergedClusters [ localId.x ].outlinePos.y, 0, DUMMY_UINT );
	InterlockedExchange( gmMergedClusters [ localId.x ].outlinePos.z, 0, DUMMY_UINT );
	InterlockedExchange( gmMergedClusters [ localId.x ].outlinePos.w, 0, DUMMY_UINT );
	InterlockedExchange( gmMergedClusters [ localId.x ].clusterSize, 0, DUMMY_UINT );
	InterlockedExchange( gmMergedClusters [ localId.x ].allowance, 0, DUMMY_UINT );
	if ( IsTargetGroupThread( localId, uint2( 0, 0 ) ) )
	{
		InterlockedExchange( gmValidCount, 0, DUMMY_UINT );
	}
}

// This function atomically initializes the group hair cluster
inline void InitGmHairCluster()
{
	InterlockedExchange( gmHairCluster.outlinePos.x, MAX_UINT, DUMMY_UINT );
	InterlockedExchange( gmHairCluster.outlinePos.y, MAX_UINT, DUMMY_UINT );
	InterlockedExchange( gmHairCluster.outlinePos.z, MIN_UINT, DUMMY_UINT );
	InterlockedExchange( gmHairCluster.outlinePos.w, MIN_UINT, DUMMY_UINT );
	InterlockedExchange( gmHairCluster.clusterSize, 0, DUMMY_UINT );
}

// This function is used to check the group status atomically
// If the status is an error we will set the threads status to error
inline void CheckGroupStatus( inout uint status )
{
	uint gmStatus = gmGroupStatus;
	if ( gmStatus != STATUS_OK )
	{
		status = gmStatus;
	}
}

inline void SetGroupStatus( const uint status )
{
	InterlockedExchange( gmGroupStatus, status, DUMMY_UINT );
}
