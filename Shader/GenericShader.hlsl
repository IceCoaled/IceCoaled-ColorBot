#include "ShaderFunctions.fxh"

///----------Main-----------///


[numofthreads( X_THREADGROUP, 8, 1 )]
void main( int3 ThreadId : SV_DispatchThreadID,
          int3 LocalId : SV_GroupThreadID )
{
    // Make sure we are within the bounds of the image    
    [flatten]
    if ( ThreadId.x >= WINDOW_SIZE_X || ThreadId.y >= WINDOW_SIZE_Y )
    {
        return;
    }
    
    // Swap colors
    float4 swapColor = float4( 0, 0, 0, 0 );
    
    // Load the pixel color
    float4 pixelColor = UavBuffer.Load( int3( ( ( int2 ) ThreadId.xy ), ( ( int1 ) 0 ) ) ); // Load the pixel color, hlsl is very strict so we are casting to int2 and int1 to stop the compiler from complaining
     
    // Sync all threads
    AllMemoryBarrierWithGroupSync();
    
    // Check if the pixel is within the range of any of the color ranges
    int result = CheckAndSetPixel( int2( LocalId.xy ), pixelColor, COLOR_NAME_OUTLNZ, swapColor );
    result = result == SET_BACKGROUND ? CheckAndSetPixel( int2( LocalId.xy ), pixelColor, COLOR_NAME_HAIR, swapColor ) : result;
    //result = result == SET_BACKGROUND ? CheckAndSetPixel(int2(LocalId.xy), pixelColor, COLOR_NAME_SKIN, swapColor) : result; //< not using this for now
   

    // if the pixel wasnt set to a color, set it to the background color
    if ( result == SET_BACKGROUND )
    {
        localSharedMatrix [ LocalId.x ] [ LocalId.y ] = PX_BACKGROUND;
    }
    else if ( result == ALIGNMENT_ERROR || result == NO_RANGE_ERROR )
    {
        return;
    }
    
    // We need to sync the group threads any time the local matrix is changed
    GroupMemoryBarrierWithGroupSync();
            
    // Fill in any gaps in the character outline
    // We run this twice to make sure we fill in all the gaps
    FindOutlineConnection( int2( LocalId.xy ), PX_OUTLINE );
    
    GroupMemoryBarrierWithGroupSync();
    
    FindOutlineConnection( int2( LocalId.xy ), PX_OUTLINE );
    
    GroupMemoryBarrierWithGroupSync();
    
    // Remove any noise from the hair
    RemoveNonHair( int2( LocalId.xy ), PX_HAIR );
    
    GroupMemoryBarrierWithGroupSync();
    
    // FloodFill the body
    FloodFillBody( int2( LocalId.xy ), PX_FLOODFILL );
    
    GroupMemoryBarrierWithGroupSync();
    
    // If we have more than 2 hair clusters, attempt to merge them
    if ( hairClusterCount > 2 )
    {
        MergeHairClusters( hairClusterCount, int2( LocalId.xy ) );
    }
}

 