#include "ShaderFunctions.hlsli"

//----------Main-----------///

// There is some desisions made in main to minimize temp/indexable temp registers being used. 
// I wouldnt ever use another variables init value to init other over variables typically unless its meaningful.
// In this case ive made this desision to optimize the shader a bit.

[numthreads( X_THREADGROUP, Y_THREADGROUP, 1 )]
void main( uint3 ThreadId : SV_DispatchThreadID,
           uint3 LocalId : SV_GroupThreadID,
           uint3 GroupId : SV_GroupID )
{
    
    UavBuffer [ ThreadId.xy ] = OBJECT_FILL_COLOR;
    ColorRangeBuffer [ 0 ].CheckAlignment();
    GroupDetailsBuffer [ 0 ].CheckAlignment();
    PlayerPositionBuffer [ 0 ].CheckAlignment();
    
//    // Flag to block out any threads that get errors during initial setup.
//    uint status = 0;
//    uint2 tempForInit = uint2( status, status );
//    uint4x2 tempforInit4x2 = uint4x2( tempForInit.x, tempForInit.y, tempForInit.x, tempForInit.y, tempForInit.x, tempForInit.y, tempForInit.x, tempForInit.y );
         
//    if ( ThreadId.x == status && ThreadId.y == status )
//    {
//        // Set starting value for random id generator.
//        // Setting global merge flag.
//        PlayerPositionBuffer [ 1 ].UID_BASE_VALUE = 0x0B;
//        PlayerPositionBuffer [ 1 ].GLOBAL_MERGE_FLAG = 0x01;
//    }
    
//    // Sync all theres so they see the new value.
//    AllMemoryBarrierWithGroupSync();
    
//    // Initialize everything in buffers or group shared.
//    WriteGroupMatrix( LocalId.xy, PX_BACKGROUND );
//    if ( ThreadId.x < MAX_PLAYERS || ThreadId.y < THREAD_GROUP_SIZE )
//    {
//        if ( ThreadId.x < MAX_PLAYERS )
//        {
//            InterlockedExchange( fillModified, tempForInit.x, tempForInit.x ); //< This is just initializing the shared value to 0, this is for compiler warning.
//            InterlockedExchange( hairClusterCount, tempForInit.y, tempForInit.y ); //< This is just initializing the shared value to 0, this is for compiler warning.
//            tempForInit.x = status;  // Reset values to zero.
//            tempForInit.y = status;
//            hairClusters [ ThreadId.x ].averagePos = tempForInit;
//            hairClusters [ ThreadId.x ].clusterSize = tempForInit.x;
//            groupMax [ ThreadId.x ] = tempForInit;
//            tempForInit.x = WINDOW_SIZE_X;
//            tempForInit.y = WINDOW_SIZE_Y;
//            groupMin [ ThreadId.x ] = tempForInit;
//            tempForInit.x = status; // Reset values to zero.
//            tempForInit.y = status;
//        }
        
//        if ( ThreadId.y < THREAD_GROUP_SIZE )
//        {
//            hairClusters [ ThreadId.x ].positions [ ThreadId.y ] = tempForInit;
//        }
    
//        PlayerPositionBuffer [ status ].players [ ThreadId.x ] = PlayerPositionCTOR( tempForInit, tempForInit, tempforInit4x2 );
//        GroupDetailsBuffer [ ThreadId.x ].hairCentroids [ ThreadId.y ] = HairCentroidCTOR( tempForInit, tempForInit.x, tempForInit.y );
//        GroupDetailsBuffer [ ThreadId.x ].boundingBoxes [ ThreadId.y ] = BoundingBoxCTOR( tempForInit, tempForInit, tempForInit.x, tempForInit.y );
//    }
    
//    AllMemoryBarrierWithGroupSync();
  
//    //Swap colors.
//    float4 swapColor = float4( tempForInit.x, tempForInit.y, tempForInit.x, tempForInit.y );
    
//    //Load pixel.
//    const unorm float4 pixelColor = UavBuffer.Load( int3( ThreadId.xy, tempForInit.x ) );
//    // We know if the pixel coords are out of bounds it returns a zero'd vector.
//    // This is the easy way to check if thread.x or y is out of bounds; without a seperate check.
//    if ( !any( pixelColor ) )
//    {
//        status = 1;
//    }
         
//    //Sync all threads.
//    AllMemoryBarrierWithGroupSync();
    
//    // Check if the pixel is outline.
//    uint result = status == tempForInit.x ? CheckAndSetPixel( LocalId.xy, pixelColor, COLOR_NAME_OUTLNZ, swapColor ) : 0xFA1L;
    
//    // We need to sync the group threads any time the local matrix is changed.
//    GroupMemoryBarrierWithGroupSync();
    
//    // If result is px background we check to see if the pixel is of type hair.
//    // Background is a result of not finding any matching pixel color
//    if ( result == PX_BACKGROUND && status == 0 )
//    {
//        result = CheckAndSetPixel( LocalId.xy, pixelColor, COLOR_NAME_HAIR, swapColor );
//    }
    
//    GroupMemoryBarrierWithGroupSync();
    
//    switch ( result )
//    {
//            // Error validating pixel.
//        case NO_RANGE_ERROR:
//        case ALIGNMENT_ERROR:
//            status = 1;
//            break;
//        case PX_BACKGROUND:
//            // Update shared matrix if result is PX_BACKGROUND.
//            WriteGroupMatrix( LocalId.xy, PX_BACKGROUND );
//            break;
//        default:
//            break;
//    }
    
    
//    GroupMemoryBarrierWithGroupSync();
    
//    // Fill in any gaps in the character outline.
//    // We run this twice to make sure we fill in all the gaps.
//    FindOutlineConnection( LocalId.xy, PX_OUTLINE, status );
    
//    GroupMemoryBarrierWithGroupSync();
    
//    // Remove any noise from the hair.
//    RemoveNonHair( LocalId.xy, PX_HAIR, status );
    
//    GroupMemoryBarrierWithGroupSync();
    
//    // FloodFill the body.
//    FloodFillBody( LocalId.xy, PX_OUTLINE, status );
    
//    GroupMemoryBarrierWithGroupSync();
    
//    // If we have more than 2 hair clusters, attempt to merge them.
//    if ( hairClusterCount > 2 )
//    {
//        MergeHairClusters( LocalId.xy, status );
//    }
    
//    GroupMemoryBarrierWithGroupSync();
    
//    // Set bounding boxes
//    GetBoundingBoxPositions( LocalId.xy, status );
    
//    GroupMemoryBarrierWithGroupSync();
    
//    // Join any of the bounding boxes from each segment, if they are within the threshold.   
//    BoundingBoxMergeHelper( LocalId.xy, status );
    
//    GroupMemoryBarrierWithGroupSync();
    
//    // Set the group details buffer.
//    SetGroupDetails( LocalId.xy, GroupId.xy, ThreadId.xy, swapColor, status );
    
//    // Global sync.
//    AllMemoryBarrierWithGroupSync();

//    BoundingBox boundingBox = BoundingBoxCTOR( tempForInit, tempForInit, tempForInit.x, tempForInit.y );
//    HairCentroid hairCentroid = HairCentroidCTOR( tempForInit, tempForInit.x, tempForInit.y );
//    uint segmentPos = SegmentPosCalc( LocalId.xy );
    
//    // Blocking off extra threads.
//    if ( status == 0 && segmentPos < MAX_PLAYERS && GroupId.x < NUM_GROUPS )
//    {
//        GroupDetailsBuffer [ GroupId.x ].GetHairCentroid( segmentPos, hairCentroid );
//        GroupDetailsBuffer [ GroupId.x ].GetBoundingBox( segmentPos, boundingBox );
//    }
    
//    AllMemoryBarrierWithGroupSync();
    
//    // Filter out threads, and make sure we only use the details we know are players.
//    if ( status == 0 && segmentPos < MAX_PLAYERS && !hairCentroid.IsMerged() &&
//        hairCentroid.isLinked() && boundingBox.isLinked() && !boundingBox.IsMerged() )
//    {
//        // Get all our global details.
//        const uint2 globalHairCentroid = GroupPosToGlobal( hairCentroid.position, GroupId.xy );
//        uint4x2 globalBoundBox = tempforInit4x2;
//        GroupBBToGlobalBB( boundingBox.min, boundingBox.max, GroupId.xy, globalBoundBox );
//        const uint2 globalTorsoPos = GetTorsoPos( globalBoundBox );
        
//        // There will always be only 1 struct in the player position buffer.
//        InterlockedAdd( PlayerPositionBuffer [ 0 ].playerCount, 1 );
//        ATOMIC_EXCHANGE_UINT2( PlayerPositionBuffer [ 0 ].players [ segmentPos ].headPosition, globalHairCentroid, result );
//        ATOMIC_EXCHANGE_UINT2( PlayerPositionBuffer [ 0 ].players [ segmentPos ].bodyPosition, globalTorsoPos, result  );
//        ATOMIC_EXCHANGE_UINT4X2( PlayerPositionBuffer [ 0 ].players [ segmentPos ].boundingBox, globalBoundBox, result );
//    }
    
//    AllMemoryBarrierWithGroupSync();
    
//#ifdef DEBUG
//    if ( status == 0 )
//    {
//        DrawBoundingBox( ThreadId.xy );
//    }
//    AllMemoryBarrierWithGroupSync();
//#endif
}

 