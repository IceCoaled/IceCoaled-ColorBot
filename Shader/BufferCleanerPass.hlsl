#include "ShaderFunctions.hlsli"

[numthreads( X_THREADGROUP, Y_THREADGROUP, 1 )]
void main( uint3 ThreadId : SV_DispatchThreadID,
           uint3 LocalId : SV_GroupThreadID,
           uint3 GroupId : SV_GroupID )
{
    
        // Calculate group centroids index
	const int groupDataIndex = GetGroupDataIndex( GroupId.xy );
	
	const int scanBoxIndex = ( ThreadId.y * X_THREADGROUP ) + ThreadId.x;
	
	if ( groupDataIndex >= MIN_UINT && groupDataIndex < ACTUAL_SCAN_GROUPS && IsTargetGroupThread( LocalId.xy, uint2( 0, 0 ) ))
	{		
		InterlockedExchange( GroupDataBuffer [ groupDataIndex ].hairCentroid.outlinePos.x, 0, DUMMY_UINT );
		InterlockedExchange( GroupDataBuffer [ groupDataIndex ].hairCentroid.outlinePos.y, 0, DUMMY_UINT );
		InterlockedExchange( GroupDataBuffer [ groupDataIndex ].hairCentroid.outlinePos.z, 0, DUMMY_UINT );
		InterlockedExchange( GroupDataBuffer [ groupDataIndex ].hairCentroid.outlinePos.w, 0, DUMMY_UINT );
		InterlockedExchange( GroupDataBuffer [ groupDataIndex ].hairCentroid.clusterSize, 0, DUMMY_UINT );
		InterlockedExchange( GroupDataBuffer [ groupDataIndex ].hairCentroid.allowance, 0, DUMMY_UINT );
		InterlockedExchange( GroupDataBuffer [ groupDataIndex ].hasCluster, 0, DUMMY_UINT );	
	}	
	if ( groupDataIndex == ( MAX_SCAN_BOX_GROUPS - 1 ) && all( LocalId.xy <= uint2( ( MAX_PLAYERS - 1 ), 0 ) ) )
	{
		InterlockedExchange( PlayerPositionBuffer [ 0 ].players [ LocalId.x ].bodyPosition.x, 0, DUMMY_UINT );
		InterlockedExchange( PlayerPositionBuffer [ 0 ].players [ LocalId.x ].bodyPosition.y, 0, DUMMY_UINT );            
		InterlockedExchange( PlayerPositionBuffer [ 0 ].players [ LocalId.x ].headPosition.x, 0, DUMMY_UINT );
		InterlockedExchange( PlayerPositionBuffer [ 0 ].players [ LocalId.x ].headPosition.y, 0, DUMMY_UINT );                       
		InterlockedExchange( PlayerPositionBuffer [ 0 ].players [ LocalId.x ].boundingBox [ 0 ].x, 0, DUMMY_UINT );
		InterlockedExchange( PlayerPositionBuffer [ 0 ].players [ LocalId.x ].boundingBox [ 0 ].y, 0, DUMMY_UINT );
		InterlockedExchange( PlayerPositionBuffer [ 0 ].players [ LocalId.x ].boundingBox [ 1 ].x, 0, DUMMY_UINT );
		InterlockedExchange( PlayerPositionBuffer [ 0 ].players [ LocalId.x ].boundingBox [ 1 ].y, 0, DUMMY_UINT );
		InterlockedExchange( PlayerPositionBuffer [ 0 ].players [ LocalId.x ].boundingBox [ 2 ].x, 0, DUMMY_UINT );
		InterlockedExchange( PlayerPositionBuffer [ 0 ].players [ LocalId.x ].boundingBox [ 2 ].y, 0, DUMMY_UINT );
		InterlockedExchange( PlayerPositionBuffer [ 0 ].players [ LocalId.x ].boundingBox [ 3 ].x, 0, DUMMY_UINT );
		InterlockedExchange( PlayerPositionBuffer [ 0 ].players [ LocalId.x ].boundingBox [ 3 ].y, 0, DUMMY_UINT );
	}	
	if ( groupDataIndex == ( MAX_SCAN_BOX_GROUPS - 3 ) && IsTargetGroupThread( LocalId.xy, uint2( 0, 0 ) ) )
	{
		InterlockedExchange( PlayerPositionBuffer [ 0 ].globals.leftLowestPoint.x, 0, DUMMY_UINT );
		InterlockedExchange( PlayerPositionBuffer [ 0 ].globals.leftLowestPoint.y, 0, DUMMY_UINT );        
		InterlockedExchange( PlayerPositionBuffer [ 0 ].globals.rightLowestPoint.x, 0, DUMMY_UINT );
		InterlockedExchange( PlayerPositionBuffer [ 0 ].globals.rightLowestPoint.y, 0, DUMMY_UINT );        
		InterlockedExchange( PlayerPositionBuffer [ 0 ].scanBoxBB.bbMax.x, 0, DUMMY_UINT );
		InterlockedExchange( PlayerPositionBuffer [ 0 ].scanBoxBB.bbMax.y, 0, DUMMY_UINT );        
		InterlockedExchange( PlayerPositionBuffer [ 0 ].scanBoxBB.bbMin.x, 0, DUMMY_UINT );
		InterlockedExchange( PlayerPositionBuffer [ 0 ].scanBoxBB.bbMin.y, 0, DUMMY_UINT );        
		InterlockedExchange( PlayerPositionBuffer [ 0 ].globals.ySearchPlaneLane.x, 0, DUMMY_UINT );
		InterlockedExchange( PlayerPositionBuffer [ 0 ].globals.ySearchPlaneLane.y, 0, DUMMY_UINT );        
		InterlockedExchange( PlayerPositionBuffer [ 0 ].globals.DegHairToLeftLow, 0, DUMMY_UINT );
		InterlockedExchange( PlayerPositionBuffer [ 0 ].globals.DegHairToRightLow, 0, DUMMY_UINT );
		InterlockedExchange( PlayerPositionBuffer [ 0 ].globals.distance, 0, DUMMY_UINT );
		InterlockedExchange( PlayerPositionBuffer [ 0 ].globals.leftReductionDegree, 0, DUMMY_UINT );
		InterlockedExchange( PlayerPositionBuffer [ 0 ].playerCount, 0, DUMMY_UINT );
		InterlockedExchange( PlayerPositionBuffer [ 0 ].centroidMergeFlag, 0, DUMMY_UINT );
	}	
	if ( scanBoxIndex >= MIN_UINT && scanBoxIndex < MAX_SCAN_BOX_BUFFER_INDEX )
	{
		ClearScanBoxByte( scanBoxIndex );
	}
}
