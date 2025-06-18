using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GDC.Copilio.Common
{
    public partial class Util
    {


        public static class Gaurd
        {


            public static void ArgumentIsNotNull(object argument, string name)
            {
                if (argument == null)
                {
                    throw new ArgumentNullException(name);
                }



            }


            public static void StringIsNotNullOrEmpty(string value, string name)
            { 
            
                if(string.IsNullOrEmpty(value)){
                    throw new ArgumentNullException(name);
                }
            
            }



        }
    }
}
