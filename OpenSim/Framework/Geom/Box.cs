/**
 * Box
 * A model of a 3d rectangle 
 * 
 * (c) 2010 InWorldz, LLC.
 */

using System;
using System.Collections.Generic;
using System.Text;
using OpenMetaverse;

namespace OpenSim.Framework.Geom
{
    /// <summary>
    /// Models a 3d rectangle
    /// </summary>
    public class Box
    {
        private Vector3 _center;
        private Vector3 _size;
        private Quaternion _rotation;

        public Vector3 Center
        {
            get { return _center; }
            set { _center = value; }
        }

        public Vector3 Size
        {
            get { return _size; }
            set { _size = value; }
        }

        public Vector3 Extent
        {
            get { return _size / 2.0f; }
            set { _size = value * 2.0f; }
        }

        public Quaternion Rotation
        {
            get { return _rotation; }
            set { _rotation = value; }
        }

        /// <summary>
        /// Construct a new box
        /// </summary>
        /// <param name="center"></param>
        /// <param name="size"></param>
        public Box(Vector3 center, Vector3 size)
        {
            _center = center;
            _size = size;
        }
        public Box(Vector3 center, Vector3 size, Quaternion rotation)
        {
            _center = center;
            _size = size;
            _rotation = rotation;
        }
        public Box(Box other)
        {
            _center = other._center;
            _size = other._size;
            _rotation = other._rotation;
        }

        bool Intersects(Box other)
        {
            float test, test2;
            
            test = this.Center.X - (this.Size.X / 2.0f);
            test2 = other.Center.X - (other.Size.X / 2.0f);
            if ((test+Size.X < test2) || (test > test2+other.Size.X))
                return false;

            test = this.Center.Y - (this.Size.Y / 2.0f);
            test2 = other.Center.Y - (other.Size.Y / 2.0f);
            if ((test+Size.Y < test2) || (test > test2+other.Size.Y))
                return false;

            test = this.Center.Z - (this.Size.Z / 2.0f);
            test2 = other.Center.Z - (other.Size.Z / 2.0f);
            if ((test+Size.Z < test2) || (test > test2+other.Size.Z))
                return false;

            return true;
        }

        public bool ContainsPoint(Vector3 point)
        {
            float test;

            test = this.Center.X - (this.Size.X / 2.0f);
            if ((test + Size.X < point.X) || (test > point.X))
                return false;

            test = this.Center.Y - (this.Size.Y / 2.0f);
            if ((test + Size.Y < point.Y) || (test > point.Y))
                return false;

            test = this.Center.Z - (this.Size.Z / 2.0f);
            if ((test + Size.Z < point.Z) || (test > point.Z))
                return false;

            return true;
        }

        /// <summary>
        /// Calculates the minimum bounding box to contain the given boxes.
        /// Please note, this function does not yet take rotation into account
        /// </summary>
        /// <param name="boxes"></param>
        /// <returns></returns>
        public static Box CalculateBoundingBox(IEnumerable<Box> boxes)
        {
            Vector3 min = new Vector3(9999, 9999, 9999);
            Vector3 max = new Vector3(-9999, -9999, -9999);

            foreach (Box box in boxes)
            {
                //get the center point of the box
                Vector3 boxMin = box.Center - Vector3.Divide(box.Size, 2.0f);
                Vector3 boxMax = box.Center + Vector3.Divide(box.Size, 2.0f);

                min = Vector3.Min(boxMin, min);
                max = Vector3.Max(boxMax, max);
            }

            //the new box will have a size of the min and max vectors subtracted 
            //and a center at min + (size / 2)
            Vector3 size = max - min;
            Vector3 center = min + Vector3.Divide(size, 2.0f);

            return new Box(center, size);
        }

        /// <summary>
        /// Converts the given list of boxes to boxes located at a offset of the given 
        /// center point
        /// </summary>
        /// <param name="center"></param>
        /// <param name="boxes"></param>
        public static void TranslateToOffsets(Vector3 center, IEnumerable<Box> boxes)
        {
            foreach (Box box in boxes)
            {
                box.Center = box.Center - center;
            }
        }

        /// <summary>
        /// Recenters the given boxes that already have offset positions to a new center point
        /// </summary>
        /// <param name="newCenter">The new center point</param>
        /// <param name="boxes">The boxes to use</param>
        public static void RecenterUsingOffsetPositions(Vector3 center, IEnumerable<Box> boxes)
        {
            foreach (Box box in boxes)
            {
                box.Center = center + box.Center;
            }
        }


        public static void RecenterUsingNewCenter(Vector3 oldCenter, Vector3 newCenter, IEnumerable<Box> boxes)
        {
            foreach (Box box in boxes)
            {
                box.Center = (box.Center - oldCenter) + newCenter;
            }
        }
    }
}
