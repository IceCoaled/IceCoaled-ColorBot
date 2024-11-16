namespace SCB
{

    /// <summary>
    /// Circular buffer class to store and retrieve the most recent N items.
    /// This implementation uses a ping-pong buffer mechanism to alternate between two buffers for reading and writing.
    /// </summary>
    /// <typeparam name="T">The type of data the buffer will store. Must be unmanaged.</typeparam>
    internal unsafe class PingPongBuffer<T> where T : unmanaged
    {
        private T[] bufferA;
        private T[] bufferB;
        private T* currentWriteBufferPtr;
        private T* currentReadBufferPtr;
        private bool isReadingFromBufferA;
        private int writeIndex = 0;
        private int readIndex = 0;
        private readonly int capacity;
        private readonly object internalBufferlock = new();

        /// <summary>
        /// Gets the number of items in the read buffer.
        /// </summary>
        internal int Count => readIndex;

        /// <summary>
        /// Gets the total capacity of the buffer.
        /// </summary>
        internal int Capacity => capacity;

        /// <summary>
        /// Initializes a new instance of the PingPongBuffer class with the specified capacity.
        /// </summary>
        /// <param name="size">The maximum number of items the buffer can hold.</param>
        internal PingPongBuffer( int size )
        {
            capacity = size;
            bufferA = new T[ size ];
            bufferB = new T[ size ];
            isReadingFromBufferA = true; // Initially read from bufferA

            // Initialize pointers to the beginning of each buffer
            fixed ( T* bufferAPtr = bufferA, bufferBPtr = bufferB )
            {
                currentWriteBufferPtr = bufferAPtr;
                currentReadBufferPtr = bufferBPtr;
            }
        }

        /// <summary>
        /// Swaps the read and write buffers without copying data.
        /// </summary>
        internal void SwapBuffers()
        {
            lock ( internalBufferlock )
            {
                T* temp = currentReadBufferPtr;
                currentReadBufferPtr = currentWriteBufferPtr;
                currentWriteBufferPtr = temp;
                readIndex = writeIndex;
                writeIndex = 0;
            }

            isReadingFromBufferA = false;
        }

        /// <summary>
        /// Writes data to the current write buffer.
        /// </summary>
        /// <param name="data">The data array to be written to the buffer.</param>
        /// <exception cref="ArgumentException">Thrown if the data size exceeds the buffer capacity.</exception>
        internal void WriteBuffer( T[] data )
        {
            lock ( internalBufferlock )
            {
                if ( data.Length + writeIndex > capacity )
                {
                    ErrorHandler.HandleException( new ArgumentException( "Data size exceeds buffer capacity." ) );
                }

                // Copy data into the buffer using memory copy to handle unmanaged types.
                fixed ( T* dataPtr = data )
                {
                    Buffer.MemoryCopy( dataPtr, currentWriteBufferPtr + writeIndex, ( capacity - writeIndex ) * sizeof( T ), data.Length * sizeof( T ) );
                    writeIndex += data.Length; // Update write index
                }
            }
        }

        /// <summary>
        /// Retrieves the most recent entry in the read buffer.
        /// </summary>
        /// <returns>The most recent entry in the buffer.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the buffer is empty.</exception>
        internal T GetLatestEntry()
        {
            lock ( internalBufferlock )
            {
                if ( readIndex == 0 )
                {
                    ErrorHandler.HandleExceptionNonExit( new InvalidOperationException( "Buffer is empty" ) );
                }

                // Return the latest entry from the read buffer.
                return currentReadBufferPtr[ readIndex - 1 ];
            }
        }

        /// <summary>
        /// Retrieves the two most recent entries in the read buffer for prediction purposes.
        /// </summary>
        /// <returns>A tuple containing the two most recent entries.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the buffer does not contain at least two entries.</exception>
        internal (T, T) GetTwoMostRecentEntries()
        {
            lock ( internalBufferlock )
            {
                if ( readIndex < 2 )
                {
                    ErrorHandler.HandleExceptionNonExit( new InvalidOperationException( "Not enough data in the buffer" ) );
                }

                // Return the two most recent entries
                return (currentReadBufferPtr[ readIndex - 2 ], currentReadBufferPtr[ readIndex - 1 ]);
            }
        }

        /// <summary>
        /// Clears the current write buffer, resetting it to default values.
        /// </summary>
        internal void ClearWriteBuffer()
        {
            lock ( internalBufferlock )
            {
                for ( int i = 0; i < writeIndex; i++ )
                {
                    currentWriteBufferPtr[ i ] = default; // Clear each element to default
                }
                writeIndex = 0; // Reset write index
            }
        }

        /// <summary>
        /// Clears the current read buffer, resetting it to default values.
        /// </summary>
        internal void ClearReadBuffer()
        {
            lock ( internalBufferlock )
            {
                for ( int i = 0; i < readIndex; i++ )
                {
                    currentReadBufferPtr[ i ] = default; // Clear each element in the read buffer
                }
                readIndex = 0; // Reset read index
            }
        }

        /// <summary>
        /// Checks if the read buffer is empty.
        /// </summary>
        /// <returns>True if the read buffer is empty; otherwise, false.</returns>
        internal bool IsReadBufferEmpty()
        {
            lock ( internalBufferlock )
            {
                return readIndex == 0;
            }
        }

        /// <summary>
        /// Sorts the read buffer in place using the default comparer for type T.
        /// </summary>
        public void Sort()
        {
            if ( isReadingFromBufferA )
            {
                Array.Sort( bufferA, 0, readIndex ); // Sort bufferA up to the current readIndex
            } else
            {
                Array.Sort( bufferB, 0, readIndex ); // Sort bufferB up to the current readIndex
            }
        }
    }
}
