using System.ComponentModel;

namespace FnOpFaceMask.Utils
{
    public enum Operations
    {
        None,

        [Description("learn more options")]
        Help,

        [Description("start hosting an event drive")]
        Start,

        [Description("donate an item")]
        Donate,

        [Description("designate a drop off and pick up location")]
        DonateLocation,

        [Description("end a hosted event")]
        Close,

        [Description("receive a donated item")]
        Receive,
    }
}