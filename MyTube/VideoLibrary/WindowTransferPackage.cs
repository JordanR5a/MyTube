using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MyTube.Model;
using Windows.Storage;

namespace MyTube.VideoLibrary
{
    class WindowTransferPackage
    {
        public State State { get; set; }
        public Dictionary<string, object> Parameters { get; set; }

        public WindowTransferPackage(State state, Dictionary<string, object> parameters)
        {
            State = state;
            Parameters = parameters;
        }
    }
}