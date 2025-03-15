#include "ShaderFunctions.hlsli"


/// This shader is used to merge all the centroids
// In group centroid buffer
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
	
	AllMemoryBarrier();
	
	// Have thread 0,0 of each group check if the min/max is valid
	if ( overlapScanBox && IsTargetGroupThread( LocalId.xy, uint2( 0, 0 ) ) )
	{
		if ( CheckMinMax() != STATUS_OK )
		{
			SetGroupStatus( STATUS_NO_MIN_MAX );
		}
	}
	
	GroupMemoryBarrierWithGroupSync();
	
	// Check if anyone in the group had an error loading the scan box pixel data
	CheckGroupStatus( status );
	
	// Initialize group memory merge cluster
	if ( IsTargetGroup( GroupId.xy, uint2( 0, 0 ) ) && all( LocalId.xy <= uint2( ( MAX_PLAYERS - 1 ), 0 ) ) )
	{
		InitGMCentroids( LocalId.xy );
	}
	
	GroupMemoryBarrierWithGroupSync();
	
	// Merge hair centroids on global level 
	// Only thread group 0,0 will do this
	if ( IsTargetGroup( GroupId.xy, uint2( 0, 0 ) ) )
	{
		MergeGroupHairCentroids( LocalId.xy );
	}

	// Allow all the threads to regroup
	GroupMemoryBarrierWithGroupSync();
	
	// Allow thread 0,0 of thread group 0,0 to do 
	// A group memory merge
	// As well as add the final valid
	// Cluster to player position buffer												
	if ( ( IsTargetGroup( GroupId.xy, uint2( 0, 0 ) ) && IsTargetGroupThread( LocalId.xy, uint2( 0, 0 ) ) ) )
	{
		//CopyMergedClusterForDebug( true );
		FinalMerge();
		//CopyMergedClusterForDebug( false );	
	}
	
	// write status back to buffer, just in case it was changed
	if ( overlapScanBox && groupDataIndex >= MIN_UINT && groupDataIndex < ACTUAL_SCAN_GROUPS )
	{
		SetGroupStatus( status, LocalId.xy, groupDataIndex );
	}
	AllMemoryBarrier();
}
