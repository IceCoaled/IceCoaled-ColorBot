#include "ShaderDefines.fxh"
///----------Functions-----------///


// Check if the pixel is an outline pixel.
inline bool VerifyPixel( int pixelType, int2 localPos, int2 scanOffset )
{
    return all( pixelType - localSharedMatrix [ localPos.x + scanOffset.x ] [ localPos.y + scanOffset.y ] ) ? false : true;
}


// Connect missing outline pixels.
// This will be done 2 or 3 times, this way we can connect all the outline pixels.
inline void FindOutlineConnection( int2 localPos, int pixelType )
{
    while ( fillModified )
    {
        bool2 fillCurrentPos = false;

        // Check if there is a line of the outline color in any direction.
        [forcecase]
        switch ( true )
        {
            case 0:
                fillCurrentPos.x = VerifyPixel( pixelType, localPos, ScanOffsets [ 0 ] );
                fillCurrentPos.y = VerifyPixel( pixelType, localPos, ScanOffsets [ 7 ] );
                break;
            case 1:
                if ( !fillCurrentPos.x || !fillCurrentPos.y )
                {
                    fillCurrentPos.x = VerifyPixel( pixelType, localPos, ScanOffsets [ 1 ] );
                    fillCurrentPos.y = VerifyPixel( pixelType, localPos, ScanOffsets [ 6 ] );
                }
                break;
            case 2:
                if ( !fillCurrentPos.x || !fillCurrentPos.y )
                {
                    fillCurrentPos.x = VerifyPixel( pixelType, localPos, ScanOffsets [ 2 ] );
                    fillCurrentPos.y = VerifyPixel( pixelType, localPos, ScanOffsets [ 5 ] );
                }
                break;
            case 3:
                if ( !fillCurrentPos.x || !fillCurrentPos.y )
                {
                    fillCurrentPos.x = VerifyPixel( pixelType, localPos, ScanOffsets [ 3 ] );
                    fillCurrentPos.y = VerifyPixel( pixelType, localPos, ScanOffsets [ 4 ] );
                }
                break;
            default:
                return;
        }
      
       
        if ( fillCurrentPos.x || fillCurrentPos.y )
        {
            // If there is a line in any direction, fill the current position.
            localSharedMatrix [ localPos.x ] [ localPos.y ] = PX_OUTLINE;
            
            // Set the fill modified flag.
            InterlockedCompareExchange( fillModified, false, true, 0 );
        }
        else
        {
            // Set the fill modified flag to false.
            InterlockedCompareExchange( fillModified, true, false, 0 );
        }
        
        // Sync the threads in the group, this way they alll see the initialized values.
        // this sync the mofiied flag as well before the next iteration.
        GroupMemoryBarrierWithGroupSync();
    }
}


inline void RemoveNonSkin( int2 localPos, int pixelType )
{
    bool isSkin = false;
    
    [forcecase]
    switch ( true )
    {
        case 0:
            isSkin = VerifyPixel( pixelType, localPos, ScanOffsets [ 2 ] ) && VerifyPixel( pixelType, localPos, ScanOffsets [ 4 ] ) && VerifyPixel( pixelType, localPos, ScanOffsets [ 8 ] );
            break;
        case 1:
            if ( !isSkin )
            {
                isSkin = VerifyPixel( pixelType, localPos, ScanOffsets [ 0 ] ) && VerifyPixel( pixelType, localPos, ScanOffsets [ 3 ] ) && VerifyPixel( pixelType, localPos, ScanOffsets [ 5 ] );
            }
            break;
        case 2:
            if ( !isSkin )
            {
                isSkin = VerifyPixel( pixelType, localPos, ScanOffsets [ 1 ] ) && VerifyPixel( pixelType, localPos, ScanOffsets [ 2 ] ) && VerifyPixel( pixelType, localPos, ScanOffsets [ 4 ] );
            }
        case 3:
            if ( !isSkin )
            {
                isSkin = VerifyPixel( pixelType, localPos, ScanOffsets [ 5 ] ) && VerifyPixel( pixelType, localPos, ScanOffsets [ 3 ] ) && VerifyPixel( pixelType, localPos, ScanOffsets [ 1 ] );
            }
            break;
        default:
            return;
    }
    
    if ( !isSkin )
    {
        localSharedMatrix [ localPos.x ] [ localPos.y ] = PX_BACKGROUND;
    }
}



// Flood fill the body of the character.
inline void FloodFillBody( int2 localPos, int pixelType )
{
    while ( fillModified )
    {
        bool isBody = false;
    
        [forcecase]
        switch ( true )
        {
            case 0:
                isBody = VerifyPixel( pixelType, localPos, ScanOffsets [ 0 ] ) && VerifyPixel( pixelType, localPos, ScanOffsets [ 1 ] ) && VerifyPixel( pixelType, localPos, ScanOffsets [ 2 ] );
                break;
            case 1:
                if ( !isBody )
                {
                    isBody = VerifyPixel( pixelType, localPos, ScanOffsets [ 2 ] ) && VerifyPixel( pixelType, localPos, ScanOffsets [ 4 ] ) && VerifyPixel( pixelType, localPos, ScanOffsets [ 7 ] );
                }
                break;
            case 2:
                if ( !isBody )
                {
                    isBody = VerifyPixel( pixelType, localPos, ScanOffsets [ 1 ] ) && VerifyPixel( pixelType, localPos, ScanOffsets [ 2 ] ) && VerifyPixel( pixelType, localPos, ScanOffsets [ 4 ] );
                }
                break;
            case 3:
                if ( !isBody )
                {
                    isBody = VerifyPixel( pixelType, localPos, ScanOffsets [ 5 ] ) && VerifyPixel( pixelType, localPos, ScanOffsets [ 3 ] ) && VerifyPixel( pixelType, localPos, ScanOffsets [ 1 ] );
                }
                break;
            case 4:
                if ( !isBody )
                {
                    isBody = VerifyPixel( pixelType, localPos, ScanOffsets [ 5 ] ) && VerifyPixel( pixelType, localPos, ScanOffsets [ 6 ] ) && VerifyPixel( pixelType, localPos, ScanOffsets [ 7 ] );
                }
                break;
            case 5:
                if ( !isBody )
                {
                    isBody = VerifyPixel( pixelType, localPos, ScanOffsets [ 6 ] ) && VerifyPixel( pixelType, localPos, ScanOffsets [ 3 ] ) && VerifyPixel( pixelType, localPos, ScanOffsets [ 0 ] );
                }
                break;
            case 6:
                if ( !isBody )
                {
                    isBody = VerifyPixel( pixelType, localPos, ScanOffsets [ 6 ] ) && VerifyPixel( pixelType, localPos, ScanOffsets [ 4 ] ) && VerifyPixel( pixelType, localPos, ScanOffsets [ 2 ] );
                }
                break;
            case 7:
                if ( !isBody )
                {
                    isBody = VerifyPixel( PX_FLOODFILL, localPos, ScanOffsets [ 0 ] ) && VerifyPixel( pixelType, localPos, ScanOffsets [ 2 ] );
                }
                break;
            case 8:
                if ( !isBody )
                {
                    isBody = VerifyPixel( PX_FLOODFILL, localPos, ScanOffsets [ 5 ] ) && VerifyPixel( pixelType, localPos, ScanOffsets [ 0 ] );
                }
                break;
            case 9:
                if ( !isBody )
                {
                    isBody = VerifyPixel( PX_FLOODFILL, localPos, ScanOffsets [ 2 ] ) && VerifyPixel( pixelType, localPos, ScanOffsets [ 7 ] );
                }
                break;
            case 10:
                if ( !isBody )
                {
                    isBody = VerifyPixel( PX_FLOODFILL, localPos, ScanOffsets [ 0 ] ) && VerifyPixel( pixelType, localPos, ScanOffsets [ 7 ] );
                }
                break;
            case 11:
                if ( !isBody )
                {
                    isBody = VerifyPixel( PX_FLOODFILL, localPos, ScanOffsets [ 2 ] ) && VerifyPixel( pixelType, localPos, ScanOffsets [ 5 ] );
                }
                break;
            case 12:
                if ( !isBody )
                {
                    isBody = VerifyPixel( PX_FLOODFILL, localPos, ScanOffsets [ 1 ] ) && VerifyPixel( pixelType, localPos, ScanOffsets [ 6 ] );
                }
                break;
            case 13:
                if ( !isBody )
                {
                    isBody = VerifyPixel( PX_FLOODFILL, localPos, ScanOffsets [ 3 ] ) && VerifyPixel( pixelType, localPos, ScanOffsets [ 4 ] );
                }
                break;
            default:
                return;
        }
          
        if ( isBody )
        {
            // Modify the pixel if it is a body pixel.
            localSharedMatrix [ localPos.x ] [ localPos.y ] = PX_FLOODFILL;
            
            // Set the flood fill modified flag.
            InterlockedCompareExchange( fillModified, false, true, 0 );
        }
        else
        {
            // Set the flood fill modified flag to false.
            InterlockedCompareExchange( fillModified, true, false, 0 );
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
    
    // Check if the pixel is colored as hair but is not hair.
    [forcecase]
    switch ( true )
    {
        case 0:
            isHair = VerifyPixel( pixelType, localPos, ScanOffsets [ 0 ] ) && VerifyPixel( pixelType, localPos, ScanOffsets [ 1 ] ) && VerifyPixel( pixelType, localPos, ScanOffsets [ 2 ] );
            break;
        case 1:
            if ( !isHair )
            {
                isHair = VerifyPixel( pixelType, localPos, ScanOffsets [ 3 ] ) && VerifyPixel( pixelType, localPos, ScanOffsets [ 0 ] ) && VerifyPixel( pixelType, localPos, ScanOffsets [ 1 ] );
            }
            break;
        case 2:
            if ( !isHair )
            {
                isHair = VerifyPixel( pixelType, localPos, ScanOffsets [ 1 ] ) && VerifyPixel( pixelType, localPos, ScanOffsets [ 2 ] ) && VerifyPixel( pixelType, localPos, ScanOffsets [ 4 ] );
            }
            break;
        default:
            return;
    }
    
    // If the pixel is not hair, set it to the background color.
    if ( !isHair )
    {
        localSharedMatrix [ localPos.x ] [ localPos.y ] = PX_BACKGROUND;
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
    
    if ( clusterCount > 2 && localId < ( clusterCount >> 1 ) )
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
    if ( localSharedMatrix [ localPos.x ] [ localPos.y ] == PX_FLOODFILL )
    {
        InterlockedMax( groupMax [ segmentIndex ].x, localPos.x, groupMax [ segmentIndex ].x );
        InterlockedMax( groupMax [ segmentIndex ].y, localPos.y, groupMax [ segmentIndex ].y );
        InterlockedMin( groupMin [ segmentIndex ].x, localPos.x, groupMin [ segmentIndex ].x );
        InterlockedMin( groupMin [ segmentIndex ].y, localPos.y, groupMin [ segmentIndex ].y );
    }
}


// This will calculate max players worth of bounding boxes, per thread group.
// in the default case thats 6 bounding boxes.
inline void GetBoundingBoxPositions( int2 localPos )
{
    // this way is faster than using a loop with 1 thread.
    if ( localPos < SEGMENT_SIZE )
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

inline void SetPlayerBoundingBoxs( int2 localPos, int clusterCount, int2 globalPos )
{
    bool avgPixel = false;
    // Get the bounding box positions that contain the hair clusters.    
    if ( SEGMENT_CALC( localPos ) == hairClusters [ SEGMENT_CALC( localPos ) ].GetClusterId() ||
        SEGMENT_CALC( localPos ) == hairClusters [ SEGMENT_CALC( localPos ) ].GetClusterId() * 2 )
    {
        avgPixel = localPos == hairClusters [ SEGMENT_CALC( localPos ) ].GetAveragePos();     
    }
    
    // Sync the threads
    GroupMemoryBarrierWithGroupSync();  
    
    // Join any bounding boxes that are around the current target into one bounding box.
    // We know where the head is, and we got the info to know if the current pixel is the average pixel of the hair cluster.
    
    
   
}





