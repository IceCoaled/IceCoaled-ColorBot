///----------User-Defines-----------///

// AMD uses 32/64, NVIDIA uses 16/32 wavefront/warp size.
#define X_THREADGROUP int( 32 )
#define Y_THREADGROUP int( 8 )

// Monitor resolution
#define WINDOW_SIZE_X int( 2560 )
#define WINDOW_SIZE_Y int( 1440 )

// pixel format
// We will always use UNORM for the bit representation.
// All we are worried about here is the order of the channels, as we are using a bitmap.
#define BRGA

// Define for better precision
#define precise

// Debug flag
#define DEBUG

// Number of players we are detecting
#define MAX_PLAYERS int( 6 )//< Keep in decmial form for editing form

// Thread group size
#define THREAD_GROUP_SIZE int( X_THREADGROUP * Y_THREADGROUP ) 

// Segment size
#define SEGMENT_SIZE int( THREAD_GROUP_SIZE / MAX_PLAYERS )

// Number of segments
#define NUM_SEGMENTS int( THREAD_GROUP_SIZE / SEGMENT_SIZE )

// Number of groups
#define NUM_GROUPS int( ( ( WINDOW_SIZE_X / X_THREADGROUP ) * ( WINDOW_SIZE_Y / Y_THREADGROUP ) ) ) 

// Number of color ranges in the constant buffer
#define NUM_COLOR_RANGES int( 20 )//< Keep in decmial form for editing code.

// Debug pixel replacement color
#define BACKGROUND_PIXEL_COLOR float4( 0.164, 0.415, 0.545, 0.54 )  // Dark Khaki( 42, 106, 138, 0.54 )

// Object Fill Color'
#define OBJECT_FILL_COLOR float4( 0.294, 0.317, 0.0, 0.51 )  // Deep Teal( 75, 81, 0, 0.51 )

// Bounding Box Color
#define BOUNDING_BOX_COLOR float4( 0.0, 0.0, 1.0, 1.0 )  // Red ( 0, 0, 255, 1.0 )

// Max uint value
#define MAX_INT int( 0x7FFFFFFF )
#define MIN_INT int( 0x80000000 )

// Merged flag
#define MERGED_FLAG ( ( int ) -0xDE1E7E )

///----------Return-Codes-----------///
#define NO_RANGE_ERROR int( -0xB00B5 )
#define ALIGNMENT_ERROR int( -0x6969 )
#define SET_BACKGROUND int( 0x0A55 )
#define SET_OUTLINE int( 0x0 )
#define SET_HAIR int( 0x1 )
#define SET_SKIN int( 0x2 )

///----------Color-Names-----------///
// Edit these while we edit the other constants.
#define COLOR_NAME_OUTLNZ half3( 0, 0, 0 )
#define COLOR_NAME_HAIR half3( 0, 0, 0 )
#define COLOR_NAME_SKIN half3( 0, 0, 0 )
// Add more names here.

///----------Structs-----------///

// Hair cluster struct.
// Hair cluster threshold.
// if a new hair pixel is outside this threshold, it will be considered a new cluster.
#define HAIR_CLUSTER_THRESHOLD int( 0xD )
#define HAIR_MERGE_THRESHOLD  HAIR_CLUSTER_THRESHOLD
struct HairCluster
{
    int2 positions [ THREAD_GROUP_SIZE ]; //< Way overkill, but so be it.
    int2 averagePos;
    int clusterSize;
    
    inline bool CheckForGrouping( int2 pos )
    {
        return distance( asfloat( pos ), asfloat( averagePos ) ) <= HAIR_CLUSTER_THRESHOLD ? true : false;
    }
    
    inline void AddAverage( int2 pos, int currentSize )
    {
        int2 currentAvg = averagePos;
        int2 newAvg = ( ( currentAvg * currentSize ) + pos ) / currentSize;
        InterlockedExchange( averagePos.x, newAvg.x, 0 );
        InterlockedExchange( averagePos.y, newAvg.y, 0 );
    }
    
    inline void AddPosition( int2 pos )
    {
        int currentSize = 0;
        
        InterlockedAdd( clusterSize, 1, currentSize + 1 );
        InterlockedCompareExchange( positions [ currentSize ].x, 0, pos.x, 0 );
        InterlockedCompareExchange( positions [ currentSize ].y, 0, pos.y, 0 );

        AddAverage( pos, currentSize );
    }
    
    inline int2 GetPosition( int index )
    {
        return positions [ index ];
    }    
    
    inline int2 GetAveragePos()
    {
        return averagePos;
    }
    
    inline void SetMergedFlag( HairCluster cluster )
    {
        InterlockedExchange( cluster.averagePos.x, MERGED_FLAG, 0 );
        InterlockedExchange( cluster.averagePos.y, MERGED_FLAG, 0 );
    }
    
    inline void MergeCluster( HairCluster cluster )
    {
        int ourCurrentSize = 0;
        int otherClusterSize = cluster.clusterSize;
        
        for ( int i = 0; i < clusterSize; i++ )
        {
            InterlockedAdd( ourCurrentSize, 1 );
            
            // Add new position.
            InterlockedCompareExchange( positions [ ourCurrentSize ].x, 0, cluster.GetPosition( i ).x, 0 );
            InterlockedCompareExchange( positions [ ourCurrentSize ].y, 0, cluster.GetPosition( i ).y, 0 );
            
            // Update average.
            AddAverage( cluster.GetPosition( i ), clusterSize );

            // Update size.
            InterlockedAdd( clusterSize, 1 );
        }
        
        SetMergedFlag( cluster );
    }   
    
    inline bool IsMerged()
    {
        return averagePos.x == MERGED_FLAG & averagePos.y == MERGED_FLAG ? true : false;
    }
};

// Define the Range structure for color limits.
struct Range
{
    float minimum;
    float maximum;
};

// ColorRange to hold RGB ranges.
struct ColorRange
{
    Range redRange;
    Range greenRange;
    Range blueRange;
};

// ColorRanges to hold multiple ranges and the swap color.
struct ColorRanges
{
    ColorRange ranges [ 0xC ]; // Define a fixed maximum size.
    uint numOfRanges;
    float4 swapColor;
    
    int safetyCheck; // Debugging purposes, we can use this here as well.
    half3 name; // Debugging purposes, we can use this here as well.
    
    // Check if the struct has ranges
    inline bool HasRanges()
    {
        return numOfRanges > 0 ? true : false;
    }
    
    // Check if the name is the same as the compare name.
    inline bool CheckName( half3 compare )
    {
        // The ternary result is flipped because All returns true if all elements are true, and we want to return true if all elements are false.
        return all( name - compare ) ? false : true;
    }
    
    // Because we are using a bitmap, it will be BGRA, z is red, x is blue, y is green.
    inline bool IsInRange( float4 pixel, int rangeIndex )
    {
#ifdef BRGA
        return ( pixel.z >= ranges [ rangeIndex ].redRange.minimum && pixel.x <= ranges [ rangeIndex ].redRange.maximum &&
                pixel.y >= ranges [ rangeIndex ].greenRange.minimum && pixel.y <= ranges [ rangeIndex ].greenRange.maximum &&
                pixel.x >= ranges [ rangeIndex ].blueRange.minimum && pixel.z <= ranges [ rangeIndex ].blueRange.maximum ) ? true : false;
#else
        return ( pixel.x >= ranges[rangeIndex].redRange.minimum && pixel.x <= ranges[rangeIndex].redRange.maximum &&
                pixel.y >= ranges[rangeIndex].greenRange.minimum && pixel.y <= ranges[rangeIndex].greenRange.maximum &&
                pixel.z >= ranges[rangeIndex].blueRange.minimum && pixel.z <= ranges[rangeIndex].blueRange.maximum ) ? true : false;
#endif
    }
    
    // Checks of the safety check value is the max int value.
    // This is the easiest way to check if the struct is aligned properly.
    inline bool CheckAlignment()
    {
        return safetyCheck == MAX_INT;
    }
};


// Player position struct
struct PlayerPosition
{
    int2 headPosition;
    int2 bodyPosition;
    int4x2 boundingBox;
};

// PlayerPositions struct holds a max of 6 players.
// Change according to the maximum number of players you want to detect.
struct DetectedPlayers
{
    PlayerPosition players [ MAX_PLAYERS ];
    int playerCount;
    int safetyCheck; // Debugging purposes, we can use this here as well.
    
    inline void AddPlayerPos( int2 headPos, int2 bodyPos, int4x2 boundingBox )
    {
        int currentCount = 0;
        InterlockedAdd( playerCount, 1, currentCount + 1 );
        InterlockedExchange( players [ currentCount ].headPosition.x, headPos.x, 0 );
        InterlockedExchange( players [ currentCount ].headPosition.y, headPos.y, 0 );
        InterlockedExchange( players [ currentCount ].bodyPosition.x, bodyPos.x, 0 );
        InterlockedExchange( players [ currentCount ].bodyPosition.y, bodyPos.y, 0 );
        InterlockedExchange( players [ currentCount ].boundingBox [ 0 ].x, boundingBox [ 0 ].x, 0 );
        InterlockedExchange( players [ currentCount ].boundingBox [ 0 ].y, boundingBox [ 0 ].y, 0 );
        InterlockedExchange( players [ currentCount ].boundingBox [ 1 ].x, boundingBox [ 1 ].x, 0 );
        InterlockedExchange( players [ currentCount ].boundingBox [ 1 ].y, boundingBox [ 1 ].y, 0 );
        InterlockedExchange( players [ currentCount ].boundingBox [ 2 ].x, boundingBox [ 2 ].x, 0 );
        InterlockedExchange( players [ currentCount ].boundingBox [ 2 ].y, boundingBox [ 2 ].y, 0 );
        InterlockedExchange( players [ currentCount ].boundingBox [ 3 ].x, boundingBox [ 3 ].x, 0 );
        InterlockedExchange( players [ currentCount ].boundingBox [ 3 ].y, boundingBox [ 3 ].y, 0 );
    }
    
    inline bool CheckAlignment()
    {
        return safetyCheck == MAX_INT;
    }
};

///<<< create bounding box struct with min/max and unqiue id, it will just be the closest hair centroid within a threshold, otherwise it could be a new player, or junk data.
///<<< merge hair centroids conversly, merge any bounding boxes that were attached to the 2 different hair centroids.
///<<< If 2 hair centroids are close but not enough to merge, scan for the character outline that connects them, and use it to calculate the bounding box for both players.
///<<< That is possible as 1 player will be fully visible. So we can use their size to estimate the other players size.

///>>> Still need to come up with something for bounding boxes that dont have a hair centroid, we can assume the centroid is in the thread group above. But how do we label the bounding boxes?

// Bounding box struct, and hair centroid struct.
// position for the hair centroid.
// min/max values for the bounding box.
// unique id for merging.
struct BoundingBox
{
    int2 min;
    int2 max;
    int uniqueId;
    bool linked;
    
    inline bool IsMerged()
    {
        return uniqueId == MERGED_FLAG;
    }
    
    inline bool isLinked()
    {
        return linked;
    }
};

struct HairCentroid
{
    int2 position;
    int uniqueId;
    bool linked;
    
    inline bool IsMerged()
    {
        return uniqueId == MERGED_FLAG;
    }
    
    inline bool isLinked()
    {
        return linked;
    }
};


// GroupBoundingBoxDetails struct.
// This is for when we merge all the details from the group shared memory, to the full texture.
struct GroupDetails
{
    BoundingBox boundingBoxes [ MAX_PLAYERS ];
    HairCentroid hairCentroids [ MAX_PLAYERS ];
    int safetyCheck; // Debugging purposes, we can use this here as well.
    
    
    inline void AddBoundingBox( int2 min, int2 max, int boxId, int segmentPos, bool linked = false )
    {
        InterlockedExchange( boundingBoxes [ segmentPos ].min.x, min.x, 0 );
        InterlockedExchange( boundingBoxes [ segmentPos ].min.y, min.y, 0 );
        InterlockedExchange( boundingBoxes [ segmentPos ].max.x, max.x, 0 );
        InterlockedExchange( boundingBoxes [ segmentPos ].max.y, max.y, 0 );
        InterlockedExchange( boundingBoxes [ segmentPos ].uniqueId, boxId, 0 );        
        InterlockedExchange( boundingBoxes [ segmentPos ].linked, linked, 0 );

    }
    
    inline void AddHairCentroid( int2 hairPos, int hairId, int segmentPos, bool linked = false )
    {
        InterlockedExchange( hairCentroids [ segmentPos ].position.x, hairPos.x, 0 );
        InterlockedExchange( hairCentroids [ segmentPos ].position.y, hairPos.y, 0 );
        InterlockedExchange( hairCentroids [ segmentPos ].uniqueId, hairId, 0 );
        InterlockedExchange( hairCentroids [ segmentPos ].linked, linked, 0 );
    }
    
    inline void AddGroupDetail( int2 min, int2 max, int2 hairPos, int id, int segmentPos )
    {
        AddBoundingBox( min, max, id, segmentPos, true );
        AddHairCentroid( hairPos, id, segmentPos, true );
    }
    
    inline void GetHairCentroid( int segmentPos, out HairCentroid hairCentroid )
    {
        hairCentroid.position = hairCentroids [ segmentPos ].position;
        hairCentroid.uniqueId = hairCentroids [ segmentPos ].uniqueId;
    }
    
    inline void GetBoundingBox( int segmentPos, out BoundingBox boundingBox )
    {
        boundingBox.min = boundingBoxes [ segmentPos ].min;
        boundingBox.max = boundingBoxes [ segmentPos ].max;
        boundingBox.uniqueId = boundingBoxes [ segmentPos ].uniqueId;
    }
    
    inline void GetAllHairCentroids( out HairCentroid groupHairCentroids [ MAX_PLAYERS ] )
    {
        groupHairCentroids = hairCentroids;
    }
    
    inline void GetAllBoundingBoxs( out BoundingBox groupBoundingBoxes [ MAX_PLAYERS ] )
    {
        groupBoundingBoxes = boundingBoxes;
    }
    
    inline void GetAllGroupDetails( out BoundingBox groupBoundingBoxes [ MAX_PLAYERS ], out HairCentroid groupHairCentroids [ MAX_PLAYERS ] )
    {
        groupBoundingBoxes = boundingBoxes;
        groupHairCentroids = hairCentroids;
    }
    
    inline void SetMergedFlag( int segmentPos, bool hairMerged = false, bool bbMerged = false )
    {
        InterlockedExchange( hairCentroids [ segmentPos ].uniqueId, hairMerged ? MERGED_FLAG : hairCentroids [ segmentPos ].uniqueId, 0 );
        InterlockedExchange( boundingBoxes [ segmentPos ].uniqueId, bbMerged ? MERGED_FLAG : boundingBoxes [ segmentPos ].uniqueId, 0 );
        
        // Unlink as well just for safety mesaures for later.
        InterlockedExchange( hairCentroids [ segmentPos ].linked, hairCentroids [ segmentPos ].isLinked() ? !hairCentroids [ segmentPos ].linked : hairCentroids [ segmentPos ].linked, 0 );
        InterlockedExchange( boundingBoxes [ segmentPos ].linked, boundingBoxes [ segmentPos ].isLinked() ? !boundingBoxes [ segmentPos ].linked : boundingBoxes [ segmentPos ].linked, 0 );
    }
           
    inline bool CheckAlignment()
    {
        return safetyCheck == MAX_INT;
    }
};

///----------Buffers-----------///

// UAV buffer for the image we can R/W on all threads simultaneously with a uav buffer.
// We are using a typed load as the texture is DXGI_FORMAT_B8G8R8A8_UNORM, it could be DXGI_FORMAT_R8G8B8X8_UNORM as well, we are using a float4 regardless.
// This is also why we checked for typed load support in the main program, and we arent using a simple float4 pixel = UavBuffer[ThreadId.xy] here.
RWTexture2D<unorm float4> UavBuffer : register( u0 );

// Buffer for precomputed color ranges.
RWStructuredBuffer<ColorRanges> ColorRangeBuffer : register( u1 );

// Buffer for the group bounding box details.
RWStructuredBuffer<GroupDetails> GroupDetailsBuffer : register( u2 );

// Our resulting buffer for player positions.
RWStructuredBuffer<DetectedPlayers> PlayerPositionBuffer : register( u3 );

///----------GroupSharedData-----------///

// Thread group shared matrix.
// Reduces the amount of memory accesses.
groupshared int localSharedMatrix [ X_THREADGROUP ] [ Y_THREADGROUP ];

// Fill modified flag, for outline connection, and flood fill.
groupshared bool fillModified = true; //< Set to true to start the flood fill.

// Group shared min and max values for bounding box calculation.
// Set to six as we expect there could only be 6 targets located in a thread group.
// That is still overkill, but it is better to have more than less.
groupshared int2 groupMin [ MAX_PLAYERS ];
groupshared int2 groupMax [ MAX_PLAYERS ];

// Group shared bounding box merge threshold.
#define BB_MERGE_THRESHOLD int( 0x05 )

// Hair position data.
groupshared HairCluster hairClusters [ MAX_PLAYERS ];
groupshared int hairClusterCount = 0;

// Calculate the current segment.
// This goes down to the nearest segment size.
// Then sets the current segment to a segment index with 0 based index.
#define SEGMENT_CALC( localPos ) ( ( int ) ( ( ( ( int ) localPos.x ) + SEGMENT_SIZE ) & ~( SEGMENT_SIZE - 1 ) ) / ( SEGMENT_SIZE - 1 ) )
#define SEGMENT_POS_CALC( localPos ) ( ( int ) ( ( int ) localPos.y ) % SEGMENT_SIZE )

///------------Neighborhood-Scans------------///
// We are using a 3x3 neighborhood scan
#define SCAN_TOP_LEFT int2( -1, -1 )
#define SCAN_TOP int2( 0, -1 )
#define SCAN_TOP_RIGHT int2( 1, -1 )
#define SCAN_LEFT int2( -1, 0 )
#define SCAN_RIGHT int2( 1, 0 )
#define SCAN_BOTTOM_LEFT int2( -1, 1 )
#define SCAN_BOTTOM int2( 0, 1 )
#define SCAN_BOTTOM_RIGHT int2( 1, 1 )

///------------Pixel-Types------------///
// We use these for the local matrix instead of actual pixel colors.
// This is to reduce the computation time.
#define PX_OUTLINE int( 1 << 1 ) 
#define PX_HAIR int( 1 << 2 ) 
#define PX_SKIN int( 1 << 3 ) 
#define PX_FLOODFILL int( 1 << 4 )
#define PX_BACKGROUND int( 1 << 5 )

///----------Unqiue-Id---------------///
static uint UNIQUE_ID_BASE = 0x0B007EE;

// Converts group id to single uint for bounding box id.
#define GLOBAL_MIN_TO_UID( globalMin ) uint( ( globalMin.x & 0x7FFFFFFF ) |= ( globalMin.y & 0x7FFFFFFF ) )

// Converts id back to group id.
#define UUID_TO_GLOBAL_MIN( uid ) int2( ( uid & 0x0000FFFF ), ( ( uid & 0xFFFF0000 ) >> 16 ) )

// Roll right macro.
#define ROR( value, shift ) ( ( ( value >> shift ) | (value << ( 32 - shift ) ) ) ) 

///---------Global-Loop-Flag---------///

// Just like the fill modified flag just global rather than local to thread group.
static bool loopFlag = true;