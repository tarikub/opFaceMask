namespace FnOpFaceMask.Utils
{
    public class TwilioResponse
    {
        public string From { get; set; }

        public string To { get; set; }
        public string DateCreated { get; set; }
        public string Body { get; set; }
        public string MessageSid { get; set; }
        public string Medias { get; set; }
        public Operations Operation { get; set; }
    }
}