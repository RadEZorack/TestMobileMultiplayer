//#define USES_TINTING

using System.Runtime.CompilerServices;
using UnityEngine;

namespace VoxelPlay {


    public partial struct Voxel {

        public const ushort EMPTY_TYPE_INDEX = 0;
        public const ushort HOLE_TYPE_INDEX = 2;
        public const ushort DEFAULT_VOXEL_TYPE_INDEX = 3;

        /// <summary>
        /// Voxel definition index in voxelDefinitions list
        /// There're some reserved values: 0 = absolute empty, 1 = empty (not used, reserved), 2 = empty type (hole), 3 = dummy fully opaque, 4+ voxel types with content
        /// </summary>
        public ushort typeIndex;

        /// <summary>
        /// If this voxel lets light pass through, opaque = 0, otherwise this is the light intensity reduction factor
        /// </summary>
        public byte opaque;

        /// <summary>
        /// Current Sun light value of this voxel. Light is the intensity of light that crosses the voxel.
        /// </summary>
        public byte light;

        /// <summary>
        /// Torch light intensity for this voxel.
        /// </summary>
        public byte torchLight;

        /// <summary>
        /// Packed light intensities (torch + Sun light)
        /// </summary>
        public int packedLight => (torchLight << 12) | light;

#if USES_TINTING
		public byte red, green, blue;
#else
        public byte red => 255;
        public byte green => 255;
        public byte blue => 255;
#endif

        /// <summary>
        /// Returns the 
        /// </summary>
        /// <value>The color of the tint</value>
        public Color32 color {
            get {
#if USES_TINTING
				return new Color32 (red, green, blue, 255);
#else
                return Misc.color32White;
#endif
            }
            set {
#if USES_TINTING
				red = value.r;
				green = value.g;
				blue = value.b;
#endif
            }
        }

        /// <summary>
        /// Returns true if this voxel is empty
        /// </summary>
        public bool isEmpty {
            get { return typeIndex <= HOLE_TYPE_INDEX; }
            set {
                bool isCurrentlyEmpty = typeIndex <= HOLE_TYPE_INDEX;
                if (isCurrentlyEmpty != value) {
                    if (value) {
                        typeIndex = 0;
                    } else {
                        typeIndex = HOLE_TYPE_INDEX + 1;
                    }
                }
            }
        }

        /// <summary>
        /// Returns true if this voxel is empty
        /// </summary>
        public bool hasContent => typeIndex > HOLE_TYPE_INDEX;


        /// <summary>
        /// Rreturns true if this voxel is a hole
        /// </summary>
        public bool isHole {
            get { return typeIndex == HOLE_TYPE_INDEX; }
            set {
                if (value) {
                    typeIndex = HOLE_TYPE_INDEX;
                }
            }
        }

        /// <summary>
        /// Returns the voxel definition object of this voxel
        /// </summary>
        public VoxelDefinition type => VoxelPlayEnvironment.instance.voxelDefinitions[typeIndex];

        /// <summary>
        /// Returns the Sun and Torch light intensities packed into a single integer
        /// </summary>
        /// <param name="variation">Optionally provide a small variation in light intensity</param>
        [MethodImpl(256)] // equals to MethodImplOptions.AggressiveInlining
        public int GetPackedLight (float variation) {
            return (torchLight << 12) + (int)(light * variation);
        }

        /// <summary>
        /// Returns true if this voxel has a custom color
        /// </summary>
        public bool isColored => red != 255 || green != 255 || blue != 255;

        /// <summary>
        /// Extra packed info for this voxel
        /// Lower 4 bits = water level
        /// Upper 4 bits = texture rotation
        /// </summary>
        byte _flags;

        [MethodImpl(256)] // equals to MethodImplOptions.AggressiveInlining
        public byte GetFlags () {
            return _flags;
        }

        [MethodImpl(256)] // equals to MethodImplOptions.AggressiveInlining
        public void SetFlags (byte value) {
            _flags = value;
        }

        [MethodImpl(256)] // equals to MethodImplOptions.AggressiveInlining
        public int GetWaterLevel () {
            return _flags & 0xF;
        }

        [MethodImpl(256)] // equals to MethodImplOptions.AggressiveInlining
        public void SetWaterLevel (int value) {
            _flags = (byte)((_flags & 0xF0) | value);
        }

        /// <summary>
        /// Returns the texture rotation bit of this voxel (0=0, 1=90, 2=180, 3=270)
        /// </summary>
        [MethodImpl(256)] // equals to MethodImplOptions.AggressiveInlining
        public int GetTextureRotation () {
            return _flags >> 4;
        }


        /// <summary>
        /// Returns the rotation degrees (0-360) of this voxel
        /// </summary>
        [MethodImpl(256)] // equals to MethodImplOptions.AggressiveInlining
        public float GetTextureRotationDegrees () {
            int rot = _flags >> 4;
            switch (rot) {
                case 1:
                    return 90;
                case 2:
                    return 180;
                case 3:
                    return 270;
                default:
                    return 0;
            }
        }


        [MethodImpl(256)] // equals to MethodImplOptions.AggressiveInlining
        public static float GetTextureRotationDegrees (int rotation) {
            switch (rotation) {
                case 1:
                    return 90;
                case 2:
                    return 180;
                case 3:
                    return 270;
                default:
                    return 0;
            }
        }


        [MethodImpl(256)] // equals to MethodImplOptions.AggressiveInlining
        public static int GetTextureRotationFromDegrees (float rotation) {
            switch ((int)rotation) {
                case 90:
                    return 1;
                case 180:
                    return 2;
                case 270:
                    return 3;
                default:
                    return 0;
            }
        }

        [MethodImpl(256)] // equals to MethodImplOptions.AggressiveInlining
        public void SetTextureRotation (int value) {
            _flags = (byte)((_flags & 0xF) | (value << 4));
        }

        /// <summary>
        /// Last light computed.
        /// </summary>
        public byte lightOrTorch => light > torchLight ? light : torchLight;

        /// <summary>
        /// Whether this voxel has water
        /// </summary>
        /// <value><c>true</c> if has water; otherwise, <c>false</c>.</value>
        public bool hasWater => (_flags & 0xF) > 0;

        /// <summary>
        /// Whether this voxel is a solid block
        /// </summary>
        /// <value><c>true</c> if is solid; otherwise, <c>false</c>.</value>
        public bool isSolid => opaque >= VoxelPlayEnvironment.FULL_OPAQUE;

        public void Clear (byte light) {
            typeIndex = 0;
            opaque = 0;
            this.light = light;
            torchLight = 0;
            _flags = 0;
#if USES_TINTING
                this.red = this.green = this.blue = 255;
#endif
        }

        public static void Clear (Voxel[] voxels, byte light) {
            // Faster method
            Voxel emptyVoxel = new Voxel();
            emptyVoxel.light = light;
            voxels.Fill(emptyVoxel);
        }

        /// <summary>
        /// Sets the voxel type. This method does not update lightmap. Use chunk.SetVoxel if the voxel emits light.
        /// </summary>
        public void Set (VoxelDefinition type) {
#if USES_TINTING
			this.red = type.tintColor.r;
			this.green = type.tintColor.g;
			this.blue = type.tintColor.b;
#endif

            this.typeIndex = type.index;
            switch (type.renderType) {
                case RenderType.Opaque:
                case RenderType.Opaque6tex:
                case RenderType.OpaqueAnimated:
                    this.opaque = VoxelPlayEnvironment.FULL_OPAQUE;
                    this._flags = 0;
                    break;
                case RenderType.Transp6tex:
                    this.opaque = 2;
                    this._flags = 0;
                    break;
                case RenderType.Cutout:
                    this.opaque = 3;
                    this._flags &= 15; // keeps any water amount
                    break;
                case RenderType.Fluid:
                    this.opaque = 4;
                    this._flags = type.height;
                    break;
                case RenderType.Water:
                    this.opaque = 2;
                    this._flags = type.height;
                    break;
                case RenderType.OpaqueNoAO:
                case RenderType.Cloud:
                    this.opaque = VoxelPlayEnvironment.FULL_OPAQUE;
                    this._flags = 0;
                    break;
                default:
                    this.opaque = type.opaque;
                    this._flags &= 15; // keeps any water amount
                    break;
            }
        }

        /// <summary>
        /// Sets the voxel type. This method does not update lightmap. Use chunk.SetVoxel if the voxel emits light.
        /// </summary>
        public void Set (VoxelDefinition type, Color32 tintColor) {
#if USES_TINTING
			this.red = tintColor.r;
			this.green = tintColor.g;
			this.blue = tintColor.b;
#endif

            this.typeIndex = type.index;
            switch (type.renderType) {
                case RenderType.Opaque:
                case RenderType.Opaque6tex:
                case RenderType.OpaqueAnimated:
                    this.opaque = VoxelPlayEnvironment.FULL_OPAQUE;
                    this._flags = 0;
                    break;
                case RenderType.Transp6tex:
                    this.opaque = 2;
                    this._flags = 0;
                    break;
                case RenderType.Cutout:
                    this.opaque = 3;
                    this._flags &= 15;  // keeps any water amount
                    break;
                case RenderType.Fluid:
                    this.opaque = 4;
                    this._flags = type.height;
                    break;
                case RenderType.Water:
                    this.opaque = 2;
                    this._flags = type.height;
                    break;
                case RenderType.OpaqueNoAO:
                case RenderType.Cloud:
                    this.opaque = VoxelPlayEnvironment.FULL_OPAQUE;
                    this._flags = 0;
                    break;
                default:
                    this.opaque = type.opaque;
                    this._flags &= 15;  // keeps any water amount
                    break;
            }
        }

        /// <summary>
        /// Sets the voxel type. This method does not update lightmap. Use chunk.SetVoxel if the voxel emits light.
        /// </summary>
        [MethodImpl(256)]
        public void SetFastOpaque (VoxelDefinition type) {
#if USES_TINTING
			this.red = type.tintColor.r;
			this.green = type.tintColor.g;
			this.blue = type.tintColor.b;
#endif
            this.typeIndex = type.index;
            this.opaque = VoxelPlayEnvironment.FULL_OPAQUE;
            this._flags = 0;
        }

        /// <summary>
        /// Sets the voxel type. This method does not update lightmap. Use chunk.SetVoxel if the voxel emits light.
        /// </summary>
        [MethodImpl(256)]
        public void SetFastWater (VoxelDefinition type) {
            this.typeIndex = type.index;
            this.opaque = 2;
            this._flags = type.height;
        }

        /// <summary>
        /// Represents nothing
        /// </summary>
        public static Voxel Empty = GetEmptyVoxel();

        static Voxel GetEmptyVoxel () {
            Voxel empty = new Voxel();
            empty.Clear(0);
            return empty;
        }

        /// <summary>
        /// Represents a hole (an empty voxel that won't be filled by terrain generator if this voxel is placed before the terrain generator fills the chunk)
        /// </summary>
        public static Voxel Hole = new Voxel() { typeIndex = HOLE_TYPE_INDEX };


        public static bool supportsTinting {
            get {
#if USES_TINTING
				return true;
#else
                return false;
#endif
            }
        }

        public int WriteRawData (byte[] buffer, int index) {
            if (typeIndex >= 255) {
                buffer[index++] = 255;
                buffer[index++] = (byte)(typeIndex >> 8);
            }
            buffer[index++] = (byte)(typeIndex & 0xFF);
            buffer[index++] = (byte)opaque;
            buffer[index++] = (byte)(light + (torchLight << 4));
            buffer[index++] = _flags;
#if USES_TINTING
            buffer [index++] = red;
            buffer [index++] = green;
            buffer [index++] = blue;
#endif
            return index;
        }

        public int ReadRawData (byte[] buffer, int index) {
            typeIndex = buffer[index++];
            if (typeIndex == 255) {
                typeIndex = (ushort)((buffer[index] << 8) + buffer[index + 1]);
                index += 2;
            }

            int packed = buffer[index++];
            opaque = (byte)(packed & 0xF);

            int packedLight = buffer[index++];
            light = (byte)(packedLight & 0xF);
            torchLight = (byte)(packedLight >> 4);

            _flags = buffer[index++];

#if USES_TINTING
            red = buffer [index++];
            green = buffer [index++];
            blue = buffer [index++];
#endif
            return index;

        }


        public static bool operator == (Voxel c1, Voxel c2) {
            return (c1.typeIndex == c2.typeIndex && c1.opaque == c2.opaque && c1.light == c2.light && c1.torchLight == c2.torchLight && c1.hasContent == c2.hasContent && c1.red == c2.red && c1.green == c2.green && c1.blue == c2.blue);
        }

        public static bool operator != (Voxel c1, Voxel c2) {
            return !(c1.typeIndex == c2.typeIndex && c1.opaque == c2.opaque && c1.light == c2.light && c1.torchLight == c2.torchLight && c1.hasContent == c2.hasContent && c1.red == c2.red && c1.green == c2.green && c1.blue == c2.blue);
        }

        public override bool Equals (object obj) {
            if ((obj == null) || !GetType().Equals(obj.GetType())) {
                return false;
            }
            Voxel c2 = (Voxel)obj;
            return (typeIndex == c2.typeIndex && opaque == c2.opaque && light == c2.light && torchLight == c2.torchLight && hasContent == c2.hasContent && red == c2.red && green == c2.green && blue == c2.blue);
        }

        public override int GetHashCode () => base.GetHashCode();

        public override string ToString () => "Voxel type: " + type;

        public static int memorySize {
            get {
#if USES_TINTING
                return 10;
#else
                return 7;
#endif
            }
        }



    }

}
