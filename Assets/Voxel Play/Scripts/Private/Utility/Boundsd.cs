using UnityEngine;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace VoxelPlay {
    [StructLayout(LayoutKind.Sequential)]
    public partial struct Boundsd {

        private Vector3d m_Center;
        private Vector3 m_Extents;

        public static Boundsd empty = new Boundsd();

        // Creates new Bounds with a given /center/ and total /size/. Bound ::ref::extents will be half the given size.
        // This signature shouldn't be used but it's here to avoid mistakes by passing size as Vector3d. Size should be passed as Vector3 because size shouldn't be affected by the shift.
        public Boundsd (Vector3d center, Vector3d size) {
            m_Center = center;
            // don't apply shift to size
            m_Extents.x = (float)(size.x * 0.5);
            m_Extents.y = (float)(size.y * 0.5);
            m_Extents.z = (float)(size.z * 0.5);
        }

        // Creates new Bounds with a given /center/ and total /size/. Bound ::ref::extents will be half the given size.
        public Boundsd (Vector3d center, Vector3 size) {
            m_Center = center;
            m_Extents.x = size.x * 0.5f;
            m_Extents.y = size.y * 0.5f;
            m_Extents.z = size.z * 0.5f;
        }

        [MethodImpl(256)]
        public static implicit operator Bounds (Boundsd bb) {
            return new Bounds(bb.center, bb.size);
        }

        [MethodImpl(256)]
        public static implicit operator Boundsd (Bounds bb) {
            return new Boundsd(bb.center, bb.size);
        }

        // used to allow Bounds to be used as keys in hash tables
        public override int GetHashCode () {
            return center.GetHashCode() ^ (extents.GetHashCode() << 2);
        }

        // also required for being able to use Vector4s as keys in hash tables
        public override bool Equals (object other) {
            if (!(other is Boundsd)) return false;

            return Equals((Boundsd)other);
        }

        public bool Equals (Boundsd other) {
            return center.Equals(other.center) && extents.Equals(other.extents);
        }

        // The center of the bounding box.
        public Vector3d center { get { return m_Center; } set { m_Center = value; } }

        // The total size of the box. This is always twice as large as the ::ref::extents.
        public Vector3 size { get { return m_Extents * 2.0f; } set { m_Extents.x = value.x * 0.5f; m_Extents.y = value.y * 0.5f; m_Extents.z = value.z * 0.5f; } }

        // The extents of the box. This is always half of the ::ref::size.
        public Vector3 extents { get { return m_Extents; } set { m_Extents.x = value.x; m_Extents.y = value.y; m_Extents.z = value.z; } }

        // The minimal point of the box. This is always equal to ''center-extents''.
        public Vector3d min { get { return center - extents; } set { SetMinMax(value, max); } }

        // The maximal point of the box. This is always equal to ''center+extents''.
        public Vector3d max { get { return center + extents; } set { SetMinMax(min, value); } }

        //*undoc*
        public static bool operator == (Boundsd lhs, Boundsd rhs) {
            // Returns false in the presence of NaN values.
            return (lhs.center == rhs.center && lhs.extents == rhs.extents);
        }

        //*undoc*
        public static bool operator != (Boundsd lhs, Boundsd rhs) {
            // Returns true in the presence of NaN values.
            return !(lhs == rhs);
        }

        // Sets the bounds to the /min/ and /max/ value of the box.
        public void SetMinMax (Vector3d min, Vector3d max) {
            extents = (max - min) * 0.5;
            center = min + extents;
        }

        // Grows the Bounds to include the /point/.
        public void Encapsulate (Vector3d point) {
            SetMinMax(Vector3d.Min(min, point), Vector3d.Max(max, point));
        }

        // Grows the Bounds to include the /Bounds/.
        public void Encapsulate (Boundsd bounds) {
            Encapsulate(bounds.center - bounds.extents);
            Encapsulate(bounds.center + bounds.extents);
        }

        // Expand the bounds by increasing its /size/ by /amount/ along each side.
        public void Expand (double amount) {
            amount *= .5;
            extents += new Vector3d(amount, amount, amount);
        }

        // Expand the bounds by increasing its /size/ by /amount/ along each side.
        public void Expand (Vector3d amount) {
            extents += amount * 0.5;
        }

        // Does another bounding box intersect with this bounding box?
        public bool Intersects (Boundsd bounds) {
            return (min.x <= bounds.max.x) && (max.x >= bounds.min.x) &&
                (min.y <= bounds.max.y) && (max.y >= bounds.min.y) &&
                (min.z <= bounds.max.z) && (max.z >= bounds.min.z);
        }


        override public string ToString () {
            return ToString(null);
        }

        // Returns a nicely formatted string for the bounds.
        public string ToString (string format) {
            if (string.IsNullOrEmpty(format))
                format = "F1";
            return string.Format("Center: {0}, Extents: {1}", m_Center.ToString(format), m_Extents.ToString(format));
        }
    }
}

