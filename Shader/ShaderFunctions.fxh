#include "ShaderDefines.fxh"
///----------Functions-----------///

///---------Utility-Functions--------///

// Generate random id with our incrementel base value
inline uint GenerateUniqueId()
{
    uint baseValue = UNIQUE_ID_BASE;
    uint hash = 0x0F01010;

    // Apply alogrithm
    // This comes from my github repo for my custom string hashing asm function
    // With 3 small changes or hash value starts much lower,
    // and we shift right for the last step so our value doesnt get to big.
    // We also dont touch the hash value since its used once
    // https://github.com/IceCoaled/UserMode-KernelMode-Asm-Functions/blob/main/CustomHash.asm
    baseValue *= hash;
    baseValue = ROR( baseValue, 0x10 );
    baseValue >> 0x6;
        
    // Increment base value
    InterlockedAdd( UNIQUE_ID_BASE, 1 );
    
    return baseValue;
}

// Check if the current pixel is the same as the pixel type.
inline bool VerifyPixel( int pixelType, int2 localPos, int2 scanOffset )
{
    return pixelType == localSharedMatrix [ localPos.x + scanOffset.x ] [ localPos.y + scanOffset.y ]  ? true : false;
}

// Group or local position to global position.
inline void GroupBBToGlobalBB( int2 groupMin, int2 groupMax, int2 groupId, out int4x2 globalPos )
{
    // Create thread group size int2, this shrinks the stack size. as you could use `int2( X_THREADGROUP, Y_THREADGROUP )` in each calculation.
    // But that would create 4 int2's on the stack, this way we only create 1.
    int2 groupSize = int2( X_THREADGROUP, Y_THREADGROUP );
    
    // Calculate the global texture positions.
    globalPos = int4x2( groupId * groupSize + groupMin,
    groupId * groupSize + int2( groupMin.x, groupMax.y ),
    groupId * groupSize + int2( groupMax.x, groupMin.y ),
    groupId * groupSize + groupMax );
}

// Calculates global position from group position
inline int2 GroupPosToGlobal( int2 groupPos, int2 groupId )
{
    int2 groupSize = int2( X_THREADGROUP, Y_THREADGROUP );    
    return ( groupId * groupSize + groupPos );
}

inline int2 GetTorsoPos( int4x2 boundingBox )
{
    return ( boundingBox [ 0 ] + boundingBox [ 1 ] + boundingBox [ 2 ] + boundingBox [ 3 ] ) / 4;
}

// Returns true if the two values are within the threshold.
inline bool PlusMinus( int value1, int value2, int threshold )
{
    return abs( value1 - value2 ) <= threshold ? true : false;
}

// Overload for int2.
inline bool PlusMinus( int2 value1, int2 value2, int threshold )
{
    return abs( value1 - value2 ) <= threshold ? true : false;
}

// Checks the positions of the hair centroid in relation to the bounding box.
inline bool BbToHairLink( int2 bbMin, int2 bbMax, int2 hairPos )
{
    return PlusMinus( hairPos.x, bbMin.x, ( ( bbMax.x - bbMin.x ) / 2 ) + 10 ) & PlusMinus( hairPos.x, bbMax.x, ( ( bbMax.x - bbMin.x ) / 2 ) + 10 ) & PlusMinus( hairPos.y, bbMin.y, ( ( bbMax.y - bbMin.y ) / 3 ) );
}

// returns true if global pixel is within 1 of the pixel coordinates connecting the 4 corners
inline bool IsPixelBoundingBox( int2 globalPos, int4x2 boundingBox )
{
    // Checks for top and bottom horizontial lines.
    bool posCheck = PlusMinus( globalPos.x, boundingBox [ 0 ].x, 0x01 ) || PlusMinus( globalPos.x, boundingBox [ 1 ].x, 0x01 ) &&
     ( globalPos.y >= boundingBox [ 0 ].y - 1 ) || ( globalPos.y <= boundingBox [ 2 ].y + 1 );
    
    if (posCheck)
    {
        return true;
    }
    
    // Checks for left and right virtical lines
     return posCheck = PlusMinus( globalPos.y, boundingBox [ 0 ].y, 0x01 ) || PlusMinus( globalPos.y, boundingBox [ 2 ].y, 0x01 ) &&
     ( globalPos.x >= boundingBox [ 0 ].x - 1 ) || ( globalPos.x <= boundingBox [ 1 ].x + 1 );   
}


///---------------Main-Shader-Functions----------------///

// Connect missing outline pixels.
// This will be done 2 or 3 times, this way we can connect all the outline pixels.
inline void FindOutlineConnection( int2 localPos, int pixelType )
{
    // if the current pixel is already an outline pixel, return.
    [branch]
    if ( VerifyPixel( PX_OUTLINE, localPos, 0 ) )
    {
        return;
    }
    
    // Reset the fill modified flag.
    [flatten]
    if ( localPos == 0 )
    {
        fillModified = true;
    }
    
    static const int2 scanMatrix [ 8 ] [ 3 ] = 
    { 
        { SCAN_TOP_LEFT, SCAN_TOP, SCAN_TOP_RIGHT }, 
        { SCAN_TOP_RIGHT, SCAN_RIGHT, SCAN_BOTTOM_RIGHT }, 
        { SCAN_TOP, SCAN_TOP_RIGHT, SCAN_RIGHT }, 
        { SCAN_BOTTOM_LEFT, SCAN_LEFT, SCAN_TOP_LEFT }, 
        { SCAN_BOTTOM_LEFT, SCAN_BOTTOM, SCAN_BOTTOM_RIGHT }, 
        { SCAN_BOTTOM, SCAN_LEFT, SCAN_TOP_LEFT }, 
        { SCAN_BOTTOM, SCAN_RIGHT, SCAN_TOP_RIGHT }, 
        { SCAN_TOP, SCAN_LEFT, SCAN_BOTTOM_LEFT } 
    };

    
    
    // Connect the outline pixels.
#ifdef DEBUG
    [loop]
#else
    [unroll]
#endif
    while ( fillModified )
    {
        bool2 fillCurrentPos = false;
        
        // Search for a line of the outline color in any direction.
#ifdef DEBUG
        [loop]
#else
        [unroll(4)]
#endif
        for ( int i = 0; i < 4; i += 2 )
        {
            fillCurrentPos.x = VerifyPixel( pixelType, localPos, scanMatrix [ i ] [ 0 ] ) && VerifyPixel( pixelType, localPos, scanMatrix [ i ] [ 1 ] ) && VerifyPixel( pixelType, localPos, scanMatrix [ i ] [ 2 ] );
            fillCurrentPos.y = VerifyPixel( pixelType, localPos, scanMatrix [ i + 1 ] [ 0 ] ) && VerifyPixel( pixelType, localPos, scanMatrix [ i + 1 ] [ 1 ] ) && VerifyPixel( pixelType, localPos, scanMatrix [ i + 1 ] [ 2 ] );
            
            if ( fillCurrentPos.x | fillCurrentPos.y )
            {
                break;
            }
        }
        
        // redundant check, but it is needed.
        [flatten]
        if ( fillCurrentPos.x | fillCurrentPos.y )
        {
                // If there is a line in any direction, fill the current position.
            localSharedMatrix [ localPos.x ] [ localPos.y ] = PX_OUTLINE;
                
                // Set the fill modified flag.
            InterlockedCompareExchange( fillModified, false, true, 0 );
        }
        else
        {
            InterlockedExchange( fillModified, false, 0 );
        }
        // Sync the threads in the group.
        GroupMemoryBarrierWithGroupSync();
    }    
    GroupMemoryBarrierWithGroupSync();
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
inline void FloodFillBody( int2 localPos, int pixelType )
{
         
    // If the current pixel is hair or outline, return.
    [flatten]
    if ( VerifyPixel( PX_HAIR, localPos, 0 ) || VerifyPixel( PX_OUTLINE, localPos, 0 ) )
    {
        return;
    }
    
    
    static const int2 fillMatrix1 [ 7 ] [ 3 ] =
    { 
        { SCAN_TOP_LEFT, SCAN_TOP, SCAN_TOP_RIGHT }, 
        { SCAN_TOP_RIGHT, SCAN_RIGHT, SCAN_BOTTOM_RIGHT }, 
        { SCAN_TOP, SCAN_TOP_RIGHT, SCAN_RIGHT }, 
        { SCAN_BOTTOM_LEFT, SCAN_LEFT, SCAN_TOP }, 
        { SCAN_BOTTOM_LEFT, SCAN_BOTTOM, SCAN_BOTTOM_RIGHT }, 
        { SCAN_BOTTOM, SCAN_LEFT, SCAN_TOP_LEFT }, 
        { SCAN_BOTTOM, SCAN_RIGHT, SCAN_TOP_RIGHT } 
    };
    
    static const int2 fillMatrix2 [ 5 ] [ 2 ] =
    {
        { SCAN_TOP_LEFT, SCAN_TOP },
        { SCAN_TOP_RIGHT, SCAN_RIGHT },
        { SCAN_BOTTOM_RIGHT, SCAN_BOTTOM },
        { SCAN_BOTTOM_LEFT, SCAN_LEFT },
        { SCAN_TOP_LEFT, SCAN_RIGHT }
    };
    
    // Reset the fill modified flag.
    [flatten]
    if ( localPos == 0 )
    {
        fillModified = true;
    }
    
    // Fill the body of the character.
#ifdef DEBUG
    [loop]
#else
    [unroll]
#endif
    while ( fillModified )
    {
        bool isBody = false;
        
#ifdef DEBUG
        [loop]
#else
        [unroll(7)]
#endif
        for ( int i = 0; i < 7; i++ )
        {
            // Check if the pixel is a body pixel.
            isBody = VerifyPixel( pixelType, localPos, fillMatrix1 [ i ] [ 0 ] ) && VerifyPixel( pixelType, localPos, fillMatrix1 [ i ] [ 1 ] ) && VerifyPixel( pixelType, localPos, fillMatrix1 [ i ] [ 2 ] );
            
            [branch]
            if ( isBody )
            {
                break;
            }
            
            // This checks if we are next to other body pixels, if we are we are a body pixel.
            [flatten]
            if ( i < 5 )
            {
                isBody = VerifyPixel( PX_FLOODFILL, localPos, fillMatrix2 [ i ] [ 0 ] ) && VerifyPixel( PX_FLOODFILL, localPos, fillMatrix2 [ i ] [ 1 ] );
                
                if ( isBody )
                {
                    break;
                }
            }
        }
    
        [branch]
        if ( isBody )
        {
            // Modify the pixel if it is a body pixel.
            localSharedMatrix [ localPos.x ] [ localPos.y ] = PX_FLOODFILL;
            
            // Set the flood fill modified flag.
            InterlockedCompareExchange( fillModified, false, true, 0 );
        }else
        {
            // Set the flood fill modified flag to false.
            InterlockedExchange( fillModified, false, 0 );
        }
        
        // Sync the threads in the group, this way they alll see the initialized values.
        // This syncs the modified flag as well before the next iteration.
        GroupMemoryBarrierWithGroupSync();
    }    
    GroupMemoryBarrierWithGroupSync();
}



// Blend any pixels that are colored as hair but not hair to the background color.
inline void RemoveNonHair( int2 localPos, int pixelType )
{
    bool isHair = false;
    
    // If the curret pixel isnt hair, return.
    [branch]
    if ( !VerifyPixel( PX_HAIR, localPos, 0 ) )
    {
        return;
    }
    
    // Reset the fill modified flag.
    [flatten]
    if ( localPos == 0 )
    {
        fillModified = true;
    }
    
    static const int2 scanMatrix [ 5 ] [ 3 ] =
    {
        { SCAN_TOP_LEFT, SCAN_TOP, SCAN_TOP_RIGHT },
        { SCAN_TOP_RIGHT, SCAN_RIGHT, SCAN_BOTTOM_RIGHT },
        { SCAN_TOP, SCAN_TOP_RIGHT, SCAN_RIGHT },
        { SCAN_BOTTOM_LEFT, SCAN_LEFT, SCAN_TOP_LEFT },
        { SCAN_BOTTOM_LEFT, SCAN_BOTTOM, SCAN_BOTTOM_RIGHT }
    };
    
#ifdef DEBUG
    [loop]
#else
    [unroll]
#endif
    while ( fillModified )
    {
        // Check if the pixel is colored as hair but is not hair.
#ifdef DEBUG
        [loop]
#else
        [unroll(5)]
#endif
        for ( int i = 0; i < 5; i++ )
        {
            isHair = VerifyPixel( pixelType, localPos, scanMatrix [ i ] [ 0 ] ) && VerifyPixel( pixelType, localPos, scanMatrix [ i ] [ 1 ] ) && VerifyPixel( pixelType, localPos, scanMatrix [ i ] [ 2 ] );
            
            [branch]
            if ( isHair )
            {
                break;
            }
        }
    
        // If the pixel is not hair, set it to the background color.
        [branch]
        if ( !isHair )
        {
            localSharedMatrix [ localPos.x ] [ localPos.y ] = PX_BACKGROUND;
            
            // Set the flood fill modified flag.
            InterlockedCompareExchange( fillModified, false, true, 0 );
        }else
        {
            // Set the flood fill modified flag to false.
            InterlockedExchange( fillModified, false, 0 );
        }
        
        // Sync the threads in the group, this way they alll see the initialized values.
        // This syncs the modified flag as well before the next iteration.
        GroupMemoryBarrierWithGroupSync();
    }    
    GroupMemoryBarrierWithGroupSync();
}


inline void CheckAndSetHairCluster( int2 hairPos )
{
    // If there are no hair clusters, add a new hair cluster.
    [flatten]
    if ( hairClusterCount == 0 )
    {
        hairClusters [ hairClusterCount ].AddPosition( hairPos );
        InterlockedAdd( hairClusterCount, 1, 1 );
    }
    
    // Check if the hair position is within the threshold of any of the hair clusters.
    bool hairGroupable = false;
    
#ifdef DEBUG
    [loop]
#else
    [unroll]
#endif
    for ( int i = 0; i < hairClusterCount; i++ )
    {
        hairGroupable = hairClusters [ i ].CheckForGrouping( hairPos );
        
        // If the hair position is within the threshold of the hair cluster, add the hair position to the cluster.
        if ( hairGroupable )
        {
            hairClusters [ i ].AddPosition( hairPos );
        }
    }
    
    // If the hair position is not within the threshold of any of the hair clusters, add a new hair cluster.
    [branch]
    if ( !hairGroupable && hairClusterCount < MAX_PLAYERS )
    {
        int currentHairClusterCount = 0;
        InterlockedAdd( hairClusterCount, 1, currentHairClusterCount + 1 );
        
        if ( currentHairClusterCount >= MAX_PLAYERS ) // Redundant check.
        {
            return;
        }        
        // Add new hair cluster.
        hairClusters [ currentHairClusterCount ].AddPosition( hairPos );
    }
}



// Merge hair clusters that are within the threshold of each other.
// This is very computationally expensive, so we only do it if there is more than 2 hair clusters.
inline void MergeHairClusters()
{
    // Reset fill modified flag
    fillModified = true;
    
    // Merge the hair clusters in the current group.
    while ( fillModified )
    {
        for ( int i = 0, y = 1; i < hairClusterCount & y < hairClusterCount; i++, y++ )
        {
            if ( hairClusters [ i ].IsMerged() )
            {
                continue;
            }
            
            if ( i == y )//< Redunant check
            {
                continue;
            }
            else if ( !hairClusters [ y ].IsMerged() )
            {
                if ( PlusMinus( hairClusters [ i ].GetAveragePos(), hairClusters [ y ].GetAveragePos(), HAIR_MERGE_THRESHOLD ) )
                {
                    // Merge the hair clusters.
                    hairClusters [ i ].MergeCluster( hairClusters [ y ] );
                    
                    // Set the fill modified flag to true.
                    InterlockedExchange( fillModified, true, 0 );
                }
                else
                {
                    //Set the fill modified flag to false.
                    InterlockedExchange( fillModified, false, 0 );
                }                   
            }
            else
            {
                // Set the fill modified flag to false.
                InterlockedExchange( fillModified, false, 0 );
            }            
        }
        // Sync the threads in the group, this way they all see the initialized values.
        GroupMemoryBarrierWithGroupSync();
    }
    GroupMemoryBarrierWithGroupSync();
}



// Check if the pixel is within the range of any of the color ranges.
// If the pixel is within the range, add the pixel to the detected object buffer and set the pixel to the swap color.
// We use buffer indexing because if we load the whole color range it flogs the gpu instruction cache.
// As well as we dont need to load the whole buffer, just the parts we need.
inline int CheckAndSetPixel( int2 localPos, float4 pixelColor, half3 rangeName, out float4 swapColor )
{
    for ( int i = 0; i < NUM_COLOR_RANGES; i++ )
    {
        // Check if the name is the same as the skin name
        [branch]
        if ( !ColorRangeBuffer [ i ].CheckName( rangeName ) )
        {
            continue;
        }
        else
        {

            [branch]
            if ( !ColorRangeBuffer [ i ].CheckAlignment() )
            {
#ifdef DEBUG
                errorf( "ColorRangeBuffer[%d] is not aligned properly", i );
#endif
                return ALIGNMENT_ERROR;
            }
            else if ( !ColorRangeBuffer [ i ].HasRanges() )
            {
#ifdef DEBUG
                errorf( "ColorRangeBuffer[%d] has no ranges", i );
#endif
                return NO_RANGE_ERROR;
            }
            else
            {
#ifdef DEBUG
                [loop]
#else
                [unroll]
#endif
                for ( int n = 0; n < ColorRangeBuffer [ i ].numOfRanges; n++ )
                {
                    [branch]
                    if ( ColorRangeBuffer [ i ].IsInRange( pixelColor, n ) )
                    {
                        // Set output swap color to the skin swap color .
                        swapColor = ColorRangeBuffer [ i ].swapColor;
                           
                        // return the proper mofifier.
                        switch ( rangeName )
                        {
                            case COLOR_NAME_HAIR:
                                // Set local shared matrix to desired pixel type.
                                localSharedMatrix [ localPos.x ] [ localPos.y ] = PX_HAIR;
                                // Set hair position.
                                CheckAndSetHairCluster( localPos );
                                return SET_HAIR;
                                break;
                            case COLOR_NAME_SKIN:
                                localSharedMatrix [ localPos.x ] [ localPos.y ] = PX_SKIN;
                                return SET_SKIN;
                                break;
                            case COLOR_NAME_OUTLNZ:
                                localSharedMatrix [ localPos.x ] [ localPos.y ] = PX_OUTLINE;
                                return SET_OUTLINE;
                                break;
                        }
                    }
                }
            }
        }
    }
    return SET_BACKGROUND;
}


// This is a atomic reduction function that will reduce the bounding box of the filled area.
// this starts the bounding box at the size of the texture.
// Each time a thread finds a filled pixel it will reduce the bounding box.
inline void BoundingBoxReductionHelper( int2 localPos, int segmentIndex )
{
    [flatten]
    if ( localSharedMatrix [ localPos.x ] [ localPos.y ] == PX_FLOODFILL ||
        localSharedMatrix [ localPos.x ] [ localPos.y ] == PX_OUTLINE )
    {
        InterlockedMax( groupMax [ segmentIndex ].x, localPos.x);
        InterlockedMax( groupMax [ segmentIndex ].y, localPos.y);
        InterlockedMin( groupMin [ segmentIndex ].x, localPos.x);
        InterlockedMin( groupMin [ segmentIndex ].y, localPos.y);
    }
}


// This will calculate max players worth of bounding boxes, per thread group.
// in the default case thats 6 bounding boxes.
inline void GetBoundingBoxPositions( int2 localPos )
{
    // this way is faster than using a loop with 1 thread.
    [flatten]
    if ( localPos < MAX_PLAYERS)
    {
        // Set the group min/max be the size of the texture, this is much larger than the actual bounding box.
        // This way we can reduce the bounding box to the actual size.
        groupMin [ localPos.x ] = int2( WINDOW_SIZE_X, WINDOW_SIZE_Y );
        groupMax [ localPos.x ] = int2( 0, 0 );
    }

    // Sync the threads in the group, this way they all see the initialized values.
    GroupMemoryBarrierWithGroupSync();
          
    // Reduce the bounding box to the current detected size.
    BoundingBoxReductionHelper( localPos, SEGMENT_CALC( localPos ) );
    
    GroupMemoryBarrierWithGroupSync();
}


// This will merge the bounding boxes of the segments that are within the threshold of each other.
inline void BoundingBoxMergeHelper()
{
    // Reset the fill modified flag.
    fillModified = true;
    
    // Merge the bounding boxes in the current group.
    while ( fillModified )
    {
        for ( int i = 0, y = 1; i < MAX_PLAYERS & y < MAX_PLAYERS; i++, y++ )
        {
            if ( groupMin [ i ] == MERGED_FLAG & groupMax [ i ] == MERGED_FLAG )
            {
                continue;
            }
            
           
            if ( i == y ) //< Redundant check.
            {
                continue;
            }
            
            else if ( groupMin [ y ] != MERGED_FLAG & groupMax [ y ] != MERGED_FLAG )
            {
                // if the bounding boxes are beside each other on X axis, merge them.
                if ( PlusMinus( groupMin [ i ].x, groupMax [ y ].x, BB_MERGE_THRESHOLD ) )
                {
                    InterlockedMax( groupMax [ i ].x, groupMax [ y ].x );
                    InterlockedMin( groupMin [ i ].x, groupMin [ y ].x );
                    InterlockedMax( groupMax [ i ].y, groupMax [ y ].y );
                    InterlockedMin( groupMin [ i ].y, groupMin [ y ].y );
                        
                    // Set the merged flag.
                    InterlockedExchange( groupMin [ y ].x, MERGED_FLAG, 0 );
                    InterlockedExchange( groupMin [ y ].y, MERGED_FLAG, 0 );
                    InterlockedExchange( groupMax [ y ].x, MERGED_FLAG, 0 );
                    InterlockedExchange( groupMax [ y ].y, MERGED_FLAG, 0 );
                        
                    // Set the fill modified flag to true.
                    InterlockedExchange( fillModified, true, 0 );
                }
                else if ( PlusMinus( groupMin [ i ].y, groupMax [ y ].y, BB_MERGE_THRESHOLD ) ) //< If the bounding boxes are above or below each other on the Y axis, merge them.
                {
                    InterlockedMax( groupMax [ i ].x, groupMax [ y ].x );
                    InterlockedMin( groupMin [ i ].x, groupMin [ y ].x );
                    InterlockedMax( groupMax [ i ].y, groupMax [ y ].y );
                    InterlockedMin( groupMin [ i ].y, groupMin [ y ].y );
                        
                    // Set the merged flag.
                    InterlockedExchange( groupMin [ y ].x, MERGED_FLAG, 0 );
                    InterlockedExchange( groupMin [ y ].y, MERGED_FLAG, 0 );
                    InterlockedExchange( groupMax [ y ].x, MERGED_FLAG, 0 );
                    InterlockedExchange( groupMax [ y ].y, MERGED_FLAG, 0 );
                        
                    // Set the fill modified flag to true.
                    InterlockedExchange( fillModified, true, 0 );
                }
                else
                {
                    // Set the fill modified flag to false.
                    InterlockedExchange( fillModified, false, 0 );
                }
            }
            else
            {
                // Set the fill modified flag to false.
                InterlockedExchange( fillModified, false, 0 );
            }
            
        }
        // Sync the threads in the group, this way they all see the initialized values.
        GroupMemoryBarrierWithGroupSync();
    }    
    GroupMemoryBarrierWithGroupSync();
}


inline void AssignUniqueIds( int segmentPos, int2 groupId )
{
    int2 linkedBBC = int2( -1, -1 );

    for ( int i = 0; i < hairClusterCount; i++ )
    {                   
        // Check if hair centroid is inside box
        if ( BbToHairLink( groupMin [ segmentPos ], groupMax [ segmentPos ], hairClusters [ i ].GetAveragePos() ) )
        {
            linkedBBC.x = 0x111;
            linkedBBC.y = i;
        }
    }
    
    // If hair Centroid is in bounding box.
    if ( linkedBBC.x == 0x111 )
    {
            // Add the hair centroid and min/max values to goup detail buffer.
        GroupDetailsBuffer [ groupId.x ].AddGroupDetail( groupMin [ segmentPos ], groupMax [ segmentPos ], hairClusters [ linkedBBC.y ].GetAveragePos(), GenerateUniqueId(), segmentPos );
    }
    else
    {
        // If there is no hair centroid for box then add box to group detail buffer with global minimum position as unique id.
        int2 globalMin = GroupPosToGlobal( groupMin [ segmentPos ], groupId );
        GroupDetailsBuffer [ groupId.x ].AddBoundingBox( groupMin [ segmentPos ], groupMax [ segmentPos ], GLOBAL_MIN_TO_UID( globalMin ), segmentPos );
        
        // Add a hair centroid to group details
        if ( segmentPos < hairClusterCount)
        {
            int2 globalHairPos = GroupPosToGlobal( hairClusters [ linkedBBC.y ].GetAveragePos(), groupId );
            GroupDetailsBuffer [ groupId.x ].AddHairCentroid( hairClusters [ segmentPos ].GetAveragePos(), GLOBAL_MIN_TO_UID( globalHairPos ), segmentPos );
        }
    }
}


// Look at the local matrix and get the details for the global matrix.
// The swap color input will only be valid if the pixel type is hair, outline or skin.
// Otherwise the flood fill and background colors are global.
// Im hoping to only use this for debugging
inline void GetAndSetDetailsForGlobal( int2 localPos, int2 globalPos, float4 swapColor )
{
    [branch]
    if ( any( swapColor ) )
    {
        UavBuffer [ globalPos ] = swapColor;
    }
    else
    {
        UavBuffer [ globalPos ] = localSharedMatrix [ localPos.x ] [ localPos.y ] == PX_FLOODFILL ? OBJECT_FILL_COLOR : BACKGROUND_PIXEL_COLOR;
    }
    
    // Sync everything.
    DeviceMemoryBarrier();
}


inline void MergeGlobalDetails( int segmentPos, int2 groupId)
{
    // Make sure loop flag is reset.
    loopFlag = true;
    
    while ( loopFlag )
    {
        // Sync all threads
        DeviceMemoryBarrier();
        
        if ( segmentPos < MAX_PLAYERS && GroupDetailsBuffer [ groupId.x ].boundingBoxes [ segmentPos ].IsMerged() )
        {
            for ( int i = 0, otherSegmentPos = 0; i < NUM_GROUPS; i++, otherSegmentPos = 0 )
            {
                if ( i == groupId.x )
                {
                    continue;
                }
                else
                {
                    BoundingBox boundingBoxes [ MAX_PLAYERS ];
                    HairCentroid hairCentroids [ MAX_PLAYERS ];
                    HairCentroid ourThreadsHairCentroid;
                
                    // Get other groups details.
                    GroupDetailsBuffer [ i ].GetAllGroupDetails( boundingBoxes, hairCentroids );
                
                    // Get this threads hair centroid.
                    GroupDetailsBuffer [ groupId.x ].GetHairCentroid( segmentPos, ourThreadsHairCentroid );
                
                    while ( otherSegmentPos < MAX_PLAYERS )
                    {
                        if ( !hairCentroids [ otherSegmentPos ].IsMerged() && PlusMinus( hairCentroids [ otherSegmentPos ].position, ourThreadsHairCentroid.position, HAIR_MERGE_THRESHOLD ) & hairCentroids [ otherSegmentPos ].isLinked() )
                        {
                            ( ourThreadsHairCentroid.position += hairCentroids [ otherSegmentPos ].position ) / 2;
                            BoundingBox ourBoundingBox;
                        
                            // Merge bounding boxs.
                            InterlockedMin( GroupDetailsBuffer [ groupId.x ].boundingBoxes [ segmentPos ].min.x, boundingBoxes [ otherSegmentPos ].min.x );
                            InterlockedMin( GroupDetailsBuffer [ groupId.x ].boundingBoxes [ segmentPos ].min.y, boundingBoxes [ otherSegmentPos ].min.y );
                            InterlockedMax( GroupDetailsBuffer [ groupId.x ].boundingBoxes [ segmentPos ].max.x, boundingBoxes [ otherSegmentPos ].max.x );
                            InterlockedMax( GroupDetailsBuffer [ groupId.x ].boundingBoxes [ segmentPos ].max.y, boundingBoxes [ otherSegmentPos ].max.y );
                        
                            // Set merge flag for both.
                            GroupDetailsBuffer [ i ].SetMergedFlag( otherSegmentPos, true, true );
                                                                
                            // Make sure flag stays true.
                            InterlockedExchange( loopFlag, true, 0 );
                        }
                        else if ( !hairCentroids [ otherSegmentPos ].IsMerged() && !boundingBoxes [ otherSegmentPos ].isLinked() )
                        {
                            
                            // If the two bounding boxes are within the threshold merge them.
                            if ( PlusMinus( boundingBoxes [ otherSegmentPos ].min.y, GroupDetailsBuffer [ groupId.x ].boundingBoxes [ segmentPos ].max.y, BB_MERGE_THRESHOLD ) ||
                             PlusMinus( boundingBoxes [ otherSegmentPos ].min.x, GroupDetailsBuffer [ groupId.x ].boundingBoxes [ segmentPos ].max.x, BB_MERGE_THRESHOLD ) ||
                              PlusMinus( boundingBoxes [ otherSegmentPos ].max.y, GroupDetailsBuffer [ groupId.x ].boundingBoxes [ segmentPos ].min.y, BB_MERGE_THRESHOLD ) ||
                              PlusMinus( boundingBoxes [ otherSegmentPos ].max.x, GroupDetailsBuffer [ groupId.x ].boundingBoxes [ segmentPos ].min.x, BB_MERGE_THRESHOLD ) )
                            {
                                // Merge bounding boxs.
                                InterlockedMin( GroupDetailsBuffer [ groupId.x ].boundingBoxes [ segmentPos ].min.x, boundingBoxes [ otherSegmentPos ].min.x );
                                InterlockedMin( GroupDetailsBuffer [ groupId.x ].boundingBoxes [ segmentPos ].min.y, boundingBoxes [ otherSegmentPos ].min.y );
                                InterlockedMax( GroupDetailsBuffer [ groupId.x ].boundingBoxes [ segmentPos ].max.x, boundingBoxes [ otherSegmentPos ].max.x );
                                InterlockedMax( GroupDetailsBuffer [ groupId.x ].boundingBoxes [ segmentPos ].max.y, boundingBoxes [ otherSegmentPos ].max.y );
                        
                                // Set merge flag for both.
                                GroupDetailsBuffer [ i ].SetMergedFlag( otherSegmentPos, false, true );
                                                                
                                // Make sure flag stays true.
                                InterlockedExchange( loopFlag, true, 0 );
                            }
                        }
                        else
                        {
                            // Set flag to false
                            InterlockedExchange( loopFlag, false, 0 );
                        
                        }                        
                        // Increment other segment position
                        otherSegmentPos++;
                    }
                }
            }
        }
    }    
    DeviceMemoryBarrier();
}



// This will merge the potential 6 bounding boxes into 1 bounding box.
// Get the locations of the average hair position(s).
// Add the details to the group details buffer.
inline void SetGroupDetails( int2 localPos, int2 groupId, int2 globalPos, float4 swapColor )
{
    // Add all bounding boxes to the group details uav buffer.
    // This also assigns unique id's to mathching boxes and hair centroids.
    // Else it uses the global position of the min x/y as the unique id.
    int segmentPos = SEGMENT_POS_CALC( localPos );
    if ( segmentPos < MAX_PLAYERS )
    {
        AssignUniqueIds( segmentPos, groupId );
    }
    
    // Sync all threads, as we just wrote to a UAV buffer.
    DeviceMemoryBarrier();
    
#ifdef DEBUG
    // Update texture with all edited details.
    GetAndSetDetailsForGlobal( localPos, globalPos, swapColor );
#endif
    
    // Merge the group buffer details.
    MergeGlobalDetails( segmentPos, groupId );
}
  
  
// Draws bounding box on texture 3 pixels wide. 
inline void DrawBoundingBox( int2 globalPos )
{
    for ( int i = 0; i < MAX_PLAYERS; i++ )
    {
        UavBuffer [ globalPos ] = IsPixelBoundingBox( globalPos, PlayerPositionBuffer [ 0 ].players [ i ].boundingBox ) ? BOUNDING_BOX_COLOR : UavBuffer [ globalPos ];
    }
}