using OxyPlot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BliMonitorTest.data
{
    public struct DataPoints : ICodeGenerating, IEquatable<DataPoints>
    {
        /// <summary>
        /// The undefined.
        /// </summary>
        public static readonly DataPoints Undefined = new DataPoints(double.NaN, double.NaN);

        /// <summary>
        /// The x-coordinate.
        /// </summary>
        internal readonly double x;

        /// <summary>
        /// The y-coordinate.
        /// </summary>
        internal readonly double y;

        /// <summary>
        /// Gets the X-coordinate of the point.
        /// </summary>
        /// <value>The X-coordinate.</value>
        public double X => x;

        /// <summary>
        /// Gets the Y-coordinate of the point.
        /// </summary>
        /// <value>The Y-coordinate.</value>
        public double Y => y;

        /// <summary>
        /// Initializes a new instance of the <see cref="T:OxyPlot.DataPoints" /> struct.
        /// </summary>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        public DataPoints(double x, double y)
        {
            this.x = x;
            this.y = y;
        }

        /// <summary>
        /// Returns C# code that generates this instance.
        /// </summary>
        /// <returns>The to code.</returns>
        public string ToCode()
        {
            return CodeGenerator.FormatConstructor(GetType(), "{0},{1}", x, y);
        }

        /// <summary>
        /// Determines whether this instance and another specified <see cref="T:DataPoints" /> object have the same value.
        /// </summary>
        /// <param name="other">The point to compare to this instance.</param>
        /// <returns><c>true</c> if the value of the <paramref name="other" /> parameter is the same as the value of this instance; otherwise, <c>false</c>.</returns>
        public bool Equals(DataPoints other)
        {
            double num = x;
            int result;
            if (num.Equals(other.x))
            {
                num = y;
                result = (num.Equals(other.y) ? 1 : 0);
            }
            else
            {
                result = 0;
            }

            return (byte)result != 0;
        }

        /// <summary>
        /// Returns a <see cref="T:System.String" /> that represents this instance.
        /// </summary>
        /// <returns>A <see cref="T:System.String" /> that represents this instance.</returns>
        public override string ToString()
        {
            double num = x;
            string str = num.ToString();
            num = y;
            return str + " " + num;
        }

        /// <summary>
        /// Determines whether this point is defined.
        /// </summary>
        /// <returns><c>true</c> if this point is defined; otherwise, <c>false</c>.</returns>
        public bool IsDefined()
        {
            return x == x && y == y;
        }
    }
}
