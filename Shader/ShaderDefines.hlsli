///----------User-Defines-----------///

// Anything in this section in decmial form gets modified before
// each compile

// AMD uses 64, NVIDIA uses 32 wavefront/warp size
// We use wavefront size * 4 for out thread groups
// Its best for performance to make thread groups multiples of the hardwars wave front
static const uint X_THREADGROUP = uint( 16 );
static const uint Y_THREADGROUP = uint( 4 );

// Monitor resolution
static const uint WINDOW_SIZE_X = uint( 4000 );
static const uint WINDOW_SIZE_Y = uint( 10000 );

// Scan Fov
static const uint2 SCAN_FOV = uint2( 100, 100 );

// Debug flag
#define DEBUG //< If we set debug flag it will define debug automatically


// Number of players we are detecting
static const uint MAX_PLAYERS = uint( 10 ); 

// Number of color ranges in the constant buffer
static const uint NUM_COLOR_RANGES = uint( 20 );  

// Color range names
// Add more names here as needed
static const uint4 COLOR_NAME_OUTLNZ = uint4( 0, 0, 0, 0 );
static const uint4 COLOR_NAME_HAIR = uint4( 0, 0, 0, 0 );

// Max number of groups for scan box
// Max Scan Box Buffer size
// These are used for memory initialization
// Makes things less dynamic which is important for shaders
static const int MAX_SCAN_BOX_BUFFER_SIZE = int( 50000 );
static const int MAX_SCAN_BOX_GROUPS = int( 10000 );

///------------Shader-Constants-------------///

// Thread group details for whole texture
static const uint THREAD_GROUP_SIZE = uint( X_THREADGROUP * Y_THREADGROUP );
static const uint X_GROUP_MAX = uint( ( ( WINDOW_SIZE_X + ( X_THREADGROUP - 1 ) ) / X_THREADGROUP ) );
static const uint Y_GROUP_MAX = uint( ( ( WINDOW_SIZE_Y + ( Y_THREADGROUP - 1 ) ) / Y_THREADGROUP ) );

// Number of thread groups for whole window
static const uint TOTAL_NUM_GROUPS = uint( ( ( WINDOW_SIZE_X + X_THREADGROUP - 1 ) / X_THREADGROUP ) * ( ( WINDOW_SIZE_Y + Y_THREADGROUP - 1 ) / Y_THREADGROUP ) );

// Max number of ranges
static const uint MAX_RANGE_SIZE = 12u;

// Bounding box color
static const unorm float4 BOUNDING_BOX_COLOR = float4( 1.0, 0.0, 0.0, 1.0 );

// Scan box color
static const unorm float4 SCAN_BOX_COLOR = float4( 1.0, 0.91, 0.09, 1.0 );

// Scan box classification details
// 32 bits / 2 bits per classification
static const int MAX_SCAN_BOX_SIDE = 700u;
static const int CLASSIFICATIONS_PER_UINT = 16u;
static const uint CLASSIFICATIONS_PER_BYTE = 4u;
static const int ACTUAL_SCAN_BOX_BUFFER_SIZE = int( float( ( SCAN_FOV.x * SCAN_FOV.y + CLASSIFICATIONS_PER_UINT - 1 ) / CLASSIFICATIONS_PER_UINT ) );
static const int MAX_SCAN_BOX_BUFFER_INDEX = int( ( ACTUAL_SCAN_BOX_BUFFER_SIZE << 2 ) - 1 );

// Scan Box min max 
static const uint2 SCAN_BOX_MIN = uint2( ( WINDOW_SIZE_X / 2 ) - ( SCAN_FOV.x / 2 ), ( WINDOW_SIZE_Y / 2 ) - ( SCAN_FOV.y / 2 ) );
static const uint2 SCAN_BOX_MAX = uint2( ( WINDOW_SIZE_X / 2 ) + ( SCAN_FOV.x / 2 ), ( WINDOW_SIZE_Y / 2 ) + ( SCAN_FOV.y / 2 ) );

// First group that will overlap the scan box
static const uint2 FIRST_GROUP_OVERLAP = uint2( ( SCAN_BOX_MIN.x - ( SCAN_BOX_MIN.x & ( X_THREADGROUP - 1 ) ) ), ( SCAN_BOX_MIN.y - ( SCAN_BOX_MIN.y & ( Y_THREADGROUP - 1 ) ) ) );

// Thread group details for scan box
static const int SCAN_GROUPS_X = uint( ( ( SCAN_BOX_MAX.x - ( SCAN_BOX_MAX.x & ( X_THREADGROUP - 1 ) ) ) - FIRST_GROUP_OVERLAP.x ) / X_THREADGROUP );
static const int SCAN_GROUPS_Y = uint( ( ( SCAN_BOX_MAX.y - ( SCAN_BOX_MAX.y & ( Y_THREADGROUP - 1 ) ) ) - FIRST_GROUP_OVERLAP.y ) / Y_THREADGROUP );
static const int ACTUAL_SCAN_GROUPS = int( ( SCAN_GROUPS_X * SCAN_GROUPS_Y ) );


// Hud blocker
// This is amount of y pixels from top down
// To be used in shader, in order to 
// Reduce noise in results
static const uint BOTTOM_HUD_BLOCK = uint( ( WINDOW_SIZE_Y - uint( floor( float( WINDOW_SIZE_Y * 0.16 ) ) ) ) );

// Min / Max uint values
static const uint MIN_UINT = 0x0000000u;
static const uint MAX_UINT = 0xFFFFFFFFu;
static const int MAX_INT = 0x7FFFFFFF;
static const int MIN_INT = 0x80000000;
static const uint MAX_USHORT = 0xFFFFu;
static const uint MAX_BYTE = 0xFFu;

// Status codes
static const uint STATUS_OK = 1;
static const uint STATUS_ERROR = 2;
static const uint STATUS_NO_MIN_MAX = 3;

// Error return codes
static const uint NULL_RANGE_ERROR = 0xA55355u;
static const uint NO_RANGE_ERROR = 0xB00B5u;
static const uint ALIGNMENT_ERROR = 0x6969u;
static const uint NO_MATCHING_NAME = 0xBADA55u;

// Pixel classifications
// max gives us an easy way to check if we have an error
static const uint PX_MAX = 0x04u;
static const uint PX_OUTLINE = 0x01u;
static const uint PX_HAIR = 0x02u;
static const uint PX_BACKGROUND = 0x03u;
static const uint PX_COMPARAND = 0x0u;

// Mask for px classification
// Mask for group status and pixel type
static const uint BITS2_MASK = 0x03u;

// Group data status/ pixel type constants
static const uint GROUP_DATA_PER_UINT = 0x04u;
static const uint GROUP_DATA_PER_BYTE = 0x02u;


// Constants for bounding box creation
static const uint TOP_BOX = 100u;
static const uint BOTTOM_BOX = 200u;

// Constants for batch processing
// 31 for zero based indexing
static const uint INDEXES_PER_UINT = 31u;

// Flag for group centroids buffer
static const uint CLUSTER_AVAILABLE = 0x3E8u;

// Max number of centroids a thread will check when merging
// Max scan groups/ thread group size, which is 121
// But we max it out at bits per uint * 4
static const uint MAX_CENTROIDS = 128u;

// Constants for thread blocking
static const uint TB_CENTROIDS_MERGED = 0x969696u;
static const uint TB_DEBUG_DRAW = 0x696969u;
// Loop cap for thread barrier
static const uint MEMORY_BARRIER_MAXOUT = 1000u;


// Constants for head size
// Based off 1 screen shot up close
// and 1 taken at a far distance
static const uint MIN_HEAD_SIZE = 5u;
static const uint MAX_HEAD_SIZE = 50u;
static const uint MAX_BODY_WIDTH = 350u;

///----------Structs-----------///


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
    ColorRange ranges [ MAX_RANGE_SIZE ]; //offset: 0 // Fixed maximum size 12.
    unorm float4 swapColor; //offset: 288
    uint numOfRanges; //offset: 304
    uint2 padding0; //offset: 308
    uint4 colorName; //offset: 316
    uint4x3 padding1; //offset: 332
	uint safetyCheck; //offset: 380
    
    
    // Check if the struct has ranges
    inline bool HasRanges()
    {
        return numOfRanges > 0;
    }
    
    // Note for anyone learning shaders at least for HLSL
    // The gpu automatically points float4 swizzles for colors
    // To RGBA, even if the texture is BGRA 
    // Not knowing this screwed me over for a long time
	inline bool IsInRange( const unorm float4 pixel, const uint rangeIndex )
    {
        ColorRange range = ranges [ rangeIndex ];
        return pixel.r >= range.redRange.minimum && pixel.r <= range.redRange.maximum &&
        pixel.g >= range.greenRange.minimum && pixel.g <= range.greenRange.maximum && 
        pixel.b >= range.blueRange.minimum && pixel.b <= range.blueRange.maximum;
    }
    
    inline bool CheckNull()
    {
        return !any( swapColor ) && 
        !any( colorName ) && 
        numOfRanges == 0;
    }
    
    // Checks of the safety check value is the max int value.
    // This is the easiest way to check if the struct is aligned properly.
    inline bool CheckAlignment()
    {
        return safetyCheck == MAX_UINT;
    }

}; //size : 384




struct BndBoxMM
{
    uint2 bbMin; //offset : 0
    uint2 bbMax; //offset : 8     
}; // size: 16


// Struct for  holding global variables
// For Detecting final player locations
struct TargetFinder
{
    uint2 rightLowestPoint; //offset : 0
    uint2 leftLowestPoint; //offset : 8
    uint2 ySearchPlaneLane; //offset : 16
    int leftReductionDegree; //offset : 24
    int DegHairToRightLow; //offset : 28
    int DegHairToLeftLow; //offset : 32
    int distance; //offset : 36   
}; //size : 40



// Player position struct
struct PlayerPosition
{
    uint2 headPosition; //offset : 0
    uint2 bodyPosition; //offset : 8
    uint4x2 boundingBox; //offset : 16
}; //size : 48



// PlayerPositions struct holds a max of 6 players
// Change according to the maximum number of players you want to detect
struct DetectedPlayers
{
    PlayerPosition players [ MAX_PLAYERS ]; //offset : 0
	BndBoxMM scanBoxBB; //offset : 288
	TargetFinder globals; //offset : 304
    uint playerCount; //offset : 344 
	uint centroidMergeFlag; //offset : 348
	uint4 padding0; //offset : 352
	uint3 padding1; //offset : 368
    uint safetyCheck; //offset : 380 

    inline bool CheckAlignment()
    {
        return safetyCheck == MAX_UINT;
    }
}; //size : 384



struct HairCentroid
{
    uint4 outlinePos; //offset : 0
    uint clusterSize; //offset : 16
	uint allowance; //offset : 20
    uint2 padding0; //offset : 24
    
    inline bool VerifyCentroid()
    {
		return clusterSize > 5 && 
        all( outlinePos.xz < SCAN_BOX_MAX.x ) && 
        all( outlinePos.xz > SCAN_BOX_MIN.x ) &&
        all( outlinePos.yw < SCAN_BOX_MAX.y ) && 
        all( outlinePos.yw > SCAN_BOX_MIN.y );
	}
    
	inline bool CheckForMerge( const uint4 otherOutlinePos )
    {
		return any( otherOutlinePos >= uint4( outlinePos - allowance ) ) && any( otherOutlinePos <= uint4( outlinePos + allowance ) );
	}
	
}; // size: 32


// Group data struct
// Holds the group centroid data
// each pixel( thread ) will also hold their status & pixel type
// each thread will get half a byte(4 bits) for their status & pixel type
// Status will be bits 0, 1 and pixel type will be bits 2, 3
struct GroupData
{
    HairCentroid hairCentroid; //offset : 0
	uint hasCluster; //offset : 32
	uint3 padding0; //offset : 36
	uint4x4 statusPxlType; //offset : 48 
	uint3 padding2; //offset : 112
	uint safetyCheck; //offset : 124
    
	inline bool CheckForClusters()
	{
		return hasCluster == CLUSTER_AVAILABLE;
	}
    
    inline bool CheckAlignment()
    {
        return safetyCheck == MAX_UINT;
    }
}; // size: 128


// Hair cluster struct
// We only really need 1 position since we are verifying
// The positions are inside an outline
// This way we put a extra check in place
struct HairCluster
{
    uint4 outlinePos;
	uint clusterSize;
	uint2 padding0;
	uint safetyCheck;
    
	inline bool VerifyOutline()
	{
		return all( outlinePos != MAX_UINT ) && all( outlinePos != MIN_UINT );
	}
    
	inline bool VerifyCluster()
	{
		return all( outlinePos != MAX_UINT ) && all( outlinePos != MIN_UINT ) && ( clusterSize > 5 && clusterSize < THREAD_GROUP_SIZE );
	}
};


//struct DebugCentroidMerge
//{
//	HairCentroid hairCentroids [ MAX_PLAYERS ]; //offset : 0
//	uint4x3 padding0; //offset : 192
//	uint2 padding1; //offset : 240
//	uint hasCluster; //offset : 248
//	uint safetyCheck; //offset : 252
	
    
//	inline bool CheckAlignment()
//	{
//		return safetyCheck == MAX_UINT;
//	}
//}; // size: 256



///----------Buffers-----------///

// UAV buffers are are unordered access, in simple terms
// Think of them as regular memory, any UAV buffer marked 'RW'
// Can simultaneously be read / written to by all threads
// This doesnt stop race conditions! but this is why we mark
// Any buffer being written to as globally coherent
// This helps the compiler force memory alignment I.E
// Barriers to help with race conditions
// We also use our own barriers when necessary


// We are using a typed load as the texture is DXGI_FORMAT_B8G8R8A8_UNORM, we are using a unorm float4.
// This is also why we checked for typed load support in the main program.
globallycoherent RWTexture2D<unorm float4> UavBuffer : register( u0 );

// Buffer for precomputed color ranges
// NUM_COLOR_RANGES is our max index
RWStructuredBuffer<ColorRanges> ColorRangeBuffer : register( u1 );

// Buffer for group centroids
// MAX_SCAN_BOX_GROUPS is our max index
// ACTUAL_SCAN_BOX_GROUPS is our actual max index
globallycoherent RWStructuredBuffer<GroupData> GroupDataBuffer : register( u2 );

///-----------------ScanBoxData------------------///
// Classifications are packed: 16 pixels per uint    
// [31:30][29:28]...[3:2][1:0] = 16 pixels        
// Each pixel uses 2 bits:                                                           
// 01 = Outline                                     
// 10 = Hair                                       
// 11 = Background 

//|----[P0][P1][P2]...[P14][P15]| = uint0
//|----[P16][P17]...[P30][P31]| = uint1
//|----[...continues...]
// Buffer for the scan box data
// MAX_SCAN_BOX_MATRIX_SIZE is our max index
// MAX_SCAN_BOX_BUFFER_SIZE is our actual max index 
globallycoherent RWByteAddressBuffer ScanBoxBuffer : register( u3 );

// Our resulting buffer for player positions
// PlayerPositionBuffer[0] is our only struct
globallycoherent RWStructuredBuffer<DetectedPlayers> PlayerPositionBuffer : register( u4 );



//globallycoherent RWStructuredBuffer<DebugCentroidMerge> ClusterMergeDebug : register( u6 );



///----------GroupSharedData-----------///

groupshared HairCentroid gmMergedClusters [ MAX_PLAYERS ];
groupshared uint gmValidCount;

// Hair position data
groupshared HairCluster gmHairCluster;

// Used when group thread 0,0 checks for errors
groupshared uint gmGroupStatus = 0u;

groupshared int gmGroupDataIndex = -10;

// Dummy values for atomic operations
static uint DUMMY_UINT = 0u;
static uint DUMMY_COMPARAND = 0xF1F1F1F1u;


