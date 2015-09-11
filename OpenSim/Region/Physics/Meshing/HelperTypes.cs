/*
 * Copyright (c) InWorldz Halcyon Developers
 * Copyright (c) Contributors, http://opensimulator.org/
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSim Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using OpenMetaverse;
using OpenSim.Region.Physics.Manager;
using OpenSim.Region.Physics.Meshing;

public class Vertex : IComparable<Vertex>, IEquatable<Vertex>
{
    private OpenMetaverse.Vector3 _impl;

    public float X
    {
        get { return _impl.X; }
        set { _impl.X = value; }
    }

    public float Y
    {
        get { return _impl.Y; }
        set { _impl.Y = value; }
    }

    public float Z
    {
        get { return _impl.Z; }
        set { _impl.Z = value; }
    }



    public Vertex()
    {
        _impl = new Vector3();
    }

    public Vertex(float x, float y, float z)
    {
        Set(x, y, z);
    }

    public Vector3 AsVector()
    {
        return _impl;
    }

    public void Set(float x, float y, float z)
    {
        if (float.IsInfinity(x) || float.IsNaN(x))
            x = 0f;
        if (float.IsInfinity(y) || float.IsNaN(y))
            y = 0f;
        if (float.IsInfinity(z) || float.IsNaN(z))
            z = 0f;

        _impl = new Vector3(x, y, z);
    }

    public Vertex normalize()
    {
        float tlength = _impl.Length();
        if (tlength != 0)
        {
            float mul = 1.0f / tlength;
            return new Vertex(X * mul, Y * mul, Z * mul);
        }
        else
        {
            return new Vertex(0, 0, 0);
        }
    }

    public Vertex cross(Vertex v)
    {
        return new Vertex(Y * v.Z - Z * v.Y, Z * v.X - X * v.Z, X * v.Y - Y * v.X);
    }

    // disable warning: mono compiler moans about overloading
    // operators hiding base operator but should not according to C#
    // language spec
#pragma warning disable 0108
    public static Vertex operator *(Vertex v, Quaternion q)
    {
        // From http://www.euclideanspace.com/maths/algebra/realNormedAlgebra/quaternions/transforms/

        Vertex v2 = new Vertex(0f, 0f, 0f);

        v2.X =   q.W * q.W * v.X +
            2f * q.Y * q.W * v.Z -
            2f * q.Z * q.W * v.Y +
                 q.X * q.X * v.X +
            2f * q.Y * q.X * v.Y +
            2f * q.Z * q.X * v.Z -
                 q.Z * q.Z * v.X -
                 q.Y * q.Y * v.X;

        v2.Y =
            2f * q.X * q.Y * v.X +
                 q.Y * q.Y * v.Y +
            2f * q.Z * q.Y * v.Z +
            2f * q.W * q.Z * v.X -
                 q.Z * q.Z * v.Y +
                 q.W * q.W * v.Y -
            2f * q.X * q.W * v.Z -
                 q.X * q.X * v.Y;

        v2.Z =
            2f * q.X * q.Z * v.X +
            2f * q.Y * q.Z * v.Y +
                 q.Z * q.Z * v.Z -
            2f * q.W * q.Y * v.X -
                 q.Y * q.Y * v.Z +
            2f * q.W * q.X * v.Y -
                 q.X * q.X * v.Z +
                 q.W * q.W * v.Z;

        return v2;
    }

    public static Vertex operator +(Vertex v1, Vertex v2)
    {
        return new Vertex(v1.X + v2.X, v1.Y + v2.Y, v1.Z + v2.Z);
    }

    public static Vertex operator -(Vertex v1, Vertex v2)
    {
        return new Vertex(v1.X - v2.X, v1.Y - v2.Y, v1.Z - v2.Z);
    }

    public static Vertex operator *(Vertex v1, Vertex v2)
    {
        return new Vertex(v1.X * v2.X, v1.Y * v2.Y, v1.Z * v2.Z);
    }

    public static Vertex operator +(Vertex v1, float am)
    {
        v1.X += am;
        v1.Y += am;
        v1.Z += am;
        return v1;
    }

    public static Vertex operator -(Vertex v1, float am)
    {
        v1.X -= am;
        v1.Y -= am;
        v1.Z -= am;
        return v1;
    }

    public static Vertex operator *(Vertex v1, float am)
    {
        v1.X *= am;
        v1.Y *= am;
        v1.Z *= am;
        return v1;
    }

    public static Vertex operator /(Vertex v1, float am)
    {
        if (am == 0f)
        {
            return new Vertex(0f,0f,0f);
        }
        float mul = 1.0f / am;
        v1.X *= mul;
        v1.Y *= mul;
        v1.Z *= mul;
        return v1;
    }
#pragma warning restore 0108


    public float dot(Vertex v)
    {
        return X * v.X + Y * v.Y + Z * v.Z;
    }

    public Vertex(OpenMetaverse.Vector3 v)
    {
        _impl = new Vector3(v);
    }

    public Vertex Clone()
    {
        return new Vertex(X, Y, Z);
    }

    public static Vertex FromAngle(double angle)
    {
        return new Vertex((float) Math.Cos(angle), (float) Math.Sin(angle), 0.0f);
    }


    public virtual bool Equals(Vertex v, float tolerance)
    {
        OpenMetaverse.Vector3 diff = this._impl - v._impl;
        float d = diff.Length();
        if (d < tolerance)
            return true;

        return false;
    }


    public int CompareTo(Vertex other)
    {
        if (X < other.X)
            return -1;

        if (X > other.X)
            return 1;

        if (Y < other.Y)
            return -1;

        if (Y > other.Y)
            return 1;

        if (Z < other.Z)
            return -1;

        if (Z > other.Z)
            return 1;

        return 0;
    }

    public static bool operator >(Vertex me, Vertex other)
    {
        return me.CompareTo(other) > 0;
    }

    public static bool operator <(Vertex me, Vertex other)
    {
        return me.CompareTo(other) < 0;
    }

    public String ToRaw()
    {
        // Why this stuff with the number formatter?
        // Well, the raw format uses the english/US notation of numbers
        // where the "," separates groups of 1000 while the "." marks the border between 1 and 10E-1.
        // The german notation uses these characters exactly vice versa!
        // The Float.ToString() routine is a localized one, giving different results depending on the country
        // settings your machine works with. Unusable for a machine readable file format :-(
        NumberFormatInfo nfi = new NumberFormatInfo();
        nfi.NumberDecimalSeparator = ".";
        nfi.NumberDecimalDigits = 3;

        String s1 = X.ToString("N2", nfi) + " " + Y.ToString("N2", nfi) + " " + Z.ToString("N2", nfi);

        return s1;
    }

    #region IEquatable<Vertex> Members

    public bool Equals(Vertex other)
    {
        return this._impl.Equals(other._impl);
    }

    public override int GetHashCode()
    {
        return this._impl.GetHashCode();
    }

    #endregion
}

public class Triangle : IEquatable<Triangle>
{
    public Vertex v1;
    public Vertex v2;
    public Vertex v3;

    private float radius_square;
    private float cx;
    private float cy;

    public Triangle()
    {
    }

    public Triangle(Vertex _v1, Vertex _v2, Vertex _v3)
    {
        v1 = _v1;
        v2 = _v2;
        v3 = _v3;

        CalcCircle();
    }

    public void Set(Vertex _v1, Vertex _v2, Vertex _v3)
    {
        v1 = _v1;
        v2 = _v2;
        v3 = _v3;

        CalcCircle();
    }

    public bool isInCircle(float x, float y)
    {
        float dx, dy;
        float dd;

        dx = x - cx;
        dy = y - cy;

        dd = dx*dx + dy*dy;
        if (dd < radius_square)
            return true;
        else
            return false;
    }

    public bool isDegraded()
    {
        // This means, the vertices of this triangle are somewhat strange.
        // They either line up or at least two of them are identical
        return (radius_square == 0.0);
    }

    private void CalcCircle()
    {
        // Calculate the center and the radius of a circle given by three points p1, p2, p3
        // It is assumed, that the triangles vertices are already set correctly
        double p1x, p2x, p1y, p2y, p3x, p3y;

        // Deviation of this routine:
        // A circle has the general equation (M-p)^2=r^2, where M and p are vectors
        // this gives us three equations f(p)=r^2, each for one point p1, p2, p3
        // putting respectively two equations together gives two equations
        // f(p1)=f(p2) and f(p1)=f(p3)
        // bringing all constant terms to one side brings them to the form
        // M*v1=c1 resp.M*v2=c2 where v1=(p1-p2) and v2=(p1-p3) (still vectors)
        // and c1, c2 are scalars (Naming conventions like the variables below)
        // Now using the equations that are formed by the components of the vectors
        // and isolate Mx lets you make one equation that only holds My
        // The rest is straight forward and eaasy :-)
        //

        /* helping variables for temporary results */
        double c1, c2;
        double v1x, v1y, v2x, v2y;

        double z, n;

        double rx, ry;

        // Readout the three points, the triangle consists of
        p1x = v1.X;
        p1y = v1.Y;

        p2x = v2.X;
        p2y = v2.Y;

        p3x = v3.X;
        p3y = v3.Y;

        /* calc helping values first */
        c1 = (p1x*p1x + p1y*p1y - p2x*p2x - p2y*p2y)/2;
        c2 = (p1x*p1x + p1y*p1y - p3x*p3x - p3y*p3y)/2;

        v1x = p1x - p2x;
        v1y = p1y - p2y;

        v2x = p1x - p3x;
        v2y = p1y - p3y;

        z = (c1*v2x - c2*v1x);
        n = (v1y*v2x - v2y*v1x);

        if (n == 0.0) // This is no triangle, i.e there are (at least) two points at the same location
        {
            radius_square = 0.0f;
            return;
        }

        cy = (float) (z/n);

        if (v2x != 0.0)
        {
            cx = (float) ((c2 - v2y*cy)/v2x);
        }
        else if (v1x != 0.0)
        {
            cx = (float) ((c1 - v1y*cy)/v1x);
        }
        else
        {
            Debug.Assert(false, "Malformed triangle"); /* Both terms zero means nothing good */
        }

        rx = (p1x - cx);
        ry = (p1y - cy);

        radius_square = (float) (rx*rx + ry*ry);
    }

    public override String ToString()
    {
        NumberFormatInfo nfi = new NumberFormatInfo();
        nfi.CurrencyDecimalDigits = 2;
        nfi.CurrencyDecimalSeparator = ".";

        String s1 = "<" + v1.X.ToString(nfi) + "," + v1.Y.ToString(nfi) + "," + v1.Z.ToString(nfi) + ">";
        String s2 = "<" + v2.X.ToString(nfi) + "," + v2.Y.ToString(nfi) + "," + v2.Z.ToString(nfi) + ">";
        String s3 = "<" + v3.X.ToString(nfi) + "," + v3.Y.ToString(nfi) + "," + v3.Z.ToString(nfi) + ">";

        return s1 + ";" + s2 + ";" + s3;
    }

    public OpenMetaverse.Vector3 getNormal()
    {
        // Vertices

        // Vectors for edges
        OpenMetaverse.Vector3 e1;
        OpenMetaverse.Vector3 e2;

        e1 = new OpenMetaverse.Vector3(v1.X - v2.X, v1.Y - v2.Y, v1.Z - v2.Z);
        e2 = new OpenMetaverse.Vector3(v1.X - v3.X, v1.Y - v3.Y, v1.Z - v3.Z);

        // Cross product for normal
        OpenMetaverse.Vector3 n = OpenMetaverse.Vector3.Cross(e1, e2);

        // Length
        float l = n.Length();

        // Normalized "normal"
        n = n/l;

        return n;
    }

    public void invertNormal()
    {
        Vertex vt;
        vt = v1;
        v1 = v2;
        v2 = vt;
    }

    // Dumps a triangle in the "raw faces" format, blender can import. This is for visualisation and
    // debugging purposes
    public String ToStringRaw()
    {
        String output = v1.ToRaw() + " " + v2.ToRaw() + " " + v3.ToRaw();
        return output;
    }

    public bool Equals(Triangle other)
    {
        
        return this.v1.Equals(other.v1) && this.v2.Equals(other.v2) && this.v3.Equals(other.v3);
    }

    public override int GetHashCode()
    {
        return v1.GetHashCode() ^ v2.GetHashCode() ^ v3.GetHashCode();
    }
}

public class IndexedTriangle : IEquatable<IndexedTriangle>
{
    public int v1;
    public int v2;
    public int v3;

    public IndexedTriangle(int _v1, int _v2, int _v3)
    {
        int[] arr = new int[3] { _v1, _v2, _v3 };
        Array.Sort(arr);

        v1 = arr[0];
        v2 = arr[1];
        v3 = arr[2];
    }

    public override String ToString()
    {
        return "<" + v1.ToString() + "," + v2.ToString() + "," + v3.ToString() + ">";
    }

    public bool Equals(IndexedTriangle other)
    {
        return this.v1.Equals(other.v1) && this.v2.Equals(other.v2) && this.v3.Equals(other.v3);
    }

    public override int GetHashCode()
    {
        return v1.GetHashCode() ^ v2.GetHashCode() ^ v3.GetHashCode();
    }
}
