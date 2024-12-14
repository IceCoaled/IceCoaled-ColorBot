#include "ShaderFunctions.fxh"

///----------Main-----------///


[numofthreads( X_THREADGROUP, 8, 1 )]
void main( int3 ThreadId : SV_DispatchThreadID,
           int3 LocalId : SV_GroupThreadID,
           int3 GroupId : SV_GroupID)
{
    // Make sure we are within the bounds of the image.    
    [flatten]
    if ( ThreadId.x >= WINDOW_SIZE_X || ThreadId.y >= WINDOW_SIZE_Y )
    {
        return;
    }
    
    // Swap colors.
    float4 swapColor = float4( 0, 0, 0, 0 );
    
    // Load the pixel color.
    float4 pixelColor = UavBuffer.Load( int3( ( ( int2 ) ThreadId.xy ), ( ( int1 ) 0 ) ) ); // Load the pixel color, hlsl is very strict so we are casting to int2 and int1 to stop the compiler from complaining.
     
    // Sync all threads.
    AllMemoryBarrierWithGroupSync();
    
    // Check if the pixel is within the range of any of the color ranges.
    int result = CheckAndSetPixel( int2( LocalId.xy ), pixelColor, COLOR_NAME_OUTLNZ, swapColor );
    result = result == SET_BACKGROUND ? CheckAndSetPixel( int2( LocalId.xy ), pixelColor, COLOR_NAME_HAIR, swapColor ) : result;
    //result = result == SET_BACKGROUND ? CheckAndSetPixel(int2(LocalId.xy), pixelColor, COLOR_NAME_SKIN, swapColor) : result; //< not using this for now.
   

    // if the pixel wasnt set to a color, set it to the background color.
    if ( result == SET_BACKGROUND )
    {
        localSharedMatrix [ LocalId.x ] [ LocalId.y ] = PX_BACKGROUND;
    }
    else if ( result == ALIGNMENT_ERROR || result == NO_RANGE_ERROR )
    {
        return;
    }
    
    // We need to sync the group threads any time the local matrix is changed.
    GroupMemoryBarrierWithGroupSync();
    
    // Fill in any gaps in the character outline.
    // We run this twice to make sure we fill in all the gaps.
    FindOutlineConnection( int2( LocalId.xy ), PX_OUTLINE );
    
    GroupMemoryBarrierWithGroupSync();
    
    // Remove any noise from the hair.
    RemoveNonHair( int2( LocalId.xy ), PX_HAIR );
    
    GroupMemoryBarrierWithGroupSync();
    
    // FloodFill the body.
    FloodFillBody( int2( LocalId.xy ), PX_OUTLINE );
    
    GroupMemoryBarrierWithGroupSync(); 
    
    // If we have more than 2 hair clusters, attempt to merge them.
    [flatten]
    if ( hairClusterCount > 2 )
    {
        MergeHairClusters();
    }
    GroupMemoryBarrierWithGroupSync();
    
    // Set bounding boxes
    GetBoundingBoxPositions( int2( LocalId.xy ) );
    
    // Join any of the bounding boxes from each segment, if they are within the threshold.   
    BoundingBoxMergeHelper();
    
    GroupMemoryBarrierWithGroupSync();
    
    // Set the group details buffer.
    SetGroupDetails( int2( LocalId.xy ), int2( GroupId.xy ), int2( ThreadId.xy ), swapColor );

    BoundingBox boundingBoxes [ NUM_GROUPS ] [ MAX_PLAYERS ];
    HairCentroid hairCentroids [ NUM_GROUPS ] [ MAX_PLAYERS ];
    int segmentPos = SEGMENT_POS_CALC( LocalId );
    
    // Blocking off extra threads.
    if ( segmentPos < NUM_GROUPS && GroupId.x < NUM_GROUPS )
    {
        GroupDetailsBuffer [ GroupId.x ].GetAllGroupDetails( boundingBoxes [ GroupId.x ], hairCentroids [ GroupId.x ] );
    }
    
    // Global sync.
    DeviceMemoryBarrier();
    
    // Filter out threads, and make sure we only use the details we know are players.
    if ( segmentPos < MAX_PLAYERS && !hairCentroids [ GroupId.x ] [ segmentPos ].IsMerged() &&
    hairCentroids [ GroupId.x ] [ segmentPos ].isLinked() && boundingBoxes [ GroupId.x ] [ segmentPos ].isLinked() &&
    !boundingBoxes [ GroupId.x ] [ segmentPos ].IsMerged() )
    {
        // Get all our global details.
        int2 globalHairCentroid = GroupPosToGlobal( hairCentroids [ GroupId.x ] [ segmentPos ].position, int2( GroupId.xy ) );
        int4x2 globalBoundBox;
        GroupBBToGlobalBB( boundingBoxes [ GroupId.x ] [ segmentPos ].min, boundingBoxes [ GroupId.x ] [ segmentPos ].max, int2( GroupId.xy ), globalBoundBox );
        int2 globalTorsoPos = GetTorsoPos( globalBoundBox );
        
        // There will always be only 1 struct in the player position buffer.
        PlayerPositionBuffer [ 0 ].AddPlayerPos( globalHairCentroid, globalTorsoPos, globalBoundBox );        
    }
    
    DeviceMemoryBarrier();   
    
#ifdef DEBUG
    DrawBoundingBox( int2( ThreadId.xy ) );
    
    DeviceMemoryBarrier();
#endif
}

 