#include "ShaderFunctions.hlsli"


/// This shader is used for hair detection
/// Will add hair clusters to the group centroid buffer
// And bounding box creation
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
	
	
    // Initialize the bounding box min/max
	if ( IsTargetThread( ThreadId.xy, uint2( 0, 0 ) ) )
	{	
		InitializeDPMaxMin();
	}
	
	AllMemoryBarrier();
	
	// Initialize group memory hair cluster		
	if ( IsTargetGroupThread( LocalId.xy, uint2( 0, 0 ) ) && overlapScanBox )
	{		
		InitGmHairCluster();
	}
	
	GroupMemoryBarrierWithGroupSync();
	
	// Search for outline pixel around hair cluster
	if ( status == STATUS_OK && pixelType == PX_HAIR )
	{
		OutlineSearch( ThreadId.xy, LocalId.xy );
	}
		
	// Allow all the threads to regroup
	GroupMemoryBarrierWithGroupSync();
    
    // Add group Clusters to centroid buffer
	if ( overlapScanBox && IsTargetGroupThread( LocalId.xy, uint2( 0, 0 ) ) && gmHairCluster.VerifyCluster() && ( groupDataIndex >= MIN_UINT && groupDataIndex < ACTUAL_SCAN_GROUPS ) )
	{
		SetCluster( groupDataIndex );
	}
    
    // Creates bounding box for scan box
	if ( status == STATUS_OK )
	{
		CreateBoundingBox( ThreadId.xy, LocalId.xy );
	}
	
	// Sync for buffer writes
	AllMemoryBarrier();
}