using System.Collections.Generic;
using UnityEngine;

namespace VoxelPlay {

    public static class BufferPool<T> {

        public const int CAPACITY = 32;

        struct BufferEntry {
            public List<T> buffer;
            public bool inUse;
        }

        static readonly BufferEntry[] buffers = new BufferEntry[CAPACITY];

        public static List<T> Get() {
            int buffersLength = buffers.Length;
            for (int k = 0; k < buffersLength; k++) {
                if (!buffers[k].inUse) {
                    if (buffers[k].buffer == null) {
                        buffers[k].buffer = new List<T>();
                    }
                    buffers[k].inUse = true;
                    return buffers[k].buffer;
                }
            }
            Debug.LogError("Buffer pool exhausted. This shouldn't occur. Are you releasing the pool after using it?");
            return null;
        }

        public static void Release(List<T> buffer) {
            if (buffer == null) return;
            int buffersLength = buffers.Length;
            for (int k = 0; k < buffersLength; k++) {
                if (buffers[k].buffer == buffer) {
                    buffer.Clear();
                    buffers[k].inUse = false;
                    return;
                }
            }
        }


    }

}