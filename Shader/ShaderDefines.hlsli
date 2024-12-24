///----------User-Defines-----------///

// AMD uses 64, NVIDIA uses 32 wavefront/warp size.
#define X_THREADGROUP uint( 16 )
#define Y_THREADGROUP uint( 4 ) //< This never changes.

// Monitor resolution
#define WINDOW_SIZE_X uint( 2560 )
#define WINDOW_SIZE_Y uint( 1440 )

// pixel format
// We will always use UNORM for the bit representation.
// All we are worried about here is the order of the channels, as we are using a bitmap.
#define BRGA

// Debug flag
#define DEBUG //< If we set debug flag it will define debug automatically

// Max loop cap.
// We need this as a safety backup so no runtime calculated loop can get stuck without a breakout.
#define LOOP_SAFETY_BREAKOUT  0x3E8

// Number of players we are detecting
#define MAX_PLAYERS uint( 6 )//< Keep in decmial form for editing form

// Thread group size
#define THREAD_GROUP_SIZE uint( X_THREADGROUP * Y_THREADGROUP ) 

// Segment size
#define SEGMENT_SIZE uint( THREAD_GROUP_SIZE / MAX_PLAYERS )

// Number of segments
#define NUM_SEGMENTS uint( THREAD_GROUP_SIZE / SEGMENT_SIZE )

// Number of groups
#define NUM_GROUPS uint( ( ( WINDOW_SIZE_X / X_THREADGROUP ) * ( WINDOW_SIZE_Y / Y_THREADGROUP ) ) ) 

// Number of color ranges in the constant buffer
#define NUM_COLOR_RANGES uint( 20 )//< Keep in decmial form for editing code.

// Debug pixel replacement color
#define BACKGROUND_PIXEL_COLOR float4( 0.164, 0.415, 0.545, 0.54 )  // Dark Khaki( 42, 106, 138, 0.54 )

// Object Fill Color'
#define OBJECT_FILL_COLOR float4( 0.294, 0.317, 0.0, 0.51 )  // Deep Teal( 75, 81, 0, 0.51 )

// Bounding Box Color
#define BOUNDING_BOX_COLOR float4( 0.0, 0.0, 1.0, 1.0 )  // Red ( 0, 0, 255, 1.0 )

// Max uint value
#define MAX_UINT uint( 0xFFFFFFFF )
#define MIN_UINT uint( 0x0 )

// Merged flag
#define MERGED_FLAG uint( 0xDE1E7E )

///----------Return-Codes-----------///
#define NO_RANGE_ERROR uint( 0xB00B5 )
#define ALIGNMENT_ERROR uint( 0x6969 )
#define NO_MATCHING_NAME uint( 0xBADA55 )

///----------Color-Names-----------///
// Edit these while we edit the other constants.
min16uint3 COLOR_NAME_OUTLNZ [ 2 ] = { min16uint3( 0, 0, 0 ), min16uint3( 0, 0, 0 ) };
min16uint3 COLOR_NAME_HAIR [ 2 ] = { min16uint3( 0, 0, 0 ), min16uint3( 0, 0, 0 ) };
min16uint3 COLOR_NAME_SKIN [ 2 ] = { min16uint3( 0, 0, 0 ), min16uint3( 0, 0, 0 ) };
// Add more names here.

///----------Structs-----------///

// Hair cluster struct.
// Hair cluster threshold.
// if a new hair pixel is outside this threshold, it will be considered a new cluster.
#define HAIR_CLUSTER_THRESHOLD uint( 0x0D )
#define HAIR_MERGE_THRESHOLD  HAIR_CLUSTER_THRESHOLD
struct HairCluster
{
    uint2 positions [ THREAD_GROUP_SIZE ]; //< Way overkill, but so be it.
    uint2 averagePos;
    uint clusterSize;
    
    inline uint2 GetPosition( int index )
    {
        return positions [ index ];
    }
    
    inline uint2 GetAveragePos()
    {
        return averagePos;
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
    ColorRange ranges [ 0x0C ]; // Fixed maximum size.
    uint numOfRanges;
    float4 swapColor;    
    uint safetyCheck; // Debugging purposes, we can use this here as well.
    min16uint3 colorName [ 2 ]; // Debugging purposes, we can use this here as well.
    
    // Check if the struct has ranges
    inline bool HasRanges()
    {
        return numOfRanges > 0 ? true : false;
    }
    
    // Check if the name is the same as the compare name.
    inline bool CheckName( min16uint3 compare [ 2 ])
    {
        return !any( colorName [ 0 ] - compare [ 0 ] ) & !any( colorName [ 1 ]  - compare [ 1 ] );
    }
    
    // Because we are using a bitmap, it will be BGRA, z is red, x is blue, y is green.
    inline bool IsInRange( float4 pixel, uint rangeIndex )
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
        return safetyCheck == MAX_UINT;
    }
};


// Player position struct
struct PlayerPosition
{
    uint2 headPosition;
    uint2 bodyPosition;
    uint4x2 boundingBox;
};

// PlayerPositions struct holds a max of 6 players.
// Change according to the maximum number of players you want to detect.
struct DetectedPlayers
{
    PlayerPosition players [ MAX_PLAYERS ];
    uint playerCount;
    
        
    ///----------------FOR-GLOBAL-SHARED-VARIABLES-----------------///
    /// These 2 variables are for global variables.
    /// They will only be accessed in PlayerPositionBuffer[1].
    /// This is because there is only 1 actually needed DetectedPlayers struct for output.
    // These 2 variables are needed globally for all threads.
    uint GLOBAL_MERGE_FLAG;
    uint UID_BASE_VALUE;
    
    uint safetyCheck; // Debugging purposes, we can use this here as well.
    
    
    inline bool CheckAlignment()
    {
        return safetyCheck == MAX_UINT;
    }
};

// Bounding box struct, and hair centroid struct.
// position for the hair centroid.
// min/max values for the bounding box.
// unique id for merging.
struct BoundingBox
{
    uint2 min;
    uint2 max;
    uint uniqueId;
    uint linked;
    
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
    uint2 position;
    uint uniqueId;
    uint linked;
    
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
    uint safetyCheck; // Debugging purposes, we can use this here as well.
    
    inline void GetHairCentroid( uint segmentPos, out HairCentroid hairCentroid )
    {
        hairCentroid.linked = hairCentroids [ segmentPos ].linked;
        hairCentroid.position = hairCentroids [ segmentPos ].position;
        hairCentroid.uniqueId = hairCentroids [ segmentPos ].uniqueId;
    }
    
    inline void GetBoundingBox( uint segmentPos, out BoundingBox boundingBox )
    {
        boundingBox.min = boundingBoxes [ segmentPos ].min;
        boundingBox.max = boundingBoxes [ segmentPos ].max;
        boundingBox.uniqueId = boundingBoxes [ segmentPos ].uniqueId;
        boundingBox.linked = boundingBoxes [ segmentPos ].linked;

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
           
    inline bool CheckAlignment()
    {
        return safetyCheck == MAX_UINT;
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
// PlayerPositionBuffer[0] is our output data.
// PlayerPositionBuffer[1] is for global variables
RWStructuredBuffer<DetectedPlayers> PlayerPositionBuffer : register( u3 );

///----------GroupSharedData-----------///

// Thread group shared matrix.
// Reduces the amount of memory accesses.
struct GroupSharedMaxtrix
{
    uint row0 [ X_THREADGROUP ];
    uint row1 [ X_THREADGROUP ];
    uint row2 [ X_THREADGROUP ];
    uint row3 [ X_THREADGROUP ];
};
groupshared GroupSharedMaxtrix localSharedMatrix;

// Fill modified flag, for outline connection, and flood fill.
// We use an int as to be able to safetly use interlocked operations on this.
groupshared uint fillModified; //< Set to 1 to start the flood fill.

// Group shared min and max values for bounding box calculation.
// Set to six as we expect there could only be 6 targets located in a thread group.
// That is still overkill, but it is better to have more than less.
groupshared uint2 groupMin [ MAX_PLAYERS ];
groupshared uint2 groupMax [ MAX_PLAYERS ];

// Group shared bounding box merge threshold.
#define BB_MERGE_THRESHOLD uint( 0x05 )

// Hair position data.
groupshared HairCluster hairClusters [ MAX_PLAYERS ];
groupshared uint hairClusterCount;



///------------Neighborhood-Scans------------///
// We are using a 3x3 neighborhood scan
//#define SCAN_TOP_LEFT int2( -1, -1 )
//#define SCAN_TOP int2( 0, -1 )
//#define SCAN_TOP_RIGHT int2( 1, -1 )
//#define SCAN_LEFT int2( -1, 0 )
//#define SCAN_MIDDLE int2( 0, 0 )
//#define SCAN_RIGHT int2( 1, 0 )
//#define SCAN_BOTTOM_LEFT int2( -1, 1 )
//#define SCAN_BOTTOM int2( 0, 1 )
//#define SCAN_BOTTOM_RIGHT int2( 1, 1 )

#define MATRIX_TYPE_FILL0 uint( 0x0 )
#define MATRIX_TYPE_FILL1 uint( 0x1 )
#define MATRIX_TYPE_SCAN uint( 0x2 )

groupshared const int2 SharedScanMatrix [ 3 ] [ 8 ] [ 3 ] =
{
    // fillMatrix1
    {
        { int2( -1, -1 ), int2( 0, -1 ), int2( 1, -1 ) },
        { int2( 1, -1 ), int2( 1, 0 ), int2( 1, 1 ) },
        { int2( 0, -1 ), int2( 1, -1 ), int2( 1, 0 ) },
        { int2( -1, 1 ), int2( -1, 0 ), int2( 0, -1 ) },
        { int2( -1, 1 ), int2( 0, 1 ), int2( 1, 1 ) },
        { int2( 0, 1 ), int2( -1, 0 ), int2( 0, -1 ) },
        { int2( 0, 1 ), int2( 1, 0 ), int2( 1, -1 ) },
        { int2( 0, 0 ), int2( 0, 0 ), int2( 0, 0 ) } // Dummy data.
    },
    // fillMatrix2
    {                                   // Dummy data.
        { int2( -1, -1 ), int2( 0, -1 ), int2( 0, 0 ) },
        { int2( 1, -1 ), int2( 1, 0 ), int2( 0, 0 ) },
        { int2( 1, 1 ), int2( 0, 1 ), int2( 0, 0 ) },
        { int2( -1, 1 ), int2( -1, 0 ), int2( 0, 0 ) },
        { int2( -1, -1 ), int2( 1, 0 ), int2( 0, 0 ) },
        { int2( 0, 0 ), int2( 0, 0 ), int2( 0, 0 ) }, // Dummy data.
        { int2( 0, 0 ), int2( 0, 0 ), int2( 0, 0 ) }, // Dummy data.
        { int2( 0, 0 ), int2( 0, 0 ), int2( 0, 0 ) } // Dummy data.
    },
    // scanMatrix
    {
        { int2( -1, -1 ), int2( 0, -1 ), int2( 1, -1 ) },
        { int2( 1, -1 ), int2( 1, 0 ), int2( 1, 1 ) },
        { int2( 0, -1 ), int2( 1, -1 ), int2( 1, 0 ) },
        { int2( -1, 1 ), int2( -1, 0 ), int2( 0, -1 ) },
        { int2( -1, 1 ), int2( 0, 1 ), int2( 1, 1 ) },
        { int2( 0, 1 ), int2( -1, 0 ), int2( 0, -1 ) },
        { int2( 0, 1 ), int2( 1, 0 ), int2( 1, -1 ) },
        { int2( 0, -1 ), int2( -1, 0 ), int2( -1, 1 ) }
    }
};

///------------Pixel-Types------------///
// We use these for the local matrix instead of actual pixel colors.
// This is to reduce the computation time.
#define PX_OUTLINE uint( ( 0x01 << 0x01 ) ) 
#define PX_HAIR uint( ( 0x01 << 0x02 ) ) 
#define PX_SKIN uint( ( 0x01 << 0x03 ) ) 
#define PX_FLOODFILL uint( ( 0x01 << 0x04 ) )
#define PX_BACKGROUND uint( ( 0x01 << 0x05 ) )

///------------MACROS-------------///

// Roll right macro.
#define ROR( value, shift ) ( ( ( value >> shift ) | (value << ( 0x020 - shift ) ) ) ) 

// Atomic macros
#define ATOMIC_EXCHANGE_UINT2(target, source, dummy)    \
[unroll(1)]                                             \
do {                                                    \
    InterlockedExchange((target).x, (source).x, dummy); \
    InterlockedExchange((target).y, (source).y, dummy); \
} while (0)

// We are okay with making 4 dummy variables on the stack.
// this macro is used like once or twice and we dont even see the added stack variables.. only in assembly.
#define ATOMIC_EXCHANGE_UINT4X2(target, source, dummy )        \
[unroll(1)]                                                    \
do {                                                           \
    ATOMIC_EXCHANGE_UINT2((target[0]), (source[0]), dummy);    \
    ATOMIC_EXCHANGE_UINT2((target[1]), (source[1]), dummy);    \
    ATOMIC_EXCHANGE_UINT2((target[2]), (source[2]), dummy);    \
    ATOMIC_EXCHANGE_UINT2((target[3]), (source[3]), dummy);    \
} while (0)

#define ATOMIC_MIN_UINT2(target, source)    \
[unroll(1)]                                 \
do {                                        \
    InterlockedMin((target).x, (source).x); \
    InterlockedMin((target).y, (source).y); \
} while (0)

#define ATOMIC_MAX_UINT2(target, source)    \
[unroll(1)]                                 \
do {                                        \
    InterlockedMax((target).x, (source).x); \
    InterlockedMax((target).y, (source).y); \
} while (0)


