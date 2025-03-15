#include "ShaderFunctions.hlsli"

 /// This is the final shader pass
// Its used to do final player detection
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
	
	// Same as checking for ThreadId.xy == 0,0 but i want to keep it all this way to avoid edge cases
	bool IsGZeroTZero = ( IsTargetGroup( GroupId.xy, uint2( 0, 0 ) ) && IsTargetGroupThread( LocalId.xy, uint2( 0, 0 ) ) );
	
	uint2 min = uint2( 0, 0 );
	uint2 max = uint2( 0, 0 );
	uint playerCount = 0;
	
	// Final player detection threads aqquire some details
	if ( ( pixelType == PX_OUTLINE && status == STATUS_OK ) || IsGZeroTZero )
	{
		// Get player count
		playerCount = PlayerPositionBuffer [ 0 ].playerCount;
		// Intermediates for bounding box
		min = PlayerPositionBuffer [ 0 ].scanBoxBB.bbMin;
		max = PlayerPositionBuffer [ 0 ].scanBoxBB.bbMax;
	}
	
	// Sync for buffer reads
	AllMemoryBarrier();
	
	// If no players are detected
	if ( playerCount == 0 && IsGZeroTZero )
	{
		NoPlayersDetected( min, max );
	}
	// If theres only 1 player detected
	else if ( playerCount == 1 && IsGZeroTZero )
	{
		SinglePlayerDetected( min, max );
	}	
	// If multiple players are detected
	else if ( ( pixelType == PX_OUTLINE && status == STATUS_OK ) )
	{
		GetSetPlayerDetails( ThreadId.xy, pixelType, min, max, playerCount );
	}
	
	AllMemoryBarrier();
}