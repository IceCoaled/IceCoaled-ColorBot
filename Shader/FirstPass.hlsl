#include "ShaderFunctions.hlsli"


//----------Main-----------///

/// This shader is used to get all initial data from texture
// It sets up the scan box labels, and pixel classifications
// It also sets up the scan box data buffer
// Each thread will get a status, used throughout the shader
// To either let it run or block it from certain functions
// This initalizes all thread data each thread thats being used
// Will need to run all passes
[numthreads( X_THREADGROUP, Y_THREADGROUP, 1 )]
void main( uint3 ThreadId : SV_DispatchThreadID,
           uint3 LocalId : SV_GroupThreadID,
           uint3 GroupId : SV_GroupID )
{
	// thread status used to block threads
    // From certain functions, but still allows
    // It to run so we can still group and all sync
	uint status = STATUS_OK;
	
	// Scan box label variables 
	int sbPixelIndex = 0;
	int sbBitPos = 0;
	int sbByteIndex = 0;
        
    // Group pixel classification
	uint pixelType = 0;
	
	// Get the group centroid index
	const int groupDataIndex = GetGroupDataIndex( GroupId.xy );
    
    // Check if current thread is in the scan box
	const bool overlapScanBox = IsGroupOverScanBox( GroupId.xy );

    // Check if current thread is out of bounds
	if ( IsOutsideTexture( ThreadId.xy ) || IsInHudBlock( ThreadId.y ) || !IsWithinScanBox( ThreadId.xy ) )
	{
		status = STATUS_ERROR;
	}


    //Swap color
    unorm float4 swapColor = float4( 0.0, 0.0, 0.0, 0.0 );
    
    // Get pixel color
    const unorm float4 pixelColor = UavBuffer.Load( int3( ThreadId.xy, 0 ) );
	
	GroupMemoryBarrierWithGroupSync();
    
    // Check if the pixel was out of bounds or failed to read
	if ( IsZero( pixelColor ) )
	{
		status = STATUS_ERROR;
	}
   
    // Check if we are a hair or outline pixel
	if ( status == STATUS_OK )
	{
		pixelType = CheckPixelColor( LocalId.xy, pixelColor, COLOR_NAME_OUTLNZ, swapColor );
		pixelType = ( pixelType == PX_BACKGROUND ) ? CheckPixelColor( LocalId.xy, pixelColor, COLOR_NAME_HAIR, swapColor ) : pixelType;
        
        // If pixelType is above px_max an error occurred
		if ( pixelType > PX_MAX )
		{
			status = STATUS_ERROR;
		}
	}

    // Write out pixel classification to scan box labels
	if ( status == STATUS_OK )
    {
		GetScanBoxIndexing( ThreadId.xy, sbPixelIndex, sbBitPos, sbByteIndex );
		WriteScanBoxClass( pixelType, sbBitPos, sbByteIndex );
    }
	
	// Set group pixel classification and status
	if ( overlapScanBox )
	{
		SetGroupPixelType( pixelType, LocalId.xy, groupDataIndex );
		SetGroupStatus( status, LocalId.xy, groupDataIndex );
	}
	
	AllMemoryBarrier();
}

 