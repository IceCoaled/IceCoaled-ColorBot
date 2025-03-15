#include "ShaderFunctions.hlsli"


/// This Shader is used to draw debug information to the screen
// I.E Scan box, player bounding boxes, and swap colors
// So you can see what the shader is doing
[numthreads( X_THREADGROUP, Y_THREADGROUP, 1 )]
void main( uint3 ThreadId : SV_DispatchThreadID,
           uint3 LocalId : SV_GroupThreadID,
           uint3 GroupId : SV_GroupID )
{
	// Get the group centroid index
	const int groupDataIndex = GetGroupDataIndex( GroupId.xy );
    
    // Check if current thread is in the scan box
	const bool overlapScanBox = IsGroupOverScanBox( GroupId.xy );
	
	// Get the status and pixel type	
	uint status = MAX_UINT;
	uint pixelType = MAX_UINT;
	if ( overlapScanBox && groupDataIndex >= MIN_UINT && groupDataIndex < ACTUAL_SCAN_GROUPS )
	{
		status = GetGroupStatus( LocalId.xy, groupDataIndex );
		pixelType = GetGroupPixelType( LocalId.xy, groupDataIndex );
	}
	
	unorm float4 swapColor = float4( 0.0, 0.0, 0.0, 0.0 );
	unorm float4 pixelColor = UavBuffer.Load( int3( ThreadId.xy, 0 ) );
	if ( IsZero( pixelColor ) )
	{
		status = STATUS_ERROR;
	}
	else
	{
		uint pxTypeTemp = CheckPixelColor( LocalId.xy, pixelColor, COLOR_NAME_OUTLNZ, swapColor );
		pxTypeTemp = ( pxTypeTemp == PX_BACKGROUND ) ? CheckPixelColor( LocalId.xy, pixelColor, COLOR_NAME_HAIR, swapColor ) : pxTypeTemp;
		// If pixelType is above px_max an error occurred
		if ( pxTypeTemp > PX_MAX )
		{
			status = STATUS_ERROR;
		}
	}

	
	
	
		// Draw scanbox
	if ( IsWithin3PixelBorder( ThreadId.xy, SCAN_BOX_MIN, SCAN_BOX_MAX ) )
	{
		// Write Camaro yellow to the UAV
		UavBuffer [ ThreadId.xy ] = SCAN_BOX_COLOR;
	}
    
	// Load detected player info
	const DetectedPlayers dtecPlayers = PlayerPositionBuffer.Load( 0 );
	
	// Sync for buffer load
	AllMemoryBarrier();
    
	// Check if we have any players
	if ( status == STATUS_OK && ( dtecPlayers.playerCount > 0 ) )
	{
		for ( uint i = 0; i < MAX_PLAYERS; ++i )
		{
			if ( i >= dtecPlayers.playerCount )
			{
				break;
			}
        
			uint2 minPos = dtecPlayers.players [ i ].boundingBox [ 0 ];
			uint2 maxPos = dtecPlayers.players [ i ].boundingBox [ 3 ];
        
			// Check if they are out of bounds
			if ( ( all( minPos < SCAN_BOX_MIN ) && all( maxPos > SCAN_BOX_MAX ) ) )
			{
				continue;
			}
			else if ( IsWithin3PixelBorder( ThreadId.xy, minPos, maxPos ) )
			{
				// Draw player bounding boxs
				UavBuffer [ ThreadId.xy ] = BOUNDING_BOX_COLOR;
			}
		}
	}
    
    
	// Change outline pixels and hair pixels to swap color so we can see
	if ( status == STATUS_OK && !IsZero( swapColor ) && ( pixelType == PX_HAIR || pixelType == PX_OUTLINE ) )
	{
		// Write swap color to the UAV
		UavBuffer [ ThreadId.xy ] = swapColor;
	}
}