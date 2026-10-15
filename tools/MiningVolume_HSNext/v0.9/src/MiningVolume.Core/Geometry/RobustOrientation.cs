using System;
using System.Numerics;

namespace MiningVolume.Core.Geometry
{
    // Filtered orient2d. The filter bounds IEEE-754 rounding, not a survey-area
    // cutoff. Ambiguous signs are resolved exactly over the input doubles.
    public static class RobustOrientation
    {
        const double Epsilon=1.1102230246251565e-16;
        public static double Determinant(Vec2 a,Vec2 b,Vec2 c)
        {
            double x=b.X-a.X,y=b.Y-a.Y,u=c.X-a.X,v=c.Y-a.Y;
            double left=x*v,right=y*u,det=left-right;
            double bound=(3+16*Epsilon)*Epsilon*(Math.Abs(left)+Math.Abs(right));
            if(!double.IsNaN(det)&&!double.IsInfinity(det)&&Math.Abs(det)>bound)return det;
            if(left==0 && right==0 && (x==0||v==0) && (y==0||u==0))return 0;
            var values=new[]{a.X,a.Y,b.X,b.Y,c.X,c.Y};var mant=new BigInteger[6];var exp=new int[6];int min=1024;
            for(int i=0;i<6;i++) {
                if(double.IsNaN(values[i])||double.IsInfinity(values[i]))throw new InvalidOperationException("Tọa độ không hữu hạn.");
                long bits=BitConverter.DoubleToInt64Bits(values[i]);int e=(int)((bits>>52)&2047);
                long m=bits&0x000fffffffffffffL;if(e!=0)m|=1L<<52;
                mant[i]=bits<0?-new BigInteger(m):new BigInteger(m);exp[i]=e==0?-1074:e-1075;
                if(m!=0)min=Math.Min(min,exp[i]);
            }
            if(min==1024)return 0;
            for(int i=0;i<6;i++)if(!mant[i].IsZero)mant[i]<<=exp[i]-min;
            var exact=(mant[2]-mant[0])*(mant[5]-mant[1])-(mant[3]-mant[1])*(mant[4]-mant[0]);
            if(exact.IsZero)return 0;
            var abs=BigInteger.Abs(exact);var bytes=abs.ToByteArray();int high=bytes.Length-1;while(bytes[high]==0)high--;
            int length=high*8;for(int k=bytes[high];k>0;k>>=1)length++;
            int shift=Math.Max(0,length-53);
            double result=(double)(abs>>shift)*Math.Pow(2,2*min+shift)*exact.Sign;
            if(result==0||double.IsInfinity(result))throw new InvalidOperationException("Diện tích vượt miền biểu diễn số; không được bỏ tam giác.");
            return result;
        }
    }
}
