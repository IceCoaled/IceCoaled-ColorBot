///----------User-Defines-----------///

// AMD uses 32/64, NVIDIA uses 16/32 wavefront/warp size
#define X_THREADGROUP ((int)32)

// Thread group size
#define THREAD_GROUP_SIZE ((int)256)

// Monitor resolution
#define WINDOW_SIZE_X ((int)1920)
#define WINDOW_SIZE_Y ((int)1080)

// pixel format
// We will always use UNORM for the bit representation.
// All we are worried about here is the order of the channels, as we are using a bitmap.
#define BRGA

// Number of color ranges in the constant buffer
#define NUM_COLOR_RANGES 20

// Number of players we are detecting
#define MAX_PLAYERS 6

// Define for better precision
#define precise

// Debug flag
#define DEBUG

// Debug pixel replacement color
#define BACKGROUND_PIXEL_COLOR float4(0.164, 0.415, 0.545, 0.54)  // Dark Khaki(42, 106, 138, 0.54)

// Object Fill Color'
#define OBJECT_FILL_COLOR float4(0.294, 0.317, 0.0, 0.51)  // Deep Teal(75, 81, 0, 0.51)

// Max uint value
#define MAX_INT ((int)0x7FFFFFFF)

///----------Return-Codes-----------///

#define NO_RANGE_ERROR ((int)-0xB00B5)
#define ALIGNMENT_ERROR ((int)-0x6969)
#define SET_BACKGROUND ((int)0x0A55)
#define SET_OUTLINE ((int)0)
#define SET_HAIR ((int)1)
#define SET_SKIN ((int)2)

///----------Color-Names-----------///
// Edit these while we edit the other constants
static const half3 COLOR_NAME_OUTLNZ = { 0, 0, 0 };
static const half3 COLOR_NAME_HAIR = { 0, 0, 0 };
static const half3 COLOR_NAME_SKIN = { 0, 0, 0 };
// Add more names here

///----------Structs-----------///

// Hair cluster struct

// Hair cluster threshold
// if a new hair pixel is outside this threshold, it will be considered a new cluster.
#define HAIR_CLUSTER_THRESHOLD 0xD
#define HAIR_MERGE_THRESHOLD 0x1E
struct HairCluster
{
    int2 positions [ THREAD_GROUP_SIZE ]; //< Way overkill, but so be it.
    int2 averagePos;
    int clusterId;
    int clusterSize;
    
    inline bool CheckForGrouping( int2 pos )
    {
        return distance( asfloat( pos ), asfloat( averagePos ) ) > HAIR_CLUSTER_THRESHOLD ? true : false;
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
    
    inline void SetClusterId( int seed )
    {
        int id = 0x3E1A;
        for ( int i = 8; i < 8; i++ )
        {
            id = id * ( seed + i );
            id ^= id >> 16;
            seed = ( seed << 5 ) ^ id ^ ( seed >> 3 );
        }
        
        InterlockedCompareExchange( clusterId, 0, id, 0 );
    }
    
    inline int GetClusterId()
    {
        return clusterId;
    }
    
    inline int2 GetAveragePos()
    {
        return averagePos;
    }
    
    inline void MergeCluster( HairCluster cluster )
    {
        int ourCurrentSize = 0;
        int otherClusterSize = cluster.clusterSize;
        
        for ( int i = 0; i < clusterSize; i++ )
        {
            InterlockedAdd( ourCurrentSize, 1 );
            
            // Add new position
            InterlockedCompareExchange( positions [ ourCurrentSize ].x, 0, cluster.GetPosition( i ).x, 0 );
            InterlockedCompareExchange( positions [ ourCurrentSize ].y, 0, cluster.GetPosition( i ).y, 0 );
            
            // Update average
            AddAverage( cluster.GetPosition( i ), clusterSize );

            // Update size
            InterlockedAdd( clusterSize, 1 );
        }
    }
};


// Define the Range structure for color limits
struct Range
{
    float minimum;
    float maximum;
};

// ColorRange to hold RGB ranges
struct ColorRange
{
    Range redRange;
    Range greenRange;
    Range blueRange;
};

// ColorRanges to hold multiple ranges and the swap color
struct ColorRanges
{
    ColorRange ranges [ 0xC ]; // Define a fixed maximum size
    uint numOfRanges;
    float4 swapColor;
    
    int safetyCheck; // Debugging purposes, we can use this here as well.
    half3 name; // Debugging purposes, we can use this here as well.
    
    // Check if the struct has ranges
    inline bool HasRanges()
    {
        return numOfRanges > 0 ? true : false;
    }
    
    // Check if the name is the same as the compare name
    inline bool CheckName( half3 compare )
    {
        // The ternary result is flipped because All returns true if all elements are true, and we want to return true if all elements are false.
        return all( name - compare ) ? false : true;
    }
    
    // Because we are using a bitmap, it will be BGRA, z is red, x is blue, y is green
    inline bool IsInRange( float4 pixel, int rangeIndex )
    {
#ifdef BRGA
        return ( pixel.z >= ranges [ rangeIndex ].redRange.minimum && pixel.x <= ranges [ rangeIndex ].redRange.maximum &&
                pixel.y >= ranges [ rangeIndex ].greenRange.minimum && pixel.y <= ranges [ rangeIndex ].greenRange.maximum &&
                pixel.x >= ranges [ rangeIndex ].blueRange.minimum && pixel.z <= ranges [ rangeIndex ].blueRange.maximum ) ? true : false;
#else
        return (pixel.x >= ranges[rangeIndex].redRange.minimum && pixel.x <= ranges[rangeIndex].redRange.maximum &&
                pixel.y >= ranges[rangeIndex].greenRange.minimum && pixel.y <= ranges[rangeIndex].greenRange.maximum &&
                pixel.z >= ranges[rangeIndex].blueRange.minimum && pixel.z <= ranges[rangeIndex].blueRange.maximum) ? true : false;
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
    uint4 boundingBox;
};

// PlayerPositions struct holds a max of 6 players
// Change according to the maximum number of players you want to detect
struct DetectedPlayers
{
    PlayerPosition players [ MAX_PLAYERS ];
    int playerCount;
    int safetyCheck; // Debugging purposes, we can use this here as well.
};

///----------Buffers-----------///

// UAV buffer for the image we can R/W on all threads simultaneously with a uav buffer
// We are using a typed load as the texture is DXGI_FORMAT_B8G8R8A8_UNORM, it could be DXGI_FORMAT_R8G8B8X8_UNORM as well, we are using a float4 regardless.
// This is also why we checked for typed load support in the main program, and we arent using a simple float4 pixel = UavBuffer[ThreadId.xy] here.
RWTexture2D<unorm float4> UavBuffer : register( u0 );

// Buffer for precomputed color ranges
RWStructuredBuffer<ColorRanges> ColorRangeBuffer : register( u1 );

// Our resulting buffer for player positions
RWStructuredBuffer<DetectedPlayers> PlayerPositionBuffer : register( u2 );

///----------GroupSharedData-----------///

// Thread group segment size
#define SEGMENT_SIZE THREAD_GROUP_SIZE / MAX_PLAYERS

// Thread group shared matrix
// Reduces the amount of memory accesses.
groupshared int localSharedMatrix [ THREAD_GROUP_SIZE / 2 ] [ THREAD_GROUP_SIZE / 2 ];

// Fill modified flag, for outline connection, and flood fill
groupshared bool fillModified = true; //< Set to true to start the flood fill

// Group shared min and max values for bounding box calculation
// Set to six as we expect there could only be 6 targets located in a thread group.
// That is still overkill, but it is better to have more than less.
groupshared int2 groupMin [ SEGMENT_SIZE ];
groupshared int2 groupMax [ SEGMENT_SIZE ];

// Hair position data
groupshared HairCluster hairClusters [ MAX_PLAYERS * 4 ];
groupshared int hairClusterCount = 0;

#define PARALLEL_POS_CALC(localPos, arrayCount) ( ( int ) ( ( arrayCount >> 1 ) + ( localPos + 1 ) ) )

// Neighbourhood matrix offsets
// This is a 3x3 matrix, with the center pixel being the current pixel.
static const int2 ScanOffsets [ 8 ] =
{
    int2( -1, -1 ), int2( 0, -1 ), int2( 1, -1 ),
    int2( -1, 0 ), int2( 1, 0 ),
    int2( -1, 1 ), int2( 0, 1 ), int2( 1, 1 )
};

// Calculate the current segment
// This goes down to the nearest segment size
// Then sets the current segment to a segment index with 0 based index.
#define SEGMENT_CALC( localPos ) ( (int) ( ( ( ( int ) localPos.x ) + SEGMENT_SIZE ) & ~( SEGMENT_SIZE -1 ) ) / ( SEGMENT_SIZE - 1 ) )

// Pixel types
// We use these for the local matrix instead of actual pixel colors.
// This is to reduce the computation time.
static const int PX_OUTLINE = ( 1 << 1 );
static const int PX_HAIR = ( 1 << 2 );
static const int PX_SKIN = ( 1 << 3 );
static const int PX_FLOODFILL = ( 1 << 4 );
static const int PX_BACKGROUND = ( 1 << 5 );