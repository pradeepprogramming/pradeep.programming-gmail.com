using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace getsolddata.Modal
{
    public class ModalLocation
    {
        public string MLS { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string Address { get; set; }
        public string Zip { get; set; }
        public string Country { get; internal set; }
        public string Id { get; internal set; }
    }
}
