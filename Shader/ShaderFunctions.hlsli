
#include "ShaderDefines.hlsli"

///-------Struct-Constructor-Functions----///

inline BoundingBox BoundingBoxCTOR( uint2 minimum, uint2 maximum, uint id, uint link )
{
    BoundingBox result;
    result.min = minimum;
    result.max = maximum;
    result.linked = link;
    result.uniqueId = id;
    return result;
}

inline HairCentroid HairCentroidCTOR( uint2 pos, uint id, uint link )
{
    HairCentroid result;
    result.linked = link;
    result.position = pos;
    result.uniqueId = id;
    return result;
}

inline HairCluster HairClusterCTOR( uint2 avgPos, uint szCluster, uint2 posArry [ THREAD_GROUP_SIZE ] )
{
    HairCluster result;
    result.averagePos = avgPos;
    result.clusterSize = szCluster;
    result.positions = posArry;
    return result;
}

inline void HairClusterPosCTOR( uint threadIdY, inout uint2 posArry [ THREAD_GROUP_SIZE ] )
{
    posArry [ threadIdY ] = uint2( 0, 0 );
}

inline PlayerPosition PlayerPositionCTOR(uint2 headPos, uint2 torsoPos, uint4x2 bb )
{
    PlayerPosition result;
    result.bodyPosition = torsoPos;
    result.headPosition = headPos;
    result.boundingBox = bb;
    return result;
}



///------Group-Matrix-Functions-------///

// Reads and writes to group matrix.
inline uint ReadGroupMatrix( const uint2 localId )
{
    uint result = MAX_UINT;
    if ( localId.x < X_THREADGROUP && localId.y < Y_THREADGROUP )
    {
        if ( localId.y == 0 )
            result = localSharedMatrix.row0 [ localId.x ];
        else if ( localId.y == 1 )
            result = localSharedMatrix.row1 [ localId.x ];
        else if ( localId.y == 2 ) 
            result = localSharedMatrix.row2 [ localId.x ];
        else 
            result = localSharedMatrix.row3 [ localId.x ];
    }    
    return result;
}


inline void WriteGroupMatrix( const uint2 localId, uint value )
{
    uint dummy = 0;
    if ( localId.x < X_THREADGROUP && localId.y < Y_THREADGROUP)
    {
        if ( localId.y == 0 )
            InterlockedExchange( localSharedMatrix.row0 [ localId.x ], value, dummy );
        else if ( localId.y == 1 )
            InterlockedExchange( localSharedMatrix.row1 [ localId.x ], value, dummy );
        else if ( localId.y == 2 )
            InterlockedExchange( localSharedMatrix.row2 [ localId.x ], value, dummy ); 
        else 
        InterlockedExchange( localSharedMatrix.row3 [ localId.x ], value, dummy );
    }   
}

// Check if the current pixel is the same as the pixel type.
inline bool VerifyPixel( const uint pixelType, const uint2 localPos, int2 scanOffset )
{
    bool result = false;
    if ( localPos.x < X_THREADGROUP && localPos.y < Y_THREADGROUP )
    {
        uint2 localIndex = ( localPos + scanOffset );
        uint pixelCheck = ReadGroupMatrix( uint2( clamp( localIndex, uint2( 0, 0 ), uint2( X_THREADGROUP, Y_THREADGROUP ) ) ) );
        result = ( pixelType == pixelCheck );
    }
    return result;
}


///---------Utility-Functions--------///

// Calculates the distance between two points and checks if it's within a threshold.
inline bool DetectGrouping( const uint2 pos1, const uint2 pos2, const uint threshold )
{
    float dx = max( pos1.x, pos2.x ) - min( pos1.x, pos2.x );
    float dy = max( pos1.y, pos2.y ) - min( pos1.y, pos2.y );
    return sqrt( float( pow( dx, 2.0 ) + pow( dy, 2.0 ) ) ) <= float( threshold ) ? true : false;
}

// Generate random id with our incrementel base value
inline uint GenerateUniqueId()
{
    uint baseValue = PlayerPositionBuffer [ 1 ].UID_BASE_VALUE;
    uint hash = 0x0F01010;

    // Apply alogrithm
    // This comes from my github repo for my custom string hashing asm function
    // With 3 small changes or hash value starts much lower,
    // and we shift right for the last step so our value doesnt get to big.
    // We also dont touch the hash value since its used once
    // https://github.com/IceCoaled/UserMode-KernelMode-Asm-Functions/blob/main/CustomHash.asm
    baseValue *= hash;
    baseValue = ROR( baseValue, 0x010 );
    baseValue >>= 0x6;
      
    // Increment base value
    InterlockedAdd( PlayerPositionBuffer [ 1 ].UID_BASE_VALUE, 0x01 );
  
    return baseValue;
}

// Custom distance function that takes uint2's.
inline float Distance( uint2 pos1, uint2 pos2 )
{
    uint dx = max( pos1.x, pos2.x ) - min( pos1.x, pos2.x );
    uint dy = max( pos1.y, pos2.y ) - min( pos1.y, pos2.y );
    return sqrt( float( pow( dx, 2 ) + pow( dy, 2 ) ) );
}

// Checks if input name is one of the range names.
uint CompareNames( half3 name )
{
    uint result = NO_MATCHING_NAME;
    if ( !any( name - COLOR_NAME_OUTLNZ ) )
    {
        result = PX_OUTLINE;
    }
    else if ( !any( name - COLOR_NAME_HAIR ) )
    {
        result = PX_HAIR;
    }
    else if ( !any( name - COLOR_NAME_SKIN ) )
    {
        result = PX_SKIN;
    }
    return result;
}

// Converts group id to single uint for bounding box id.
inline uint GlobalPosToUID( const uint2 globalPos )
{
    return uint( ( globalPos.x & 0x0000FFFF ) ) | uint( ( globalPos.y & 0x0000FFFF ) >> 0x010 );
}

// Calculate the current segment.
// This goes down to the nearest segment size.
// Then sets the current segment to a segment index with 0 based index.
inline uint SegmentCalc( const uint2 localPos )
{
    return ( ( localPos.x + SEGMENT_SIZE ) & ~( SEGMENT_SIZE - 0x01 ) ) / ( SEGMENT_SIZE - 0x01 );
}

// Calculates the threads position inside the thread group segment
inline uint SegmentPosCalc( uint2 localPos )
{
    return uint( localPos.y % SEGMENT_SIZE );
}

// Checks to see if group min max have been merged.
inline bool IsGroupMerged( uint2 min, uint2 max )
{
    uint2 merged = uint2( MERGED_FLAG, MERGED_FLAG );

    return !any( min - merged ) & !any( max - merged );
}

// Group or local position to global position.
inline void GroupBBToGlobalBB( uint2 groupMin, uint2 groupMax, const uint2 groupId, out uint4x2 globalPos )
{
    // Create thread group size int2, this shrinks the stack size. as you could use `int2( X_THREADGROUP, Y_THREADGROUP )` in each calculation.
    // But that would create 4 int2's on the stack, this way we only create 1.
    uint2 groupSize = uint2( X_THREADGROUP, Y_THREADGROUP );
  
    // Calculate the global texture positions.
    globalPos = uint4x2( groupId * groupSize + groupMin,
    groupId * groupSize + uint2( groupMin.x, groupMax.y ),
    groupId * groupSize + uint2( groupMax.x, groupMin.y ),
    groupId * groupSize + groupMax );
}

// Calculates global position from group position
inline uint2 GroupPosToGlobal( uint2 groupPos, const uint2 groupId )
{
    uint2 groupSize = uint2( X_THREADGROUP, Y_THREADGROUP );    
    return uint2( groupId * groupSize + groupPos );
}

inline uint2 GetTorsoPos( inout uint4x2 boundingBox )
{
    return ( boundingBox [ 0 ] + boundingBox [ 1 ] + boundingBox [ 2 ] + boundingBox [ 3 ] ) * 0.25;
}

// Returns true if the two values are within the threshold.
inline bool PlusMinus( uint value1, uint value2, uint threshold )
{
    return asuint( max( value1, value2 ) - min( value1, value2 ) ) <= threshold ? true : false;
}

// Overload for uint2.
inline bool PlusMinus( uint2 value1, uint2 value2, uint threshold )
{
    return asuint( max( value1, value2 ).x - min( value1, value2 ).x ) <= threshold &&
    asuint( max( value1, value2 ).y - min( value1, value2 ).y ) <= threshold ? true : false;
}

// Checks the positions of the hair centroid in relation to the bounding box.
inline bool BbToHairLink( uint2 bbMin, uint2 bbMax, uint2 hairPos )
{
    return PlusMinus( hairPos.x, bbMin.x, ( ( bbMax.x - bbMin.x ) / 2 ) + 10 ) &&
    PlusMinus( hairPos.x, bbMax.x, ( ( bbMax.x - bbMin.x ) / 2 ) + 10 ) &&
    PlusMinus( hairPos.y, bbMin.y, ( ( bbMax.y - bbMin.y ) / 3 ) );
}


// returns true if global pixel is within 1 of the pixel coordinates connecting the 4 corners
bool IsPixelBoundingBox( uint2 globalPos, uint4x2 boundingBox )
{
    bool2 result = bool2( false, false );
    // Checks for top and bottom horizontial lines.
    result.x = ( PlusMinus( globalPos.x, boundingBox [ 0 ].x, 0x01 ) || PlusMinus( globalPos.x, boundingBox [ 1 ].x, 0x01 ) ) &&
    ( ( globalPos.y >= boundingBox [ 0 ].y - 1 ) || ( globalPos.y <= boundingBox [ 2 ].y + 1 ) );
      
  
    // Checks for left and right virtical lines
    result.y = ( PlusMinus( globalPos.y, boundingBox [ 0 ].y, 0x01 ) || PlusMinus( globalPos.y, boundingBox [ 2 ].y, 0x01 ) )  &&
    ( ( globalPos.x >= boundingBox [ 0 ].x - 1 ) || ( globalPos.x <= boundingBox [ 1 ].x + 1 ) );
    
    return result.x | result.y;
}


///---------------Main-Shader-Functions----------------///

// Connect missing outline pixels.
// This will be done 2 or 3 times, this way we can connect all the outline pixels.
void FindOutlineConnection( const uint2 localPos, const uint pixelType, uint failedThread )
{
    uint blockThread = 0;
    // if the current pixel is already an outline pixel, return.  
    if ( VerifyPixel( PX_OUTLINE, localPos, 0 ) )
    {
        blockThread = 1;
    }
    
    // Dummy value for interlocked operation.
    volatile uint dummy = 0;
  
    // Reset the fill modified flag.
    InterlockedCompareExchange( fillModified, 0, 1, dummy );
     
    // Safety breakout.
    volatile uint backup = 0;
  
    // Connect the outline pixels.  
    while ( backup < LOOP_SAFETY_BREAKOUT )
    {            
        if ( fillModified == 1 )
        {
            break;
        }
        else if ( failedThread == 0 && blockThread == 0 )
        {
            bool2 fillCurrentPos = bool2( false, false );
      
            // Search for a line of the outline color in any direction.       
            for ( uint i = 0; i < 8; i += 2 )
            {
                fillCurrentPos.x = ( VerifyPixel( pixelType, localPos, SharedScanMatrix [ MATRIX_TYPE_SCAN ] [ i ] [ 0 ] ) & VerifyPixel( pixelType, localPos, SharedScanMatrix [ MATRIX_TYPE_SCAN ] [ i ] [ 1 ] ) & VerifyPixel( pixelType, localPos, SharedScanMatrix [ MATRIX_TYPE_SCAN ] [ i ] [ 2 ] ) );
                fillCurrentPos.y = ( VerifyPixel( pixelType, localPos, SharedScanMatrix [ MATRIX_TYPE_SCAN ] [ i + 1 ] [ 0 ] ) & VerifyPixel( pixelType, localPos, SharedScanMatrix [ MATRIX_TYPE_SCAN ] [ i + 1 ] [ 1 ] ) & VerifyPixel( pixelType, localPos, SharedScanMatrix [ MATRIX_TYPE_SCAN ] [ i + 1 ] [ 2 ] ) );
          
                if ( fillCurrentPos.x & fillCurrentPos.y )
                {
                    break;
                }
            }
           
            // redundant check, but it is needed.
            if ( fillCurrentPos.x & fillCurrentPos.y )
            {
                // If there is a line in any direction, fill the current position.
                WriteGroupMatrix( localPos, PX_OUTLINE );
                // Set the fill modified flag.
                InterlockedCompareExchange( fillModified, 0, 1, dummy );
            }
            else
            {
                InterlockedCompareExchange( fillModified, 1, 0, dummy );
            }
        } 
        // Iterate backup.
        ++backup;
    }    
}



//// Currently not used.
//// Needs to be updated if used.
//inline void RemoveNonSkin( int2 localPos, int pixelType )
//{
//    bool isSkin = false;
  
//    // Check if the pixel is colored as skin but is not skin.
//    isSkin = VerifyPixel( pixelType, localPos, ScanOffsets [ 0 ] ) & VerifyPixel( pixelType, localPos, ScanOffsets [ 1 ] ) & VerifyPixel( pixelType, localPos, ScanOffsets [ 2 ] );
  
//    isSkin = isSkin ? isSkin : VerifyPixel( pixelType, localPos, ScanOffsets [ 2 ] ) & VerifyPixel( pixelType, localPos, ScanOffsets [ 4 ] ) & VerifyPixel( pixelType, localPos, ScanOffsets [ 7 ] );
  
//    isSkin = isSkin ? isSkin : VerifyPixel( pixelType, localPos, ScanOffsets [ 1 ] ) & VerifyPixel( pixelType, localPos, ScanOffsets [ 2 ] ) & VerifyPixel( pixelType, localPos, ScanOffsets [ 4 ] );
  
//    isSkin = isSkin ? isSkin : VerifyPixel( pixelType, localPos, ScanOffsets [ 5 ] ) & VerifyPixel( pixelType, localPos, ScanOffsets [ 3 ] ) & VerifyPixel( pixelType, localPos, ScanOffsets [ 1 ] );
     
//    if ( !isSkin )
//    {
//        localSharedMatrix [ localPos.x ] [ localPos.y ] = PX_BACKGROUND;
//    }
//}



// Flood fill the body of the character.
void FloodFillBody( const uint2 localPos, const uint pixelType, uint failedThread )
{
    uint blockThread = 0;
    // If the current pixel is hair or outline, return.
    if ( VerifyPixel( PX_HAIR, localPos, 0 ) || VerifyPixel( PX_OUTLINE, localPos, 0 ) )
    {
        blockThread = 1;
    }
    
    // Dummy value for Interlocked operation.
    volatile uint dummy = 0;
  
    // Reset the fill modified flag.
    InterlockedCompareExchange( fillModified, 0, 1, dummy );
  
    // Safety breakout.
    volatile uint backup = 0;
  
    // Fill the body of the character.
  
    while ( backup < LOOP_SAFETY_BREAKOUT )
    {
        if ( fillModified == 1 )
        {
            break;
        }
        else if ( failedThread == 0 && blockThread == 0 )
        {
            bool isBody = false;
              
            for ( uint i = 0; i < 7; i++ )
            {
                // Check if the pixel is a body pixel.
                isBody = ( VerifyPixel( pixelType, localPos, SharedScanMatrix [ MATRIX_TYPE_FILL0 ] [ i ] [ 0 ] ) & VerifyPixel( pixelType, localPos, SharedScanMatrix [ MATRIX_TYPE_FILL0 ] [ i ] [ 1 ] ) & VerifyPixel( pixelType, localPos, SharedScanMatrix [ MATRIX_TYPE_FILL0 ] [ i ] [ 2 ] ) );
                      
                // This checks if we are next to other body pixels, if we are we are a body pixel.
                if ( isBody || i < 5 && ( VerifyPixel( PX_FLOODFILL, localPos, SharedScanMatrix [ MATRIX_TYPE_FILL1 ] [ i ] [ 0 ] ) & VerifyPixel( PX_FLOODFILL, localPos, SharedScanMatrix [ MATRIX_TYPE_FILL1 ] [ i ] [ 1 ] ) ) )
                {
                    break;
                }
            }
          
            if ( isBody )
            {
                // Modify the pixel if it is a body pixel.
                WriteGroupMatrix( localPos, PX_FLOODFILL );
                // Set the flood fill modified flag.
                InterlockedCompareExchange( fillModified, 0, 1, dummy );
            }
            else
            {
                // Set the flood fill modified flag to false.
                InterlockedCompareExchange( fillModified, 1, 0, dummy );
            }
        }
        // Iterate backup.
        ++backup;
    }    
}



// Blend any pixels that are colored as hair but not hair to the background color.
void RemoveNonHair( const uint2 localPos, const uint pixelType, uint failedThread )
{
    uint blockThread = 0;
    // If the curret pixel isnt hair, return.  
    if ( !VerifyPixel( PX_HAIR, localPos, 0 ) )
    {
        blockThread = 1;
    }
    
    // Dummy value for Interlocked operation.
    volatile uint dummy = 0;
  
    // Reset the fill modified flag.
    InterlockedCompareExchange( fillModified, 0, 1, dummy );
       
  
    // Safety breakout.
    volatile uint backup = 0;
  
    while ( backup < LOOP_SAFETY_BREAKOUT )
    {
        if ( fillModified == 1 )
        {
            break;
        }
        else if ( failedThread == 0 && blockThread == 0 )
        {
            bool isHair = false;
            // Check if the pixel is colored as hair but is not hair.     
            for ( uint i = 0; i < 5; i++ )
            {
                if ( isHair = ( VerifyPixel( pixelType, localPos, SharedScanMatrix [ MATRIX_TYPE_SCAN ] [ i ] [ 0 ] ) & VerifyPixel( pixelType, localPos, SharedScanMatrix [ MATRIX_TYPE_SCAN ] [ i ] [ 1 ] ) & VerifyPixel( pixelType, localPos, SharedScanMatrix [ MATRIX_TYPE_SCAN ] [ i ] [ 2 ] ) ) == true )
                {
                    break;
                }
            }
  
            // If the pixel is not hair, set it to the background color.        
            if ( isHair )
            {
                // Set the flood fill modified flag to false.
                InterlockedCompareExchange( fillModified, 1, 0, dummy );
            }
            else
            {
                WriteGroupMatrix( localPos, PX_BACKGROUND );
                // Set the flood fill modified flag.
                InterlockedCompareExchange( fillModified, 0, 1, dummy );
            }
        }
        // Iterate backup.
        ++backup;
    }    
}


void CheckAndSetHairCluster( const uint2 hairPos )
{
    // Interlocked operation dummy value.
    uint dummy = 0;
    // If there are no hair clusters, add a new hair cluster.
    // A quick early out.
    if ( hairClusterCount == 0 )
    {
        InterlockedAdd( hairClusterCount, 1 );
        InterlockedAdd( hairClusters [ 0 ].clusterSize, 1 );
        ATOMIC_EXCHANGE_UINT2( hairClusters [ 0 ].positions[ 0 ], hairPos, dummy );
        ATOMIC_EXCHANGE_UINT2( hairClusters [ 0 ].averagePos, hairPos, dummy );
    }
    else
    {
        // Check if the hair position is within the threshold of any of the hair clusters.
        volatile uint hairGroupable = 0;      
        for ( uint i = 0; i < MAX_PLAYERS || i < hairClusterCount; i++ )
        {
            const uint2 avgPos = hairClusters [ i ].GetAveragePos();
            hairGroupable = DetectGrouping( avgPos, hairPos, HAIR_CLUSTER_THRESHOLD );
            
            uint currentSize = hairClusters [ i ].clusterSize;
            
            // If the hair position is within the threshold of the hair cluster, add the hair position to the cluster.     
            if ( hairGroupable == 1 && !hairClusters [ i ].IsMerged() || currentSize < THREAD_GROUP_SIZE )
            {               
                uint2 newAvg = hairClusters [ i ].GetAveragePos();
                currentSize++;
                newAvg = ( ( newAvg * currentSize ) + hairPos ) / currentSize;
                ATOMIC_EXCHANGE_UINT2( hairClusters [ i ].averagePos, newAvg, dummy );
                ATOMIC_EXCHANGE_UINT2( hairClusters [ i ].positions [ currentSize ], hairPos, dummy );
                break;
            }
        }
    
        // If the hair position is not within the threshold of any of the hair clusters, add a new hair cluster. 
        if ( hairGroupable == 0 && hairClusterCount < ( MAX_PLAYERS - 1 ) )
        {
            uint currentHairClusterCount = 0;
            InterlockedAdd( hairClusterCount, 1, currentHairClusterCount );
            ++currentHairClusterCount;
              
            // Add new hair cluster.
            ATOMIC_EXCHANGE_UINT2( hairClusters [ currentHairClusterCount ].positions [ 0 ], hairPos, dummy );
            ATOMIC_EXCHANGE_UINT2( hairClusters [ currentHairClusterCount ].averagePos, hairPos, dummy );
        }
    }  
}



// Merge hair clusters that are within the threshold of each other.
// This is very computationally expensive, so we only do it if there is more than 2 hair clusters.
void MergeHairClusters( const uint2 localPos, const uint failedThread )
{
    // Dummy value for Interlocked operation.
    volatile uint dummy = 0;
    
    // Reset the fill modified flag.
    InterlockedCompareExchange( fillModified, 0, 1, dummy );
  
    // Safety breakout.
    volatile uint backup = 0;
  
    // Merge the hair clusters in the current group.    
    while ( backup < LOOP_SAFETY_BREAKOUT )
    {       
        if ( fillModified == 1 )
        {
            break;
        }
        else if ( failedThread == 0 )
        {
            volatile uint y = 1;
            for ( uint i = 0; ( ( i < MAX_PLAYERS || i < hairClusterCount ) & ( y < MAX_PLAYERS || y < hairClusterCount ) );
            i = ( i < MAX_PLAYERS || i < hairClusterCount ) ? ++i : i, y = ( y < MAX_PLAYERS || y < hairClusterCount ) ? ++y : y )
            {          
                if ( hairClusters [ i ].IsMerged() )
                {
                    continue;
                }
                else if ( fillModified == 1 )
                {
                    break;
                }         
                else if ( i == y )//< Redunant check
                {
                    continue;
                }
                else if ( !hairClusters [ y ].IsMerged() && !hairClusters [ i ].IsMerged() &&
                    PlusMinus( hairClusters [ i ].GetAveragePos(), hairClusters [ y ].GetAveragePos(), HAIR_MERGE_THRESHOLD ) )
                {
                   
                    // Merge the hair clusters.
                    uint szMergeCluster = hairClusters [ y ].clusterSize;
                    uint szOurCluster = hairClusters [ i ].clusterSize;
                    volatile uint szCurrentCluster = szOurCluster + 1;
                    for ( uint z = 0; z < szMergeCluster; ++i, szOurCluster = ++szCurrentCluster )
                    {
                        ATOMIC_EXCHANGE_UINT2( hairClusters [ i ].positions [ szCurrentCluster ], hairClusters [ y ].positions [ z ], dummy );
                        uint2 newAvg = hairClusters [ i ].GetAveragePos();
                        newAvg = ( ( newAvg * szOurCluster ) + hairClusters [ y ].positions [ z ] ) / szCurrentCluster;
                        ATOMIC_EXCHANGE_UINT2( hairClusters [ i ].averagePos, newAvg, dummy );
                        ATOMIC_EXCHANGE_UINT2( hairClusters [ i ].positions [ szCurrentCluster ], hairClusters [ y ].positions [ z ], dummy );
                    }
                    // Set the fill modified flag to true.
                    InterlockedCompareExchange( fillModified, 0, 1, dummy );
                }
                else
                {
                    // Set the fill modified flag to false.
                    InterlockedCompareExchange( fillModified, 1, 0, dummy );
                }
            }
        }             
        // Iterate the backup breakout.
        ++backup;
    }
}



// Check if the pixel is within the range of any of the color ranges.
// If the pixel is within the range, add the pixel to the detected object buffer and set the pixel to the swap color.
// We use buffer indexing because if we load the whole color range it flogs the gpu instruction cache.
// As well as we dont need to load the whole buffer, just the parts we need.
int CheckAndSetPixel( const uint2 localPos, const float4 pixelColor, const half3 rangeName, out float4 swapColor )
{
    // Dummy value for interlocked operation
    volatile uint dummy = 0;
      
    for ( uint i = 0; i < NUM_COLOR_RANGES; i++ )
    {
        if ( !ColorRangeBuffer [ i ].CheckAlignment() )
        {
            return ALIGNMENT_ERROR;
        }
        else if ( !ColorRangeBuffer [ i ].CheckName( rangeName ) || !ColorRangeBuffer [ i ].HasRanges() )
        {
            continue;
        }
        else
        {           
            for ( uint n = 0; n < ColorRangeBuffer [ i ].numOfRanges; n++ )
            {                    
                if ( ColorRangeBuffer [ i ].IsInRange( pixelColor, n ) )
                {
                    // Set output swap color to the skin swap color .
                    swapColor = ColorRangeBuffer [ i ].swapColor;
                         
                    // return the proper mofifier.
                    switch ( CompareNames( rangeName ) )
                    {
                        case PX_HAIR:
                            // Set local shared matrix to desired pixel type.
                            WriteGroupMatrix( localPos, PX_HAIR );
                            // Set hair position.
                            CheckAndSetHairCluster( localPos );
                            return PX_HAIR;
                            break;
                        case PX_SKIN:
                            WriteGroupMatrix( localPos, PX_SKIN );
                            return PX_SKIN;
                            break;
                        case PX_OUTLINE:
                            WriteGroupMatrix( localPos, PX_OUTLINE );
                            return PX_OUTLINE;
                            break;
                        default:
                            break;
                    }
                }
            }            
        }
    }
    return PX_BACKGROUND;
}


// This is a atomic reduction function that will reduce the bounding box of the filled area.
// this starts the bounding box at the size of the texture.
// Each time a thread finds a filled pixel it will reduce the bounding box.
inline void BoundingBoxReductionHelper( const uint2 localPos, uint segmentIndex )
{
    uint pixelType = ReadGroupMatrix( localPos );
    if ( pixelType  == PX_FLOODFILL || pixelType == PX_OUTLINE )
    {
        ATOMIC_MAX_UINT2( groupMax [ segmentIndex ], localPos );
        ATOMIC_MIN_UINT2( groupMax [ segmentIndex ], localPos );
    }
}


// This will calculate max players worth of bounding boxes, per thread group.
// in the default case thats 6 bounding boxes.
void GetBoundingBoxPositions( const uint2 localPos, uint failedThread )
{
    // this way is faster than using a loop with 1 thread.
    if ( localPos.x < MAX_PLAYERS && failedThread == 0 )
    {
        // Interlocked operation dummy value.
        uint dummy = 0;
        // Set the group min/max be the size of the texture, this is much larger than the actual bounding box.
        // This way we can reduce the bounding box to the actual size.
        ATOMIC_EXCHANGE_UINT2( groupMin [ localPos.x ], uint2( WINDOW_SIZE_X, WINDOW_SIZE_Y ), dummy );
        ATOMIC_EXCHANGE_UINT2( groupMax [ localPos.x ], uint2( 0, 0 ), dummy );
    }

    // Sync the threads in the group, this way they all see the initialized values.
    GroupMemoryBarrierWithGroupSync();
        
    // Reduce the bounding box to the current detected size.
    if ( failedThread == 0 )
    {
        BoundingBoxReductionHelper( localPos, SegmentCalc( localPos ) );
    }      
}


// This will merge the bounding boxes of the segments that are within the threshold of each other.
void BoundingBoxMergeHelper( const uint2 localPos, uint failedThread )
{
    // Dummy value for interlocked operation.
    volatile uint dummy = 0;
    
    // Reset the fill modified flag.
    InterlockedCompareExchange( fillModified, 0, 1, dummy );
 
    // Safety breakout.
    volatile uint backup = 0;
    // Merge flage.
    uint2 merge2 = uint2( MERGED_FLAG, MERGED_FLAG );
  
    // Merge the bounding boxes in the current group.    
    while ( backup < LOOP_SAFETY_BREAKOUT )
    {       
        if ( fillModified == 1 )
        {
            break;
        }
        else if ( failedThread == 0 )
        {
            volatile uint y = 1;
            for ( uint i = 0; i < MAX_PLAYERS & y < MAX_PLAYERS;
            i = ( i < MAX_PLAYERS ) ? ++i : i, y = ( y < MAX_PLAYERS ) ? ++y : y )
            {
                if ( IsGroupMerged( groupMin [ i ], groupMax [ i ] ) )
                {
                    continue;
                }
          
                if ( i == y ) //< Redundant check.
                {
                    continue;
                }
                else if ( !IsGroupMerged( groupMin [ y ], groupMax [ y ] ) &&
                ( PlusMinus( groupMin [ i ].x, groupMax [ y ].x, BB_MERGE_THRESHOLD ) ||
                PlusMinus( groupMin [ i ].y, groupMax [ y ].y, BB_MERGE_THRESHOLD ) ) )
                {
                    // if the bounding boxes are beside each other on X axis, merge them.                   
                    ATOMIC_MAX_UINT2( groupMax [ i ], groupMax [ y ] );
                    ATOMIC_MIN_UINT2( groupMax [ i ], groupMax [ y ] );
                      
                    // Set the merged flag.
                    ATOMIC_EXCHANGE_UINT2( groupMin [ y ], merge2, dummy );
                    ATOMIC_EXCHANGE_UINT2( groupMax [ y ], merge2, dummy );
                    
                    // Set the fill modified flag to true.
                    InterlockedCompareExchange( fillModified, 0, 1, dummy );                   
                }
                else
                {
                    // Set the fill modified flag to false.
                    InterlockedCompareExchange( fillModified, 1, 0, dummy );
                }          
            }
        }             
        // Iterate the backup breakout.
        ++backup;
    }    
}


void AssignUniqueIds( const uint segmentPos, const uint2 groupId )
{
    uint2 linkedBBC = uint2( 0, 0 );
    uint hcInBB = 0xF1A5;
    
    // Interlocked operation dummy value.
    volatile uint dummy = 0;
      
    for ( uint i = 0; i < MAX_PLAYERS || i < hairClusterCount; i++ )
    {                   
        // Check if hair centroid is inside box        
        if ( BbToHairLink( groupMin [ segmentPos ], groupMax [ segmentPos ], hairClusters [ i ].GetAveragePos() ) )
        {
            linkedBBC.x = hcInBB;
            linkedBBC.y = i;
        }
    }
    
    uint2 avgPos = uint2( 0, 0 );
  
    // If hair Centroid is in bounding box.
    if ( linkedBBC.x == hcInBB )
    {
        uint uId = GenerateUniqueId();
        // Add the hair centroid and min/max values to goup detail buffer.
        avgPos = hairClusters [ linkedBBC.y ].GetAveragePos();

        // Setup bounding box.
        ATOMIC_EXCHANGE_UINT2( GroupDetailsBuffer [ groupId.x ].boundingBoxes [ segmentPos ].min, groupMin [ segmentPos ], dummy );
        ATOMIC_EXCHANGE_UINT2( GroupDetailsBuffer [ groupId.x ].boundingBoxes [ segmentPos ].max, groupMax [ segmentPos ], dummy );
        InterlockedExchange( GroupDetailsBuffer [ groupId.x ].boundingBoxes [ segmentPos ].uniqueId, uId, dummy );
        InterlockedExchange( GroupDetailsBuffer [ groupId.x ].boundingBoxes [ segmentPos ].linked, 1, dummy );
        // Setup hair centroid.
        ATOMIC_EXCHANGE_UINT2( GroupDetailsBuffer [ groupId.x ].hairCentroids [ segmentPos ].position, avgPos, dummy );
        InterlockedExchange( GroupDetailsBuffer [ groupId.x ].hairCentroids [ segmentPos ].linked, 1, dummy );
        InterlockedExchange( GroupDetailsBuffer [ groupId.x ].hairCentroids [ segmentPos ].uniqueId, dummy, dummy );

    }
    else
    {
        // If there is no hair centroid for box then add box to group detail buffer with global minimum position as unique id.
        ATOMIC_EXCHANGE_UINT2( GroupDetailsBuffer [ groupId.x ].boundingBoxes [ segmentPos ].min, groupMin [ segmentPos ], dummy );
        ATOMIC_EXCHANGE_UINT2( GroupDetailsBuffer [ groupId.x ].boundingBoxes [ segmentPos ].max, groupMax [ segmentPos ], dummy );
        InterlockedExchange( GroupDetailsBuffer [ groupId.x ].boundingBoxes [ segmentPos ].linked, 0, dummy ); // Make sure linked is 0.
        InterlockedExchange( GroupDetailsBuffer [ groupId.x ].boundingBoxes [ segmentPos ].uniqueId, GlobalPosToUID( GroupPosToGlobal( groupMin [ segmentPos ], groupId ) ), dummy );
        
        // Add a hair centroid to group details
        if ( segmentPos < hairClusterCount)
        {
            avgPos = hairClusters [ segmentPos ].GetAveragePos();
            ATOMIC_EXCHANGE_UINT2( GroupDetailsBuffer [ groupId.x ].hairCentroids [ segmentPos ].position, avgPos, dummy );
            InterlockedExchange( GroupDetailsBuffer [ groupId.x ].hairCentroids [ segmentPos ].linked, 0, dummy ); // Make sure linked is 0.
            InterlockedExchange( GroupDetailsBuffer [ groupId.x ].hairCentroids [ segmentPos ].uniqueId, GlobalPosToUID( GroupPosToGlobal( hairClusters [ linkedBBC.y ].GetAveragePos(), groupId ) ), dummy );
        }
    }
}


// Look at the local matrix and get the details for the global matrix.
// The swap color input will only be valid if the pixel type is hair, outline or skin.
// Otherwise the flood fill and background colors are global.
// Im hoping to only use this for debugging
inline void GetAndSetDetailsForGlobal( const uint2 localPos, const uint2 globalPos, const float4 swapColor,  uint failedThread )
{    
    if ( failedThread == 0 && any( swapColor ) )
    {
        UavBuffer [ globalPos ] = swapColor;
    }
    else if ( failedThread == 0 )
    {
        UavBuffer [ globalPos ] = ReadGroupMatrix( localPos ) == PX_FLOODFILL ? OBJECT_FILL_COLOR : BACKGROUND_PIXEL_COLOR;
    }
  
    // Sync everything.
    AllMemoryBarrierWithGroupSync();
}


// Merges bounding boxes from all thread groups.
// Keeping interlocked operations only to help keep all variables most recent values as visible as possible to all threads.
void MergeGlobalDetails( const uint segmentPos, const uint2 groupId, uint failedThread )
{  
    // Dummy value for interlocked operation.
    volatile uint dummy = 0;
    // Safety breakout.
    volatile uint backup = 0;
    // Merge flag.
    uint2 merge2 = uint2( MERGED_FLAG, MERGED_FLAG );
    
    // Reset the fill modified flag.
    InterlockedCompareExchange( PlayerPositionBuffer [ 1 ].GLOBAL_MERGE_FLAG, 0, 1, dummy );
        
    while ( backup < LOOP_SAFETY_BREAKOUT ) 
    {                    
        if ( PlayerPositionBuffer [ 1 ].GLOBAL_MERGE_FLAG == 1 )
        {
            break;
        }
        else if ( failedThread == 0 && segmentPos < MAX_PLAYERS && !GroupDetailsBuffer [ groupId.x ].boundingBoxes [ segmentPos ].IsMerged() && 
            !GroupDetailsBuffer [ groupId.x ].hairCentroids [ segmentPos ].IsMerged() )
        {
            volatile uint otherSegmentPos = 0;
            for ( uint i = 0; i < NUM_GROUPS; i++, otherSegmentPos = 0 )
            {
                if ( i == groupId.x )
                {
                    continue;
                }
                else
                {
                    BoundingBox boundingBoxes [ MAX_PLAYERS ];
                    HairCentroid hairCentroids [ MAX_PLAYERS ];
                    HairCentroid ourThreadsHairCentroid = HairCentroidCTOR( uint2( 0, 0 ), 0, 0 );
              
                    // Get other groups details.
                    GroupDetailsBuffer [ i ].GetAllGroupDetails( boundingBoxes, hairCentroids );
              
                    // Get this threads hair centroid.
                    GroupDetailsBuffer [ groupId.x ].GetHairCentroid( segmentPos, ourThreadsHairCentroid );
              
                    
                    while ( otherSegmentPos < MAX_PLAYERS )
                    {
                        if ( !hairCentroids [ otherSegmentPos ].IsMerged() && PlusMinus( hairCentroids [ otherSegmentPos ].position, ourThreadsHairCentroid.position, HAIR_MERGE_THRESHOLD ) && hairCentroids [ otherSegmentPos ].isLinked() )
                        {
                            // Average the 2 positions.
                            ourThreadsHairCentroid.position += hairCentroids [ otherSegmentPos ].position;
                            ourThreadsHairCentroid.position >>= 1;
                          
                            // Exchange the new postion.
                            ATOMIC_EXCHANGE_UINT2( GroupDetailsBuffer [ groupId.x ].hairCentroids [segmentPos].position, ourThreadsHairCentroid.position, dummy );                                                                          
                            
                            // Merge bounding boxes.
                            ATOMIC_MIN_UINT2( GroupDetailsBuffer [ groupId.x ].boundingBoxes [ segmentPos ].min, boundingBoxes [ otherSegmentPos ].min );
                            ATOMIC_MAX_UINT2( GroupDetailsBuffer [ groupId.x ].boundingBoxes [ segmentPos ].max, boundingBoxes [ otherSegmentPos ].max );
                      
                            // Set merge flag for both.
                            InterlockedExchange( GroupDetailsBuffer [ i ].hairCentroids [ otherSegmentPos].uniqueId,  MERGED_FLAG, dummy );
                            InterlockedExchange( GroupDetailsBuffer [ i ].boundingBoxes [ otherSegmentPos ].uniqueId, MERGED_FLAG, dummy );
                            // Unlink just because.
                            InterlockedCompareExchange( GroupDetailsBuffer [ i ].hairCentroids [ otherSegmentPos ].linked, 1, 0, dummy );
                            InterlockedCompareExchange( GroupDetailsBuffer [ i ].boundingBoxes [ otherSegmentPos ].linked, 1, 0, dummy );
                            
                            // Make sure flag stays true.
                            InterlockedCompareExchange( PlayerPositionBuffer [ 1 ].GLOBAL_MERGE_FLAG, 0, 1, dummy );
                        }
                        else if ( !hairCentroids [ otherSegmentPos ].IsMerged() && !boundingBoxes [ otherSegmentPos ].isLinked() &&
                        ( PlusMinus( boundingBoxes [ otherSegmentPos ].min.y, GroupDetailsBuffer [ groupId.x ].boundingBoxes [ segmentPos ].max.y, BB_MERGE_THRESHOLD ) ||
                        PlusMinus( boundingBoxes [ otherSegmentPos ].min.x, GroupDetailsBuffer [ groupId.x ].boundingBoxes [ segmentPos ].max.x, BB_MERGE_THRESHOLD ) ||
                        PlusMinus( boundingBoxes [ otherSegmentPos ].max.y, GroupDetailsBuffer [ groupId.x ].boundingBoxes [ segmentPos ].min.y, BB_MERGE_THRESHOLD ) ||
                        PlusMinus( boundingBoxes [ otherSegmentPos ].max.x, GroupDetailsBuffer [ groupId.x ].boundingBoxes [ segmentPos ].min.x, BB_MERGE_THRESHOLD ) ) )
                        {
                            // Merge bounding boxs.
                            ATOMIC_MIN_UINT2( GroupDetailsBuffer [ groupId.x ].boundingBoxes [ segmentPos ].min, boundingBoxes [ otherSegmentPos ].min );
                            ATOMIC_MAX_UINT2( GroupDetailsBuffer [ groupId.x ].boundingBoxes [ segmentPos ].max, boundingBoxes [ otherSegmentPos ].max );
                                              
                            // Set merge flag for both.
                            InterlockedExchange( GroupDetailsBuffer [ i ].hairCentroids [ otherSegmentPos ].uniqueId, MERGED_FLAG, dummy );
                            InterlockedExchange( GroupDetailsBuffer [ i ].boundingBoxes [ otherSegmentPos ].uniqueId, MERGED_FLAG, dummy );
                            // Unlink just because.
                            InterlockedCompareExchange( GroupDetailsBuffer [ i ].hairCentroids [ otherSegmentPos ].linked, 1, 0, dummy );
                            InterlockedCompareExchange( GroupDetailsBuffer [ i ].boundingBoxes [ otherSegmentPos ].linked, 1, 0, dummy );
                                                                                          
                            // Make sure flag stays true.
                            InterlockedCompareExchange( PlayerPositionBuffer [ 1 ].GLOBAL_MERGE_FLAG, 0, 1, dummy );                            
                        }
                        else
                        {
                            // Set flag to false
                            InterlockedCompareExchange( PlayerPositionBuffer [ 1 ].GLOBAL_MERGE_FLAG, 1, 0, dummy );
                        }                         
                        // Iterate other segment postion
                        otherSegmentPos++;
                    }
                }
            }
        }
        // Iterate the backup breakout.
        ++backup;
    }    
}



// This will merge the potential 6 bounding boxes into 1 bounding box.
// Get the locations of the average hair position(s).
// Add the details to the group details buffer.
void SetGroupDetails( const uint2 localPos, const uint2 groupId, const uint2 globalPos, const float4 swapColor, uint failedThread )
{
    // Add all bounding boxes to the group details uav buffer.
    // This also assigns unique id's to mathching boxes and hair centroids.
    // Else it uses the global position of the min x/y as the unique id.
    const uint segmentPos = SegmentPosCalc( localPos );
  
    if ( failedThread == 0 && segmentPos < MAX_PLAYERS )
    {
        AssignUniqueIds( segmentPos, groupId );
    }
  
    // Sync all threads.
    AllMemoryBarrierWithGroupSync();
  
#ifdef DEBUG
    // Update texture with all edited details.
    GetAndSetDetailsForGlobal( localPos, globalPos, swapColor, failedThread );
#endif
  
    // Merge the group buffer details.
    MergeGlobalDetails( segmentPos, groupId, failedThread );
}


// Draws bounding box on texture 3 pixels wide. 
inline void DrawBoundingBox( const uint2 globalPos )
{ 
    for ( uint i = 0; i < MAX_PLAYERS; i++ )
    {
        UavBuffer [ globalPos ] = IsPixelBoundingBox( globalPos, PlayerPositionBuffer [ 0 ].players [ i ].boundingBox ) ? BOUNDING_BOX_COLOR : UavBuffer [ globalPos ];
    }
}
