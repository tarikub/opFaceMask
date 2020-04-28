namespace fnOpFaceMask.Simulator
{
    public class Locations
    {
        public Address[] Addresses;
    }

    public class Address
    {
        public string Address1 { get; set; }

        public string Address2 { get; set; }
        public string City { get; set; }

        public string State { get; set; }

        public string PostalCode { get; set; }

        public string FullAddress()
        {
            return $"{Address1} {Address2} {City} {State} , {PostalCode}";
        }
    }
}