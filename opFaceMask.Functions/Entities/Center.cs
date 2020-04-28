using Microsoft.WindowsAzure.Storage.Table;
using System;

namespace FnOpFaceMask
{
    public class Center : TableEntity
    {
        public string Id { set; get; }

        public string HostPhoneNumber { get; set; }

        public string DonationPhoneNumber { get; set; }

        public string DonationPhoneNumberSID { get; set; }

        public string Address { get; set; }

        public string Lat { get; set; }

        public string Lng { get; set; }

        public bool Active { get; set; } = true;

        public Center()
        {
            RowKey = Guid.NewGuid().ToString();
            PartitionKey = nameof(Center);
        }
    }
}