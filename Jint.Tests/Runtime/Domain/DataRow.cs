using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Jint.Tests.Runtime.Domain
{
    public class DataRow
    {
        private Int32 _columnIndex = 0;
        private string _columnName = "MrColumn";
        private object _column = "Column Data";

        public DataRow()
        {

        }

        public object this[int columnIndex]
        {
            get
            {
                if (columnIndex == _columnIndex) return _column;
                else return null;
            }
            set
            {
            }
        }

        public object this[string columnName]
        {
            get
            {
                if (columnName == (string)_columnName) return _column;
                else return null;
            }
            set
            {
            }
        }

    }
}
