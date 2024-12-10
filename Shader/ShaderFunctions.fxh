﻿#include "ShaderDefines.fxh"
///----------Functions-----------///


// Check if the current pixel is the same as the pixel type.
inline bool VerifyPixel( int pixelType, int2 localPos, int2 scanOffset )
{
    return pixelType == localSharedMatrix [ localPos.x + scanOffset.x ] [ localPos.y + scanOffset.y ]  ? true : false;
}


// Connect missing outline pixels.
// This will be done 2 or 3 times, this way we can connect all the outline pixels.
inline void FindOutlineConnection( int2 localPos, int pixelType )
{
    // if the current pixel is already an outline pixel, return.
    if ( VerifyPixel( PX_OUTLINE, localPos, 0 ) )
    {
        return;
    }
    
    // Reset the fill modified flag.
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
    while ( fillModified )
    {
        bool2 fillCurrentPos = false;
        
        // Search for a line of the outline color in any direction.
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
    if ( localPos == 0 )
    {
        fillModified = true;
    }
    
    // Fill the body of the character.
    while ( fillModified )
    {
        bool isBody = false;
        
        for ( int i = 0; i < 7; i++ )
        {
            // Check if the pixel is a body pixel.
            isBody = VerifyPixel( pixelType, localPos, fillMatrix1 [ i ] [ 0 ] ) && VerifyPixel( pixelType, localPos, fillMatrix1 [ i ] [ 1 ] ) && VerifyPixel( pixelType, localPos, fillMatrix1 [ i ] [ 2 ] );
            
            if ( isBody )
            {
                break;
            }
            
            // This checks if we are next to other body pixels, if we are we are a body pixel.
            if ( i < 5 )
            {
                isBody = VerifyPixel( PX_FLOODFILL, localPos, fillMatrix2 [ i ] [ 0 ] ) && VerifyPixel( PX_FLOODFILL, localPos, fillMatrix2 [ i ] [ 1 ] );
                
                if ( isBody )
                {
                    break;
                }
            }
        }
    
                
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
}



// Blend any pixels that are colored as hair but not hair to the background color.
inline void RemoveNonHair( int2 localPos, int pixelType )
{
    bool isHair = false;
    
    // If the curret pixel isnt hair, return.
    if ( !VerifyPixel( PX_HAIR, localPos, 0 ) )
    {
        return;
    }
    
    // Reset the fill modified flag.
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
    
    while ( fillModified )
    {
        // Check if the pixel is colored as hair but is not hair.
        for ( int i = 0; i < 5; i++ )
        {
            isHair = VerifyPixel( pixelType, localPos, scanMatrix [ i ] [ 0 ] ) && VerifyPixel( pixelType, localPos, scanMatrix [ i ] [ 1 ] ) && VerifyPixel( pixelType, localPos, scanMatrix [ i ] [ 2 ] );
            
            if ( isHair )
            {
                break;
            }
        }
    
        // If the pixel is not hair, set it to the background color.
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
}


inline void CheckAndSetHairCluster( int2 hairPos )
{
    // If there are no hair clusters, add a new hair cluster.
    if ( hairClusterCount == 0 )
    {
        InterlockedAdd( hairClusterCount, 1, 1 );
        hairClusters [ 0 ].SetClusterId( SEGMENT_CALC( hairPos ) );
    }
    
    // Check if the hair position is within the threshold of any of the hair clusters.
    bool hairGroupable = false;
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
    if ( !hairGroupable )
    {
        int currentHairClusterCount = 0;
        InterlockedAdd( hairClusterCount, 1, currentHairClusterCount + 1 );
        
        if ( currentHairClusterCount >= ( MAX_PLAYERS * 4 ) )
        {
            return;
        }
        
        // Add new hair cluster.
        hairClusters [ currentHairClusterCount ].SetClusterId( SEGMENT_CALC( hairPos * 2 ) ); //< i dont suspect we will get a ton of hair clusters, so this will be used when we look to merge clusters.
        hairClusters [ currentHairClusterCount ].AddPosition( hairPos );
    }

}



// Merge hair clusters that are within the threshold of each other.
// This is very computationally expensive, so we only do it if there is more than 2 hair clusters.
inline void MergeHairClusters( int clusterCount, int2 localId )
{
    bool oddClusters = clusterCount % 2;
    
    if ( clusterCount > 2 && localId <= ( clusterCount >> 1 ) )
    {
        if ( distance( asfloat( hairClusters [ localId.x ].GetAveragePos() ), asfloat( hairClusters [ PARALLEL_POS_CALC( localId, clusterCount ) ].GetAveragePos() ) < HAIR_CLUSTER_THRESHOLD ) )
        {
            // Merge the hair clusters.
            hairClusters [ localId.x ].MergeCluster( hairClusters [ PARALLEL_POS_CALC( localId, clusterCount ) ] );
            
            // Update the cluster count.
            InterlockedAdd( hairClusterCount, -1, 0 );
            InterlockedAdd( clusterCount, -1, 0 );
        }
    }
    
    // Sync the threads
    GroupMemoryBarrierWithGroupSync();
    
    if ( oddClusters && clusterCount > 1 && localId == 0 )
    {
        if ( distance( asfloat( hairClusters [ localId.x ].GetAveragePos() ), asfloat( hairClusters [ clusterCount - 1 ].GetAveragePos() ) < HAIR_CLUSTER_THRESHOLD ) )
        {
            hairClusters [ localId.x ].MergeCluster( hairClusters [ clusterCount - 1 ] );
        }
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
                    [flatten]
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
    if ( localSharedMatrix [ localPos.x ] [ localPos.y ] == PX_FLOODFILL )
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
    if ( localPos <= MAX_PLAYERS)
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
inline void BoundingBoxMergeHelper( int segmentIndex)
{
#ifdef DEBUG
    [loop]
#else
    [unroll]
#endif
    for ( int i = 0; i < MAX_PLAYERS; i++ )
    {
        if ( segmentIndex != i && distance( asfloat( groupMin [ segmentIndex ] ), asfloat( groupMin [ i ] ) ) <= BOUNDINGBOX_MERGE_THRESHOLD )
        {
            int2 outMax = int2( 0, 0 );
            int2 outMin = int2( 0, 0 );
            InterlockedMax( groupMax [ segmentIndex ].x, groupMax [ i ].x, outMax.x );
            InterlockedMax( groupMax [ segmentIndex ].y, groupMax [ i ].y, outMax.y );
            InterlockedMin( groupMin [ segmentIndex ].x, groupMin [ i ].x, outMin.x );
            InterlockedAdd( groupMin [ segmentIndex ].y, groupMin [ i ].y, outMin.y );
            
            // Remove any bounding boxes that have been merged.
            [flatten]
            if ( all(outMax) && all(outMin) && distance( asfloat( groupMax [ i ] ), asfloat( outMax ) ) <= BOUNDINGBOX_MERGE_THRESHOLD )
            {
                InterlockedAdd( groupMax [ i ].x, -groupMax [ i ].x, 0 );
                InterlockedAdd( groupMax [ i ].y, -groupMax [ i ].y, 0 );
                InterlockedAdd( groupMin [ i ].x, -groupMin [ i ].x, 0 );
                InterlockedAdd( groupMin [ i ].y, -groupMin [ i ].y, 0 );
            }
        }
    }
}

// This will merge the potential 6 bounding boxes into 1 bounding box.
// Get the locations of the average hair position(s).
// Add the details to the group details buffer.
inline void SetGroupDetails( int2 localPos, int groupId, int2 globalPos )
{
    int2 averageHairPos = int2( 0, 0 );
    int2 gMin = int2( 0, 0 );
    int2 gMax = int2( 0, 0 );
    int segmentId = SEGMENT_CALC( localPos );
    
    // Get hair cluster details.
    [flatten]
    if ( localPos >= hairClusterCount )
    {
        averageHairPos = hairClusters [ localPos.x ].GetAveragePos();
    }
    
    // Join any of the bounding boxes from each segment, if they are within the threshold.   
    BoundingBoxMergeHelper( segmentId );
    
    // Sync the threads in the group, this way they all see the initialized values.
    GroupMemoryBarrierWithGroupSync();
    
    // Run the bounding box merge again, just in case we missed any.
    BoundingBoxMergeHelper( segmentId );
    
    GroupMemoryBarrierWithGroupSync();
    
    if ( averageHairPos != 0 )
    {
        // If the average hair position is within the threshold of the bounding box we keep it.
        averageHairPos = clamp( distance( asfloat( averageHairPos ), asfloat( groupMin [ segmentId ] ) ), 0, HAIR_TO_BOUNDINGBOX_THRESHOLD ) == averageHairPos ? averageHairPos : 0;
        
        // if this hair cluster is the one that matches the bounding box, set the min/max for the bounding box.
        [branch]
        if ( averageHairPos != 0 )
        {
            gMin = groupMin [ segmentId ];
            gMax = groupMax [ segmentId ];
        }
    }
    
    // Set the group details.
    [branch]
    if ( all( gMin ) && all( gMax ) && all( averageHairPos ) )
    {
        GroupDetailsBuffer [ groupId ].SetGroupMinMax( gMin, gMax, segmentId );
        GroupDetailsBuffer [ groupId ].SetHairClusterPos( averageHairPos, segmentId );
    } 

    // Sync everything.
    // This is because we are writing to a unordered access buffer.
    DeviceMemoryBarrier();
}



// Look at the local matrix and get the details for the global matrix.
// The swap color input will only be valid if the pixel type is hair, outline or skin.
// Otherwise the flood fill and background colors are global.
inline void GetAndSetDetailsForGlobal(int2 localPos, int2 globalPos , float4 swapColor)
{
    [branch]
    if (any(swapColor))
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






